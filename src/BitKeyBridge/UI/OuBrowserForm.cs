namespace BitKeyBridge;

public sealed class OuBrowserForm : DpiAwareForm
{
    private readonly TextBox _filter = new();
    private readonly ListBox _list = new();
    private List<BitLockerScope> _all = [];
    public BitLockerScope? SelectedScope => _list.SelectedItem as BitLockerScope;

    public OuBrowserForm()
    {
        Text = "Select Active Directory OU";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(950, 650);
        MinimumSize = new Size(620, 420);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var filterRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filterRow.Controls.Add(new Label
        {
            Text = "Filter:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 0)
        }, 0, 0);
        _filter.Dock = DockStyle.Fill;
        filterRow.Controls.Add(_filter, 1, 0);
        root.Controls.Add(filterRow, 0, 0);

        _list.Dock = DockStyle.Fill;
        _list.HorizontalScrollbar = true;
        _list.FormattingEnabled = true;
        _list.IntegralHeight = false;
        _list.Format += (_, e) =>
        {
            if (e.ListItem is BitLockerScope scope)
                e.Value = $"{scope.Name} — {scope.SearchBase}";
        };
        root.Controls.Add(_list, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(120, 34)
        };
        var ok = new Button
        {
            Text = "Select OU",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(130, 34)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = ok;
        CancelButton = cancel;
        _filter.TextChanged += (_, _) => ApplyFilter();
        _list.DoubleClick += (_, _) =>
        {
            if (_list.SelectedItem is not null)
                DialogResult = DialogResult.OK;
        };
    }

    public void LoadScopes(IEnumerable<BitLockerScope> scopes)
    {
        _all = scopes.ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = _filter.Text.Trim();
        var items = string.IsNullOrWhiteSpace(q)
            ? _all
            : _all.Where(x =>
                    x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.SearchBase.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in items)
            _list.Items.Add(item);
        _list.EndUpdate();

        if (_list.Items.Count > 0 && _list.SelectedIndex < 0)
            _list.SelectedIndex = 0;
    }
}
