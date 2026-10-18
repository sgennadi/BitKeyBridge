# Startup connection chooser — remember last source

**Status: unreleased development change, after v0.32.0-rc.2.** This feature
is not included in the public v0.32.0-rc.2 binary.

## First launch and existing installations

At interactive startup the user sees a compact connection wizard:

- **Active Directory**: the current Windows account by default, or an
  explicitly selected alternate account with a fresh password prompt.
- **Microsoft Entra / Intune**: existing delegated Device Code + MFA login.
- **Both**: AD first, then Microsoft 365 using the existing permission checks.
- **Choose later**: launch Start without authenticating to either source.

The checkbox **Remember this connection** is **unchecked by default**. The
source from an earlier BitKeyBridge install may appear preselected to make
the wizard convenient, but that old saved selection alone never skips the
wizard or grants permission to reconnect automatically.

## Remembered startup

When the user selects AD, Cloud or Both, checks **Remember this connection**,
and clicks **Connect**, BitKeyBridge saves ONLY the source and the choice of
Windows identity versus an alternate AD account in the current user's
`%LocalAppData%\BitKeyBridge\start_connection.json`.

On the next launch, if `AppConfig.AutoConnectOnStart` is enabled, the chooser
is skipped and the saved connection flow begins automatically.

- Windows-integrated AD uses the current Windows logon token.
- Alternate AD sign-in always requests a **fresh** password; no password,
  access token, certificate private key or secret is added to this preference.
- Entra / Intune uses the existing delegated authentication and MFA; a user
  may still need to confirm Device Code or an administrator-approved
  application configuration. It does not silently grant tenant consent.
- **Both** combines the two existing guarded flows, without expanding
  RBAC or automatically revealing BitLocker/LAPS passwords.
- The feature does **not** register a Windows startup task, force the app
  to start with Windows, or install unsigned updates.

If machine configuration disables `AutoConnectOnStart`, the checkbox is
disabled and the wizard continues to appear.

## Change the remembered preference

Use **Start → Reconnect / Change connection...** at any time. Uncheck
**Remember this connection** and press **Connect** to make the wizard appear
on the next startup.

Changing the source directly in the Start drop-down also clears the
remembered auto-connect opt-in. Recheck it via the wizard if the newly
selected source should automatically reconnect next time.

Choosing **Choose later** also clears remembered auto-connect. Canceling
the wizard without confirming leaves the saved preference unchanged.

## Safety and verification

This feature runs in the ordinary helpdesk Windows account. No UAC, local
admin rights, privileged service operation, unattended secret retrieval,
cloud application-only token or elevated AD permission is introduced.

`--self-test` verifies default/legacy opt-out and source/policy cases.
`--ui-self-test` verifies the startup checkbox state and behavior at
supported HiDPI layouts on Windows.
