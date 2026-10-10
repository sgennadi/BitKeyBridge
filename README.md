# BitKeyBridge

**Current release line: 0.26.x** — keeps the normal helpdesk path short: Start provides metadata-first BitLocker/LAPS work with direct recovery actions and visible secret lifetimes, while Administration separates everyday Helpdesk settings from optional enterprise policy controls. Access Health, searchable recovery audit history, environment profiles, updater rollback/history and one-click sanitized diagnostics are built in.

BitKeyBridge is a native Windows recovery and administration utility for **BitLocker** and **LAPS** across on-premises Active Directory, Microsoft Entra ID, and Intune.

It provides live AD recovery lookup, Legacy Microsoft LAPS and Windows LAPS current/history access, Entra LAPS, Intune recovery-key rotation, secure auditing, RBAC/JIT approval controls, and administrative health/coverage tooling in one WinForms application.

BitKeyBridge is written in **C# / .NET 10 / WinForms** and directly uses LDAP, Microsoft Graph, Windows APIs, DPAPI-NG, Credential Manager, and the Windows certificate store. Runtime operation does **not** use PowerShell, the ActiveDirectory PowerShell module, the Microsoft Graph PowerShell SDK, or shell fallbacks.

Release packages are self-contained single-file Windows builds for **x64**, **x86**, and **ARM64**.

## Guided setup wizards (0.21.0)

Administration → **Cloud** now includes **Intune / Entra Setup Wizard...** as the recommended first-run path. The wizard checks local prerequisites, asks for the tenant, shows the exact Microsoft Graph capabilities, optionally includes Entra LAPS, then uses the existing native Device Code bootstrap to create/repair the BitKeyBridge App Registration and Enterprise Application, grant admin consent, create/reuse the LocalMachine certificate, and verify certificate authentication. Verification is non-destructive: it checks the app-only token roles, BitLocker metadata access, and the Intune managed-device endpoint; it does not rotate a BitLocker key or retrieve a LAPS password.

Required Graph capabilities configured by the wizard are **BitlockerKey.Read.All**, **Device.Read.All**, and **DeviceManagementManagedDevices.ReadWrite.All**. **DeviceLocalCredential.Read.All** is optional for Entra LAPS. After setup, the wizard verifies the required application roles are present in the issued certificate-authentication token so missing admin consent is reported immediately.

Administration → **Advanced** contains **RBAC Setup Wizard...**, **RBAC Advanced...**, and **Privileged Access...**. RBAC, JIT recovery, two-person approval, SIEM, mandatory Ticket/Reference, audit signing and Remote API are **not enabled by default**. They are opt-in enterprise controls and are never required for the normal helpdesk flow unless an administrator deliberately enables them.

Administration → **Helpdesk** contains the normal recovery settings: setup status, non-secret environment profiles, one-click sanitized support bundles, optional ticket/reference policy, optional post-recovery Intune rotation suggestion, and secret/clipboard lifetime settings. The detailed Cloud **First-Run / Repair** and **Bootstrap...** controls remain available for repair/manual configuration.
## Start helpdesk dashboard (0.24.0)

The GUI opens on **Start**. BitKeyBridge first shows the shared Active Directory session state and automatically connects when possible. Normal credential selection is reduced to **Use current Windows account** or **Use another AD account**; DC/FQDN, domain, LDAP port, LDAPS, protected credential storage and vault actions are kept under **Advanced** on the same Start page.

After connection, one field accepts a partial computer name, Recovery ID, AD object/device ID, or other identifier. Typing two or more characters starts the existing 450 ms debounced metadata search; **Enter** searches immediately. The unified result grid combines BitLocker and LAPS metadata for each computer and shows compact states such as **Available**, **Multiple keys**, **Old key**, **No backup**, **Detected** and **Expired**, plus the source used for the result.

Search remains secret-free. BitLocker recovery passwords are still fetched only after an explicit **Reveal BitLocker Key** / **Copy BitLocker Key** action, and LAPS passwords are fetched only after the explicit **LAPS Passwords** action. When Cloud is configured, **Rotate via Intune** is available as a separate post-recovery action. The selected result shows a compact helpdesk summary and enables only actions that have matching backup metadata. Loaded secrets have a visible lifetime indicator and a **Clear secrets now** action.

