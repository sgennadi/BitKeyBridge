# GitHub anonymous diagnostics — BitKeyBridge

## Privacy design

This feature **never uploads a raw log, screenshot, configuration file, clipboard contents or support ZIP**. Pattern-based text redaction cannot reliably remove every private hostname, principal, tenant or recovery ID. Instead, the public GitHub Issue is built only from enumerated component/failure/exception types, HRESULT, count, a SHA-256 fingerprint of those categories, UTC calendar dates, product version and process architecture. There is **no installation ID or user ID**. Arbitrary exception messages, call stacks, directory paths, device names and secrets are not collected in the GitHub counter queue.

The per-user local queue is `%LOCALAPPDATA%\\BitKeyBridge\\github-diagnostic-counters.json`. The normal **Support Bundle** remains a separate local ZIP and may contain infrastructure metadata: inspect it before sharing privately. This feature does not upload it.

## Configure and send

1. On the disposable Windows 11 test PC install GitHub CLI (`gh`) and, as the intended standard user, run `gh auth login` with permission to create Issues in `sgennadi/BitKeyBridge`. BitKeyBridge never handles GitHub passwords, PATs, Graph tokens or browser authentication.
2. In **Helpdesk** select **Preview anonymous diagnostics**. Only the preview's allowlisted JSON can be sent to a public GitHub Issue.
3. Enable **Automatic GitHub error reports (opt-in)**, initially OFF. Manual **Send anonymous report** requires the same consent. **Support → GitHub diagnostics...** navigates to the existing Helpdesk panel from any workspace.
4. Auto-send is attempted while BitKeyBridge is open after at least three pending occurrences, no more than once per six hours after a successful automatic batch. A failed GitHub request leaves all local counters queued. Disabling consent prevents future submissions; it cannot remove already published public Issues.
5. No implicit `gh auth login` or permission escalation is attempted. Without `gh` or an authorized account, the app shows a non-secret explanation.

## Automated analysis

`.github/scripts/diagnostic_triage.py` validates `BKB-DIAG/v1` issue bodies against an exact allowlist. Unexpected fields or free-form issue text are rejected. Prebuild uses a read-only analysis job and offline schema tests **before Windows compilation**. `.github/workflows/diagnostics-triage.yml` analyzes matching newly opened/edited Issues, runs every six hours and supports manual dispatch. It writes a GitHub Actions summary and a 14-day aggregate artifact. The analyzer groups repeated fingerprints and proposes *static troubleshooting directions*, not an asserted root cause or autonomous code change.

**GitHub schedule and Issues event workflows run using the default branch.** The periodic mechanism becomes active after the workflow is merged into `main`. Before that, the PR's exact-HEAD CI prebuild step tests the analyzer on the candidate branch.

No new network submission happens when consent is OFF. The triage workflow itself is read-only: it cannot alter an Issue, build, signature, deployment or security control. If the Issues API is unavailable, it emits a warning without removing normal release/security gates.

## Acceptance

- Run `BitKeyBridge.exe --self-test --no-elevation` and `--ui-self-test --no-elevation`. The tests must not send any network data or change real consent state.
- In Helpdesk verify EN/RU/HE/RTL and 100–200% DPI: notice, opt-in checkbox, Preview and Send must remain visible.
- Inspect the preview with intentionally sensitive synthetic exception messages. They must never appear in the allowed JSON or GitHub Issue.
- Test without `gh`, without auth and without network: all failures keep the counters for retry and do not block GUI or authorized recoveries.
- With a dedicated test GitHub account and operator consent, verify public Issue schema, three-error automatic threshold, six-hour rate limiting and safe repeated aggregation; never submit a real secret in a test.
- Verify prebuild GitHub Actions runs offline schema tests and read-only issue analysis; verify scheduled job after merging to default branch.
