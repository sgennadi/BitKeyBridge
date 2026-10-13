# Least-privilege connection setup

BitKeyBridge does not require the helpdesk operator to run as local
Administrator for ordinary, authorized recovery reads. This does **not**
grant access to confidential recovery passwords: AD/Entra authorization
and audit continue to apply.

## Start source selection

Start > Connect using:

- **Active Directory**: Windows integrated identity is preferred. The app
  attempts an automatic connection only when AD is selected and the
  administrator-configured AutoConnectOnStart setting permits it. Failure
  does not automatically prompt for administrator credentials.
- **Microsoft Entra / Intune (Microsoft 365)**: connect with **Device Code
  and MFA** using an approved tenant/app registration; AD connection and
  AD credential prompts are never required.
- **Both**: select and authenticate both sources. AD data and Entra
  metadata may be searched together once each source is authorized.
- **Choose a source**: the first-run default; no implicit credential
  prompt.

The selection is stored under the current user's LocalAppData; it is
not an administrator-level machine policy.

## Microsoft Entra / Intune: regular user setup

The Start wizard accepts:
- Tenant ID or verified tenant domain.
- The **Application (client) ID** of an existing IT-approved native/public
  application (public client / device-code sign-in enabled).

No client secret, administrator password or certificate is saved by this
path. The delegated Graph permissions and **tenant administrator consent**
must already be approved; an ordinary user cannot grant themselves new
tenant permissions or Entra roles.

Permission/role checklist (least privilege depends on actual task):

| Task | Delegated Graph permission | Additional access |
| --- | --- | --- |
| List BitLocker recovery metadata | `BitlockerKey.ReadBasic.All` | Device owner or supported Entra role |
| Reveal BitLocker recovery secret | `BitlockerKey.Read.All` | Authorized owner or appropriate Entra role |
| Read device/Entra directory metadata | `Device.Read.All` | Tenant scope/policy as configured |
| List Entra LAPS metadata | `DeviceLocalCredential.ReadBasic.All` | Entra role with local-credential standard/read |
| Reveal Entra LAPS password | `DeviceLocalCredential.Read.All` | Entra permission `microsoft.directory/deviceLocalCredentials/password/read` |
| Intune device inventory | `DeviceManagementManagedDevices.Read.All` | Intune managed-device visibility |
| Rotate BitLocker key | `DeviceManagementManagedDevices.ReadWrite.All` | Intune remote task Rotate BitLockerKeys + device read; separately enabled |

Note: The application's current standard recovery sign-in requests
`BitlockerKey.Read.All` plus Device/ManagedDevices read scopes to support
explicit reveal without a second login. Individual delegated Graph scope
grants do not override each user's Entra RBAC. Administrators should
approve only the features their helpdesk users need, and audit secret
retrieval. Disabling Intune rotation keeps its write scope optional.

Microsoft guidance:
- https://learn.microsoft.com/en-us/graph/api/bitlockerrecoverykey-get
- https://learn.microsoft.com/en-us/graph/api/directory-list-devicelocalcredentials
- https://learn.microsoft.com/en-us/entra/identity/devices/howto-manage-local-admin-passwords
- https://learn.microsoft.com/en-us/intune/device-management/actions/rotate-bitlocker-keys

## If no application has been approved

Select **Administrator setup** in the Start wizard. This opens the
existing native Intune / Entra setup wizard **only after confirmation**.
Creation/repair of an App Registration and granting tenant consent
require a duly authorized tenant administrator; local certificate
management may also require elevation. **Do not** request or store that
administrator's password for ordinary helpdesk sessions.

The existing administrative wizard can grant broad app-only roles.
Prefer a delegated, admin-approved public client for daily support where
possible. Review and narrow existing tenant grants before deployment.

## Active Directory: regular domain account

- A normal domain account can enumerate devices in OUs for which it has
  search/read permissions.
- Delegate Windows LAPS password read on the intended OUs to a dedicated
  helpdesk group (e.g. with the Windows LAPS PowerShell cmdlet
  `Set-LapsADReadPasswordPermission`).
- If Windows LAPS password encryption is used, configure the permitted
  decryption principal with the LAPS policy
  `ADPasswordEncryptionPrincipal` and group membership appropriately.
- Delegated reading of `msFVE-RecoveryPassword` requires the applicable
  BitLocker recovery-object ACLs. Avoid granting Domain Admin merely
  to make a helpdesk recovery query work.
- Searches and password reads remain distinct operations; metadata
  discovered is **not** proof that the secret is readable.

Reference:
https://learn.microsoft.com/en-us/powershell/module/laps/set-lapsadreadpasswordpermission

## Troubleshooting

An authentication/consent failure is different from a permission
failure. The wizard saves only tenant/app IDs. Sign in with Device Code
and MFA, then check the explicit Graph status. If metadata lists but
Reveal fails, check both Graph delegated permission and the operator's
Entra role on that device (for LAPS: password/read). Never bypass
role checks or silently fall back to app-only privileges.
