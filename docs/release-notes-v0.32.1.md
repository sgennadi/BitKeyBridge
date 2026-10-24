# BitKeyBridge v0.32.1 — Stable UI / translation hotfix

**Portable Windows x64, x86 and ARM64, self-contained single-file EXEs.** Successor to v0.32.0; all files independently built and tested from this tagged source.

## Fixes

- Hebrew RTL no longer combines native mirrored tabs with custom owner-drawn placement, eliminating the seven empty header areas beside tab captions.
- Language / Язык / שפה menu now autosizes and requests a proper WinForms menu reflow.
- Russian and Hebrew translation of 179 fixed admin labels, including Export & Automation, Updates, Coverage, Service, Audit and table headers, while preserving typed values and recovery credentials.
- Per-phase non-secret metrics for RTL/tab chrome, caption translations and layout resume.

## Optional managed canary installation

Download the correctly named `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, or `BitKeyBridge-win-arm64.zip` and `Install-BitKeyBridge-v0.32.1.ps1` from **this** GitHub Release. First verify `SHA256SUMS.txt`. The installer is pinned to the exact released ZIP hashes, checks PE architecture and EXE version, refuses modified ZIPs or a running recovery session, and keeps one prior executable for rollback.

With Windows PowerShell 5.1 from an elevated administrator session, verify a ZIP using `powershell.exe -NoProfile -File .\Install-BitKeyBridge-v0.32.1.ps1 -Mode Validate -Architecture win-x64 -PackagePath .\BitKeyBridge-win-x64.zip`. For an approved pilot only, run with `-Mode Install -ApproveUnsigned -Ring Canary`. An institution-wide rollout is **not** automatically enabled; it requires independent Windows/AD/Entra/CyberArk/SIEM acceptance.

**UNSIGNED.** No organizational Authenticode certificate, Azure signing or CyberArk allowlisting is asserted. Existing signed-only execution/update policies still apply, and the release does not create a GPO/Intune task or alter automatic-update consent. The existing v0.32.0 release remains available for rollback.