**Recent computers** stores only computer/device identifiers, the last helpdesk action and timestamp in the current user's LocalAppData. Recovery keys and LAPS passwords are never written to Recent.

The bottom status bar remains visible across workspaces and shows the current AD session plus BitKeyBridge version. Clicking the connection status returns to Start and opens **Advanced**. The direct Recovery, Devices, LAPS, Administration and Health & Audit tabs remain available for advanced, Local-cache and Entra/Intune workflows.

## Helpdesk defaults and Access Health

BitKeyBridge 0.25 keeps enterprise enforcement out of the default recovery path. A fresh configuration does **not** enable RBAC, JIT recovery, two-person approval, SIEM, mandatory Ticket/Reference, audit signing, Remote API, Coverage policy/service Coverage, storage ACL hardening, or trusted-update-signature enforcement. Administrators can opt into those controls under **Administration → Advanced** or the relevant Updates/Audit page.

**Administration → Helpdesk** is the normal configuration surface. It provides an at-a-glance setup status, non-secret environment profiles, optional Ticket/Reference rules (including an optional regex format), optional post-recovery Intune rotation suggestion, configurable secret/clipboard lifetimes, and a one-click sanitized support bundle.

**Health & Audit → Access Health** runs metadata-only checks for AD bind, AD BitLocker recovery-object visibility, AD LAPS, Microsoft Graph BitLocker metadata, Intune managed-device visibility and Entra LAPS. Access Health does not request a BitLocker recovery password or LAPS password.

The Audit page can filter recent recovery activity by computer, ticket/reference, operator, action, Recovery ID, source and date range. Recovery passwords and LAPS passwords are never written to the audit trail.

Environment profiles store only non-secret endpoint/preferences such as AD/DC/domain/LDAPS and Cloud tenant/client/certificate selection. They do not store passwords, tokens, BitLocker recovery keys or LAPS passwords.

## LAPS and manual AD connections

The **LAPS** workspace reads Legacy Microsoft LAPS, Windows LAPS plaintext/encrypted current passwords, encrypted AD history, DSRM passwords/history, and Entra credentials/history. Type part of a computer/device name or ID for debounced metadata search. Select a device and press **Enter** or double-click to read LAPS immediately; pressing Enter on an exact/single search result also proceeds directly to the read. The **Search** button remains metadata-only and never reads a password. Discovery results are shown in a sortable DataGridView with device ID, key date metadata when available, latest marker and source. The password/history grid is also sortable and shows source/version, account, key date, latest/current marker, age, expiry, and read/decryption status. A single returned credential row is selected automatically while the password remains masked. Expired current credentials are marked **Available / Expired** with a rotation-overdue warning. **Copy account** is separate from **Reveal password** / **Copy password**, and secret actions stay disabled for records that were not successfully read or decrypted. The AD logical password-version GUID is shown only when the Windows Server 2025 attribute is actually available.

For normal on-premises work, connect from **Start**. Choose the current Windows account or another AD account, then use **Connect to AD**. The manual credential prompt remains available when a session password is needed. **Start → Advanced** contains DC/FQDN, domain, port, LDAPS and protected Credential Manager / machine-DPAPI storage. A selected DC determines the remote domain even on a workstation joined to another domain. **Disconnect** clears only the in-memory session identity and loaded Recovery/LAPS state; it does not delete protected stored credentials.

Legacy LAPS and unencrypted Windows LAPS do not provide password history. Windows LAPS local-account and DSRM history require enabled AD encryption/history retention. Reading encrypted attributes and decrypting them are separate AD permissions; missing attributes can also mean insufficient read access. BitKeyBridge uses native LDAP, Windows identity impersonation and `NCryptUnprotectSecret`; it does not invoke a shell or require LAPS management modules.

