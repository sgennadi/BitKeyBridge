# BitKeyBridge v0.30.0 staged stable — managed workstation acceptance

This document is for manual testing of the **GitHub Actions CI build artifact**, before publishing a stable GitHub Latest release. The experimental SYSTEM Protected Audit Broker in Draft PR #57 is not present.

## Package and commands

1. Open the CI run for the stable-v0.30.0 PR. Confirm success for x64, x86, ARM64, LAPS regression, offline and UI tests, the workflow-safety regression and CodeQL. Download your architecture artifact and note the source commit.
2. Verify ProductVersion `0.30.0` (with optional +source-SHA metadata), FileVersion `0.30.0.0`, correct CPU architecture and SHA256. Unsigned Actions executables are for controlled manual testing only, not unattended deployment.
3. In normal, unelevated CMD, from the extracted directory:

    BitKeyBridge.exe --version
    BitKeyBridge.exe --self-test
    BitKeyBridge.exe --ui-self-test
    BitKeyBridge.exe --update-checks-status --no-elevation
    BitKeyBridge.exe --auto-update-status --no-elevation

## Real workstation QA

1. On a domain-joined Windows 11 workstation as a delegated non-admin user, verify startup has no UAC and AD/Entra connection wizard appears before updater check. Check integrated AD login, manual credential login and invalid-password retry. Check standalone behavior separately.
2. Confirm metadata-only searches do not reveal passwords or BitLocker keys. Test ALLOWED and DENIED BitLocker recovery and Legacy/Windows LAPS current/history permissions against dedicated test computers, including missing LAPS attributes, decryption errors and runbook diagnostics.
3. Check the Start/Recovery/LAPS/Devices/Diagnostics navigation, clipboard auto-clear, secret expiry, and text clipping at 100%, 150%, 200% DPI, 4K and RDP layouts.
4. Verify an authorization audit record precedes every privileged secret read; an unavailable required audit writer must block the read. The per-user audit fallback is not tamper-resistant; strict protected auditing requires a separately approved machine writer.
5. If enabled, test delegated Entra/Intune Microsoft Graph access, JIT/two-person approvals and SIEM delivery on designated test systems. Do not rotate production keys.
6. Once an actual institutional Code Signing certificate is available, verify approved publisher pinning and both signed manual update and rollback. Deliberately test unsigned/wrong signer rejection and existing v0.29.12 saved automatic update preferences.

## Production gate

Existing v0.29.x defaults may auto-install newly published stable updates without requiring Authenticode. Keep public Latest at v0.29.12 until the managed acceptance above is complete and `BITKEYBRIDGE_STABLE_030_APPROVED=true` and approved organizational signer are configured. GitHub CI cannot prove live campus permissions, SIEM receipt or operational approvals.
