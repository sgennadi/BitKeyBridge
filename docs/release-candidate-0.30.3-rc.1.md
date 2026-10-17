# BitKeyBridge v0.30.3-rc.1 — isolated Windows QA

This is an **unsigned release candidate**, not a production release. Stable remains v0.30.1. PR #62 is stacked on PR #61, both remain draft until real managed-domain and RDP acceptance.

## Build and download

Open the latest green build GitHub Actions run for PR #62. Download the artifact named BitKeyBridge-v0.30.3-rc.1-TEST-BUNDLE-UNSIGNED. The workflow gates this artifact on Windows builds for win-x64, win-x86, win-arm64, LAPS regression, two standard Windows tokens protected-audit integration and ephemeral signing positive/negative tests. Those temporary test signatures are **not** organizational publisher signatures.

Verify SHA256SUMS.txt for the 3 architecture ZIPs before extracting. Use only a disposable QA Windows endpoint, never production endpoints, domain controllers, SCCM/Intune/GPO auto-deployment, or automatic updates. Preserve existing production configuration without copying credentials into QA.

## QA commands (non-elevated terminal)

    BitKeyBridge.exe --version
    BitKeyBridge.exe --startup-diagnostics --no-elevation
    BitKeyBridge.exe --self-test --no-elevation
    BitKeyBridge.exe --ui-self-test
    BitKeyBridge.exe --audit-broker-preflight --no-elevation

To exercise the broker, an administrator must provision the test service and explicit scoped RecoveryReaders policy. Then as an authorized **non-admin** run (writes one synthetic health event):

    BitKeyBridge.exe --audit-broker-preflight --probe --no-elevation

Test with a separate unauthorized non-admin as well: privileged recovery must be denied, no secret must be emitted, and no positive broker ACK should occur. Testing an administrator or using "Run as administrator" does not substitute for this. Test real AD BitLocker access, Windows/legacy LAPS read/decrypt/rotation permissions, optional Entra/Intune roles, audit journal evidence and stable updater rollback. In strict audit mode, do not use the user-audit fallback.

Use docs/non-admin-startup-diagnostics.md for CyberArk EPM/WDAC/AppLocker triage. If EPM blocks process creation, the EXE cannot tell you which rule did so; inspect EPM and Windows application-control events with your endpoint-security team. Do not disable protection to make the test pass.

## Required before stable release

- Confirm CI green (including CodeQL) for the exact head commit.
- Validate authorized and unauthorized non-admin domain users against managed test computers.
- Confirm real protected broker audit ACK + durable journal, deny-by-default RBAC, AD/LAPS/BitLocker delegation, cloud permissions where used and zero secret leakage.
- Confirm Authenticode publisher trust under approved enterprise policy and update/rollback on real managed endpoints.
- Validate WinForms at 100%, 150%, 175% and 200% in real interactive/RDP sessions; CI headless layout tests are a preliminary gate only.

Do not merge PR #61/#62 or tag a stable release until these checks are documented and approved.
