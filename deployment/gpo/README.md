# BitKeyBridge GPO / Intune - development templates (v0.32)

This ADMX is **machine restrictions only**, not a credential delivery,
BitLocker recovery permission or automatic update opt-in mechanism.

## Installation

Copy `BitKeyBridge.admx` and the chosen language-specific `BitKeyBridge.adml`
into a controlled Group Policy Central Store or equivalent Administrative
Templates location. Localization resources are provided for en-US, ru-RU and
he-IL. Windows group policy management selects the correct ADML automatically.

Under Computer Configuration / Administrative Templates / Windows Components /
BitKeyBridge, the three restrictions set DWORDs in the native 64-bit machine
registry policy key:

    HKLM\SOFTWARE\Policies\BitKeyBridge

| Policy | Enabled DWORD | Meaning |
| --- | --- | --- |
| Disable automatic updates | DisableAutomaticUpdates=1 | Blocks all auto-install |
| Disable unsigned updates | DisableUnsignedAutomaticUpdates=1 | Blocks unsigned auto-install |
| Require signed updates | RequireSignedAutomaticUpdates=1 | Requires an existing local trusted publisher pin |

When disabled, each writes DWORD 0; Not Configured does not enforce that
restriction. A corrupt or inaccessible registry policy fails closed.
**No policy enables unsigned consent.**

## Scheduled fleet collection

An approved GPO Scheduled Task may run
`Collect-Fleet.cmd "\\fileserver\secure-inventory\BitKeyBridge"` as
LocalSystem, only AFTER installing the x64/x86/ARM64-matching BitKeyBridge.
The share must already exist and the device account needs its normal Windows
write permissions for its own filename. Do not embed a password in a script.
Do not grant all workstation accounts Modify permissions for every other
machine's reports. On Intune, deploy the same EXE CLI action from the device
context with an approved inventory destination; the command has no hidden
elevation or network transport of its own.

Collecting a report runs `BitKeyBridge.exe --fleet-export <MACHINE.json> --no-elevation`.
The native exporter writes the same versioned, whitelisted schema as
`--fleet-status` using a temporary file and atomic replacement. It rejects
mismatched machine filenames, directory junctions and reparse-point targets.
It does not use PowerShell or require administrator elevation; the existing
machine policy, Windows access token and share ACL still apply.

The wrapper writes its latest diagnostic status to
`%WINDIR%\Temp\BitKeyBridgeFleet.log` without console output.

On an administrator workstation, create the consolidated CSV with:

    BitKeyBridge.exe --fleet-aggregate "\\fileserver\secure-inventory\BitKeyBridge" "C:\Inventory\BitKeyBridge.csv" --no-elevation

The aggregator reads at most 5,000 JSON files, rejects unknown or
unexpected fields and untrusted OS architecture values, preserves the newest
UTC report per machine, and flags samples older than seven days as Stale.
The aggregator verifies that each JSON filename matches the embedded machine:
use `MACHINE.json` for live GPO exports, or the UTC-only archived form
`MACHINE.yyyyMMddTHHmmssZ.json` for older samples. Renaming a report to
another computer name without editing the content will be rejected; filename
checks supplement, but do not replace, per-computer SMB/NTFS ACLs.

Do not run the older, incompatible `FleetSnapshotTransport --fleet-aggregate`
JSON-summary implementation from the superseded draft PR #67. The supported
contract here is `--fleet-status` / `--fleet-export` / `--fleet-aggregate
<input-folder> <output.csv>`.

Treat missing/stale reports as *unknown*, never as proven AD/Graph outages,
proof of a durable protected-audit ACK or authorization for key retrieval.

These files need technician validation in a real Windows GPMC/Intune pilot
before any production GPO linkage.
