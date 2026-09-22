using System.Text.Json;

namespace BitKeyBridge;

public static class PrivilegedAccessCliService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

    public static int ShowStatus(
        AppConfig config)
    {
        try
        {
            var status =
                new PrivilegedAccessPolicyService(
                    config)
                    .GetStatus();

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    JsonOptions));

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int GrantJit(
        AppConfig config,
        string subject,
        string? reason)
    {
        try
        {
            var grant =
                new JitRecoveryService(
                    config)
                    .CreateGrant(
                        subject,
                        reason);

            Console.WriteLine(
                JsonSerializer.Serialize(
                    grant,
                    JsonOptions));

            return 0;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 4;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int RevokeJit(
        AppConfig config,
        string grantId,
        string? reason)
    {
        try
        {
            var revoked =
                new JitRecoveryService(
                    config)
                    .RevokeGrant(
                        grantId,
                        reason);

            Console.WriteLine(
                revoked
                    ? "JIT grant revoked."
                    : "JIT grant was not found.");

            return revoked
                ? 0
                : 3;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 4;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int ShowApproval(
        AppConfig config,
        string sessionId)
    {
        try
        {
            var status =
                new RecoveryApprovalService(
                    config)
                    .GetStatus(
                        sessionId);

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    JsonOptions));

            return status.Approved
                ? 0
                : status.Status ==
                  "Pending"
                    ? 5
                    : 4;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int DecideApproval(
        AppConfig config,
        string sessionId,
        bool approved,
        string? comment)
    {
        try
        {
            var decision =
                new RecoveryApprovalService(
                    config)
                    .Decide(
                        sessionId,
                        approved,
                        comment);

            Console.WriteLine(
                JsonSerializer.Serialize(
                    decision,
                    JsonOptions));

            return approved
                ? 0
                : 5;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 4;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int ShowSiemStatus(
        AppConfig config)
    {
        try
        {
            var status =
                new SiemForwardingService(
                    config)
                    .CheckReadiness();

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    JsonOptions));

            return status.Ready
                ? 0
                : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static async Task<int> FlushSiemAsync(
        AppConfig config)
    {
        try
        {
            var status =
                await new SiemForwardingService(
                        config)
                    .FlushAsync();

            Console.WriteLine(
                JsonSerializer.Serialize(
                    status,
                    JsonOptions));

            return status.Ready &&
                   string.IsNullOrWhiteSpace(
                       status.LastError)
                ? 0
                : 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    public static int ApplySettings(
        AppConfig config,
        string[] args)
    {
        try
        {
            if (!SecurityContext.IsAdministrator())
            {
                throw new InvalidOperationException(
                    "Administrator rights are required to change privileged-access settings.");
            }

            if (Has(
                    args,
                    "--jit-enable"))
            {
                config.JitRecoveryEnabled =
                    true;
            }

            if (Has(
                    args,
                    "--jit-disable"))
            {
                config.JitRecoveryEnabled =
                    false;
            }

            ApplyInt(
                args,
                "--jit-minutes",
                1,
                1440,
                x =>
                    config.JitRecoveryGrantMinutes =
                        x);

            ApplyPrincipalChange(
                args,
                "--jit-grantor-add",
                config.RbacJitGrantors,
                add: true);

            ApplyPrincipalChange(
                args,
                "--jit-grantor-remove",
                config.RbacJitGrantors,
                add: false);

            if (Has(
                    args,
                    "--approval-enable"))
            {
                config.TwoPersonApprovalEnabled =
                    true;
            }

            if (Has(
                    args,
                    "--approval-disable"))
            {
                config.TwoPersonApprovalEnabled =
                    false;
            }

            ApplyInt(
                args,
                "--approval-minutes",
                1,
                1440,
                x =>
                    config.TwoPersonApprovalMinutes =
                        x);

            ApplyPrincipalChange(
                args,
                "--approver-add",
                config.RbacRecoveryApprovers,
                add: true);

            ApplyPrincipalChange(
                args,
                "--approver-remove",
                config.RbacRecoveryApprovers,
                add: false);

            if (Has(
                    args,
                    "--siem-enable"))
            {
                config.SiemEnabled =
                    true;
            }

            if (Has(
                    args,
                    "--siem-disable"))
            {
                config.SiemEnabled =
                    false;
            }

            ApplyString(
                args,
                "--siem-mode",
                x =>
                    config.SiemMode =
                        SiemForwardingService
                            .NormalizeMode(
                                x));

            ApplyString(
                args,
                "--siem-file",
                x =>
                    config.SiemFilePath =
                        x);

            ApplyString(
                args,
                "--siem-webhook",
                x =>
                    config.SiemWebhookUrl =
                        x);

            ApplyString(
                args,
                "--siem-client-cert",
                x =>
                    config.SiemClientCertificateThumbprint =
                        x);

            ApplyInt(
                args,
                "--siem-timeout-seconds",
                2,
                120,
                x =>
                    config.SiemWebhookTimeoutSeconds =
                        x);

            ApplyInt(
                args,
                "--siem-flush-minutes",
                1,
                1440,
                x =>
                    config.SiemFlushIntervalMinutes =
                        x);

            ApplyInt(
                args,
                "--siem-max-outbox",
                100,
                100000,
                x =>
                    config.SiemMaxOutboxEvents =
                        x);

            if (Has(
                    args,
                    "--siem-fail-closed"))
            {
                config.SiemFailClosed =
                    true;
            }

            if (Has(
                    args,
                    "--siem-fail-open"))
            {
                config.SiemFailClosed =
                    false;
            }

            ConfigurationMaintenanceService
                .ValidateAppConfig(
                    config);

            ConfigService.SaveAppConfig(
                config);

            RestartServiceIfRunning();

            Console.WriteLine(
                "Privileged-access settings saved.");

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                ex.Message);
            return 1;
        }
    }

    private static bool Has(
        IReadOnlyList<string> args,
        string option) =>
        args.Any(
            x =>
                x.Equals(
                    option,
                    StringComparison.OrdinalIgnoreCase));

    private static void ApplyString(
        IReadOnlyList<string> args,
        string option,
        Action<string> apply)
    {
        var value =
            GetValue(
                args,
                option);

        if (value is not null)
            apply(value);
    }

    private static void ApplyInt(
        IReadOnlyList<string> args,
        string option,
        int minimum,
        int maximum,
        Action<int> apply)
    {
        var value =
            GetValue(
                args,
                option);

        if (value is null)
            return;

        if (!int.TryParse(
                value,
                out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new ArgumentException(
                $"{option} requires an integer between {minimum} and {maximum}.");
        }

        apply(parsed);
    }

    private static void ApplyPrincipalChange(
        IReadOnlyList<string> args,
        string option,
        List<string> values,
        bool add)
    {
        var value =
            GetValue(
                args,
                option);

        if (value is null)
            return;

        var normalized =
            value.Trim();

        if (string.IsNullOrWhiteSpace(
                normalized))
        {
            throw new ArgumentException(
                $"{option} requires a Windows user/group or SID.");
        }

        _ =
            AuthorizationService
                .ResolvePrincipalSid(
                    normalized);

        if (add)
        {
            if (!values.Any(x =>
                    x.Equals(
                        normalized,
                        StringComparison.OrdinalIgnoreCase)))
            {
                values.Add(
                    normalized);
            }
        }
        else
        {
            values.RemoveAll(
                x =>
                    x.Equals(
                        normalized,
                        StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string? GetValue(
        IReadOnlyList<string> args,
        string option)
    {
        for (var i = 0;
             i < args.Count;
             i++)
        {
            if (!args[i].Equals(
                    option,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Count)
            {
                throw new ArgumentException(
                    $"{option} requires a value.");
            }

            return args[i + 1];
        }

        return null;
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
