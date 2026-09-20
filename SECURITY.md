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


## Update security

BitKeyBridge verifies release ZIPs against `SHA256SUMS.txt` and, when available, the SHA-256 digest returned in GitHub release-asset metadata. The staged executable must pass `--self-test` before installation.

These checks protect against corruption and unexpected assets, but they do not replace Authenticode signing. Until signed releases are enabled, GitHub repository/release publishing permissions remain part of the update trust boundary.

## Remote API

The Remote API is disabled by default. Enabling it requires administrator rights.

- TLS 1.2/1.3 is mandatory.
- A random 256-bit bearer token is generated; only its SHA-256 hash is stored.
- The plaintext token is shown only at provisioning/rotation time.
- The generated Windows Firewall rule is limited to Domain and Private profiles.
- Remote management is disabled separately by default.
- No Remote API endpoint returns BitLocker recovery passwords.
- The default generated server certificate is self-signed; clients should trust/pin the displayed thumbprint or use an organization-managed TLS certificate.