For Entra, enter the **device ID**, not the object ID. The caller needs **DeviceLocalCredential.Read.All** and an applicable Entra role/admin consent. Opt in to **Include LAPS read permissions in setup** on Administration → Cloud before First-Run / Repair to provision the application's delegated/application permissions. Existing permissions are preserved when the option is disabled. LAPS requests its own read scopes and does not depend on a successful BitLocker access test.

LAPS Read/Reveal/Copy use the existing RecoveryRead role, optional privileged-access policies and audit trail. Passwords are never included in exports, settings or diagnostics. Recovery/LAPS failures use non-blocking inline diagnostics with a redacted **Copy diagnostics** action instead of blocking error dialogs. Loaded passwords clear after the configured secret lifetime (120 seconds by default), on source/computer/account changes, on reconnect, on explicit clearing, and when the window closes. Sensitive clipboard copies are excluded from Windows clipboard history/cloud processing, show a visible countdown, and are cleared after the configured timeout (60 seconds by default) only if the same secret is still present. Only retained records returned by AD/Entra can be displayed.

Technical details and permission references: [LAPS guide](docs/LAPS.md).

For `Access denied`, missing BitLocker recovery objects, unreadable LAPS attributes, or encrypted LAPS/history decryption failures, see [AD access troubleshooting](docs/ACCESS-TROUBLESHOOTING.md). The same guidance is available in the GUI through **AD access help...**.

## Current GUI structure

The GUI is intentionally simplified around the real helpdesk workflow.

- **Start** — default helpdesk dashboard with shared AD connection state, unified BitLocker/LAPS metadata search, context-aware actions, Recent computers, simple account choice and Start-owned Advanced connection settings.
- **Recovery** — advanced BitLocker workspace with Live AD/Local cache, OU scope, debounced partial-name/Recovery-ID search, latest-record selection and on-demand Reveal/Copy.
- **Devices** — one AD device search with optional Entra/Intune enrichment, Entra recovery-key retrieval, and Intune key rotation.
- **LAPS** — debounced device discovery, direct Enter/double-click reading, compact status/results and optional technical Details.
- **Administration** — **Helpdesk** basics/profiles/support bundle, Cloud, Updates, **Advanced** enterprise policies, export, and automation. Visible only to authorized BitKeyBridge administrators.
- **Health & Audit** — **Access Health** metadata-only permission checks, service/health, domain-controller comparison, and searchable recovery audit history. Visible to the same administrator role.
- **Tools** — consolidated shortcuts for export files/logs/folders, Windows Event Log, and the latest GitHub release.

The previous ten top-level tabs and duplicate Recovery Search / Cloud Search / Unified Devices pages are no longer part of the active UI.

## Highlights

- Live Active Directory recovery lookup over LDAP v3.
- Debounced live BitLocker and LAPS search (450 ms), with Enter for immediate search.
- Partial computer-name and punctuation-insensitive identifier matching.
- Sortable DataGridView results with key date and latest-key markers where metadata allows it.
- Dynamic writable-DC discovery; no hard-coded DC names.
- Explicit DC / standalone / workgroup operation with optional protected AD credentials.
- Configurable OU scope with remembered last OU and **Entire domain** support.
- **Live AD** and **Local cache** in the same Recovery workspace.
- Metadata-only search: normal search does not read or retain the 48-digit recovery password.
- Exact on-demand password retrieval only after the configured authorization checks; RBAC/JIT/two-person approval are optional and disabled by default.
- Single-result recovery card with computer, scope, Recovery ID, timestamp, source, and explicit Reveal/Copy actions.
- Automatic AD connection on GUI startup, enabled by default.
- AD-only Devices search works even when Microsoft Graph is not configured or temporarily unavailable.
- Optional Entra/Intune enrichment and recovery-key rotation.
- Device Code and app-registration + certificate Graph authentication. Password/ROPC remains available only as a **deprecated compatibility mode** and is not recommended.
- Zero-registration first-run Entra setup with native Graph calls.
- Role-aware GUI navigation.
- Responsive WinForms layouts using TableLayoutPanel / FlowLayoutPanel for DPI, RDP, text scaling, and smaller displays.
- Native Windows Service for scheduled export / coverage.
- Metadata-only AD + Entra + Intune BitLocker Coverage reporting.
- Tamper-evident JSONL recovery audit with optional signed checkpoints and GUI filters by computer/ticket/user/action/Recovery ID/date.
- Optional RBAC, mandatory ticket/reference, JIT recovery, two-person approval, SIEM forwarding, audit signing and Remote API — all disabled by default.
- Optional TLS Remote API with scoped bearer tokens.
- Credential storage using Current User Credential Manager or machine-scope DPAPI with restricted ACL.
- Verified self-update from GitHub Releases with SHA-256 checks, staged self-test, update history, retry, one-click rollback, SBOM and attestations; optional Authenticode trust/publisher policy is available and disabled by default.
- Release automation removes stale `release/*` branches only after preserving any unmerged branch tip under an `archive/*` tag.

