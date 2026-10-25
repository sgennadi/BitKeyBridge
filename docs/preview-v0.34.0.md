# BitKeyBridge 0.34.0-preview.1 — EN/RU/HE WinForms localization & layout acceptance

**Status:** development preview, NOT a GitHub Release. The published production-download baseline is v0.33.1; do not merge this development branch into Stable `main` or create a release tag until a separate explicit release review and source-pinned CI gate.

## Scope

1. Modal dialogs: retain the original English for recognized `TextBox.PlaceholderText` and `ToolStripItem.ToolTipText`, in addition to existing fixed captions; allow repeated RU → HE → EN and HE → RU transitions without retaining old-language hints.
2. Caller-provided runtime placeholders/tooltips, user-entered text, usernames, device identifiers and secret fields are NOT translated. The original known English source is replaced only when the owner explicitly supplies a different, catalogued English literal.
3. MainForm DataGridView/ListView columns: keep exact localized header text and minimum column widths, but avoid suspending/resuming a grid when no caption/width change is required. Cache only catalogue-approved header measurements, bounded to 512 entries per MainForm and separated by font/DPI/style. Do not touch cells, source records or the selected device. Non-secret UI telemetry reports cache hits/misses.
4. No Graph, AD, BitLocker, LAPS, audit, updater trust, admin authorization, machine assignment or signed-only policy changes.

## Required GitHub Actions QA before moving out of draft

- Multi-architecture Windows x64/x86/ARM64 build and strict PowerShell source regressions.
- Native `--ui-self-test`: modal placeholder and context-menu tooltip language roundtrips EN/RU/HE, dynamic hints preserved, menu `Tag` unchanged, populated Tenant ID unchanged, all startup and existing 100–200% DPI/RTL checks preserved.
- Existing offline `--self-test`, LAPS regressions and CodeQL.
- Manual Windows RDP / physical 100%, 150%, 175% and 200% text scaling: no blank glyphs or clipped labels when repeatedly switching English, Russian and Hebrew.
- Measure warm language-switch mean/p95, native tab handle churn, initial DPI/font refresh, visible-label batch counts and selected-page callback/reflow separately using non-secret diagnostics; do not claim speed improvement solely from static code inspection.

## Explicitly NOT authorized

No stable v0.34 GitHub Release, v0.34 release tag, GPO/Intune deployment or managed endpoint installation, installer signing policy relaxation, recovery-key reads, production tenant registration or privileged Graph consent is authorized by this preview document. Published v0.33.1 remains the rollback/download baseline.
