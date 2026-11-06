import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from universal_diagnostics import core
from universal_diagnostics.github import issue_draft_url

PROJECT = "sgennadi/DISM-GUI"


class CoreTests(unittest.TestCase):
    def test_secret_filter(self):
        source = "\n".join([
            "Authorization: Bearer abcdef", "password: abc", "Recovery Password: secret",
            "001122-334455-667788-990011-223344-556677-889900-112233",
            "https://server.example/?token=abcdef", r"C:\Users\bob\Desktop\secret.txt",
            r"\\server\share\support\test.log", "bob@company.com", "10.0.0.193",
            "S-1-5-21-12345-45678-78910", "12345678-1234-1234-1234-123456789abc",
            "ghp_012345678901234567890123456789", "unexpected exception: Error 87"
        ])
        result = core.redact(source, 10000)
        for secret in ["abcdef", "secret.txt", "bob@company.com", "10.0.0.193", "ghp_", "001122-", "112233", "78910"]:
            self.assertNotIn(secret, result)
        self.assertIn("Error 87", result)

    def test_no_excerpt_unless_opted_in(self):
        src = dict(project=PROJECT, component="WIM mount", summary="failed", excerpt="PRIVATE TRACE")
        ev = core.new_event(src)
        self.assertNotIn("excerpt", ev)
        ev2 = core.new_event(src, include_excerpt=True)
        self.assertIn("PRIVATE TRACE", ev2["excerpt"])

    def test_fingerprint_stable_without_reporter_identity(self):
        src = dict(project=PROJECT, component="Mount", error_type="DismFailure", code="87")
        a, b = core.new_event(src), core.new_event(src)
        self.assertEqual(a["fingerprint"], b["fingerprint"])
        self.assertNotEqual(a["id"], b["id"])
        for forbidden in ["hostname", "username", "email", "serial", "ip_address"]:
            self.assertNotIn(forbidden, a)

    def test_validation_rejects_path_traversal(self):
        for invalid in ["../../etc", "sgennadi/../test", "sgennadi//bad", "https://github.com/a/b", "a/b/c"]:
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                core.validate_project(invalid)

    def test_queue_preview_and_submission(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ev = core.capture(dict(project=PROJECT, component="DISM", summary="Error 87", code=87), root=root)
            record = core.load_record(PROJECT, ev["id"], root)
            self.assertEqual("pending", record["delivery"]["status"])
            report = core.report_markdown(ev)
            self.assertIn(ev["fingerprint"], report)
            self.assertIn(ev["id"], report)
            self.assertNotIn("hostname", report)
            core.mark_submitted(PROJECT, ev["id"], "https://github.com/sgennadi/DISM-GUI/issues/123", root)
            self.assertEqual("submitted", core.load_record(PROJECT, ev["id"], root)["delivery"]["status"])

    def test_cleanup_never_deletes_unsent_records(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            pending = core.capture(dict(project=PROJECT, component="a"), root=root)
            submitted = core.capture(dict(project=PROJECT, component="b"), root=root)
            core.mark_submitted(PROJECT, submitted["id"],
                                "https://github.com/sgennadi/DISM-GUI/issues/88", root)
            path = core.project_dir(PROJECT, root) / (submitted["id"] + ".json")
            rec = json.loads(path.read_text())
            rec["delivery"]["at_utc"] = "2020-01-01T00:00:00Z"
            path.write_text(json.dumps(rec))
            self.assertEqual(1, core.cleanup_submitted(PROJECT, root=root))
            self.assertEqual("pending", core.load_record(PROJECT, pending["id"], root)["delivery"]["status"])
            self.assertFalse(path.exists())

    def test_review_digest_changes_with_excerpt(self):
        e = core.new_event(dict(project=PROJECT, excerpt="Extra trace"), include_excerpt=True)
        self.assertNotEqual(core.report_digest(e), core.report_digest(e, with_excerpt=True))

    def test_untrusted_url_does_not_acknowledge(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ev = core.capture(dict(project=PROJECT), root=root)
            with self.assertRaises(Exception):
                core.mark_submitted(PROJECT, ev["id"], "https://example.com/hack", root)
            self.assertEqual("pending", core.load_record(PROJECT, ev["id"], root)["delivery"]["status"])

    def test_local_frequency_analysis(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = dict(project=PROJECT, component="mount", code="87")
            core.capture(src, root=root)
            core.capture(src, root=root)
            result = core.analyze_local(PROJECT, root=root)
            self.assertEqual(2, result[0]["count"])

    def test_draft_does_not_transmit(self):
        ev = core.new_event(dict(project=PROJECT))
        link = issue_draft_url(ev)
        self.assertTrue(link.startswith("https://github.com/sgennadi/DISM-GUI/issues/new?"))
        self.assertLess(len(link), 2500)

    def test_cli_requires_confirmation_and_token(self):
        with tempfile.TemporaryDirectory() as d:
            env = os.environ.copy()
            env["UDX_STATE_DIR"] = d
            env.pop("GH_TOKEN", None)
            payload = json.dumps(dict(project=PROJECT, component="Tester", code=19))
            recorded = subprocess.run([sys.executable, "-m", "universal_diagnostics", "capture-json"],
                                      input=payload, text=True, capture_output=True, env=env)
            self.assertEqual(0, recorded.returncode, recorded.stderr)
            id_ = json.loads(recorded.stdout)["id"]
            base = [sys.executable, "-m", "universal_diagnostics", "submit",
                    "--project", PROJECT, "--id", id_]
            rejected = subprocess.run(base, capture_output=True, text=True, env=env)
            self.assertEqual(1, rejected.returncode)
            rejected = subprocess.run(base + ["--approved"], capture_output=True, text=True, env=env)
            self.assertEqual(1, rejected.returncode)
            self.assertEqual("pending", core.load_record(PROJECT, id_, Path(d))["delivery"]["status"])

    def test_submit_requires_digest_of_exact_preview(self):
        from universal_diagnostics.cli import main
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ev = core.capture(dict(project=PROJECT, component="Test", code=87), root=root)
            base = ["submit", "--project", PROJECT, "--id", ev["id"], "--approved"]
            with patch.dict(os.environ, {"UDX_STATE_DIR": tmp, "GH_TOKEN": "dummy"}):
                with patch("universal_diagnostics.cli.submit_event") as transport:
                    self.assertEqual(1, main(base + ["--review-sha256", "not-a-valid-digest"]))
                    transport.assert_not_called()
                    transport.return_value = "https://github.com/sgennadi/DISM-GUI/issues/145"
                    self.assertEqual(0, main(base + ["--review-sha256", core.report_digest(ev)]))
                    transport.assert_called_once()
                    self.assertEqual("submitted", core.load_record(PROJECT, ev["id"], root)["delivery"]["status"])

    def test_reject_unbounded_event(self):
        with self.assertRaises(ValueError):
            core.new_event(dict(project=PROJECT, summary={"private": "no"}))


if __name__ == "__main__":
    unittest.main()