## Platform

- Windows 11 / Windows Server environments supported by .NET 10 WinForms.
- Primary server/domain-controller release: **win-x64**.
- Additional **win-x86** and **win-arm64** packages are published.
- Destination machines do not need a separate .NET installation when using the self-contained release package.
- AD features work on a domain controller, domain-joined workstation, or standalone/workgroup Windows computer.
- Entra / Intune features do not require domain membership.

## First run

Run:

```text
BitKeyBridge.exe
```

The normal helpdesk path starts on **Start**.

### Connect to Active Directory

For on-premises BitLocker or LAPS work:

1. Stay on **Start**.
2. Choose **Use current Windows account** or **Use another AD account**.
3. Let auto-connect run, or click **Connect to AD**. If a session password is required, BitKeyBridge opens the credential prompt.
4. Use **Advanced** only when you need to choose a DC/FQDN, domain, port, LDAPS or protected credential-storage mode.
5. After a successful bind, the Start status and persistent bottom status bar show the effective connection.
6. **Disconnect** clears only in-memory session credentials and loaded secrets; it does not delete protected stored credentials.

### Unified Start search

1. Enter part of a computer name, Recovery ID or device/object ID.
2. Wait for live search or press **Enter** immediately.
3. Select the computer. BitKeyBridge shows BitLocker and LAPS availability in the same row.
4. Choose **BitLocker Recovery** or **LAPS Passwords**. An unavailable action is disabled instead of sending you to an empty workflow.
5. Double-click/Enter on a Start result follows the unambiguous action automatically; when both BitLocker and LAPS are available, BitKeyBridge asks you to choose.

The Start search reads metadata only and does not retrieve a BitLocker recovery password or LAPS password.

### BitLocker recovery

1. Open **Recovery**.
2. Choose **Live AD** or **Local cache**.
3. For Live AD, choose **Select / Change OU...** and select an OU or **Entire domain** when required.
4. Enter a computer name or Recovery ID.
5. Click **Search BitLocker**.
6. Search returns recovery metadata only; the 48-digit password is not read during normal search.
7. Select the result. A single result opens directly in the selected-record card.
8. Use **Reveal Recovery Key** or **Copy Key** only when the password is actually required.

The selected recovery password is fetched only after the authorization / privileged-access checks pass.

### LAPS passwords and history

1. Use the unified **Start** search and choose **LAPS Passwords**, or open **LAPS** directly.
2. In LAPS, choose **Active Directory (Legacy / Windows LAPS / DSRM)** or **Microsoft Entra ID**.
3. Type part of the computer/device identifier; live search is metadata-only.
4. Select a device and press **Enter** or double-click to read LAPS. An exact/single result can proceed directly.
5. Keep **Include password history** enabled when Windows LAPS encrypted history/DSRM history is required.
6. Use **All / Current / History** to filter returned records.
7. Select a record and use **Reveal password**, **Copy password**, or **Copy account** as required.
8. Technical details stay collapsed after normal success and open automatically for read/decrypt/data failures.
9. If access is denied or attributes/history are unavailable, use **AD access help...** for targeted delegation guidance and example commands.

Legacy Microsoft LAPS and plaintext Windows LAPS do not provide password history. Encrypted Windows LAPS history also requires history retention and a decryptor principal configured by Windows LAPS policy.

