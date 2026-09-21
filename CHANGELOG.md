# Changelog

## 0.13.0

- Added recovery-session correlation IDs across local AD reveal/copy, Entra get/reveal/copy, and Intune rotation workflows.
- Added metadata-only recovery incident bundles under `%ProgramData%\BitKeyBridge\Incidents`; bundles contain operator/device/ticket/timestamps/actions/audit hashes and rotation state, never the 48-digit recovery password.
- Added backward-compatible audit chain version 2. Version 1 entries remain verifiable; version 2 adds the recovery-session CorrelationId to the SHA-256 entry hash.
- Audit writes now return the appended entry and EntryHash so incident bundles can reference exact tamper-evident audit records.
- Added the Session/Correlation ID column to the Audit tab.
- Added focused local health endpoints: `/health/ready`, `/health/security`, and `/health/coverage`, while preserving `/health` and `/health/live`.
- Added least-privilege Remote API bearer tokens with independent `read`, `coverage-run`, and `export` scopes. The existing bearer token remains the backward-compatible Admin token.
- Remote API GET endpoints accept any valid scope; Coverage POST requires Admin/CoverageRun and Export POST requires Admin/Export, in addition to the global remote-management switch.
- Added scoped-token lifecycle through CLI and GUI; tokens are shown once and only SHA-256 hashes are persisted.
- Added safe JSON configuration backup/restore with validation and an automatic pre-restore rollback backup.
- Configuration backup intentionally excludes Credential Manager/DPAPI password material, Graph tokens, certificate private keys, and BitLocker recovery passwords.
- Added sanitized diagnostics ZIP generation with health/service/config/certificate ACL/status/log metadata while explicitly excluding recovery CSV/passwords, audit contents, credential blobs, bearer tokens/hashes, Graph tokens, and private keys.
- Added Configuration / Diagnostics controls to Operations and CLI commands `--config-backup`, `--config-restore`, and `--diagnostics-bundle`.
- Added staged Microsoft Entra certificate rollover. A new credential is added without removing the active credential, app-only Graph authentication is verified, service private-key ACL is prepared, and only then are user/machine configs switched.
- Entra rollover retains the previous Graph credential and local certificate for rollback/grace instead of automatically deleting them.
- Added dual-signed audit-signing certificate rollover. The transition payload is signed by both the previous and new RSA private keys and transition history is retained under `%ProgramData%\BitKeyBridge\AuditSigningTransitions`.
- Audit-signing rollover verifies the current trust anchor before transition, creates a new signed checkpoint, rolls configuration/checkpoint back on normal failures, and retains old certificates for historical verification.
- New Remote API TLS certificates are persisted without the Exportable flag.
- Added Remote API TLS private-key ACL preflight for installed service identities and service-identity changes.
- Health/security output now reports Remote API scope configuration, TLS certificate expiry/status, and service private-key access without exposing token hashes.
- Added offline self-tests for mixed audit v1→v2 chains, CorrelationId hashing, metadata-only incident bundles, dual-signed rollover tamper detection, Remote API scope normalization, and configuration validation.
- Updated the public appsettings example for scoped Remote API token hashes.

## 0.12.0

- Added cryptographically signed audit checkpoints on top of the existing SHA-256 audit hash chain.
- Added a dedicated RSA-3072 LocalMachine audit-signing certificate, separate from Entra and Remote API certificates.
- Audit-signing private keys are persisted as machine keys without the Exportable flag.
- Signed checkpoints include the audit head hash, entry counts, chain version, machine identity, signer thumbprint, timestamp, and RSA-SHA256-PKCS1 signature.
- Existing signed checkpoints must verify successfully and their signed hash must still exist in the current valid audit chain before BitKeyBridge can overwrite the trust anchor.
- This continuity guard prevents the Windows Service from silently signing an already rewritten audit chain during the next scheduled verification.
- Added audit-signing CLI commands: `--audit-signing-status`, `--audit-signing-setup`, `--audit-signing-sign`, `--audit-signing-verify`, `--audit-signing-disable`, and `--audit-signing-years`.
- Added Audit-tab controls for setup, immediate signing, signature verification, disabling new checkpoints, and signer/checkpoint status.
- Windows Service identity changes now preflight private-key access for the audit-signing certificate in addition to the Entra certificate.
- The Windows Service signs the verified audit head during its daily integrity cycle when audit signing is enabled.
- Health output now exposes signed-checkpoint state, signer-certificate expiry, signature validity, checkpoint age/count, and whether the current audit head is signed.
- Added configurable warning horizon for audit-signing certificate expiry.
- Added offline self-tests for RSA signed-checkpoint round-trip and signed-payload tamper detection.
- Updated the public appsettings example with audit-signing settings.

