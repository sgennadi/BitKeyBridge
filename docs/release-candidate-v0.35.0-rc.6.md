# BitKeyBridge 0.35.0-rc.6 — anonymous GitHub diagnostic QA

**UNSIGNED isolated manual QA; NOT Stable, no GPO/Intune deployment.** PR #115, historical branch `release/v0.35.0-rc.1`.

## Scope

- A non-secret local error counter queue from startup and native WinForms errors.
- Explicit opt-in (default OFF), Preview, Send, Support menu shortcut and EN/RU/HE labels.
- Existing authenticated GitHub CLI (`gh`) is required for sending Issues; no password, PAT, implicit MFA or hidden credential flow is added.
- Three occurrences trigger non-blocking auto-send at most once every six hours after a successful send. Failed sends preserve counts.
- `BKB-DIAG/v1` strict-schema GitHub Issues with no raw logs/device/user data, followed by read-only prebuild/on-Issue/six-hour analysis. Scheduled and Issue workflows activate on default branch after merge.
- ProductVersion/InformationalVersion: `0.35.0-rc.6`; numeric FileVersion: `0.35.0.0`. The old RC5 ZIP does not contain this feature.

## CI and manual gates

- Exact HEAD Windows x64/x86/ARM64, native UI/RTL/DPI tests, CodeQL, LAPS, protected Audit Broker, rollback/migration and ephemeral signing checks all must pass. Verify BUILD-INFO source and nested SHA256 checksums before installing any QA archive.
- Run `--self-test --no-elevation` and `--ui-self-test --no-elevation` on Windows 11. Test consent-off/consent-on with authorized test GitHub account, no GH CLI, auth denied, no network, failed retries and no raw issue payloads.
- Test Windows/RDP resolutions and EN/RU/HE at 100/125/150/175/200% DPI; no clipped actions and no blocking network work on the UI thread.
- Confirm real AD/Entra/Intune search, RBAC, protected audit and BitLocker/LAPS Reveal/Copy remain unchanged. Human operator acceptance, corporate Authenticode signing and signed-only canary/rollback are required before Stable.

See `docs/github-anonymous-diagnostics.md`.