### Devices / Entra / Intune

1. Open **Devices → Search**.
2. Search by computer name, serial number, user/UPN, Entra device ID, or Intune managed-device ID.
3. AD results remain available even when Microsoft Graph is not connected.
4. When Graph is configured, BitKeyBridge enriches results with Entra/Intune data.
5. Retrieve an Entra BitLocker recovery key only when required.
6. Use **Rotate BitLocker Key** for an explicit Intune rotation request; rotation is never performed automatically.

For first-time cloud configuration, administrators should use **Administration → Cloud → Intune / Entra Setup Wizard...**. Normal helpdesk policy belongs under **Administration → Helpdesk**; optional RBAC/JIT/approval/SIEM controls are under **Administration → Advanced**.

### Local cache fallback

Change **Source** from **Live AD** to **Local cache**.

Local cache uses the administrative recovery-export CSV, but the GUI still searches metadata first and reads only the exact selected password on demand.

The default export location is:

```text
%ProgramData%\BitKeyBridge\RecoveryExport
```

## GUI sections

### Recovery

The main helpdesk screen contains only the normal recovery workflow.

**Advanced connection settings...** contains:

- Auto or Explicit DC mode
- DC/FQDN
- domain
- LDAP / LDAPS port
- explicit AD credentials
- Credential Manager / machine DPAPI storage selection
- Auto-connect on startup

Export paths are intentionally not on the Recovery screen.

### Devices

Search by:

- computer name
- serial number
- user / UPN
- Entra device ID
- Intune managed-device ID

When Microsoft Graph is connected, results merge AD, Entra BitLocker metadata, and Intune inventory.

When Graph is unavailable, the same page continues with AD-only results.

For Entra recovery access, choose a Recovery ID and explicitly retrieve the key. Intune rotation always requires a separate confirmed action.

### Administration

Visible to:

- local Windows Administrators, or
- users/groups listed in `RbacAdministrators`.

Contains:

- **Cloud** — Entra / Intune authentication and App Registration management
- **Updates** — verified updater, manual check/update, release channel and automatic-check preference
- **Security & Settings** — Remote API, RBAC, privileged-access policy, diagnostics
- **Export & Automation** — output storage, export scopes, offline cache export

### Health & Audit

Contains:

- Windows Service / health configuration
- domain-controller comparison and replication diagnostics
- tamper-evident recovery audit controls

### Tools

The Tools menu consolidates shortcuts that used to occupy separate buttons:

- recovery export CSV
- export log
- export folder
- Windows Event Log
- latest GitHub release

## Active Directory connection

For domain-joined machines, Auto mode discovers a writable domain controller and normally uses the current Windows identity.

For standalone/workgroup operation, open **Recovery → Advanced connection settings...** and configure an explicit DC.

Supported credential modes:

- **Session only** — password exists only in the current BitKeyBridge process.
- **Current User - Credential Manager** — interactive-user storage.
- **Machine / Service - DPAPI** — machine-bound DPAPI blob under ProgramData with restricted ACL.

BitKeyBridge does not provide a plaintext `--ad-password` CLI argument.

Secure interactive CLI example:

```text
BitKeyBridge.exe --ad-test --ad-server dc01.example.com --ad-domain example.com --ad-user EXAMPLE\admin --ad-password-prompt --ad-ldaps
```

A non-admin helpdesk session can still use connection settings in memory even when it cannot persist machine-wide appsettings.

## Microsoft Entra / Intune

Configure Microsoft Graph under **Administration → Cloud**.

Authentication modes:

- **Device Code** — recommended interactive mode for MFA / Conditional Access.
- **Username + Password (ROPC)** — **deprecated compatibility mode**. It doesn't support MFA, is commonly blocked by Conditional Access, and should be replaced by Device Code or certificate authentication.
- **App registration + certificate** — recommended unattended/service mode.

Runtime Graph permissions are limited to the permissions required by the tool, including BitLocker metadata/key access, device reads, and Intune managed-device rotation.

### First-Run / Repair

