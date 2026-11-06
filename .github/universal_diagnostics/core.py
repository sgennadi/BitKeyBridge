"""Privacy-first, local diagnostic events shared by multiple projects.

Events are deliberately not uploaded by this module. Transport is opt-in.
"""
from __future__ import annotations

import hashlib
import json
import os
import platform
import re
import tempfile
import uuid
from collections import Counter
from datetime import datetime, timezone, timedelta
from pathlib import Path
from typing import Any

SCHEMA = "universal-diagnostics/v1"
MAX_EVENT_BYTES = 65_536
MAX_EXCERPT = 2_000
MAX_REPORT = 8_000
PROJECT_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]{0,38}/[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$")

# Apply selective removal before replacing paths and identifiers. These rules
# minimize accidental publication, not a substitute for human review.
_SECRET_LINE = re.compile(
    r"(?im)^.*\b(?:password|passwd|passphrase|pwd|recovery[\s_-]*(?:key|password)|"
    r"bitlocker[\s_-]*(?:key|password)|laps[\s_-]*password|access[\s_-]*token|"
    r"refresh[\s_-]*token|auth(?:entication|orization)?[\s_-]*header|"
    r"authorization|bearer|client[\s_-]*secret|api[\s_-]*key|"
    r"private[\s_-]*key|secret[\s_-]*key|github_pat_|gh[pousr]_)\b.*$"
)
_PATTERNS = [
    (re.compile(r"(?i)\b\d{6}(?:-\d{6}){7}\b"), "[BITLOCKER-KEY]"),
    (re.compile(r"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b"), "[JWT]"),
    (re.compile(r"(?i)\b(?:github_pat_[A-Za-z0-9_]{15,}|gh[pousr]_[A-Za-z0-9_]{15,})\b"), "[TOKEN]"),
    (re.compile(r"(?i)https?://[^\s<>\"']+"), "[URL]"),
    (re.compile(r"(?i)\\\\[^\s\r\n<>\"']+"), "[NETWORK-PATH]"),
    (re.compile(r"(?i)(?<![A-Za-z0-9])(?:[A-Z]:\\)[^\s\r\n\"']+"), "[WINDOWS-PATH]"),
    (re.compile(r"(?i)(?:/home/|/Users/|/root/)[^\s\r\n\"']+"), "[USER-PATH]"),
    (re.compile(r"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b"), "[EMAIL]"),
    (re.compile(r"(?i)\bS-1-\d+(?:-\d+){2,}\b"), "[SID]"),
    (re.compile(r"(?i)\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b"), "[GUID]"),
    (re.compile(r"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])"), "[IPV4]"),
    (re.compile(r"(?i)\b(?:[0-9a-f]{1,4}:){2,7}[0-9a-f]{0,4}\b"), "[IPV6]"),
    (re.compile(r"(?i)\b(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b"), "[MAC]"),
    (re.compile(r"(?i)\b[A-Za-z0-9_.-]{2,70}\\[A-Za-z0-9_.-]{2,70}\b"), "[DOMAIN-ACCOUNT]"),
    (re.compile(r"(?i)\b(?:CN|OU|DC)=[^\s,;]+(?:,(?:CN|OU|DC)=[^\s,;]+)*"), "[LDAP-DN]"),
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def validate_project(project: str) -> str:
    if not isinstance(project, str) or not PROJECT_PATTERN.fullmatch(project) or ".." in project:
        raise ValueError("Project must be a safe GitHub owner/repository name")
    return project


def redact(value: Any, max_chars: int = 1800) -> str:
    text = str(value) if value is not None else ""
    # Replace NUL and ANSI control sequences; never allow multiline raw secrets.
    text = text.replace("\x00", " ")[:max(max_chars * 3, 4000)]
    text = re.sub(r"\x1b\[[0-9;]*[A-Za-z]", "", text)
    text = _SECRET_LINE.sub("[REDACTED: confidential line]", text)
    for regex, replacement in _PATTERNS:
        text = regex.sub(replacement, text)
    return text[:max_chars]


def field(value: Any, max_chars: int = 120) -> str:
    if isinstance(value, (dict, list, tuple, set)):
        raise ValueError("Diagnostic metadata fields must be scalar")
    return re.sub(r"[\r\n\t]+", " ", redact(value, max_chars)).strip() or "unspecified"


def new_event(source: dict[str, Any], *, include_excerpt: bool = False) -> dict[str, Any]:
    if not isinstance(source, dict):
        raise ValueError("Expected a JSON object")
    project = validate_project(source.get("project", ""))
    component = field(source.get("component", "Application"), 90)
    error_type = field(source.get("error_type", "Error"), 90)
    code = field(source.get("code", "unknown"), 60)
    level = str(source.get("level", "error")).lower()
    if level not in {"error", "warning", "info"}:
        raise ValueError("Level must be error, warning or info")
    signature_basis = "|".join([project.lower(), component.lower(), error_type.lower(), code.lower()])
    fingerprint = hashlib.sha256(signature_basis.encode("utf-8")).hexdigest()
    event = {
        "schema": SCHEMA,
        "id": str(uuid.uuid4()),
        "timestamp_utc": utc_now(),
        "project": project,
        "component": component,
        "error_type": error_type,
        "code": code,
        "level": level,
        "version": field(source.get("version", "unknown"), 70),
        "summary": field(source.get("summary", "No description supplied"), 450),
        "platform": platform.system(),
        "architecture": platform.machine(),
        "fingerprint": fingerprint,
    }
    # Excerpts are opt-in per capture; never open or collect log files implicitly.
    if include_excerpt:
        excerpt = source.get("excerpt", "")
        if not isinstance(excerpt, str):
            raise ValueError("Excerpt must be a string")
        event["excerpt"] = redact(excerpt, MAX_EXCERPT)
    return event


def state_root() -> Path:
    custom = os.getenv("UDX_STATE_DIR")
    if custom:
        return Path(custom).expanduser().resolve()
    if os.name == "nt":
        base = Path(os.environ.get("LOCALAPPDATA") or Path.home() / "AppData" / "Local")
    else:
        base = Path(os.environ.get("XDG_STATE_HOME", str(Path.home() / ".local" / "state")))
    return base / "UniversalDiagnostics"


def project_dir(project: str, root: Path | None = None) -> Path:
    validate_project(project)
    folder = hashlib.sha256(project.lower().encode()).hexdigest()[:16]
    return (root if root is not None else state_root()) / folder


def _write_atomic(dest: Path, obj: dict[str, Any]) -> None:
    data = json.dumps(obj, indent=2, ensure_ascii=False).encode("utf-8")
    if len(data) > MAX_EVENT_BYTES:
        raise ValueError("Diagnostic record exceeds size limit")
    dest.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    fd, temp_path = tempfile.mkstemp(prefix=".udx-", suffix=".tmp", dir=dest.parent)
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(data)
            f.flush()
            os.fsync(f.fileno())
        os.chmod(temp_path, 0o600)
        os.replace(temp_path, dest)
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)


def capture(source: dict[str, Any], *, include_excerpt: bool = False,
            root: Path | None = None) -> dict[str, Any]:
    event = new_event(source, include_excerpt=include_excerpt)
    record = {"event": event, "delivery": {"status": "pending", "url": ""}}
    path = project_dir(event["project"], root) / (event["id"] + ".json")
    _write_atomic(path, record)
    return event


def _record_path(project: str, event_id: str, root: Path | None = None) -> Path:
    try:
        normalized = str(uuid.UUID(event_id))
        if normalized != event_id.lower():
            raise ValueError
    except (ValueError, TypeError, AttributeError):
        raise ValueError("Invalid report identifier") from None
    return project_dir(project, root) / (normalized + ".json")


def load_record(project: str, event_id: str, root: Path | None = None) -> dict[str, Any]:
    path = _record_path(project, event_id, root)
    if path.is_symlink() or path.stat().st_size > MAX_EVENT_BYTES:
        raise ValueError("Unsafe diagnostic record")
    record = json.loads(path.read_text(encoding="utf-8"))
    event = record.get("event", {})
    if event.get("project") != project or event.get("id") != event_id:
        raise ValueError("Diagnostic identity mismatch")
    return record


def list_records(project: str, root: Path | None = None) -> list[dict[str, Any]]:
    directory = project_dir(project, root)
    if not directory.exists():
        return []
    result = []
    for path in sorted(directory.glob("*.json"))[:5000]:
        try:
            result.append(load_record(project, path.stem, root))
        except (OSError, ValueError, KeyError, json.JSONDecodeError):
            continue
    return result


def mark_submitted(project: str, event_id: str, url: str,
                   root: Path | None = None) -> None:
    from .github import verify_issue_url
    verify_issue_url(project, url)
    record = load_record(project, event_id, root)
    record["delivery"] = {"status": "submitted", "url": url, "at_utc": utc_now()}
    _write_atomic(_record_path(project, event_id, root), record)



def cleanup_submitted(project: str, days: int = 14, root: Path | None = None) -> int:
    """Prune acknowledged metadata only; pending diagnostics are never deleted."""
    if days < 1 or days > 365:
        raise ValueError("Retention days must be 1-365")
    cutoff = datetime.now(timezone.utc) - timedelta(days=days)
    removed = 0
    for record in list_records(project, root):
        if record.get("delivery", {}).get("status") != "submitted":
            continue
        try:
            stamp = datetime.fromisoformat(record["delivery"]["at_utc"].replace("Z", "+00:00"))
            if stamp < cutoff:
                _record_path(project, record["event"]["id"], root).unlink()
                removed += 1
        except (KeyError, ValueError, OSError):
            continue
    return removed


def _inline(value: Any, length: int = 400) -> str:
    return field(value, length).replace("`", "'").replace("|", "/")


def report_markdown(event: dict[str, Any], *, with_excerpt: bool = False) -> str:
    validate_project(event["project"])
    report = [
        f"<!-- udx-report-id:{event['id']} -->",
        "## Universal Diagnostics (operator-reviewed)",
        "",
        f"- Schema: `{SCHEMA}`",
        f"- Project: `{_inline(event['project'])}`",
        f"- Component: {_inline(event['component'])}",
        f"- App version: {_inline(event['version'])}",
        f"- Severity: {_inline(event['level'])}",
        f"- Error class: {_inline(event['error_type'])}",
        f"- Error code: {_inline(event['code'])}",
        f"- First observed UTC: {_inline(event['timestamp_utc'])}",
        f"- OS family: {_inline(event['platform'])}",
        f"- Architecture: {_inline(event['architecture'])}",
        f"- Diagnostic-Fingerprint: `{event['fingerprint']}`",
        "",
        "### Description (redacted)",
        _inline(event['summary']),
    ]
    if with_excerpt and event.get("excerpt"):
        report.extend(["", "### Optional sanitized excerpt", "```text",
                       redact(event["excerpt"], MAX_EXCERPT).replace("```", "'''"), "```"])
    report.extend(["", "Review every line before publishing. No raw logs are attached."])
    return "\n".join(report)[:MAX_REPORT]


def report_digest(event: dict[str, Any], *, with_excerpt: bool = False) -> str:
    """Digest of the exact report submitted (including optional excerpts)."""
    return hashlib.sha256(report_markdown(event, with_excerpt=with_excerpt).encode("utf-8")).hexdigest()


def report_title(event: dict[str, Any]) -> str:
    # Keep stable fingerprint visible to facilitate manual triage.
    return f"[Diagnostics] {_inline(event['component'], 45)}: {_inline(event['error_type'], 35)} ({event['fingerprint'][:12]})"[:140]


def analyze_local(project: str, days: int = 14, root: Path | None = None) -> list[dict[str, Any]]:
    cutoff = datetime.now(timezone.utc) - timedelta(days=days)
    counter: Counter = Counter()
    for record in list_records(project, root):
        event = record["event"]
        try:
            stamp = datetime.fromisoformat(event["timestamp_utc"].replace("Z", "+00:00"))
            if stamp >= cutoff:
                counter[(event["fingerprint"], event["component"], event["error_type"], event["code"])] += 1
        except (ValueError, KeyError, TypeError):
            continue
    return [{"fingerprint": sig, "component": comp, "type": typ, "code": code, "count": count}
            for (sig, comp, typ, code), count in counter.most_common()]
