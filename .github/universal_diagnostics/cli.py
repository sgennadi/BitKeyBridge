"""CLI surface. JSON over standard input keeps data off process command lines."""
from __future__ import annotations

import argparse
import json
import os
import sys
import webbrowser
from .core import (MAX_EVENT_BYTES, capture, list_records, load_record,
                   analyze_local, cleanup_submitted, mark_submitted, report_markdown, report_digest)
from .github import GitHubError, issue_draft_url, submit_event

VERSION = "0.1.0-rc.1"


def arguments() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="udx", description="Local privacy-first project diagnostics")
    p.add_argument("--version", action="version", version=VERSION)
    commands = p.add_subparsers(dest="command", required=True)
    cap = commands.add_parser("capture-json", help="Read a JSON event from stdin and queue locally")
    cap.add_argument("--include-excerpt", action="store_true", help="Explicitly include a sanitized text excerpt")
    listing = commands.add_parser("list", help="Show pending and sent event metadata")
    listing.add_argument("--project", required=True)
    review = commands.add_parser("preview", help="Print the exact GitHub Issue body for review")
    review.add_argument("--project", required=True)
    review.add_argument("--id", required=True)
    review.add_argument("--include-excerpt", action="store_true")
    draft = commands.add_parser("draft", help="Print or open a prefilled browser Issue (not submitted)")
    draft.add_argument("--project", required=True)
    draft.add_argument("--id", required=True)
    draft.add_argument("--open", action="store_true", help="Open user's browser")
    send = commands.add_parser("submit", help="Explicit GitHub API submit using env GH_TOKEN")
    send.add_argument("--project", required=True)
    send.add_argument("--id", required=True)
    send.add_argument("--approved", action="store_true", help="Confirm preview was personally reviewed")
    send.add_argument("--review-sha256", required=False, help="SHA-256 printed by the matching preview")
    send.add_argument("--include-excerpt", action="store_true")
    summary = commands.add_parser("analyze", help="Count repeated local error signatures")
    summary.add_argument("--project", required=True)
    summary.add_argument("--days", type=int, default=14)
    cleanup = commands.add_parser("cleanup", help="Remove only acknowledged reports older than a retention period")
    cleanup.add_argument("--project", required=True)
    cleanup.add_argument("--days", type=int, default=14)
    return p


def read_stdin_json() -> dict:
    data = sys.stdin.buffer.read(MAX_EVENT_BYTES + 1)
    if len(data) > MAX_EVENT_BYTES:
        raise ValueError("Input JSON exceeds 64 KiB")
    value = json.loads(data.decode("utf-8"))
    if not isinstance(value, dict):
        raise ValueError("Expected one JSON object")
    return value


def main(argv: list[str] | None = None) -> int:
    args = arguments().parse_args(argv)
    try:
        if args.command == "capture-json":
            event = capture(read_stdin_json(), include_excerpt=args.include_excerpt)
            print(json.dumps({"status": "queued", "id": event["id"],
                              "fingerprint": event["fingerprint"]}))
            return 0
        if args.command == "list":
            for record in list_records(args.project):
                ev = record["event"]
                print(json.dumps({"id": ev["id"], "time": ev["timestamp_utc"],
                                  "component": ev["component"], "code": ev["code"],
                                  "fingerprint": ev["fingerprint"][:12],
                                  "status": record["delivery"]["status"]}))
            return 0
        if args.command == "cleanup":
            print(json.dumps({"submitted_records_removed": cleanup_submitted(args.project, args.days)}))
            return 0
        if args.command == "analyze":
            if args.days < 1 or args.days > 365:
                raise ValueError("--days must be between 1 and 365")
            print(json.dumps(analyze_local(args.project, args.days), indent=2))
            return 0
        record = load_record(args.project, args.id)
        ev = record["event"]
        if args.command == "preview":
            print(report_markdown(ev, with_excerpt=args.include_excerpt))
            print("\nReview-SHA256: " + report_digest(ev, with_excerpt=args.include_excerpt))
            return 0
        if args.command == "draft":
            url = issue_draft_url(ev)
            print(url)
            if args.open:
                webbrowser.open(url)
            return 0
        if args.command == "submit":
            if not args.approved or args.review_sha256 != report_digest(ev, with_excerpt=args.include_excerpt):
                raise ValueError("Submission blocked: preview the exact report and pass --approved --review-sha256 <digest>")
            if record.get("delivery", {}).get("status") == "submitted":
                print(json.dumps({"status": "already_submitted", "url": record["delivery"]["url"]}))
                return 0
            token = os.getenv("GH_TOKEN", "")
            if not token:
                raise GitHubError("GH_TOKEN not set; use 'draft --open' for user-authenticated submission")
            url = submit_event(ev, token, with_excerpt=args.include_excerpt)
            mark_submitted(args.project, args.id, url)
            print(json.dumps({"status": "submitted", "url": url}))
            return 0
        return 2
    except (ValueError, OSError, KeyError, json.JSONDecodeError, GitHubError) as exc:
        # Never display user-supplied text or raw HTTP responses on failures.
        if isinstance(exc, (ValueError, GitHubError)):
            # Errors from validations are generated locally, never raw payloads.
            print("Error: " + str(exc), file=sys.stderr)
        else:
            print("Error: diagnostic operation failed; no data was uploaded", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
