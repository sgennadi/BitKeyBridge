# BitKeyBridge v0.33.0 — development and acceptance plan

**State:** development only, UNSIGNED, not a published Stable or prerelease.

**Source:** `release/v0.33.0`, starting from the published v0.32.5
lineage. The existing v0.32.5 release and installed fleets are unchanged.

## Candidate changes

- Prevent unnecessary expensive WinForms native RTL updates on hidden
  caption controls while still localizing their Text. Apply their requested
  direction on visibility and keep source names, secrets and identity fields
  left-to-right. Headless UI self-tests continue to exercise eager direction.
- Preserve `ToolStripItem.Tag` command data and keep original English
  menu source captions in a separate cache. Translate known static nested
  Tools and connection menu entries, including when late-created submenus open.
- During initial DPI layout, register wrapping labels once and measure their
  available width once **after** layout rather than redundantly during the
  registration tree walk and again in the final measurement pass.
- Run repeatable WinForms test sequences EN -> RU -> HE -> EN, including
  dynamic control registration, deep menus, LTR secret inputs, native
  TabControl mirroring, and 100%-200% UI scaling.
- Keep existing authorization, JIT, RBAC, audit journal/SIEM, recovery
  secret handling, release trust, rollback holds and update opt-in unchanged.

## Required CI on exact candidate SHA

1. .NET 10 Windows x64, x86 and ARM64 self-contained builds.
2. UI self-tests + source regressions including nested menu captions.
3. LAPS regression suite, CodeQL analysis.
4. Two-identity protected Audit Broker integration on disposable GitHub
   Windows runners.
5. Legacy upgrade, staging and rollback validation from supported baselines.
6. No publishing from this branch or from the v0.33.0 tag until a dedicated,
   reviewed exact-source stable release gate is added with owner authorization.
   In particular, a generic `Release v0.33.0` message is insufficient.

## Native Windows acceptance before stable / fleet deployment

- Compare baseline v0.32.5 and v0.33.0 on the **same** 100%, 150%, 175% and
  200% Windows 11 monitor and RDP sessions, recording the non-secret
  Ctrl+Shift+P report. Check cold DPI phases, native TabControl handle
  recreation, initial and warm EN/RU/HE switches, and tab navigation p50/p95.
- Check Settings, Diagnostics, LAPS, Devices and Helpdesk after switching
  away/back: no missing Hebrew/Russian captions, no clipped controls, no
  invisible recovery actions, and no stale RTL on newly shown controls.
- Confirm no passwords, keys, SIDs, tokens, device IDs or user data are
  included in logs, reported timings, or translated as fixed captions.
- Validate delegated live AD/Entra/Intune, CyberArk/WDAC/AppLocker, protected
  audit commit, service lifecycle and updater rollback on an approved canary.
- Code signing and institutional approval are separate from an unsigned
  GitHub release. No machine opts into unsigned updates or installs via GPO
  merely because this source branch exists.

**Performance honesty:** deleting repeated layout work is a source-level
optimization, not a measured end-to-end speedup until real Windows timings
confirm it. Published v0.32.5 is the known rollback baseline.
