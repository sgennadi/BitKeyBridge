## 0.29.9

**Recovery cards on the left, computer picker on the right**

- Reversed Start's wide-screen layout: the main BitLocker and LAPS recovery cards occupy the left 66% of the available workspace, while the computer/results list stays on the right (34%) for switching computers without navigating away.
- BitLocker remains left of LAPS within the recovery area; LAPS password history and explicit Reveal/Copy/Diagnostics operations are unchanged.
- At narrow window widths, RDP resolutions or elevated DPI, keep a compact computer list above the cards so the search and selected-device actions remain usable without clipping.
- Added draggable pane headers and an accessible **Swap sides** button to swap BitLocker/LAPS and the computer picker on a wide Start layout without losing the selected computer. The side order is saved per Windows user under LocalAppData; compact screens still show the list above the cards.
- Added a persistent **Reconnect / Change connection...** action on Start to reopen the AD / Microsoft 365 wizard without restarting BitKeyBridge.
- If an AD sign-in fails because the password is invalid, discard the rejected session password and offer a one-time automatic retry wizard. **Retry AD Login** always requests a fresh password for a manual account; repeat failures never create an endless prompt loop. Windows Credential Manager entries are not silently deleted.
- Extended WinForms UI tests for both pane orders, swap controls and the invalid-credential retry classifier, preserving LAPS/BitLocker secrets and history during layout changes.
- Updated the runtime WinForms layout regression for compact, 150% and 200% display scaling.

## 0.29.8

**Explicit first-screen connection wizard and immediately visible BitLocker/LAPS cards**

- Open a dedicated **Connect to BitKeyBridge** wizard on every interactive launch after the updater, even when Active Directory was the previously saved choice. Pick **Active Directory**, **Microsoft Entra / Intune (Microsoft 365)**, **Both**, or **Choose later**; only Connect starts authentication.
- For AD, integrated Windows credentials are preferred. An explicit optional choice requests another AD account. For cloud, use the existing delegated Device Code/MFA flow, launching the cloud configuration wizard if Tenant/Client ID are missing. No local-admin or tenant-admin credential is silently requested.
- Fix Start's percentage-height search results region that pushed the BitLocker and LAPS cards far below a mostly empty grid. A selected computer now displays search results on the left and recovery cards on the right on a wide display; a narrow/RDP/HiDPI window uses a bounded compact grid immediately above the recovery cards.
- Keep Recent Computers in the remaining scrollable Start workspace, preserve masked/on-demand passwords and LAPS history, and reflow the layout when selection or window size changes.
- Extend UI self-tests: exercise the actual selected-computer layout at compact, 150% and 200% scenarios; ensure metadata-only selection cannot reveal secrets; include the startup connection wizard in cross-DPI dialog tests.

## 0.29.7

**Responsive maximized workspace, selectable AD / Microsoft Entra sources, least-privilege onboarding**

- Fix large-screen and RDP maximize behavior: MainForm now uses page-level scrollbars and a full-client Dock=Fill root, skips working-area corrections while maximized, reflows the active workspace on resize/DPI changes, and starts maximized for ordinary use.
- Add **Connect using** on Start: choose Active Directory, Microsoft Entra / Intune (Microsoft 365), or Both. The source is saved in each user's LocalAppData without requiring administrator access, and Start searches only the selected/connected source(s).
- Remove forced Active Directory credential prompts on startup. Integrated Windows identity is attempted only for selected AD source when AutoConnectOnStart is enabled; failed AD auto-connect waits for an explicit Connect action.
- Add a non-administrative Microsoft Entra connection wizard. Use an IT-approved App Registration's Tenant and Client IDs, then sign in using Device Code and MFA. A separate, explicit administrator setup path invokes the existing privileged Entra/Intune wizard if a new application or tenant consent is required.
- Keep BitLocker/LAPS password read privilege checks, audit, and masking unchanged; no broad privileges or secret storage are automatically granted. Document delegated Graph consent, per-device Entra roles and Windows LAPS AD delegation in docs/least-privilege-setup.md.
- Extend Windows UI layout regression to check full-width resize rather than only normalizing dock geometry in tests, verify the new source controls and wizard, and preserve HiDPI accessibility.

## 0.29.6

**Unified rounded Windows UI and direct BitLocker diagnostics**

- Apply the modern rounded visual design across all DPI-aware windows and dialogs: page tabs, input fields, buttons, DataGridViews, group boxes, status indicators, menus, and supported Windows 11 window corners, without changing the business logic or credentials behavior.
- Use a shared UiModernTheme connected to DpiAwareForm plus centralized UiStyle tokens for radius, backgrounds, borders, interaction states and DPI-safe rendering. Honor Windows high-contrast mode.
- Remove the three confusing Start toolbar buttons (Device Details, Intune rotation selector and duplicate BitLocker diagnostics). Device details and confirmed Intune key rotation stay in the Devices workspace.
- Place a direct **View diagnostic commands** link inside the BitLocker card. It opens the actual runbook for the selected computer without navigating to another workspace; the link remains disabled until selection.
- Keep explicit Reveal/Copy, LAPS history, audit and sensitive-data protections unchanged.
- Extend runtime UI checks to require the direct diagnostic link and reject obsolete navigation-only Start controls.

## 0.29.5

**Polished BitLocker/LAPS recovery cards and inline password history**

- Rebuilt the Start recovery cards with a unified modern Windows-style visual system: rounded white surfaces, vector shield/key/eye/copy icons, soft status pills, clearer typography, generous spacing, and prominent full-width Reveal/Copy actions. Design colors, typography, spacing, badge palettes and button geometry live in the central UiStyle/UiRecoveryCard layer with high-contrast support.
- Kept BitLocker on the left and LAPS on the right when space permits, with responsive stacking on narrow/high-DPI displays. Advanced diagnostics and safe Intune rotation remain in the secondary Start toolbar.
- Added **Password history** directly inside the LAPS card. **Load history** is an explicit privileged action: the resulting read-only grid shows current/historical records, managed account, update timestamp and access status, but never plaintext passwords.
- Selecting a current or historical row changes the target for **Reveal Password** / **Copy Password**, with existing per-operation authorization, decryption and audit logging. If a record is unreadable, a diagnostic warning replaces the secret.
- Computer changes, password lifetime expiry and **Clear secrets now** also clear Start history selections and cached credentials. Start never auto-reads a password when a computer is selected.
- Extended the WinForms UI layout self-tests to verify the new card hierarchy and initially masked/empty history controls.

## 0.29.4

**Side-by-side BitLocker and LAPS on Start**

- Replaced the Start BitLocker/LAPS switchable tabs with two compact recovery cards shown simultaneously: BitLocker on the left, LAPS on the right. There is no service-navigation button in either card; Reveal/Copy are available directly.
- When metadata confirms only one recovery service, its card fills the available width. Unknown or failed lookups remain explicitly unverified, never claimed to mean no backup.
- Both cards wrap into a single column on narrow windows/high DPI and use shared UiStyle sizing constants. Only metadata appears by default, with masked placeholders until explicit password request.
- Start LAPS Reveal/Copy requests only the current password (not history) through the established authorized/audited read/decrypt path, without changing the history setting in the detailed LAPS workspace.
- Renamed the main navigation tab from **Recovery** to **BitLocker**; internal recovery identifiers and behavior remain unchanged.
- Updated the WinForms UI self-test to verify the two recovery cards, order, and disabled secret actions before a computer is selected.

## 0.29.3

**Start computer tabs for BitLocker and LAPS (test build)**

- Selected AD computers now show neighboring **BitLocker | LAPS** tabs directly under Start search results, with BitLocker first and only metadata-confirmed services shown when lookups complete.
- BitLocker Reveal/Copy and recovery diagnostics remain available in the BitLocker tab; LAPS gains **Reveal LAPS Password** and **Copy LAPS Password** on Start.
- Selecting a computer or switching tabs never reads or reveals either password. LAPS only reads through the existing authorized, audited AD/decryption flow after an explicit action; secrets are shown in a temporary dialog or copied with the existing clipboard expiry.
- AD LAPS reads that return no passwords display a specific access/backup-policy warning. Multiple current readable password records require explicit selection in the detailed LAPS view.
- Advanced Recovery, Devices and LAPS views remain available. Navigating to detailed LAPS no longer triggers a password read automatically.
- Tab height is centralized in UiStyle for DPI scaling and wrapping action buttons.
- Enlarged the AD credential connection dialog and shortened field placeholders so explanatory text and credential fields fit without the prior clipping; dimensions are centralized in UiStyle.

## 0.29.2

**Native GKDI/KDS RPC endpoint discovery and readable network failure diagnostics**

- **Start > Environment Check** now probes the observed TCP 1344 endpoint as an explicitly site-specific diagnostic, not as a universal/static KDS port. A closed historical TCP 1344 port is informational if it is not advertised by the DC for MS-GKDI.
- Added read-only native Windows RPC Endpoint Mapper inquiry on TCP 135 for the **MS-GKDI v1.0** interface UUID `b9785960-524f-11df-8b6d-83dcded72085`. The application displays the exact advertised `ncacn_ip_tcp` ports on the selected DC and checks only those ports. No dynamic RPC port-range scans, firewall changes, or secret/decrypt operations.
- TCP probes now distinguish **Connected**, **Connection refused**, **Timed out**, **Unreachable** and **DNS failure** instead of reporting every failure as an undifferentiated timeout. An unreachable advertised GKDI endpoint is a warning; a successful TCP connection is not proof of authenticated decryption or permissions.
- Added manual, copyable RPC/KDS checks in the AD LAPS diagnostic runbook and documented the difference between TCP 1344 / IANA ICAP registration and an actual listener.
- Extended LAPS regression tests for endpoint parsing, port status classification, observed-port policy and the parameterized PowerShell troubleshooting guide.
- No changes to BitLocker/LAPS secret handling, AD permissions, DC services or automatic firewall policy.

## 0.29.1

**LAPS layout / HiDPI text-overlap regression**

- Fixed LAPS text and warning overlays seen when an encrypted Windows LAPS password cannot be decrypted. The former percentage-height results row inside an AutoScroll TableLayoutPanel could collapse and paint record details over the actions or warning banner at compact sizes / 150%-200% scaling.
- Moved scrolling into a dedicated viewport; all LAPS content sections now occupy individual AutoSize rows. Both results grids have explicit minimum/visible heights and their own scrollbars, and expanded record diagnostics have a dedicated measured height.
- Preserved the pinned password/status footer outside the scrollable area, without changing LAPS read/decrypt permissions, secret-handling behavior or warning messages.
- Extended the WinForms layout self-test to assert scroll ownership, row ordering, non-collapsing result grids, wrapping actions and footer separation at compact, 150% and 200% UI scales.

## 0.29.0

**Verified Local and Online Rollback**

- Retained **Rollback Previous Build** after clean installations: a missing local `.bak` now falls back to selecting the nearest strictly older published GitHub Release for the running x64/x86/ARM64 architecture. An explicit **Online Rollback...** action also works when a local backup exists.
- Reused the existing GitHub asset download/verification pipeline for downgrades, including restricted release URLs, declared package sizes, `SHA256SUMS.txt` and GitHub asset SHA-256 digest checks, executable version validation, configurable Authenticode policy, and `--self-test`.
- Added the same Authenticode-policy and staged self-test checks to local `.bak` rollback, before launching the privileged replacement helper. Rollback actions remain manual; startup update checks never automatically downgrade.
- Preserved the current executable as the next `.bak` and recorded Local/Online rollback provenance in update history and audit events. Failed apply attempts restore backed-up executables.
- Prevented an immediate re-upgrade after a deliberate rollback: the updater writes a version-bound rollback hold and also disables the existing automatic-install configuration flag so **older restored releases** that do not understand the hold cannot silently reinstall the latest version.
- Manual **Update Now** remains available. A new **Resume Auto-Install** action re-enables automatic checks/install and clears the rollback hold. Added offline self-test coverage for architecture/prerelease/version-aware release selection and persistence of the hold.

## 0.28.0

