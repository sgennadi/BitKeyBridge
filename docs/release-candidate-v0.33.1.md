# BitKeyBridge v0.33.1 — development acceptance

**Status:** exact-source v0.33.1 release acceptance checklist. Public GitHub publication is separately gated; managed Entra/Intune and Windows/RDP deployment acceptance remains outstanding regardless of public release status. v0.33.0 is the rollback baseline.

## Changes

- Merge PR #108: missing Cloud authentication in Devices leads to the already-authorized Settings → Cloud page. No extra credentials or Graph operations.
- For privileged Entra app setup/tenant-wide admin consent, require a specific tenant GUID or verified DNS domain. Reject empty input, all-zero GUID, generic `organizations`/`common`/`consumers` authorities, malformed hostnames, paths and URI schemes **before** Graph.
- Show explicit inline EN/RU/HE warnings on the Intune Wizard tenant step and keep Next on the same step when invalid. Revalidate before Configure & Verify. First-Run / Repair has the same preflight.
- Ordinary delegated Device Code sign-in continues to support `organizations`; all-zero public-client GUIDs are rejected before saving.
- No audit/authorization, recovery secret, trusted-update policy or machine deployment defaults change.

## Required validation

1. Windows x64, x86, ARM64 builds; offline LAPS and native WinForms self-tests.
2. Test the exact tenant validator with valid domain/GUID and blank, alias, zero GUID, malformed URL/DNS examples. No Graph network in tests.
3. EN/RU/HE and HiDPI 100–200% wizard Next/Back/Cancel and status-label visibility. Closing Device Code must stop token polling and progress.
4. CodeQL and security checks. Before stable release: Protected Audit Broker, legacy updater migration/rollback and a separately approved exact-source protected release workflow.
5. Manual approved Entra test tenant: delegated sign-in and app registration/certificate setup, proper tenant selection, failure and cancellation behavior, and no secret-bearing diagnostics.
6. No unsigned deployment by GPO/Intune without existing separate approved managed rollout and security policy checks.

Publishing a public unsigned release is separate from managed workstation acceptance and enterprise Authenticode signing.
