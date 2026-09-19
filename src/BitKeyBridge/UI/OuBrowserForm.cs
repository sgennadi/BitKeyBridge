namespace BitKeyBridge;

public sealed class OuBrowserForm : Form
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
        MinimumSize = new Size(700, 450);

        var label = new Label { Text = "Filter:", Left = 12, Top = 16, AutoSize = true };
        _filter.SetBounds(65, 12, 855, 26);
        _list.SetBounds(12, 48, 908, 510);
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _list.HorizontalScrollbar = true;
        _list.DisplayMember = nameof(BitLockerScope.SearchBase);
        var ok = new Button { Text = "Add selected OU", DialogResult = DialogResult.OK, Width = 130, Height = 32 };
        ok.SetBounds(650, 570, 130, 32);
        ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 130, Height = 32 };
        cancel.SetBounds(790, 570, 130, 32);
        cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        Controls.AddRange([label, _filter, _list, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        _filter.TextChanged += (_, _) => ApplyFilter();
        _list.DoubleClick += (_, _) => { if (_list.SelectedItem is not null) DialogResult = DialogResult.OK; };
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
            : _all.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.SearchBase.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in items) _list.Items.Add(item);
        _list.EndUpdate();
    }
}
