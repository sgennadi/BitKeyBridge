# BitKeyBridge

Native Windows administration utility for BitLocker recovery information in on-premises Active Directory and Microsoft Entra ID / Intune.

The application is written in **C# / .NET 10 LTS / WinForms**. Runtime operation does **not** use PowerShell, the ActiveDirectory PowerShell module, or the Microsoft Graph PowerShell SDK.

The release is a **self-contained .NET single-file Windows executable**, not NativeAOT. "Native" here means the application calls Windows/LDAP/Graph APIs directly instead of shelling out to PowerShell. This keeps WinForms, DirectoryServices, and enterprise debugging/support straightforward.

## Highlights

- Self-contained single-file Windows builds for x64, x86, and ARM64 (`BitKeyBridge.exe` in each architecture package).
- GUI, native Windows Service host, and non-interactive CLI in the same executable.
- Reads `msFVE-RecoveryInformation` directly over LDAP v3 using the current Windows credentials.
- No destructive AD operations. The exporter never deletes or changes BitLocker objects.
- Dynamic domain-controller discovery; no hard-coded DC names.
- AD replication-health view and recovery-object count comparison across current DCs.
- Configurable OU scopes with a GUI OU browser.
- Atomic verified CSV publishing with partial-export, empty-export, row-drop, and scope-change safety guards.
- Last-success and status JSON files for monitoring.
- Local recovery search with masked key display and timed clipboard clearing.
- Microsoft Graph BitLocker metadata search.
- Recovery password requested from Entra only on explicit **Get Key** action.
- Graph authentication by Device Code (MFA / Conditional Access), legacy username/password (ROPC), or app registration + certificate.
- Zero-registration first-run Entra setup using Microsoft's first-party Device Code bootstrap; no pre-created App Registration, PowerShell, or Graph SDK is required.
- Unified device search across on-prem AD, Entra BitLocker metadata, and Intune managed devices.
- Intune BitLocker recovery-key rotation with explicit confirmation.
- Local JSONL security audit for key reveal/copy/retrieval/rotation events; recovery passwords are redacted and never written to the audit log.
- Certificate rolling preserves active credentials when the previously managed private certificate is available.
- Health Dashboard with service/export/replication/certificate status.
- Native Windows Service mode with scheduled exports and no PowerShell dependency.
- Loopback-only JSON health endpoint for monitoring systems.
- Secure Output Wizard for protected NTFS recovery storage and optional restricted SMB shares.

## Platform

- Primary Windows Server / domain-controller build: **win-x64**. Additional **win-x86** and **win-arm64** packages are produced for Windows devices that need those architectures.
- .NET is not required on the destination machine when using the self-contained release build.
- AD features require the machine to be domain joined and the running account to have permission to read the selected OUs.
- The default output root is `C:\Windows\SYSVOL\domain\scripts`, so the default configuration is intended for a domain controller. It can be changed in `%ProgramData%\BitKeyBridge\appsettings.json`.

## First run

Run `BitKeyBridge.exe` as an administrator. The app self-elevates through UAC when required.

On a clean public build, no organization-specific OU is embedded. In the **Export** tab:

1. Click **Add OU...**.
2. Select one or more OUs.
3. Click **Save defaults**.
4. Run **Dry Run** first.
5. Review the DC comparison and security warning before publishing.

Machine configuration is stored at:

```text
%ProgramData%\BitKeyBridge\appsettings.json
```

Per-user Entra authentication metadata is stored at:

```text
%LOCALAPPDATA%\BitKeyBridge\cloud_auth_config.json
```

Passwords and recovery passwords are never written to the cloud-auth config.

## CLI

Normal export for Task Scheduler:

```text
BitKeyBridge.exe --cli
```

Dry run:

```text
BitKeyBridge.exe --dry-run
```

Intentional publish after reviewing a scope change or large row-count reduction:

```text
BitKeyBridge.exe --cli --force-publish
```

Run offline smoke tests (no AD/Graph access):

```text
BitKeyBridge.exe --self-test
```

Test all currently discovered domain controllers:

```text
BitKeyBridge.exe --dc-test
```

Show the local health snapshot as JSON:

```text
BitKeyBridge.exe --health
```

