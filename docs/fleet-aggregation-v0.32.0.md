# v0.32.0 - offline centralized fleet inventory aggregator

This is a **local** collection/aggregation foundation for an approved
GPO/Intune inventory workflow, not a central management agent.

On each endpoint, an approved management job can capture **only**:

    BitKeyBridge.exe --fleet-status --no-elevation

Collect each output as a separate JSON file in an administrator-controlled
directory using your existing Intune/GPO collection mechanism. On the central
technician/administrator workstation run:

    BitKeyBridge.exe --fleet-aggregate "C:\\Inventory\\Reports" "C:\\Inventory\\fleet.csv"

The native CLI processes at most 5,000 real JSON files (64 KiB each),
without directory traversal or following reparse points. It requires the exact
versioned `NonSecretMetadataOnly` schema, rejects unknown/duplicate
properties (including unexpected key/password/token values), rejects
unexpected status fields and untrusted machine identifiers, and keeps the
newest UTC sample per machine.

The output is a UTF-8 CSV with one row per machine: installed version,
platform, last collection timestamp/age, Windows Service state,
`brokerServiceHealth`, last update outcome/time, automatic-update
eligibility and rollback hold. A sample older than seven days is marked
Stale. Unrecognized or unsafe source records are rejected and reported by
counts, without echoing file contents.

The aggregator has **no network connection**, no AD/Graph access, no remote
process launching, no local privilege escalation, and no recovery secret or
token retrieval. It runs before loading machine policy and requires only
read rights on the input files and write access to the selected CSV path.

SCM `Running` does not guarantee a working protected Audit Broker or
committed audit ACK. Use the protected broker checks separately.

### Security limits

This is offline ingestion, not authenticated central telemetry. Validate
machine/report provenance through your approved deployment tooling, use
access-controlled shares, retain and clean files according to institutional
policy, and do not treat a reported healthy service as permission to read a
key. CSV fields are quoted and protected from formula evaluation in Excel.