- Added administrator-only Devices > LAPS Coverage for metadata-only Windows/Legacy LAPS inventory by AD OU/domain. Native paged LDAP reads objectGUID, distinguished name, expiration indicators and AD lastLogonTimestamp without requesting any password/history attribute.
- Classifies evidence as Windows, Legacy, Both or Not verified. Expired and expiring-soon indicators are based only on visible expiration metadata; hidden/unavailable metadata is never asserted to mean missing backups. A truncated scan cannot replace the previous full snapshot.
- Added stable identity-based baseline comparison (AD objectGUID/DN, never name), reporting newly visible/hidden backup indicators, changes of expiration, new AD objects and objects no longer visible. Stores a rolling last 30 non-secret snapshots under the current user's LocalAppData.
- Added native --laps-coverage CLI mode for scheduled tasks, producing metadata-only CSV/JSON and a comparison JSON without Graph, PowerShell or RSAT. Optional --laps-coverage-fail-expired returns exit code 24 when any visible expiration is overdue.
- Added optional manual AD LAPS expiration requests: disabled by default for every application session, requires a recent complete scan, unambiguous objectGUID+DN, exactly one Windows or Legacy LAPS policy, BitKeyBridge Rotate authorization, ticket/reason where configured, explicit confirmation and auditable result. The app only sets the past expiration timestamp; the password is NOT rotated until the target computer next processes LAPS policy.
- CSV exports guard spreadsheet formula injection. Integration and HiDPI regression tests cover expiry classification, stable-ID comparison, mixed-policy refusal, CSV safety and default-disabled rotation controls.
- Added deployment templates for Microsoft Intune Win32 packaging and Group Policy computer startup without forcing installation of a Windows service. Uninstall preserves administrator-owned ProgramData and user-owned recovery/audit configuration.

## 0.27.0

**Unified Helpdesk**

- Added authenticated Entra/Intune discovery to Start alongside AD BitLocker/LAPS. Search/Enter includes Cloud when the active Graph session is valid; live typing stays AD-only to avoid excessive Graph requests. Cloud-only searching works without an AD session.
- Source-qualified cloud results never open AD Recovery/LAPS automatically. Selecting a Cloud row opens the Devices workspace, where the operator must explicitly identify the managed object before recovery/rotation.
- Device 360 displays immutable source IDs, AD GUID/DN, Entra and Intune device IDs, hardware, OS, compliance, encryption, last sync and unverified recovery status. Copy Device 360 Summary omits secret values and recovery ID lists.
- Microsoft Graph delegated Intune access now defaults to DeviceManagementManagedDevices.Read.All. The administrator can explicitly enable ReadWrite.All for Intune rotation in Administration > Cloud or the Setup Wizard. Rotation additionally requires source ID verification, access checks and confirmation.
- Pre-existing Entra application grants are not silently revoked when read-only is selected. Administrators upgrading from an older release must separately review and revoke any unneeded tenant write role assignments.
- Regression checks added for metadata-only Cloud mappings, least-privilege permission selection, cloud routing and non-secret Device 360 summaries. Maintains native .NET 10 WinForms and x64/x86/ARM64 packages.

## 0.26.9

- Removed the unsafe Start > Rotate via Intune name-match/first-result fallback. Start now opens Devices and performs read-only discovery; the operator must select an exact Intune managed-device object and confirm its identifiers before using the existing privileged Rotate action. No rotation occurs when entering Devices.
- For manually selected Devices rows, name collisions require an explicit Entra Recovery ID selection before key retrieval; Intune rotation can target an exact managedDeviceId after explicit operator confirmation. Conflicting Entra device IDs remain blocked.
- Unified Devices now queries on-prem AD, Intune inventory and Entra BitLocker metadata independently. Failed Graph/LDAP sources no longer hide successful results from other sources; the status and sanitized diagnostics state exactly which source failed.
- Failed or unqueried Entra metadata is marked unverified rather than presenting a false zero recovery-key claim. Cloud recovery IDs are labelled separately from AD recovery coverage, and partial per-device enrichment errors are surfaced.
- Extended deterministic regression tests to cover partial AD/Intune/Entra source responses, 403/429-style failures and complete source failure, plus the HiDPI Start rotation selection label. Builds remain native self-contained Windows x64/x86/ARM64 with no PowerShell dependency.
- The existing v0.26.8 source-qualified identity and ambiguous-device protections remain enforced. Authenticode signing is not enabled without an organization-approved signing certificate.

## 0.26.8

- Hardened unified Devices identity correlation: AD computer objectGUID/DN, Intune managedDeviceId, and Entra deviceId are retained as source-qualified identifiers. An identical short hostname/UPN/serial does not prove cross-source identity, so such entries are kept separate and visibly marked as name collisions.
- Attached cloud recovery IDs to a managed Intune device only when its Entra deviceId identifies exactly one Intune record. Conflicting Entra IDs keep recovery metadata isolated; ambiguous records disable quick recovery-key selection and rotation instead of guessing.
- Added objectGUID collection to AD computer searches and exact-name collision protection. AD LAPS search metadata now preserves the computer distinguished name, allowing Start to detect two different AD objects with the same display name.
- Made BitLocker and LAPS Start searches failure-isolated, including exact-name/domain retry. Results from the successful source remain visible; failed lookups are explicitly reported as failures, not proof that backups are absent. Canceled operations remain canceled.
- Disabled one-click Start recovery actions for ambiguous AD computer names, and prevented automatic LAPS routing if BitLocker metadata could not be read.
- Expanded deterministic offline and UI regression checks for source-ID collisions, recovery association, independent metadata errors, cancellation, and safe fallback behavior. No AD writes, Graph secret reads during search, automatic key rotation, or additional required operator permissions.

## 0.26.7

- Updated Start results navigation: BitLocker metadata takes priority; confirmed LAPS-only metadata automatically opens the LAPS page on Enter/double-click. Unknown backup status continues to require an explicit choice.
- Preserved the computer search target across Start, BitLocker Recovery and LAPS tabs, including both directions, manual input, selected Start rows, selected BitLocker records and LAPS candidates.
- Reused a non-secret computer context; switching tabs does not automatically read keys or passwords. Switching target cancels stale searches and clears the destination workspace's old sensitive results.
- Expanded WinForms UI regression coverage for routing, search target persistence, and layout at compact, 150% and 200% scaling.

## 0.26.6

- Added a DPI-aware UiStyle.SectionGap (12 logical pixels) between the Start page Active Directory "Connected" status and "Connected as" account indicator; preserved auto-sized rows for compact/150%/200% scaling.
- Removed duplicated yellow LAPS read/decryption warnings: one detailed warning remains above the results grid, while the pinned footer displays only a neutral summary with record and readable-password counts.
- Preserved the selected LAPS record's separate error details, the diagnostic commands button, and read-only/no-secret diagnostic behavior.
- Added HiDPI UI layout and cross-platform LAPS regression assertions for the gap and nonduplicated warning/footer text.

## 0.26.5

- Clarified PowerShell 7 and Windows PowerShell 5.1 prerequisites for optional Active Directory and LAPS diagnostic commands, including module checks, optional Windows client/server RSAT installation and compatibility guidance.
- Added a guarded, read-only AD group SID check in the LAPS diagnostic runbook: predefined DC, explicitly replaced group identity, handled unavailable Get-ADGroup, and a nonempty-SID check before whoami /groups and Select-String.
- Explained that Windows sign-in token membership is different from an explicit BitKeyBridge AD connection and does not prove LAPS confidential-attribute read rights or DPAPI-NG decrypt authorization.
- Extended BitLocker guidance and diagnostic regression tests. BitKeyBridge itself remains native .NET, requires no PowerShell/RSAT, and does not install modules or alter AD permissions.

## 0.26.4

- Fixed long LAPS status messages becoming invisible or cut off: shared UiStyle now configures width-constrained, auto-height multiline status labels, and UiStatusLabel preferred-size measurement honors MaximumSize.Width even for an unconstrained WinForms TableLayoutPanel sizing pass.
- LAPS operation status, selected-record summary and no-readable-password warnings now wrap instead of running off-screen. Result summaries separate history/expiration/cleanup notices into readable lines.
- Moved the busy marquee progress bar out of the scrollable LAPS records workspace and into a full-width row of the pinned password/status footer. The narrow ghost progress strip beside the results grid no longer has a place to render.
- Added regression checks for multiline preferred-size measurement, compact/150%/200% DPI footer structure and location of the busy indicator. The password field and buttons remain pinned; no changes to LAPS decryption or AD permissions.

## 0.26.3

- Fixed LAPS compact-window layout: a dedicated pinned lower footer keeps Password, Reveal, Copy password, Copy account, selected record status and operation messages visible while metadata/results/details scroll above. The shared UiStyle layer now prevents important LAPS DataGridView columns and date/status text from being squeezed below readable widths; narrow grids scroll horizontally.
- Improved LAPS account presentation: Legacy LAPS entries without a backed-up account name show a short "Name not stored in AD" label, preserve the full explanation in the cell tooltip, and disable misleading Copy account; other actual accounts remain copyable.
- Fixed Start unified search navigation: when BitLocker and LAPS metadata are both present, double-click/Enter opens BitLocker Recovery; an unverified BitLocker result asks the operator to choose instead of silently opening LAPS. BitLocker Recovery navigation stays available for a selected computer even without visible recovery metadata; reading a key still requires the normal permissions.
- Fixed missing BitLocker metadata in the unified search when a remembered OU excludes the computer: exact-computer searches retry the domain naming context and preserve the selected OU on Start. Opening Recovery for a key found outside the OU now switches Recovery to that domain scope, so the discovered key does not disappear on navigation. Missing BitLocker metadata is "Not verified" rather than "No backup".
- Fixed Updater's stale saved-result behavior: Update Now always refreshes GitHub Releases before asking to install (preventing an old v0.26.0 selection), sends explicit no-cache headers, and labels previously saved release information as potentially stale with the repository/tag shown.
- Expanded Windows HiDPI UI self-tests for pinned LAPS password controls at compact/150%/200% scales, readable result columns, BitLocker-versus-LAPS routing and scoped-to-domain metadata retry. No automatic AD permissions or secret changes.

## 0.26.2

- Fixed unified Start LAPS status: when LDAP computer lookup succeeds but expiration/version metadata is missing, the result is "Not verified", not "No backup". "Detected" and "Expired" still require positive non-secret metadata.
- The LAPS Passwords action remains enabled for any selected AD computer so an authorized, explicit LAPS read is possible even if metadata visibility is limited. Search itself remains metadata-only and never requests secret values.
- The LAPS results distinguish native DPAPI-NG AccessDenied ("Decrypt unauthorized") from other decryption errors ("Decrypt failed"), rather than incorrectly treating both as an absent password. Returned encrypted attributes count as backup evidence.
- Yellow warning banners and record details now explain whether the AD encrypted password was returned but decryption was unauthorized or failed for another reason; compare the Connected as account to AuthorizedDecryptor without automatic AD changes.
- Added non-secret troubleshooting guidance and offline regression tests for missing metadata, expired/detected states, unauthorized encryption principal, and generic DPAPI-NG failure. No permissions, GPOs, schema, or password backups are modified.

## 0.26.1

- Added a dedicated "Connected as" Active Directory account indicator on Start and an always-visible "AD user" field in the global status bar (also visible on BitLocker and LAPS tabs).
- Reports the actual credential selected for a successful LDAP bind: current Windows identity, session credentials, Current User vault or Machine / Service vault. The connection test reuses that credential for DC discovery and bind instead of assuming the configured username matches a stored credential.
- Clears the connected identity when reconnecting, canceling, disconnecting or failing authentication, and uses the validated account in AD access troubleshooting.
- Adds UI self-test coverage for dedicated Start and global account indicators at compact and high-DPI window sizes. Only account names are shown; no passwords or tokens.

## 0.26.0

- Device Diagnostics Center: added Start > Device Diagnostics... with responsive, shared UiStyle WinForms controls and compact/200% DPI checks.
- Exact-computer DC consistency: compare BitLocker Recovery ID metadata, Windows/legacy LAPS expiry/version, computer visibility, site/RODC status on up to 24 controllers; no password attributes requested.
- Smart Diagnostic Engine: evidence-graded findings (Confirmed/Likely/Possible/NotVerified), explicit next action and execution location; absence of metadata never proves AccessDenied.
- AD permission inspection: read-only computer-object DACL including observed explicit/inherited ACE SID, object GUID and control/read masks; confidential attribute and DPAPI-NG effective rights remain not verified without separate authorized tests.
- Offline endpoint collector: --collect-endpoint [--output file.zip], GUI collect/import, bounded ZIP format, Windows OS and whitelisted nonsecret policy settings, local nonsecret BitLocker volume/TPM WMI states and BitLocker/LAPS event IDs/severity/timestamps. Event descriptions, keys and tokens are never exported.
- Advanced LDAP/LDAPS: native SRV records, DC site/bind latency, strict TLS certificate SAN/expiry/chain verification; suggested writable DC requires explicit operator confirmation and reconnect.
- AD versus Entra BitLocker recovery metadata comparison: only uniquely resolved Entra deviceId or explicit GUID accepted; partial sources reported without claiming no backup.
- Protected recovery cache: DPAPI CurrentUser .bkb sidecar, atomic verified migration with protected previous version, Live AD / Local CSV / Protected cache choices, direct in-memory metadata and authorized on-demand key reading. Existing plaintext CSV is never silently removed.
- Optional Authenticode signing in CI with a configured organizational signing provider/certificate; signature validation before packaging, all existing trust-enforcement toggles remain opt-in. Unsigned builds remain the default until a real signing certificate is provisioned.
- Expanded offline/portable rules, CurrentUser cache tamper and round-trip, endpoint ZIP validation and responsive UI self-tests; no AD writes or automatic PowerShell.

