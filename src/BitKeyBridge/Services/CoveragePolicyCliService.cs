namespace BitKeyBridge;

public static class CoveragePolicyCliService
{
    public static int ShowStatus(AppConfig config)
    {
        try
        {
            PrintConfig(config);

            var status = CoverageReportService.ReadStatus();
            if (status is null)
            {
                Console.WriteLine("Last coverage status: not available");
                return 0;
            }

            var policy = new CoveragePolicyService(config)
                .Evaluate(status.Summary);

            Console.WriteLine(
                $"Last policy result: Compliant={policy.Compliant}; " +
                $"Errors={policy.ErrorCount}; Warnings={policy.WarningCount}; " +
                $"EvaluatedAtUtc={policy.EvaluatedAtUtc:O}");

            foreach (var violation in policy.Violations)
            {
                Console.WriteLine(
                    $"[{violation.Severity}] {violation.Code}: " +
                    $"Actual={violation.Actual}; Maximum={violation.Maximum}; " +
                    violation.Message);
            }

            return policy.ErrorCount > 0 ? 4 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Configure(AppConfig config, string[] args)
    {
        try
        {
            if (HasFlag(args, "--coverage-policy-enable") &&
                HasFlag(args, "--coverage-policy-disable"))
            {
                throw new ArgumentException(
                    "Specify either --coverage-policy-enable or --coverage-policy-disable, not both.");
            }

            if (HasFlag(args, "--coverage-policy-enable"))
                config.CoveragePolicyEnabled = true;
            if (HasFlag(args, "--coverage-policy-disable"))
                config.CoveragePolicyEnabled = false;

            ApplyInt(
                args,
                "--coverage-policy-max-no-key",
                value => config.CoveragePolicyMaxNoRecoveryKey = value);
            ApplyInt(
                args,
                "--coverage-policy-max-unencrypted",
                value => config.CoveragePolicyMaxIntuneNotEncrypted = value);
            ApplyInt(
                args,
                "--coverage-policy-max-stale",
                value => config.CoveragePolicyMaxIntuneStale = value);
            ApplyInt(
                args,
                "--coverage-policy-max-old-key",
                value => config.CoveragePolicyMaxOldCloudKey = value);

            ApplySeverity(
                args,
                "--coverage-policy-severity-no-key",
                value => config.CoveragePolicyNoRecoveryKeySeverity = value);
            ApplySeverity(
                args,
                "--coverage-policy-severity-unencrypted",
                value => config.CoveragePolicyIntuneNotEncryptedSeverity = value);
            ApplySeverity(
                args,
                "--coverage-policy-severity-stale",
                value => config.CoveragePolicyIntuneStaleSeverity = value);
            ApplySeverity(
                args,
                "--coverage-policy-severity-old-key",
                value => config.CoveragePolicyOldCloudKeySeverity = value);

            ConfigService.SaveAppConfig(config);

            var status = CoverageReportService.ReadStatus();
            if (status is not null)
            {
                status.Policy =
                    new CoveragePolicyService(config)
                        .Evaluate(status.Summary);
                JsonStore.WriteAtomic(
                    AppPaths.CoverageStatusFile,
                    status);
            }

            PrintConfig(config);
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void PrintConfig(AppConfig config)
    {
        Console.WriteLine(
            $"Coverage policy: Enabled={config.CoveragePolicyEnabled}");
        Console.WriteLine(
            $"NoRecoveryKey: Max={Math.Max(0, config.CoveragePolicyMaxNoRecoveryKey)}; " +
            $"Severity={CoveragePolicyService.NormalizeSeverity(config.CoveragePolicyNoRecoveryKeySeverity)}");
        Console.WriteLine(
            $"IntuneNotEncrypted: Max={Math.Max(0, config.CoveragePolicyMaxIntuneNotEncrypted)}; " +
            $"Severity={CoveragePolicyService.NormalizeSeverity(config.CoveragePolicyIntuneNotEncryptedSeverity)}");
        Console.WriteLine(
            $"IntuneStale: Max={Math.Max(0, config.CoveragePolicyMaxIntuneStale)}; " +
            $"Severity={CoveragePolicyService.NormalizeSeverity(config.CoveragePolicyIntuneStaleSeverity)}");
        Console.WriteLine(
            $"OldCloudKey: Max={Math.Max(0, config.CoveragePolicyMaxOldCloudKey)}; " +
            $"Severity={CoveragePolicyService.NormalizeSeverity(config.CoveragePolicyOldCloudKeySeverity)}");
    }

    private static void ApplyInt(
        IReadOnlyList<string> args,
        string option,
        Action<int> setter)
    {
        var value = GetOptionValue(args, option);
        if (value is null)
            return;

        if (!int.TryParse(value, out var parsed) ||
            parsed < 0 ||
            parsed > 1000000)
        {
            throw new ArgumentException(
                $"{option} must be an integer between 0 and 1000000.");
        }

        setter(parsed);
    }

    private static void ApplySeverity(
        IReadOnlyList<string> args,
        string option,
        Action<string> setter)
    {
        var value = GetOptionValue(args, option);
        if (value is null)
            return;

        var normalized =
            CoveragePolicyService.NormalizeSeverity(value);

        if (!value.Equals("Error", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("Critical", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("Warning", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("Info", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("Information", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"{option} must be Error, Warning, or Info.");
        }

        setter(normalized);
    }

    private static string? GetOptionValue(
        IReadOnlyList<string> args,
        string option)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Count)
                throw new ArgumentException(
                    $"{option} requires a value.");

            return args[i + 1];
        }

        return null;
    }

    private static bool HasFlag(
        IReadOnlyCollection<string> args,
        string flag) =>
        args.Any(x => x.Equals(flag, StringComparison.OrdinalIgnoreCase));
}
