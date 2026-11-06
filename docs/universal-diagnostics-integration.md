# UniversalDiagnostics integration in BitKeyBridge 0.35.0-rc.8

**Status: unsigned QA candidate in PR #115. No stable rollout is authorized.**

## Shared source

- Source of truth: private GitHub repository `sgennadi/UniversalDiagnocticsModule`,
  version `0.1.0-rc.1`.
- Canonical embedded source:
  `src/UniversalDiagnostics.Embedded/UniversalDiagnosticsEmbedded.vb`,
  vendored from `embedded/dotnet/UniversalDiagnosticsEmbedded.vb` with
  its own `net10.0` VB project. BitKeyBridge builds and bundles this assembly
  into its single-file publish; no separate UDX app must be installed.
- Queue/schema: `universal-diagnostics/v1` with deterministic SHA-256
  fingerprint, random event UUID, UTC timestamp and `delivery.status=pending`.
  The queue is namespaced under
  `%LOCALAPPDATA%\UniversalDiagnostics\<sha256(project-lower)[0:16]>\`.
- CI reviewer: canonical Python modules vendored under
  `.github/universal_diagnostics/`. A read-only prebuild analysis and a
  single scheduled/default-branch review replace the earlier BKB-DIAG-v1
  analyzer and Cloudflare Worker proof of concept.

## Local application behavior

- Existing AppLogger log files, `DiagnosticRedaction`, WinForms error banner,
  support ZIPs, Advanced/Environment/Device diagnostics and Windows
  protected audit continue to work. They are **not** replaced by a cloud logger.
- UI and startup exceptions can be projected into **allowlisted** service,
  UI, Graph, AD-adjacent, BitLocker and LAPS error categories and a numeric
  HRESULT. Raw exception messages, stack traces, file paths, account
  identifiers, device names and recovery secrets are never supplied to UDX.
- Classification uses a 10-minute cooldown per category/type/code to avoid
  flooding per-user storage. Capture happens off the WinForms UI thread.
- Automatic **local** error capture is OFF by default and requires a user to
  enable **Capture diagnostic error categories locally (opt-in)** in Helpdesk.
  The setting affects future errors only. It does not sign in to GitHub.
- **Save local diagnostic report** is a direct operator action which creates
  a non-secret manual UDX event. **Preview local diagnostics** shows limited
  category/count summaries without dumping raw records. **Open local reports
  folder** opens the per-user queue in Explorer without uploading anything.
- **Support → Universal diagnostics...** selects Helpdesk and shows the
  same local preview. EN/RU/HE translations use the existing WinForms
  static-caption localization system.
- Legacy `%LOCALAPPDATA%\BitKeyBridge\github-diagnostic-counters.json`
  remains on disk for explicit future migration, but is not read into UDX
  automatically or transmitted. No older consent is silently inherited.
- The old `GitHubDiagnosticReporter`, `DiagnosticRelayTransport`,
  experimental Worker code and old GitHub-specific Issues triage are removed.

## Explicit privacy and transport boundary

This integration performs **NO automatic GitHub/network diagnostic upload**.
It has no GitHub CLI invocation or PAT on the workstation, no background
OAuth login, and no use of a Cloudflare relay. App update checks and
authorized AD/Graph operations are unrelated and retain their existing
policies. If a future opt-in remote reporting flow is added, it must have
a separately reviewed threat model, complete human-visible preview and
scoped authorization. Do not embed a shared GitHub PAT in Windows clients.

The existing support bundle may contain more detail than UDX events and
remains a local/operator-controlled artifact; it must never be published
to public Issues automatically.

## Build, regression and acceptance

1. Check out the exact candidate HEAD. Run the shared Python tests with
   `PYTHONPATH=.github python3 -m unittest discover -s .github/universal_diagnostics/tests -v`.
   The standard `build.yml` automatically executes this offline stage.
2. On Windows run `BitKeyBridge.exe --self-test --no-elevation`;
   this exercises a temporary isolated UDX queue and verifies the exact
   cross-language SHA-256 fingerprint, schema, pending state, and that
   synthetic bearer tokens / recovery passwords never reach the queue.
3. Run `BitKeyBridge.exe --ui-self-test --no-elevation`, including native
   Helpdesk EN/RU/HE controls at 100/125/150/175/200% and compact/RDP
   layouts. Verify right-to-left Hebrew and no clipped buttons.
4. Confirm no report is recorded by default after startup/AD/Graph failures;
   enable local opt-in and verify an allowlisted error is written without
   blocking the UI. Disable it and verify new background captures stop.
5. Save a manual UDX snapshot, preview and inspect a random queued event.
   Ensure no hostname, username, tenant ID, paths, exception text, token,
   recovery ID, BitLocker key or LAPS password appears.
6. Upgrade from v0.33.1/v0.35 RC7 in a controlled test, verify old local
   logs and legacy queue are preserved and no relay setting causes upload.
7. Confirm CI Windows x64/x86/ARM64 build + CodeQL, LAPS regressions,
   protected audit and upgrade/rollback tests pass on the final exact SHA.
   The reviewer workflow starts on the default branch only after merge.
8. Real managed Windows 11, AD/Entra/Intune, DPI/RDP, delegated/denied
   recovery, enterprise Authenticode signing, canary and rollback acceptance
   remain necessary before an approved Stable release.

## Source upgrades

To update the common module, change the canonical source in
`UniversalDiagnocticsModule` first, then vendor a reviewed copy into
BitKeyBridge with a self-test update and exact-head CI gate. Do not
download private source during application startup or a customer build.
