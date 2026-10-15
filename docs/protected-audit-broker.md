# BitKeyBridge protected audit broker — 0.30.0-rc.2 TEST BUILD

This preview is deliberately **disabled by default**. It exists for an
enterprise requirement: ordinary Helpdesk users must be able to read only
their delegated LAPS/BitLocker secrets without local Administrator/UAC,
while a SYSTEM-owned local Windows Service records a protected pre-read
audit event. It does not itself grant AD, Entra, Graph or LAPS access.

## Preconditions (test computer first)

- Windows 10/11 or Windows Server with a compatible .NET 10 BitKeyBridge
  **0.30.0-rc.2** service binary installed; service identity must be
  **LocalSystem**. The broker currently rejects gMSA/domain identities.
- A Windows administrator controls the machine configuration under
  `%ProgramData%\BitKeyBridge` and can configure RBAC.
- Enable BitKeyBridge RBAC and explicitly add a **narrow helpdesk group** or
  individual test SID under `RbacRecoveryReaders`. Local Windows
  Administrators do *not* automatically bypass the broker allowlist.
- Do not configure Everyone, Authenticated Users, BUILTIN Users, Guests or
  Network/Interactive logon SIDs as recovery readers.
- Validate AD's BitLocker/LAPS ACLs or Entra's delegated permissions
  separately: broker authorization is only permission to record an audit.
- Back up the existing `appsettings.json` and NTFS ACLs before provisioning.

## Automated standard-user integration (ephemeral GitHub runner)

PR #57 has a Windows-hosted integration job in `.github/workflows/build.yml`
that runs only for `feature/protected-audit-broker-20261014` after the tested
x64/x86/ARM64 builds complete. On a **fresh GitHub-hosted Windows runner**
it uses `tests/windows/ProtectedAuditBrokerIntegration.ps1` to:

- Create two temporary local **non-admin** Windows accounts.
- Install the test-only SYSTEM service, register just one user's SID as an
  RBAC RecoveryReader, harden NTFS and restart the broker.
- Prove the authorized account gets a `BrokerProbe=Committed` event while an
  unlisted account receives exit code 3, without a local UAC prompt.
- Verify the protected journal and hash chain as an administrator and
  confirm health events contain the real caller SID, not the denied user's SID.
- Corrupt the hash chain and prove no new entry is appended while access is denied.
- Temporarily grant BUILTIN Users write access to the machine policy root,
  protected directory, appsettings file, journal, journal lock and rotated
  archive one at a time. Both broker and admin verification must reject
  each unsafe ACL. Restore original owner/DACL in a finally block and confirm
  the authenticated recovery-reader probe succeeds after restoration.
- Change only the whitespace in administrator-protected appsettings.json:
  even with the same trusted ACL, the running service must reject an existing
  reader until the original file bytes are restored. Any legitimate RBAC or
  other machine policy change requires a service restart to load the new
  explicit recovery-reader allowlist.
- Stop the service and require the previously authorized user to receive
  exit code 3 (strict fail-closed, never portable fallback).
- Remove the test service and accounts in `finally`.

The script **refuses** to run when a service or machine configuration
already exists, on a self-hosted/non-GitHub runner, or without its explicit
`-EphemeralHostedRunner` switch. It changes Windows Service/NTFS settings
on that disposable CI virtual machine: do not run it on a DC, managed
workstation, production server or persistent CI host.

The standard-user integration also runs the native
`--audit-broker-preflight` JSON command as allowed and denied accounts,
asserts that passive preflight leaves the journal unchanged, and verifies
`--probe` alone records an explicit synthetic event.

[Manual managed-domain and Entra acceptance runbook](managed-domain-acceptance-0.30.0-rc.2.md)
includes the CMD-only report wrapper
[`tests/windows/BitKeyBridge-DomainPreflight.cmd`](../tests/windows/BitKeyBridge-DomainPreflight.cmd).

This checks local Windows account impersonation and the Named Pipe; it does
not substitute for the separate domain-connected AD/LAPS/Entra acceptance
tests below.

## First-time setup on the managed test machine

1. In an elevated CMD/PowerShell window, install/update the service as
   usual with `BitKeyBridge.exe --install-service`. Confirm its identity:
   `BitKeyBridge.exe --service-status`.
2. Configure `--rbac-enable` and `--rbac-reader-add "DOMAIN\YourHelpdeskGroup"`
   as an administrator, then verify with `--rbac-status`.
3. Run `BitKeyBridge.exe --audit-broker-provision --no-elevation` from the
   same *already elevated* console. This operation intentionally adjusts
   NTFS security:
   - `%ProgramData%\BitKeyBridge`: SYSTEM/Admin FullControl;
     authenticated users may traverse the directory but do not receive write
     or directory-listing permissions.
   - `appsettings.json`: SYSTEM/Admin FullControl and authenticated
     users **read-only** so an ordinary GUI can load the connection policy.
   - `ProtectedAudit\audit.jsonl`: separate journal, owned and writable
     only by SYSTEM/Administrators. No ordinary user read/write privileges.
   - Existing legacy `audit.jsonl` remains in place and is not overwritten.
   - Original machine-folder and appsettings NTFS owner/DACL SDDL are saved
     automatically as an admin-only `ProtectedAudit\\pre-provision-acl-*.json`
     snapshot before any existing permissions are changed. Preserve this
     file if you need to restore the prior ACLs after testing.
