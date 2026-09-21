namespace BitKeyBridge;

public sealed class RemoteApiScopedTokenDialog : Form
{
    private readonly AppConfig _config;
    private readonly Label _readStatus = new();
    private readonly Label _coverageStatus = new();
    private readonly Label _exportStatus = new();

    public RemoteApiScopedTokenDialog(
        AppConfig config)
    {
        _config = config;

        Text = "Remote API Scoped Tokens";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 390);
        MinimumSize = new Size(720, 360);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        Controls.Add(new Label
        {
            Text = "Least-Privilege Remote API Tokens",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Left = 18,
            Top = 16
        });

        Controls.Add(new Label
        {
            Text =
                "The existing Admin token remains unchanged. Scoped tokens are shown only once; BitKeyBridge stores only SHA-256 hashes.",
            Left = 20,
            Top = 55,
            Width = 710,
            Height = 42
        });

        AddScopeRow(
            "Read",
            "Read-only access to Remote API GET endpoints.",
            "read",
            _readStatus,
            110);

        AddScopeRow(
            "CoverageRun",
            "GET access plus POST /api/v1/coverage/run when remote management is enabled.",
            "coverage-run",
            _coverageStatus,
            190);

        AddScopeRow(
            "Export",
            "GET access plus POST /api/v1/export when remote management is enabled.",
            "export",
            _exportStatus,
            270);

        var close = new Button
        {
            Text = "Close",
            Left = 630,
            Top = 342,
            Width = 100,
            Height = 32
        };
        close.Click += (_, _) => Close();
        Controls.Add(close);

        RefreshStatuses();
    }

    private void AddScopeRow(
        string title,
        string description,
        string scope,
        Label status,
        int top)
    {
        Controls.Add(new Label
        {
            Text = title,
            Font = new Font(
                "Segoe UI Semibold",
                10F),
            Left = 20,
            Top = top,
            Width = 110,
            Height = 24
        });

        Controls.Add(new Label
        {
            Text = description,
            Left = 130,
            Top = top,
            Width = 380,
            Height = 36
        });

        status.SetBounds(
            20,
            top + 35,
            220,
            24);
        Controls.Add(status);

        var generate = new Button
        {
            Text = "Generate",
            Left = 520,
            Top = top,
            Width = 95,
            Height = 32
        };
        var revoke = new Button
        {
            Text = "Revoke",
            Left = 625,
            Top = top,
            Width = 95,
            Height = 32
        };

        generate.Click += (_, _) =>
            Generate(scope);
        revoke.Click += (_, _) =>
            Revoke(scope);

        Controls.AddRange([
            generate,
            revoke
        ]);
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
            MessageBox.Show(
                this,
                ex.Message,
                "Remote API Scoped Token",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
            MessageBox.Show(
                this,
                ex.Message,
                "Remote API Scoped Token",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshStatuses()
    {
        _readStatus.Text =
            "Status: " +
            State(
                _config.RemoteApiReadTokenSha256);
        _coverageStatus.Text =
            "Status: " +
            State(
                _config.RemoteApiCoverageRunTokenSha256);
        _exportStatus.Text =
            "Status: " +
            State(
                _config.RemoteApiExportTokenSha256);
    }

    private static string State(
        string hash) =>
        string.IsNullOrWhiteSpace(hash)
            ? "Not configured"
            : "Configured";

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
