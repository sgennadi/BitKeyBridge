# Security

BitLocker recovery passwords are high-value secrets.

## Reporting

Do not include real recovery passwords, production LDAP distinguished names, tenant secrets, certificates with private keys, or exported production CSV files in public bug reports.

## Design constraints

- AD access is read-only.
- Cloud recovery-key values are requested only on explicit user action.
- Password authentication is kept in memory and is not written to the cloud-auth configuration file.
- Certificates are generated in the Windows machine certificate store with persisted private keys and are not exported by the application.
- Recovery CSV publishing uses a temporary verified file and atomic replacement.
- The application warns when common broad groups have read access to the output directory.

A dedicated restricted share is preferred over broadly readable SYSVOL/NETLOGON storage for recovery passwords.


## Protected local audit broker (0.30.0-rc.2 test build)

Issue #56 introduces an opt-in LocalSystem Windows Service named-pipe
endpoint for **pre-read audit only**. The service uses real Windows token
impersonation, explicit RBAC SID membership, a narrow pipe ACL with a
NETWORK deny rule, and a protected journal outside the user profile. Its
client rejects fake named pipes that are not SYSTEM-owned. The service
never accepts claimed user identities, recovery keys, passwords or tokens.
All writes require trusted config/journal ACLs, valid chain, bounded
metadata and disk flush before an ACK; any failure blocks strict recovery.

See [broker deployment](docs/protected-audit-broker.md). Do not enable the
preview in production without real non-admin AD/Entra and service identity
validation. It is not a substitute for backend access control or guaranteed
SIEM delivery.

## Non-admin recovery and audit boundary

The ordinary interactive GUI runs as its caller, without attempting a global
UAC elevation. Authorization for password recovery remains enforced by AD
ACLs/decryption delegation, Microsoft Graph permissions and device roles,
plus configured BitKeyBridge RBAC/JIT/approval policy. Metadata-only search
does not imply the right to read secrets.

Before a privileged recovery operation fetches a password, BitKeyBridge
commits an authorization audit event. If writing to both journals fails,
the operation is rejected. The current-user portable audit fallback is stored
in LocalAppData only when protected ProgramData auditing is not writable,
and the GUI displays a warning. This fallback **does not provide tamper
resistance** against its owner even though events are hash-chained. IT-managed
endpoints that require integrity must provision a protected journal/service
and enable `RequireProtectedAuditForRecovery` in Helpdesk settings.
Strict mode also checks the machine audit file and directory owners and NTFS
Allow write rules. An ordinary user-writable `ProgramData` file does not
count as protected. Provision a protected writer/service before requiring this
policy for non-admin operators.

A machine-wide audit file must **not** be made writable by all ordinary
users to make portable Helpdesk access work. Centralized log collection,
access controls, and SIEM delivery remain administrator responsibilities.

## Update security

BitKeyBridge binds update metadata to the configured GitHub repository, release tag, architecture asset name, and exact asset size. Release ZIPs are verified against `SHA256SUMS.txt` and, when available, the SHA-256 digest returned in GitHub release-asset metadata. When both checksum sources are present they must agree. Release metadata is revalidated immediately before download, redirected downloads must remain HTTPS on GitHub/GitHubusercontent, and the staged package must contain exactly one root `BitKeyBridge.exe` whose version exactly matches the selected release. The staged executable must then pass `--self-test` before installation, and the elevated helper rechecks its hash and exact version before replacement.

These checks protect against corruption, metadata drift, and unexpected assets, but they do not replace Authenticode signing. Until signed releases are enabled, GitHub repository/release publishing permissions remain part of the update trust boundary.

## Remote API

The Remote API is disabled by default. Enabling it requires administrator rights.

- TLS 1.2/1.3 is mandatory.
- A random 256-bit bearer token is generated; only its SHA-256 hash is stored.
- Bearer tokens must decode to exactly 256 bits; duplicate `Authorization` headers are rejected.
- Scoped token rotation/revocation is persisted transactionally, and enabled Remote API configuration fails closed when token hashes or TLS configuration are invalid.
- The plaintext token is shown only at provisioning/rotation time.
- The generated Windows Firewall rule is limited to Domain and Private profiles.
- Remote management is disabled separately by default.
- No Remote API endpoint returns BitLocker recovery passwords.
- The default generated server certificate is self-signed; clients should trust/pin the displayed thumbprint or use an organization-managed TLS certificate.

## Diagnostics and SIEM

Diagnostic text is passed through a shared secret-redaction layer before it is written to a diagnostics bundle. Remote API token hashes are removed from the sanitized configuration, SIEM webhook path/query material is not exported, and known password/token/Authorization/JWT/BitLocker recovery-key patterns are redacted. SIEM webhook delivery does not follow redirects.
