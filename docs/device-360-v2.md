# Device 360 v2 — unified helpdesk workspace (QA)

Status: development on `release/v0.35.0-rc.1`, PR #115. **Not Stable.**
The features below do not change recovery-policy enforcement, identity authorization, or the protected audit broker.

## Operator workflow

1. Start -> Connect AD. Optionally choose **Connect Microsoft 365** (delegated Device Code + MFA) explicitly. A token is kept only in process memory, scoped to the configured tenant, client, permissions and expiry.
2. Run a **single explicit metadata search**. Start searches AD BitLocker/LAPS and AD inventory independently; when an authenticated Graph session exists it also searches Entra/Intune without asking for another login. An exact object identity is required to attach device inventory.
3. Open **Computer card**. Device 360 v2 opens its in-memory snapshot without a network call or permission escalation.
4. Device 360 tabs:
   - **Overview:** source status, uncertainty-aware health summary, Copy / Save Helpdesk report;
   - **Hardware:** inventory from the original search, plus an **optional** read-only Windows WMI BIOS/TPM check;
   - **Security:** observed BitLocker/LAPS metadata and configured policy gates; the actual recovery rights are explicitly untested;
   - **Identity:** AD object GUID/DN, Entra ID, Intune managed-device ID and link evidence; hostname/UPN/serial are never accepted as cross-source identity proof;
   - **Diagnostics:** per-source failure vs unverified distinction and suggested checks, including stale Intune sync, noncompliance, expired LAPS metadata.
5. Start -> Devices navigation **reuses the exact UnifiedDeviceInfo** already collected on Start. If it cannot prove an authoritative identity, it does NOT silently search or link by name; use an explicit Devices Search or select a new exact record.
6. The Devices Search never calls Device Code. It uses a valid in-memory delegated session or returns AD-only metadata with a **Connect Microsoft 365** button. Reauth is an explicit operator action.
7. Reports use an explicit Copy or Save action. The report contains *allowlisted* nonsecret fields only. It never traverses the original Graph/AD result objects; no recovery IDs/passwords, LAPS passwords, OAuth tokens or credentials are exported.

## Remote hardware

- Not launched on Start, on Device 360 open, on tab change or when creating a report.
- Explicit **Check hardware (WMI)** is enabled only for an identity-verified, non-ambiguous AD object with an AD source-provided fully qualified DNS hostname. Unqualified names, IP addresses, cloud-only IDs and duplicate identities cannot initiate this operation.
- Queries: `Win32_ComputerSystem` (manufacturer/model), `Win32_BIOS` (version/manufacturer/release date), `Win32_Tpm` (public status fields). No Win32_EncryptableVolume recovery protector, no TPM EK/private key and no LAPS attributes.
- Uses current Windows integrated credentials; PacketPrivacy, impersonation, no elevation, no credential fallback, no settings or ACL changes. The remote endpoint must allow DCOM/RPC and WMI read permissions. Failures show `AccessDenied`, `Timeout`, `WmiError` or `DCOM/RPCUnavailable`. TPM failure does not discard valid BIOS metadata.
- WMI is a legacy DCOM protocol and may be unavailable across VLAN/firewall or on non-Windows endpoints. Timeouts/cancellation cannot instantly interrupt a blocked underlying COM operation. The UI remains responsive and never starts repeated attempts automatically.
- **No implied computer identity equivalence:** the verified AD DNS hostname can identify the AD object, but Kerberos/Windows authorization and the remote host's own access controls remain necessary. Avoid changing DNS/permissions to make this test pass.

## Health evidence / safety

- `Observed` = metadata returned by a source, *not proof* that the secret is readable or the machine is fully healthy.
- `Attention` = observed noncompliance, expired LAPS, source query failure, stale Intune sync (14 days) or duplicate identity.
- `NotVerified` = source not connected, field absent, incomplete BitLocker metadata, unknown encryption or unknown compliance. **Never** interpret 0 keys as no backup when a lookup was incomplete.
- Authorization, ticket, protected audit and policy are still enforced only by the existing Reveal/Copy/Rotate handlers. A read-only Device 360 card never grants rights or auto-reads a secret.

## QA / acceptance gates

CI tests must pass on the exact HEAD: x64/x86/ARM64 publish, synthetic WinForms EN/RU/HE including compact 610x440 and 1366x768/125%, LAPS regression, protected audit broker, signed-update and rollback, CodeQL. Regression checks include:
- Helpdesk report excludes synthetic recovery IDs and OAuth token strings;
- missing metadata is not classified as healthy;
- Device 360 has five tabs and explicit report actions;
- WMI is disabled for cloud-only/unqualified identities;
- exact-ID bridge Start -> Devices preserves the same selected object without Graph;
- a deliberately forged same-name but different identity is rejected;
- Devices Search with configured tenant but no token does not launch MFA.

Before Stable: test on actual Windows 11 (100/125/150/200% scaling, EN/RU/HE), delegated and denied AD/Graph identities, a truly reachable AD workstation with WMI permissions, an RDP session and a small monitor. Verify enterprise Authenticode signing, canary, rollback, and real-user acceptance. Do not deploy an unsigned QA artifact to the domain.

## Limitations

- Native Intune managedDevices supplies model, manufacturer, serial, operating system, compliance, encryption, last sync; **BIOS/TPM are not invented** from Intune when the queried endpoint lacks an accessible remote WMI source.
- This milestone does not install agents, enable WinRM, open ports, change domain GPOs, persist auth tokens, or auto-link an AD computer and Intune object by display name.
- This is not a continuous monitoring service or automatic remediation engine.