## 0.25.5

- Fixed the BitLocker diagnostic runbook to include the selected Active Directory OU/search scope when inspecting zero recovery records or permissions.
- Added an explicit read-only AD OU verification command scoped to the chosen DC and a regression assertion checking correct OU substitution.
- Retains the 0.25.4 Environment Check, yellow BitLocker warnings, workstation/AD/Entra diagnostics and read-only behavior. No service, AD ACL, GPO, recovery password or policy changes.

## 0.25.4

- Added Start > Environment Check... for normal helpdesk operators, including on non-domain-joined workstations with explicit AD credentials. Probe is native C#/.NET, read-only and does not require PowerShell or RSAT.
- Checks Windows OS/architecture, DC discovery and host DNS, configured LDAP/LDAPS TCP port, Kerberos TCP 88, RPC mapper TCP 135 and SMB TCP 445, and separately performs a signed/sealed or TLS authenticated LDAP bind.
- When an exact computer is provided, reads only AD BitLocker recovery object metadata and LAPS expiration/version metadata. No recovery passwords or LAPS secret attributes are requested.
- Cloud-configured installations additionally check outbound TCP 443 to Microsoft identity and Graph endpoints; token permissions and consent are explicitly not inferred from the result.
- Reports remote DC service health, KDS/DPAPI-NG, dynamic RPC, confidential attribute read/decrypt authorization as NotVerified unless an actual authorized test proves it. Avoids false-positive permission diagnosis.
- Added a prominent yellow BitLocker Recovery warning above the results grid for zero records, missing connection/scope/cache, LDAP search errors and recovery-password retrieval failures.
- Added context-aware BitLocker diagnostic commands on Recovery and the selected Start computer (including when the computer has no key metadata). AD, local cache and Entra paths have separate safe instructions, with the optional secret-read test explicitly marked as sensitive.
- Added cancellable, responsive/200-percent DPI environment GUI, safe report copy, cross-platform diagnostic regression checks and UI layout assertions.
- All diagnostics remain read-only; no ACL/GPO/BitLocker secret edits, password rotations, remote scripts, or new mandatory RBAC/JIT/two-person/ticket/SIEM controls.

## 0.25.3

- Added a prominent yellow LAPS zero-readable-password warning immediately above the password results grid. Visible backup metadata triggers a likely read-delegation explanation, without falsely asserting that an actual stored password or access denial was verified.
- Added Diagnostic steps and commands... with a responsive copyable dialog identifying where to run checks: affected workstation, domain controller / RSAT workstation, and operator workstation.
- Automatically uses selected AD computer DN, OU and DC; covers Windows LAPS, Legacy LAPS and Entra LAPS separately.
- Includes GPO, LAPS Operational log, non-secret AD expiration metadata, Find-LapsADExtendedRights, whoami groups and Entra metadata checks. A separate password-read/decrypt probe is explicitly marked optional and sensitive.
- If encrypted LAPS rows were returned but none could be decrypted, the warning links to separate AD read and decryption guidance without losing per-row errors.
- Leaves diagnostic details visible until a new lookup or explicit clear. No PowerShell commands are executed by the app; no ACL changes, password resets, mandatory tickets, RBAC/JIT, two-person approval or SIEM defaults are changed.
- Adds regression tests for safe command generation, escaped-comma OU extraction, AD versus Entra instructions, and a UI layout check that keeps the warning above the results grid.

# Changelog

## 0.25.2

- Fixed **LAPS → Read LAPS** when AD or Entra returns zero password records: the screen now explains the probable causes instead of displaying only `0 records, 0 readable passwords`.
- Distinguishes non-secret **Legacy LAPS / Windows LAPS backup indicators** from actual returned password attributes. Visible expiration/version metadata is not proof that a password was backed up or that permissions are correct.
- For zero-record results, automatically expands **Details** and shows copyable, sanitized diagnostics: directory/DC, source, which LAPS backup indicators were visible, and targeted OU delegation / client policy / encrypted-password decrypt guidance.
- Entra empty responses get their own Graph/Entra permission and backup guidance instead of AD instructions.
- If LDAP returns encrypted LAPS records but none can be decrypted, the existing records and per-row error statuses remain visible with separate authorization guidance.
- No password-expiration timer starts when **zero readable passwords** were returned. Diagnostics remain available until the user clears the result or starts a new search.
- Extended LAPS regression tests for all combinations of AD metadata indicators, the Entra empty case, and secret-free diagnostic text.
- This release does **not** change Active Directory ACLs, enable ticket prompts, or enable any RBAC/JIT/two-person/SIEM enterprise policy by default.


## 0.25.1

- Fixed the **Start** layout regression visible in 0.25.0 where the large **LAPS Passwords** / **BitLocker Recovery** action buttons could be painted over the unified search-results grid at some window sizes or DPI/scaling combinations.
- Search results and selected-device actions now live in one nested responsive layout: the grid always occupies the first row and the action/status area always occupies the row below it.
- The selected-device action area stays hidden while no result is selected, so an empty search grid no longer contains floating action text.
- BitLocker/LAPS workspace actions are no longer enabled without a selected result; direct Reveal/Copy/Rotate/Device Details actions remain context-aware.
- Secret/clipboard lifetime information can keep the action area visible while sensitive data is active, then collapses again when the secret is cleared and no result is selected.
- Added a runtime UI regression check that verifies the Start result grid and action panel are in separate layout rows and that the action panel is hidden in the initial empty state.


## 0.25.0

- Reworked the recovery-access prompt so **Ticket / Reference is optional by default**. A fresh installation no longer interrupts BitLocker/LAPS reads with a ticket dialog; the dialog appears only when the helpdesk policy explicitly requires a reference.
- Decoupled the Intune rotation reminder from the ticket dialog. Optional BitLocker-key rotation is suggested **after** cloud recovery access instead of blocking the secret-read path.
- Added optional ticket-format validation, an example/placeholder, **Copy reference**, and session reuse of the same ticket/reason for the same computer.
- Added schema-5 migration for helpdesk-first defaults. Legacy ticket/rotation prompts are reset for non-enterprise configurations, while explicitly configured RBAC/JIT/approval/SIEM deployments are preserved.
- Confirmed enterprise recovery controls remain explicit opt-ins: **RBAC, JIT recovery, two-person approval, SIEM, mandatory ticket/reference, audit signing, Remote API, Coverage policy/service Coverage, storage ACL hardening and trusted-update-signature enforcement are disabled by default**.
- Expanded **Start** with a selected-computer helpdesk summary, explicit source, direct **Reveal BitLocker Key**, **Copy BitLocker Key**, **LAPS Passwords**, **Rotate via Intune**, **Device Details**, and **Clear secrets now** actions.
- Added visible secret/clipboard lifetime status. BitLocker/LAPS secrets use configurable in-memory expiration and sensitive clipboard copies show a countdown and can be cleared immediately.
- Added **Administration → Helpdesk** for normal settings: setup status, sanitized one-click support bundle, environment profiles, optional ticket policy, optional post-recovery rotation suggestion, and secret/clipboard timeouts.
- Added non-secret **environment profiles** for switching AD/DC/domain/LDAPS/recovery-source and Cloud tenant/client/certificate preferences without storing passwords, access tokens, BitLocker keys or LAPS passwords.
- Moved enterprise-only controls into **Administration → Advanced** and labeled RBAC/JIT/two-person/SIEM as optional policies rather than normal helpdesk requirements.
- Added **Health & Audit → Access Health**. It runs metadata-only checks for AD LDAP bind, BitLocker recovery-object visibility, AD LAPS, Microsoft Graph BitLocker metadata, Intune managed-device visibility and Entra LAPS without retrieving recovery passwords or LAPS passwords.
- Added searchable **Recovery Audit History** filters for computer, ticket/reference, user, action, Recovery ID, source and date range, plus session-ID copy for incident correlation.
- Added a one-click **Create Support Bundle** workflow that writes a sanitized diagnostics ZIP under the current user's BitKeyBridge data and excludes recovery passwords, LAPS passwords, tokens, protected credential blobs and private keys.
- Added updater **history, retry and one-click rollback** to the retained previous `.bak` executable. Install/rollback results record from/to version, automatic/manual mode, publisher and details.
- Updater status now displays release SHA-256 metadata. Added optional Authenticode trust enforcement and optional expected-publisher matching; these controls are **off by default** so unsigned existing releases are not blocked unless an administrator opts in.
- Refactored unified AD/Entra/Intune result merging so Cloud results survive an unavailable AD session, multiple recovery IDs are retained, and serial/UPN/Intune hits can still be enriched safely.
- Added regression coverage for non-enterprise defaults, schema migration, AD-unavailable + Cloud-available search, Graph 403 behavior, multiple BitLocker recovery IDs, missing Session AD credentials, LAPS history, updater N→N+1/rollback history, secret-free helpdesk profiles, and the new HiDPI/UI controls.


## 0.24.3

- Fixed the meaning of **automatic updates**. Previous builds only checked GitHub at GUI startup and never installed the discovered release automatically.
- Added `AutoInstallUpdatesOnStart` (enabled by default). When startup checks find a newer verified release, BitKeyBridge now downloads the architecture-specific ZIP, validates GitHub/SHA256 metadata, runs the staged EXE self-test, launches the elevated replacement helper, closes, and restarts the GUI on the new version.
- Startup update handling now runs **before** AD discovery and credential prompts, so an AD connection workflow cannot delay or hide a pending application update.
- Startup release checks have a 20-second bound. Network/GitHub failures are logged and the normal BitKeyBridge startup continues instead of being blocked.
- Automatic release checks/install are no longer gated by the BitKeyBridge Administration workspace. The updater is an application-maintenance path, not a helpdesk RBAC feature.
- Added an **Automatically install verified updates and restart** option under Administration → Updates. The status panel now shows startup-check and automatic-install state separately.
- Added CLI controls `--auto-update-status`, `--auto-update-enable`, and `--auto-update-disable`. Enabling automatic updates also enables startup update checks.
- A failure to write the optional machine-wide update-status cache no longer turns an otherwise successful GitHub release check into a false update failure.
- Added CI persistence checks for automatic-update preferences and a UI regression check for the automatic-install control.
- Bootstrap note: v0.24.2 can detect v0.24.3 but cannot install it automatically because that behavior did not exist yet. Use **Update Now** once (or install v0.24.3 manually); future releases can then install automatically.


## 0.24.2

- Fixed **Devices → Search AD + Cloud** so Entra/Intune search no longer fails when Active Directory uses **Session only** credentials but no session password is currently loaded. Cloud search can now run independently while AD is disconnected.
- Unified device search now skips AD cleanly when there is no active AD session, preserves Entra/Intune results, and reports whether results came from **AD + Entra + Intune**, **Entra + Intune**, or **AD only** instead of masking Cloud with an AD credential exception.
- If Microsoft Graph authentication does not complete and AD is also disconnected, Devices now shows an explicit source/authentication message rather than throwing the misleading `Session credential mode is selected` error.
- Fixed the tiny yellow control under **Save settings** in **Start → Advanced**. It is the AD credential-status surface (not a button); hidden WinForms layout could collapse its wrapping width before Advanced was opened.
- The credential-status surface is now labeled explicitly (`Credential status: ...`) and receives a stable control name for UI diagnostics.
- Added a runtime UI regression check so hidden Advanced layout cannot silently collapse the credential-status banner again.


## 0.24.1