## 0.11.0

- Added optional Windows user/group RBAC for privileged recovery workflows while preserving backward-compatible behavior when RBAC is disabled.
- Added separate `RecoveryRead` and `Rotate` permissions so helpdesk recovery access and Intune rotation can be delegated independently.
- RBAC principals accept Windows users, groups, and SIDs; local Administrators bypass is configurable.
- Enforced RBAC before local recovery CSV search, AD recovery reveal/copy, Entra recovery retrieval/reveal/copy, and Intune BitLocker key rotation.
- RBAC denials are written to the BitKeyBridge security audit and Windows Application Event Log without recording recovery passwords.
- Added RBAC GUI configuration with principal validation and effective-permission preview for the current Windows identity.
- Added RBAC CLI commands: `--rbac-status`, `--rbac-enable`, `--rbac-disable`, `--rbac-admin-bypass`, reader add/remove, and rotator add/remove.
- Added RBAC configuration health fields and unresolved-principal validation without exposing configured group names through the health endpoint.
- Added SHA-256 tamper-evident hash chaining to new audit JSONL entries while keeping existing legacy audit lines readable.
- Added cross-process audit serialization so GUI and Windows Service writes cannot race the hash chain.
- Added `--audit-verify` and **Verify Chain** in the Audit tab to validate `audit.jsonl` and its rotated `.old` file.
- The native Windows Service now verifies audit integrity at startup and every 24 hours, caches the result for health monitoring, and writes integrity failures to Windows Event Log.
- Added offline self-tests for RBAC backward compatibility, audit hash-chain verification, and deliberate tamper detection.
- Updated the public appsettings example with scheduled Coverage, Coverage Policy, and RBAC settings.

## 0.10.0

- Added automatic LocalMachine certificate private-key ACL management for Windows Service identities.
- Supports both CNG private keys under `%ProgramData%\Microsoft\Crypto\Keys` and legacy CAPI keys under `RSA\MachineKeys`.
- gMSA and regular domain service identities can receive a narrow explicit Read ACE without replacing the existing private-key ACL.
- LocalSystem is recognized as not requiring a separate BitKeyBridge-managed Read grant.
- Service identity changes now perform certificate-access preflight before SCM identity changes when scheduled Coverage depends on the machine certificate.
- Machine cloud save/status now validates the installed service identity's certificate private-key access.
- Added certificate ACL CLI commands: `--cert-key-status`, `--cert-key-grant`, `--cert-key-revoke`, and `--cert-account`.
- Added **Repair Cert Access** to the Dashboard and private-key access state to the health snapshot.
- Added a configurable Coverage Policy engine for No Recovery Key, Intune Not Encrypted, Intune Stale, and Old Cloud Key metrics.
- Each policy metric has an allowed maximum and Error/Warning/Info severity.
- Added Coverage Policy GUI configuration and CLI configuration/status commands.
- Added `--coverage-fail-policy` with exit code 23 for monitoring wrappers.
- Coverage status JSON now persists the evaluated policy result and structured violations.
- Scheduled Coverage maps policy severity to Windows Event Log severity.
- Added a shared unattended Coverage automation service with process-wide serialization to prevent overlapping scheduled/remote runs.
- Added metadata-only Remote API endpoints `GET /api/v1/coverage` and `GET /api/v1/coverage/policy`.
- Added opt-in management endpoint `POST /api/v1/coverage/run`; it never returns recovery passwords or device-level recovery secrets.
- Extended offline self-tests for certificate identity normalization and Coverage Policy evaluation.

