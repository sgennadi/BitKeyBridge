"""Summarize recurring application diagnostic Issues across one repository.

Only reads explicit fingerprint tokens, timestamps and numeric issue IDs;
never publishes issue bodies, raw logs, user information or excerpts.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from datetime import datetime, timedelta, timezone
import os
import re

from .core import validate_project
from .github import GitHubError, request_json, verify_issue_url

TITLE = "[Diagnostics Review] Recurring runtime errors"
MARKER = "<!-- universal-diagnostics-runtime-review-v1 -->"
FINGERPRINT = re.compile(r"(?im)^\s*(?:-\s*)?Diagnostic-Fingerprint\s*:\s*`?([a-f0-9]{16,64})`?\s*$")
SCHEMA_MARKERS = ("<!-- udx-report-id:", "## GPO Settings Explorer - opt-in diagnostics")


def collect(project: str, token: str, days: int = 30) -> tuple[Counter, dict[str, list[int]]]:
    validate_project(project)
    if days < 1 or days > 365:
        raise ValueError("days must be between 1 and 365")
    cutoff = datetime.now(timezone.utc) - timedelta(days=days)
    counts: Counter = Counter()
    samples: dict[str, list[int]] = defaultdict(list)
    for page in range(1, 6):
        issues = request_json("GET", f"/repos/{project}/issues?state=all&per_page=100&page={page}", token)
        if not isinstance(issues, list):
            raise GitHubError("Unexpected Issues response")
        for item in issues:
            if item.get("pull_request"):
                continue
            body = item.get("body") or ""
            if not any(marker in body for marker in SCHEMA_MARKERS):
                continue
            found = FINGERPRINT.search(body)
            if not found:
                continue
            try:
                created = datetime.fromisoformat(str(item["created_at"]).replace("Z", "+00:00"))
                number = int(item["number"])
                if created < cutoff or number < 1:
                    continue
            except (KeyError, TypeError, ValueError):
                continue
            sig = found.group(1).lower()
            counts[sig] += 1
            if len(samples[sig]) < 5:
                samples[sig].append(number)
        if len(issues) < 100:
            return counts, samples
    raise GitHubError("Issue scan exceeded 500 records; use a narrower repository review")


def summarize(counts: Counter, samples: dict[str, list[int]], days: int) -> str:
    lines = [MARKER, f"## Repeated runtime diagnostic fingerprints ({days} days)", "",
             "Only fingerprints and GitHub Issue numbers were analyzed.",
             "No submitted excerpts, user names, stack traces or device data are republished.", ""]
    repeated = [(sig, n) for sig, n in counts.most_common() if n >= 2]
    if not repeated:
        lines.append("No fingerprint repeated in separate diagnostic Issues.")
    else:
        lines.extend(["| Fingerprint | Issues | Examples |", "|---|---:|---|"])
        for sig, n in repeated[:30]:
            refs = ", ".join("#" + str(i) for i in samples[sig][:5])
            lines.append(f"| `{sig[:16]}` | {n} | {refs} |")
    lines.extend(["", "Investigate each repeated fingerprint, add a regression test, and close the original diagnostic Issues once fixed."])
    return "\n".join(lines)


def upsert(project: str, token: str, body: str) -> str:
    issues = request_json("GET", f"/repos/{project}/issues?state=open&per_page=100", token)
    if not isinstance(issues, list):
        raise GitHubError("Unexpected Issues response")
    for item in issues:
        if item.get("pull_request"):
            continue
        if item.get("title") == TITLE and MARKER in (item.get("body") or ""):
            url = verify_issue_url(project, item.get("html_url", ""))
            if item.get("body") != body:
                request_json("PATCH", f"/repos/{project}/issues/{int(item['number'])}", token, {"body": body})
            return url
    if len(issues) == 100:
        # Do not create a duplicate if a matching Issue might be on page 2.
        raise GitHubError("Too many open Issues to deduplicate this summary")
    result = request_json("POST", f"/repos/{project}/issues", token, {"title": TITLE, "body": body})
    if not isinstance(result, dict):
        raise GitHubError("Unexpected issue creation response")
    return verify_issue_url(project, result.get("html_url", ""))


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description="Repeated error analysis from GitHub Issues")
    p.add_argument("--days", type=int, default=30)
    p.add_argument("--mode", choices=("prebuild", "review"), default="prebuild")
    args = p.parse_args(argv)
    project = os.environ.get("GITHUB_REPOSITORY", "")
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN") or ""
    try:
        counts, examples = collect(project, token, days=args.days)
        text = summarize(counts, examples, args.days)
        print(text)
        if os.getenv("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as f:
                f.write(text + "\n")
        if args.mode == "review" and any(n >= 2 for n in counts.values()):
            print("Runtime diagnostics summary: " + upsert(project, token, text))
        return 0
    except (GitHubError, ValueError, OSError) as exc:
        print("Runtime diagnostics review unavailable: " + str(exc))
        return 0 if args.mode == "prebuild" else 1


if __name__ == "__main__":
    raise SystemExit(main())
