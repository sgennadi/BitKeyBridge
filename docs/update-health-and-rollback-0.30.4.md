# BitKeyBridge v0.30.4 - Post-install health and rollback

This change adds a second acceptance point after every executable replacement.
The existing download, SHA-256, publisher/trust policy and staged self-tests
remain mandatory. The privileged helper now verifies the actual installed
executable bytes against the previously verified staged digest, starts it with
`--self-test --no-elevation` under its existing identity, and rechecks the
Windows Service state if it was running before the update.

The helper commits success to update history and clears an older rollback hold
**only after the post-install health probe succeeds**.

On failure the helper attempts to stop a newly started, unhealthy service,
restores each backed-up EXE (or removes a newly created target), restarts the
previous service, and records the outcome. Automatic failure creates a
cross-version hold, preventing infinite install/rollback loops. Service or
file restoration failure remains an error requiring technician attention.

The health probe does not exercise live AD/Entra/Intune recovery and does not
read passwords or recovery keys. It also cannot certify CyberArk EPM/WDAC,
service dependencies or interactive/RDP GUI usability under every account.
Managed-domain acceptance is a separate check; automatic upgrades should
first be enabled for a small approved pilot.

CI regression tests verify post-install hash mismatch rejection and PowerShell
parsing of the release approval step, including the interpolation syntax that
previously blocked the v0.30.4 GitHub Release job.

## Legacy upgrade coverage (GitHub-hosted Windows)

A real `--apply-update-plan` transaction upgrades the pinned, previously published x64 EXEs for `v0.30.1` and `v0.30.3` to this candidate, then rolls each back. The test runs only in disposable GitHub-hosted Windows as an elevated administrator. It confirms byte-for-byte SHA256 recovery, the `.bak` checkpoint, unmodified synthetic machine/user configuration, file ACLs, and CurrentUser DPAPI decryptability. No real credentials are provisioned or uploaded. This is a single-architecture acceptance supplement, not proof of live domain recovery or updater network trust in a production environment.
