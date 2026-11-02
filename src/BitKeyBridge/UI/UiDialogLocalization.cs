using System.Runtime.CompilerServices;

namespace BitKeyBridge;

/// <summary>
/// One-time, exact-literal localization for modal WinForms dialogs. MainForm
/// uses its existing incremental shell translator; RecoveryWorkflowDialog
/// already uses an explicit caller-provided language. Input, secrets, device
/// identifiers, audit records and arbitrary status values are never translated.
/// </summary>
internal static class UiDialogLocalization
{
    private sealed class CaptionState
    {
        internal string Language = "en";
        internal string? English;
        // Last program-generated caption, not caller-owned runtime text.
        internal string? LastLocalizedCaption;
        // Only catalogued UI hints are translated. Remember their English
        // source and last generated value so HE -> RU -> EN roundtrips do
        // not retain stale localized placeholders or menu tooltips.
        internal string? OriginalPlaceholder;
        internal string? LastLocalizedPlaceholder;
        internal string? OriginalToolTip;
        internal string? LastLocalizedToolTip;
        internal bool Updating;
    }

    private static readonly ConditionalWeakTable<Control, CaptionState> Controls = new();
    private static readonly ConditionalWeakTable<ColumnHeader, CaptionState> Headers = new();
    private static readonly ConditionalWeakTable<DataGridViewColumn, CaptionState> GridHeaders = new();
    private static readonly ConditionalWeakTable<ToolStripItem, CaptionState> MenuItems = new();

    internal static void Apply(DpiAwareForm dialog, string? language = null)
    {
        if (dialog.IsDisposed || dialog is MainForm or RecoveryWorkflowDialog)
            return;

        var code = UiLanguageService.Normalize(language ?? UiLanguageService.ReadPreferred());
        var rtl = UiLanguageService.IsRightToLeft(code);
        dialog.SuspendLayout();
        try
        {
            // Modal dialogs can mirror when opened. Never mirror the MainForm
            // HWND or recovery dialogs with a separately supplied language.
            if (dialog.RightToLeftLayout != rtl)
                dialog.RightToLeftLayout = rtl;
            var direction = rtl ? RightToLeft.Yes : RightToLeft.No;
            if (dialog.RightToLeft != direction)
                dialog.RightToLeft = direction;
            ApplyControls(dialog, code);
        }
        finally
        {
            dialog.ResumeLayout(performLayout: true);
        }
    }

    private static bool IsFixedCaption(Control control) =>
        control is Form or Button or CheckBox or RadioButton or
            GroupBox or TabPage or LinkLabel ||
        control is Label and not UiStatusLabel;

    private static string? TranslatePreservedHint(
        string? current, string? previous, ref string? english, string code)
    {
        // ToolStripItem.ToolTipText can legitimately be null. Never turn a
        // missing/dynamic hint into a fabricated translatable caption.
        if (!string.Equals(current, previous, StringComparison.Ordinal))
            english = !string.IsNullOrEmpty(current) &&
                UiStaticCaptionCatalog.Contains(current) ? current : null;
        return english is null
            ? current
            : UiStaticCaptionCatalog.TranslateOrOriginal(code, english);
    }

    private static string? TranslatePreservedCaption(
        string? current, CaptionState state, string code)
    {
        // Headers and ToolStripItem.Text have no reliable TextChanged event.
        // If a caller replaces a caption with runtime content, drop the old
        // translation source instead of restoring a stale static label.
        var caption = TranslatePreservedHint(
            current, state.LastLocalizedCaption, ref state.English, code);
        state.LastLocalizedCaption = state.English is null ? null : caption;
        return caption;
    }

