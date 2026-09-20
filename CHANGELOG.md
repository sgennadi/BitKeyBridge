# Changelog

## 0.6.0

- Added Active Directory connection modes for domain-joined workstations/DCs and standalone/workgroup computers.
- Added explicit DC, AD domain, LDAP/LDAPS port and AD username configuration.
- Added LDAPS/TLS support (normally TCP 636) alongside signed/sealed LDAP (normally TCP 389).
- Added session-only explicit AD password handling; passwords are never written to appsettings or logs.
- Added current-Windows-credentials mode for domain workstations and DCs.
- Added a Directory Connection GUI tab with DC connection test and session-password clearing.
- Added CLI connection overrides: `--ad-auto`, `--ad-server`, `--ad-domain`, `--ad-user`, `--ad-password-prompt`, `--ad-integrated`, `--ad-port`, and `--ad-test`.
- Added configurable local/UNC export root so BitLocker recovery export no longer requires BitKeyBridge itself to run on a domain controller.
- Kept the legacy SYSVOL root as a backward-compatible fallback when `OutputRoot` is empty.
- Microsoft 365 / Entra / Intune workflows remain independent of Windows domain membership.
- Added a CI gate requiring every project version to have a matching CHANGELOG section.

## 0.5.0

- Added optional ticket/reference requirement before recovery-key access.
- Added helpdesk reason capture and structured Reference/Reason fields in the JSONL security audit.
- Reused recovery access context across reveal, copy, retrieval, and rotation during the GUI session.
- Added post-recovery Intune rotation reminders without automatic rotation.
- Added CodeQL v4 scanning on Node 24.
- Added Dependabot for NuGet and GitHub Actions.
- Added pinned CycloneDX 6.2.0 JSON SBOM generation for tagged releases.
- Added GitHub provenance and SBOM attestations through actions/attest@v4.
- Added the SBOM to SHA256SUMS and GitHub Release assets.
- Extended offline self-test coverage for audit reference/reason persistence.


## 0.4.0

- Added verified GitHub Release self-update for x64, x86, and ARM64.
- Added SHA256SUMS verification and GitHub asset-digest cross-checking.
- Added staged executable version validation and mandatory offline `--self-test` before update application.
- Added elevated temporary update helper with GUI/service replacement and rollback.
- Added `--check-update` and `--update` CLI commands.
- Added Windows Event Log integration with recovery-password sanitization.
- Added opt-in TLS Remote API with one-time bearer token provisioning.
- Added Windows Firewall Domain/Private inbound rule management through the Windows Firewall COM API.
- Added read-only remote health/service/version endpoints and separately gated remote export management.
- Added native Windows Service failure-recovery configuration.
- Kept updater cleanup shell-free through Win32 `MoveFileEx`.
- Added Operations UI for update, Remote API, token rotation, and Event Log access.


## 0.3.0

- Added a Health & Service Dashboard.
- Added native Windows Service installation/runtime through Service Control Manager APIs.
- Added scheduled service exports with configurable interval and run-on-start behavior.
- Added a loopback-only JSON health endpoint on 127.0.0.1 with HTTP 503 on error health states.
- Added `--health`, `--install-service`, `--uninstall-service`, `--start-service`, `--stop-service`, and `--service-status` CLI commands.
- Added certificate-expiry and export/replication health reporting.
- Added Secure Output Wizard with protected NTFS ACLs and optional restricted SMB share creation through `NetShareAdd`.
- Added GUI sensitive-state cleanup on close.
- Kept runtime operation free of PowerShell and `sc.exe`.


## 0.2.1

- Removed the requirement to pre-create an App Registration for first-run Entra setup.
- Added built-in Microsoft first-party bootstrap using Microsoft Graph Command Line Tools and Device Code.
- Moved custom Bootstrap Client ID to an advanced fallback for restricted Conditional Access environments.
- Resolved runtime Graph permission IDs dynamically from the Microsoft Graph service principal instead of hard-coding them.
- Added retry/backoff for Graph throttling and transient service errors.
- Added propagation handling for newly created Enterprise Applications.
- Added offline self-test coverage for the built-in bootstrap configuration.


## 0.2.0

- Added Device Code authentication as the recommended interactive Graph sign-in mode.
- Added Intune managed-device inventory and unified AD + Entra + Intune device search.
- Added BitLocker recovery-key rotation through Intune with explicit confirmation.
- Added local JSONL security audit with automatic BitLocker recovery-password redaction.
- Added audit self-test.
- Added `DeviceManagementManagedDevices.ReadWrite.All` to native Entra Auto Setup.
- Updated GitHub Actions to Node 24 runtimes.
- Kept legacy ROPC and certificate authentication modes.

## 0.1.0

Initial native Windows implementation.

- C# / .NET 10 LTS / WinForms.
- Self-contained single-file executables for win-x64, win-x86, and win-arm64; each architecture uses the same GUI/CLI codebase.
- Direct LDAP BitLocker export with atomic CSV publishing and safety guards.
- Dynamic domain-controller discovery and replication diagnostics.
- Configurable OU scopes and local recovery search.
- Microsoft Graph recovery metadata search and explicit on-demand recovery-key retrieval.
- Password/ROPC and certificate authentication.
- Native Entra App Registration bootstrap/setup flow.
- GitHub Actions Windows build, single-file publish, offline self-test, and tagged release packaging.
