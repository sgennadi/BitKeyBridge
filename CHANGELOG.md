# Changelog

## 0.2.1

- Removed the requirement to pre-create an App Registration for first-run Entra setup.
- Added built-in Microsoft first-party bootstrap using Microsoft Graph Command Line Tools and Device Code.
- Moved custom Bootstrap Client ID to an advanced fallback for restricted Conditional Access environments.
- Resolved runtime Graph permission IDs dynamically from the Microsoft Graph service principal instead of hard-coding them.
- Added retry/backoff for Graph throttling and transient service errors.
- Added propagation handling for newly created Enterprise Applications.
- Added offline self-test coverage for the built-in bootstrap configuration.


## 0.2.0

- Added Device Code authentication as the recommended interactive Graph sign-in mode.
- Added Intune managed-device inventory and unified AD + Entra + Intune device search.
- Added BitLocker recovery-key rotation through Intune with explicit confirmation.
- Added local JSONL security audit with automatic BitLocker recovery-password redaction.
- Added audit self-test.
- Added `DeviceManagementManagedDevices.ReadWrite.All` to native Entra Auto Setup.
- Updated GitHub Actions to Node 24 runtimes.
- Kept legacy ROPC and certificate authentication modes.

## 0.1.0

Initial native Windows implementation.

- C# / .NET 10 LTS / WinForms.
- Self-contained single-file executables for win-x64, win-x86, and win-arm64; each architecture uses the same GUI/CLI codebase.
- Direct LDAP BitLocker export with atomic CSV publishing and safety guards.
- Dynamic domain-controller discovery and replication diagnostics.
- Configurable OU scopes and local recovery search.
- Microsoft Graph recovery metadata search and explicit on-demand recovery-key retrieval.
- Password/ROPC and certificate authentication.
- Native Entra App Registration bootstrap/setup flow.
- GitHub Actions Windows build, single-file publish, offline self-test, and tagged release packaging.
