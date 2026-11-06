"""Opt-in direct GitHub Issues transport; does not store any GitHub token."""
from __future__ import annotations

import json
import re
import urllib.error
import urllib.parse
import urllib.request
from typing import Any

from .core import report_markdown, report_title, validate_project

API = "https://api.github.com"


class GitHubError(Exception):
    pass


def verify_issue_url(project: str, url: str) -> str:
    validate_project(project)
    owner, repo = project.split("/")
    if not re.fullmatch(r"https://github\.com/" + re.escape(owner) + r"/" +
                        re.escape(repo) + r"/issues/[1-9][0-9]*", url, flags=re.I):
        raise GitHubError("GitHub did not confirm an issue URL for this repository")
    return url


def request_json(method: str, path: str, token: str, payload: dict[str, Any] | None = None) -> Any:
    if not token or "\n" in token or "\r" in token:
        raise GitHubError("GH_TOKEN is missing or invalid")
    url = API + path
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(
        url, data=body, method=method,
        headers={
            "Authorization": "Bearer " + token,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "UniversalDiagnostics/0.1.0-rc.1",
            **({"Content-Type": "application/json"} if body else {}),
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=20) as response:
            if response.status < 200 or response.status >= 300:
                raise GitHubError("GitHub returned HTTP " + str(response.status))
            return json.load(response)
    except urllib.error.HTTPError as exc:
        # Never echo an API response: it could reflect unredacted input.
        raise GitHubError("GitHub HTTP " + str(exc.code) + "; check repo, Issues permissions and rate limits") from None
    except (urllib.error.URLError, TimeoutError) as exc:
        # A timeout after POST is ambiguous; caller keeps its pending record.
        raise GitHubError("GitHub request failed; report is still retained locally") from None


def find_issue_by_marker(project: str, event_id: str, token: str) -> str | None:
    """Avoid retries duplicating reports: scan 500 latest issues, both states.

    Returns None only on completed scan. Failure prevents creating a new Issue.
    """
    validate_project(project)
    marker = "<!-- udx-report-id:" + event_id + " -->"
    for page in range(1, 6):
        issues = request_json("GET", f"/repos/{project}/issues?state=all&per_page=100&page={page}", token)
        if not isinstance(issues, list):
            raise GitHubError("Unexpected GitHub Issues response")
        for issue in issues:
            if issue.get("pull_request"):
                continue
            if marker in (issue.get("body") or ""):
                return verify_issue_url(project, issue.get("html_url", ""))
        if len(issues) < 100:
            return None
    # In very busy repos a report could be on a later page.
    raise GitHubError("Cannot exclude duplicate Issue after scanning 500 Issues; inspect manually")


def submit_event(event: dict[str, Any], token: str, *, with_excerpt: bool = False) -> str:
    project = validate_project(event["project"])
    previous = find_issue_by_marker(project, event["id"], token)
    if previous:
        return previous
    response = request_json("POST", f"/repos/{project}/issues", token, {
        "title": report_title(event), "body": report_markdown(event, with_excerpt=with_excerpt)
    })
    if not isinstance(response, dict):
        raise GitHubError("Unexpected GitHub response; report still retained locally")
    return verify_issue_url(project, response.get("html_url", ""))


def issue_draft_url(event: dict[str, Any]) -> str:
    """Small browser-prefilled draft. Opening this never marks an event sent."""
    project = validate_project(event["project"])
    # Long issue URLs can be rejected by browsers/proxies. User can copy full
    # report from preview and paste into the Issue editor if needed.
    body = report_markdown(event, with_excerpt=False)[:850]
    return f"https://github.com/{project}/issues/new?" + urllib.parse.urlencode({
        "title": report_title(event), "body": body
    })
