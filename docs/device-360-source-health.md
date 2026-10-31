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

A partial search marks the card **Search incomplete**, without treating
missing data as missing recovery backups. There is no per-source 429,
403, denied or timeout indicator until structured search results expose one.

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