- Fixed Start startup connection behavior. BitKeyBridge now attempts the configured AD connection on launch and opens the AD credential prompt when automatic connection cannot proceed or fails. When automatic connection is explicitly disabled, Start asks how to connect instead of silently remaining disconnected.
- Fixed Entra LAPS reads that could report **Entra returned an invalid LAPS response** even after successful Device Code authentication. The Graph request now explicitly asks for JSON, accepts UTF-8/UTF-16 BOM responses, retries one non-JSON success response once, and reports only safe HTTP metadata (status, content type, byte count and request ID). Response bodies, `passwordBase64` and decoded passwords are never written to diagnostics.
- Extended Entra LAPS parsing to accept both the normal `value: { ... }` wrapper and a defensive single-item `value: [ ... ]` wrapper.
- Fixed updater release-version validation so Windows file versions such as `0.24.0.0` are treated as the same release as `0.24.0`. The same normalized comparison is used both when staging an update and by the elevated apply helper.
- Added regression coverage for four-part updater file versions and Entra LAPS UTF-8/UTF-16 BOM responses.
- Added **Back to Start** buttons in Recovery and LAPS when those workspaces were opened from the Start dashboard. Direct tab navigation stays uncluttered.
- Replaced the plain Microsoft Entra Device Code message box with a dedicated dialog containing a selectable code, **Copy code**, **Open browser** and **OK**. Automatic clipboard copy and browser launch remain, with explicit retry buttons when either fails.
- Upgrade note: the updater bug exists in 0.24.0 itself, so 0.24.0 may be unable to bootstrap the updater fix automatically. If that happens, install 0.24.1 once from the release ZIP/EXE; subsequent updates use the corrected comparison.

## 0.24.0

- Reworked **Start** into the primary helpdesk dashboard. One metadata-only search now checks both **BitLocker** and **LAPS** by partial computer name, Recovery ID or device/object ID, with the existing 450 ms debounce and immediate **Enter** search.
- Start search results are shown in one sortable DataGridView with computer name, BitLocker status/date and LAPS status/metadata date. A single result is selected automatically.
- Start now uses human-readable states instead of raw implementation details: **Available**, **Multiple keys**, **Old key**, **No backup**, **Detected** and **Expired**. Secret-read failures continue to surface as **Access denied**, **Decrypt denied**, **Invalid data** or **No backup** when a real LAPS read is attempted.
- Start action buttons are context-aware: **BitLocker Recovery** is disabled when the selected computer has no recovery-key metadata, and **LAPS Passwords** is disabled when no LAPS backup indicator is present. Recovery-ID-like input prefers the BitLocker path.
- Added a user-scoped **Recent computers** list. It stores only computer/device identifiers, last action and timestamp in LocalAppData; BitLocker keys and LAPS passwords are never stored. Successful direct BitLocker Reveal/Copy and LAPS reads also update Recent.
- Moved Active Directory connection ownership out of Recovery and onto **Start**. Recovery now keeps only its task-specific source, OU and search controls plus a **Connection on Start** shortcut; DC/FQDN, credentials, Connect/Disconnect, port, LDAPS and vault settings are centralized on Start.
- Simplified the normal credential choice on Start to **Use current Windows account** or **Use another AD account**. DC/FQDN, domain, port, LDAPS, credential-storage mode and vault actions remain available under **Advanced**.
- Added a persistent bottom connection/status bar showing AD session state and the running BitKeyBridge version. Clicking the connection status returns to Start and expands connection settings.
- LAPS technical details are collapsed by default behind **Details...** and automatically expand only for read/decrypt/data failures. Normal successful reads stay compact.
- Unified keyboard flow: Enter searches immediately, Enter/double-click on a selected LAPS device reads LAPS, and a BitLocker search for one computer automatically selects its latest recovery record without revealing the secret.
- LAPS metadata discovery now carries the non-secret expiration timestamp when Active Directory exposes it. Start labels this field **LAPS metadata date** so expiration is never presented as a password-change timestamp.
- Missing LAPS history is summarized as **History unavailable (disabled, not retained, or not readable)** instead of a long generic diagnostic paragraph.
- Disconnect now cancels the unified Start search, clears its result set and disables helpdesk actions consistently.
- Extended runtime UI/HiDPI self-tests to require the unified Start search, Recent list, simplified credential choices, Start-owned Advanced settings and global status bar.

## 0.23.0

- Added a new **Start** workspace as the default GUI landing page. BitKeyBridge now opens on connection state first instead of immediately dropping the operator into Recovery or LAPS.
- The Start page shows the shared Active Directory session status, connection progress, **Connect to AD**, **Disconnect**, and **Connection settings...** actions.
- After a successful AD bind, the Start page enables two primary helpdesk actions: **BitLocker Recovery** and **LAPS Passwords**. These navigate directly to the corresponding workspace and focus the search box.
- Start-page task buttons are disabled while disconnected or while a connection attempt is in progress, so the normal workflow is explicitly **connect first → choose BitLocker or LAPS**.
- Automatic startup connection now runs from the Start workflow regardless of the previously selected Recovery source. It uses the current Windows identity or configured protected credentials without prompting.
- If explicit Session credential mode is selected but no session password is loaded, startup does not display a password dialog automatically; the Start page reports that credentials are required and **Connect to AD** opens the normal credential prompt.
- Entering BitLocker from Start switches Recovery to **Live AD**; entering LAPS from Start switches LAPS to the **Active Directory** source. The existing top-level tabs remain available for Local cache, Entra-only, Devices, Administration and Health/Audit scenarios.
- The same connection state is synchronized between Start and the existing Recovery connection surface; disconnects, failures, progress and successful binds update both places.
- Added UI self-test enforcement that Start is the first/default workspace and that its connection and BitLocker/LAPS controls are present.
- Includes the 0.22.1 LAPS workflow cleanup: Enter/double-click reads the selected LAPS device, successful details omit empty/redundant fields, one-row reads auto-select, and expired current credentials are marked **Available / Expired**.

## 0.22.1

- Simplified the normal LAPS workflow: **Check access** is no longer shown as a primary action. Live search remains metadata-only; after selecting a computer/device, **Enter** or double-click reads LAPS immediately. Pressing Enter on an exact/single search result also proceeds directly to **Read LAPS**.
- The Search button remains metadata-only, so browsing candidates never reads a password.
- A single returned LAPS password row is selected automatically after a successful read; Reveal/Copy become ready without requiring an extra row click, while the password remains masked.
- Cleaned successful LAPS details: empty **Account SID**, **Password version**, and device/object ID fields are omitted, and the generic Legacy/history permissions paragraph is no longer shown after every successful read.
- When **Include password history** is enabled but no history is returned, the status now says so briefly instead of displaying a long generic explanation.
- Current LAPS credentials whose expiration timestamp is already in the past are clearly shown as **Available / Expired**, with an explicit warning that password rotation may be overdue. The password remains readable/copyable because expiry and read/decrypt availability are separate conditions.
- Redundant `Password read successfully` text is suppressed when the row already shows **Available**.
- Existing AD access diagnostics remain implemented for troubleshooting, while **AD access help...** remains the visible help path.

## 0.22.0

- Added debounced live search to both **Recovery / BitLocker** and **LAPS**. Typing two or more characters queues a 450 ms metadata search; **Enter** or the Search button runs immediately.
- BitLocker search now always combines partial computer-name matching with partial Recovery ID matching. Recovery ID fragments are normalized so braces, hyphens and case do not matter.
- Replaced BitLocker search results with a sortable **DataGridView** showing **Computer**, **Recovery ID**, **Key date**, **Latest**, and **Source**. Live AD results identify the newest recovery object for each computer; legacy CSV cache rows keep **Latest** unknown when the export does not contain object-creation metadata.
- Added a separate safe LAPS device-discovery stage. AD search supports partial computer names, exact object GUIDs and sufficiently long object-GUID fragments; Entra search supports partial device names and device-ID fragments.
- LAPS discovery reads metadata only. AD search does not request password-bearing LAPS attributes, and Entra search uses deviceLocalCredentials metadata without requesting the `credentials` collection.
- Added sortable **DataGridView** results to LAPS discovery and LAPS password/history records. LAPS key rows show **Key date** and **Latest**; current rows are marked latest and history rows are not.
- Entra LAPS discovery populates key date from `lastBackupDateTime` / `refreshDateTime` when available. AD LAPS discovery does not invent a key date when the schema exposes only expiration/version metadata.
- Added shared HiDPI-aware DataGridView styling and regression checks for debounce settings and punctuation-insensitive identifier matching.
- Verified x64/x86/ARM64 builds, x64 offline self-test, LAPS regression tests and runtime UI layout/HiDPI self-test before release.

## 0.21.3

- Moved the verified updater out of **Administration → Security & Settings** into its own **Administration → Updates** tab.
- Added explicit **Check for Updates**, **Update Now**, **Save Update Settings**, **Enable/Disable Automatic Checks**, and **Open Release Page** actions.
- Disabling automatic checks now affects only the GUI-start check; manual update checks and manual updates remain available.
- Split updater persistence from generic Operations settings so an update check no longer saves unrelated Remote API or helpdesk settings.
- Added CLI controls `--update-checks-status`, `--update-checks-enable`, and `--update-checks-disable` alongside existing `--check-update` and `--update`.
- Added a post-release x64 updater smoke test that checks published GitHub release metadata, persists automatic-check disable/enable state, and exercises the safe current-version `--update` no-op path.
- Existing updater verification remains in place: architecture-specific packages, SHA-256, GitHub asset digest when available, exact staged EXE version, staged `--self-test`, hashed elevated apply plan, rollback, and x64/x86/ARM64 publishing.

## 0.21.2

- Fixed **LAPS → Check access** incorrectly reporting Legacy/Windows LAPS schema as `NotDetected` when schema inspection had not actually run. RootDSE now requests `schemaNamingContext`, with a safe `CN=Schema,<configurationNamingContext>` fallback.
- Expanded the secret-free schema probe to include the Legacy expiration attribute and Windows LAPS metadata/version attributes. Existing non-secret computer metadata can positively confirm the corresponding LAPS schema even if the separate schema lookup is unavailable.
- A failed or unavailable schema inspection now produces `NotProbed` for schema absence instead of a false negative. A real `NotDetected` result is emitted only after a completed schema query.
- Added portable regression checks for the `Available` / `NotDetected` / `NotProbed` decision logic. The safe access check still never requests a Legacy/plaintext password attribute and does not decrypt an encrypted LAPS value.

## 0.21.1

- Added CI enforcement that every semantic-version heading in `CHANGELOG.md` is unique. The build now fails when a version appears more than once, preventing the duplicate-section problem that previously affected 0.18.4.
- Repaired the CHANGELOG validation workflow itself, restored the known-good workflow structure, and kept the uniqueness check in the normal Windows build matrix before compilation.
- Includes the complete 0.21 feature set: Intune / Entra Setup Wizard, RBAC Setup Wizard, current README/documentation, deprecated ROPC labeling, scoped AD access troubleshooting for BitLocker/LAPS/history, and the new troubleshooting regression/UI tests.
- Runtime remains native C#/.NET with no PowerShell execution; PowerShell snippets shown in AD access troubleshooting are administrator-side examples only and are never executed by BitKeyBridge.

## 0.21.0

- Added a guided **Intune / Entra Setup Wizard** as the recommended first-run cloud path. It checks local administrator prerequisites, captures tenant selection, explains the exact Microsoft Graph capabilities, supports optional Entra LAPS, and reuses the native Device Code setup engine to create/repair the App Registration and Enterprise Application, grant tenant-wide consent, and create/reuse a LocalMachine certificate.
- The cloud wizard performs non-destructive post-setup verification with certificate authentication: required app-only roles are checked directly from the issued access token, BitLocker metadata access is tested, and the Intune managed-device endpoint is queried without rotating any key. Optional DeviceLocalCredential.Read.All is also verified in the token when Entra LAPS is selected; no LAPS password is requested.
- Added a guided **RBAC Setup Wizard** for Recovery Readers, Intune Rotation Operators, BitKeyBridge Administrators, local-Administrators bypass, and RBAC enable/disable state. The wizard can add the current Windows identity to selected roles, validates every configured Windows principal, previews effective access, and blocks an obvious current-admin lockout before saving.
- RBAC policy defaults remain unchanged: the wizard does not silently enable RBAC, JIT recovery, two-person approval, or SIEM. Changes are committed only after **Finish**; advanced RBAC and Privileged Access dialogs remain available.
- Added both setup wizards to compact, 150% large-text and 200% UI layout self-tests. The existing native C#/.NET, no-PowerShell architecture and x64/x86/ARM64 release model remain unchanged.
- Added **AD access troubleshooting** in Recovery and LAPS with scoped command templates for BitLocker `msFVE-RecoveryPassword`, Windows LAPS, and Legacy Microsoft LAPS. The guidance explicitly separates Windows LAPS read ACL, DPAPI-NG decrypt authorization (`ADPasswordEncryptionPrincipal`), and encrypted-history retention (`ADEncryptedPasswordHistorySize`).
- Added `docs/ACCESS-TROUBLESHOOTING.md` and contextual hints when BitLocker/LAPS secret retrieval or encrypted LAPS decryption fails. Runtime remains PowerShell-free; PowerShell snippets shown by the troubleshooting UI are optional administrator-side delegation commands only.
- Marked Username + Password / ROPC as a **deprecated compatibility mode** in the Cloud UI and documentation because it doesn't support MFA and is commonly blocked by Conditional Access. Device Code and certificate authentication are the recommended modes.
- Cleaned the release documentation: README now describes the current 0.21.x BitLocker/LAPS/wizard feature set, and the duplicate 0.18.4 CHANGELOG section was merged into one canonical entry.
- Added official Microsoft reference links to the AD access troubleshooting guide and release-tested the new wizard/troubleshooting surfaces through the existing build, security and UI validation pipeline.

