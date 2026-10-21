# RC8 preview: compact Connection and Settings

**Development-only follow-on to PR #100. Not tagged or published.**
Base: `feature/rc8-unified-device-workspace-20261020`. The separate RC7
release-preparation PR #99, existing release tags and Stable are unchanged.
The project version still identifies the RC7 source baseline until an
independently accepted RC8 release candidate is prepared.

## Connection workflow

- The Start workspace shows one primary `Connect / Change...` action;
  `More connection options...` exposes the existing AD/Entra source
  selector, direct Microsoft 365 sign-in and cloud configuration.
  Existing control identifiers and underlying authentication paths remain.
- The startup chooser names four options plainly: AD, Microsoft 365,
  both, or offline Start. `Cancel` closes without saving a new selection;
  `Open Start` explicitly selects no source.
- The per-user 'Use this source next time' control keeps the existing
  explicit opt-in and machine-policy gate. AD passwords, Graph tokens,
  secret recovery values and MFA consent are never persisted by this option.
- Cloud onboarding starts with approved-app delegated sign-in. Detailed
  required Graph scopes appear only when requested; the short note still
  states that an approved app and per-user permissions are necessary.
  Selecting **Administrator: configure an app** can open the existing
  administrator-only setup wizard even when no tenant/client ID is entered.
  No automatic registration or consent is added.

## Compact Settings

- Settings > Helpdesk defaults to visible setup status, support actions,
  and a non-secret **Saved recovery policy** summary.
- Environment profiles and Recovery Access Policy are separate,
  individually expandable sections. The same controls and save actions
  remain; collapsing or expanding does **not** persist a configuration.
- The summary reflects the **saved** protected-audit requirement and ticket
  requirement, never an unsaved checkbox state or an access decision.
- Helpdesk > Setup & Permissions navigates directly to the appropriate
  Settings page and opens the otherwise-collapsed recovery policy section.
- Enterprise controls remain under Settings > Enterprise; existing RBAC,
  protected audit, JIT, secret lifetime, source qualification and updater
  authorization checks are unchanged.

## Reset Visual Layout

- Settings > Helpdesk > **Reset Visual Layout...** asks for confirmation.
- Restores the default BitLocker / LAPS / Computers card order, compact
  Update Center and a maximized, visible main window. Collapses optional
  Connection, Environment Profiles and Recovery Access Policy sections.
  Only per-user visual preferences are written.
- Does **not** change AD/Entra source, remembered authentication method,
  RBAC, recovery policy, tokens or recovery secrets. Existing card controls
  and selected metadata are preserved while the outer pane hosts move.
- Use this after RDP session resizing, monitor removal or unexpected panel
  reordering. A new offline WinForms test covers the visual reset.

## UI rendering performance

- Repeated identical status values no longer set AutoSize text/images, avoiding
  invalidation of surrounding panels on polling updates.
- DpiAwareForm no longer repeats its entire layout on first Shown or triggers
  a second unconditional full PerformLayout on DPI/font refresh. Theme regions
  refresh when DPI/high-contrast state actually changes.
- The selected chain of nested TabPages is explicitly repaired only after
  selection/DPI changes or when geometry is genuinely stale. Normal WM_SIZE
  relies on native Dock=Fill rather than forcing every flow section to layout.
- Diagnostics > Tools > **UI Performance...** shows non-secret in-memory
  counts, mean/worst milliseconds and >50 ms samples for viewport, recovery
  cards and DPI reflow. It cannot measure slow AD/Graph operations or native
  Windows painting; real user timing remains necessary.

## Old ProgramData update folders

- When enabled, existing housekeeping includes stale staging folders under
  `%ProgramData%\\BitKeyBridge\\Updates` that match BitKeyBridge's version
  (`v<semver>`) or helper/rollback (`helper-<GUID>`, `rollback-<GUID>`)
  naming conventions. Default minimum age: 7 days, using the existing
  `TemporaryFileRetentionDays` setting. A value of 0 disables deletion.
- The cleanup does not touch other ProgramData folders, event/audit journals,
  configurations, current or rollback-held versions, the running executable,
  unknown directories, symlinks, `.bak` or pending newer staging.
- Cross-process scheduled-download and apply/rollback gates are checked
  before cleanup. A busy updater causes a safe skip, not a forced deletion.
  Runs with the existing housekeeping worker (normally daily if the Windows
  service is enabled) or its explicit administrative CLI command; neither
  a new scheduled task nor a machine-wide policy change is introduced.
- Offline --self-test now covers dry-run counts and preservation of current,
  recent and unknown folders. Retained versions are never removed based on a
  path wildcard or without the age check.

## Automated verification

- Windows x64 / x86 / ARM64 compile and package checks.
- Native `--ui-self-test` for 100/150/175/200% font scenarios, including
  visible setup overview, retained controls, offline vs cancel wizard,
  and switching approved-app vs administrator cloud setup.
- UI source regression enforces no duplicate search, an explicit connection
  primary action, collapsed optional Settings and no overwrite of existing
  release gates.
- LAPS, protected-audit two-identity checks, CodeQL, and legacy rollback
  suites must pass on the same commit before the PR is considered ready.

## Interactive acceptance still required

Run on Windows 11 and through RDP at 100%, 150%, 175% and 200%, including
reconnect and resize. Check that the primary Connect button stays visible
while optional options wrap normally; expanding profile and recovery
settings must never horizontally clip controls. Test all AD/Entra/Both/offline
choices, a cloud setup with *no* existing App Registration, and a normal
helpdesk token that must not be allowed to run admin provisioning. Check
saved vs unsaved protected-audit status, Settings shortcut navigation,
English/Russian/Hebrew text and RTL, and authorized/denied recovery controls.
Do not promote unsigned builds into managed production before signing and
CyberArk/EPM allowlisting acceptance.