A pre-created BitKeyBridge App Registration is not required.

1. Open **Administration → Cloud**.
2. Click **First-Run / Repair**.
3. Complete the Device Code sign-in with an Entra administrator.
4. BitKeyBridge creates or repairs its dedicated app registration / service principal, required Graph permissions, local certificate, and saved identifiers.
5. Administrator passwords, access tokens, and certificate private keys are not written to configuration.

Certificate rollover adds and verifies the new credential before switching configuration and retains the previous credential for rollback/grace.

## Recovery-secret security model

BitKeyBridge treats the recovery password differently from ordinary metadata.

Normal Live AD search requests:

- computer information
- Recovery ID
- recovery-object timestamp
- recovery-object DN

It does **not** request `msFVE-RecoveryPassword`.

The password is requested only for the selected recovery object after the explicit Reveal/Copy workflow passes configured controls.

Local-cache search follows the same model: metadata is scanned first, then the exact selected CSV row is reread only when the password is requested.

Recovery passwords are never intentionally written to:

- appsettings
- cloud-auth configuration
- JSONL audit
- Windows Event Log
- incident bundles
- diagnostics bundles
- SIEM events
- GitHub Actions artifacts

Clipboard content is automatically cleared when the configured timed-clear logic still finds the copied key on the clipboard.

## Windows RBAC

RBAC is disabled by default for backward compatibility.

Permissions include:

- **RecoveryRead** — reveal/copy/retrieve recovery passwords.
- **Rotate** — submit Intune BitLocker rotation.
- **Administrator** — display Administration and Health & Audit sections.
- **JitGrant** — grant time-limited recovery access when JIT is enabled.
- **RecoveryApprove** — approve another operator's recovery session when two-person approval is enabled.

Example:

```text
BitKeyBridge.exe --rbac-reader-add "DOMAIN\BitLocker Helpdesk"
BitKeyBridge.exe --rbac-rotator-add "DOMAIN\BitLocker Rotation Operators"
BitKeyBridge.exe --rbac-ui-admin-add "DOMAIN\BitKeyBridge Admins"
BitKeyBridge.exe --rbac-enable
BitKeyBridge.exe --rbac-status
```

The navigation role is evaluated when the GUI starts.

## Optional privileged recovery controls

These features are **off by default** and upgrades do not enable them:

- **JIT Recovery** — time-limited recovery grants.
- **Two-person approval** — a distinct authorized approver must approve the recovery session.
- **SIEM forwarding** — metadata-only JSONL or HTTPS webhook delivery with durable local outbox.

GUI configuration:

**Administration → Security & Settings → Privileged Access...**

CLI examples:

```text
BitKeyBridge.exe --privileged-status
BitKeyBridge.exe --jit-enable --jit-minutes 15
BitKeyBridge.exe --approval-enable --approval-minutes 15
BitKeyBridge.exe --siem-status
```

Use `BitKeyBridge.exe --help` for the full command set.

## Recovery export / offline cache

Administrative export is separate from live recovery lookup.

GUI:

**Administration → Export & Automation**

Normal CLI export:

```text
BitKeyBridge.exe --cli
```

Dry run:

```text
BitKeyBridge.exe --dry-run
```

Intentional publish after reviewing row/scope safety guards:

```text
BitKeyBridge.exe --cli --force-publish
```

Standalone export example:

```text
BitKeyBridge.exe --cli --ad-server dc01.example.com --ad-domain example.com --ad-user EXAMPLE\admin --ad-password-prompt --output-root "\\fileserver\secure\BitLocker"
```

## BitLocker Coverage

Coverage is metadata-only and never requests the recovery password.

GUI:

**Devices → Coverage** (administrator role)

CLI:

```text
BitKeyBridge.exe --coverage
```

Useful policy flags include:

```text
--coverage-fail-no-key
--coverage-fail-unencrypted
--coverage-fail-stale
--coverage-fail-policy
```

For unattended runs, certificate authentication is recommended:

```text
BitKeyBridge.exe --coverage --cloud-auth Certificate --tenant-id <tenant-guid> --client-id <app-guid> --cert-thumbprint <thumbprint>
```

