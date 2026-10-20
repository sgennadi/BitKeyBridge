# Device Workspace consolidation (post-RC7 preview)

**Development-only UX refactor. NOT a tagged build, prerelease or production rollout.**
This branch is based on the RC7 preparation commit and must not be merged into the
RC7 source/release branch. RC7 is separately awaiting interactive acceptance.

## User-facing change

- **Start is the single Helpdesk device search and selection surface.**
- Helpdesk is now a compact troubleshooting workspace: one **Device Workspace**
  link, Recovery Steps, Access Health, Support Bundle, status and administrator
  guidance. The duplicate HelpdeskQuickSearch box and duplicate BitLocker/LAPS
  navigation buttons have been removed.
- A selected device's non-secret summary is shown on Helpdesk and updated from
  Start selection. No extra LDAP or Microsoft Graph query is issued when
  Helpdesk opens or the technician clicks Device Workspace.
- Ctrl+F in Helpdesk switches to Start's canonical search field.
- Navigating back to Start preserves an already selected exact result even
  when the query text is only a fragment of the selected computer name.
  The selected row receives focus when available; without a selection the
  regular Start search input receives focus. A genuinely changed query still
  cancels stale work and invalidates results.

## Security boundaries

- The selected HomeSearchResult is kept as an **in-memory metadata selection**;
  a hostname is never treated as a recovery authorization or an AD/Entra
  object identity.
- Ambiguous records and cloud rows without a source-qualified identity are
  clearly marked unverified in the Helpdesk summary.
- Only existing BitLocker/LAPS controls perform recovery; existing RBAC,
  current source, JIT, protected-audit and secret-lifetime checks remain intact.
- No passwords, recovery keys, access tokens, machine credentials or sensitive
  audit payloads are copied into Helpdesk or stored in a persistent cache.
- No GPO, SYSTEM scheduled task, signing, updater policy or opt-in setting is
  changed. Stable and existing RC7 preparation remain untouched.

## Regression gates

- Windows x64/x86/ARM64 builds and the existing LAPS, CodeQL, protected audit,
  migration/rollback and release safety pipelines.
- Source regression rejects a duplicate Helpdesk search or wrong Ctrl+F target.
- Native WinForms --ui-self-test at Compact, 150%, 175%, RDP Compact 200% and
  200% stress sizing validates the Helpdesk summary, no duplicate search box,
  selected-result persistence across Helpdesk -> Start and BitLocker navigation.
- Windows interactive pilot: connect to AD and Entra; enter a short query,
  select one exact record, visit Helpdesk and return to Start; verify the same
  selected result and no automatic recovery-secret retrieval. Repeat with
  ambiguous same-name computers, cloud-only records, disconnected sources,
  150-200% DPI, RDP resolution changes, EN/RU/HE.

## Later phases (not implemented here)

A fully unified source-qualified Device 360 screen, lazily loaded search results
and settings simplification require additional design and authorized-domain
testing. They must not silently combine devices by hostname or remove dedicated
BitLocker/LAPS tools before an equivalently safe workflow is verified.
