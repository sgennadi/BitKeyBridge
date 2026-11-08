# BitKeyBridge 0.36.0-preview.3 — Windows 11 managed canary acceptance

**Status: manual enterprise acceptance, not production authorization.**

GitHub Actions tests Windows EXE/WinForms behavior using synthetic data. It cannot validate the user's AD/Entra/Intune tenant, actual delegated rights, protected audit receipt or corporate signing. This guide closes that gap on a **designated disposable Windows 11 workstation** with approved test identities. The canary helper does **not** contact AD, Graph, GitHub or any external endpoint. It performs only local read-only startup/broker checks and lets the operator record status choices without free text.

## Obtain and verify

Download the exact-source **BitKeyBridge-v0.36.0-preview.3-QA-UNSIGNED** artifact from PR #117's passing GitHub Actions job. Compare the architecture ZIP SHA256 against `SHA256SUMS.txt` in that same QA bundle. Check `BUILD-INFO.txt` for the selected source SHA. The binary is unsigned test software: do not install it on a DC or production Helpdesk workstation, or deploy it through GPO, Intune or automatic updates.

Extract only the ZIP matching Windows architecture into an isolated QA folder, e.g. `C:\QA\BitKeyBridge\`. Run `BitKeyBridge.exe --version` and verify the response is exactly `0.36.0-preview.3`. An alternative is to run the packaged `BitKeyBridge-Win11-Canary.cmd` with the EXE's quoted absolute path.

## Run safely

From **ordinary non-elevated CMD**, in the folder containing the QA helper:

`BitKeyBridge-Win11-Canary.cmd "C:\QA\BitKeyBridge\BitKeyBridge.exe" /preflight`

For the guided manual checklist, substitute `/guided`. The script refuses a Windows Server/domain controller, non-Windows 11 client, elevated process, missing EXE or incorrect Preview version. It runs only `--version`, `--startup-diagnostics --no-elevation` and **read-only** `--audit-broker-preflight --no-elevation`. It does not install a service, grant permissions or invoke `--probe` and never reads LAPS/BitLocker secrets itself.

The helper creates a separate session folder under `%LOCALAPPDATA%\BitKeyBridge\Acceptance\` and reports the path. **Only `SAFE-RESULTS.txt` is a candidate to share after review.** It contains static PASS/FAIL/SKIP and version statuses, no user/device identifiers. The adjacent `startup-PRIVATE.json`, `broker-PRIVATE.json` and `version-PRIVATE.txt` are **local confidential records**. Do not upload them or full support bundles to GitHub.

## Two identity runs, same exact test device

Run separately under a real non-admin test account with approved delegated permissions, and then under a **different denied standard account**, preferably after signing out and back in (token group memberships do not update merely because AD group memberships changed).

1. From Start, search a qualified test computer and open Devices. Check the same AD/Entra/Intune identity, model and hardware without repeating a network search. An ambiguous or name-only Start record must not inherit a prior selection.
2. Connect Microsoft 365 **explicitly** through Device Code/MFA. Navigate Start, Devices and Device 360; there must not be another automatic MFA prompt while the same session remains valid. After a token/session change, only explicit reconnect can allow a new recovery read.
3. Verify a single Recovery ID may be preselected but multiple IDs require a deliberate operator choice. No key read occurs simply by selecting an ID.
4. Under the authorized user, manually exercise approved BitLocker and LAPS Reveal/Copy **only on a designated disposable/rotatable test device** and only when RBAC, ticket/JIT and protected pre-read audit are approved. The operator must never transcribe, print, screenshot, paste or export the secret into a ticket, terminal, test report or GitHub.
5. Under the denied user, verify Reveal/Copy fails closed and never exposes or copies a secret. A missing broker or missing backend permission is a failure to authorize, not a reason to bypass controls.
6. Try explicit read-only WMI under allowed and denied accounts; disconnected, identity-mismatched and partial-inventory cases must show typed reasons and no stale hardware evidence.
7. Verify EN/RU/HE and RTL with DPI 100%, 125%, 150%, 175%, 200% and RDP, including all Reveal/Copy buttons. If unavailable, mark SKIP rather than PASS.

## Approval

A passing CI run does not imply these human checks are complete. Only use the local summary alongside an authorized security review of audit/identity results. Before a production Stable release, confirm corporate Authenticode signer/publisher policy, signing-aware update and rollback, SIEM receipts, and appropriate approval. Retain Stable 0.35.0 until all gates are satisfied.

The canary helper's PASS/FAIL/SKIP choices are operator-entered **observations**, not technical attestations of backend security, secret validity or SIEM delivery.

## Packaging and CI limitations

The Windows 11 canary CMD and this guide are included in the exact-source PR #117 unsigned QA archive, alongside their SHA-256 entries. GitHub-hosted Windows Server validates the helper's help-mode parsing and exact allowlist of three read-only BitKeyBridge commands, **not** its Windows 11, AD/Intune or protected-audit behavior. Do not treat an unrun manual canary as PASS. The `/guided` script may exit 0 with skipped observations; reviewers must examine each recorded PASS/FAIL/SKIP and the restricted preflight result before approving any broader test.


## WinExe redirected-output fix

Preview.2 may print the correct version into the parent CMD console while its redirection file stays empty. Preview.3 preserves inherited pipe/file handles and CI checks both redirected SemVer and startup JSON. Use the matching Preview.3 EXE and Canary CMD from one exact-source QA bundle; do not mix versions.
