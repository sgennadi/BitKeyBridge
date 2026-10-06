# BitKeyBridge UI Standard

This document defines the reusable UI baseline for BitKeyBridge and the preferred
WinForms style for related administrative utilities.

## Visual language

- Use a clean Windows-native administrative UI. Prefer system colors and standard
  controls over decorative chrome.
- Body text uses the Windows message-box/system UI font through `UiStyle.BodyFont`.
- Page titles use Segoe UI Semibold 18 pt.
- Section titles use Segoe UI Semibold 14 pt.
- Monospace text is reserved for recovery IDs, hashes, keys, paths, logs and other
  machine-oriented values.
- Use the shared spacing scale: 8 px control gap, 12 px section gap and 18 px page
  padding at the 96-DPI logical baseline.
- Primary and secondary buttons use `AutoSize` with a minimum logical size of
  88 x 32 and shared padding.
- Action/dialog buttons are created through `UiStyle.CreateActionButton` or
  explicitly configured through the shared button style. Direct per-window
  `new Button` construction is prohibited.
- Modal dialogs with a clear primary action provide standard keyboard semantics:
  Enter invokes the primary action and Escape invokes Cancel/Close.
- Connection/source selectors and their primary action should use balanced
  logical widths in the same `TableLayoutPanel`; long connection/error text belongs
  on a separate full-width status row rather than between controls.
- Status surfaces use the shared `UiStatusKind` / `UiStyle.ApplyStatusLabel`
  states: Neutral, Busy, Success, Warning and Error. Do not invent per-window
  status colors.
- Status surfaces use DPI-safe vector glyphs from the shared UI layer. Do not add
  fixed-resolution PNG status icons; glyphs must be regenerated for the active
  monitor DPI and remain sharp at 200% scaling.
- Informational status surfaces are not part of keyboard tab navigation and must
  expose an AccessibleName and AccessibleDescription.
- Status text and semantic state must be updated together through the shared
  status API; screen-reader descriptions must never lag behind visible text.
- Windows High Contrast is mandatory: status backgrounds/foregrounds fall back
  to SystemColors.Window/SystemColors.WindowText, while state remains
  distinguishable by vector glyph shape and text rather than color alone.
- Direct Segoe UI / Consolas construction outside `UiStyle` is not allowed.
  Typography changes must flow through the shared style tokens.

## HiDPI and resizing

- The application must remain `PerMonitorV2` DPI aware.
- Every application form must inherit `DpiAwareForm`.
- Forms use `AutoScaleMode.Dpi` with a 96-DPI design baseline.
- Layout must remain usable when moving a window between monitors with different
  scale factors.
- Forms are constrained to the current monitor working area. Scrolling is preferred
  to clipping.
- Horizontal action/status `FlowLayoutPanel` rows must be allowed to wrap.
- Vertical workspaces resize their child sections to the available client width.
- Arbitrary user-facing text must never depend on a fixed pixel width to remain
  visible.
- Fixed logical widths are acceptable for bounded machine-oriented fields (for
  example a numeric selector or recovery-ID selector) only when the containing
  layout can wrap or scroll.
- Do not use `Left`, `Top`, `SetBounds`, absolute positioning, or fixed button
  width/height.
- Prefer `TableLayoutPanel`, `FlowLayoutPanel`, `Dock`, `Anchor`, `AutoSize`,
  and percentage columns.

## Long text

- Labels with explanatory/status text must use `AutoSize` and live in a layout
  that can resize or wrap.
- Paths, identifiers and secrets that users may need to copy should use selectable
  text controls instead of truncated labels.
- Logs/details use resizable multiline controls with scroll bars.
- Never rely on ellipsis for security-relevant values.

## Minimum validation matrix

Every significant GUI change should be reviewed against these scenarios:

| Scenario | Resolution / scale |
| --- | --- |
| Standard desktop | 1920 x 1080 @ 100% |
| Common laptop | 1920 x 1080 @ 125% |
| HiDPI laptop | 2560 x 1440 @ 150% |
| 4K desktop | 3840 x 2160 @ 150% |
| 4K high scaling | 3840 x 2160 @ 200% |
| Runtime stress | compact + 150% + 200% large-text/layout validation |
| Accessibility | keyboard order + status metadata + High Contrast palette |
| Compact / RDP | 1366 x 768 @ 100-150% |
| Accessibility | Windows text size 125-150% |

Acceptance criteria:

- No caption, status, checkbox label, button text or dialog action is clipped.
- No modal dialog opens outside the working area.
- Essential actions remain reachable without changing Windows scaling.
- Horizontal action rows wrap rather than disappear beyond the right edge.
- Main data grids and log/detail areas receive remaining space rather than forcing
  the entire form beyond the screen.
- Moving the application between mixed-DPI monitors does not leave a form at the
  old physical size or outside the visible work area.

## CI invariants

The build checks enforce:

- `ApplicationHighDpiMode=PerMonitorV2`.
- `DpiAwareForm` uses DPI scaling.
- Application forms do not inherit directly from `Form`.
- Fixed-position WinForms layout is rejected.
- Fixed button Width/Height declarations are rejected.
- Direct Segoe UI / Consolas construction outside `UiStyle` is rejected.
- Direct `new Button` construction outside `UiStyle` is rejected.
- Runtime validation checks action-button AutoSize/minimum height and modal
  Enter/Escape behavior in addition to clipping and DPI layout.
- The published win-x64 executable runs `--ui-self-test`, which constructs the
  real forms and validates compact and 150% large-text responsive-layout invariants.

When a layout needs an exception, prefer changing the layout architecture rather
than weakening these checks.
