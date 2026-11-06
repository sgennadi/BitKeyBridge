"""GitHub Actions failure review without ever downloading raw build logs.

Intended for trusted default-branch workflow_run/schedule workflows. A limited
pre-build mode prints a diagnostic summary and never writes GitHub Issues.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone, timedelta
import os
from pathlib import Path
import re

from .core import validate_project
from .github import GitHubError, request_json, verify_issue_url

TITLE = "[CI Diagnostics] Recurring build failures"
MARKER = "<!-- universal-diagnostics-ci-v1 -->"
CATEGORIES = (
    ("Dependency restore", r"\b(restore|nuget|dependencies|dependency|packages)\b"),
    ("Compilation", r"\b(build|compile|msbuild|dotnet|cmake|compiler)\b"),
    ("Automated tests", r"\b(test|smoke|regression|unit|integration)\b"),
    ("Packaging", r"\b(package|publish|artifact|bundle|zip|pyinstaller)\b"),
    ("Release publishing", r"\b(release|sign|deploy|attest)\b"),
    ("Quality/security", r"\b(lint|codeql|security|audit|format|analysis|prebuild)\b"),
)
ADVICE = {
    "Dependency restore": "Verify pinned dependencies and SDK availability.",
    "Compilation": "Reproduce the build and add a focused compiler regression test.",
    "Automated tests": "Inspect the failing test in Actions and add a regression fixture.",
    "Packaging": "Verify output paths, architecture and artifact checks.",
    "Release publishing": "Review branch/tag gates, permissions and signing preconditions.",
    "Quality/security": "Inspect the quality gate and fix the underlying finding.",
    "Other CI step": "Inspect the failing workflow step and add a targeted test.",
}
WORKFLOW_RE = re.compile(r"^[A-Za-z0-9_.-]+\.ya?ml$")


def category_for_step(name: str) -> str:
    # Only published categories, no raw step names or failing command output.
    for category, pattern in CATEGORIES:
        if re.search(pattern, name, re.I):
            return category
    return "Other CI step"


def get_failure_counts(project: str, token: str, workflow: str = "build.yml",
                       days: int = 14, limit: int = 25) -> Counter:
    validate_project(project)
    if not WORKFLOW_RE.fullmatch(workflow):
        raise ValueError("Workflow must be a YAML filename, e.g. build.yml")
    if days < 1 or days > 90:
        raise ValueError("days must be 1-90")
    if limit < 1 or limit > 50:
        raise ValueError("limit must be 1-50")
    from urllib.parse import quote
    response = request_json("GET", f"/repos/{project}/actions/workflows/{quote(workflow)}/runs?status=failure&per_page=70", token)
    if not isinstance(response, dict):
        raise GitHubError("Unexpected workflow response")
    now = datetime.now(timezone.utc)
    cutoff = now - timedelta(days=days)
    counter: Counter = Counter()
    examined = 0
    for run in response.get("workflow_runs", []):
        if examined >= limit:
            break
        try:
            created = datetime.fromisoformat(str(run["created_at"]).replace("Z", "+00:00"))
            run_id = int(run["id"])
            if created < cutoff:
                continue
        except (KeyError, ValueError, TypeError):
            continue
        examined += 1
        jobs = request_json("GET", f"/repos/{project}/actions/runs/{run_id}/jobs?per_page=100", token)
        if not isinstance(jobs, dict):
            continue
        categories = set()
        for job in jobs.get("jobs", []):
            if job.get("conclusion") != "failure":
                continue
            for step in job.get("steps", []):
                if step.get("conclusion") == "failure":
                    categories.add(category_for_step(str(step.get("name", ""))))
            if not categories:
                categories.add("Other CI step")
        for category in categories:
            counter[category] += 1
    return counter


def render_summary(counts: Counter, days: int, *, threshold: int = 2) -> str:
    repeated = [(cat, num) for cat, num in Counter(counts).most_common() if num >= threshold]
    lines = [MARKER, f"## Recurring CI failures over the last {days} days", "",
             "Analysis uses **workflow and job metadata only**. No raw logs,",
             "source paths, machine names or credentials are published.", ""]
    if not repeated:
        lines.append("No category repeated across separate failed workflow runs.")
    else:
        lines.extend(["| Failure category | Failed runs | Suggested next step |",
                      "|---|---:|---|"])
        for name, num in repeated:
            lines.append(f"| {name} | {num} | {ADVICE[name]} |")
    lines.extend(["", "Resolve the cause, add a regression test and rerun the original workflow."])
    return "\n".join(lines)


def upsert_issue(project: str, token: str, text: str) -> str:
    # Single stable issue: no issue flood on subsequent scheduled runs.
    existing = None
    for page in range(1, 6):
        issues = request_json("GET", f"/repos/{project}/issues?state=open&per_page=100&page={page}", token)
        if not isinstance(issues, list):
            raise GitHubError("Unexpected GitHub Issues response")
        for item in issues:
            if item.get("pull_request"):
                continue
            if item.get("title") == TITLE and MARKER in (item.get("body") or ""):
                existing = item
                break
        if existing or len(issues) < 100:
            break
    if existing:
        url = verify_issue_url(project, existing.get("html_url", ""))
        if existing.get("body") != text:
            request_json("PATCH", f"/repos/{project}/issues/{int(existing['number'])}", token, {"body": text})
        return url
    result = request_json("POST", f"/repos/{project}/issues", token, {"title": TITLE, "body": text})
    if not isinstance(result, dict):
        raise GitHubError("Unexpected issue creation response")
    return verify_issue_url(project, result.get("html_url", ""))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Analyze recurring GitHub Actions failures")
    parser.add_argument("--workflow", default="build.yml")
    parser.add_argument("--days", type=int, default=14)
    parser.add_argument("--mode", choices=("prebuild", "review"), default="prebuild")
    parser.add_argument("--fail-on-recurring", action="store_true", help="Only relevant for prebuild")
    args = parser.parse_args(argv)
    project = os.environ.get("GITHUB_REPOSITORY", "")
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN") or ""
    try:
        counts = get_failure_counts(project, token, args.workflow, args.days)
        body = render_summary(counts, args.days)
        print(body)
        summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
        if summary_path:
            with Path(summary_path).open("a", encoding="utf-8") as stream:
                stream.write(body + "\n")
        if args.mode == "review" and any(value >= 2 for value in counts.values()):
            print("Updated CI diagnostics: " + upsert_issue(project, token, body))
        if args.mode == "prebuild" and args.fail_on_recurring and any(n >= 2 for n in counts.values()):
            return 3
        return 0
    except (ValueError, OSError, GitHubError) as exc:
        # Read-only prebuild is advisory: avoid blocking ordinary compilation if
        # GitHub Actions is unavailable or a token has insufficient privileges.
        print("CI diagnostics unavailable: " + str(exc))
        return 0 if args.mode == "prebuild" else 1


if __name__ == "__main__":
    raise SystemExit(main())