## 0.20.0

- Completed the native **BitLocker + LAPS** helpdesk workflow across Active Directory, Microsoft Entra ID and Intune without PowerShell, shell fallbacks, the ActiveDirectory PowerShell module or Microsoft Graph PowerShell SDK.
- Extended the LAPS workspace for Legacy Microsoft LAPS, Windows LAPS plaintext/encrypted current passwords, encrypted password history, DSRM current/history and Entra LAPS with **All / Current / History** views, account names, password age, expiry, per-record read/decryption status, **Copy account**, and Reveal/Copy only for successfully available secrets.
- Added secret-free **Check access** diagnostics. AD checks LDAP bind, computer resolution, LAPS schema/history and non-secret backup/version metadata without requesting password-bearing attributes; Entra checks DeviceLocalCredential.Read.All and backup metadata without requesting the credentials collection. Secret-read and DPAPI-NG decrypt authorization are reported as **NotProbed** when they cannot be verified safely without touching a real secret.
- Standardized **Cancel + progress + non-blocking diagnostics** across Active Directory connection, BitLocker Live AD search, Devices search, LAPS reads, Microsoft Graph connection, DC discovery/comparison, Export/Dry Run, Coverage, Entra First-Run / Repair and Entra certificate rollover. Slow native LDAP work runs off the UI thread and stale results are discarded after cancellation.
- Replaced blocking application error MessageBoxes with shared secret-safe **UiDiagnosticPanel** surfaces. Modal dialogs remain only where the operator must make an explicit decision or complete Device Code authentication. CI rejects blocking error MessageBoxes.
- Removed silent empty catch blocks from application paths and added CI enforcement that rejects future empty catches. Non-fatal cleanup/fallback failures now emit sanitized Debug/Event Log diagnostics.
- Hardened recovery-secret clipboard handling with Windows clipboard-history/cloud-processing exclusion formats. BitLocker/LAPS secrets are cleared on timeout only if the clipboard still contains the same tracked secret, preserving unrelated clipboard content.
- Improved manual AD connection UX: successful status includes DC, resolved domain, effective identity and LDAP sign/seal or LDAPS port. **Disconnect / Forget session** cancels pending work and clears only in-memory session credentials and loaded Recovery/LAPS state without deleting Credential Manager or machine-DPAPI credentials.
- Removed obsolete SYSVOL/NETLOGON/BL export assumptions and the retired SysvolScriptsRoot setting from current configuration/documentation. The normal local default remains `%ProgramData%\BitKeyBridge\RecoveryExport`, with optional local/UNC output configuration.
- Strengthened CI/regression coverage for LAPS codecs/lifetime, diagnostic redaction, secure clipboard formats, x64/x86/ARM64 self-contained builds, x64 offline self-test, CodeQL, and compact/large-text/200% WinForms layout validation.
- **RBAC/JIT/two-person/SIEM defaults are intentionally unchanged.** RBAC remains opt-in while the secure-by-default policy design is considered separately.

## 0.19.3

- Added **Cancel**, marquee progress, and secret-safe inline diagnostics to **Discover and Test DCs**. The existing CancellationToken is now wired from the GUI through domain-controller comparison.
- Removed silent exception swallowing from the updated UI/AD paths. Non-fatal cleanup, status, DNS/DC metadata, clipboard, update-status, coverage-status, audit-checkpoint, and Event Log failures now emit sanitized diagnostics instead of disappearing.
- Improved Device Code feedback so BitKeyBridge no longer claims the code was copied or the browser opened when either operation failed.
- Domain-controller discovery metadata failures are logged safely while allowing the comparison to continue with partial information.
- Preserved the v0.19.2 cancellable Recovery/LAPS flows, non-blocking diagnostic panels, LAPS-secret redaction, and the PowerShell-free native C#/.NET implementation.


## 0.19.2

- Added a shared non-blocking diagnostics panel with **Copy diagnostics** and **Dismiss** actions. Copied data is sanitized and does not include stack traces, raw LDAP/Graph payloads, recovery keys, tokens, or LAPS password values.
- Recovery **Connect to AD** now has an explicit **Cancel** button and marquee progress indicator. Cancellation returns the GUI immediately even while a synchronous native LDAP bind finishes on its worker thread; stale results are discarded.
- Active Directory connection and OU-discovery failures no longer open blocking error MessageBoxes in the Recovery workflow. The reason and safe connection context are shown inline instead.
- LAPS reads now show an explicit marquee progress indicator and non-blocking diagnostics. AD-LAPS cancellation releases the GUI immediately instead of waiting for the LDAP timeout; Graph cancellation continues through CancellationToken.
- Added explicit LAPS JSON/password redaction rules to the shared diagnostic sanitizer and regression coverage that fails if synthetic LAPS secrets survive sanitization.
- Preserved the PowerShell-free implementation: C#/.NET, native LDAP, Win32/DPAPI-NG, Windows impersonation, and Microsoft Graph only.


## 0.19.1

- Added an explicit **Cancel** button to the LAPS workspace for in-flight Active Directory and Microsoft Graph reads.
- While a LAPS read is running, source/query/history controls are temporarily disabled to prevent connection identity or target changes mid-operation; they are restored when the operation completes or is canceled.
- Cancellation now reports a non-blocking `LAPS read canceled` status and uses the existing `CancellationToken` path for Graph calls and for discarding pending LDAP results safely.
- Preserved the v0.19.0 native implementation: no PowerShell process, module, SDK, or fallback is used.


## 0.19.0

- Added a native LAPS workspace for Legacy Microsoft LAPS, Windows LAPS plaintext/encrypted passwords, encrypted AD history, DSRM passwords/history, and Microsoft Entra LAPS credentials/history.
- Added native DPAPI-NG decryption with strict 16-byte Windows LAPS header validation, per-record access/decryption statuses, and UTF-16LE/UTF-8 JSON parsing. The explicit AD connection account is used for domain key retrieval through a bounded Windows impersonation scope.
- AD lookup accepts an exact computer name, DNS name, SAM account name, object GUID or computer DN. Cloud lookup accepts an exact Entra device name or device ID and rejects ambiguous names.
- Password history is sorted by UTC update/backup time rather than LDAP/Graph enumeration order. Windows Server 2025 logical password-version GUIDs are displayed when available.
- Read, reveal and copy operations use existing RecoveryRead RBAC, optional JIT/two-person/SIEM policies and secret-free audit records. LAPS passwords are kept out of CSV exports, settings, diagnostics and serialized models.
- Added timed password clearing, explicit clearing, clipboard clearing, disposal of managed secret buffers and zeroing of native decrypted buffers. Source, computer, connection or account changes discard loaded LAPS results and cancel pending reads.
- Connect to AD and Discover DC now open the manual credential dialog regardless of the configured storage mode. The dialog accepts domain/DC/user/password, defaults to manual credentials, and offers an explicit current-Windows-account option; Cancel preserves the previous credentials.
- Explicit DC domain resolution now takes precedence over the workstation's joined domain. Direct connection tests report rejected credentials instead of hiding bind errors behind generic RootDSE failures.
- Added opt-in DeviceLocalCredential.Read.All provisioning in native First-Run / Repair setup. LAPS cloud authentication requests LAPS/device-read scopes independently of BitLocker/Intune write scopes.
- Added portable LAPS regression checks and Windows offline DPAPI-NG roundtrip/tamper tests. Existing HiDPI/large-text/UI checks include the new workspace and expanded credential dialog.
- Preserved the v0.18.9 shared DPI-aware ListView improvements. Release packages remain self-contained C#/.NET single-file builds for x64, x86 and ARM64; application runtime does not use PowerShell.

## 0.18.9

- Added shared DPI-aware `ListView` column management through `UiStyle.ConfigureListViewColumns`.
- Migrated all six details tables — Recovery results, unified Devices, Export scope results, Coverage, DC comparison, and Audit — to the shared logical-width column path.
- Column widths are authored at the 96-DPI logical baseline and recalculated for the active monitor DPI instead of remaining fixed physical pixels.
- Enlarged system text is included in the minimum header-width calculation, preventing column captions from being clipped at 150% / 200% large-text settings.
- User column resizing is preserved as logical width, so moving the application between monitors with different scaling keeps the intended proportions.
- `DpiAwareForm` refreshes configured ListView columns during the normal per-monitor DPI/font relayout path.
- Runtime UI self-test now rejects any visible ListView that bypasses the shared configuration and verifies every header remains wide enough for its caption.
- Added direct 100% / 150% / 200% scaling and logical-width round-trip regression checks for ListView columns.
- CI now rejects direct `ListView.Columns.Add` and `ColumnHeader` construction outside `UiStyle`.
- Updated the shared UI standard with the ListView/table-column rules.
- Release builds remain self-contained single-file packages for win-x64, win-x86 and win-arm64.


## 0.18.8

- Added `UiStyle.CreateActionButton(...)` as the centralized factory for WinForms action/dialog buttons. AutoSize, minimum dimensions, padding, and Windows visual-style behavior now come from the shared UI layer.
- Migrated dialog and workspace action buttons away from per-window `new Button` construction, including AD credentials, input prompts, OU browser, Recovery access, incident verification, secure output, secret display, Cloud/Graph administration, credential management, unified device search, Remote API scoped tokens, RBAC, and privileged-access workflows.
- Added CI enforcement that rejects direct `new Button` construction anywhere in the UI layer outside `UiStyle`.
- Extended runtime UI validation to require shared AutoSize behavior and the common minimum button height for every visible button.
- Standardized modal keyboard behavior: Enter executes the primary action and Escape executes Cancel/Close where the dialog has a clear primary action.
- Migrated RBAC and Remote API scoped-token state displays to shared `UiStatusLabel` surfaces with semantic Success/Error/Neutral states and accessibility metadata.
- Migrated JIT recovery, two-person approval, and SIEM state displays in Privileged Access to the shared status system; failed actions now immediately replace stale healthy status with Error state.
- Migrated the remaining Recovery workflow/OU status labels to shared inline `UiStatusLabel` styling. Search, connection, OU selection, empty-result, and failure states now use semantic Busy/Success/Warning/Error feedback without adding heavy framed panels.
- `Select / Change OU` and `Search BitLocker` now use the centralized button sizing/style path.
- Existing compact, 150%, 200%, High Contrast, accessibility, and DPI-safe glyph runtime validation remains enabled.
- Release builds remain self-contained single-file packages for win-x64, win-x86 and win-arm64.


## 0.18.7

- Extended the shared `UiStatusLabel` visual system beyond Recovery to Cloud/Graph, unified device search, coverage, software updates, Remote API, credential vault, service health, machine cloud configuration, service identity, and audit signing.
- Added semantic status states throughout those workflows: Busy for in-progress work, Success for healthy/completed operations, Warning for partial or missing configuration, Error for failures, and Neutral for informational/disabled states.
- Added `UiStyle.SetStatus(...)` so visible text, semantic state, glyph, colors, and accessibility description update together.
- Status state is now intrinsic to `UiStatusLabel`; the previous external weak-table state tracking was removed.
- Accessibility descriptions now refresh whenever status text changes, preventing stale screen-reader descriptions.
- Added Windows High Contrast support: status surfaces use system Window/WindowText colors while retaining distinct DPI-safe vector glyph shapes.
- Added deterministic High Contrast palette validation to `--ui-self-test`.
- Runtime UI validation now checks every visible shared status surface for a DPI-safe glyph, keyboard exclusion, accessibility name/description, and stale accessibility text.
- Existing compact, 150%, and 200% layout validation remains enabled.
- Release builds remain self-contained single-file packages for win-x64, win-x86 and win-arm64.


## 0.18.6

