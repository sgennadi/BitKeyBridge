# BitKeyBridge responsive UI polish and repaint reduction

## Goal

A calmer, clearer, native Windows 11-inspired administrative UI with fewer
per-frame operations during window drag, 4K/200% HiDPI scaling and RDP resize.

This feature does **not** alter recovery-key visibility, authorization, audit,
signing, automatic installation, backend services or privileged commands.

## Actual source-level bottlenecks removed

- `MainForm.ResponsiveViewport` previously executed repeated `PerformLayout()`
  on the whole shell, selected tab/page and card contents, then called
  `UiModernTheme.Refresh(shell)` for every `ClientSizeChanged`.
  This repeatedly walked the entire control tree and recreated Win32 rounded
  regions. The ordinary WinForms Dock=Fill layout already handles size changes.
- A 55-ms quiet-period debounce groups WM_SIZE bursts for only the responsive
  connection/recent and three-recovery-pane transitions. The manual layout pass
  now runs only when actual bounds/docking properties differ.
- `UiStyle.MakeVerticalWorkspaceResponsive` used to change child widths inside
  both `Layout` and `ClientSizeChanged` with no recursion guard. It now batches
  width changes under `SuspendLayout`, guards reentrancy, and resumes one
  ordinary final layout so AutoSize heights still update.
- `DpiAwareForm` previously remeasured all wrapping labels on **every**
  SizeChanged including height-only resizes and rebuilt the full theme during
  load/shown/DPI/font transitions. It now batches width-dependent measurement
  at 65 ms and full DPI/font refresh at 65 ms.
- `UiModernTheme.Apply` is now idempotent and O(1) for an already-themed root,
  while `ControlAdded` continues to theme new dynamically created controls.
- Rounded Win32 clipping regions remain on simple buttons and bordered
  surfaces, but are no longer applied to native textboxes, ComboBoxes, grids,
  ListViews or ProgressBars. Native caret hit-testing, scrolling and painting
  are preserved on mixed-DPI monitors and RDP.

## Visual polish

- The important non-secret actions get a consistent accent surface with
  readable hover/pressed states: **Connect to AD**, **Search**, **Save Update
  Settings**, **Update Now**, and the **Retry check** control.
- The selected tab gets a slim accent indicator and muted unselected tab text.
- Recovery password reveal/copy actions deliberately retain their separate
  security-specific button styling.
- All colors, radii and spacing are centralized under `UiStyle`. High Contrast
  and the existing native WinForms keyboard and accessibility behavior remain.

## Validation

The existing `--ui-self-test` continues to exercise the **real forms** under
Compact, LargeText 150%, Scale175, RDP 200% and 200% stress scenarios.
The new `tests/windows/UiRenderingRegression.ps1` adds CI assertions against
known recursive repaint patterns, loss of PerMonitorV2, unbounded child width
updates and global WS_EX_COMPOSITED.

UI timing improvements are **not** presented as measured benchmarks; a direct
before/after wall-clock comparison on real 4K/RDP hardware is still recommended.
Do not install this development branch automatically on managed endpoints.
