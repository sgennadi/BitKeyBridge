# BitKeyBridge 0.35.0-rc.7 — production diagnostics relay acceptance

**Candidate, not Stable. Unsigned manual QA only.** This change stays in
the existing single PR #115. Never distribute an unsigned RC through GPO,
Intune or an untrusted auto-update channel to production endpoints.

## New capability

Real Windows users can submit **anonymous, typed error counters**
through an IT-provisioned HTTPS relay without GitHub CLI, a GitHub login
or a personal access token. Server-side GitHub Issues creation and
the existing read-only automated triage are separate from the app.
No arbitrary log text, support ZIP, identity, computer name, recovery key,
LAPS password or cloud token is transmitted.

## Mandatory source and security gates

- Latest exact HEAD Windows x64, x86, ARM64 self-contained builds succeed.
- Build pipeline prebuild runs Node Worker tests, strict Python issue
  schema/aggregation self-tests and read-only existing issue triage.
- CodeQL and stable provenance checks complete on the **same HEAD**.
- Windows native UI, EN/RU/HE, right-to-left Hebrew, DPI 100%–200%,
  no clipped Helpdesk privacy notice or inaccessible Send/Preview actions.
- Windows LAPS regressions and protected audit, authorized recovery,
  legacy updater migration, signing verification and rollback remain green.
- Unsupported/missing relay configuration never blocks recovery,
  network connections, GUI painting, search, or Microsoft Graph sign-in.

## Manual isolated Windows and relay tests

1. Deploy a separate HTTPS Worker with a dedicated fine-grained GitHub
   Issues write token and a unique 48-character ingest key. Configure
   edge rate limits, monitoring and corporate TLS. Test without secrets
   or identifiable data.
2. Apply DiagnosticRelayEnabled and DiagnosticRelayManagedAutoSend
   settings from docs/production-diagnostics-relay.md to the managed
   machine configuration, preserving all existing settings.
3. Launch BitKeyBridge as a **standard Windows user**. Confirm no
   installed gh, no GitHub account, no UAC prompt and no new MFA.
   Check Helpdesk privacy text and managed checkbox in EN/RU/HE.
4. Trigger only synthetic, non-secret error cases. After three
   occurrences, verify a BKB-DIAG/v1 GitHub Issue was created and
   the local queue was decremented only on successful acknowledgement.
   A manual Send action should also work without gh.
5. Reject a wrong/missing ingest key, malformed/oversize JSON,
   extra property, wrong fingerprint, HTTP redirect, missing GitHub
   permission and 429/503: counters must remain queued and no raw
   exception contents may be exposed.
6. Confirm saved diagnostics ZIP contains no ingress key and that
   full logs and identity-bearing data never appear in public Issues.
7. Disable reporting by machine policy and/or user opt-in:
   no new outbound diagnostics requests should be made.
8. Verify scheduled GitHub Actions analysis is activated only after
   merging the workflow to main. The triage must remain read-only.
9. Run signed limited-canary pilot after corporate authorization,
   checking GitHub rate limits, tenant consent/privacy obligations,
   Windows 11/RDP/UI and ordinary AD/Entra/Intune flows. No widespread
   rollout until approved.

## Remaining deployment dependency

No Cloudflare Worker URL, production ingest key, GitHub PAT or live
server-side authorization is embedded in this branch. The code is
deployable, but user submissions **will not reach GitHub** until
an administrator deploys the relay and configures the machines.
Do not report the relay as operating in production from CI alone.
