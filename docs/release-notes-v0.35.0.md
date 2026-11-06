# BitKeyBridge v0.35.0 Stable

**Public UNSIGNED manual-download release** for Windows x64 / x86 / ARM64. Previous Stable v0.33.1 remains available.

- **Start, Devices, Device 360:** one verified device search and one valid Graph session, source-qualified AD/Entra/Intune identity matching, Hardware (authorized BIOS/TPM/WMI), Security, Audit, Diagnostics and Source Health.
- **BitLocker and LAPS:** actionable Show/Copy statuses, exact Recovery ID and identity requirements, protected audit, JIT/ticket/RBAC and stale-session/selection key invalidation. No automatic MFA during metadata search.
- **Windows UI:** EN/RU/HE, Hebrew RTL, native icon, improved DPI/RDP compact layout and saved window geometry.
- **UniversalDiagnostics:** local-only privacy-classified events with explicit consent, manual preview/export and cancellation of queued writes after consent is disabled. No GitHub PAT or automatic upload.
- **Update Center:** existing trusted update/rollback rules retained; separately integrity-pinned optional Canary installer.

Download `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip` or `BitKeyBridge-win-arm64.zip`; verify SHA256SUMS and BUILD-INFO.txt. Extract and run `BitKeyBridge.exe` on an approved Windows test computer. The optional `Install-BitKeyBridge-v0.35.0.ps1` must be validated before any use and is not authorization for broad deployment.

**Not enterprise signed.** Real tenant/AD/Graph/Intune tests with authorized and denied BitLocker/LAPS read, remote WMI, RTL/DPI/RDP, corporate code signing and managed canary/rollback are still required before GPO/Intune rollout. No unsigned fleet install is authorized by this release.
