# BitKeyBridge 0.28.0 deployment templates

These are optional organization-managed installers. The BitKeyBridge runtime is
native C#/.NET 10 with no PowerShell, RSAT or Graph PowerShell dependency.

## Microsoft Intune Win32

1. Download the official `BitKeyBridge-win-x64.zip` from the verified v0.28.0 release and verify its SHA256 against the release checksums. Use ARM64 only for ARM64 endpoints.
2. Extract `BitKeyBridge.exe` into the same directory as `intune/install.cmd` and `intune/uninstall.cmd`. Rename or copy the scripts to the packaging root.
3. Package that folder with Microsoft's **Microsoft Win32 Content Prep Tool** (`IntuneWinAppUtil.exe`). Do not package application secrets, credential stores or administrator certificates.
4. Use install command `install.cmd` and uninstall command `uninstall.cmd`; run in SYSTEM context (device assignment), 64-bit Windows.
5. Recommended detection rule: **File**, path `%ProgramFiles%\\BitKeyBridge`, file `BitKeyBridge.exe`, detection method **file version**, greater than or equal to `0.28.0.0`. Validate EXE embedded file version against the release in your environment before production rollout.
6. Pilot on a workstation and domain controller separately. Do not automate BitLocker key reveal, Entra admin consent or AD LAPS rotation during deployment.

## Group Policy computer startup

Copy the verified, unpacked executable and `intune/install.cmd` into a
read-only deployment share. Use a computer startup script to run
`install.cmd` from a local staged directory or controlled UNC path. Ensure
the computer account can read the share. Avoid deploying a blocked unsigned
binary where CyberArk/WDAC/AppLocker requires an organization-approved
Authenticode certificate.

## Safety and rollback

The installer updates only `%ProgramFiles%\\BitKeyBridge\\BitKeyBridge.exe`;
it does not install/start/delete a Windows service and does not alter
ProgramData, DPAPI credentials, LocalAppData, recovery audit or LAPS history.
Uninstall removes the deployed binary only. Stop any separately configured
BitKeyBridge Windows service before replacing its binary, and retain audit
data according to university policy.

Release signing depends on organizational code-signing credentials and cannot
be enabled automatically by these scripts.