## Windows Service and health

Install/update the service:

```text
BitKeyBridge.exe --install-service
```

Service lifecycle:

```text
BitKeyBridge.exe --service-status
BitKeyBridge.exe --start-service
BitKeyBridge.exe --stop-service
BitKeyBridge.exe --uninstall-service
```

The optional loopback health endpoint is intended for local monitoring integrations.

The service can run as:

- LocalSystem
- gMSA / managed account
- regular domain account

For unattended Graph access, use machine cloud configuration with a LocalMachine certificate.

## Remote API

The Remote API is disabled by default.

When enabled it requires TLS and bearer-token authentication. Scoped tokens are available for:

- read
- coverage-run
- export
- admin

The Remote API does not expose BitLocker recovery passwords.

## Audit and incident evidence

Recovery actions are recorded in local JSONL security audit.

Audit entries are SHA-256 chained. Optional LocalMachine certificate checkpoints can sign the current chain head.

Recovery sessions can also create metadata-only incident bundles keyed by Session/Correlation ID.

The audit / incident subsystems redact strings matching the 48-digit BitLocker password format.

Useful commands:

```text
BitKeyBridge.exe --audit-verify
BitKeyBridge.exe --audit-signing-status
BitKeyBridge.exe --incident-verify <session-id>
```

## Housekeeping and protected storage

Housekeeping can manage:

- verified incident retention
- configuration-backup retention
- stale atomic-write temporary files

Incident retention defaults to **keep forever** unless explicitly configured.

Protected storage ACL checks/repair are available for incident, backup, and signing-transition directories.

```text
BitKeyBridge.exe --housekeeping-status
BitKeyBridge.exe --housekeeping-dry-run
BitKeyBridge.exe --storage-acl-status
```

## Configuration

Machine configuration:

```text
%ProgramData%\BitKeyBridge\appsettings.json
```

Per-user cloud metadata:

```text
%LOCALAPPDATA%\BitKeyBridge\cloud_auth_config.json
```

Machine/service cloud metadata:

```text
%ProgramData%\BitKeyBridge\cloud_auth_machine.json
```

Schema **v4** adds:

- `AutoConnectOnStart`
- `RecoverySearchSource`
- remembered Recovery OU
- `RbacAdministrators`

Older configurations are migrated using the existing backup-and-migrate mechanism.

## Verified updates and supply-chain security

BitKeyBridge can check and install verified releases from the configured GitHub repository. The GUI controls are in **Administration → Updates**. **Check for Updates** and **Update Now** remain available even when automatic startup checks are disabled.

CLI provides the same controls:

```text
BitKeyBridge.exe --check-update
BitKeyBridge.exe --update
BitKeyBridge.exe --update-checks-status
BitKeyBridge.exe --update-checks-enable
BitKeyBridge.exe --update-checks-disable
```

Release CI builds:

- win-x64
- win-x86
- win-arm64

Release metadata includes:

- SHA256SUMS
- CycloneDX SBOM
- GitHub provenance attestation
- SBOM attestation

The updater verifies architecture and SHA-256, stages the new executable, runs `--self-test`, and supports rollback.

After a successful release, stale `release/*` branches are cleaned automatically. An unmerged branch is first preserved as an `archive/*` tag before its branch ref is deleted.

## CLI quick reference

Full help:

```text
BitKeyBridge.exe --help
```

Common commands:

```text
BitKeyBridge.exe --version
BitKeyBridge.exe --self-test
BitKeyBridge.exe --ad-test
BitKeyBridge.exe --dc-test
BitKeyBridge.exe --health
BitKeyBridge.exe --coverage
BitKeyBridge.exe --rbac-status
BitKeyBridge.exe --privileged-status
BitKeyBridge.exe --check-update
BitKeyBridge.exe --update
BitKeyBridge.exe --update-checks-status
BitKeyBridge.exe --update-checks-enable
BitKeyBridge.exe --update-checks-disable
```

## Build

Requirements:

- .NET 10 SDK
- Windows targeting enabled

Build:

