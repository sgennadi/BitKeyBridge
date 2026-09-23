using System.Security.Principal;

namespace BitKeyBridge;

public static class RbacCliService
{
    public static int ShowStatus(AppConfig config)
    {
        try
        {
            var auth = new AuthorizationService(config);
            var read = auth.Check(BitKeyBridgePermission.RecoveryRead);
            var rotate = auth.Check(BitKeyBridgePermission.Rotate);
            var admin = auth.Check(BitKeyBridgePermission.Administrator);
            var validation = auth.ValidateConfiguredPrincipals();

            Console.WriteLine($"RBAC enabled: {config.RbacEnabled}");
            Console.WriteLine(
                $"Local Administrators bypass: {config.RbacAllowLocalAdministrators}");
            Console.WriteLine(
                $"Current identity: {AuthorizationService.CurrentIdentityName()}");
            Console.WriteLine(
                "Recovery readers: " +
                FormatPrincipals(config.RbacRecoveryReaders));
            Console.WriteLine(
                "Rotation operators: " +
                FormatPrincipals(config.RbacRotationOperators));
            Console.WriteLine(
                "BitKeyBridge administrators: " +
                FormatPrincipals(config.RbacAdministrators));
            Console.WriteLine(
                $"RecoveryRead: Allowed={read.Allowed}; Matched={read.MatchedPrincipal}; Reason={read.Reason}");
            Console.WriteLine(
                $"Rotate: Allowed={rotate.Allowed}; Matched={rotate.MatchedPrincipal}; Reason={rotate.Reason}");
            Console.WriteLine(
                $"Administrator UI: Allowed={admin.Allowed}; Matched={admin.MatchedPrincipal}; Reason={admin.Reason}");

            if (validation.Count > 0)
            {
                Console.WriteLine("Configuration warnings:");
                foreach (var warning in validation)
                    Console.WriteLine("  " + warning);
                return 4;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int Apply(AppConfig config, string[] args)
    {
        try
        {
            if (HasFlag(args, "--rbac-enable"))
                config.RbacEnabled = true;
            if (HasFlag(args, "--rbac-disable"))
                config.RbacEnabled = false;

            var bypass = GetOptionValue(args, "--rbac-admin-bypass");
            if (!string.IsNullOrWhiteSpace(bypass))
            {
                if (!TryParseBoolean(bypass, out var enabled))
                {
                    throw new ArgumentException(
                        "--rbac-admin-bypass must be on/off, true/false, or 1/0.");
                }

                config.RbacAllowLocalAdministrators = enabled;
            }

            MutatePrincipalList(
                config.RbacRecoveryReaders,
                GetOptionValue(args, "--rbac-reader-add"),
                add: true);
            MutatePrincipalList(
                config.RbacRecoveryReaders,
                GetOptionValue(args, "--rbac-reader-remove"),
                add: false);
            MutatePrincipalList(
                config.RbacRotationOperators,
                GetOptionValue(args, "--rbac-rotator-add"),
                add: true);
            MutatePrincipalList(
                config.RbacRotationOperators,
                GetOptionValue(args, "--rbac-rotator-remove"),
                add: false);
            MutatePrincipalList(
                config.RbacAdministrators,
                GetOptionValue(args, "--rbac-ui-admin-add"),
                add: true);
            MutatePrincipalList(
                config.RbacAdministrators,
                GetOptionValue(args, "--rbac-ui-admin-remove"),
                add: false);

            if (config.RbacEnabled &&
                !config.RbacAllowLocalAdministrators &&
                config.RbacRecoveryReaders.Count == 0 &&
                config.RbacRotationOperators.Count == 0 &&
                config.RbacAdministrators.Count == 0)
            {
                throw new InvalidOperationException(
                    "RBAC would deny all privileged actions: configure at least one reader/rotator/BitKeyBridge administrator or keep the local Administrators bypass enabled.");
            }

            var validation =
                new AuthorizationService(config)
                    .ValidateConfiguredPrincipals();

            if (validation.Count > 0)
            {
                throw new InvalidOperationException(
                    "RBAC configuration contains unresolved principals:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, validation));
            }

            ConfigService.SaveAppConfig(config);

            Console.WriteLine("RBAC configuration saved.");
            return ShowStatus(config);
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

    private static void MutatePrincipalList(
        List<string> list,
        string? principal,
        bool add)
    {
        if (string.IsNullOrWhiteSpace(principal))
            return;

        var normalized = principal.Trim();

        if (add)
        {
            ValidatePrincipal(normalized);
            if (!list.Any(x =>
                    x.Equals(
                        normalized,
                        StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(normalized);
            }

            return;
        }

        list.RemoveAll(x =>
            x.Equals(
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidatePrincipal(string principal)
    {
        if (principal.StartsWith(
                "S-1-",
                StringComparison.OrdinalIgnoreCase))
        {
            _ = new SecurityIdentifier(principal);
            return;
        }

        var account = new NTAccount(principal);
        _ = account.Translate(typeof(SecurityIdentifier));
    }

    private static bool TryParseBoolean(
        string value,
        out bool result)
    {
        var normalized = value.Trim();

        if (normalized.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            normalized == "1")
        {
            result = true;
            return true;
        }

        if (normalized.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            normalized == "0")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static bool HasFlag(
        IEnumerable<string> args,
        string flag) =>
        args.Any(x =>
            x.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOptionValue(
        IReadOnlyList<string> args,
        string option)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals(
                    option,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Count)
                throw new ArgumentException(
                    $"{option} requires a value.");

            return args[i + 1];
        }

        return null;
    }

    private static string FormatPrincipals(
        IReadOnlyCollection<string> principals) =>
        principals.Count == 0
            ? "(none)"
            : string.Join(", ", principals);
}
