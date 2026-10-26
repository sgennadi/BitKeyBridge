# BitKeyBridge 0.34.0-preview.1 — EN/RU/HE WinForms localization & layout acceptance

**Status:** development preview, NOT a GitHub Release. The published production-download baseline is v0.33.1; do not merge this development branch into Stable `main` or create a release tag until a separate explicit release review and source-pinned CI gate.

## Scope

1. Modal dialogs: retain the original English for recognized `TextBox.PlaceholderText` and `ToolStripItem.ToolTipText`, in addition to existing fixed captions; allow repeated RU → HE → EN and HE → RU transitions without retaining old-language hints.
2. Caller-provided runtime placeholders/tooltips, user-entered text, usernames, device identifiers and secret fields are NOT translated. The original known English source is replaced only when the owner explicitly supplies a different, catalogued English literal.
3. MainForm DataGridView/ListView columns: keep exact localized header text and minimum column widths, but avoid suspending/resuming a grid when no caption/width change is required. Cache only catalogue-approved header measurements, bounded to 512 entries per MainForm and separated by font/DPI/style. Do not touch cells, source records or the selected device. Non-secret UI telemetry reports cache hits/misses.
4. No Graph, AD, BitLocker, LAPS, audit, updater trust, admin authorization, machine assignment or signed-only policy changes.
5. Main menu and native grid/list column localization must honor caller changes made after a translated static caption. Menu TextBox/ComboBox values and ToolStripItem.Tag remain untouched; replacing an item with a NEW catalogued English caption is allowed and then translated. Runtime values must never enter localization caches or diagnostics.
6. Native WinForms HWND recreation on DPI/RTL/RDP resets posted callback generations; stale callbacks must not discard the new selected page or strand visible label widths.

## Required GitHub Actions QA before moving out of draft

- Multi-architecture Windows x64/x86/ARM64 build and strict PowerShell source regressions.
- Native `--ui-self-test`: modal placeholder and context-menu tooltip language roundtrips EN/RU/HE, dynamic hints preserved, menu `Tag` unchanged, populated Tenant ID unchanged, all startup and existing 100–200% DPI/RTL checks preserved.
- Existing offline `--self-test`, LAPS regressions and CodeQL.
- Manual Windows RDP / physical 100%, 150%, 175% and 200% text scaling: no blank glyphs or clipped labels when repeatedly switching English, Russian and Hebrew.
- Direction-specific latency diagnostics must report bounded recent p50/p95 for EN/RU to Hebrew, Hebrew to EN/RU and EN/RU-only switches, plus tab mirror, captions and layout resume. Compare p50/p95 and native handle counts across physical Windows and Hebrew RDP; do not collect device, account, tenant or secret data in timings.
- Measure warm language-switch mean/p95, native tab handle churn, initial DPI/font refresh, visible-label batch counts and selected-page callback/reflow separately using non-secret diagnostics; do not claim speed improvement solely from static code inspection.
- Native navigation burst test must prove three queued tab updates post one callback, run only the latest visible-page repair, and permit a second burst; compare actual tab switching p50/p95 before/after optimization.
- Real UI navigation regression cycles Start, BitLocker, and LAPS, then a nested no-timing request; verify the main switch timer survives, the final tab is repaired and the TabPages/TabControl native handle are not recreated.

## Explicitly NOT authorized

No stable v0.34 GitHub Release, v0.34 release tag, GPO/Intune deployment or managed endpoint installation, installer signing policy relaxation, recovery-key reads, production tenant registration or privileged Graph consent is authorized by this preview document. Published v0.33.1 remains the rollback/download baseline.
