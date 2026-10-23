# BitKeyBridge v0.32.0-rc.8 — consolidated release candidate

**STATUS: SOURCE CANDIDATE / NOT PUBLISHED / NOT APPROVED FOR PRODUCTION.**

This is one auditable integration candidate for RC7 layout fixes, RC8 performance/UX and safe staging cleanup, plus the Microsoft Graph ROPC removal. The existing unsigned stable v0.30.5 and published RC6 assets remain immutable. A successful CI run is **not** university code-signing, a production approval, or a Windows/RDP/AD/Entra pilot.

## Source ownership and scope

- Single integration PR from `consolidation/v0.32.0-rc8-single-20261021` into `integration/v0.32.0-staging-20261018`. It replaces the stacked RC7/RC8 development-PR approach and imports the security fixes previously reviewed in PR #102. Do not merge both paths separately after consolidation.
- Version `0.32.0-rc.8` with PE FileVersion `0.32.0.0`; .NET 10 and native WinForms for x64, x86 and ARM64.
- Device selection remains metadata-only until the existing source-specific BitLocker/LAPS authorization and protected-audit ACK succeed.
- Graph password grant is gone. Authentication is Device Code (MFA/Conditional Access) or approved app registration certificate. Old `Password` settings map to Device Code and require interactive sign-in. Never persist or reintroduce legacy usernames/passwords.
- Remote API remains an optional, disabled-by-default advanced feature. It is **not** enabled by this candidate.
- Start/Helpdesk use one source-qualified search and preserve exact selection while navigating. Connection settings and Helpdesk recovery policy are compact, with explicit expansion for advanced controls.
- Diagnostics/Tools and nested tabs reflow at 100%, 150%, 175%, 200% simulated DPI with reduced repaint/layout work and readable metrics. The runtime must be separately tested on physical Windows/RDP.
- Housekeeping may prune stale `ProgramData/BitKeyBridge/Updates` staging directories after the configured 7-day retention. Protect current/held versions, active update, rollback, .bak, journal, configuration, unrecognized names, and all reparse points.

## Automated release gates

1. Full Windows .NET 10 `win-x64`, `win-x86`, `win-arm64` build, publish, executable machine validation, immutable root-only ZIP packaging and SHA-256 checks.
2. All offline `--self-test --no-elevation`, HiDPI `--ui-self-test --no-elevation`, source repaint checks, ROPC migration tests, LAPS lifecycle regression, CodeQL.
3. Protected Audit integration under two unprivileged Windows identities; reject an absent client SID and all unauthorized requests.
4. Pinning tests: WinVerifyTrust trusted signer, leaf certificate via `X509CertificateLoader`, publisher and exact thumbprint checks, reject tampered/unsigned/wrong-signer packages per policy.
5. Upgrade and rollback on disposable hosted Windows machines from known v0.30.1 and v0.30.3 releases, plus safe updater staging pruning.
6. Strict source/CHANGELOG/version checks. Do not reuse RC7 release notes, branch or tag.

## Manual acceptance — required before publication

On an isolated Windows 11 workstation (and a separate RDP client), verify layout at 100%, 150%, 175% and 200%, including 1024x768, maximized/compact widths, monitor switches, locale changes and RDP reconnect. Record UI p50/p95 tab-switch latency and 30 cycles of CPU, GDI handles and memory without harvesting device names/secrets.

Exercise AD and Entra searches, ambiguous names, offline DC, cancel/retry and denied recovery reads. Test genuine Helpdesk and administrator accounts; require explicit reveal, a permitted RBAC/JIT/ticket path, and auditable no-secret-on-error behavior. Check unsigned-consent policy and Authenticode in CyberArk EPM. Verify legacy ROPC profile migration needs Device Code and never prompts for a cloud password.

Test a signed, SHA256-matched update in a lab, rollback after induced failure, and ProgramData cleanup with dry-run and retained audit/recovery data. Do not test destructive cleanup against production folders.

## Publication is a separate approval

Release workflow is prepared but **cannot** publish from PRs or integration branches. Only an explicitly approved push on `release/v0.32.0-rc.8` with the complete head commit message `Release candidate v0.32.0-rc.8`, after every prerequisite succeeds, may publish new `v0.32.0-rc.8` immutable unsigned **prerelease** artifacts. It refuses to overwrite tags/releases. Stable v0.30.5 must remain Latest; never enable automatic installation, GPO enrollment or CyberArk bypass during publication.

**Stop conditions:** any failure of Protected Audit or RBAC, unsafe rollback/ProgramData cleanup, unacceptable UI clipping/freeze, unexpected secrets in logs, disabled signature enforcement when required, or incomplete signing/InfoSec acceptance. Such a candidate remains unpublished.
