# Changelog

## 0.18.1

- Hardened the helpdesk Recovery workflow after the v0.18 UI review.
- Remembered Recovery OUs are now validated against the connected Active Directory naming context before reuse; stale cross-domain scopes are discarded and replaced with the safe `Entire domain` fallback.
- If a previously saved/selected OU is deleted, renamed, or otherwise stops resolving during live recovery search, BitKeyBridge retries once against `Entire domain` instead of leaving the operator at a dead scope.
- Switching between `Live AD` and `Local cache` now clears prior results, selected recovery metadata, in-memory recovery secret state, cached recovery-access context, and any BitLocker key currently tracked in the clipboard.
- Clipboard cleanup now tracks the exact copied BitLocker secret independently from the currently selected row, so changing selection or closing the application cannot leave an older copied recovery key behind.
- Added an offline self-test for remembered-scope naming-context validation.

## 0.18.0

- Rebuilt every remaining WinForms dialog under `src/BitKeyBridge/UI` with responsive `TableLayoutPanel` / `FlowLayoutPanel` layouts instead of fixed `Left`, `Top`, or `SetBounds` coordinates.
- Updated Input, OU Browser, Recovery Access, Recovery Incident Verification, Remote API Scoped Tokens, Secret Display, Secure Output, RBAC, Privileged Recovery Access, Housekeeping / Protected Storage, and Coverage Policy dialogs for DPI scaling, large text, RDP, and smaller displays.
- Added a CI guard that rejects reintroduction of fixed-position WinForms layout in the UI directory.
- Scoped build-workflow concurrency by commit SHA so a stale architecture publish cannot block a newer release run.
- Recovery startup now falls back to `Entire domain` when no OU is selected, preserving a working metadata search path instead of leaving Search disabled.
- If OU enumeration fails after the Active Directory naming context is known, Recovery continues with the entire-domain scope and surfaces the OU-enumeration warning without blocking recovery search.
- The selected/default recovery scope continues to be persisted through the existing recovery UI state settings.
- Release packages remain self-contained single-file builds for win-x64, win-x86, and win-arm64 with SHA-256 checksums, CycloneDX SBOM, provenance/SBOM attestations, offline self-test, and CodeQL.

## 0.17.1

- Published a fresh patch build from the completed v0.17 UI and recovery workflow.
- Retained the four-section role-aware GUI: Recovery, Devices, Administration, and Health & Audit.
- Retained metadata-only BitLocker search with on-demand recovery-key retrieval and the unified Live AD / local-cache recovery workspace.
- Retained responsive WinForms layouts, administrator RBAC, unified AD + Entra + Intune device search, and the v0.17 security/audit behavior.
- Release packages remain self-contained single-file builds for win-x64, win-x86, and win-arm64 with SHA-256 checksums, CycloneDX SBOM, and attestations.

## 0.17.0

- Replaced the ten top-level GUI tabs with a role-aware four-section shell: **Recovery**, **Devices**, **Administration**, and **Health & Audit**.
- Helpdesk users see only Recovery and Devices; local Windows Administrators and principals in the new `RbacAdministrators` list also see Administration and Health & Audit.
- Added `--rbac-ui-admin-add` and `--rbac-ui-admin-remove` for assigning the Administration UI role from CLI.
- Reworked live AD recovery search to be metadata-only. Normal search no longer reads or holds `msFVE-RecoveryPassword`; the selected recovery object is read only after an explicit Reveal or Copy action passes RBAC/JIT/approval checks.
- Added server-side LDAP filtering for partial Recovery ID lookup so large OU searches no longer enumerate all recovery metadata client-side.
- Added metadata-only local-cache search and exact on-demand CSV recovery-password retrieval, so Live AD and Local cache are now two sources in the same Recovery workspace.
- Removed the separate top-level Recovery Search page and its duplicate key-handling code.
- Added a selected-recovery card. Single-result searches collapse directly to the card with Computer, scope/OU, Recovery ID, time, source, and an explicit **Reveal Recovery Key** action.
- Added automatic AD connection on GUI start, enabled by default, plus persisted last recovery OU and recovery-search source.
- Rebuilt the primary Recovery and Devices workspaces with `TableLayoutPanel` / `FlowLayoutPanel` so they respond cleanly to DPI, text scaling, RDP, and narrower windows.
- Unified device inventory and Entra/Intune recovery operations in **Devices**. AD-only search remains available when Graph is not configured or temporarily unavailable.
- Moved Microsoft Graph/App Registration configuration to **Administration → Cloud**.
- Moved Export, Service, DC comparison, Coverage, Operations, and Audit away from the top level into role-appropriate nested Administration / Health & Audit pages.
- Consolidated Open CSV/Log/Folder/Event Log/Release shortcuts into a **Tools** menu and removed the duplicate buttons from active admin pages.
- Removed obsolete duplicate Directory Connection, Local Recovery Search, Unified Devices, and Cloud Search page builders and handlers from `MainForm`.
- Added schema v4 for helpdesk UI state and the BitKeyBridge administrator role; older configuration is migrated with the existing backup/migration mechanism.
- Added self-tests for schema-v4 defaults, metadata-only cache search, and exact on-demand recovery-password reads.
- Release automation now archives any unmerged `release/*` branch to an `archive/*` tag before deleting the stale release branch after a successful GitHub Release; merged release branches are deleted directly.

