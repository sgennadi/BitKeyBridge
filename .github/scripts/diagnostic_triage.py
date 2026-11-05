#!/usr/bin/env python3
"""Analyze strictly allowlisted BitKeyBridge diagnostic GitHub Issues.

No raw logs, messages, paths, host/user/device identifiers, stack traces,
credentials, issue body text, or issue authors enter the resulting report.
This runs read-only during prebuild and at scheduled intervals.
"""
import argparse
import collections
import datetime as dt
import json
import os
import re
import sys
import urllib.error
import urllib.request

PREFIX = "[BKB-DIAG/v1]"
SCHEMA = "BKB-DIAG/v1"
COMPONENTS = frozenset(("Startup", "Devices", "Graph", "BitLocker", "LAPS", "Update", "UI", "Helpdesk", "Other"))
FAILURES = frozenset(("AccessDenied", "Timeout", "Network", "NotFound", "InvalidConfig", "Unexpected", "IO"))
EXCEPTIONS = frozenset(("UnauthorizedAccessException", "SecurityException", "TimeoutException",
    "HttpRequestException", "IOException", "FileNotFoundException", "DirectoryNotFoundException",
    "ArgumentException", "InvalidOperationException", "Win32Exception", "Other"))
ARCHES = frozenset(("X64", "X86", "Arm64"))
SAFE_KEYS = frozenset(("component", "failure", "exceptionType", "hresult", "count", "fingerprint",
                       "firstDay", "lastDay"))
TOP_KEYS = frozenset(("schema", "product", "version", "architecture", "buckets", "privacy"))
FINGERPRINT_RE = re.compile(r"^[0-9a-f]{16}$")
HRESULT_RE = re.compile(r"^0x[0-9A-F]{8}$")
VERSION_RE = re.compile(r"^[0-9]{1,3}[.][0-9]{1,3}[.][0-9]{1,3}(?:-rc[.][0-9]{1,3})?$")
DAY_RE = re.compile(r"^20[0-9]{2}-[01][0-9]-[0-3][0-9]$")
TITLE_RE = re.compile(r"^\\[BKB-DIAG/v1\\] 20[0-9]{2}-[01][0-9]-[0-3][0-9] [0-9a-f]{12}$")

GUIDANCE = {
    "AccessDenied": "Check approved RBAC, Graph consent and protected audit policy. Do not bypass authorization.",
    "Timeout": "Inspect AD connectivity and Graph throttling/source deadline; retain partial-source diagnostics.",
    "Network": "Check supported Graph/DC endpoints, DNS and conditional access without changing security controls.",
    "InvalidConfig": "Verify tenant/client configuration and supported options; never request passwords in Issues.",
    "NotFound": "Verify exact source-qualified identity and the selected scope before concluding absence.",
    "IO": "Check user-profile storage permissions and endpoint file locks; preserve rollback.",
    "Unexpected": "Reproduce in a non-production environment and use only typed non-secret event codes."
}

def strict_issue(issue):
    """Return a list of validated, minimal issue counters or None."""
    if not isinstance(issue, dict) or "pull_request" in issue:
        return None
    title = issue.get("title")
    body = issue.get("body")
    if not isinstance(title, str) or not TITLE_RE.fullmatch(title):
        return None
    if not isinstance(body, str) or len(body) > 12000:
        return None
    if not body.startswith("```json\n") or not body.rstrip().endswith("\n```"):
        return None
    try:
        document = json.loads(body[len("```json\n"):-len("\n```")].strip())
    except (ValueError, TypeError):
        return None
    if not isinstance(document, dict) or set(document) != TOP_KEYS:
        return None
    if document.get("schema") != SCHEMA or document.get("product") != "BitKeyBridge":
        return None
    if document.get("privacy") != "allowlisted-error-counters-only":
        return None
    if not isinstance(document.get("version"), str) or not VERSION_RE.fullmatch(document["version"]):
        return None
    if document.get("architecture") not in ARCHES:
        return None
    buckets = document.get("buckets")
    if not isinstance(buckets, list) or not 1 <= len(buckets) <= 20:
        return None
    parsed = []
    for item in buckets:
        if not isinstance(item, dict) or set(item) != SAFE_KEYS:
            return None
        if (item["component"] not in COMPONENTS or item["failure"] not in FAILURES or
                item["exceptionType"] not in EXCEPTIONS or
                not isinstance(item["hresult"], str) or not HRESULT_RE.fullmatch(item["hresult"]) or
                type(item["count"]) is not int or not 1 <= item["count"] <= 10000 or
                not isinstance(item["fingerprint"], str) or
                not FINGERPRINT_RE.fullmatch(item["fingerprint"]) or
                any(not isinstance(item[name], str) or not DAY_RE.fullmatch(item[name])
                    for name in ("firstDay", "lastDay"))):
            return None
        parsed.append({key: item[key] for key in ("fingerprint", "component", "failure", "exceptionType", "count")})
    return parsed


