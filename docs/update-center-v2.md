# Update Center 2.0 — pilot workflow

This is a development feature branch change (PR #90). It has NOT yet been
released as a new stable version or approved for mass deployment. The
published stable is v0.30.5. The previously published v0.32.0-rc.3 is
immutable and is not modified by development in PR #90.

## Desktop UX

Open **Settings > Updates > Update Center 2.0**. The view shows:

- Installed executable version, selected Stable/Prerelease channel and
  last checked GitHub release. A cached release is NOT presented as live.
- UTC-captured check time displayed in the technician's local time zone.
- Effective auto-install state: startup check flag, automatic install flag,
  rollback/failure hold, active trust mode and machine policy.
- Latest local installation / rollback result and timestamp.
- Background task installed/unknown status, next and last execution times,
  and last Windows Task Scheduler result (hex). Missing task and unavailable
  scheduler permissions are distinct conditions.

**Refresh status** reads existing files, policy and Task Scheduler without
sending a network request. **Retry check** queries GitHub again, but does not
install, change policy, enroll a task or clear a rollback hold.
**Pause auto-install** disables automatic installation without deleting the
scheduled task, changing publisher trust, or disabling manual checks.
**Enable auto-install** requires an elevated administrator, uses only the
already-saved signing/unsigned consent and a separate Yes/No confirmation. It
does not save other unsaved fields, grant unsigned consent or bypass GPO, and
NEVER clears a failed-update hold.
Use the existing **Resume Auto-Install** only after administrator
investigation, explicit warning confirmation and fresh signed/unsigned and
GPO validation of a held update. Daily Task Scheduler registration/removal still requires the
machine-installed Program Files executable and administrator authorization.

## Compact layout, EN/RU/HE and 4K

The compact switch hides supplementary history/task results, but still shows
current version, last check, effective installation state, task safety/next
scheduled run and Pause/Enable/Retry actions. Expand to inspect
task dates and diagnostic state. New controls are translated for English,
Russian and Hebrew; language switches update the buttons immediately.
Sensitive passwords, GUIDs, user identifiers and recovery keys are never
translated or mirrored into a different character order. Full translation
of older dialogs is a separate localization effort.

The main window saves **per user**: restored bounds, normal/maximized state,
and Update Center density beneath
`%LocalAppData%\BitKeyBridge\ui-workspace.json`. Invalid, tiny and fully off-screen bounds are ignored; partially visible
windows are clamped to the available monitor after RDP reconnect or a DPI
or monitor change.
No credentials, tokens or keys are saved there.

## CLI for helpdesk diagnostics

From the published single-file EXE after this feature is released:

    BitKeyBridge.exe --update-center-status --no-elevation

Returns read-only JSON for scripting. This does not trigger a network check
or update; call `--check-update --no-elevation` separately to request a
live GitHub check. Task Scheduler state uses Windows COM as a read-only query outside the
WinForms UI thread. The task principal (SYSTEM), executable path, highest
run level, single action and arguments must match the expected updater;
otherwise the dashboard explicitly warns of an unexpected task definition.

## Pilot acceptance

1. Open the app at 100%, 125%, 150%, 175% and 200% DPI, also through RDP.
   Confirm every caption wraps or scrolls; no cropped labels.
2. Restore/maximize, unplug a display or resize RDP, and relaunch; the window
   must open visibly. Compact preference must persist per user.
3. Switch to Russian then Hebrew and back while Updates is selected.
   Confirm right-to-left shell navigation with LTR secrets and identifiers.
4. Test Pause and Enable with a machine policy denying unsigned updates;
   there must be no installation or policy bypass.
5. Activate a synthetic rollback/failure hold; ordinary Save and Enable
   must never clear it. Only administrator-confirmed Resume after investigation
   and GPO/trust checks may clear it. Revoke prerelease permission mid-download;
   the scheduled worker and privileged helper must reject that preview.
6. Check task state as a standard Helpdesk user and SYSTEM. Confirm
   inaccessible task returns unavailable, not falsely missing/healthy.
7. Validate local JSON CLI status without GitHub access. Test GitHub Retry
   separately on a disposable workstation.
8. Verify x64/x86/ARM64 CI, CodeQL, protected audit, LAPS regression, ZIP,
   upgrade/rollback and `--ui-self-test` gates before promoting a new RC.

No release publishing, Intune/GPO linking, unsigned consent, task registration
or institutional code-signing setup is performed automatically by this PR.
