using System.Diagnostics;

namespace BitKeyBridge;

public sealed class DeviceCodeDialog : DpiAwareForm
{
    private readonly DeviceCodeInfo _info;
    private readonly TextBox _code = new();
    private readonly TextBox _verificationUrl = new();
    private readonly UiStatusLabel _status = new();

    public DeviceCodeDialog(
        DeviceCodeInfo info)
    {
        _info =
            info ??
            throw new ArgumentNullException(
                nameof(info));

        Text =
            "Microsoft Entra Device Code";
        StartPosition =
            FormStartPosition.CenterParent;
        ClientSize =
            new Size(
                620,
                330);
        MinimumSize =
            new Size(
                500,
                300);
        MaximizeBox =
            false;
        MinimizeBox =
            false;
        Font =
            UiStyle.BodyFont;

        var root =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Fill,
                AutoScroll =
                    true,
                Padding =
                    new Padding(
                        UiStyle.PagePadding),
                ColumnCount =
                    1,
                RowCount =
                    7
            };

        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        for (var index = 0;
             index < 7;
             index++)
        {
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
        }

        Controls.Add(
            root);

        root.Controls.Add(
            new Label
            {
                Text =
                    "Microsoft Entra sign-in",
                AutoSize =
                    true,
                Font =
                    UiStyle.CreateDialogTitleFont(),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.ControlGap)
            },
            0,
            0);

        root.Controls.Add(
            new Label
            {
                Text =
                    "Open the verification page, enter this code, complete sign-in, then return to BitKeyBridge.",
                AutoSize =
                    true,
                Dock =
                    DockStyle.Top,
                MaximumSize =
                    new Size(
                        560,
                        0),
                Margin =
                    new Padding(
                        0,
                        0,
                        0,
                        UiStyle.SectionGap)
            },
            0,
            1);

        var codeRow =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    2,
                RowCount =
                    1
            };

        codeRow.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        codeRow.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        _code.Name =
            "DeviceCodeValue";
        _code.AccessibleName =
            "Microsoft Entra device code";
        _code.ReadOnly =
            true;
        _code.Text =
            _info.UserCode;
        _code.Dock =
            DockStyle.Fill;
        _code.Font =
            UiStyle.CreateEmphasisFont(
                12F);
        _code.Margin =
            new Padding(
                0,
                0,
                UiStyle.ControlGap,
                0);

        var copy =
            UiStyle.CreateActionButton(
                "Copy code");
        copy.Name =
            "DeviceCodeCopyButton";
        copy.Click +=
            (_, _) =>
                CopyCode(
                    showResult: true);

        codeRow.Controls.Add(
            _code,
            0,
            0);
        codeRow.Controls.Add(
            copy,
            1,
            0);

        root.Controls.Add(
            codeRow,
            0,
            2);

        var urlRow =
            new TableLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                ColumnCount =
                    2,
                RowCount =
                    1,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        0)
            };

        urlRow.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));
        urlRow.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        _verificationUrl.Name =
            "DeviceCodeVerificationUrl";
        _verificationUrl.AccessibleName =
            "Microsoft Entra verification URL";
        _verificationUrl.ReadOnly =
            true;
        _verificationUrl.Text =
            _info.VerificationUri;
        _verificationUrl.Dock =
            DockStyle.Fill;
        _verificationUrl.Margin =
            new Padding(
                0,
                0,
                UiStyle.ControlGap,
                0);

        var open =
            UiStyle.CreateActionButton(
                "Open browser");
        open.Name =
            "DeviceCodeOpenBrowserButton";
        open.Click +=
            (_, _) =>
                OpenBrowser(
                    showResult: true);

        urlRow.Controls.Add(
            _verificationUrl,
            0,
            0);
        urlRow.Controls.Add(
            open,
            1,
            0);

        root.Controls.Add(
            urlRow,
            0,
            3);

        UiStyle.ConfigureStatusLabel(
            _status);
        _status.Name =
            "DeviceCodeStatus";
        _status.AccessibleName =
            "Microsoft Entra device-code status";
        UiStyle.SetStatus(
            _status,
            "The code remains visible and can be copied again at any time.",
            UiStatusKind.Neutral);
        root.Controls.Add(
            _status,
            0,
            4);

        root.Controls.Add(
            new Label
            {
                Text =
                    $"Code expires in approximately {Math.Max(1, _info.ExpiresIn / 60)} minute(s).",
                AutoSize =
                    true,
                Margin =
                    new Padding(
                        0,
                        UiStyle.ControlGap,
                        0,
                        UiStyle.SectionGap)
            },
            0,
            5);

        var buttons =
            new FlowLayoutPanel
            {
                Dock =
                    DockStyle.Top,
                AutoSize =
                    true,
                FlowDirection =
                    FlowDirection.RightToLeft,
                WrapContents =
                    true,
                Margin =
                    new Padding(0)
            };

        var ok =
            UiStyle.CreateActionButton(
                "OK",
                DialogResult.OK);
        buttons.Controls.Add(
            ok);
        root.Controls.Add(
            buttons,
            0,
            6);

        AcceptButton =
            ok;

        Shown +=
            (_, _) =>
            {
                var copied =
                    CopyCode(
                        showResult: false);
                var opened =
                    OpenBrowser(
                        showResult: false);

                UiStyle.SetStatus(
                    _status,
                    copied && opened
                        ? "The code was copied and the sign-in page was opened. Use Copy code or Open browser again if needed."
                        : copied
                            ? "The code was copied. The browser could not be opened automatically; use Open browser."
                            : opened
                                ? "The sign-in page was opened. The code could not be copied automatically; use Copy code."
                                : "Automatic copy/browser launch failed. Use Copy code and Open browser.",
                    copied && opened
                        ? UiStatusKind.Success
                        : UiStatusKind.Warning);

                _code.SelectAll();
                _code.Focus();
            };
    }

    private bool CopyCode(
        bool showResult)
    {
        try
        {
            Clipboard.SetText(
                _info.UserCode);

            if (showResult)
            {
                UiStyle.SetStatus(
                    _status,
                    "Device code copied to the clipboard.",
                    UiStatusKind.Success);
            }

            return true;
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Device Code clipboard copy failed: " +
                DiagnosticRedaction.Sanitize(
                    ex.Message),
                EventLogSeverity.Warning,
                4542,
                "Entra");

            if (showResult)
            {
                UiStyle.SetStatus(
                    _status,
                    "The code could not be copied. Select the code and press Ctrl+C.",
                    UiStatusKind.Warning);
            }

            return false;
        }
    }

    private bool OpenBrowser(
        bool showResult)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo(
                    _info.VerificationUri)
                {
                    UseShellExecute =
                        true
                });

            if (showResult)
            {
                UiStyle.SetStatus(
                    _status,
                    "Microsoft Entra sign-in page opened.",
                    UiStatusKind.Success);
            }

            return true;
        }
        catch (Exception ex)
        {
            WindowsEventLogService.TryWrite(
                "Device Code browser launch failed: " +
                DiagnosticRedaction.Sanitize(
                    ex.Message),
                EventLogSeverity.Warning,
                4543,
                "Entra");

            if (showResult)
            {
                UiStyle.SetStatus(
                    _status,
                    "The browser could not be opened automatically. Copy the URL and open it manually.",
                    UiStatusKind.Warning);
            }

            return false;
        }
    }
}
