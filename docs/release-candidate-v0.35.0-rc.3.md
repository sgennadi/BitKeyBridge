# BitKeyBridge 0.35.0-rc.3 — Devices Search and recovery UI QA

**Status: UNSIGNED manual QA only. Not Stable, not a GitHub Release and not permitted for automatic GPO/Intune rollout.**

## Identity and lineage

- Application/ProductVersion: `0.35.0-rc.3`; numeric Windows FileVersion remains `0.35.0.0`.
- One Draft [PR #115](https://github.com/sgennadi/BitKeyBridge/pull/115), historical branch `release/v0.35.0-rc.1`. The Git branch name does not define current SemVer.
- After all checks pass, obtain the **exact-head** unsigned CI artifact `BitKeyBridge-v0.35.0-rc.3-QA-UNSIGNED` with separate x64/x86/ARM64 portable EXE ZIPs and `SHA256SUMS.txt`. Confirm `BUILD-INFO.txt`, source SHA and each archive hash. Do not use an RC1/RC2 artifact when testing RC3.
- RC2 release notes: [previous candidate](release-candidate-v0.35.0-rc.2.md).

## What changed

1. **Responsive unified Devices search.** Metadata is collected independently from AD, Intune and Entra. Completed sources can display provisional rows before slower sources return; these rows cannot be used for secret retrieval until final reconciliation. The status shows each source's progress.
2. **Restart Search** cancels the old request before starting a new generation. Search remains responsive; old partial results and late failures may not overwrite newer ones.
3. **Narrow Intune search first.** Device name, full UPN and exact Entra device GUID use Graph server-side filters when supported. A rejected filter or no matching records falls back to existing multi-field client-side matching (name, serial, UPN, display name, managed ID or Entra ID).
4. **Entra lookup improvements.** BitLocker recoveryKeys list does not support `$top`; RC3 removes it. A GUID may be an exact key ID or device ID. Up to 64 Entra name matches are processed with six bounded concurrent metadata requests, and too-wide searches show explicit refinement guidance.
5. **Do not infer missing backups.** Per-device enrichment is capped at 32 matched devices; deferred identities are marked unknown and require narrowing by exact name/ID. A retry of one failed source must never invent verified absence of a recovery key.
6. RC2 protected Reveal/Show, LAPS, identity matching, ticket/JIT, RBAC, protected audit, clipboard expiry and Device 360 remain unchanged.

## Operator test matrix (real managed Windows 11; no secrets in reports)

- In **Devices**, search for a reasonably common substring such as `clinic`: observe quick provisional rows from whichever metadata source finishes first; verify the status changes from searching to AD/Intune/Entra completed or explicit failed/denied. When final, verify only source-qualified rows and no implicit Device Code/MFA popup.
- During a slow search press **Restart Search** after changing the query: previous results must not reappear, Cancel must work and buttons must become responsive. Use narrow and no-match queries; a genuine zero result is explicitly explained.
- Verify a specific known computer name, serial number, UPN, Intune managed device GUID and Entra Device GUID. A Graph 401/403 must be reported as such and not be treated as device/key absence. If too many Entra matches exist, use the actionable narrower-query message.
- Search AD-only while Microsoft 365 is disconnected: AD results must still be shown. Search cloud-only when AD is disconnected and the valid Graph session is already active.
- Select one final verified device and an actual returned Recovery ID. **Reveal/Copy** must require original authorization, ticket/protected audit and a valid already-connected Graph session; it must not automatically trigger MFA or return a key for another identity.
- Test Start → BitLocker exact AD DN + Recovery ID (including outside the selected OU), LAPS Reveal/Copy, and Device 360 on 100%, 125%, 150%, 175% and 200% DPI including RDP resize and Hebrew/Russian UI. Save only **sanitized diagnostics**.

## CI gates

The candidate is not ready until exact-source Windows x64/x86/ARM64 builds, offline integration tests, native WinForms UI, LAPS, Audit Broker, CodeQL, stable provenance, signing/tamper and legacy rollback are all green, and the exact QA artifact is generated. Corporate signing and live AD/Intune operator acceptance remain separate Stable gates.

```cmd
BitKeyBridge.exe --version
BitKeyBridge.exe --self-test --no-elevation
BitKeyBridge.exe --ui-self-test --no-elevation
```

`--version` must print `0.35.0-rc.3`.
