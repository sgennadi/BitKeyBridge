# Contributing

1. Do not commit organization-specific domains, OUs, DC hostnames, recovery passwords, tenant IDs, or certificate material.
2. Target .NET 10 and Windows x64 unless a change explicitly adds another supported runtime identifier.
3. Keep AD operations read-only unless a future feature is separately reviewed and explicitly gated.
4. Run `build-release.cmd` or confirm the GitHub Actions Windows build passes before merging.
5. Avoid logging secrets. Error messages should contain identifiers only when operationally necessary.
