# BitKeyBridge v0.31.0 - UI language foundation

**First localization increment, not full application translation.**
Language selection is available to every technician from the top main
menu and is stored only in the current user's LocalAppData:
`ui-language.json`. Supported UI language choices:

- English (`en`)
- Russian / Русский (`ru`)
- Hebrew / עברית (`he`)

The primary workspace names (Start, BitLocker, LAPS, Devices, Helpdesk,
Diagnostics and permission-gated Settings), Tools/Language menu captions,
Start Search/Cancel buttons and Start connection shortcut tooltip are
translated. The selected language persists across launches and is not
stored in machine policy, cloud credentials or the Windows registry.
Hebrew enables WinForms `RightToLeft` and `RightToLeftLayout` on the main
shell and TabControl. Other languages retain left-to-right navigation.

BitLocker recovery keys, LAPS passwords, recovery IDs and computer/device
identifiers are **never reversed, transformed or translated**. Functional
subtabs, detailed errors, dialogs, technical commands and diagnostic content
still require additional localization work; no claim of full translated
coverage is made. This does not change RBAC, security decisions, audit,
credential storage or privilege elevation. Ctrl+1..7 still uses the actual
technician order even in RTL mode.

Offline tests validate translations, RTL mode, preference persistence and
unsupported-code rejection. RDP 100%–200% mirrored layout and screen-reader
acceptance with genuine Hebrew and Russian Windows user profiles remains a
technician pilot requirement.
