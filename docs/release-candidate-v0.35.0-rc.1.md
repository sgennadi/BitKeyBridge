# BitKeyBridge 0.35.0-rc.1 — unified release candidate

**Status:** Release candidate source only; UNSIGNED manual QA, not Stable and not authorized for production/GPO/Intune distribution.

- Published rollback baseline: [v0.33.1](https://github.com/sgennadi/BitKeyBridge/releases/tag/v0.33.1)
- Integrated feature sources: [PR #110](https://github.com/sgennadi/BitKeyBridge/pull/110) (v0.34 EN/RU/HE, DPI and layout) plus [PR #114](https://github.com/sgennadi/BitKeyBridge/pull/114) (Device 360), with their commit history retained.
- NuGet maintenance: [PR #111](https://github.com/sgennadi/BitKeyBridge/pull/111) and [PR #112](https://github.com/sgennadi/BitKeyBridge/pull/112), with System.DirectoryServices.Protocols and System.Management at 10.0.14.
- Final candidate commit / PR and exact-head CI: fill only after GitHub has built this candidate. Do not reuse an earlier PR #114 success as proof of RC acceptance.

## Product scope

The combined preview adds the Device 360 *read-only* cross-source computer card, conservative connection/source-health signals, metadata-only BitLocker/LAPS presence, a source-qualified identity and launch/navigation to the existing authorized workspaces. It also brings the complete 0.34 EN/RU/HE localization, Hebrew RTL/LTR rules, anti-aliased rounded visual styling, nav coalescing, layout caching and small-window/RDP behavior.

The card does **not** determine or grant actual recovery access, download recovery IDs/keys or LAPS secrets, change certificates, refresh tokens, execute consent, query Graph/AD, create persistent device caches or initiate rotation. Existing reveal/copy/rotate workflows retain RBAC, ticket/JIT, protected audit and exact identity/source checks. A source lookup failure is **unverified**, not evidence that a device or key is absent.

## Exact-source CI and packaging gate

1. The **final integration commit SHA** must have successful Windows native build + `--self-test` + `--ui-self-test` for x64 (and compilations for x86/ARM64), LAPS, protected Audit Broker (two non-admin Windows tokens), ephemeral positive test signing, legacy upgrade/rollback, CodeQL and provenance checks. Skipped integration jobs are **not** accepted as green.
2. Verify that `ui-device-360-en/ru/he.png`, `ui-device-360-en/ru/he-compact.png`, and `ui-device-360-en/ru/he-policy.png` from the **same final SHA** show no clipped text, source rows, rounded corners or footer buttons. The three-source Hebrew ordering must be AD (right), Entra (center), Intune (left).
3. An unsigned QA bundle must contain Windows `win-x64`, `win-x86`, `win-arm64` *single EXE* ZIP packages, exact SHA build manifest, SHA256SUMS, and clear nonproduction instructions. Recompute hashes; fail closed on a mismatch. Do **not** call the bundle organization-signed.
4. Never expose recovery IDs, BitLocker keys, LAPS secrets, tokens, user credentials or real directory names in the bundle, test logs or screenshots.

## Manual domain and device acceptance — mandatory before Stable or fleet rollout

Use a disposable, managed Windows 11 computer; do not run the unsigned RC on a domain controller or production helpdesk desktop. Test normal users, delegated technicians and denied technicians with actual AD, Entra and Intune, including incomplete/failed source lookups, correct computer selection, permission denial and protected audit. Validate EN → RU → HE → RU → EN, High Contrast, RDP reconnect, and monitor scaling 100%, 125%, 150%, 175%, 200%, including compact 610x440 layout. Confirm reveal/copy and rotation remain strictly operator-initiated in their existing authorized surfaces.

Test a **real organizational Authenticode signing identity** and signed-only canary rollout/rollback (including recovery from interruption) before any managed deployment or trusted update. The CI's ephemeral positive signing check does **not** satisfy this gate. Do not silently allow unsigned automatic updates.

## Per-user appearance preferences

The View menu adds UI Density (Auto/Compact/Comfortable) and UI Scale
(90/100/110/125%). Defaults are Auto/100%, saved in the existing
per-user ui-workspace.json without affecting machine policy, RBAC, Graph,
audit or protected secrets. Reset visual layout restores Auto/100%.
OS PerMonitorV2 scaling remains enabled and independent. Native CI
and manual Windows 11 validation at 100/125/150/200% Windows DPI
are mandatory before Stable promotion.

## Operator QA

Extract only the package matching the test computer architecture and verify `SHA256SUMS.txt` and `BUILD-INFO.txt` first. Run under a standard user from a disposable folder:

```cmd
BitKeyBridge.exe --version
BitKeyBridge.exe --startup-diagnostics --no-elevation
BitKeyBridge.exe --self-test --no-elevation
BitKeyBridge.exe --ui-self-test --no-elevation
BitKeyBridge.exe --audit-broker-preflight --no-elevation
```

Then launch the WinForms GUI manually and examine all Device 360 source states and the localized footer on Start and Devices. Record only safe high-level pass/fail evidence; do not export recovery secrets.

**Release decision:** The RC may be built and downloaded for isolated QA after exact-HEAD CI. Publishing a stable `v0.35.0` tag, promotion to default `main`, signing/deploying, or changing update/authorization defaults requires a separate explicit acceptance once the outstanding real-world gates are met.
