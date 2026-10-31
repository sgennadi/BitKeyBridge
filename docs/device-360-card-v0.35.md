# Device 360 computer card — v0.35 draft

This is a development-only increment building upon the existing Device 360 metadata
service and v0.34 Start layout. **Not a published release, entitlement check,
secret reader, deployment instruction or replacement for authorization.**

## Operator workflow

- Select one exact row on **Start** and press **Computer card**.
- Alternatively select one row in **Devices** and press **Computer card**
  (or activate its row).
- The native DPI-aware card displays independent AD, Entra and Intune source
  states; exact source-qualified device identity where available; BitLocker
  backup metadata count and LAPS metadata state; OS/hardware/compliance fields
  when actually supplied by the current source.
- **Not verified** means no confirmed answer, never that no backup exists.
  A collision or missing authoritative identity blocks guided navigation.
- From a verified Start AD row, *Open BitLocker* and *Open LAPS* lead to
  existing dedicated workspaces. From a verified Start cloud row,
  *Open Devices* navigates only to Devices; the operator must independently
  select/confirm the exact managed device before a subsequent operation.
- The Devices card is read-only; its existing source-specific Reveal, Copy,
  ticket, audit and Intune rotation buttons remain on Devices, not inside
  this dialog. There are no new secret, network, clipboard or privilege actions.

## Data boundaries

The shared `Device360Snapshot` projection intentionally excludes recovery
IDs, BitLocker recovery passwords, LAPS passwords, tokens, sessions and
credentials. It is rendered only in memory from the original selected
`HomeSearchResult` or `UnifiedDeviceInfo`, without additional LDAP or Graph
calls. An Entra recovery-key-only ID is *not* accepted as a device identity.

AD, Entra and Intune source presence in the same card is **not** a claim
that same-name directory objects refer to the same device. Source identities
are qualified; unqueried sources are shown as **Not verified**. A selected row
must still be the exact same object with the same source identity after the
modal closes before any navigation request proceeds.

No device data is written to new persistent files or logs. The test-only
screenshots contain exclusively synthetic records, are emitted only when
`GITHUB_ACTIONS=true` and `BITKEYBRIDGE_SYNTHETIC_UI_SNAPSHOTS=1`,
and never contain recovery IDs.

## Regression acceptance

- Windows x64/x86/ARM64 and CodeQL; native WinForms UI self-test at compact,
  150%, 175% and 200%, including Hebrew RTL.
- Tests must reject ambiguous identity routes and recovery-ID-as-device-ID.
- Test window text and screenshots contain no synthetic recovery-ID sentinel.
- Review native `ui-device-360-en.png`, `ui-device-360-ru.png` and
  `ui-device-360-he.png` plus old Start snapshots for clipping/rounding.
- Keep existing strict RBAC, JIT, ticket, protected-audit and recovery gating,
  including legacy update migration/rollback and installer provenance.
- Manual managed Windows 11 + RDP acceptance at 100%–200% and AD/Graph
  delegated vs denied users remains mandatory before production approval.

No changes to stable 0.33.1 or v0.34 PR #110 are made by this branch.
