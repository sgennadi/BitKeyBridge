# BitKeyBridge 0.35.0-rc.5 — Support contact and localized Helpdesk

**UNSIGNED isolated manual QA only. Not Stable. Do not deploy via GPO, Intune or the scheduled updater.**

## Identity and artifact

- ProductVersion/InformationalVersion: `0.35.0-rc.5`; Windows FileVersion stays `0.35.0.0`.
- Source: [one integration PR #115](https://github.com/sgennadi/BitKeyBridge/pull/115) on `release/v0.35.0-rc.1` (historical branch name).
- The expected CI artifact, **only after all exact-HEAD checks pass**, is `BitKeyBridge-v0.35.0-rc.5-QA-UNSIGNED`, with x64, x86 and ARM64 single-file executable ZIPs, `BUILD-INFO.txt` and `SHA256SUMS.txt`.
- Previous successfully tested baseline: RC4, commit `b64b38fdad3aea10aa6d5c71bb8ff67a23152568`, [RC4 verification](release-candidate-v0.35.0-rc.4.md). Its QA ZIP **does not** include this Support feature.

## Support feature

1. The top menu **Support** must be visible to both standard Helpdesk users and administrators, from Start, Devices, LAPS and other workspaces.
2. **Email support** opens the Windows default email app with a *new draft* to `sgennadi@hotmail.com`, subject `BitKeyBridge Support`. BitKeyBridge sends nothing automatically. The app adds no device identifiers, Graph tokens, recovery keys, passwords, logs or attachments.
3. **Copy support email** copies only `sgennadi@hotmail.com` to the clipboard. If the default mail client is missing or the clipboard is locked, show a non-secret, non-modal diagnostic and the literal address.
4. Helpdesk shows the same visible **Email support** button and LTR address link, separate from **Support Bundle**, which remains unchanged.
5. English, Russian and Hebrew translate all fixed text. The email stays LTR and readable under Hebrew RTL, in compact / 100-200% DPI / RDP layouts.
6. Offline native UI self-test verifies these menu entries, label and email address in repeated EN/RU/HE switches and the exact `mailto:` URI, without launching email or accessing secrets.

## Mandatory acceptance gates

- Run `BitKeyBridge.exe --version`, `--self-test --no-elevation`, `--ui-self-test --no-elevation` on a disposable Windows 11 workstation, then manually exercise Support in EN/RU/HE with and without a default mail application.
- Require exact-HEAD x64/x86/ARM64 build, CodeQL, UI, LAPS, legacy 0.30.1/0.30.3 upgrade/rollback, protected audit and ephemeral signing checks.
- The previously unresolved live AD/Entra/Intune search, effective authorization, BitLocker/LAPS Reveal/Copy and session-reuse tests still require a managed, authorized human operator. Corporate Authenticode signing, signed-only canary and rollback are required before any Stable rollout.
