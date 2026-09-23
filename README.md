# BitKeyBridge

BitKeyBridge is a native Windows BitLocker recovery utility for helpdesk and administrators. It searches recovery metadata in on-premises Active Directory and Microsoft Entra ID / Intune, retrieves the actual recovery password only when the operator explicitly requests it, and keeps recovery access auditable.

The application is written in **C# / .NET 10 / WinForms**. Runtime operation does not use PowerShell, the ActiveDirectory PowerShell module, or the Microsoft Graph PowerShell SDK.

Release packages are self-contained single-file Windows builds for **x64**, **x86**, and **ARM64**.

## What changed in 0.17.0

The GUI is intentionally simplified around the real helpdesk workflow.

- **Recovery** — search BitLocker metadata by computer name or Recovery ID and retrieve the selected password only on Reveal/Copy.
- **Devices** — one AD device search with optional Entra/Intune enrichment, Entra recovery-key retrieval, and Intune key rotation.
- **Administration** — Cloud, security/settings, export, and automation. Visible only to authorized BitKeyBridge administrators.
- **Health & Audit** — service/health, domain-controller comparison, and security audit. Visible to the same administrator role.
- **Tools** — consolidated shortcuts for export files/logs/folders, Windows Event Log, and the latest GitHub release.

The previous ten top-level tabs and duplicate Recovery Search / Cloud Search / Unified Devices pages are no longer part of the active UI.

## Highlights

- Live Active Directory recovery lookup over LDAP v3.
- Dynamic writable-DC discovery; no hard-coded DC names.
- Explicit DC / standalone / workgroup operation with optional protected AD credentials.
- Configurable OU scope with remembered last OU and **Entire domain** support.
- **Live AD** and **Local cache** in the same Recovery workspace.
- Metadata-only search: normal search does not read or retain the 48-digit recovery password.
- Exact on-demand password retrieval only after RBAC/JIT/approval checks.
- Single-result recovery card with computer, scope, Recovery ID, timestamp, source, and explicit Reveal/Copy actions.
- Automatic AD connection on GUI startup, enabled by default.
- AD-only Devices search works even when Microsoft Graph is not configured or temporarily unavailable.
- Optional Entra/Intune enrichment and recovery-key rotation.
- Device Code, legacy ROPC, and app-registration + certificate Graph authentication.
- Zero-registration first-run Entra setup with native Graph calls.
- Role-aware GUI navigation.
- Responsive WinForms layouts using TableLayoutPanel / FlowLayoutPanel for DPI, RDP, text scaling, and smaller displays.
- Native Windows Service for scheduled export / coverage.
- Metadata-only AD + Entra + Intune BitLocker Coverage reporting.
- Tamper-evident JSONL recovery audit with optional signed checkpoints.
- Optional JIT recovery, two-person approval, and SIEM forwarding — all disabled by default.
- Optional TLS Remote API with scoped bearer tokens.
- Credential storage using Current User Credential Manager or machine-scope DPAPI with restricted ACL.
- Verified self-update from GitHub Releases with SHA-256 checks, self-test, rollback, SBOM, and attestations.
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

The normal helpdesk workflow is:

1. Open **Recovery**.
2. Keep **Live AD** selected. BitKeyBridge attempts to connect to a writable DC automatically.
3. On first use, choose **Select / Change OU...** and select an OU or **Entire domain**.
4. Enter a computer name or Recovery ID.
5. Click **Search BitLocker**.
6. Search returns metadata only.
7. Select the result. If exactly one result is found, BitKeyBridge goes directly to the selected-record card.
8. Use **Reveal Recovery Key** or **Copy Key** only when the password is actually required.

The selected recovery password is fetched only after the authorization / privileged-access checks pass.

### Local cache fallback

Change **Source** from **Live AD** to **Local cache**.

Local cache uses the administrative recovery-export CSV, but the GUI still searches metadata first and reads only the exact selected password on demand.

The default export location is:

```text
%ProgramData%\BitKeyBridge\RecoveryExport
```

There is no SYSVOL or forced `BL` subdirectory default.

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
- **Security & Settings** — verified updates, Remote API, RBAC, privileged-access policy, diagnostics
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
- **Username + Password** — legacy ROPC mode.
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

BitKeyBridge can check and install verified releases from the configured GitHub repository.

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
