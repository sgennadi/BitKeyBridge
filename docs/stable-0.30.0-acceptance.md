# BitKeyBridge v0.30.0 staged stable — managed workstation acceptance

This document is for manual testing of **public stable v0.30.0** on designated technician workstations. It uses MANUAL-UNSIGNED download filenames specifically to prevent unattended upgrades by v0.29.x. The experimental SYSTEM Protected Audit Broker in Draft PR #57 is not present.

## Package and commands

1. Open the CI run for the stable-v0.30.0 PR. Confirm success for x64, x86, ARM64, LAPS regression, offline and UI tests, the workflow-safety regression and CodeQL. Download the MANUAL-UNSIGNED architecture ZIP from GitHub Releases, verify SHA256SUMS.txt and note the source commit.
2. Verify ProductVersion `0.30.0` (with optional +source-SHA metadata), FileVersion `0.30.0.0`, correct CPU architecture and SHA256. Public unsigned executables are for controlled manual testing only, not unattended deployment.
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

Existing v0.29.x defaults may auto-install newly published stable updates without requiring Authenticode. Public v0.30.0 Stable/Latest uses manual-only noncanonical ZIP names to avoid auto-installing unsigned EXEs on older clients. Before offering canonical auto-update asset names, complete managed acceptance and configure an approved organizational signer. GitHub CI cannot prove live campus permissions, SIEM receipt or operational approvals.
