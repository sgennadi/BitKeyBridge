# BitKeyBridge v0.32.2 — Stable localization and grid corrections

**Portable Windows x64, x86, ARM64. UNSIGNED.** Updates v0.32.1; earlier stable assets remain available.

- Extend Russian and Hebrew captions across AD/Entra connection workflows, diagnostics, LAPS, RBAC and protected-storage guidance.
- Fix Start source selection, including dynamic expand/collapse translations without changing the selected identity provider or credentials.
- Translate Start, BitLocker, LAPS and LAPS Coverage table headings, including late-created columns. Preserve every row, key, SID, account, recovery value and RBAC/audit decision.
- Batch first-use WinForms caption updates and preserve late-created control localization. Increase minimum widths of translated column headings instead of clipping Russian/Hebrew.
- Add regression tests for repeated Hebrew/Russian/English language switching, modal guidance, grid headers, source-choice stability and HiDPI layout.
- Real Windows/RDP Hebrew layout performance and some dynamic runtime status translations still require separate user acceptance; speed improvements are not represented as measured benchmarks.

## Downloads and trust

Release includes `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, `BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt`, `BUILD-INFO.txt`, CycloneDX SBOM, provenance and optional `Install-BitKeyBridge-v0.32.2.ps1` with embedded SHA256 pins for this exact release source.

Validate a downloaded package with the supplied installer in Windows PowerShell 5.1 (`-Mode Validate -Architecture win-x64 -PackagePath .\BitKeyBridge-win-x64.zip`) before an approved pilot. Any modified ZIP must be rejected. Managed pilot installation explicitly requires elevation, `-Mode Install -ApproveUnsigned -Ring Canary`; institution-wide rollout needs independent testing and `-Ring Broad -AuthorizeBroadRollout`.

**UNSIGNED:** no organizational Authenticode certificate or CyberArk EPM allowlisting. Does not enroll GPO/Intune computers, change auto-update consent, skip signer policies, or bypass recovery authorization.
