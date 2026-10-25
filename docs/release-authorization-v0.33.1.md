# BitKeyBridge v0.33.1 — conditional authorization for an UNSIGNED GitHub Stable release

Date: 2026-10-25. The maintainer requested continuation of the BitKeyBridge release after published v0.33.0. This gate permits only an on-demand public GitHub release of v0.33.1 after every exact-source CI check below passes. It is NOT evidence that managed Entra/Intune, AD, Hebrew RDP or university security acceptance has passed.

## Mandatory immutable-source gates

- Release only from `release/v0.33.1` with HEAD commit message exactly `Release v0.33.1`, project/assembly version `0.33.1`, and matching immutable release tag `v0.33.1`.
- Windows x64/x86/ARM64 build and WinForms self-test, LAPS regression, same-SHA CodeQL, protected audit broker integration with actual standard-user tokens, legacy updater migration and rollback must all pass on the release SHA.
- Release job assembles ONLY downloaded CI-tested binaries, generates SBOM and SHA256SUMS, attests provenance, verifies a SHA- and source-pinned optional canary installer for all three architectures, and rejects deliberately tampered ZIPs BEFORE publishing.
- If any check fails, publish nothing. Never force tags or rewrite previous releases. After the protected workflow publishes, merge the identical tagged source as an ancestor into `main`, so its stable provenance guard stays intact.

## Scope and security restrictions

This is a public **UNSIGNED** downloadable release only; not approval to deploy by GPO/Intune or bypass CyberArk EPM, WDAC, AppLocker, organizational Authenticode/Azure signing or existing signed-only updater rules. The optional installer requires explicit unsigned consent and an administrator-chosen Canary ring; broad rollout needs separate specific authorization. No change to authorization, protected audit, secret-export, device policy, or update opt-in defaults is permitted.

Manual approved-tenant testing of administrative registration/consent, delegated Device Code cancellation, real Intune and managed Windows/Hebrew/RDP/200% DPI, and canary rollback is still required before deployment in production. Automated local validators do not prove Entra domain ownership, application consent, or correct permissions in a real tenant. Keep v0.33.0 available as rollback baseline.
