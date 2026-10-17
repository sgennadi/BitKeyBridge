# BitKeyBridge deployment and upgrades (Windows .NET 10)

Current stable GitHub release: v0.30.3 (unsigned, exact x64/x86/ARM64 ZIPs).
This directory contains the optional Win32/GPO deployment templates. The
background scheduled updater documented below is in unreleased v0.30.4.

## Intune Win32 / GPO Startup installation

1. Download the exact architecture ZIP from the official GitHub Release.
2. Verify the ZIP against SHA256SUMS.txt acquired from the official Release.
   Note: matching a checksum from the same unsigned GitHub publisher does NOT
   provide independent publisher authenticity.
3. Extract BitKeyBridge.exe into the same folder as intune/install.cmd and
   intune/uninstall.cmd. Do not include appsettings.json, BitLocker keys, LAPS
   passwords, access tokens, AD credentials or private certificate keys.
4. For IntuneWinAppUtil package the folder and set install.cmd / uninstall.cmd
   as SYSTEM commands. Use file-version detection for
   %ProgramFiles%\BitKeyBridge\BitKeyBridge.exe, >= 0.30.3.0 on the stable
   v0.30.3 package. For a later build, update the version detection rule.
5. Use x64 ZIP for x64 endpoints, ARM64 ZIP for ARM64 endpoints, or x86 if
   32-bit Windows is intentionally supported. Never force ARM64 on x64.
6. Pilot first on disposable managed workstations and standard Helpdesk users,
   then validate deployed service, AD/Graph permissions, CyberArk EPM and
   rolled-back versions. Do not silently disable endpoint protection.

The installer only replaces the EXE in Program Files. It leaves ProgramData,
DPAPI, machine secrets, the managed BitKeyBridge Windows Service, protected
journal and per-user profile files untouched. A running process can prevent
replacement; the script will fail and preserve backup instead of forcibly
killing an operator's recovery session.

## Optional v0.30.4 background updates

The compiled binary has an optional built-in scheduler, disabled until
an administrator explicitly enables auto-install from the official repository.

From an **already elevated** cmd on a disposable test workstation after
installing v0.30.4 into Program Files:

    cd /d "%ProgramFiles%\BitKeyBridge"
    BitKeyBridge.exe --auto-update-enable --accept-unsigned-update-risk --no-elevation
    BitKeyBridge.exe --update-task-install --update-task-hour 3 --no-elevation
    BitKeyBridge.exe --update-task-status --no-elevation

The fixed task action is "%ProgramFiles%\BitKeyBridge\BitKeyBridge.exe"
--scheduled-update --no-elevation, running as LocalSystem daily at 03:00.
No PowerShell, credential prompt or hidden UAC acceptance is involved.
Changing the machine policy requires admin rights. Local rollback holds pause
the scheduler; disabled consent blocks installation even if the task remains.

To remove the task, in elevated cmd:

    BitKeyBridge.exe --update-task-remove --no-elevation

Do not run the scheduler from a downloaded portable EXE. The service and
scheduler use the same verified update pipeline, but the scheduler never
authorizes secret recovery requests. Verify maintenance windows, service
restart, audit journal availability, and rollback on your endpoint management
pilot before a broad deployment.

## Audit journal capacity

Ordinary users can request a read-only status without revealing any journal
events or keys:

    BitKeyBridge.exe --audit-journal-capacity --no-elevation

Ready = active journal is below warning size; Warning = archive exists or
active journal is >= 80% of the 20 MiB rotation threshold; Blocked = both
the active journal is at threshold and an existing protected .old archive
would be overwritten. NotProvisioned = active journal is absent. Access-denied
is reported as an error, not interpreted as missing data.

Never truncate/delete existing protected logs. Off-host sealing and archival
must be designed and approved before changing retention. A capacity report is
not a verified journal hash chain and not authorization to read a secret.
