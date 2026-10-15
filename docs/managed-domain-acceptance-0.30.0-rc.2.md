# BitKeyBridge 0.30.0-rc.2: managed Windows / AD / Entra acceptance

**Status: laboratory preview; NOT approved for production.**
The source remains in draft PR #57. Stable `main` and v0.29.12 are
unchanged. Use the EXE ZIP from the most recent **passing** PR #57
GitHub Actions run, not the published v0.30.0-rc.1 release.

## Scope and safety limits

- Use an **isolated, disposable Windows test workstation**, not a DC or
  an existing Helpdesk production machine. Snapshot/backup before any
  service installation or NTFS provisioning.
- A test local administrator is needed once to install the SYSTEM service,
  enable RBAC, provision the broker, and inspect protected ACLs.
- A **different, real standard Windows account** with narrowly delegated
  access tests the actual GUI and audit pipe. Never launch the entire
  Helpdesk GUI elevated just to make recovery work.
- Choose a *test computer* whose LAPS password and BitLocker recovery key
  can be rotated after acceptance. Do not export actual recovery secrets
  in test logs or send them to GitHub.
- GitHub CI validates local non-admin Windows tokens and NTFS impersonation.
  It cannot validate your tenant, AD rights, delegation, JIT/approval chain,
  enterprise Authenticode signer, or SIEM receipt.
- Unsigned CI test binaries must **not** be deployed through unattended
  automatic updates or treated as verified production releases.

## 1. Stage the tested EXE locally

Download an x64, x86 or ARM64 ZIP from **Actions -> build -> Artifacts**
for the latest PR commit with passed Windows matrix, LAPS and CodeQL.
Check SHA256 against the downloaded artifact metadata or trusted manifest
you obtained from GitHub. Preserve the exact version and commit ID in
your acceptance notes. Do not overwrite an existing installed service
on an operational workstation.

Run the commands below in a **CMD** window from the extracted directory.
No RSAT or PowerShell is required for the first preflight.

    BitKeyBridge.exe --version
    BitKeyBridge.exe --audit-broker-preflight --no-elevation

For a locally saved report, run the optional CMD-only helper from the
repository's `tests/windows` directory:

    BitKeyBridge-DomainPreflight.cmd "C:\Path\To\BitKeyBridge.exe"

Add `/probe` as the second argument only when you intentionally want to
write a synthetic protected audit event. The report stays in the current
user's `%LOCALAPPDATA%\BitKeyBridge\Acceptance` folder.

The preflight prints JSON showing join status, actual Windows identity,
configured RecoveryReaders, service/LocalSystem, signing and optional
ACL checks. With no `--probe` it DOES NOT write a health event or read
any recovery secret. Exit 3 means one or more required checks failed.
Some findings are deliberately `NotTested` until enterprise acceptance.

## 2. Provision test workstation as a local administrator

**This step intentionally changes local service/config/NTFS state.**
Run only on the designated test workstation, in an *already elevated*
CMD, with the **test Helpdesk group** selected by your AD administrator.

    BitKeyBridge.exe --install-service --no-elevation
    BitKeyBridge.exe --service-status --no-elevation
    BitKeyBridge.exe --rbac-enable --rbac-admin-bypass off --rbac-reader-add "DOMAIN\BitKeyBridge-Test-RecoveryReaders" --no-elevation
    BitKeyBridge.exe --rbac-status --no-elevation
    BitKeyBridge.exe --audit-broker-provision --no-elevation
    BitKeyBridge.exe --stop-service --no-elevation
    BitKeyBridge.exe --start-service --no-elevation
    BitKeyBridge.exe --audit-broker-verify --no-elevation

Replace the example group with a deliberately narrow existing test
group. Do NOT configure Everyone, Authenticated Users, BUILTIN Users,
Guests or the broad Domain Users group. Keep the administrator-only ACL
backup created inside `%ProgramData%\BitKeyBridge\ProtectedAudit`.

The service must run as `LocalSystem`, not gMSA or a domain account in
this preview. Preflight from an administrator may correctly say its
current identity is NOT an explicit RecoveryReader. Administrators are
not automatically accepted by the broker's pipe authorization.

## 3. Test the actual standard Helpdesk identity

Sign in with the new test Helpdesk user. **Sign out and in again after
group membership changes:** existing Windows access tokens may retain
old groups until the next logon. From ordinary CMD:

    BitKeyBridge.exe --audit-broker-preflight --no-elevation

Expected: `IsLocalAdministrator=false`,
`CallerEnabledRecoveryReader=Pass`, `MachineAuditPolicy=Pass`,
`ServiceRunning=Pass`, `ServiceIdentity=Pass`,
`AuthenticatedDurableAuditProbe=NotTested`.
`ProtectedNtfs=NotTested` for a normal user is expected: only an
administrator can verify protected ACLs.

Run an explicitly **opt-in** synthetic audit-only health check:

    BitKeyBridge.exe --audit-broker-preflight --probe --no-elevation

Expected: `AuthenticatedDurableAuditProbe=Pass` and exit 0. This
records only a synthetic local SYSTEM journal event, not a password or
BitLocker key. Alternatively:

    BitKeyBridge.exe --audit-broker-status --no-elevation

