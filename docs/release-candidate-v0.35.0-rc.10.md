# BitKeyBridge 0.35.0-rc.10 — title and local diagnostic consent

**UNSIGNED manual QA candidate, not Stable.** Integration branch `release/v0.35.0-rc.1`, PR #115. Do not deploy with GPO, Intune or an unattended auto-upgrade.

## Scope

- The main window displays the exact assembly SemVer candidate, including `-rc.10`, rather than only Windows' numeric FileVersion. The latter stays `0.35.0.0`.
- Opting out of local UniversalDiagnostics now cancels pending queued background event writes. The disk write and consent update share one lock; explicit subsequent opt-in does not revive a stale task.
- The 10-minute per-category throttle is reset when consent changes. Existing local manual snapshot/export, strict `universal-diagnostics/v1` schema and no-upload defaults are unchanged.
- Native UI regression tests the release-candidate title. Offline `--self-test` verifies opt-out and later opt-in both reject the prior synthetic worker, using only a temporary queue.

## Automated gates for exact final HEAD

1. `BitKeyBridge.exe --self-test --no-elevation`: canonical UDX queue, redaction, fingerprint and consent revocation.
2. `BitKeyBridge.exe --ui-self-test --no-elevation`: EN/RU/HE, RTL, DPI 100–200%, native title and recovery action regressions.
3. Windows x64/x86/ARM64 .NET 10 build/publish, shared Python diagnostics tests, LAPS and protected audit integration, migration/rollback, CodeQL, source-pinned QA archives and tamper/signing checks.
4. Complete all checks for **this** commit SHA; a previous RC9 green run does not validate RC10.

## Operator acceptance on an isolated Windows 11 workstation

- Confirm the title reads `BitKeyBridge 0.35.0-rc.10 (.NET)` during English, Russian, Hebrew and resized/RDP sessions.
- With automatic diagnostic capture OFF, verify no event appears from simulated benign failures. Turn capture ON for a safe test, then OFF; inspect local queue/preview for category metadata only. Confirm no GitHub transport or credentials.
- Exercise Start → Devices → Device 360 for a verified AD/Entra/Intune device using one approved Graph session, source retry, canceled searches and expired session handling; test allowed and denied BitLocker/LAPS Reveal/Copy with protected audit, exact recovery ID, RBAC and ticket gates.
- Never put actual recovery secrets, tokens, device identities or raw private logs in a public issue. Keep existing Stable available for rollback. Signing and approved signed-only canary remain prerequisites for Stable.
