# BitKeyBridge v0.30.5 publication authorization

Approved by the repository owner for release as of 2026-10-18.

## Scope
- Stable updater ZIP compatibility hotfix for installed v0.30.3/v0.30.4 clients.
- Publish x64, x86 and ARM64 architecture ZIPs, each with exactly one BitKeyBridge.exe in the root.
- Preserve SHA-256 checksums, GitHub provenance/SBOM, protected Audit Broker and two-standard-user regression, legacy migration regression and staged updater smoke.
- Current binaries are **unsigned**; SHA-256 protects integrity against accidental/certain unauthorized alteration only when the checksum source is trusted. No Authenticode publisher identity is claimed.

## Deployment scope
- Public release does not create GPO links, configure auto-install on devices or change machine trust policy.
- Existing, previously enabled automatic-update clients may adopt this official release according to their own local explicit consent, GPO restrictions, maintenance windows, and rollback hold.
- Do not enable unsigned unattended updates broadly by default. Test one canary group, verify post-install outcomes, and expand only after administrator review.

## Release trigger
The exact commit subject `Release v0.30.5` is the audited GitHub Actions release gate. All run-level checks must pass before publication; never upload manually repackaged ZIPs.
