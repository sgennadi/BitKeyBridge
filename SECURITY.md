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
