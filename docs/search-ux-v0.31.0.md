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
