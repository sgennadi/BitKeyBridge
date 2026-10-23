# BitKeyBridge v0.32.0 Stable publication authorization

Date: 2026-10-23. Repository owner requests public Stable publication.

## Exact release boundaries
Only publish from `release/v0.32.0`, exact commit subject `Release v0.32.0`, and **all on-SHA GitHub Actions release checks successful**. Required: x64/x86/ARM64 root EXE ZIP packages, UI/HiDPI/WinForms, LAPS, CodeQL, protected audit integration with two real non-admin Windows identities, tested legacy update/rollback, updater staging and ZIP checks, SHA256SUMS, SBOM and provenance. Never replace tags or existing release assets.

## Trust, deployment and pilot limitations
**UNSIGNED.** No organization-issued Authenticode certificate, Azure Trusted Signing identity or CyberArk acceptance is asserted. SHA-256 plus provenance do not replace publisher signatures.

Real Windows 11/RDP/200%-DPI Hebrew, production domain AD and delegated Entra/Intune access, CyberArk EPM/WDAC/AppLocker, SIEM journal and update/rollback pilot acceptance **has not been performed by these GitHub workflows**. Public Stable is not authorization for an institution-wide rollout; conduct a managed canary before expansion.

Publication never links GPOs, creates enrollment tasks, changes auto-install consent or weakens update signer/trust rules. Machines already configured and explicitly consented to unsigned Stable auto-install may independently adopt this release subject to existing local policy and rollback holds. To avoid that, leave automatic install disabled.

Promote the published stable source to `main` only after release/tag existence and stable provenance checks.
