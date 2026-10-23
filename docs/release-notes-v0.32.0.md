# BitKeyBridge v0.32.0 — Stable

**Windows x64, x86, ARM64** — portable, self-contained, single-file packages. Successor to v0.30.5, integrating published RC9 plus post-RC9 Hebrew/HiDPI fixes.

- Hebrew RTL navigation including dynamically inserted/duplicate-name captions and blank static action label recovery, without touching secret fields.
- Faster compact native Windows UI across tabs, RDP and HiDPI.
- Source-qualified unified AD/Entra Helpdesk device lookup, startup chooser with optional remembered connection and auto-connect.
- Graph Device Code and certificate sign-in, not password-based ROPC; protected audit and permission checks.
- Update Center: exact version/architecture/checksum validation, explicit unsigned auto-install opt-in, history, staging cleanup and rollback.
- Secret-free fleet diagnostics and reports.

Download `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip` or `BitKeyBridge-win-arm64.zip`, verifying `SHA256SUMS.txt`. One root-level `BitKeyBridge.exe` per archive. `BUILD-INFO.txt` records exact source SHA. CycloneDX SBOM and GitHub attestation evidence included.

**Unsigned binaries; no organization Authenticode/CyberArk approval.** No automatic fleet enrollment or unsigned consent changes. Previously opted-in Stable automatic-update clients may adopt the new version under saved policy. Complete real Windows/AD/Entra/RDP/CyberArk, audit and rollback canary testing before wider rollout. Previous v0.30.5 and the immutable RC9 remain available.
