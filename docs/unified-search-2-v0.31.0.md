# BitKeyBridge v0.31.0 - Unified Search 2.0 source-aware retry

The existing unified metadata coordinator independently queries Active
Directory, Intune managed devices and Entra BitLocker metadata. The results
are merged only by authoritative AD/Entra/Intune IDs, not hostname alone.

This stage adds **one bounded retry** to the two Microsoft Graph metadata
source lookups for typed HTTP 429, 500, 502, 503 and 504 errors. Each retry
is delayed by 350 ms and observes the caller cancellation token.
Permission-denied 401/403, wrong IDs, corrupt response data, authentication
or invalid machine policy do **not** retry. One source's failure remains
explicit in the existing source status and does not overwrite other results.

The retry is intentionally not applied to BitLocker secret retrieval,
LAPS password decrypt, rotation, ticket/audit authorization or the many
individual per-device enrichment requests. A transient error is not proof
that a backup is missing; source records remain marked unverified.

The Graph GET helper now propagates the exact HTTP status code as a typed
HttpRequestException. Diagnostic strings are redacted before entering
unified-search status or logs. The offline self-test covers 503 recovery,
403 without retry, 429 bounded budget and canceled operations.

Future Unified Search work includes operator-controlled per-source manual
retry, metadata search pagination and keeping ticket/recovery context across
all workspaces. This development PR is not part of the v0.30.4 release.
