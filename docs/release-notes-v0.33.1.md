# BitKeyBridge v0.33.1 — Tenant-safe Entra setup and Intune wizard refinement

Self-contained portable **Windows x64 / x86 / ARM64** ZIPs, **UNSIGNED**. Previous Stable v0.33.0 remains available for rollback.

- **Safer administrative setup:** tenant-wide Entra app registration/consent requires a specific tenant GUID or syntactically valid DNS tenant domain. Blank, all-zero GUID, generic `organizations`/`common`/`consumers` or malformed tenant input is rejected before contacting Graph. DNS syntax checks do not verify tenant ownership; Microsoft Entra still authenticates and authorizes the actual account.
- **Intune wizard EN/RU/HE:** starts with an empty or existing valid tenant, shows localized guidance, blocks Next on invalid tenant, and revalidates before Configure & Verify. Full native WinForms layout and 100–200% checks are retained.
- **First-Run / Repair:** applies the same privileged tenant check before consent. Ordinary delegated Device Code login still supports `organizations`, while an empty/all-zero client application GUID is not accepted for a stored approved app.
- **Devices → Cloud:** missing-auth warning can open the existing authorized Settings → Cloud tab without starting a new authentication operation or clearing device search.
- **Safety:** preserves cancellation behavior from v0.33.0, RBAC, audit, LAPS/BitLocker secret handling, trusted-updater restrictions, and the default no-automatic-deployment posture.

Release assets: `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, `BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt`, `BUILD-INFO.txt`, `BitKeyBridge.cdx.json`, `BitKeyBridge-DomainPreflight.cmd` and optional `Install-BitKeyBridge-v0.33.1.ps1`. Use `-Mode Validate` before any elevation. Unsigned install requires `-Mode Install -ApproveUnsigned -Ring Canary`; broad rollout needs additional approval.

**UNSIGNED:** no institutional code-signature or policy allow-list approval is conveyed by publication. A live authorized Entra/Intune test tenant and managed Hebrew/RDP/HiDPI acceptance remain outstanding; this release alone does not authorize GPO/Intune deployment.
