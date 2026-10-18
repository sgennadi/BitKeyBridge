# BitKeyBridge v0.32.0-rc.3 — release candidate and automatic updates

This is an **unsigned, opt-in prerelease**, never a Stable/Latest release.
It combines the v0.31.0 Unified Search/Recovery features and v0.32.0
secret-free Fleet/GPO reporting with v0.30.5 update/rollback and Protected
Audit Broker hardening. It does **not** grant any AD/Entra permissions, read
secrets without existing controls, deploy a GPO or consent to unsigned updates.

## Release publication and provenance

The isolated `.github/workflows/rc-0.32.0.yml` pipeline tests the exact
source SHA on GitHub-hosted Windows and Ubuntu before publishing:

- LAPS regression and CodeQL security analysis.
- Windows x64, x86, ARM64 single-file self-contained builds, PE machine and
  ProductVersion verification, offline self-tests and x64 runtime HiDPI UI tests.
- Negative signing policy smoke tests, GPO ADMX/ADML XML validation, and
  strict single-root-EXE ZIP regression and archive-to-executable SHA-256.
- A real, isolated two-standard-user Protected Audit Broker integration test.
- Real legacy v0.30.1/v0.30.3 upgrade and rollback regression using synthetic
  DPAPI and configuration; no genuine user recovery keys.
- Atomic publish ONLY for the designated release branch and explicit
  `Release candidate v0.32.0-rc.3` commit, after every job succeeds.
- Never overwrite an existing release/tag. Include exact build source SHA,
  `SHA256SUMS.txt`, and all three canonical architecture ZIPs.
  The tag is marked `--prerelease`, leaving Stable/Latest unchanged.

The architecture ZIPs must contain exactly `BitKeyBridge.exe` at their
root. Signing is **not** claimed or simulated for public binaries.
Ephemeral PKI test keys and tokens are never published.

GitHub's SHA-256 hashes, Release digest and HTTPS improve transfer integrity,
but are not an independent code-signing identity. Stable corporate
distribution requires institutional Authenticode, code-control acceptance,
and reviewed production permissions.

## Background auto-update reliability fix (RC2)

RC1 exposed a Windows synchronization bug in `--scheduled-update`: the background
worker acquired a thread-affine named mutex and performed asynchronous HTTPS
download/staging before releasing it. Async continuation may resume on a
different managed thread, so `ReleaseMutex` could throw even after verified
update staging and misreport an otherwise successful run.

RC2 uses a cross-process named semaphore for the async download/staging gate.
It is released exactly once from any completing thread via `IDisposable`.
The existing separate, synchronous privileged update-apply mutex is unchanged.
An executable `--self-test` regression verifies contention and cross-thread
release on Windows as part of RC2's x64 release build.

The scheduler stays **opt-in**: it neither creates tasks nor grants unsigned
update consent on any existing computer. RC1 users explicitly enrolled in
prerelease checks can move to RC2 after GitHub publication. Stable Latest
remains v0.30.5 until separately approved.

## RC3: remember the startup connection

The interactive connection chooser adds an unchecked-by-default **Remember
this connection** checkbox. AD, Microsoft Entra / Intune and Both may be
reused on subsequent launches only with the current user's explicit consent
and the machine's existing `AutoConnectOnStart` permission. Earlier stored
source preferences do not grant consent. **Choose later** and a quick manual
source switch disable remembered reconnect until the user confirms it again.

Only the source and choice of Windows vs alternate AD authentication are saved
in the user's LocalAppData. Alternate AD passwords are prompted again. Cloud
sign-in continues to require delegated permissions, Device Code/MFA where
applicable, and tenant-admin consent for any previously unapproved app. No
BitLocker/LAPS secret is loaded or exposed by the remembered choice.

This release retains the RC2 async-safe SYSTEM updater and does not create any
scheduled tasks or opt managed workstations into unsigned packages.
See [startup source and privacy guide](startup-remember-connection.md).

## Current updater behavior

- **Stable channel** (default): only stable GitHub Releases, currently v0.30.5.
- **Prerelease checks**: administrator/technician manually enables
  *Allow prerelease versions* in Settings > Updates on a test machine.
- **Manual update**: check release and explicitly choose Update Now.
- **Automatic update on GUI startup**: off by default, requires separately
  configured valid signer/publisher policy OR explicit risk acknowledgment
  for unsigned updates from `sgennadi/BitKeyBridge`.
- **Background update**: optional daily LocalSystem Task Scheduler job
  running the installed Program Files executable; cannot be registered
  by a portable copy, and never silently enrolls another endpoint.
- All automatic paths recheck trust, managed GPO restrictions, rollback
  holds, package hashes, executable identity, post-install self-test, and
  preservation of protected audit data.

### Check status without installing

    BitKeyBridge.exe --version --no-elevation
    BitKeyBridge.exe --check-update --no-elevation
    BitKeyBridge.exe --auto-update-status --no-elevation
    BitKeyBridge.exe --managed-update-policy --no-elevation
    BitKeyBridge.exe --update-task-status --no-elevation

### Explicit unsigned test-mode consent (only for approved test computers)

    BitKeyBridge.exe --auto-update-enable --accept-unsigned-update-risk --no-elevation

This command does NOT select the prerelease channel; choose that setting
separately in the GUI. It does not bypass higher-trust signing policies or GPO.

When intentionally installed to `%ProgramFiles%\BitKeyBridge` and from an
already elevated administrator CMD, optionally configure a daily task:

    BitKeyBridge.exe --update-task-install --update-task-hour 3 --no-elevation

Disable or remove it with:

    BitKeyBridge.exe --auto-update-disable --no-elevation
    BitKeyBridge.exe --update-task-remove --no-elevation

Use the GUI's Updates/Rollback workflow when an update fails. An automatic
failure hold deliberately blocks repeat attempts until repaired or resumed.

## Pilot acceptance before stable v0.32.0

Only on controlled Windows 11 domain / Entra managed workstations, confirm:

1. Normal helpdesk account works without UAC; AD, Entra and Intune read/deny
   pathways enforce original RBAC, no silent LAPS/BitLocker reveal or rotation.
2. Protected audit service is installed by an administrator, and two separate
   standard-user identities exercise allow/deny and durable audit ACK.
3. Screen layout, EN/RU/HE, 100%/125%/150%/175%/200% scaling, 4K and RDP
   have no cropped text or broken direct actions.
4. Check WDAC/AppLocker/CyberArk EPM permissions, SmartScreen, Windows
   service restart, task scheduler identity, update-in-use and signed/unsigned
   policy denial, before enabling installation on any managed device.
5. Verify update, rollback, prior `.bak`, protected journal history,
   permissions, and prevention of automatic retry after an update failure.
6. Test GPMC import of ADMX/ADML, the approved collection SMB path, per-device
   NTFS writer ownership, and correct rejection of foreign/oversized JSON.
7. For broad deployment, require organization-approved signing and staged
   canary approval. No release workflow assigns Intune/GPO or enrolls clients.

The public prerelease is suitable for controlled evaluation, **not** an
assertion that AD/Entra/CyberArk operational acceptance has already passed.
