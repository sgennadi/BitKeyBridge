# Advanced Device Diagnostics — v0.26.0

Open **Start > Device Diagnostics...** from a workstation or DC. The runtime
does not require RSAT or PowerShell modules. A reachable network port does
not prove permission to read/decrypt confidential directory attributes.

## Features

1. **Compare DCs** — for one exact AD computer, compare visible Recovery IDs
   (metadata only), Windows/legacy LAPS expiration/version and object visibility
   on up to 24 DCs. Flag RODCs, missing objects and incomplete DC queries.
   A metadata mismatch is not a conclusive replication-failure diagnosis.
2. **Smart Diagnostic Engine** — classify confirmed metadata variance, possible
   backup problems and unverified read/decrypt permissions with next-action and
   execution-location guidance. Never invent AccessDenied from a missing value.
3. **Permission analysis** — inspect the computer-object DACL by LDAP
   SecurityDescriptorFlagControl, including observed ACE SID, allow/deny masks,
   object-specific GUIDs and inheritance. DACL listing is NOT effective access,
   cannot prove confidential-attribute reading or DPAPI-NG decryption, and
   makes no changes. Distinguish explicit AD credential identity from process
   Windows identity.
4. **Offline endpoint ZIP** — run on the affected PC from the GUI or CLI:
   BitKeyBridge.exe --collect-endpoint --output C:\Temp\endpoint.zip
   The bundle captures OS info, whitelisted non-secret GPO registry values
   and Windows BitLocker/LAPS Event IDs, level and timestamps ONLY.
   Event message texts, TPM/BitLocker protectors, credentials, secret values,
   tokens and private keys are NEVER collected. ZIP import limits size and
   allowed entry names and permits offline review by helpdesk.
5. **Advanced Connection Analyzer** — native Windows AD DNS SRV, DC site and
   LDAP bind latency, plus strict LDAPS certificate subject/SAN, expiry and
   certificate-chain validation. Select suggested writable DC only after an
   explicit operator confirmation; this is for the next AD connection, not
   an invisible automatic failover or a downgrade.
6. **AD / Entra recovery-source consistency** — compare BitLocker Recovery ID
   metadata from Active Directory and Microsoft Graph. Resolve one exact
   Intune device name to a unique Entra deviceId (not object ID), or supply
   the GUID explicitly. Cloud failure is marked partial, not no backup.
   Different Recovery IDs may be legitimate escrow history.
7. **DPAPI CurrentUser protected recovery cache** — protect an existing admin
   recovery-export CSV to an encrypted .bkb file under the current Windows
   user's LocalAppData. Recovery > Protected cache reads metadata and an
   authorized selected key directly in memory, without a temporary plaintext
   file. Original export CSV is NOT removed, overwritten, or silently retired;
   previous encrypted sidecars are retained for rollback when replaced.
8. **Authenticode signing readiness** — a conditional CI step signs each EXE
   only when a real organization-provided Code Signing certificate/private
   key is available to the runner. Unsigned builds remain supported until
   configured; enforcement is opt-in. See SIGNING.md.

## Safety and rollout

The new tools are portable C#/.NET 10 and use shared UiStyle/high DPI WinForms.
All AD/Graph diagnostics are read-only and metadata-only. No ACL/GPO edits,
automatic password reads, password changes, KDS RPC scans, mandatory RBAC/JIT,
tickets, two-person approval or SIEM controls are enabled by this release.

Troubleshooting workflow: run Start > Environment Check, then Device Diagnostics
> Compare DCs for the exact computer. If results differ, inspect replication,
identity and object visibility before ACL changes. Run Permission analysis for
configured ACL evidence; live authorized read/decrypt remains a separate
explicit action. On an affected offline computer, collect an endpoint ZIP and
open it on an administrator workstation. Workgroup clients can use explicit
DC and AD credentials under Start > Advanced; encrypted cache is tied to the
Windows user/machine which protected it.
