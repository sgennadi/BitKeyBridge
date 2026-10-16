# BitKeyBridge Authenticode code signing

v0.26.0 supports an **optional** certificate-backed signing step in the
Windows GitHub Actions build matrix. The repository does not contain an
organization code-signing certificate or its private key.

The step is skipped unless repository variable BITKEYBRIDGE_SIGNING_THUMBPRINT
is configured. The corresponding trusted Code Signing certificate and private
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

## Public stable v0.30.x release gate

The 0.30.0 Helpdesk candidate can be built and tested as unsigned artifacts in
Pull Request CI. Do not publish it as stable Latest until a controlled
managed-domain test and signed upgrade/rollback acceptance have completed.
The repository variable BITKEYBRIDGE_STABLE_030_APPROVED must be explicitly
set to true, and BITKEYBRIDGE_SIGNING_THUMBPRINT must select a real trusted
organizational Code Signing certificate present on the release runner.
Release CI verifies all three actual ZIP executables against that exact signer,
version, Git source SHA, Windows PE machine type and trusted Authenticode chain.
The verification manifest joins release SHA256SUMS and provenance attestations.

This safeguard matters because the published v0.29.x defaults included
AutoInstallUpdatesOnStart=true with RequireTrustedUpdateSignature=false.
An unsigned non-prerelease v0.30.0 could therefore automatically install
onto older endpoints before the intended acceptance tests.

## Public manual v0.30.0 exception (does NOT waive enterprise signing)

Public stable v0.30.0 is distributed only with MANUAL-UNSIGNED ZIP asset names
for technician testing. The pre-0.30 updater expects canonical
BitKeyBridge-win-<rid>.zip names and will not auto-install these assets.
Existing clients may report no compatible download. This does NOT constitute
Code Signing, managed-domain acceptance, or permission to roll out unattended
via GPO or Intune. Standard auto-update-compatible assets remain signed-release
gated until organizational PKI and workstation acceptance are completed.

## Unsigned 0.30.1 public auto-update release

At the owner's request, v0.30.1 restores canonical architecture ZIP names
for compatibility with existing update checks/auto-install. This is NOT
Authenticode signed and is NOT equivalent to verified institutional publisher
identity. GitHub HTTPS, canonical repository/tag/asset URL checks, exact
asset size, SHA-256 in SHA256SUMS.txt and GitHub digest, staged executable
version check/self-test, and rollback remain enforced.

CAUTION: pre-v0.30.0 machines that still have legacy unattended installation
may install this update automatically on startup. From v0.30.1 onward, new
installations default to check-only, and explicit unsigned opt-in is required.
The official signed-release job remains restricted to approved identity and
still requires organizational Authenticode; no signing gate has been relaxed.
