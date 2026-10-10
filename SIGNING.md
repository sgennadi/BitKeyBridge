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
