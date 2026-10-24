# BitKeyBridge v0.32.5 — Stable WinForms profiling improvements

Portable self-contained **Windows x64 / x86 / ARM64** ZIPs, **UNSIGNED**. This release supersedes v0.32.4, which remains available.

- **Initial DPI/font refresh:** separates main window and auxiliary windows, then shows timing for theme application, control defaults, working-area fit, PerformLayout, label-width adjustment and conditional theme refresh.
- **Language switching:** separately times actual warm TabControl RTL-direction/native mirror property changes and counts native handle creations/destroys (counts only, no HWND values).
- **Navigation:** for Start and other workspaces, splits total selection-to-layout-ready time into selection-to-UI-callback and viewport-reflow phases; p50/p95 of existing completed switches remain.
- **Security and usability:** keeps native Hebrew navigation, LTR recovery/identity text and Copy diagnostics accessible using the bottom status bar, Tools or **Ctrl+Shift+P**. Reports only aggregate fixed-page timing values.
- These changes improve diagnostics, not necessarily raw speed. Please send a fresh report after several EN → RU → HE → EN switches and visits to Start and Diagnostics so a targeted optimization can follow.

Artifacts: `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, `BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt`, `BUILD-INFO.txt`, `BitKeyBridge.cdx.json`, `BitKeyBridge-DomainPreflight.cmd` and `Install-BitKeyBridge-v0.32.5.ps1`. The optional Canary script validates pinned SHA256 values of the exact released ZIPs, rejecting modified content. Validate with PowerShell 5.1 `-Mode Validate` before elevated and approved `-Mode Install -ApproveUnsigned -Ring Canary`. Broad rollout needs separate approval.

**UNSIGNED:** no corporate Authenticode certificate or CyberArk allowlisting is implied. No GPO/Intune deployment, forced automatic update, update-consent change or trust bypass.