    private static void ApplyControls(Control control, string code)
    {
        if (control.IsDisposed || control.Disposing)
            return;

        if (!Controls.TryGetValue(control, out var state))
        {
            state = new CaptionState();
            Controls.Add(control, state);
            control.ControlAdded += OnControlAdded;
            if (IsFixedCaption(control))
                control.TextChanged += OnCaptionChanged;
            if (control is DataGridView grid)
                grid.ColumnAdded += OnGridColumnAdded;
        }
        state.Language = code;

        // Keep user-entered text, account names, URLs, GUIDs and passwords LTR
        // even when the surrounding dialog is visually mirrored.
        if (control is TextBoxBase or ComboBox or ListView or DataGridView or
            UiStatusLabel)
        {
            if (control.RightToLeft != RightToLeft.No)
                control.RightToLeft = RightToLeft.No;
        }

        if (IsFixedCaption(control))
        {
            if (UiStaticCaptionCatalog.Contains(control.Text))
                state.English = control.Text;
            UpdateCaption(control, state);
        }

        if (control is TextBox textBox)
        {
            var placeholder = TranslatePreservedHint(
                textBox.PlaceholderText, state.LastLocalizedPlaceholder,
                ref state.OriginalPlaceholder, code);
            if (!string.Equals(textBox.PlaceholderText, placeholder,
                    StringComparison.Ordinal))
                textBox.PlaceholderText = placeholder ?? string.Empty;
            state.LastLocalizedPlaceholder =
                state.OriginalPlaceholder is null ? null : placeholder;
        }

        if (control is ListView list)
            foreach (ColumnHeader header in list.Columns)
                ApplyHeader(header, code);

        if (control is DataGridView dataGrid)
            foreach (DataGridViewColumn column in dataGrid.Columns)
                ApplyGridHeader(column, code);

        if (control is ToolStrip toolStrip)
            foreach (ToolStripItem item in toolStrip.Items)
                ApplyMenuItem(item, code);
        if (control.ContextMenuStrip is { } contextMenu)
            foreach (ToolStripItem item in contextMenu.Items)
                ApplyMenuItem(item, code);

        foreach (Control child in control.Controls)
            ApplyControls(child, code);
    }

    private static void OnControlAdded(object? sender, ControlEventArgs e)
    {
        if (sender is Control parent && e.Control is { } child &&
            Controls.TryGetValue(parent, out var state))
            ApplyControls(child, state.Language);
    }

    private static void OnCaptionChanged(object? sender, EventArgs e)
    {
        if (sender is not Control control ||
            !Controls.TryGetValue(control, out var state) || state.Updating)
            return;

        // Dynamic results are never fed through the translation catalogue.
        state.English = UiStaticCaptionCatalog.Contains(control.Text)
            ? control.Text : null;
        UpdateCaption(control, state);
    }

    private static void UpdateCaption(Control control, CaptionState state)
    {
        if (state.English is not { } english)
            return;
        var text = UiStaticCaptionCatalog.TranslateOrOriginal(state.Language, english);
        if (string.IsNullOrWhiteSpace(text))
            return;
        state.Updating = true;
        try
        {
            if (!string.Equals(control.Text, text, StringComparison.Ordinal))
                control.Text = text;
            if (control is not Form)
            {
                var direction = UiLanguageService.IsRightToLeft(state.Language)
                    ? RightToLeft.Yes : RightToLeft.No;
                if (control.RightToLeft != direction)
                    control.RightToLeft = direction;
            }
        }
        finally { state.Updating = false; }
    }

    private static void OnGridColumnAdded(object? sender, DataGridViewColumnEventArgs e)
    {
        if (sender is DataGridView grid &&
            Controls.TryGetValue(grid, out var state))
            ApplyGridHeader(e.Column, state.Language);
    }

    private static void ApplyHeader(ColumnHeader header, string code)
    {
        if (!Headers.TryGetValue(header, out var state))
        {
            state = new CaptionState();
            Headers.Add(header, state);
        }
        state.Language = code;
        var translation = TranslatePreservedCaption(header.Text, state, code);
        if (state.English is not null && header.Text != translation)
            header.Text = translation ?? string.Empty;
    }

    private static void ApplyGridHeader(DataGridViewColumn header, string code)
    {
        if (!GridHeaders.TryGetValue(header, out var state))
        {
            state = new CaptionState();
            GridHeaders.Add(header, state);
        }
        state.Language = code;
        var translation = TranslatePreservedCaption(header.HeaderText, state, code);
        if (state.English is not null && header.HeaderText != translation)
            header.HeaderText = translation ?? string.Empty;
    }

    private static void ApplyMenuItem(ToolStripItem item, string code)
    {
        if (item is ToolStripSeparator or ToolStripTextBox or ToolStripComboBox)
            return;
        if (!MenuItems.TryGetValue(item, out var state))
        {
            state = new CaptionState();
            MenuItems.Add(item, state);
        }
        state.Language = code;
        var translation = TranslatePreservedCaption(item.Text, state, code);
        if (state.English is not null && item.Text != translation)
            item.Text = translation ?? string.Empty;

        var tooltip = TranslatePreservedHint(
            item.ToolTipText, state.LastLocalizedToolTip,
            ref state.OriginalToolTip, code);
        if (!string.Equals(item.ToolTipText, tooltip, StringComparison.Ordinal))
            item.ToolTipText = tooltip;
        state.LastLocalizedToolTip =
            state.OriginalToolTip is null ? null : tooltip;

        if (item is ToolStripDropDownItem menu)
            foreach (ToolStripItem child in menu.DropDownItems)
                ApplyMenuItem(child, code);
    }
}
