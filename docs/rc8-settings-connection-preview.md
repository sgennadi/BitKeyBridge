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
