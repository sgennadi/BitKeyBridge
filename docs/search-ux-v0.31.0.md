# v0.31.0 technician search improvements (development)

- Ctrl+F focuses and selects the existing metadata-only search query for
  the currently active Start, BitLocker, LAPS, Devices or Helpdesk tab.
- Ctrl+1..7 retains the v0.30.4 work order without needing a mouse.
- No request for BitLocker/LAPS secret is triggered by a keyboard shortcut;
  existing explicit reveal/copy and audit controls remain unchanged.
- Access Health, source-specific AD/Entra/Intune search and ambiguity handling
  are reused, not replaced. Failing one metadata source does not authorize
  fallback recovery.
- Future work: source-specific retry, incident-centric workflow, stable
  language resources and correctly mirrored Hebrew RTL layout.

## Localization foundation (English / Russian / Hebrew)

The selected language for primary navigation is stored per-user as
`%LOCALAPPDATA%\BitKeyBridge\ui_language.json`, never in machine security
policy. The default follows the Windows UI language, falling back to English.
The Language menu switches tab names live, preserving search content, selected
device and Windows authorization context. Hebrew mirrors the tab header and
menu order via RightToLeft/RightToLeftLayout while computer names, recovery
IDs and search inputs remain LTR. Offline tests validate the navigation
resources for all three languages. More Helpdesk, settings and diagnostics
strings require translation in subsequent v0.31 stages; this is a localized
navigation foundation rather than a completed app-wide translation.

## Access Health 2.0 metadata checks

The Access Health panel now includes source-aware current Windows token RBAC
(search, read, rotation) and a fail-closed protected audit broker policy
check. When strict audit is enabled, it also reports the installed Windows
Service state without reading protected journal entries. A durable broker
ACK is deliberately **not** executed by the read-only diagnostics; the
explicit `--audit-broker-preflight --probe` performs that test and writes a
synthetic health event. BitLocker metadata, AD LAPS checks, Intune/Graph
metadata and Entra LAPS checks remain in their existing source-isolated
sections. These diagnostics never substitute for per-request authorization
or grant a secret reveal.