```text
dotnet restore src\BitKeyBridge\BitKeyBridge.csproj
dotnet build src\BitKeyBridge\BitKeyBridge.csproj -c Release
```

Publish x64:

```text
dotnet publish src\BitKeyBridge\BitKeyBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The GitHub workflow also publishes win-x86 and win-arm64.

## Repository layout

```text
src/BitKeyBridge/
  Core/
  Infrastructure/
  Services/
  UI/
.github/workflows/
config/
```

## Privacy / deployment configuration

Do not commit real domain names, OU distinguished names, usernames, tenant IDs, certificate thumbprints, recovery IDs, recovery passwords, bearer tokens, or internal UNC paths.

Use `config/appsettings.example.json` as a template and keep environment-specific values outside the repository.

## License

See the repository license file.

### Empty LAPS results (0.25.3)

When an AD LAPS lookup returns metadata but no readable password attributes, the LAPS tab displays an immediate yellow warning above the results list. Diagnostic steps and commands... opens a copyable guide to workstation policy/event-log checks, AD OU read delegation, operator groups and separate Windows LAPS encrypted-password decryption rights. The selected AD computer DN and DC are used automatically. Entra-only lookups get Graph/role/backup-policy checks. Commands are not executed automatically and do not change directory ACLs, rotate passwords or enable enterprise policies.

### Workstation environment checks and BitLocker diagnostics (0.25.4)

Use **Start > Environment Check...** to check AD/DC DNS, selected LDAP/LDAPS TCP port, Kerberos/RPC/SMB TCP reachability, authenticated LDAP bind and optional BitLocker/LAPS metadata for an exact computer. No domain membership, RSAT, separate .NET runtime or PowerShell modules are needed to use the portable GUI. Cloud-configured installations additionally check identity/Graph TCP 443. KDS/decryption, remote DC services and confidential-attribute permissions remain **NotVerified** without separate authorized checks; open ports never imply permission.

**Recovery** displays a yellow, above-grid warning when BitLocker metadata is absent, a local cache is missing, a search fails or a selected password could not be retrieved. **Diagnostic steps and commands...** opens read-only, copyable instructions for the affected Windows computer, DC/RSAT and the operator account, scoped to the selected computer, Recovery ID, OU and configured DC. A selected computer in **Start** also offers **BitLocker diagnostics...** when zero keys were returned. AD, local cache and Entra each have dedicated instructions. No commands run automatically; the optional secret-bearing AD read test is explicitly labeled.

See [Workstation prerequisites and diagnostics](docs/ENVIRONMENT-DIAGNOSTICS.md).

## Advanced Diagnostics Center (0.26.0)

**Start > Device Diagnostics...** adds per-computer multi-DC BitLocker/LAPS metadata consistency, evidence-graded Smart Diagnostic Engine, read-only AD ACE/DACL permission inspection, optional LDAP/SRV/site/strict LDAPS certificate validation and user-confirmed writable DC selection. Compare on-prem AD with Microsoft Entra BitLocker metadata by exact unique Entra device ID, without reading recovery passwords.

Run **Collect endpoint ZIP** directly on the problem workstation (also `BitKeyBridge.exe --collect-endpoint --output C:\Temp\endpoint.zip`) and import the resulting ZIP on an admin PC. The report contains only whitelisted non-secret Windows policy metadata and event IDs; never event messages, keys, tokens or password data.

**Protect recovery CSV** creates a DPAPI CurrentUser-protected .bkb sidecar from an existing export and enables **Recovery > Protected cache** for local metadata-first search and authorized selected-secret reads. The plaintext original CSV remains unchanged and requires separate administrative retirement. The encrypted cache is tied to its original Windows identity.

**Verify EXE signature** inspects the running EXE. GitHub Actions can optionally sign published binaries when your organization provisions a real Code Signing certificate/private key on the runner; otherwise they remain unsigned. The trusted-signature/publisher enforcement options are unchanged and disabled by default.

See [Advanced Diagnostics](docs/ADVANCED-DIAGNOSTICS.md) and [Code signing](SIGNING.md).
