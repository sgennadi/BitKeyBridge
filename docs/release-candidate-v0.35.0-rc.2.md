# BitKeyBridge 0.35.0-rc.2 — unified release candidate

**Status: UNSIGNED manual QA only; not published Stable and not authorized for production, GPO or Intune deployment.**

## Candidate identity and lineage

- Candidate application version: `0.35.0-rc.2` for both `--version` and PE ProductVersion/InformationalVersion.
- Windows numeric AssemblyVersion and FileVersion remain `0.35.0.0` for Windows interoperability; these do **not** distinguish the candidate. Validate ProductVersion, SHA256SUMS and BUILD-INFO.
- Single PR: [#115](https://github.com/sgennadi/BitKeyBridge/pull/115). For continuity, the integration branch keeps its existing historical name `release/v0.35.0-rc.1`; **this does not define the application version**. The project SemVer and the exact CI source SHA do.
- Previous RC1 changes and QA history: [RC1 documentation](release-candidate-v0.35.0-rc.1.md). Published Stable/rollback baseline: [v0.33.1](https://github.com/sgennadi/BitKeyBridge/releases/tag/v0.33.1).
- New candidate release is authorized for manual QA only once exact-HEAD required CI is green and the unsigned archive `BitKeyBridge-v0.35.0-rc.2-QA-UNSIGNED` is generated. Never substitute an earlier RC1 artifact.

## RC2 changes

- Repair **Start BitLocker Reveal/Copy** to retrieve matching recovery metadata from the selected AD computer DN, not just a chosen OU or a name-only search. Only the exact selected Recovery ID can be read. Duplicates, mismatches and stale selection fail closed.
- Allow the same in-memory source-qualified Start cloud record to open Devices for protected Entra recovery, using the existing Microsoft 365 session without a new search. Multiple recovery IDs require a deliberate selection. A missing/expired session shows a Connect instruction, **never unexpected MFA**.
- Clear the prior Devices key and re-mask the input on device/Recovery ID changes; invalidate stale async key reads when the selected source, key ID or Graph session changes. Preserve RBAC, approvals, ticket/JIT requirements and durable audit.
- Provide explicit status when the LAPS Reveal/Show record is unreadable or not selected. Do not invent a password or bypass encrypted LAPS rights.
- Keep RC1 Device 360 v2 (Overview, Hardware, Security, Identity, Audit, Diagnostics), source-targeted retry, exact AD/Entra/Intune identity evidence, Hardware Inventory, Device Health, Smart Diagnostics and non-secret Helpdesk Report.

## Required exact-source automated checks

All tests must belong to the precise candidate SHA: Windows x64 native `--self-test` and `--ui-self-test`, x86/ARM64 compilation, LAPS regression, protected Audit Broker integration with denied operators, legacy upgrade/rollback, test signing and tamper validation, CodeQL, stable-main provenance and QA packaging. No skipped required check may be counted as a pass. Verify `BUILD-INFO.txt` source SHA and the inner x64/x86/ARM64 ZIPs against `SHA256SUMS.txt`.

## Mandatory real-machine acceptance before Stable

On a disposable, managed Windows 11 workstation, run as a standard user and delegated helpdesk operator with explicit domain permissions. Confirm Start → BitLocker Reveal/Copy both inside and outside the selected OU; Devices Show/Copy after changing device/key ID; Microsoft 365 session reuse and clean expired-token explanation; LAPS current/history read and denied users; protected journal ACK/denials; Device 360 and Helpdesk Report without secret values. Check 100%, 125%, 150%, 175% and 200% DPI, narrow/RDP windows, EN/RU/HE switching, icons and accessible controls.

Never execute unsigned QA on a domain controller or auto-roll out to managed machines. Organization Authenticode signing and a deliberately approved **signed-only** canary/rollback are separate mandatory gates.

## Manual QA commands (from the extracted matching architecture ZIP)

```cmd
BitKeyBridge.exe --version
BitKeyBridge.exe --startup-diagnostics --no-elevation
BitKeyBridge.exe --self-test --no-elevation
BitKeyBridge.exe --ui-self-test --no-elevation
BitKeyBridge.exe --audit-broker-preflight --no-elevation
```

The version output must be **0.35.0-rc.2**. QA-only GitHub Actions artifacts are not published GitHub Releases and are not available to the signed automatic updater.

## Versioning rule for future candidates

For each subsequent set of fixes, advance project `Version` and `InformationalVersion` to `0.35.0-rc.3`, `-rc.4`, etc., prepend exactly one matching `CHANGELOG.md` section and add `docs/release-candidate-v0.35.0-rc.N.md`. The QA workflow derives the archive name dynamically from the version. Keep one PR/branch for iterative QA until an approved Stable promotion; never silently re-label a previous artifact or publish a new Stable release.
