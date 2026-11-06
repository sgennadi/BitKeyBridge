import unittest
from unittest.mock import patch

from universal_diagnostics.core import new_event
from universal_diagnostics.github import (GitHubError, find_issue_by_marker,
                                          submit_event, verify_issue_url)
from universal_diagnostics import ci_review

PROJECT = "sgennadi/ISO-WCX"


class GithubTests(unittest.TestCase):
    def test_dedup_avoids_post(self):
        event = new_event(dict(project=PROJECT))
        url = "https://github.com/sgennadi/ISO-WCX/issues/23"
        with patch("universal_diagnostics.github.request_json") as request:
            request.return_value = [{"body": "<!-- udx-report-id:" + event["id"] + " -->",
                                     "html_url": url}]
            self.assertEqual(url, submit_event(event, "fake-token"))
            self.assertEqual(1, request.call_count)
            self.assertEqual("GET", request.call_args.args[0])

    def test_post_requires_valid_issue_confirmation(self):
        event = new_event(dict(project=PROJECT))
        with patch("universal_diagnostics.github.request_json") as request:
            request.side_effect = [[], {"html_url": "https://attacker.example/fake"}]
            with self.assertRaises(GitHubError):
                submit_event(event, "fake-token")
            self.assertEqual("POST", request.call_args.args[0])

    def test_failed_precheck_keeps_post_closed(self):
        event = new_event(dict(project=PROJECT))
        with patch("universal_diagnostics.github.request_json") as request:
            request.side_effect = GitHubError("rate limit")
            with self.assertRaises(GitHubError):
                submit_event(event, "fake-token")
            self.assertEqual(1, request.call_count)

    def test_issue_url_validation(self):
        for invalid in ["http://github.com/sgennadi/ISO-WCX/issues/7",
                        "https://github.com/sgennadi/NOT-ISO-WCX/issues/7",
                        "https://github.com/sgennadi/ISO-WCX/issues/7?admin=1"]:
            with self.subTest(invalid=invalid), self.assertRaises(GitHubError):
                verify_issue_url(PROJECT, invalid)

    def test_ci_repeat_count_is_per_run(self):
        runs = [{"id": 101, "created_at": "2099-01-01T00:00:00Z"},
                {"id": 102, "created_at": "2099-01-02T00:00:00Z"}]
        jobs = {"jobs": [{"conclusion": "failure", "steps": [
            {"name": "Compile C++", "conclusion": "failure"},
            {"name": "Build", "conclusion": "failure"}]}]}
        with patch("universal_diagnostics.ci_review.request_json", side_effect=[
            {"workflow_runs": runs}, jobs, jobs
        ]):
            result = ci_review.get_failure_counts(PROJECT, "fake", "build.yml")
        self.assertEqual(2, result["Compilation"])

    def test_ci_no_secrets_in_report(self):
        content = ci_review.render_summary({"Compilation": 3}, 14)
        self.assertIn("Compilation", content)
        self.assertNotIn("machine", content.lower().split("source paths")[0])
        self.assertIn("No raw logs", content)

    def test_ci_upsert_existing_issue(self):
        issues = [{"number": 17, "title": ci_review.TITLE,
                   "body": ci_review.MARKER + " old",
                   "html_url": "https://github.com/sgennadi/ISO-WCX/issues/17"}]
        with patch("universal_diagnostics.ci_review.request_json", side_effect=[issues, {}]) as req:
            result = ci_review.upsert_issue(PROJECT, "fake", ci_review.MARKER + " new")
            self.assertTrue(result.endswith("/17"))
            self.assertEqual("PATCH", req.call_args.args[0])

    def test_category_sanitizes_step_name(self):
        self.assertEqual("Other CI step", ci_review.category_for_step("\\\\secret\u005cshare user bob@corp"))
        self.assertEqual("Automated tests", ci_review.category_for_step("Execute unit tests"))


if __name__ == "__main__":
    unittest.main()