4. Restart the service from an elevated console: `BitKeyBridge.exe --stop-service`,
   then `BitKeyBridge.exe --start-service`. Inspect Windows Event Log for
   `Protected local-only recovery audit broker is active.`
5. From a **standard** Windows account in the explicit RBAC
   `RecoveryReaders` group, run
   `BitKeyBridge.exe --audit-broker-status --no-elevation`.
   **Exit 0 now means an authenticated Named Pipe health event was committed
   and acknowledged by the SYSTEM-owned journal**, not merely that the
   Windows Service is running. The output contains
   `BrokerProbe=Committed`. Exit 3 / `DeniedOrUnavailable` means the
   request was denied or could not obtain a durable audit ACK.
   Each successful health probe writes a synthetic
   `AuditBrokerHealthProbe` event. An administrator not included in the
   explicit RecoveryReaders allowlist will also receive exit 3; use
   `--service-status` and `--audit-broker-verify` for admin-only checks.
   The same authenticated nonblocking test is available in the GUI through
   **Diagnostics → Tools → Check Protected Audit...**. It never reads secrets
   and intentionally records only a synthetic health event.
6. Launch the GUI as a standard Helpdesk account. Connect to AD or Entra
   as that account, perform an authorized LAPS/BitLocker read, and confirm
   the **SYSTEM-owned** broker journal received a matching, hash-chained
   pre-read event attributed to the actual Windows caller.
7. Remove the user's AD/Graph permission or RBAC group membership and
   confirm access is denied. Stop the service and verify that *strict audit*
   causes a fail-closed refusal, **not** user-profile journal fallback.
8. Run `BitKeyBridge.exe --audit-broker-verify --no-elevation`
   in an **already elevated** administrator shell. It checks owner/write NTFS
   ACLs and the protected hash chain, returns nonzero on failure and prints
   safe verification metadata. `--audit-verify` still checks only the
   independent legacy journal. Do not grant users write access to make
   the tests pass.

## Authentication & IPC boundaries

The endpoint is a LOCAL Windows named pipe; its ACL grants individual
ReadData/WriteData rights to explicit RecoveryReaders, without the
CreateNewInstance right. The service denies NETWORK logons and impersonates
each connecting client to inspect its Windows SID and enabled groups.
The client's pipe-owner check requires LocalSystem ownership before any
metadata is transmitted. No caller-provided user SID or "authorized" flag is
accepted. All request fields have fixed length limits and the only operations
allowed are pre-read auditing for LAPS and BitLocker. Replay/request-ID
filtering and per-SID rate limiting are enforced in memory.

The service records the real Windows identity, requested target, session,
ticket/reference, source and operation. The status command uses a reserved
synthetic target and source, rejects arbitrary reason, ticket and recovery
fields, and cannot count as an authorization for real secret retrieval. It does **not** receive passwords,
BitLocker recovery keys, AD credentials or Graph tokens. A commit
acknowledgement is emitted only after the protected file is append-written,
its hash chain checked and the data flushed to disk. Missing service, failed
identity/ACL validation, damaged hash chain, malformed requests or an absent
ACK block recovery. The journal trust check covers every directory down to
the machine configuration root, rejects NTFS junctions/symlinks, and checks
the machine policy plus journal, rotated archive and lock file ACLs on
every broker request. It pins the startup policy file fingerprint and
fails closed after an administrator edits that file until the service
restarts; this also prevents stale recovery-reader membership after a
policy change. Disabled/dismissed Windows token groups are never counted
as enabled recovery-reader roles. Administrator --audit-broker-verify checks the machine
policy ACL too, not just the active journal. No UAC popup is triggered in the ordinary GUI.

**Limitation:** This is a test preview; the service does not independently
re-evaluate Entra, ticket approvals, AD ACLs or JIT grants on every request.
The GUI still evaluates those and the remote backend enforces its own
permissions. Authorized group members can submit additional *audit-only*
events; they cannot retrieve secrets through this pipe. Do not interpret
the server's event as proof the external secret read actually succeeded.
SIEM delivery is asynchronous and is not guaranteed by the local ACK.

## Rollback / troubleshooting

- If provisioning fails, do not loosen NTFS ACLs. Correct group membership,
  LocalSystem service identity, journal owner or pre-existing audit corruption.
  Existing rotated or lock files with unsafe ACLs are rejected instead of
  being silently re-trusted.
- Protected archive retention fails closed: when the active protected journal
  reaches its size threshold and audit.jsonl.old already exists, automatic
  rotation refuses to delete/overwrite the old archive. Recovery then remains
  blocked until an administrator implements an approved externally anchored
  archive/checkpoint process. This is intentional, not a storage-cleanup bug.
- If the protected journal is damaged, restore a trusted backup. Do not
  overwrite, truncate or silently reset the hash chain to unblock an operator.
- Disable `AuditBrokerEnabled` only through an authorized admin change to
  the machine policy; keep `RequireProtectedAuditForRecovery` enabled unless
  security administrators explicitly approve reverting strict mode. Disabling
  the broker without a protected writer causes normal users to fail closed.
- The broker and its ACLs are local to each workstation where the service
  is installed. No inbound TCP listener or firewall opening is necessary.
- Test 100–200% HiDPI/RDP, AD/Entra delegated identity, service restarts,
  offline behavior and signed update/rollback before enterprise deployment.

This test build does not alter the stable `v0.29.12` or published
`v0.30.0-rc.1` GitHub Releases.
