# BitKeyBridge v0.32.3 — Stable WinForms language and diagnostic hotfix

**Portable Windows x64, x86, ARM64. UNSIGNED.** Supersedes v0.32.2; earlier Stable remains available.

- Set main workspace page content explicitly LTR *before* its native tab navigation is mirrored in Hebrew. Avoid expensive inherited RTL flips of child controls during EN/RU/HE changes; identity/secret field directions remain unchanged.
- Preserve the active redacted diagnostic banner and its Copy diagnostics / Dismiss buttons across language switches. Add Tools > Copy UI diagnostics with a native **Ctrl+Shift+P** shortcut for a fresh sanitized timing report even if the inline banner is clipped.
- Runtime WinForms tests cover copied report retention, translated controls, native shortcut registration, source selection stability, RTL/LTR tab direction and scaling.
- New RDP/200% Hebrew measurements are still required; a speed improvement is not yet confirmed.

Assets: `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, `BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt`, `BUILD-INFO.txt`, `BitKeyBridge.cdx.json`, `BitKeyBridge-DomainPreflight.cmd`, and `Install-BitKeyBridge-v0.32.3.ps1`. Validate with `-Mode Validate` before opt-in elevated `-Mode Install -ApproveUnsigned -Ring Canary`; broad rollout requires separate signoff.

**UNSIGNED**; no institution signing or CyberArk allowlisting, GPO/Intune auto-rollout, automatic consent or security-policy bypass.