Open the GUI as the same user and confirm there is **no UAC prompt**.
Select AD or Entra/Intune in the startup wizard. Search a delegated
test machine, reveal/copy one approved test LAPS password and one test
BitLocker recovery key. Use normal approval/JIT workflows if enabled.
The pipe provides **audit only**; it never returns the recovery value.
Check that the *backend*, not only BitKeyBridge RBAC, enforces delegation.

From the elevated administrator account run:

    BitKeyBridge.exe --audit-broker-verify --no-elevation

Confirm valid protected journal owner/ACL and hash chain. Check the
journal contains a `RecoveryPreReadRequested` record with the actual
reader SID, session ID and test target for each authorized secret read,
and no actual password/recovery-key content. Export/redact *metadata
only*, never an unprotected copy of the secrets.

## 4. Negative cases (test accounts/targets only)

Perform and record each outcome:
- A different standard account, **not** in the reader group, receives
  `CallerEnabledRecoveryReader=Fail`, and
  `--audit-broker-preflight --probe` exits 3. No secret is revealed.
- Disable or remove test group membership, then **log off and log on
  again** as that user. Confirm refusal. Mere removal from AD cannot
  be assumed to rewrite an already-issued Windows access token.
- Change the administrator-controlled machine RBAC policy and confirm
  requests fail closed until the service is restarted with the new
  policy. Never weaken the protected root or journal ACLs.
- Stop the service: strict mode must refuse all recovery reads, without
  falling back to a writable user-profile audit journal.
- Revoke actual delegated AD/Entra permissions and confirm *backend*
  denial. Observe any authentication cache/token lifetime separately.
- Test unavailable DC/Graph endpoint, expired identity/certificate and
  missing ticket/JIT approval. Ensure denial does not disclose secrets.
- Test GUI 100%, 150%, 200% DPI/RDP and Windows x64/x86/ARM64 where
  your deployment actually uses them.

Do **not** run CI's
`tests/windows/ProtectedAuditBrokerIntegration.ps1` here. That script
changes temporary users and ACLs; it deliberately refuses persistent
or self-hosted computers.

## 5. Signing, update/rollback and SIEM production gates

An acceptable production EXE needs your organization's valid code
signing certificate, a trusted publisher pinned in BitKeyBridge's update
policy and a signed update/rollback test with a **staged** binary. Do not
use this test RC to turn on unattended updates. A SHA256 checksum proves
download integrity, not organizational authorization or Authenticode
publisher trust.

Local `BrokerProbe=Committed` proves a durable LOCAL journal
acknowledgement, **not** downstream SIEM receipt. Confirm SIEM outbox
configuration, delivery, deduplication, alert handling and fail-open/
fail-closed policy separately. Treat all external delivery failures as
unverified until your security team approves a retention strategy.

The protected journal can have both `audit.jsonl` and a rotated
`audit.jsonl.old`. Both, plus the `.lock` file and ancestors, are
checked for unsafe write ACLs. A second automatic protected rotation
cannot delete the old archive: strict recovery intentionally fails
closed at that point, pending an administrator-approved off-host sealing
and archival process. Retention, external sealing and off-host
archival require a security-approved policy; do not truncate/delete
these files to make a failed audit check pass.

### Release signature verification gate

The 0.30.x GitHub Release job requires explicit protected-audit approval
and verifies Authenticode on **all three EXEs**, not just the existence
of a signing thumbprint setting. The PowerShell 7 release validator at
`tests/windows/Verify-ReleaseSigning.ps1` checks trusted Authenticode
status, an exact 40-hex signing-certificate thumbprint, the PE x64/x86/ARM64
machine code and the full `ProductVersion` against the proposed tag.
It does not execute the release binaries.

New, approved 0.30.x releases include `BitKeyBridge.signatures.json`
as a release asset and in `SHA256SUMS.txt`. GitHub Actions attests that
signature-verification report. A bad/unsigned/mismatched architecture or
version fails before publication.

The thumbprint variable names a certificate, but does **not** provide a
signing certificate or approval on its own. A dedicated, trusted signing
environment must be configured independently; the repository never
contains private signing keys. Configure restricted reviewer approval
and protected tags/branches before actual production publication.
The verification gate does not substitute for AD/Entra or SIEM acceptance.

## 6. Evidence and rollback

Record: test build commit/version and SHA256, OS/architecture,
standard-user token confirmation, exact source (AD/Entra), negative
tests, sanitized ACL/chain status, signing result and SIEM receipt.
The local JSON preflight includes computer/user/SID identifiers; handle
it as internal operational data. Never include secrets or JWTs in a
support bundle.

Keep original machine settings, owner/DACL SDDL backup and the
historical journals. If the test fails, stop test operations, restore
an approved snapshot or the pre-test configuration and ACLs under an
administrator's supervision. Do not delete protected journal history.
If strict audit stays enabled with the service removed, non-admin
recovery intentionally remains blocked.

**Production acceptance:** an authorized **non-admin domain** reader
gets a durable pre-read ACK and an authorized delegated LAPS/BitLocker
read, an unapproved identity gets neither, the journal contains no
secret bytes, installed EXE/update signer is trusted, and SIEM/rollback
policies are explicitly reviewed. Until then PR #57 remains Draft.
