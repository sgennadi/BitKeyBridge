# BitKeyBridge 0.35.0-rc.9 — recovery context and one-session hardening

**Draft unsigned manual QA only.** Do not merge, sign as Stable, distribute via GPO/Intune, or enable unattended upgrades until real managed Windows acceptance and enterprise signing are completed. Source branch: `release/v0.35.0-rc.1`, PR #115.

## Changes

- Devices query text changes immediately revoke the previous device recovery context, clearing any cached plaintext, unmasking status, recovery ID selection and outstanding Reveal/Copy generation. The next device must be re-selected using verified source identity after a new search.
- Targeted metadata source retry (AD/Entra/Intune) now revokes the prior recovery context before network work and disables results/Reveal/Copy during a retry. Failed or canceled retry does not fabricate a healthy source. No automatic MFA, no automatic secret reads.
- The initial interactive Device Code sign-in can include `DeviceLocalCredential.Read.All` with the existing BitLocker, Device and Intune scopes **only** if optional LAPS permissions are explicitly enabled. A single valid session can then serve Start, Devices and Entra LAPS. Disabled LAPS permission does not request a new scope; a dedicated LAPS-only sign-in still requests only LAPS-related scopes. This does not grant recovery or tenant permission automatically.
- A LAPS permission setting change modifies the Graph session context; old token permissions cannot be assumed to cover a newly enabled scope.
- Device 360 surfaces a safe local error when unexpected WMI exceptions occur, and exports the collection timestamp for read-only BIOS/TPM/hardware observations.
- UniversalDiagnostics 0.1.0-rc.1 remains embedded and **local only**; no GitHub uploads, relay, PAT or external EXE.

## Automated release gates

1. Windows 11 x64/x86/ARM64 .NET 10 compile/publish on exact candidate HEAD.
2. Native WinForms self-test: recovery query change masks key, invalidates selection/generation; existing Start→Devices identity mapping and no implicit MFA remain correct; Hebrew RTL and DPI 100/125/150/175/200% remain usable.
3. Scope-composition regression: no LAPS scope without explicit option; no Intune write scope without rotation option; combined consent requests LAPS only when enabled; dedicated LAPS remains least privilege.
4. Device 360 read-only hardware report includes provenance and UTC observation timestamp, no recovery identifier or key.
5. LAPS, protected audit broker, migration/rollback, signing-positive tests, CodeQL and shared UniversalDiagnostics prebuild checks all pass.
6. Final QA artifact packages tested x64/x86/ARM64 self-contained EXEs and includes exact-source `BUILD-INFO.txt` and SHA256 manifest.

## Manual Windows 11 operator acceptance still REQUIRED

- Search the same known device once on Start, open Device 360, navigate to Devices, confirm no duplicated AD/Graph fetch or MFA.
- Verify with two different exact device IDs that query edits, selection changes, canceled searches, failed source retries and Microsoft 365 reconnects revoke old Reveal/Show/Copy values and do not disclose or copy the old recovery key.
- Test authorized, delegated-but-insufficient, denied, expired MFA, and missing audit-broker configurations. BitLocker/LAPS secrets must only be available after existing protected audits, RBAC and ticket gates.
- With IT-approved `DeviceLocalCredential.Read.All` enabled, establish one delegated sign-in, confirm Entra LAPS reuses it without a second device code. With that permission disabled, verify ordinary BitLocker Device Code works without extra scopes. Changes to the permissions must require explicit reauthentication.
- Verify Device 360 AD remote WMI on a permitted host (BIOS/TPM), denial/offline RPC/timeout behavior and source timestamps. Never accept hostname-only AD/cloud identity matching.
- Verify EN/RU/HE text, RTL, font 100–200%, RDP/compact monitor, application version in title and preservation of window geometry.
- Validate GitHub release signature, installer/updater rollback on signed pilot; keep previous 0.33.1 stable available for rollback.

The existing production version is not replaced. The proposed RC9 is not proof that corporate Graph tenants grant delegated permissions or that end-user machines have been tested.
