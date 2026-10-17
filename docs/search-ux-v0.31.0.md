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
