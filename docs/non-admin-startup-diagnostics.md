# BitKeyBridge: standard-user startup diagnostics

For Windows helpdesk clients running BitKeyBridge under CyberArk EPM or another application-control product. This is a **read-only, non-elevating diagnostic**: it does not modify EPM, ACLs, RBAC, audit or recovery permissions and never retrieves a BitLocker/LAPS secret.

## Run as the affected standard user

In a normal Windows Terminal, PowerShell or Command Prompt (not "Run as administrator"), switch to the extracted BitKeyBridge directory and run:

    .\BitKeyBridge.exe --startup-diagnostics --no-elevation

The diagnostic starts before loading machine configuration. It outputs a JSON report containing only the process architecture, token elevation classification, machine configuration **readability state** and whether a user-local startup log is visible. **Readable** does not mean that the configuration is valid or that recovery access is authorized.

To verify WinForms layout without connecting to AD or Graph:

    .\BitKeyBridge.exe --ui-self-test

For the existing GUI, simply run:

    .\BitKeyBridge.exe

If that process opens and then exits with a startup error, look for:

    %LOCALAPPDATA%\BitKeyBridge\Logs\startup-errors.log

The file contains a sanitized exception type/message. Do not attach appsettings.json, secret vaults, passwords, BitLocker recovery keys, LAPS values or unredacted EPM event payloads to a public issue.

## Reading the results

- **MachineConfiguration = Readable**: the token can open the file. It does *not* prove the file is valid, safe for migration, or usable by a service.
- **MachineConfiguration = Denied**: ask an administrator to validate machine-policy **read** permissions. Do not grant modification permission on ProgramData, protected audit logs or Secrets.
- **MachineConfiguration = Missing**: the administrator should inspect initial provisioning. Do not silently replace an existing unreadable security policy with defaults.
- **WindowsToken = StandardOrFiltered**: normal for a standard user or UAC-filtered administrator; it is not evidence of RBAC approval.
- **UserStartupLog = NotVisible**: can mean no error has been logged. It does not prove the EXE started.

If the EXE does not start even for --startup-diagnostics, the program cannot report which process-creation policy blocked it. Compare Windows AppLocker/WDAC event channels and the CyberArk EPM management console using the **same user identity and file hash**. Running as administrator under a different token is not a valid standard-user acceptance test. For an unsigned QA build, discuss a policy-approved signed publisher or tightly scoped test allowlist with endpoint security; do not disable EPM.

## Release gate

Before marking PR #61/#62 ready or publishing a production release, validate two different standard-user domain tokens (authorized and unauthorized), broker ACK and durable journal evidence, least-privilege BitLocker/LAPS denial and success, updater apply/rollback, and RDP at 100%, 150%, 175% and 200%. CI compilation and an administrator-context smoke test are not a substitute for those checks.
