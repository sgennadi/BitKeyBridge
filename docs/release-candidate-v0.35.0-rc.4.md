# BitKeyBridge 0.35.0-rc.4 — recovery, search and legacy updater QA

**UNSIGNED manual QA candidate only; not a Stable release and not authorized for GPO/Intune rollout.**

## Identity and artifact

- ProductVersion: 0.35.0-rc.4 (numeric Windows FileVersion stays 0.35.0.0).
- One PR: https://github.com/sgennadi/BitKeyBridge/pull/115 on the historic branch release/v0.35.0-rc.1.
- Verify exact source SHA, BUILD-INFO.txt, SHA256SUMS.txt and the matching x64/x86/ARM64 EXE ZIPs. After all CI checks pass, artifact is BitKeyBridge-v0.35.0-rc.4-QA-UNSIGNED.
- Earlier notes: release-candidate-v0.35.0-rc.3.md. Do not use RC3 binaries when reporting RC4 behavior.

## Changes in RC4

1. A temporary Windows EXE file lock (for example after a health probe or an endpoint scanner) now gets at most seven short retries for the atomic File.Move replacement step only. Persistent access denial remains fatal, and rollback/backup integrity checks remain mandatory.
2. Transactional update history records a non-secret stage to help locate a failed backup, replacement or post-install verification.
3. An offline synthetic locked EXE verifies the new retry behavior; legacy v0.30.1 and v0.30.3 real upgrades and rollbacks must still pass.

## Managed Windows 11 operator QA

- Devices: search a known computer name of at least two characters. Verify independent AD, Intune and Entra status, early read-only preview rows, final source reconciliation and clear 401/403 or timeout diagnostics; don't infer absent backup from unavailable metadata.
- Test exact computer, serial, UPN and Entra device GUID, with AD-only and cloud-only sessions. Searching must never silently invoke Device Code / MFA: Connect Microsoft 365 is explicit.
- Verify the query stays editable while a search is in progress; type another name and press Restart Search. Cancel must remain responsive and stale metadata must not replace results.
- Select a final exact source-qualified device and recovery ID: test authorized Start/Devices BitLocker Reveal/Copy and LAPS Show/Copy; missing permission/ticket/audit or changed session must block secrets with a clear reason.
- Verify Device 360 tabs/Hardware/Identity/Source Health/Report and EN/RU/HE layouts at 100, 125, 150, 175 and 200 percent DPI, including RDP resize. Save sanitized diagnostics only.
- Confirm BitKeyBridge.exe --version prints 0.35.0-rc.4; run --self-test --no-elevation and --ui-self-test --no-elevation.

## Mandatory CI and release boundaries

Windows x64/x86/ARM64, CodeQL, native WinForms UI, LAPS, Audit Broker, signing/tamper and both real legacy migration rollback tests must pass at exact HEAD. The RC3 latest-head legacy v0.30.3 test failed on Access denied, so no RC4 QA ZIP is approved until this gate passes. Corporate signing, human device acceptance and a signed-only canary are required before any Stable rollout.
