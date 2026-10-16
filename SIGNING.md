# BitKeyBridge Authenticode code signing

v0.26.0 supports an **optional** certificate-backed signing step in the
Windows GitHub Actions build matrix. The repository does not contain an
organization code-signing certificate or its private key.

The step is skipped unless repository variable BITKEYBRIDGE_SIGNING_THUMBPRINT
is configured AND the workflow is processing an explicitly qualifying
**release push** (trusted version tag, main/master `Release v...`, or
the narrowly scoped historical rc.1 publication). It never signs Pull
Requests, arbitrary main commits or manual workflow_dispatch branches.
PR workflows only receive read-only GitHub repository permissions;
contents/write and attestation permissions are granted only to the release
job. The temporary CI signer is used only for positive/negative validation,
not artifact publication. The corresponding trusted Code Signing certificate and private
key must also be available to the Windows runner from an authorized signing
provider or protected certificate store. EV hardware tokens, HSM/cloud signing
or restricted self-hosted runners may be required. Never commit a PFX,
certificate private key or signing password.

The workflow signs **after publish and before executable self-tests, packaging,
SHA-256 and provenance attestation**:

1. Locate Microsoft's Windows SDK signtool.exe.
2. Sign the published EXE using configured thumbprint, SHA256 digest and
   RFC3161 timestamp (SignTool flags /sha1 /fd SHA256 /tr /td SHA256).
3. Run signtool verify /pa /all /v; any failure blocks packaging.
4. Run executable/UI/update self-tests on the signed artifact.
5. Package signed ZIPs and produce release checksums and attestations.

When signing is not configured, packages remain unsigned and the existing
update checksum/attestation validation continues. RequireTrustedUpdateSignature
and TrustedUpdatePublisher remain opt-in to avoid locking out old deployments.
Only enable publisher pinning after the organization has validated a trusted
signed release in staging.

Use Start > Device Diagnostics... > Verify EXE signature to check the running
EXE with Windows WinVerifyTrust. This is verification, NOT self-signing.

## Release signing: positive CI integration test

PR #57 runs a separate Windows-hosted test after all three architecture builds.
It generates a short-lived ephemeral OpenSSL Code Signing certificate
on a disposable GitHub-hosted Windows runner, signs **COPIES** of the three binaries, and
verifies the exact certificate thumbprint, trusted Authenticode, exact product
version, and PE machine type in `Verify-ReleaseSigning.ps1`. It then changes
only test ZIP contents or expected values and asserts rejection of incorrect
signer, version, architecture and unsigned executables. Certificate trust and
files are removed in `finally`. The test deliberately never uploads any
signed test artifacts.

The current PR test creates short-lived PEM/PFX credentials only on a
disposable GitHub-hosted Windows runner. It installs the temporary public
test CA into the runner's Enterprise Root store, and removes all temporary
trust during cleanup. The Windows CurrentUser Root store caused a
noninteractive certificate-enrollment timeout, so this isolation is
intentional. No ephemeral private keys or test-signed binaries are uploaded.

The GitHub release job now tests local updater settings BEFORE
publishing assets. It never performs a network update or executable
replacement on CI. Automatic deletion of release/* branches is disabled:
branch maintenance must be explicit and independently reviewed.

This is **not** an enterprise-trusted signed release. It does not configure
BITKEYBRIDGE_SIGNING_THUMBPRINT, install an organizational private key,
authorize GitHub Release publication, or test actual domain trust chains.
This PR test is separate from any organizational signing; real signing
requires its own controlled runner/provider and manual field acceptance.

## Protected audit broker: mandatory signing policy

When the managed `AuditBrokerEnabled` and
`RequireProtectedAuditForRecovery` policies are both active, the application
requires a genuinely trusted Authenticode signature **even for manual
updates and rollbacks**. Configure `TrustedUpdatePublisher` with the
signer's exact certificate Common Name (as reported by BitKeyBridge
certificate diagnostics), not a partial or wildcard publisher string.
The broker-mode comparison is exact and case-insensitive; legacy portable
updater behavior is unchanged.

The elevated update helper independently loads the current machine policy
and rechecks Authenticode on the locked, SHA256-verified staged executable
before replacing installed files. This closes a policy-bypass gap where
an apply-plan checksum alone could otherwise have been treated as approval
to install unsigned code. Signed enterprise rollout still requires
organization-specific Windows trust, certificate authorization and field
acceptance. No signing keys are included in this repository.

Security cautions:
- A Domain Controller LDAPS certificate is not necessarily a Code Signing cert.
- The Entra App Registration authentication certificate is NOT the signing cert.
- Do not export keys into repository variables or disable chain validation.
- Use restricted signing identities, audit trails and a trustworthy runner.


v0.26.8 continues to require explicit provisioning of an authorized
organizational Code Signing certificate. Without a configured signing
provider, CI correctly skips Authenticode and retains SHA-256 verification,
provenance attestations and updater rollback. An LDAPS or Entra application
certificate must not be repurposed as an executable signing certificate.
