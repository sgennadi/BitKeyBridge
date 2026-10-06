# Native LAPS support

BitKeyBridge 0.19.0 uses C#/.NET, signed/sealed LDAP (or certificate-validated LDAPS), native DPAPI-NG and Microsoft Graph. No PowerShell process, module, SDK or fallback is used by these features.

| Source | Current password | History | Identity and permissions |
| --- | --- | --- | --- |
| Legacy Microsoft LAPS / legacy emulation | `ms-Mcs-AdmPwd` | Not stored by this format | Read/control access to the confidential attribute |
| Windows LAPS plaintext | JSON in `msLAPS-Password` | Not stored without encryption | Read/control access to the confidential attribute |
| Windows LAPS encrypted | `msLAPS-EncryptedPassword` | Every returned value of `msLAPS-EncryptedPasswordHistory` | Attribute read permission plus membership in the configured encryption principal |
| Windows LAPS DSRM | `msLAPS-EncryptedDSRMPassword` | `msLAPS-EncryptedDSRMPasswordHistory` | Attribute read permission plus encryption-principal authorization |
| Windows LAPS in Entra | Graph `directory/deviceLocalCredentials/{deviceId}` | Previous credentials returned in `credentials` | `DeviceLocalCredential.Read.All`; delegated access also needs an applicable Entra role |

AD timestamps use UTC FILETIME. Windows LAPS JSON stores `n` (account), `t` (hexadecimal FILETIME) and `p` (password); passwords and passphrases are preserved without trimming. The 16-byte encrypted header is validated before passing its payload to `NCryptUnprotectSecret`. Decrypted buffers and disposable password arrays are cleared after use. Immutable .NET strings required by LDAP/JSON/WinForms/clipboard APIs cannot be explicitly overwritten; their lifetime is kept bounded and they are never persisted by the LAPS workflow.

The Windows Server 2025 `msLAPS-CurrentPasswordVersion` GUID is optional; older schemas remain supported. The legacy schema does not store the managed account name, so BitKeyBridge explicitly labels it as unknown instead of assuming `Administrator`.

LDAP multi-valued attribute order is not chronological. AD rows sort current before history and then by update time within each source. Entra current/history is determined from the newest `backupDateTime`, including account changes; each returned account SID is preserved. A failed old encrypted value is shown with its own status and does not hide readable current or other history entries. Attributes omitted by LDAP cannot reliably distinguish missing backup, disabled retention and denied read access.

## Manual connection

Connect to AD and Discover DC open a domain/DC/user/password dialog even if the previous connection used the Windows account or a protected vault. The default is manual credentials, with an explicit checkbox for the current Windows account. Cancel keeps the existing credentials. Accepting a new connection switches the GUI to Session storage without deleting existing vault entries. Passwords are not written to `appsettings.json`.

An explicit DC with no supplied domain resolves the domain from that DC's RootDSE, before inspecting the workstation's joined domain. Domain-only input permits automatic DC discovery. Username formats `DOMAIN\user`, `user@domain`, and a bare username with a domain are supported.

For encrypted AD records, `LogonUserW` NEW_CREDENTIALS / WINNT50 and `WindowsIdentity.RunImpersonated` provide outbound domain-key access under the same supplied AD identity as LDAP. This scoped operation does not change the application's RBAC identity, and it never retries under the process identity on a decryption failure. Domain KDS/GKDI connectivity and the encryption-principal permission are still required.

## Cloud setup

Administration → Cloud has an opt-in **Include LAPS read permissions in setup** checkbox. First-Run / Repair dynamically resolves and grants delegated/application `DeviceLocalCredential.Read.All` when selected, while preserving unrelated existing application permissions. Clearing the checkbox does not revoke previously granted LAPS access. Revocation remains an Entra administration operation.

Device Code uses MFA/Conditional Access as before. The existing legacy ROPC and app-certificate modes remain available subject to tenant policy. LAPS delegated authentication requests only LAPS/device-read scopes; its token is tracked independently from the recovery token capabilities. Reading a specific GUID uses Entra **deviceId**, not directory object **id**. Name resolution uses exact names and rejects ambiguity.

## Access, lifetime and verification

Read, Reveal and Copy require BitKeyBridge RecoveryRead. Existing optional JIT, two-person approval, reference and SIEM checks apply before a secret read/display/copy. LAPS access is represented by a deterministic resource GUID derived from the source and exact lookup string; approval/audit source names are `LAPS-AD` and `LAPS-Entra`. Re-enter the same lookup form when using an existing approval. Audit entries contain account/source/time/status metadata, never the password or encrypted/Base64 attribute value.

The LAPS workspace does not cache to disk or participate in BitLocker CSV exports or diagnostics. Read results clear after two minutes, when the query/source/connection identity changes, on reconnect/clear/close. Clipboard content is cleared only when it still matches a password tracked by BitKeyBridge; other clipboard content is preserved. Pending AD reads are discarded on cancellation even if the LDAP call completes later.

Run portable regression checks with `dotnet run --project tests/LapsRegression -c Release`. Windows release `--self-test` additionally verifies the native DPAPI-NG API with a local-logon roundtrip and tampered encrypted-data rejection; `--ui-self-test` verifies compact/large-text/200% layouts. Those checks use synthetic secrets and do not connect to a real AD domain or tenant. Live LDAP attribute permissions, GKDI authorization and Entra tenant policy must be validated in the deployment environment.

Official references:

- [Windows LAPS schema and rights](https://learn.microsoft.com/en-us/windows-server/identity/laps/laps-technical-reference)
- [Windows LAPS encrypted attribute structure (MS-ADA2)](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-ada2/b6ea7b78-64da-48d3-87cb-2cff378e4597)
- [NCryptUnprotectSecret](https://learn.microsoft.com/en-us/windows/win32/api/ncryptprotect/nf-ncryptprotect-ncryptunprotectsecret)
- [LogonUserW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-logonuserw)
- [Graph: get deviceLocalCredentialInfo](https://learn.microsoft.com/en-us/graph/api/devicelocalcredentialinfo-get?view=graph-rest-1.0)