## 0.9.0

- Added metadata-only BitLocker Coverage automation for CLI and Task Scheduler workflows.
- Added `--coverage` to generate both CSV and JSON coverage reports from AD, Entra ID, and Intune metadata.
- Added Device Code, certificate, and legacy password authentication selection for coverage CLI.
- Added per-run cloud overrides for tenant ID, client ID, certificate thumbprint, and cloud username without adding a plaintext password command-line option.
- Added `--coverage-output`, `--coverage-csv`, `--coverage-json`, and `--coverage-json-stdout`.
- Added monitoring exit policies: exit 20 for missing recovery metadata, 21 for Intune-reported unencrypted devices, and 22 for stale Intune devices.
- Added offline self-test coverage for coverage monitoring exit codes.
- Added machine-level Entra certificate configuration at `%ProgramData%\BitKeyBridge\cloud_auth_machine.json` for LocalSystem/gMSA/unattended scenarios; the file stores identifiers and a certificate thumbprint only.
- Added `--cloud-machine-status`, `--cloud-machine-save`, `--cloud-machine-delete`, and `--coverage-machine-config`.
- Added optional scheduled Coverage to the native Windows Service with an interval independent from the AD recovery export interval.
- Added Dashboard controls for scheduled Coverage and machine cloud configuration.
- Added Coverage status to the local health snapshot, including last run, age, missing recovery metadata, unencrypted Intune devices, stale Intune devices, and old cloud-key metadata.
- Added service Coverage CLI controls: `--service-coverage-enable`, `--service-coverage-disable`, `--service-coverage-interval`, and run-on-start switches.
- Coverage CSV now uses UTC timestamps and neutralizes spreadsheet formula-like values before export.
- Coverage automation continues to avoid AD recovery-password attributes and the Graph recovery-key value endpoint.

## 0.8.0

- Added a metadata-only BitLocker Coverage Dashboard across Active Directory, Microsoft Entra ID, and Intune.
- Added AD scope inventory queries for computers without reading any BitLocker recovery password.
- Added AD recovery-object metadata queries limited to recovery GUID and creation time.
- Added tenant-wide Entra recovery-key metadata collection without requesting the `key` property.
- Added Intune managed-device inventory correlation for encryption, compliance, last sync, user, serial, manufacturer, model, and OS.
- Added coverage classifications: AD + Entra, AD only, Entra only, and No recovery key.
- Added detection of multiple recovery objects, stale Intune devices, unencrypted Intune devices, and old Entra recovery-key metadata.
- Added Coverage filters and visible-row CSV export.
- Added configurable stale-Intune and old-cloud-key thresholds.
- Coverage auditing records counts/status only and never records a recovery password.
- Increased Intune inventory result capacity for tenant-wide coverage reports.

## 0.7.0

- Added three AD credential modes: Session only, Current User Credential Manager, and Machine / Service DPAPI vault.
- Added Windows Credential Manager storage for interactive per-user AD credentials.
- Added machine-bound DPAPI LocalMachine storage at `%ProgramData%\BitKeyBridge\Secrets\ad-machine.cred`.
- Added protected NTFS ACLs for the machine credential vault (SYSTEM and local Administrators only).
- Added explicit elevation requirement for machine-vault create/delete operations.
- Added GUI controls to save, delete, inspect metadata, and select the AD credential storage mode.
- Added CLI vault operations: `--vault-status`, `--vault-save-user`, `--vault-save-machine`, `--vault-delete-user`, and `--vault-delete-machine`.
- Added native Windows Service identity management for LocalSystem, gMSA/managed service accounts, and regular domain accounts.
- gMSA configuration never accepts or stores a password; Active Directory manages the managed-account password.
- Regular Windows Service account passwords are passed directly to SCM when the identity is changed and are never stored by BitKeyBridge.
- Added service identity to service-status and health output.
- Added a guard preventing unattended Windows Service use with the interactive Current User credential vault.
- Added offline DPAPI LocalMachine protect/unprotect self-test coverage.
- No AD or Windows Service password is added to `appsettings.json`, audit JSONL, Event Log, or release artifacts.

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