- Added shared DPI-safe vector status glyphs for neutral/info, connecting, success, warning, and error states. Glyphs are drawn at the target monitor DPI instead of scaling fixed PNG resources, so they remain sharp on HiDPI/4K displays.
- Extended `UiStyle` status handling to centralize border, spacing, text/image relation, colors, glyphs, keyboard behavior, and accessibility metadata.
- Status glyphs are refreshed automatically during the shared per-monitor DPI relayout path when windows move between displays with different scaling.
- Recovery connection status now uses the shared status component instead of local per-window styling.
- Added explicit keyboard/accessibility metadata to the Recovery connection bar: Source -> Connect to AD -> Advanced settings, while the informational status row is excluded from tab navigation.
- Added runtime assertions for status glyph presence, accessibility metadata, balanced connection controls, and keyboard order.
- Added a 200% large-text/layout stress scenario to `--ui-self-test` in addition to compact and 150% scenarios.
- Added a direct 96-DPI vs 192-DPI vector-glyph scaling check to prevent blurry fixed-size status graphics from being introduced later.
- Release builds remain self-contained single-file packages for win-x64, win-x86 and win-arm64.


## 0.18.5

- Reworked the Recovery connection bar so **Source**, **Connect to AD**, **Advanced connection settings**, and AD connection status have a stable responsive layout. The long connection status now occupies its own full-width status row instead of being squeezed between controls.
- Added shared visual status states for neutral, connecting, success, warning, and error conditions using the common `UiStyle` palette.
- Fixed interactive Live AD connection with explicit **Session** credentials. When no session password is loaded, **Connect to AD** now opens a credential dialog instead of failing with “Session credential mode is selected, but no session password is loaded.” The password remains session-only and is not written to `appsettings.json`.
- The same session-credential prompt is used before GUI **Discover DC**, so DC comparison no longer fails merely because the session password has not yet been loaded.
- Fixed DC comparison with no configured OU scopes. BitKeyBridge now resolves `defaultNamingContext` and automatically uses an **Entire domain** scope instead of throwing “No OU scopes are configured.”
- Rebuilt Recovery and Devices recovery-key controls so long key/ID fields use the available width and action buttons wrap on compact, RDP, HiDPI and large-text layouts instead of relying on fixed 470/410/330-pixel fields.
- Removed the remaining fixed 1040-pixel administrative form grid width.
- Expanded the shared typography system: body, dialog-title, page-title, section-title, emphasis and monospace fonts are now defined centrally in `UiStyle`; remaining dialogs and main workspaces were migrated away from direct Segoe UI / Consolas construction.
- Added `--ui-self-test`, which constructs the real WinForms UI without displaying it and validates responsive layout invariants in compact and 150% large-text scenarios.
- GitHub Actions now runs the runtime UI self-test on win-x64 and rejects direct Segoe UI / Consolas construction outside `UiStyle`, in addition to the existing PerMonitorV2, DPI scaling and fixed-position checks.
- CI concurrency is now bound to the exact commit SHA so delayed/out-of-order pull-request events cannot cancel validation for the current PR head.
- Release builds remain self-contained single-file packages for win-x64, win-x86 and win-arm64.


## 0.18.4

- Added a shared WinForms UI standard and `UiStyle` baseline for consistent typography, spacing, buttons, administrative workspaces, and machine-oriented monospace fields.
- Strengthened HiDPI behavior for 4K, mixed-DPI monitors, RDP, large text, and 100-200% Windows scaling. All forms inherit the shared DPI-aware base, horizontal action rows wrap automatically, windows are constrained to the active monitor work area, and responsive admin sections follow the available client width instead of a fixed workspace.
- Added CI guards for `PerMonitorV2`, `AutoScaleMode.Dpi`, `DpiAwareForm` inheritance, fixed-position UI, fixed button dimensions, and centralized font construction.
- Centralized diagnostic secret redaction across service logs, Windows Event Log, diagnostics bundles, SIEM metadata/status, and diagnostics error files. Diagnostics bundles also strip Remote API token hashes and redact SIEM webhook path/query material.
- Hardened SIEM webhook delivery by disabling automatic HTTP redirects so metadata is never forwarded to an unexpected redirect target.
- Hardened Remote API bearer handling: decoded tokens must be exactly 256 bits, duplicate Authorization headers are rejected, scoped-token changes are transactional, and enabled Remote API configuration fails closed when TLS/token-hash requirements are invalid.
- Hardened self-update release trust: release page and asset URLs are bound to the configured GitHub repository/tag/name, redirects must remain on trusted HTTPS GitHub hosts, declared asset sizes are enforced, `SHA256SUMS.txt` metadata/digest is validated, checksum entries are required when published, and GitHub asset digest/checksum sources must agree.
- Update installation revalidates release metadata immediately before download, requires exactly one root-level `BitKeyBridge.exe`, requires the staged EXE version to exactly match the selected release, and repeats the exact version check in the elevated updater before replacement.
- Added offline regression coverage for diagnostic redaction, GitHub release URL/digest validation, Remote API token-hash validation, and tamper-resistant update-plan checks.
- Release builds remain self-contained single-file win-x64, win-x86 and win-arm64 packages.

## 0.18.3

- Hardened Machine / Service AD credential storage for gMSA and regular domain service identities. The DPAPI credential file and Secrets directory now preserve the installed service identity's required read access instead of resetting to SYSTEM/Administrators only.
- Added explicit Machine credential ACL status, grant, revoke, and repair logic for Windows Service identities.
- Credential-vault metadata no longer materializes stored passwords as managed strings: Credential Manager metadata reads only the username, while Machine DPAPI metadata parses only non-secret JSON fields and zeroes temporary plaintext buffers.
- Reworked Windows Service identity changes as a preflight/commit transaction. Required Graph, audit-signing, Remote API, SIEM mTLS certificate ACLs, Machine AD credential ACL, and protected-storage ACLs are prepared before SCM identity changes.
- Certificate service-access preflight now fails if the configured certificate's private-key file is missing, including for LocalSystem where no extra read ACE is normally required.
- If a service-identity change fails before the SCM commit, newly granted BitKeyBridge ACLs, protected-storage changes, and persisted identity settings are rolled back. Post-commit failures retain the new identity's required access for repair rather than revoking it underneath the service.
- Successful service-identity changes remove BitKeyBridge-style certificate and Machine credential read ACLs from the previous non-LocalSystem service identity.
- Service uninstall now performs best-effort cleanup of managed certificate, Machine credential, and protected-storage ACLs for the removed service identity after the service is successfully deleted.
- Added unified **Repair Service Access** in the GUI and `--service-access-repair` in CLI to repair all configured certificate private-key ACLs, Machine DPAPI credential access, and protected storage for the current service identity.
- `--vault-status` and GUI vault status now report Machine credential access for the installed service account without exposing plaintext credentials.
- AD Session credentials now use a zeroable internal character buffer instead of a long-lived static immutable password string.
- Legacy Graph Password/ROPC input is cleared immediately after each authentication attempt.
- Cached Graph tokens are bound to the current Tenant / Client / authentication context; changing cloud authentication context forces a new token instead of reusing a still-valid token from the previous configuration.
- Health and `/health/security` now report aggregate Windows Service access status and Machine AD credential ACL status, surfacing access drift as a health error.
- Added offline regression tests for session credential clearing, Graph access-token non-serialization, and service certificate dependency collection including SIEM mTLS.

## 0.18.2

- Changed critical `appsettings.json` loading to fail closed: corrupt, unreadable, or invalid application configuration no longer silently falls back to default settings that could disable configured RBAC, JIT, approval, SIEM, or other security controls.
- Added startup diagnostics for configuration-load failures in the Windows Event Log plus a clear GUI/CLI/service refusal instead of continuing with implicit defaults.
- Application configuration is now validated on normal load and before save; legacy schema migration validates the effective configuration before overwriting the migrated file.
- Preserved the internal elevated updater path even when application configuration is damaged, so a verified repair/update is not blocked by the fail-closed startup rule.
- Hardened the TLS Remote API against pre-authentication resource exhaustion with a 15-second TLS/header deadline, a 32-client active connection cap, bounded request/header lines, total-header limits, and a strict header-count limit.
- Added bounded ASCII HTTP-line parsing and offline regression tests for normal and oversized request lines.
- Hardened verified self-update against verify-to-apply tampering: the staged executable is hashed before and after self-test, the elevated update plan is SHA-256-bound to the helper command line, and the elevated helper revalidates both staged executable hash and version immediately before replacement.
- The elevated updater now copies from a read-locked verified staged executable and keeps the helper binary non-writable through process creation.
- Added offline regression tests for missing/corrupt/invalid application configuration and tampered elevated update plans.

## 0.18.1

- Hardened the helpdesk Recovery workflow after the v0.18 UI review.
- Remembered Recovery OUs are now validated against the connected Active Directory naming context before reuse; stale cross-domain scopes are discarded and replaced with the safe `Entire domain` fallback.
- If a previously saved/selected OU is deleted, renamed, or otherwise stops resolving during live recovery search, BitKeyBridge retries once against `Entire domain` instead of leaving the operator at a dead scope.
- Switching between `Live AD` and `Local cache` now clears prior results, selected recovery metadata, in-memory recovery secret state, cached recovery-access context, and any BitLocker key currently tracked in the clipboard.
- `Local cache` search is now disabled when the recovery export CSV is missing, and an explicit cache-unavailable message replaces the misleading zero-results state.
- Clipboard cleanup now tracks the exact copied BitLocker secret independently from the currently selected row, so changing selection or closing the application cannot leave an older copied recovery key behind.
- Added an offline self-test for remembered-scope naming-context validation.
- Refined GitHub Actions concurrency so superseded PR builds cancel each other while `main`/release runs remain isolated by commit SHA and cannot block newer releases.

## 0.18.0

- Rebuilt every remaining WinForms dialog under `src/BitKeyBridge/UI` with responsive `TableLayoutPanel` / `FlowLayoutPanel` layouts instead of fixed `Left`, `Top`, or `SetBounds` coordinates.
- Updated Input, OU Browser, Recovery Access, Recovery Incident Verification, Remote API Scoped Tokens, Secret Display, Secure Output, RBAC, Privileged Recovery Access, Housekeeping / Protected Storage, and Coverage Policy dialogs for DPI scaling, large text, RDP, and smaller displays.
- Added a CI guard that rejects reintroduction of fixed-position WinForms layout in the UI directory.
- Scoped build-workflow concurrency by commit SHA so a stale architecture publish cannot block a newer release run.
- Recovery startup now falls back to `Entire domain` when no OU is selected, preserving a working metadata search path instead of leaving Search disabled.
- If OU enumeration fails after the Active Directory naming context is known, Recovery continues with the entire-domain scope and surfaces the OU-enumeration warning without blocking recovery search.
- The selected/default recovery scope continues to be persisted through the existing recovery UI state settings.
- Release packages remain self-contained single-file builds for win-x64, win-x86, and win-arm64 with SHA-256 checksums, CycloneDX SBOM, provenance/SBOM attestations, offline self-test, and CodeQL.

## 0.17.1

- Published a fresh patch build from the completed v0.17 UI and recovery workflow.
- Retained the four-section role-aware GUI: Recovery, Devices, Administration, and Health & Audit.
- Retained metadata-only BitLocker search with on-demand recovery-key retrieval and the unified Live AD / local-cache recovery workspace.
- Retained responsive WinForms layouts, administrator RBAC, unified AD + Entra + Intune device search, and the v0.17 security/audit behavior.
- Release packages remain self-contained single-file builds for win-x64, win-x86, and win-arm64 with SHA-256 checksums, CycloneDX SBOM, and attestations.

## 0.17.0

- Replaced the ten top-level GUI tabs with a role-aware four-section shell: **Recovery**, **Devices**, **Administration**, and **Health & Audit**.
- Helpdesk users see only Recovery and Devices; local Windows Administrators and principals in the new `RbacAdministrators` list also see Administration and Health & Audit.
- Added `--rbac-ui-admin-add` and `--rbac-ui-admin-remove` for assigning the Administration UI role from CLI.
- Reworked live AD recovery search to be metadata-only. Normal search no longer reads or holds `msFVE-RecoveryPassword`; the selected recovery object is read only after an explicit Reveal or Copy action passes RBAC/JIT/approval checks.
- Added server-side LDAP filtering for partial Recovery ID lookup so large OU searches no longer enumerate all recovery metadata client-side.
- Added metadata-only local-cache search and exact on-demand CSV recovery-password retrieval, so Live AD and Local cache are now two sources in the same Recovery workspace.
- Removed the separate top-level Recovery Search page and its duplicate key-handling code.
- Added a selected-recovery card. Single-result searches collapse directly to the card with Computer, scope/OU, Recovery ID, time, source, and an explicit **Reveal Recovery Key** action.
- Added automatic AD connection on GUI start, enabled by default, plus persisted last recovery OU and recovery-search source.
- Rebuilt the primary Recovery and Devices workspaces with `TableLayoutPanel` / `FlowLayoutPanel` so they respond cleanly to DPI, text scaling, RDP, and narrower windows.
- Unified device inventory and Entra/Intune recovery operations in **Devices**. AD-only search remains available when Graph is not configured or temporarily unavailable.
- Moved Microsoft Graph/App Registration configuration to **Administration → Cloud**.
- Moved Export, Service, DC comparison, Coverage, Operations, and Audit away from the top level into role-appropriate nested Administration / Health & Audit pages.
- Consolidated Open CSV/Log/Folder/Event Log/Release shortcuts into a **Tools** menu and removed the duplicate buttons from active admin pages.
- Removed obsolete duplicate Directory Connection, Local Recovery Search, Unified Devices, and Cloud Search page builders and handlers from `MainForm`.
- Added schema v4 for helpdesk UI state and the BitKeyBridge administrator role; older configuration is migrated with the existing backup/migration mechanism.
- Added self-tests for schema-v4 defaults, metadata-only cache search, and exact on-demand recovery-password reads.
- Release automation now archives any unmerged `release/*` branch to an `archive/*` tag before deleting the stale release branch after a successful GitHub Release; merged release branches are deleted directly.

