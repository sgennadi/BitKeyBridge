# BitKeyBridge 0.36.0-preview.1 — Device 360 hardware and diagnostics

**Development only**, based on the published Stable 0.35.0. This preview does not authorize GPO/Intune rollout, corporate signing or changes to real domain security policy.

## Hardware Inventory 2.0

Only the operator's explicit **Check hardware (WMI)** starts additional network access, and only against a verified, non-ambiguous AD DNS hostname bound to its directory identity. WMI uses integrated Windows credentials with PacketPrivacy and read-only queries. The existing manufacturer, model, BIOS and TPM checks are retained. Additional fields include BIOS serial number, CPU model, number of logical processors, physical RAM GiB, aggregate raw disk capacity/count and physical adapter count.

These queries do not request disk serials, IP/MAC addresses, local users, security secrets, BitLocker protectors or LAPS attributes. A failure in an optional provider leaves the rest of the observed inventory intact; only typed categories are displayed. No privileges, firewall, WMI configuration or GPO settings are changed. The WMI API cancellation limitation remains: a blocked underlying COM call is not forcibly terminated.

## Device Health 2.0

A new **evidence coverage** percentage shows what fraction of metadata checks yielded evidence. An observed problem counts as known evidence but also produces an Action Needed status. Unknown sources and missing data lower coverage. The number is not a security/health score and does not grant permission to retrieve a password. Effective authorization is checked solely by the existing approved recovery workflow.

## Smart Diagnostics 2.0

Diagnostics shows stable classified codes, source, finding and specific next action for identity conflicts, per-source metadata failures, incomplete BitLocker backup metadata, expired/unverified LAPS metadata, Intune compliance/encryption/stale sync and classified WMI failures. All recommendations are offline derived from the existing current snapshot and the technician's explicit WMI result, never from raw exception text. No retries or hidden MFA prompts.

## Required checks

- Run exact-source x64/x86/ARM64 CI plus `--self-test --no-elevation`, `--ui-self-test --no-elevation`, LAPS, Audit Broker, CodeQL and update/rollback regression.
- Validate EN/RU/HE, RTL, compact monitors and RDP at 100/125/150/175/200% DPI, including all new hardware fields.
- Try an authorized WMI host, denied WMI account, offline workstation, unavailable TPM class, disk WMI failure, ambiguous AD/Entra ID, stale/absent Intune data. Partial inventory must remain visible without false healthy status.
- Review manual Helpdesk report to confirm no recovery key, LAPS password, Graph token, stack trace, remote exception message, IP/MAC address or disk serial.
- Do not push unsigned previews through GPO/Intune. The generic updater and centralized diagnostic transport remain out of scope by prior decision.

## WMI retry and evidence consistency

The UI discards the previous WMI sample, clears read-only hardware fields and restores the original directory-sourced hardware label whenever the operator starts a new WMI probe. On success it recomputes both Health and Diagnostics; on classified error it shows Attention without stale TPM/CPU/disk evidence. Helpdesk exports use the same latest read-only sample and classified failure. This is not a background monitoring service.

## Native UI regression

The Windows UI self-test now injects a **synthetic-only** WMI success followed by a failed retry, using the same presenter as the real user-initiated WMI result. It verifies that stale CPU/RAM fields and previous WMI error codes disappear, the new typed error appears in Diagnostics, and the Health header refreshes in EN/RU/HE. Actual AD/WMI authorization and remote provider calls remain separate manual acceptance gates.

## Remote host identity consistency

The selected AD DNS hostname authorizes an explicit read-only WMI probe, but DNS by itself does not prove which Windows host answered. Before collecting BIOS, TPM, CPU, storage or adapter details, the probe compares `Win32_ComputerSystem.Name`, `Domain` and `PartOfDomain` against the exact selected AD object's DNS name/domain. On mismatch the result is classified `WMI.IDENTITY_MISMATCH`, with no accepted hardware inventory. The check is an additional consistency safeguard, **not** independent cryptographic endpoint attestation. It does not modify AD, DNS, firewall rules, WMI permissions or any secrets.
