# BitKeyBridge v0.32.4 — Stable Hebrew Copy Diagnostics visibility hotfix

**Portable Windows x64 / x86 / ARM64 self-contained ZIPs. UNSIGNED.** Supersedes v0.32.3; the previous Stable remains available.

- Fix Copy diagnostics disappearing while using Hebrew/RTL or 200% display scaling. Copy and Dismiss buttons are placed in the first diagnostic-banner row with minimum height and left-to-right action flow, and the banner fills its full-width parent layout row.
- Add an additional always-available translated **Copy diagnostics** link to the bottom status strip, separate from the mirrored workspace and banner. The existing **Ctrl+Shift+P** / Tools > Copy UI diagnostics continues to copy a fresh sanitized performance report independently of banner visibility.
- Add a WinForms visibility/geometry regression for both buttons and the status link in EN/RU/HE using normal and double-sized fonts. New report contents remain non-secret. No device identity, recovery password, RBAC policy or connection setting is changed.
- The CI layout test verifies WinForms controls and bounds, but RDP/native paint lag and real-machine layout still require a pilot retest.

Assets: `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, `BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt`, `BUILD-INFO.txt`, `BitKeyBridge.cdx.json`, `BitKeyBridge-DomainPreflight.cmd` and `Install-BitKeyBridge-v0.32.4.ps1`. The pinned canary script is verified against released package SHA256 and a tampered ZIP. Use `-Mode Validate` in Windows PowerShell 5.1 before any approved install. Only an elevated administrator with `-Mode Install -ApproveUnsigned -Ring Canary` should perform opt-in pilot installation; broad rollout requires a separate approval.

**UNSIGNED.** No institutional certificate/CyberArk allowlisting, automatic GPO/Intune rollout or signature-trust bypass.