## 0.16.1

- Simplified the first GUI tab into a helpdesk-focused BitLocker recovery workflow.
- The normal first-screen flow is now **Connect to AD → Select OU → Search BitLocker → Show/Copy Key**.
- Moved DC/FQDN, domain, LDAP/LDAPS, explicit credentials, credential storage, and vault controls into **Advanced connection settings...**, hidden by default.
- Added a larger computer name / Recovery ID search field and clearer recovery-purpose/status text.
- The first screen no longer looks like a connection/configuration utility; it now presents BitLocker recovery as the primary task.
- Existing advanced AD settings, standalone/workgroup connectivity, protected credential storage, RBAC, JIT recovery, two-person approval, and auditing behavior are preserved.

## 0.16.0

- Added optional time-limited JIT recovery grants. JIT is disabled by default and must be explicitly enabled by an administrator.
- Added optional two-person recovery approval with a distinct authorized Windows approver. The requester cannot approve their own session.
- Added optional metadata-only SIEM forwarding with durable local outbox, JSONL or HTTPS webhook delivery, optional client-certificate authentication, bounded queueing, and explicit fail-open/fail-closed policy.
- JIT grants and approval decisions are anchored to the existing tamper-evident audit chain; invalid or missing retained anchors are rejected.
- Added privileged-access policy enforcement before local AD and Entra recovery-key reveal/copy/get operations.
- Added privileged-access CLI controls for status, JIT grants/revocation, approval decisions, SIEM configuration/status/flush, grantor/approver authorization, and policy enable/disable.
- Added a dedicated **Operations → Privileged Access...** GUI for JIT, two-person approval, SIEM configuration, JIT grant/revoke, approval/deny, readiness checks, and SIEM flush.
- All privileged-access features remain opt-in and are never enabled by upgrade.
- Added shared DPI-aware WinForms behavior across the main window and dialogs, including working-area constraints and scroll fallback for high display scaling, large text, RDP, and smaller screens.
- All main tabs now permit scrolling when DPI/text scaling makes content larger than the visible client area.
- Main GUI title now reads the assembly version instead of a hard-coded version string.
- Reworked the first GUI tab into a guided **BitLocker Recovery Search** workflow: connect to AD, automatically open the OU selector, then search live AD by computer name or Recovery ID.
- Added live scoped AD recovery lookup so the first screen can retrieve matching BitLocker recovery records directly from the selected OU without requiring a prior CSV export.
- Standardized the internal default export root as `%ProgramData%\BitKeyBridge\RecoveryExport`, while keeping the optional output subdirectory empty unless explicitly configured.
- Application configuration schema remains v3 for the optional privileged-access/SIEM settings.

## 0.15.0

- Added evidence-aware housekeeping for recovery incidents, configuration backups, and stale atomic temporary files.
- Incident retention is opt-in: `IncidentRetentionDays = 0` keeps incident bundles forever by default.
- Incident deletion is allowed only when the bundle verifies as `Valid` against the currently retained tamper-evident audit chain.
- Incidents with `NotFullyRetained`, metadata mismatch, missing retained audit anchors, invalid audit state, or other verification failures are preserved.
- Before deleting a verified incident, BitKeyBridge writes a tamper-evident `HousekeepingDeleteIncidentPlan` audit entry containing the bundle SHA-256 digest and correlation/session ID.
- Incident deletion is refused if the authorization audit entry cannot be written.
- After successful deletion, BitKeyBridge writes a completion audit entry referencing the authorization EntryHash; completion-audit failures are surfaced in housekeeping health.
- Backup retention defaults to 90 days while preserving at least the five newest backup JSON files; setting retention to 0 keeps backups forever.
- Added cleanup of stale `.tmp` files left by interrupted atomic writes; default retention is 7 days and 0 disables this cleanup.
- Housekeeping directory-enumeration failures are no longer treated as empty directories; access/I/O problems fail the housekeeping run and surface through health/Event Log.
- Added scheduled housekeeping to the native Windows Service with an independent interval and optional run-on-start behavior.
- Added CLI controls for housekeeping status/run/dry-run, retention settings, minimum backup count, and temporary-file retention.
- Added a Housekeeping / Protected Storage GUI for retention policy, dry run, immediate cleanup, ACL status/repair, and quick access to Incidents/Backups.
- Added protected-storage ACL validation and repair for Incidents, Backups, and AuditSigningTransitions.
- Protected storage ACL repair removes inheritance and unexpected Allow ACEs, keeps FullControl for SYSTEM/local Administrators, grants the installed service identity only the directory-specific access it needs, and keeps audit-signing transition history read-only to the service.
- ACL hardening is explicit/admin-triggered; Windows Service health checks validate the policy but never silently repair ACLs.
- Changing the Windows Service identity now synchronizes protected-storage ACLs with LocalSystem, gMSA, or domain service accounts and avoids restoring stale ACLs after a successful SCM identity switch.
- Added housekeeping/storage-ACL state to `/health/security`, Prometheus `/metrics`, sanitized diagnostics, and Windows Event Log.
- Added application configuration schema v2 for the new housekeeping/storage policy. Existing v1 configuration is migrated with the existing versioned backup/migration mechanism.
- Added offline self-tests for evidence-aware incident deletion, retention preservation, backup minimum retention, temporary-file cleanup, ACL repair/validation, and housekeeping authorization/completion audit records.

## 0.14.0

- Added verification of metadata-only recovery incident bundles against retained tamper-evident audit entries.
- Incident verification validates the audit chain, stable audit snapshot, exact AuditEntryHash anchors, recovery-session CorrelationId, action/result/source/auth metadata, operator/host/device/recovery ID, ticket/reference, reason, and rotation state.
- Incident verification distinguishes a genuinely missing/mismatched audit anchor from `NotFullyRetained`, where an incident action is older than the currently retained audit window.
- Incident verification explicitly detects accidental 48-digit BitLocker recovery-password patterns inside incident JSON.
- Added `--incident-verify <session-id>` with JSON output and distinct exit behavior for valid, not-fully-retained, and failed verification.
- Added **Incident...** to the Audit tab with recent-session selection, verification details, Open Bundle, and Open Incident Folder actions.
- Added a secret-free Prometheus text formatter and loopback-only `/metrics` endpoint on the existing local health listener.
- Prometheus metrics expose operational, Coverage, RBAC, audit-integrity/signing, certificate-lifetime, and Remote API posture gauges without user/computer/ticket/recovery identifiers, token hashes, or recovery secrets.
- Added explicit application configuration schema versioning. Current schema is v1.
- Legacy pre-versioned `appsettings.json` files are upgraded atomically after an exact backup copy is written under the machine Backups directory.
- Read-only/non-admin clients can continue with the effective migrated configuration when migration cannot be persisted; migration is retried later and surfaced through health.
- Configuration files with a future unsupported schema are rejected without modification instead of being silently interpreted with older defaults.
- Added configuration migration status to health and sanitized diagnostics bundles.
- Avoided repeated migration-status writes once the current schema has been recorded.
- Added offline self-tests for incident verification/tamper detection, secret-free metrics, legacy configuration migration/backup, and future-schema refusal.

## 0.13.0

- Added recovery-session correlation IDs across local AD reveal/copy, Entra get/reveal/copy, and Intune rotation workflows.
- Added metadata-only recovery incident bundles under `%ProgramData%\BitKeyBridge\Incidents`; bundles contain operator/device/ticket/timestamps/actions/audit hashes and rotation state, never the 48-digit recovery password.
- Added backward-compatible audit chain version 2. Version 1 entries remain verifiable; version 2 adds the recovery-session CorrelationId to the SHA-256 entry hash.
- Audit writes now return the appended entry and EntryHash so incident bundles can reference exact tamper-evident audit records.
- Added the Session/Correlation ID column to the Audit tab.
- Added focused local health endpoints: `/health/ready`, `/health/security`, and `/health/coverage`, while preserving `/health` and `/health/live`.
- Added least-privilege Remote API bearer tokens with independent `read`, `coverage-run`, and `export` scopes. The existing bearer token remains the backward-compatible Admin token.
- Remote API GET endpoints accept any valid scope; Coverage POST requires Admin/CoverageRun and Export POST requires Admin/Export, in addition to the global remote-management switch.
- Added scoped-token lifecycle through CLI and GUI; tokens are shown once and only SHA-256 hashes are persisted.
- Added safe JSON configuration backup/restore with validation and an automatic pre-restore rollback backup.
- Configuration backup intentionally excludes Credential Manager/DPAPI password material, Graph tokens, certificate private keys, and BitLocker recovery passwords.
- Added sanitized diagnostics ZIP generation with health/service/config/certificate ACL/status/log metadata while explicitly excluding recovery CSV/passwords, audit contents, credential blobs, bearer tokens/hashes, Graph tokens, and private keys.
- Added Configuration / Diagnostics controls to Operations and CLI commands `--config-backup`, `--config-restore`, and `--diagnostics-bundle`.
- Added staged Microsoft Entra certificate rollover. A new credential is added without removing the active credential, app-only Graph authentication is verified, service private-key ACL is prepared, and only then are user/machine configs switched.
- Entra rollover retains the previous Graph credential and local certificate for rollback/grace instead of automatically deleting them.
- Added dual-signed audit-signing certificate rollover. The transition payload is signed by both the previous and new RSA private keys and transition history is retained under `%ProgramData%\BitKeyBridge\AuditSigningTransitions`.
- Audit-signing rollover verifies the current trust anchor before transition, creates a new signed checkpoint, rolls configuration/checkpoint back on normal failures, and retains old certificates for historical verification.
- BitKeyBridge now verifies the complete archived dual-signed transition history, including both signatures, certificate availability, machine identity, chain version, and old→new thumbprint continuity; failures are surfaced through CLI/GUI/security health.
- New Remote API TLS certificates are persisted without the Exportable flag.
- Added Remote API TLS private-key ACL preflight for installed service identities and service-identity changes.
- Health/security output now reports Remote API scope configuration, TLS certificate expiry/status, and service private-key access without exposing token hashes.
- Added offline self-tests for mixed audit v1→v2 chains, CorrelationId hashing, metadata-only incident bundles, dual-signed rollover tamper detection, Remote API scope normalization, and configuration validation.
- Updated the public appsettings example for scoped Remote API token hashes.

## 0.12.0

- Added cryptographically signed audit checkpoints on top of the existing SHA-256 audit hash chain.
- Added a dedicated RSA-3072 LocalMachine audit-signing certificate, separate from Entra and Remote API certificates.
- Audit-signing private keys are persisted as machine keys without the Exportable flag.
- Signed checkpoints include the audit head hash, entry counts, chain version, machine identity, signer thumbprint, timestamp, and RSA-SHA256-PKCS1 signature.
- Existing signed checkpoints must verify successfully and their signed hash must still exist in the current valid audit chain before BitKeyBridge can overwrite the trust anchor.
- This continuity guard prevents the Windows Service from silently signing an already rewritten audit chain during the next scheduled verification.
- Added audit-signing CLI commands: `--audit-signing-status`, `--audit-signing-setup`, `--audit-signing-sign`, `--audit-signing-verify`, `--audit-signing-disable`, and `--audit-signing-years`.
- Added Audit-tab controls for setup, immediate signing, signature verification, disabling new checkpoints, and signer/checkpoint status.
- Windows Service identity changes now preflight private-key access for the audit-signing certificate in addition to the Entra certificate.
- The Windows Service signs the verified audit head during its daily integrity cycle when audit signing is enabled.
- Health output now exposes signed-checkpoint state, signer-certificate expiry, signature validity, checkpoint age/count, and whether the current audit head is signed.
- Added configurable warning horizon for audit-signing certificate expiry.
- Added offline self-tests for RSA signed-checkpoint round-trip and signed-payload tamper detection.
- Updated the public appsettings example with audit-signing settings.