def analyze(issues):
    grouped = {}
    accepted = rejected = 0
    for issue in issues:
        if not isinstance(issue, dict) or not isinstance(issue.get("title"), str) or not issue["title"].startswith(PREFIX):
            continue
        rows = strict_issue(issue)
        if rows is None:
            rejected += 1
            continue
        accepted += 1
        for row in rows:
            fp = row["fingerprint"]
            if fp not in grouped:
                grouped[fp] = {k: row[k] for k in ("fingerprint", "component", "failure", "exceptionType")}
                grouped[fp]["count"] = 0
                grouped[fp]["issues"] = 0
            if (grouped[fp]["component"], grouped[fp]["failure"], grouped[fp]["exceptionType"]) != (
                    row["component"], row["failure"], row["exceptionType"]):
                rejected += 1
                continue
            grouped[fp]["count"] += row["count"]
            grouped[fp]["issues"] += 1
    ordered = sorted(grouped.values(), key=lambda x: (-x["count"], x["component"], x["fingerprint"]))
    return {"schema": "BKB-TRIAGE/v1", "acceptedIssues": accepted, "rejectedDiagnosticIssues": rejected,
            "fingerprints": len(ordered), "totalOccurrences": sum(x["count"] for x in ordered),
            "groups": ordered[:75]}


def fetch_issues(repository, token):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Invalid repository")
    result = []
    for page in range(1, 6):
        url = "https://api.github.com/repos/" + repository + "/issues?state=all&per_page=100&page=" + str(page)
        headers = {"Accept": "application/vnd.github+json", "User-Agent": "BitKeyBridge-DiagnosticTriage/1"}
        if token:
            headers["Authorization"] = "Bearer " + token
        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req, timeout=12) as response:
            chunk = json.load(response)
        if not isinstance(chunk, list):
            raise ValueError("Unexpected GitHub response")
        result.extend(chunk)
        if len(chunk) < 100:
            break
    return result


def markdown(summary, note):
    lines = ["## BitKeyBridge diagnostic analysis", "",
             "Read-only, allowlisted issue-code analysis; no user/device/log data.",
             "", "- Accepted diagnostic issues: **" + str(summary["acceptedIssues"]) + "**",
             "- Rejected or malformed diagnostic issues: **" + str(summary["rejectedDiagnosticIssues"]) + "**",
             "- Aggregated error occurrences: **" + str(summary["totalOccurrences"]) + "**",
             "- Unique error fingerprints: **" + str(summary["fingerprints"]) + "**", ""]
    if note:
        lines += ["Analysis unavailable: " + note, ""]
    lines += ["| Component | Failure | Exception | Count | Priority |",
              "|---|---|---|---:|---|"]
    for row in summary["groups"][:30]:
        priority = "Review" if row["count"] >= 3 else "Observe"
        lines.append("| " + row["component"] + " | " + row["failure"] + " | " +
                     row["exceptionType"] + " | " + str(row["count"]) + " | " + priority + " |")
    lines += ["", "### Automated next checks", ""]
    for failure in sorted(set(row["failure"] for row in summary["groups"])):
        lines.append("- **" + failure + "**: " + GUIDANCE[failure])
    if not summary["groups"]:
        lines.append("No valid diagnostic batches are available yet.")
    lines += ["", "**Privacy:** Never paste raw support bundles, event text, domain names or recovery secrets into Issues.",
              "**Limitation:** These are heuristics, not verified root causes or authorization decisions."]
    return "\n".join(lines) + "\n"


def self_test():
    item = {"component": "Devices", "failure": "Timeout", "exceptionType": "TimeoutException",
            "hresult": "0x80131505", "count": 3, "fingerprint": "a" * 16,
            "firstDay": "2026-11-05", "lastDay": "2026-11-05"}
    data = {"schema": SCHEMA, "product": "BitKeyBridge", "version": "0.35.0-rc.6",
            "architecture": "X64", "buckets": [item], "privacy": "allowlisted-error-counters-only"}
    title = PREFIX + " 2026-11-05 " + "b" * 12
    issue = {"title": title, "body": "```json\n" + json.dumps(data) + "\n```"}
    assert strict_issue(issue)[0]["count"] == 3
    assert analyze([issue, issue])["totalOccurrences"] == 6
    altered = json.loads(json.dumps(data))
    altered["buckets"][0]["deviceName"] = "secret-device.example"
    assert strict_issue({"title": title, "body": "```json\n" + json.dumps(altered) + "\n```"}) is None
    altered = json.loads(json.dumps(data))
    altered["buckets"][0]["count"] = 100001
    assert strict_issue({"title": title, "body": "```json\n" + json.dumps(altered) + "\n```"}) is None
    assert strict_issue({"title": title, "body": "C:\\Users\\private secret token=secret"}) is None
    output = markdown(analyze([issue]), "")
    assert "secret-device" not in output and "C:\\Users" not in output and "Timeout" in output


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--repo")
    ap.add_argument("--summary")
    ap.add_argument("--json-out")
    args = ap.parse_args()
    if args.self_test:
        self_test()
        print("Diagnostic schema, privacy rejection and aggregation self-tests passed.")
        return 0
    if not args.repo:
        ap.error("--repo is required when not self-testing")
    note = ""
    try:
        issues = fetch_issues(args.repo, os.environ.get("GH_TOKEN", ""))
        result = analyze(issues)
    except (ValueError, urllib.error.URLError, TimeoutError, OSError):
        note = "GitHub Issues read unavailable; existing build and security gates remain authoritative."
        result = analyze([])
    if args.summary:
        with open(args.summary, "a", encoding="utf-8") as f:
            f.write(markdown(result, note))
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as f:
            json.dump(result, f, indent=2)
    print("Diagnostic triage: " + str(result["acceptedIssues"]) +
          " accepted issues, " + str(result["fingerprints"]) + " unique fingerprints. " + note)
    return 0


if __name__ == "__main__":
    sys.exit(main())
