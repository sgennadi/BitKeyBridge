# Production diagnostics: managed HTTPS relay

## Goal

Installed BitKeyBridge clients send anonymous, **typed error counters** to
GitHub Issues without needing a GitHub account, GitHub CLI, MFA or a PAT on
the user's computer. IT deploys a small HTTPS relay once and provisions
organizational policy. The relay validates exact BKB-DIAG/v1 JSON and
creates GitHub Issues using a restricted **server-side** token.

**No raw logs, log messages, full stack traces, screenshots, support archives,
usernames, hostnames, tenant identifiers, recovery keys, LAPS passwords,
Graph tokens or device identifiers may be uploaded.** Local support ZIPs
are separate and must remain internal. GitHub Issues in this repository
are public, so upload data must remain strictly non-identifying.

No automatic network submission happens with default settings. In a
managed deployment, IT may enable automatic submissions by machine policy
after providing the organizational privacy notice.

## Deploy server: Cloudflare Worker

1. Create a **fine-grained** GitHub PAT for the **relay server only**,
   restricted to repository sgennadi/BitKeyBridge, with only Issues
   **Read and write** (plus GitHub-mandated basic metadata) permission.
   Do not use a classic PAT or grant broad repo access.
2. Work in tools/github-diagnostic-relay/ using Wrangler:
   - Run: npx wrangler login (admin deployment machine only).
   - Run: npx wrangler secret put GITHUB_TOKEN
   - Run: npx wrangler secret put INGEST_TOKEN
   - Run: npx wrangler deploy
3. Generate INGEST_TOKEN using a cryptographically secure source.
   Use a random ASCII key at least 32 characters; 48+ preferred.
   It is **not** a GitHub token. Rotate if the key leaks.
4. Copy the Worker HTTPS URL and append /v1/diagnostics.
   Use custom TLS domain if deploying behind a managed gateway.
5. Before enabling many users, configure Cloudflare WAF/rate limiting;
   restrict approved source IPs when practical; monitor volume and
   GitHub Issue creation; rotate credentials. For stronger client identity,
   front the relay with an identity-aware gateway or mTLS.
   A shared ingest key is **not** proof of a particular workstation:
   it may be recovered by a local user. Never grant it GitHub rights.
6. Keep both secrets out of the GitHub repository and client binary.

Worker accepts only POST /v1/diagnostics, valid JSON under 10 KB
with exact enumerated field names and consistent fingerprints, and a
Bearer ingest key. HTTP 201 is returned only after the GitHub Issue
is created and its URL validated. Clients need not reach GitHub directly.

## Configure all installed clients centrally

In the admin-owned machine configuration file
%PROGRAMDATA%\BitKeyBridge\appsettings.json keep all existing settings
and add the following properties:

- DiagnosticRelayEnabled = true
- DiagnosticRelayManagedAutoSend = true for IT-managed automatic sending,
  or false if each user must explicitly opt in from Helpdesk.
- DiagnosticRelayUrl = the actual HTTPS Worker URL ending precisely
  in /v1/diagnostics (no query or fragment).
- DiagnosticRelayIngestToken = the same 32-256-character upload-only
  key as the Worker's INGEST_TOKEN.

Deploy the configuration with your existing approved GPO/Intune
mechanism. **Never distribute a GitHub PAT**. Do not commit actual keys
or attach machine configuration to public Issues. The client redaction
and support-bundle generator exclude the upload key, but a machine
configuration backup can be sensitive; store backups appropriately.

Illustrative JSON fields only (insert into an existing JSON object,
do not overwrite unrelated BitKeyBridge settings):

    "DiagnosticRelayEnabled": true,
    "DiagnosticRelayManagedAutoSend": true,
    "DiagnosticRelayUrl": "https://YOUR-RELAY.workers.dev/v1/diagnostics",
    "DiagnosticRelayIngestToken": "REPLACE_WITH_RANDOM_48_CHARACTER_INGEST_KEY_ONLY"

## Real user behavior

- Standard users need no elevation, GitHub CLI, GitHub account or MFA
  for report submission.
- Reports are local until user opt-in, or explicit IT-managed auto policy.
  The Helpdesk panel shows managed reporting and a privacy notice.
- Typed exceptions are fingerprinted and aggregated per user.
  Auto-send attempts at three occurrences or more, with six-hour cooldown
  after successful submissions; attempts run off the GUI thread.
- HTTP errors, offline clients and unavailable GitHub retain the local
  counters for retry. The client clears only a confirmed batch.
- No user or device identity is included in GitHub Issues; only exact
  enumerated metadata, count, date, app version and architecture.
- Without a managed relay, previous manual development submission via
  signed-in GitHub CLI remains available. Do not use this for fleet rollout.

## Test, then go live

1. From the repo root: node --test tools/github-diagnostic-relay/index.test.mjs.
   This uses a fake GitHub API and cannot publish a real Issue.
2. Configure an isolated Worker with staging credentials.
   On an ordinary Windows account run BitKeyBridge.exe --self-test --no-elevation
   and GUI. Inspect the Helpdesk report preview and status.
3. Submit synthetic non-secret error buckets. Verify correct public
   Issue schema, rejection of unknown fields, invalid token/fingerprint,
   size limits, GitHub unavailability, and retention of failed batches.
4. Review then merge the candidate branch to the default branch.
   GitHub scheduled and Issues event triage starts from default branch;
   the prebuild analysis gate runs in candidate PRs already.
5. Monitor the GitHub Actions workflow "Anonymous diagnostic triage".
   It performs read-only aggregation and never changes application code.
6. Deploy the approved signed release to a small canary group first.
   Check policy, WAF, endpoint availability, localization, RDP/DPI,
   AD/Graph searches and authorized BitLocker/LAPS workflows before
   any wider rollout.

## Limitations

- Only error **categories/counters** are centralized. Full text logs
  intentionally remain local. Complete support ZIPs require a separately
  approved authenticated **private** channel, never public GitHub Issues.
- Shared upload key is not strong user/device identity. Apply network
  controls, WAF/rate limits and rotate. For higher-assurance deployments
  use identity-aware access or mutual TLS.
- If GitHub creates an Issue but its response is lost, retry may create
  a duplicate. The aggregator groups identical fingerprints. Durable
  server idempotency would be needed for exactly-once submission.
- GitHub reception is inactive until the Worker is actually deployed
  with its secrets, authorized GitHub permissions and working network.
