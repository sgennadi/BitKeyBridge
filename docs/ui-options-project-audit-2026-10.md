# BitKeyBridge UI, options and architecture audit — 2026-10-14

## Scope and baseline

Reviewed the `0.29.11` mainline WinForms shell and core configuration, the Helpdesk/Cloud/Updates/Advanced/Health/Audit navigation, LAPS and BitLocker surfaces, connection wizards, updater controls, and source tree. The repository contains approximately 164 C# files; important large files include `UI/MainForm.cs`, `UI/MainForm.Shell.cs`, `UI/MainForm.Laps.cs`, `UI/MainForm.HelpdeskUx.cs`, `Program.cs` and `UI/UiLayoutSelfTest.cs`.

This is a code-and-configuration audit, not an exhaustive manual interactive walkthrough against production AD or Microsoft Graph. Windows GUI scaling checks and CI cover structural regressions; real access requires operator validation.

## Safe reductions implemented in 0.29.12

| Area | Confirmed issue | Change |
| --- | --- | --- |
| Start / Connections | Independent Environment Check, Device Diagnostics and Advanced buttons compete with Connect, Disconnect and the persistent Reconnect wizard. | Consolidate secondary commands under **Connection tools...**, preserving all original handlers and actionable dialogs. AD-only advanced settings remain conditional. |
| Start / Advanced AD | **Auto-connect to AD when the GUI starts** is a leftover GUI checkbox from the unused `InitializeRecoveryWorkspaceAsync` path. Current launch always uses `ShowStartupConnectionWizardAsync`. | Remove the misleading GUI toggle and unreachable alternate startup method. Retain `AppConfig.AutoConnectOnStart` in the serialized schema for legacy config/CLI compatibility, but do not display or rewrite it from the GUI. |
| Start / Cloud | Advanced AD fields could remain expanded after switching to cloud-only. | Hide AD-only advanced fields when the selected source no longer includes AD; keep Connection tools available for cloud/AD troubleshooting. |
| Updates | **Disable/Enable Startup Checks** duplicates the **Automatically check for updates** checkbox and Save Update Settings. | Remove the duplicate immediate-action button and its orphaned handler. The checkbox + Save is the single configuration path. |
| Updates | **Open Release Page** repeats the global **Tools → Latest GitHub release** command. | Remove the duplicate button, keeping the global link. |
| Updates | **Resume Auto-Install** appears despite there being no rollback hold. | Show only when `UpdateRollbackHoldService.HasRecordedHold()` is true. Keep verified rollback/resume logic intact. |
| UI regression | Diagnostics could disappear silently during simplification. | Update UI self-tests to verify Connection tools contains Environment Check, Device Diagnostics and Advanced AD settings. |

## Preserve — not duplicate or dead

- **Start vs BitLocker/LAPS detailed workspaces:** Start is the metadata-first helpdesk path. Dedicated workspaces still contain deeper search, history, per-record diagnostics, source selection and operational workflows. Do not remove them until feature equivalence is verified.
- **Startup / Cloud connection / Intune setup wizards:** Different roles: connection-source choice, delegated non-admin Graph login, and admin provisioning/app-registration setup. Do not merge by simply granting broad Graph scopes.
- **LAPS history, BitLocker record history, on-demand secret reveal/copy, audit, redaction, scoped permissions, sensitive clipboard timeout:** security and operational functionality.
- **RBAC / JIT / two-person approval / SIEM / remote API / service / export / coverage / audit signing:** optional enterprise operations; defaults and authorization gates must remain intact even if hidden from everyday users.
- **Offline CLI, signed/verified updates, online and local rollback:** preserve independent operational and recovery paths.

## Follow-up simplifications — require separate PRs

1. **Separate MainForm responsibilities:** Move AD session handling, updater UI handling, and administration settings out of monolithic `MainForm.cs` into focused partial classes/services. Make code smaller *without* changing credential lifecycle, event ordering or authorization. Keep `Program.cs` CLI behavior stable while splitting command handlers by domain.
2. **Basic/Advanced progressive disclosure:** Keep Helpdesk settings to ticket/reference, clipboard/secret lifetime and source/account selection. Move export, service identity, remote API, SIEM and certificate management into explicitly labeled enterprise/advanced sections. Do not change default security gates.
3. **Standardized input validation:** Review port ranges, tenant/client IDs, regex ticket patterns, export scope safety and cross-control enablement. Prefer one validator per domain to duplicated form-level checks.
4. **Consolidate diagnostic entrypoints without losing tools:** Link Start's Connection tools to the existing Access Health/diagnostics center rather than implementing new probe engines or confusing navigation-only buttons.
5. **Retire obsolete config only through schema migration:** Audit legacy `AutoConnectOnStart` and other unused serialized fields across GUI/CLI/service and example JSON. Keep reading legacy files until migration tests cover older installations.
6. **Review stale open PRs #17 (export UI sizing) and #27 (older helpdesk UX):** Their bases predate the 0.29.11 mainline. Compare changes before rebasing or closing; do not merge old release metadata over current code.

## Release checks

Run `build.yml` for Windows x64, x86 and ARM64, the LAPS and Helpdesk regressions, the runtime WinForms UI self-test (normal, compact, 150%, 200% DPI), CodeQL and the verified updater self-test. Verify source-chooser and manual-password retry under an actual AD session before describing network behavior as field-tested.

**Rule:** UI cleanup may hide or regroup controls but may not bypass authorization, create implicit password reads, log plaintext, silently save credentials, or enable an optional enterprise service.
