# BitKeyBridge v0.32.1 — owner-authorized unsigned Stable

Date: 2026-10-24. The repository owner explicitly requested direct Stable publication and permission for controlled managed deployment. No further branches or pull requests are needed for this release.

## Exact publication boundaries

GitHub Actions must publish from the existing `release/v0.32.1` branch **only** on a commit with exact subject `Release v0.32.1`. All tests must finish successfully on the source SHA attached to the immutable tag: Windows x64/x86/ARM64, WinForms RTL/Russian/Hebrew/HiDPI, CodeQL, LAPS, two-real-user Protected Audit Broker integration, legacy migration and rollback, updater trust/rollback guards and ZIP package integrity.

Published assets must include three architecture ZIPs, SHA256SUMS, BUILD-INFO with source SHA, CycloneDX SBOM, and provenance attestations. Do not overwrite `v0.32.0` or reuse its binaries.

## Controlled deployment

The release job generates `Install-BitKeyBridge-v0.32.1.ps1` with hashes of the three exact ZIPs, verifies each archive in Windows PowerShell, and rejects a deliberately modified archive before publishing. Use it only with a trusted copy of the release assets; start with Validate and an explicit, elevated `-Mode Install -ApproveUnsigned -Ring Canary`. Broader distribution requires separate documented pilot success and `-Ring Broad -AuthorizeBroadRollout`.

The script does **not** enroll GPO/Intune devices, create tasks, grant recovery privileges, alter signers, change update opt-in or bypass Windows Defender, CyberArk EPM, WDAC or AppLocker.

## Signing and production environment limits

**UNSIGNED** executable. SHA-256 hashes and GitHub provenance are not an institutional Authenticode certificate or CyberArk allowlisting. No live acceptance of domain AD, delegated Entra/Intune, Windows 11 RDP/200%-DPI Hebrew, audit/SIEM or enterprise update/rollback has been performed by GitHub-hosted runners. Pilot on managed machines before fleet expansion. Previously consented unsigned automatic-update clients may discover a newly published Stable release under their preexisting local policies; this release does not opt clients in.
