# BitKeyBridge v0.30.5 — controlled automatic deployment

**Status: opt-in GPO/Intune deployment kit; no machines have been assigned.**

This installer deploys only a **specific administrator-approved SHA-256-pinned
BitKeyBridge.exe**. It does NOT enable the built-in unsigned updater, download
unapproved public releases, access BitLocker/LAPS secrets, terminate active
recovery sessions, or change protected Audit Broker policy.

## Prepare and approve the architecture-specific package

1. Confirm the official GitHub v0.30.5 Release has passed its release checks.
   Obtain the matching architecture ZIP (win-x64, win-x86, or win-arm64) and
   SHA256SUMS.txt from the official release. Compare their checksums.
   These builds are UNSIGNED: same-publisher SHA-256 is not independent
   Authenticode identity. Apply organizational release approval.
2. Extract the ZIP's single root BitKeyBridge.exe to an approved and audited
   read-only-to-computers SMB staging location, for example:

       \\fileserver\Software\BitKeyBridge\v0.30.5\win-x64\BitKeyBridge.exe

3. Compute SHA-256 of the EXTRACTED EXE (not the ZIP) and pin the exact
   64-character digest in the GPO task or Intune package:

       certutil -hashfile "C:\Staging\BitKeyBridge.exe" SHA256

4. Create a device allowlist in ASCII or UTF-8 without BOM, one approved
   COMPUTERNAME per line. For example:

       W11-PILOT-01
       W11-PILOT-02

   Only IT deployment administrators may edit this file or the staged EXE;
   computer accounts need READ, not MODIFY. No wildcards or ALL entries.

## Read-only preflight

    Deploy-BitKeyBridge.cmd "\\fileserver\Software\BitKeyBridge\v0.30.5\win-x64\BitKeyBridge.exe" "<APPROVED_EXE_SHA256>" "\\fileserver\Software\BitKeyBridge\pilot-x64.txt" "0.30.5" "win-x64" /verify-only

Expected result is PASS and exit code 0, with **no local installation**.
A machine outside the named ring prints SKIP and exits 0, without installing.
A mismatched SHA-256, missing input, or wrong OS architecture is rejected.

## GPO scheduled deployment

Place Deploy-BitKeyBridge.cmd under an IT-controlled SYSVOL scripts folder.
In Computer Configuration / Preferences / Control Panel Settings /
Scheduled Tasks, create a task running under **NT AUTHORITY\SYSTEM** during an
approved maintenance window with domain network connectivity.

Example action (fill in all placeholders, including the reviewed hash):

    Program: C:\Windows\System32\cmd.exe
    Arguments: /d /c ""\\yourdomain\SYSVOL\yourdomain\scripts\BitKeyBridge\Deploy-BitKeyBridge.cmd" "\\fileserver\Software\BitKeyBridge\v0.30.5\win-x64\BitKeyBridge.exe" "<APPROVED_EXE_SHA256>" "\\fileserver\Software\BitKeyBridge\pilot-x64.txt" "0.30.5" "win-x64""

Security-filter the GPO to a **small, approved computer pilot group**.
Maintain separate deployment tasks and SHA pins for x64, x86 and ARM64.
Test SYSVOL/SMB access using a real device account. Enable SMB signing,
audited write access for IT only, and no cross-computer write rights.

An approved in-ring machine installs or updates automatically at the next
scheduled run. Already approved SHA-pinned installs are skipped. Existing
same-version, newer or invalid versions are NOT silently overwritten.
Running EXE locks cause failure/retry; no active helpdesk process is killed.

The script maintains a lock and restricted rollback copy in
%ProgramData%\BitKeyBridgeDeployment, logs non-secret outcomes under
%SystemRoot%\Logs\BitKeyBridge-Deploy.log, and restores the previous EXE if
post-install validation fails. Inspect/remove only the stale _install-lock
after a verified interrupted run.

## Intune Win32

Package an approved architecture EXE, the same Deploy-BitKeyBridge.cmd and
an explicit device allowlist. Configure an Intune Win32 install command using
local package paths, the pinned EXE SHA, exact version and architecture.
Run under SYSTEM/device context, no interactive UAC. Use file-version detection
for 0.30.5.0, appropriate supersedence, and separately assigned Pilot and
Expanded Entra device groups. Do not target All Devices by default.

This repository does not have authenticated Intune tenant/endpoint access.
The application and GitHub CI cannot create or change Intune assignments.

## Promotion / rollback

Start with 1–5 approved pilot workstations, then a small expanded ring, then
the rest of the approved computer scope after administrator review.
Do not automatically promote on CI success alone. Require at least one normal
working day of checks on real users and servers.

To pause: disable the GPO task/Intune assignment or remove the device from
the ring allowlist. Do not disable antivirus, WDAC/CyberArk EPM, secure boot,
Windows security, RBAC or protected audit controls.

Before broad deployment verify: exact installed SHA and version; task exit
codes; local standard-user recovery; Windows/Legacy LAPS; Protected Audit Broker
ACK; rollback after a failed pilot; and restricted access to all deployment
logs and backup executables.

**Publishing a release is not permission for a fleet-wide update.**
The share, verified EXE, pinned SHA, ring file and GPO/Intune task assignment
must be separately approved in the enterprise management environment.
