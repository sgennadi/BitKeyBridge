# BitKeyBridge v0.33.0 — Stable localization and Intune sign-in fixes

Self-contained portable **Windows x64 / x86 / ARM64** ZIPs, **UNSIGNED**.
Previous stable v0.32.5 remains available for rollback.

- **Intune / Entra Setup Wizard:** keep Back, Next and Cancel visible in one
  non-wrapping footer across all three steps and EN/RU/HE 100-200% UI scaling.
  Report missing Tenant ID, invalid/empty Client ID or certificate thumbprint
  before attempting Microsoft Graph connection.
- **Reliable Device Code cancellation:** an explicit Cancel, Escape or window
  close stops sign-in polling. Cloud and administration Cancel immediately stop
  the activity marquee; late progress callbacks no longer overwrite the final
  canceled state. Cancel never authorizes a new app registration.
- **Language/HiDPI:** translate previously missing nested menu and Helpdesk
  preparation captions, with scoped translations for diagnostics; keep secret
  and identity inputs LTR in Hebrew. Reduce redundant cold label measurements
  and hidden-page RTL changes.
- **Security:** preserve existing protected audit and authorization checks,
  read-only defaults and signed-only trust restrictions. Run multi-architecture
  builds, native UI/LAPS, CodeQL, broker integration, migration/rollback and
  canary installer tamper tests on the exact release SHA.

Artifacts: `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`,
`BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt`, `BUILD-INFO.txt`,
`BitKeyBridge.cdx.json`, `BitKeyBridge-DomainPreflight.cmd`,
and the **optional** `Install-BitKeyBridge-v0.33.0.ps1`.
Use `-Mode Validate` before any elevation. Installing unsigned binaries needs
explicit `-Mode Install -ApproveUnsigned -Ring Canary`; broad rollout needs
separate approval.

**UNSIGNED:** no enterprise code-signing or CyberArk/WDAC/AppLocker approval.
Do not enable unsigned automatic updates, push by GPO/Intune, or bypass
signed-only policies. Fresh live Windows/RDP measurements and managed-domain
pilot acceptance are still necessary; source-level layout optimizations are
not guaranteed performance improvements.
