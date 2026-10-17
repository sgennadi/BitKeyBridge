# BitKeyBridge v0.30.4 — stable unsigned technician release

**No Authenticode signature, no QA/RC label.** The owner explicitly authorized
unsigned releases with automatic installation. This release is for technician
field testing; successful GitHub CI does not certify your AD/Entra deployment.

## Install / update

Download the matching architecture: BitKeyBridge-win-x64.zip,
BitKeyBridge-win-x86.zip or BitKeyBridge-win-arm64.zip.
Verify exact assets against SHA256SUMS.txt on the official GitHub release,
while recognizing that hashes from an unsigned source are not independent
publisher authentication. The package includes BUILD-INFO, SBOM and a
secret-free DomainPreflight CMD for technician diagnostics.

Users who previously opted into unsigned updates from sgennadi/BitKeyBridge
may receive v0.30.4 through the existing auto-updater on application launch.
The new background scheduler is optional; it is not registered automatically.

The main WinForms tabs are Start, BitLocker, LAPS, Devices (AD + Microsoft 365),
Helpdesk, Diagnostics and optional permission-gated Settings. Start is the
default. Ctrl+1..7 navigates without reloading search state.

## Optional background automatic updates

From an already **elevated** CMD on a machine where v0.30.4 is installed
into Program Files, and whose configured policies permit unsigned updates:

    cd /d "%ProgramFiles%\BitKeyBridge"
    BitKeyBridge.exe --auto-update-enable --accept-unsigned-update-risk --no-elevation
    BitKeyBridge.exe --update-task-install --update-task-hour 3 --no-elevation
    BitKeyBridge.exe --update-task-status --no-elevation

The daily Task Scheduler action uses the installed EXE under LocalSystem.
Do not register a portable EXE as a scheduled privileged updater. The job
rechecks GitHub HTTPS/repository, hashes, PE architecture, ProductVersion,
offline staged self-tests, active machine trust policy and rollback hold.

When any automatic update fails, a persistent failure hold blocks subsequent
attempts until authorized operator investigation. An administrator can remove
the scheduled job with:

    BitKeyBridge.exe --update-task-remove --no-elevation

## Audit and troubleshooting

    BitKeyBridge.exe --audit-journal-capacity --no-elevation
    BitKeyBridge.exe --audit-broker-preflight --no-elevation
    BitKeyBridge-DomainPreflight.cmd "C:\Path\To\BitKeyBridge.exe"

The journal command reports occupancy and danger of existing .old rollover,
without revealing, truncating, deleting or migrating SYSTEM-owned events.
Strict protected audit denial, Windows-token RBAC, and deny-before-secret
remain mandatory wherever configured.

Technicians must test AD delegation, Entra / Intune permissions, LAPS and
BitLocker workflows, LocalSystem service startup/restart, rollback and
CyberArk EPM / WDAC / AppLocker policies before fleet-wide rollout.
Do not attach recovery keys, passwords or cloud tokens to issue reports.

## Security note

Automatic **unsigned** updates accept the risk of compromised GitHub
publication. SHA-256 and attestations alone do not independently identify
an Authenticode publisher. Existing stronger publisher pins or Windows
application control remain enforceable; no bypass is added.