Install/update and start the native Windows Service:

```text
BitKeyBridge.exe --install-service
```

Service lifecycle commands:

```text
BitKeyBridge.exe --service-status
BitKeyBridge.exe --start-service
BitKeyBridge.exe --stop-service
BitKeyBridge.exe --uninstall-service
```

Override the saved scopes for one run (repeat the option for multiple OUs):

```text
BitKeyBridge.exe --dry-run --search-base "OU=Workstations,DC=example,DC=com"
```

## Microsoft Entra / Intune

The **Entra / Intune Cloud** tab supports:

- recommended interactive Device Code authentication with MFA / Conditional Access;
- legacy ROPC/manual authentication;
- certificate authentication for unattended use;
- delegated `BitlockerKey.Read.All`, `Device.Read.All`, and `DeviceManagementManagedDevices.ReadWrite.All`;
- application `BitlockerKey.Read.All`, `Device.Read.All`, and `DeviceManagementManagedDevices.ReadWrite.All` for certificate mode.

The actual 48-digit recovery password is not downloaded during search. It is requested only after selecting a result and clicking **Get Key from Entra**.

### First-Run / Repair Entra Setup

A pre-created App Registration is **not required**.

On a completely clean tenant-side setup:

1. Open **Entra / Intune Cloud**.
2. Leave **Client ID** empty.
3. Click **First-Run / Repair Setup**.
4. BitKeyBridge uses Microsoft's first-party **Microsoft Graph Command Line Tools** public client only for the temporary Device Code bootstrap.
5. Sign in with an Entra administrator account and approve the requested management permissions.
6. BitKeyBridge creates or repairs its own dedicated **BitKeyBridge** App Registration, Enterprise Application, Graph permissions, delegated admin-consent grant, and local certificate.
7. The generated Client ID, tenant ID, and certificate thumbprint are saved automatically. Administrator passwords and access tokens are not stored.

The temporary bootstrap requests `Application.ReadWrite.All`, `AppRoleAssignment.ReadWrite.All`, and `DelegatedPermissionGrant.ReadWrite.All` only for the interactive setup session. These management permissions are **not** granted to the BitKeyBridge application itself.

The runtime BitKeyBridge application receives only the Graph permissions required by the tool: `BitlockerKey.Read.All`, `Device.Read.All`, and `DeviceManagementManagedDevices.ReadWrite.All`.

If Conditional Access blocks Microsoft's first-party bootstrap in a specific tenant, use the advanced **Bootstrap...** button to specify a tenant-approved public-client Application ID. The normal case requires no manual Application ID.

Graph permission identifiers are resolved dynamically from the tenant's Microsoft Graph service principal rather than being hard-coded. The setup also retries transient Graph throttling/service errors and allows for new service-principal propagation.

Use a dedicated application for this tool. The setup merges API permissions instead of replacing unrelated permissions. Certificate rotation uses Graph key-rolling semantics when an existing managed valid certificate is present.

## Security notes

BitLocker recovery passwords are secrets.

- Do not publish recovery CSV files to source control.
- Review ACLs on the output directory. The application warns about broad read access granted to Everyone, Authenticated Users, BUILTIN\\Users, or Domain Users.
- SYSVOL/NETLOGON is convenient for legacy WinPE workflows but should not be broadly readable when it contains recovery passwords. A dedicated restricted share is preferred.
- Device Code is the preferred interactive mode because it can satisfy MFA and Conditional Access. ROPC is legacy only. Certificate authentication is the intended unattended mode.
- The local security audit is stored at `%ProgramData%\BitKeyBridge\audit.jsonl` and never stores a BitLocker recovery password.
- The monitoring endpoint is loopback-only and never returns recovery passwords or authentication secrets.
- The Windows Service executable is installed under `%ProgramFiles%\BitKeyBridge`; configuration and service logs remain under `%ProgramData%\BitKeyBridge`.
- Cloud recovery-key reads are auditable in Microsoft Entra.

## Dashboard, Windows Service and monitoring

BitKeyBridge can run its AD export engine as a native Windows Service. **Install / Update** from the Dashboard copies the current single-file executable to:

```text
%ProgramFiles%\BitKeyBridge\BitKeyBridge.exe
```

