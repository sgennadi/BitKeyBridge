# AD access troubleshooting

This guide is for **administrators configuring Active Directory permissions** for BitKeyBridge. BitKeyBridge itself remains PowerShell-free at runtime; the PowerShell examples below are optional administrator-side delegation commands that you run separately on a management workstation or domain controller.

Use a dedicated AD security group instead of granting individual helpdesk accounts whenever possible, and scope delegation to the smallest OU that contains the managed computers.

Replace:

- `OU=Workstations,DC=example,DC=com` with the target computer OU DN.
- `EXAMPLE\BitKeyBridge-Recovery-Readers` with the AD group/user that should receive access.

## BitLocker recovery passwords in AD DS

BitLocker recovery information is stored in child objects of class `msFVE-RecoveryInformation`. The recovery password is the confidential `msFVE-RecoveryPassword` attribute.

If BitKeyBridge can find the computer/recovery object but the password cannot be read, grant **Control Access** to the recovery-password attribute on descendant recovery objects:

```text
dsacls "OU=Workstations,DC=example,DC=com" /I:S /G "EXAMPLE\BitKeyBridge-Recovery-Readers:CA;msFVE-RecoveryPassword;msFVE-RecoveryInformation"
```

If the delegated identity cannot enumerate/read the recovery child objects themselves, also grant read-property access to descendant `msFVE-RecoveryInformation` objects:

```text
dsacls "OU=Workstations,DC=example,DC=com" /I:S /G "EXAMPLE\BitKeyBridge-Recovery-Readers:RP;;msFVE-RecoveryInformation"
```

Do **not** grant Full Control or All Extended Rights just to solve recovery-password read access.

## Windows LAPS in Active Directory

Microsoft's supported delegation cmdlet is:

```powershell
Set-LapsADReadPasswordPermission -Identity "OU=Workstations,DC=example,DC=com" -AllowedPrincipals @("EXAMPLE\BitKeyBridge-LAPS-Readers")
```

To inspect delegated Windows LAPS rights:

```powershell
Find-LapsADExtendedRights -Identity "OU=Workstations,DC=example,DC=com"
```

Important: **read permission and decrypt permission are different**. `Set-LapsADReadPasswordPermission` permits password query, but an encrypted Windows LAPS value can be decrypted only by the security principal configured in the Windows LAPS policy setting **ADPasswordEncryptionPrincipal** (Domain Admins is the default when the setting isn't configured).

For encrypted current password/history, configure Windows LAPS policy under:

```text
Computer Configuration
  Administrative Templates
    System
      LAPS
```

Relevant policy settings:

- `BackupDirectory` = Active Directory.
- `ADPasswordEncryptionEnabled` = Enabled.
- `ADPasswordEncryptionPrincipal` = the dedicated group that is allowed to decrypt.
- `ADEncryptedPasswordHistorySize` = greater than 0 if history is required.
- `ADBackupDSRMPassword` = Enabled only when DSRM backup is required for domain controllers.

Changing `ADPasswordEncryptionPrincipal` doesn't retroactively re-encrypt existing password/history blobs for the new group. After the policy is applied, create a new password backup/rotation on a test computer and validate again.

If history is not enabled, there is no history value to grant access to.

## Legacy Microsoft LAPS

Legacy Microsoft LAPS (`ms-Mcs-AdmPwd`) uses the legacy AdmPwd module:

```powershell
Set-AdmPwdReadPasswordPermission -Identity "OU=Workstations,DC=example,DC=com" -AllowedPrincipals "EXAMPLE\BitKeyBridge-LAPS-Readers"
```

To inspect existing legacy LAPS extended rights:

```powershell
Find-AdmPwdExtendedRights -Identity "OU=Workstations,DC=example,DC=com"
```

Legacy Microsoft LAPS doesn't provide password history. No ACL change can make legacy history appear.

## How to interpret BitKeyBridge results

- **Computer object / recovery metadata missing**: verify the target OU/domain/DC and normal read/enumeration permissions.
- **BitLocker object found, password blank/Access denied**: grant scoped Control Access to `msFVE-RecoveryPassword`.
- **Windows LAPS attribute can be queried but decrypt is AccessDenied**: update `ADPasswordEncryptionPrincipal`; the OU read ACL alone isn't sufficient.
- **Windows LAPS history not detected**: verify `ADEncryptedPasswordHistorySize > 0` and that new password backups have occurred after policy application.
- **Legacy LAPS history requested**: not supported by legacy LAPS.
- **Entra LAPS**: AD ACL commands don't apply. Use the Intune / Entra Setup Wizard and verify `DeviceLocalCredential.Read.All` plus an applicable Entra role.

Use **LAPS → Check access** for a metadata-only diagnostic. It intentionally doesn't retrieve a password. For BitLocker, use the Recovery page; if recovery metadata is visible but secret retrieval fails, open **AD access help...** and provide the generated commands to an AD administrator.
