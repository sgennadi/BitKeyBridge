# BitKeyBridge v0.31.0 - Access Health 2.0 (phase 1)

The existing Access Health GUI already checks AD connection, BitLocker
recovery-object metadata, Windows LAPS metadata/decrypt capability, Microsoft
Graph Entra BitLocker metadata, Intune discovery and Entra LAPS access.

This phase adds local **read-only** source-qualified findings:

- RBAC RecoveryRead against the current **enabled Windows token**;
- optional rotation permission (not an automatic failure);
- strict audit / broker policy consistency;
- separate effective broker RecoveryReaders allowlist check;
- BitKeyBridge service state and LocalSystem identity;
- protected journal owner/NTFS integrity preflight, **only when the caller
  is already an administrator**; standard users receive an explicit Not Tested
  explanation rather than being asked to change ACLs;
- explicit explanation that no durable audit ACK has been written.

The checks do not reveal LAPS passwords or BitLocker recovery keys, request
privilege elevation, rewrite AD delegation, modify Microsoft Graph consent
or write a synthetic audit probe. A passing RBAC check does NOT prove AD/Graph
permission or broker acknowledgement. Existing explicit recovery authorization
and durable audit ACK are still required before any actual secret read.

The offline regression covers disabled and inconsistent broker policy and a
valid configured-policy combination. Managed Windows test identities remain
necessary to verify token membership, service ACL visibility and real broker
ACK end-to-end. This work is deliberately staged on the v0.31.0 UX branch and
is not part of the current v0.30.4 release.
