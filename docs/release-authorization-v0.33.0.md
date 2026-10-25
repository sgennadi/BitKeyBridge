# BitKeyBridge v0.33.0 — conditional authorization to publish an UNSIGNED Stable release

Date: 2026-10-25. Publication of v0.33.0 was requested by the maintainer for
fixes to the Intune / Entra setup wizard, Device Code cancellation and EN/RU/HE
WinForms localization and layout. **This document is a release gate, not proof
that the requested CI or managed-machine acceptance has passed.**

## Mandatory exact-source conditions

- Publication must originate **only** from `release/v0.33.0` with commit
  message exactly `Release v0.33.0`, project/assembly version 0.33.0 and
  a matching immutable `v0.33.0` tag targeting that commit.
- On that exact SHA, Windows x64/x86/ARM64 compiled bundles and native WinForms
  UI self-tests must succeed, along with LAPS regression, CodeQL manual-build
  security analysis, two-real-standard-token Protected Audit Broker integration
  and Windows hosted legacy upgrade/rollback regression.
- No release is created unless the GitHub Actions release job completes all
  gates and passes local staged updater consent smoke tests, SBOM generation,
  SHA256SUMS, release attestation, and an exact-source/exact-hash pinned canary
  installer verified on each architecture. A mutated ZIP must be rejected.
- Never force-merge or auto-create tags outside the gated workflow, or bypass
  the main branch's published-release tag ancestry rule. Merge the immutable
  tagged source into `main` afterwards using a real merge commit.

## Scope and restrictions

Three unsigned portable ZIPs may be downloaded manually. The optional Canary
installer is administrator-initiated and needs `-ApproveUnsigned -Ring Canary`;
broad deployment still requires separate `-AuthorizeBroadRollout`. No GPO
or Intune assignment, scheduled update consent, RBAC authorization, recovery
secret handling, machine audit settings or signed-only trust requirements are
modified by publishing the release.

**UNSIGNED** means the binaries have no institutional Authenticode/Azure
Trusted Signing signature. SHA256 and GitHub provenance do **not** replace
CyberArk EPM, AppLocker or WDAC trust approval. Separate hands-on validation
of Hebrew/Russian DPI/RDP at 100-200%, delegated production AD/Entra/Intune,
auditing, service lifecycle and canary rollback is still required before
organization-wide use. No claim of measured performance improvement is made.
