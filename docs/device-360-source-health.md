# Device 360: Source Health and access-policy readiness (v0.35 draft)

Target: PR #114 on \`feature/v0.35.0-unified-device-card\`.

This increment extends the already existing read-only Device 360 card. It
does NOT start a network request, permission probe, recovery read, consent
flow, audit event, authentication or retry. It does not persist device data.

## Source evidence

AD, Entra and Intune each show one of three conservative states:

- **Record found** only when the current selected metadata record explicitly
  identifies the device in the source.
- **Session present; unverified** if an existing connection/token is usable,
  but the selected device has not been verified in that source.
- **Not connected; unverified** otherwise. This does not assert absence.

Graph token presence alone is insufficient: the token must be unexpired
and match the active cloud authentication context. No implicit refresh is made.

The existing structured `UnifiedDeviceSearchReport` supplies independent
AD/Entra/Intune success flags. A failed source is shown as **Source query
failed; unverified**, even when another source knows the device ID. Only
the three error flags are retained with the current results: no error strings,
tokens or prior-result collections are cached. A source failure also marks
the card **Search incomplete** and does not prove a missing recovery backup.

There is still no claim that the failure was specifically 403/429/timeout:
the current report does not provide a typed per-source failure category.
Do not parse exception-message text to guess permissions or add retries here.

## Access and audit

Show RBAC, ticket, protected audit and Audit Broker configuration separately
from actual effective permissions. The effective recovery permission indicator
always reads **Not checked by this card**. Do not change existing
secret-bearing handlers. **Open Access Health** is navigation only and
does not automatically execute any checks or expose secrets.

All UI text is localized EN/RU/HE; Hebrew field/ID LTR rules remain intact.

## Verification

- Windows x64, x86, ARM64; x64 native UI self-test and synthetic EN/RU/HE PNGs
- CodeQL, LAPS regression, protected audit/signing/upgrade integration gates
- Manual 100–200% DPI/RDP Windows 11, standard/delegated/denied domain users,
  precise identity checking, signing and rollback
- Never merge or release without verified manual domain acceptance

Stable remains unchanged. This change is in a development draft PR.
