# BitKeyBridge v0.30.0-rc.1 — prerelease validation

**This is a test release, not the stable production channel.**
Published assets must be created by GitHub Actions on the exact verified
staging branch commit. The stable `main` branch and the published stable
`v0.29.12` remain untouched.

## Contents

- BitKeyBridge-win-x64.zip
- BitKeyBridge-win-x86.zip
- BitKeyBridge-win-arm64.zip
- BitKeyBridge.cdx.json (CycloneDX SBOM)
- SHA256SUMS.txt (all packages and SBOM)

GitHub Actions includes provenance and SBOM attestations. A GitHub Actions
artifact ZIP is a build artifact, not a published GitHub Release, and cannot
be automatically installed by the existing application updater.

## Validation gates

A release requires successful cross-architecture compilation and publishing,
LAPS regression tests, x64 offline self-test, WinForms HiDPI layout tests,
updater preference self-test, exact PE FileVersion/ProductVersion identity
and Release SHA-256 verification. The CodeQL workflow runs independently.

The `Publish RC v0.30.0-rc.1` commit prefix is only recognized on the
dedicated staging branch. The release workflow verifies the tag matches the
project's declared `0.30.0-rc.1` version, and publishes it with
`--prerelease`. A stable release is never overwritten.

## Upgrading a test endpoint

1. In BitKeyBridge Settings → Updates, select **Allow prerelease versions**.
2. Select **Save Update Settings**, then **Check for Updates**.
3. Review the offered `v0.30.0-rc.1` version and choose **Update Now**.
4. The updater revalidates the GitHub Release metadata, package SHA-256,
   Windows architecture, SemVer identity, Authenticode policy and staged
   executable self-test before installing.
5. Confirm `BitKeyBridge --version` reports `0.30.0-rc.1`.

**Security:** Old `0.29.x` configurations may have automatic installation
enabled even without a trusted Authenticode publisher. For managed
workstations, disable unattended prerelease installation unless the package
has a certificate trusted by enterprise policy. The RC is not an
authorization to bypass delegated AD/Entra permissions.

When a stable `v0.30.0` Release becomes available, this candidate
recognizes it as newer and can upgrade through the normal channel.
