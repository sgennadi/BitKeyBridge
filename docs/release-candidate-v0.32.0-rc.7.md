# BitKeyBridge v0.32.0-rc.7 — nested WinForms viewport fix

**STATUS: SOURCE RELEASE CANDIDATE — NOT PUBLISHED.** This is an **unsigned, explicitly opt-in GitHub prerelease** candidate, not a university-signed or production-approved release. Existing `v0.32.0-rc.6` assets and tags must remain unchanged. The latest Stable/Latest release remains `v0.30.5`.

## Why RC7

The already published RC6 assets precede the WinForms layout correction merged in [PR #98](https://github.com/sgennadi/BitKeyBridge/pull/98). Replacing the RC6 binaries or retargeting its tag would break reproducibility. RC7 is a separate candidate from the verified post-fix source.

## Included fixes

- Diagnostics > Tools: action buttons and AutoSize section tables respect the live width of the parent scrolling workspace and wrap normally instead of remaining compressed in the upper-left corner.
- Main shell: only the **selected** nested `TabControl`/Dock=Fill hierarchy is repaired after a tab switch, window resize or RDP reconnect. No secret-handling controls are recreated.
- All primary workspaces: Start, BitLocker, LAPS, Devices, Helpdesk, Diagnostics and Settings, including Diagnostics > Tools and Settings > Enterprise, use their intended inner scrolling surfaces rather than a competing outer page scrollbar.
- Coalesced WM_SIZE and DPI/font handling remain intact. No full-tree theme refresh or `WS_EX_COMPOSITED` is introduced.
- Tests deliberately shrink the selected child viewport and confirm that production reflow restores width/height, that action buttons stay visible, and that AutoSize sections fill their parent across 100/150/175/200% simulated scale and compact/maximized sizes.

Source baseline: [post-fix RC6 commit `0225c8c`](https://github.com/sgennadi/BitKeyBridge/commit/0225c8ca0f6eaa748334bfd981a9f11230392e72).

## Mandatory automated release gates

The exact final RC7 commit must pass all checks in `.github/workflows/rc-0.32.0.yml`:

1. Source/metadata gate: `<Version>` and `InformationalVersion` = `0.32.0-rc.7`; numeric PE `FileVersion` = `0.32.0.0`; CHANGELOG and this guide exist; layout-fix entrypoints are present.
2. Windows .NET 10 `win-x64`, `win-x86`, `win-arm64` single-file release builds with actual machine architecture verification, ProductVersion check and one-root-`BitKeyBridge.exe` ZIP layout.
3. GPO templates, update hold/signer gates, SHA256/ZIP layout, UI repaint regression and actual `--self-test --no-elevation` and `--ui-self-test --no-elevation` on Windows x64.
4. LAPS regression and CodeQL; real two-unprivileged-identity Protected Audit Broker integration on a disposable GitHub-hosted Windows runner.
5. Real migration/rollback from the pinned legacy `v0.30.1` and `v0.30.3` packages on a disposable GitHub-hosted Windows runner.
6. Release package has precisely `BitKeyBridge-win-x64.zip`, `BitKeyBridge-win-x86.zip`, `BitKeyBridge-win-arm64.zip`, `SHA256SUMS.txt` and `BUILD-INFO.txt`. All published artifact digests are generated from this very workflow run.

CI success verifies automated checks, **not** physical 4K/RDP behavior or organizational sign-off.

## Hands-on Windows / RDP release acceptance

Record screenshots and a short pass/fail note from a real Windows 11 workstation and RDP client, using a non-admin helpdesk identity and, separately, a permitted admin identity where appropriate:

- Check 100% (1920x1080), 150% (2560x1440), 175%, and 200% (3840x2160) scaling. Maximize, restore, shrink and expand the window.
- Switch repeatedly Start > BitLocker > LAPS > Devices > Helpdesk > Diagnostics > Tools > Access Health and, as admin, Settings > Enterprise > Security / Export. Confirm that each selected content pane fills the available page width, action buttons are fully visible and scrollbars work.
- Resize an RDP window mid-session and reconnect at a different resolution. Verify no top-left clipped content or substantial redraw stalls.
- Switch English, Russian and Hebrew (RTL); inspect text wrap, visible buttons and high-contrast mode where available.
- Verify masked/reveal/copy BitLocker/LAPS permissions and audit acknowledgements using approved **test-only** records. Unauthorized users must be denied, without exporting or logging real recovery secrets.
- Confirm update opt-in, unsigned-package consent, scheduled updater disabled-by-default state, rollback hold and GPO-enforced denials are unchanged. Exercise rollback on an isolated test endpoint, not production.

**Go/no-go:** record passing CI run URL and SHA, pilot screenshots, authorized AD/Entra/Intune checks, CyberArk EPM allowlisting/signing status, Protected Audit / GPO acceptance and rollback readiness. Never imply that the GitHub unsigned test artifacts carry an institutional Authenticode signature.

## Safe publication procedure (manual, after approval)

1. Review and merge the RC7 preparation PR into the intended RC6-derived integration line. Confirm the exact final candidate source SHA and all GitHub checks. Do **not** merge a different source revision after recording acceptance.
2. The release workflow permits a publishing run **only** for a push to `release/v0.32.0-rc.7` where the **entire** head commit message is exactly `Release candidate v0.32.0-rc.7`. A non-publishing test/PR event cannot create a GitHub Release. A final empty commit with that exact message on the dedicated release branch is permitted after review; CI must re-run on that new immutable commit.
3. The publisher explicitly refuses to overwrite an existing `v0.32.0-rc.7` tag or release; inspect the source SHA in `BUILD-INFO.txt` and confirm the SHA256SUMS/ZIPs belong to the same successful final run.
4. Publish strictly as **prerelease** and retain Stable `v0.30.5`. Do not enable background installation, deploy to student/domain computers or assign any GPO/Intune ring as part of publication.
5. Only after separate organizational code-signing, CyberArk EPM, AD/Entra/Intune and support-pilot acceptance should a later production/stable rollout be considered.

**Current status:** release branch/tag creation and publication are intentionally held for separate approval. This preparation PR only stages source, documentation and CI gates.