## 0.16.1

- Simplified the first GUI tab into a helpdesk-focused BitLocker recovery workflow.
- The normal first-screen flow is now **Connect to AD → Select OU → Search BitLocker → Show/Copy Key**.
- Moved DC/FQDN, domain, LDAP/LDAPS, explicit credentials, credential storage, and vault controls into **Advanced connection settings...**, hidden by default.
- Added a larger computer name / Recovery ID search field and clearer recovery-purpose/status text.
- The first screen no longer looks like a connection/configuration utility; it now presents BitLocker recovery as the primary task.
- Existing advanced AD settings, standalone/workgroup connectivity, protected credential storage, RBAC, JIT recovery, two-person approval, and auditing behavior are preserved.

## 0.16.0

- Added optional time-limited JIT recovery grants. JIT is disabled by default and must be explicitly enabled by an administrator.
- Added optional two-person recovery approval with a distinct authorized Windows approver. The requester cannot approve their own session.
- Added optional metadata-only SIEM forwarding with durable local outbox, JSONL or HTTPS webhook delivery, optional client-certificate authentication, bounded queueing, and explicit fail-open/fail-closed policy.
- JIT grants and approval decisions are anchored to the existing tamper-evident audit chain; invalid or missing retained anchors are rejected.
- Added privileged-access policy enforcement before local AD and Entra recovery-key reveal/copy/get operations.
- Added privileged-access CLI controls for status, JIT grants/revocation, approval decisions, SIEM configuration/status/flush, grantor/approver authorization, and policy enable/disable.
- Added a dedicated **Operations → Privileged Access...** GUI for JIT, two-person approval, SIEM configuration, JIT grant/revoke, approval/deny, readiness checks, and SIEM flush.
- All privileged-access features remain opt-in and are never enabled by upgrade.
- Added shared DPI-aware WinForms behavior across the main window and dialogs, including working-area constraints and scroll fallback for high display scaling, large text, RDP, and smaller screens.
- All main tabs now permit scrolling when DPI/text scaling makes content larger than the visible client area.
- Main GUI title now reads the assembly version instead of a hard-coded version string.
- Reworked the first GUI tab into a guided **BitLocker Recovery Search** workflow: connect to AD, automatically open the OU selector, then search live AD by computer name or Recovery ID.
- Added live scoped AD recovery lookup so the first screen can retrieve matching BitLocker recovery records directly from the selected OU without requiring a prior CSV export.
- Removed legacy SYSVOL and `BL` defaults. New installations use `%ProgramData%\BitKeyBridge\RecoveryExport` as the internal default export root with no forced subdirectory.
- Application configuration schema remains v3 for the optional privileged-access/SIEM settings.

## 0.15.0