and registers the **BitKeyBridge** service with Windows Service Control Manager. The service runs as LocalSystem, uses the same machine configuration in `%ProgramData%\BitKeyBridge\appsettings.json`, and performs published exports on the configured interval. By default it also performs an export immediately when the service starts.

The Dashboard can:

- install/update, start, stop, and uninstall the service;
- change the export interval;
- enable/disable the health endpoint and select its port;
- show last export status, row count, DC, replication health, output state, and certificate expiry;
- open the local monitoring endpoint;
- launch Secure Output Wizard.

### Local health endpoint

When enabled, the service exposes:

```text
http://127.0.0.1:8750/health
http://127.0.0.1:8750/health/live
```

The listener is bound to **127.0.0.1 only**. It is not exposed to the LAN and does not require an HTTP URL reservation. The JSON response intentionally excludes recovery passwords, Graph access tokens, passwords, and certificate private-key material.

`/health` returns HTTP 200 for Healthy/Warning states and HTTP 503 when the snapshot contains a health error. This makes it suitable for local agents such as PRTG, Zabbix, Nagios/NRPE-style wrappers, SCOM agents, or custom monitoring scripts.

The same snapshot can be retrieved without HTTP:

```text
BitKeyBridge.exe --health
```

### Secure Output Wizard

The Dashboard's **Secure Output...** action can create a dedicated local recovery directory with protected NTFS permissions.

The wizard:

- disables inherited NTFS permissions;
- grants Full Control to LocalSystem, local Administrators, and the administrator creating the directory;
- grants configured reader accounts/groups **Read & Execute** only;
- can optionally create an SMB share with a matching restricted share ACL using the native Windows `NetShareAdd` API;
- can update BitKeyBridge's export configuration to use the new protected directory.

Existing SMB shares are never overwritten automatically. If the requested share name already exists, the wizard stops instead of modifying it.

If a legacy WinPE workflow currently reads recovery data from SYSVOL/NETLOGON, update that consumer before changing the production export path.

## Unified Devices and key rotation

The **Unified Devices** tab can search by device name, serial number, user/UPN, Entra device ID, or Intune managed-device ID. Results merge AD computer data, Intune inventory, and Entra BitLocker metadata. If the device is Intune-managed, an administrator can submit a BitLocker recovery-key rotation request after explicit confirmation.

The rotation action uses Microsoft Graph `deviceManagement/managedDevices/{id}/rotateBitLockerKeys`. Intune applies the action asynchronously on the managed device; the recovery key shown in the current session is not assumed to change immediately.

## Audit

The **Audit** tab records local administrative actions such as recovery-key reveal/copy, cloud key retrieval, unified searches, and rotation requests. Audit records contain IDs and metadata only. Any string matching the 48-digit BitLocker recovery-password format is automatically replaced with `[REDACTED-BITLOCKER-KEY]` before being written.

## Build

Install the .NET 10 SDK and run:

```text
build-release.cmd
```

The self-contained single-file executable is written to:

```text
artifacts\win-x64\BitKeyBridge.exe
artifacts\win-x86\BitKeyBridge.exe
artifacts\win-arm64\BitKeyBridge.exe
```

The build script publishes `win-x64`, `win-x86`, and `win-arm64`. The x64 equivalent command is:

```text
dotnet publish src\BitKeyBridge\BitKeyBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o artifacts\win-x64
```

GitHub Actions performs restore/build/publish on `windows-latest` for `win-x64`, `win-x86`, and `win-arm64`. The x64 package runs the built-in `--self-test` in CI; ARM64 is cross-published and packaged because the standard runner is x64. Version tags publish all three ZIPs plus `SHA256SUMS.txt`.

## Repository layout

```text
src/BitKeyBridge/
  Core/             Configuration and models
  Infrastructure/   JSON, CSV, logging, elevation, paths
  Services/         LDAP, replication, Graph, certificate, export logic
  UI/               WinForms GUI
config/              Public configuration example only
.github/workflows/   Windows build/publish workflow
```

## Privacy / deployment configuration

Organization names, internal domain names, DC names, and production OU distinguished names are intentionally not embedded in this public source tree. Keep environment-specific configuration outside the repository or in ignored local files.

## License

MIT.