## 0.11.0

- Added optional Windows user/group RBAC for privileged recovery workflows while preserving backward-compatible behavior when RBAC is disabled.
- Added separate `RecoveryRead` and `Rotate` permissions so helpdesk recovery access and Intune rotation can be delegated independently.
- RBAC principals accept Windows users, groups, and SIDs; local Administrators bypass is configurable.
- Enforced RBAC before local recovery CSV search, AD recovery reveal/copy, Entra recovery retrieval/reveal/copy, and Intune BitLocker key rotation.
- RBAC denials are written to the BitKeyBridge security audit and Windows Application Event Log without recording recovery passwords.
- Added RBAC GUI configuration with principal validation and effective-permission preview for the current Windows identity.
- Added RBAC CLI commands: `--rbac-status`, `--rbac-enable`, `--rbac-disable`, `--rbac-admin-bypass`, reader add/remove, and rotator add/remove.
- Added RBAC configuration health fields and unresolved-principal validation without exposing configured group names through the health endpoint.
- Added SHA-256 tamper-evident hash chaining to new audit JSONL entries while keeping existing legacy audit lines readable.
- Added cross-process audit serialization so GUI and Windows Service writes cannot race the hash chain.
- Added `--audit-verify` and **Verify Chain** in the Audit tab to validate `audit.jsonl` and its rotated `.old` file.
- The native Windows Service now verifies audit integrity at startup and every 24 hours, caches the result for health monitoring, and writes integrity failures to Windows Event Log.
- Added offline self-tests for RBAC backward compatibility, audit hash-chain verification, and deliberate tamper detection.
- Updated the public appsettings example with scheduled Coverage, Coverage Policy, and RBAC settings.

## 0.10.0

- Added automatic LocalMachine certificate private-key ACL management for Windows Service identities.
- Supports both CNG private keys under `%ProgramData%\Microsoft\Crypto\Keys` and legacy CAPI keys under `RSA\MachineKeys`.
- gMSA and regular domain service identities can receive a narrow explicit Read ACE without replacing the existing private-key ACL.
- LocalSystem is recognized as not requiring a separate BitKeyBridge-managed Read grant.
- Service identity changes now perform certificate-access preflight before SCM identity changes when scheduled Coverage depends on the machine certificate.
- Machine cloud save/status now validates the installed service identity's certificate private-key access.
- Added certificate ACL CLI commands: `--cert-key-status`, `--cert-key-grant`, `--cert-key-revoke`, and `--cert-account`.
- Added **Repair Cert Access** to the Dashboard and private-key access state to the health snapshot.
- Added a configurable Coverage Policy engine for No Recovery Key, Intune Not Encrypted, Intune Stale, and Old Cloud Key metrics.
- Each policy metric has an allowed maximum and Error/Warning/Info severity.
- Added Coverage Policy GUI configuration and CLI configuration/status commands.
- Added `--coverage-fail-policy` with exit code 23 for monitoring wrappers.
- Coverage status JSON now persists the evaluated policy result and structured violations.
- Scheduled Coverage maps policy severity to Windows Event Log severity.
- Added a shared unattended Coverage automation service with process-wide serialization to prevent overlapping scheduled/remote runs.
- Added metadata-only Remote API endpoints `GET /api/v1/coverage` and `GET /api/v1/coverage/policy`.
- Added opt-in management endpoint `POST /api/v1/coverage/run`; it never returns recovery passwords or device-level recovery secrets.
- Extended offline self-tests for certificate identity normalization and Coverage Policy evaluation.

## 0.9.0

- Added metadata-only BitLocker Coverage automation for CLI and Task Scheduler workflows.
- Added `--coverage` to generate both CSV and JSON coverage reports from AD, Entra ID, and Intune metadata.
- Added Device Code, certificate, and legacy password authentication selection for coverage CLI.
- Added per-run cloud overrides for tenant ID, client ID, certificate thumbprint, and cloud username without adding a plaintext password command-line option.
- Added `--coverage-output`, `--coverage-csv`, `--coverage-json`, and `--coverage-json-stdout`.
- Added monitoring exit policies: exit 20 for missing recovery metadata, 21 for Intune-reported unencrypted devices, and 22 for stale Intune devices.
- Added offline self-test coverage for coverage monitoring exit codes.
- Added machine-level Entra certificate configuration at `%ProgramData%\BitKeyBridge\cloud_auth_machine.json` for LocalSystem/gMSA/unattended scenarios; the file stores identifiers and a certificate thumbprint only.
- Added `--cloud-machine-status`, `--cloud-machine-save`, `--cloud-machine-delete`, and `--coverage-machine-config`.
- Added optional scheduled Coverage to the native Windows Service with an interval independent from the AD recovery export interval.
- Added Dashboard controls for scheduled Coverage and machine cloud configuration.
- Added Coverage status to the local health snapshot, including last run, age, missing recovery metadata, unencrypted Intune devices, stale Intune devices, and old cloud-key metadata.
- Added service Coverage CLI controls: `--service-coverage-enable`, `--service-coverage-disable`, `--service-coverage-interval`, and run-on-start switches.
- Coverage CSV now uses UTC timestamps and neutralizes spreadsheet formula-like values before export.
- Coverage automation continues to avoid AD recovery-password attributes and the Graph recovery-key value endpoint.

## 0.8.0

- Added a metadata-only BitLocker Coverage Dashboard across Active Directory, Microsoft Entra ID, and Intune.
- Added AD scope inventory queries for computers without reading any BitLocker recovery password.
- Added AD recovery-object metadata queries limited to recovery GUID and creation time.
- Added tenant-wide Entra recovery-key metadata collection without requesting the `key` property.
- Added Intune managed-device inventory correlation for encryption, compliance, last sync, user, serial, manufacturer, model, and OS.
- Added coverage classifications: AD + Entra, AD only, Entra only, and No recovery key.
- Added detection of multiple recovery objects, stale Intune devices, unencrypted Intune devices, and old Entra recovery-key metadata.
- Added Coverage filters and visible-row CSV export.
- Added configurable stale-Intune and old-cloud-key thresholds.
- Coverage auditing records counts/status only and never records a recovery password.
- Increased Intune inventory result capacity for tenant-wide coverage reports.

## 0.7.0

- Added three AD credential modes: Session only, Current User Credential Manager, and Machine / Service DPAPI vault.
- Added Windows Credential Manager storage for interactive per-user AD credentials.
- Added machine-bound DPAPI LocalMachine storage at `%ProgramData%\BitKeyBridge\Secrets\ad-machine.cred`.
- Added protected NTFS ACLs for the machine credential vault (SYSTEM and local Administrators only).
- Added explicit elevation requirement for machine-vault create/delete operations.
- Added GUI controls to save, delete, inspect metadata, and select the AD credential storage mode.
- Added CLI vault operations: `--vault-status`, `--vault-save-user`, `--vault-save-machine`, `--vault-delete-user`, and `--vault-delete-machine`.
- Added native Windows Service identity management for LocalSystem, gMSA/managed service accounts, and regular domain accounts.
- gMSA configuration never accepts or stores a password; Active Directory manages the managed-account password.
- Regular Windows Service account passwords are passed directly to SCM when the identity is changed and are never stored by BitKeyBridge.
- Added service identity to service-status and health output.
- Added a guard preventing unattended Windows Service use with the interactive Current User credential vault.
- Added offline DPAPI LocalMachine protect/unprotect self-test coverage.
- No AD or Windows Service password is added to `appsettings.json`, audit JSONL, Event Log, or release artifacts.

## 0.6.0

- Added Active Directory connection modes for domain-joined workstations/DCs and standalone/workgroup computers.
- Added explicit DC, AD domain, LDAP/LDAPS port and AD username configuration.
- Added LDAPS/TLS support (normally TCP 636) alongside signed/sealed LDAP (normally TCP 389).
- Added session-only explicit AD password handling; passwords are never written to appsettings or logs.
- Added current-Windows-credentials mode for domain workstations and DCs.
- Added a Directory Connection GUI tab with DC connection test and session-password clearing.
- Added CLI connection overrides: `--ad-auto`, `--ad-server`, `--ad-domain`, `--ad-user`, `--ad-password-prompt`, `--ad-integrated`, `--ad-port`, and `--ad-test`.
- Added configurable local/UNC export root so BitLocker recovery export no longer requires BitKeyBridge itself to run on a domain controller.
- Added configurable local/UNC output-root behavior for workstation, server, and service deployments.
- Microsoft 365 / Entra / Intune workflows remain independent of Windows domain membership.
- Added a CI gate requiring every project version to have a matching CHANGELOG section.

## 0.5.0

- Added optional ticket/reference requirement before recovery-key access.
- Added helpdesk reason capture and structured Reference/Reason fields in the JSONL security audit.
- Reused recovery access context across reveal, copy, retrieval, and rotation during the GUI session.
- Added post-recovery Intune rotation reminders without automatic rotation.
- Added CodeQL v4 scanning on Node 24.
- Added Dependabot for NuGet and GitHub Actions.
- Added pinned CycloneDX 6.2.0 JSON SBOM generation for tagged releases.
- Added GitHub provenance and SBOM attestations through actions/attest@v4.
- Added the SBOM to SHA256SUMS and GitHub Release assets.
- Extended offline self-test coverage for audit reference/reason persistence.


## 0.4.0

- Added verified GitHub Release self-update for x64, x86, and ARM64.
- Added SHA256SUMS verification and GitHub asset-digest cross-checking.
- Added staged executable version validation and mandatory offline `--self-test` before update application.
- Added elevated temporary update helper with GUI/service replacement and rollback.
- Added `--check-update` and `--update` CLI commands.
- Added Windows Event Log integration with recovery-password sanitization.
- Added opt-in TLS Remote API with one-time bearer token provisioning.
- Added Windows Firewall Domain/Private inbound rule management through the Windows Firewall COM API.
- Added read-only remote health/service/version endpoints and separately gated remote export management.
- Added native Windows Service failure-recovery configuration.
- Kept updater cleanup shell-free through Win32 `MoveFileEx`.
- Added Operations UI for update, Remote API, token rotation, and Event Log access.


## 0.3.0

- Added a Health & Service Dashboard.
- Added native Windows Service installation/runtime through Service Control Manager APIs.
- Added scheduled service exports with configurable interval and run-on-start behavior.
- Added a loopback-only JSON health endpoint on 127.0.0.1 with HTTP 503 on error health states.
- Added `--health`, `--install-service`, `--uninstall-service`, `--start-service`, `--stop-service`, and `--service-status` CLI commands.
- Added certificate-expiry and export/replication health reporting.
- Added Secure Output Wizard with protected NTFS ACLs and optional restricted SMB share creation through `NetShareAdd`.
- Added GUI sensitive-state cleanup on close.
- Kept runtime operation free of PowerShell and `sc.exe`.


## 0.2.1

- Removed the requirement to pre-create an App Registration for first-run Entra setup.
- Added built-in Microsoft first-party bootstrap using Microsoft Graph Command Line Tools and Device Code.
- Moved custom Bootstrap Client ID to an advanced fallback for restricted Conditional Access environments.
- Resolved runtime Graph permission IDs dynamically from the Microsoft Graph service principal instead of hard-coding them.
- Added retry/backoff for Graph throttling and transient service errors.
- Added propagation handling for newly created Enterprise Applications.
- Added offline self-test coverage for the built-in bootstrap configuration.


## 0.2.0

- Added Device Code authentication as the recommended interactive Graph sign-in mode.
- Added Intune managed-device inventory and unified AD + Entra + Intune device search.
- Added BitLocker recovery-key rotation through Intune with explicit confirmation.
- Added local JSONL security audit with automatic BitLocker recovery-password redaction.
- Added audit self-test.
- Added `DeviceManagementManagedDevices.ReadWrite.All` to native Entra Auto Setup.
- Updated GitHub Actions to Node 24 runtimes.
- Kept legacy ROPC and certificate authentication modes.

## 0.1.0

Initial native Windows implementation.

- C# / .NET 10 LTS / WinForms.
- Self-contained single-file executables for win-x64, win-x86, and win-arm64; each architecture uses the same GUI/CLI codebase.
- Direct LDAP BitLocker export with atomic CSV publishing and safety guards.
- Dynamic domain-controller discovery and replication diagnostics.
- Configurable OU scopes and local recovery search.
- Microsoft Graph recovery metadata search and explicit on-demand recovery-key retrieval.
- Password/ROPC and certificate authentication.
- Native Entra App Registration bootstrap/setup flow.
- GitHub Actions Windows build, single-file publish, offline self-test, and tagged release packaging.