- Added evidence-aware housekeeping for recovery incidents, configuration backups, and stale atomic temporary files.
- Incident retention is opt-in: `IncidentRetentionDays = 0` keeps incident bundles forever by default.
- Incident deletion is allowed only when the bundle verifies as `Valid` against the currently retained tamper-evident audit chain.
- Incidents with `NotFullyRetained`, metadata mismatch, missing retained audit anchors, invalid audit state, or other verification failures are preserved.
- Before deleting a verified incident, BitKeyBridge writes a tamper-evident `HousekeepingDeleteIncidentPlan` audit entry containing the bundle SHA-256 digest and correlation/session ID.
- Incident deletion is refused if the authorization audit entry cannot be written.
- After successful deletion, BitKeyBridge writes a completion audit entry referencing the authorization EntryHash; completion-audit failures are surfaced in housekeeping health.
- Backup retention defaults to 90 days while preserving at least the five newest backup JSON files; setting retention to 0 keeps backups forever.
- Added cleanup of stale `.tmp` files left by interrupted atomic writes; default retention is 7 days and 0 disables this cleanup.
- Housekeeping directory-enumeration failures are no longer treated as empty directories; access/I/O problems fail the housekeeping run and surface through health/Event Log.
- Added scheduled housekeeping to the native Windows Service with an independent interval and optional run-on-start behavior.
- Added CLI controls for housekeeping status/run/dry-run, retention settings, minimum backup count, and temporary-file retention.
- Added a Housekeeping / Protected Storage GUI for retention policy, dry run, immediate cleanup, ACL status/repair, and quick access to Incidents/Backups.
- Added protected-storage ACL validation and repair for Incidents, Backups, and AuditSigningTransitions.
- Protected storage ACL repair removes inheritance and unexpected Allow ACEs, keeps FullControl for SYSTEM/local Administrators, grants the installed service identity only the directory-specific access it needs, and keeps audit-signing transition history read-only to the service.
- ACL hardening is explicit/admin-triggered; Windows Service health checks validate the policy but never silently repair ACLs.
- Changing the Windows Service identity now synchronizes protected-storage ACLs with LocalSystem, gMSA, or domain service accounts and avoids restoring stale ACLs after a successful SCM identity switch.
- Added housekeeping/storage-ACL state to `/health/security`, Prometheus `/metrics`, sanitized diagnostics, and Windows Event Log.
- Added application configuration schema v2 for the new housekeeping/storage policy. Existing v1 configuration is migrated with the existing versioned backup/migration mechanism.
- Added offline self-tests for evidence-aware incident deletion, retention preservation, backup minimum retention, temporary-file cleanup, ACL repair/validation, and housekeeping authorization/completion audit records.

## 0.14.0

- Added verification of metadata-only recovery incident bundles against retained tamper-evident audit entries.
- Incident verification validates the audit chain, stable audit snapshot, exact AuditEntryHash anchors, recovery-session CorrelationId, action/result/source/auth metadata, operator/host/device/recovery ID, ticket/reference, reason, and rotation state.
- Incident verification distinguishes a genuinely missing/mismatched audit anchor from `NotFullyRetained`, where an incident action is older than the currently retained audit window.
- Incident verification explicitly detects accidental 48-digit BitLocker recovery-password patterns inside incident JSON.
- Added `--incident-verify <session-id>` with JSON output and distinct exit behavior for valid, not-fully-retained, and failed verification.
- Added **Incident...** to the Audit tab with recent-session selection, verification details, Open Bundle, and Open Incident Folder actions.
- Added a secret-free Prometheus text formatter and loopback-only `/metrics` endpoint on the existing local health listener.
- Prometheus metrics expose operational, Coverage, RBAC, audit-integrity/signing, certificate-lifetime, and Remote API posture gauges without user/computer/ticket/recovery identifiers, token hashes, or recovery secrets.
- Added explicit application configuration schema versioning. Current schema is v1.
- Legacy pre-versioned `appsettings.json` files are upgraded atomically after an exact backup copy is written under the machine Backups directory.
- Read-only/non-admin clients can continue with the effective migrated configuration when migration cannot be persisted; migration is retried later and surfaced through health.
- Configuration files with a future unsupported schema are rejected without modification instead of being silently interpreted with older defaults.
- Added configuration migration status to health and sanitized diagnostics bundles.
- Avoided repeated migration-status writes once the current schema has been recorded.
- Added offline self-tests for incident verification/tamper detection, secret-free metrics, legacy configuration migration/backup, and future-schema refusal.

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
- BitKeyBridge now verifies the complete archived dual-signed transition history, including both signatures, certificate availability, machine identity, chain version, and old→new thumbprint continuity; failures are surfaced through CLI/GUI/security health.
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
