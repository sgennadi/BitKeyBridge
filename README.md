# BitKeyBridge

Native Windows administration utility for BitLocker recovery information in on-premises Active Directory and Microsoft Entra ID / Intune.

The application is written in **C# / .NET 10 LTS / WinForms**. Runtime operation does **not** use PowerShell, the ActiveDirectory PowerShell module, or the Microsoft Graph PowerShell SDK.

The release is a **self-contained .NET single-file Windows executable**, not NativeAOT. "Native" here means the application calls Windows/LDAP/Graph APIs directly instead of shelling out to PowerShell. This keeps WinForms, DirectoryServices, and enterprise debugging/support straightforward.

## Highlights

- Self-contained single-file Windows builds for x64, x86, and ARM64 (`BitKeyBridge.exe` in each architecture package).
- GUI and non-interactive CLI in the same executable.
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
- Native Entra App Registration setup using Graph REST + OAuth device code; no Graph SDK is required at runtime.
- Unified device search across on-prem AD, Entra BitLocker metadata, and Intune managed devices.
- Intune BitLocker recovery-key rotation with explicit confirmation.
- Local JSONL security audit for key reveal/copy/retrieval/rotation events; recovery passwords are redacted and never written to the audit log.
- Certificate rolling preserves active credentials when the previously managed private certificate is available.

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

### Native Auto Setup

Creating an App Registration from a completely clean machine still needs one OAuth public-client application to obtain the initial management token. Supply a **Bootstrap Client ID** whose delegated permissions have already been granted for:

- `Application.ReadWrite.All`
- `AppRoleAssignment.ReadWrite.All`
- `DelegatedPermissionGrant.ReadWrite.All`

The native setup then creates or updates the dedicated BitLocker app, enterprise application, Graph permissions, admin-consent grants, and local certificate without invoking PowerShell.

Use a dedicated application for this tool. The setup merges API permissions instead of replacing unrelated permissions. Certificate rotation uses Graph key-rolling semantics when an existing managed valid certificate is present.

## Security notes

BitLocker recovery passwords are secrets.

- Do not publish recovery CSV files to source control.
- Review ACLs on the output directory. The application warns about broad read access granted to Everyone, Authenticated Users, BUILTIN\\Users, or Domain Users.
- SYSVOL/NETLOGON is convenient for legacy WinPE workflows but should not be broadly readable when it contains recovery passwords. A dedicated restricted share is preferred.
- Device Code is the preferred interactive mode because it can satisfy MFA and Conditional Access. ROPC is legacy only. Certificate authentication is the intended unattended mode.
- The local security audit is stored at `%ProgramData%\BitKeyBridge\audit.jsonl` and never stores a BitLocker recovery password.
- Cloud recovery-key reads are auditable in Microsoft Entra.

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
