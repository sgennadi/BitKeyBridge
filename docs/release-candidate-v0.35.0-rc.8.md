# BitKeyBridge 0.35.0-rc.8 — UniversalDiagnostics unified local queue

**Unsigned manual QA candidate, not Stable.** One integration PR #115.
Do not install via GPO/Intune or auto-update before enterprise signing,
real managed Windows acceptance, and approved canary/rollback.

## Replaced diagnostics

- Replace legacy GitHubDiagnosticReporter and experimental HTTPS relay with
  in-process UniversalDiagnostics 0.1.0-rc.1, vendored verbatim from the
  private canonical repository sgennadi/UniversalDiagnocticsModule.
- Capture schema: universal-diagnostics/v1. A per-user local JSON queue is
  namespaced by the project's SHA-256. No separate executable, Python runtime,
  GitHub account, GitHub token, gh CLI or network connectivity is required
  for local recording.
- Existing AppLogger, DiagnosticRedaction, native error panel, Support Bundle,
  protected audit, LAPS, BitLocker, AD/Graph, recovery authorization and update
  behavior are preserved; no automatic diagnostic submission is enabled.
- The default is opt-out for future *local* classified error captures.
  Helpdesk allows opt-in, count preview, explicit safe local report creation
  and Explorer navigation to the current user's diagnostics directory.
  Static EN/RU/HE and Hebrew RTL labels must pass native UI regression.
- Only fixed technical categories, known exception classes, numeric HRESULT,
  app version, timestamp and fingerprint enter shared events. No raw errors,
  stack traces, usernames, hostnames, AD/Entra IDs, tokens, passwords,
  BitLocker/LAPS secrets, support ZIPs or filesystem paths are submitted.
- The old BKB-DIAG queue is retained but not uploaded or silently migrated.

## GitHub Actions

- Canonical UniversalDiagnostics Python package and offline tests are vendored
  for prebuild analysis; prebuild is read-only and uses sanitized metadata.
- One default-branch review workflow summarizes recurring GitHub Actions job
  categories and manually published UDX Issues. It must never copy raw build
  logs or automatically publish client event records.
- Remove the old BKB-specific issue triage and Worker test jobs.
- The per-candidate CI package must be created from one exact reviewed SHA
  with matching x64/x86/ARM64 outputs, BUILD-INFO.txt and SHA256SUMS.txt.

## Acceptance matrix

1. Verify Windows x64, x86 and ARM64 build/publish at the exact commit.
2. Run BitKeyBridge.exe --self-test --no-elevation and
   BitKeyBridge.exe --ui-self-test --no-elevation on Windows.
3. Verify SHA-256 fingerprints match the canonical Python contract and synthetic
   BitLocker passwords / Authorization values do not survive local redaction.
4. Switch EN/RU/HE repeatedly at 100%, 125%, 150%, 175% and 200% DPI, including
   compact and RDP sessions. Preview, local opt-in, manual save and Open folder
   actions must remain visible and clickable.
5. Verify no local captures with opt-in disabled, opt-in persisting per user,
   and manual capture working without a GitHub account or elevation.
6. Confirm the previous logs and old queue remain intact after RC7 upgrade.
7. Run LAPS regression, protected Audit Broker integration, legacy
   update/rollback, signing-positive tests and CodeQL at the same exact HEAD.
8. Manual real AD/Graph identity, Device 360 inventory, delegated/denied
   BitLocker/LAPS Reveal/Copy and cloud-session reuse remain mandatory before
   any promotion to a signed Stable release.

See docs/universal-diagnostics-integration.md for source paths,
data boundaries, manual QA and synchronization with the common module.
