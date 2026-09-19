# Code signing

Release builds are designed so signing can be inserted after `dotnet publish` and before packaging.

The repository does not contain certificates, private keys, client secrets, or signing credentials.

Current baseline CI verifies each published executable with the built-in `--self-test` and publishes SHA-256 checksums for the release ZIPs. Add your organization's signing provider (for example Azure Trusted Signing or SignPath) only through GitHub environment/secrets/OIDC configuration; never commit signing credentials.

When signing is enabled, the recommended order is:

1. Build and publish `win-x64`, `win-x86`, and `win-arm64`.
2. Run `--self-test`.
3. Sign `BitKeyBridge.exe`.
4. Verify the Authenticode signature.
5. Package architecture ZIPs.
6. Generate `SHA256SUMS.txt` from the signed packages.
7. Create the GitHub Release.
