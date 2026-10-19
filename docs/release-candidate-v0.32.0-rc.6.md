# BitKeyBridge v0.32.0-rc.6 — smooth Windows UI preview

**Unsigned, opt-in prerelease. NOT production-approved.**
Stable GitHub Latest remains **v0.30.5**. RC6 does not add devices to an
automatic-update group, change a GPO, install an endpoint task or grant
unsigned-binary consent. Publisher signing and organizational CyberArk EPM,
AD/Entra/Intune and audit acceptance still need separate approval.

## What's new in RC6

This preview builds on RC5's signed-publisher/GPO-aware Update Center 2.0,
rollback holds, metadata-first BitLocker/LAPS and two-standard-user protected
audit integration. The functional privilege and secret-handling paths are unchanged.

- The main WinForms shell no longer recursively calls PerformLayout on every
  Resize or refreshes the entire theme/Win32 clipping regions at every WM_SIZE.
- A 55-ms idle debounce batches wide/narrow Home card layout decisions during
  resize/RDP reconnect, while native Dock=Fill continues resizing immediately.
- A 65-ms debounce reduces repeated DPI/font layout and wrapping-label work.
- Vertical FlowLayoutPanel child-width changes are guarded against recursive
  Layout events; one final layout still recalculates proper AutoSize height.
- The theme now treats existing controls once, with ControlAdded handling
  dynamic controls. Native text boxes, drop-downs, grid/list views and
  progress bars retain their standard lightweight unclipped Windows HWNDs.
- Selected tabs show a slim accent indicator; primary non-secret actions have
  a consistent blue accent and accessible hover state. Recovery password
  reveal/copy controls deliberately keep their existing styling.
- UI respects High Contrast and RDP/4K scale changes; user preferences and
  EN/RU/HE labels remain. There are no new bitmap assets.

These changes target a source-identified excess of synchronous repaints.
Improved end-user latency is not claimed as a measured benchmark: it should
be compared before/after on a real workstation at 100%, 150% and 200% scaling
and through RDP.

## Publication criteria

`.github/workflows/rc-0.32.0.yml` publishes **only** from exact
`release/v0.32.0-rc.6` commit message
`Release candidate v0.32.0-rc.6` when every required job succeeds.

- Verify pinned source, ProductVersion 0.32.0-rc.6 and numeric
  FileVersion 0.32.0.0; inspect architecture of x64/x86/ARM64 PE files.
- Validate one-root-EXE ZIP structure, SHA-256 hashes, GPO policy resources,
  UI-rendering source regression, Windows offline and real-form 100–200%
  HiDPI tests, LAPS and CodeQL.
- Use disposable GitHub-hosted Windows runners to verify protected journal
  under **two real unprivileged identities** and upgrade/rollback from
  pinned v0.30.1/v0.30.3, with synthetic non-secret settings.
- Produce exact BitKeyBridge-win-x64.zip, -x86.zip, -arm64.zip,
  SHA256SUMS.txt and BUILD-INFO.txt. Never overwrite a tag, replace Stable
  or silently install RC6.

The published executables have no institutional Authenticode signature.
A hash verifies integrity against the downloaded metadata, not developer
identity. Corporate deployment must wait for approved signing and acceptance.

## Tester workflow

On an approved isolated test computer, download the ZIP corresponding to its
Windows architecture and compare SHA-256 with the release's SHA256SUMS.txt.
Run BitKeyBridge.exe --self-test --no-elevation and
BitKeyBridge.exe --ui-self-test --no-elevation before user testing.
Compare window resize, tab switching, wrapped captions and update settings
on 1920×1080/100%, 2560×1440/150%, 4K/200% and an RDP session whose
resolution changes mid-session. Confirm BitLocker/LAPS secrets remain hidden
until the operator explicitly requests authorized reveal/copy.

The nightly/scheduled updater still requires **separate explicit**
prerelease selection and trusted publisher or conscious unsigned official
GitHub consent, subject to central GPO restrictions and rollback holds.
See [RC5 policy and updater notes](release-candidate-v0.32.0-rc.5.md) and
[UI repaint/HiDPI changes](ui-rendering-performance-2026-10.md).
