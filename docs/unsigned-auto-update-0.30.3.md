# BitKeyBridge v0.30.3 — stable unsigned release

BitKeyBridge v0.30.3 is an ordinary stable GitHub Release for Windows x64, x86 and ARM64, **without an Authenticode signature**. It supports manual installation and the application's built-in automatic installer. Automatic installation of an unsigned update requires an explicit, persisted operator opt-in. No signing certificate is created or required for this release.

## Download and installation

From the official repository sgennadi/BitKeyBridge, select the matching architecture: BitKeyBridge-win-x64.zip, BitKeyBridge-win-x86.zip or BitKeyBridge-win-arm64.zip. SHA256SUMS.txt contains checksums, BUILD-INFO.txt the source revision, BitKeyBridge.cdx.json the SBOM and BitKeyBridge-DomainPreflight.cmd read-only diagnostic collection.

On an authorized Windows workstation, run from the executable's folder:

    BitKeyBridge.exe --version
    BitKeyBridge.exe --update-checks-enable --no-elevation
    BitKeyBridge.exe --auto-update-enable --accept-unsigned-update-risk --no-elevation
    BitKeyBridge.exe --auto-update-status --no-elevation
    BitKeyBridge.exe --check-update --no-elevation

An existing v0.30.1 installation where the operator **already** accepted unsigned automatic updates can receive v0.30.3 automatically on startup. If consent was not previously given, the application can check for the new release but does not silently enable auto-install.

In v0.30.3 open the Administration > Updates page, enable automatic checks and installation, select **Allow unsigned auto-install from official GitHub (requires consent)**, confirm the security warning and save. Changes to managed machine configuration still require the necessary permissions. A rollback hold prevents repeat installation until the operator resumes it.

If the machine already requires a trusted Authenticode signature or a pinned publisher, explicit unsigned consent **does not override that policy**. In addition CyberArk EPM, AppLocker and WDAC may separately block unsigned EXEs.

## Important security note

GitHub SHA-256, official repository/HTTPS URL validation, asset name and size, PE architecture, ProductVersion, staged self-test, and rollback protect against many errors and corruption; **they do not independently authenticate the software publisher**. If control of the GitHub release publication is compromised, a malicious file and matching checksum could both be published. Do not assume unsigned updates provide the same guarantees as signed releases.

Protected Audit Broker, Windows-token RBAC and fail-closed audit-before-secret stay enabled where configured. Do not change production RBAC, recovery entitlement, NTFS journal permissions or audit policy just to make the update work.

Technical staff should test real AD/Entra/Intune permissions and CyberArk EPM on controlled computers before wide rollout. They can run:

    BitKeyBridge-DomainPreflight.cmd "C:\Path\To\BitKeyBridge.exe"

This produces local, secret-free startup and Audit Broker readiness reports. Do not attach recovery keys or LAPS passwords to issues or support tickets.

This release is not QA- or prerelease-labeled. It is unsigned by owner instruction.
