# BitKeyBridge branch and release policy

Updated: 2026-10-24. This document describes the desired repository model,
not an assertion that GitHub administrative rulesets have been installed.

## Source of truth

| Area | Branch/ref | Rule |
| --- | --- | --- |
| Stable source | `main` | Contains the published v0.32.0 release as an immutable ancestor and approved post-release UI fixes (PR #106). Promote v0.32.1 release source only after its separate immutable release and full tests. |
| Stable release | immutable `vX.Y.Z` tag | Exact release SHA, reproducible artifacts, release notes, all gates green, publishing authorization documented. |
| Stable release preparation | `release/v0.32.1` | Exact-commit unsigned Stable hotfix with protected audit, CodeQL, migration/rollback and canary package validation. Publication subject exactly `Release v0.32.1`. |
| Preview integration | `development/post-rc9-lookup-ux` | RC9 features and independently tested post-RC9 UX fixes; not a managed production rollout authorization. |
| Public prerelease | immutable `vX.Y.Z-rc.N` tag | Dedicated `release/vX.Y.Z-rc.N` branch and pinned-head CI; must not become GitHub Latest. |
| Development | `feature/*`, `fix/*`, `sync/*` | Pull request, CI, review; no automatic deployment or release. |

## Safe promotion sequence

1. Build and test a proposed preview from integration with no secrets.
2. Branch a **new** release candidate from the reviewed source. Version,
   changelog, executable ProductVersion, source SHA and expected asset names
   must all agree. Never reuse or overwrite a published tag.
3. Gate all Windows x64/x86/ARM64 builds, offline self-tests, HiDPI UI
   regression, CodeQL, LAPS tests, true standard-user Protected Audit Broker
   integration and pinned legacy upgrade/rollback.
4. Publish prerelease artifacts only from its exact branch, exact approval
   message and exact immutable commit, after every gate passes.
5. A public **unsigned** Stable release requires an explicit owner authorization,
   successful exact-source CI, published checksum/provenance evidence, and
   prominently documented limitations. This does **not** mean the software has
   an organizational Authenticode signature or is approved for fleet rollout.
   **Before managed production deployment**, independently validate Windows 11
   (including Hebrew/HiDPI/RDP), domain AD, delegated Entra/Intune access,
   CyberArk EPM/WDAC/AppLocker, protected journaling/SIEM, and secure update
   rollback with a representative canary group. Complete enterprise signing
   and acceptance separately.
6. Create the new stable GitHub release after all publishing checks pass,
   then review-merge the tagged release source into `main` using a true merge
   commit that preserves the exact release tag as an ancestor. Never squash
   away the stable tag's ancestry or merge a preview directly to main.

## GitHub administrator settings (manual approval required)

Use Repository Settings > Rules > Rulesets to protect `main`,
`release/*` and published tags. Recommended requirements:

- Block force pushes and deletions; limit bypass roles to approved maintainers.
- Require PR review and all relevant named CI + CodeQL checks before merge.
- Require linear history where compatible with the chosen merge strategy.
- Restrict release-tag creation and prevent deletion/mutation after publish.
- Protect `.github/workflows/**` with CODEOWNERS plus review so a PR cannot
  loosen the publication gate.
- Keep the release workflow's default token read-only; only the gated publish
  job receives `contents: write`. Never expose signing credentials to PRs.
- If signing is introduced, use approved organization PKI / HSM / key vault
  and record verified certificate rollover procedures.

A GitHub App without repository administration rights **cannot turn on these
rulesets**. Do not claim protection is active until a repository owner has
configured and validated the rules.

## Auto-update and version channels

The stable client checks `/releases/latest`; RC builds require an explicit
per-device prerelease opt-in. Signed and unsigned update trust is separately
enforced, with rollback holds and managed policy restrictions. Publication
never enrolls tasks or silently opts workstations into unsigned updates.

Use `BitKeyBridge.exe --update-center-status --no-elevation` (in builds that
include Update Center 2.0) to inspect local state without performing an update.
