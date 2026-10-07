namespace BitKeyBridge;

public sealed class RemoteApiScopedTokenDialog : DpiAwareForm
{
    private readonly AppConfig _config;
    private readonly UiStatusLabel _readStatus = new();
    private readonly UiStatusLabel _coverageStatus = new();
    private readonly UiStatusLabel _exportStatus = new();
    private readonly TableLayoutPanel _scopeGrid = new();
    private readonly UiDiagnosticPanel _diagnostics = new();

    public RemoteApiScopedTokenDialog(
        AppConfig config)
    {
        _config = config;

        Text = "Remote API Scoped Tokens";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(780, 430);
        MinimumSize = new Size(650, 390);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = UiStyle.BodyFont;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Least-Privilege Remote API Tokens",
            Font = UiStyle.CreateDialogTitleFont(),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text =
                "The existing Admin token remains unchanged. Scoped tokens are shown only once; BitKeyBridge stores only SHA-256 hashes.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);

        _scopeGrid.Dock = DockStyle.Fill;
        _scopeGrid.AutoSize = true;
        _scopeGrid.ColumnCount = 4;
        _scopeGrid.RowCount = 3;
        _scopeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _scopeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _scopeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _scopeGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.Controls.Add(_scopeGrid, 0, 2);

        AddScopeRow(
            "Read",
            "Read-only access to Remote API GET endpoints.",
            "read",
            _readStatus,
            0);

        AddScopeRow(
            "CoverageRun",
            "GET access plus POST /api/v1/coverage/run when remote management is enabled.",
            "coverage-run",
            _coverageStatus,
            1);

        AddScopeRow(
            "Export",
            "GET access plus POST /api/v1/export when remote management is enabled.",
            "export",
            _exportStatus,
            2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        var close =
            UiStyle.CreateActionButton(
                "Close",
                DialogResult.Cancel);
        CancelButton =
            close;
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);
        root.Controls.Add(_diagnostics, 0, 3);
        root.Controls.Add(footer, 0, 4);

        RefreshStatuses();
    }

    private void AddScopeRow(
        string title,
        string description,
        string scope,
        UiStatusLabel status,
        int row)
    {
        var titleLabel = new Label
        {
            Text = title,
            Font = UiStyle.CreateEmphasisFont(10F),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(0, 8, 10, 14)
        };
        _scopeGrid.Controls.Add(titleLabel, 0, row);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 4, 10, 10)
        };
        details.Controls.Add(new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(380, 0)
        });
        UiStyle.ConfigureStatusLabel(
            status);
        status.AccessibleName =
            $"{title} token status";
        status.MaximumSize =
            new Size(
                380,
                0);
        status.Margin =
            new Padding(
                0,
                4,
                0,
                0);
        details.Controls.Add(status);
        _scopeGrid.Controls.Add(details, 1, row);

        var generate =
            UiStyle.CreateActionButton(
                "Generate");
        generate.Anchor =
            AnchorStyles.Top;
        generate.Margin =
            new Padding(
                0,
                4,
                UiStyle.ControlGap,
                10);

        var revoke =
            UiStyle.CreateActionButton(
                "Revoke");
        revoke.Anchor =
            AnchorStyles.Top;
        revoke.Margin =
            new Padding(
                0,
                4,
                0,
                10);

        generate.Click += (_, _) => Generate(scope);
        revoke.Click += (_, _) => Revoke(scope);

        _scopeGrid.Controls.Add(generate, 2, row);
        _scopeGrid.Controls.Add(revoke, 3, row);
    }

    private void Generate(
        string scope)
    {
        try
        {
            var result =
                new RemoteApiSetupService()
                    .GenerateScopedToken(
                        _config,
                        scope);

            RestartServiceIfRunning();

            new AuditService().Write(
                "GenerateRemoteApiScopedToken",
                source: "RemoteAPI",
                details:
                    $"Scope={result.Scope}");

            RefreshStatuses();

            using var secret =
                new SecretDisplayDialog(
                    $"Remote API Token - {result.Scope}",
                    "Save this bearer token now. BitKeyBridge stores only its SHA-256 hash and cannot display it again.",
                    result.Token,
                    $"Scope: {result.Scope}{Environment.NewLine}" +
                    $"API base URL: https://{Environment.MachineName}:{result.Port}/api/v1/");
            secret.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _diagnostics.ShowError(
                "Remote API scoped-token operation failed.",
                "RemoteApiScopedToken",
                ex);
        }
    }

    private void Revoke(
        string scope)
    {
        try
        {
            var normalized =
                RemoteApiSetupService.NormalizeScope(
                    scope);

            var answer = MessageBox.Show(
                this,
                $"Revoke the {normalized} Remote API token?",
                "Remote API Scoped Token",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return;

            new RemoteApiSetupService()
                .RevokeScopedToken(
                    _config,
                    normalized);

            RestartServiceIfRunning();

            new AuditService().Write(
                "RevokeRemoteApiScopedToken",
                source: "RemoteAPI",
                details:
                    $"Scope={normalized}");

            RefreshStatuses();
        }
        catch (Exception ex)
        {
            _diagnostics.ShowError(
                "Remote API scoped-token operation failed.",
                "RemoteApiScopedToken",
                ex);
        }
    }

    private void RefreshStatuses()
    {
        SetTokenStatus(
            _readStatus,
            _config.RemoteApiReadTokenSha256);
        SetTokenStatus(
            _coverageStatus,
            _config.RemoteApiCoverageRunTokenSha256);
        SetTokenStatus(
            _exportStatus,
            _config.RemoteApiExportTokenSha256);
    }

    private static void SetTokenStatus(
        UiStatusLabel status,
        string hash)
    {
        var configured =
            !string.IsNullOrWhiteSpace(
                hash);

        UiStyle.SetStatus(
            status,
            configured
                ? "Status: Configured"
                : "Status: Not configured",
            configured
                ? UiStatusKind.Success
                : UiStatusKind.Neutral);
    }

    private static void RestartServiceIfRunning()
    {
        var service =
            WindowsServiceHost.GetInfo();

        if (!service.Installed ||
            !string.Equals(
                service.State,
                "Running",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WindowsServiceHost.Stop();
        WindowsServiceHost.Start();
    }
}
