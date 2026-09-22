using System.Security.Principal;

namespace BitKeyBridge;

public enum BitKeyBridgePermission
{
    RecoveryRead,
    Rotate,
    JitGrant,
    RecoveryApprove
}

public sealed class AuthorizationDecision
{
    public bool Allowed { get; set; }
    public string Permission { get; set; } = string.Empty;
    public string Identity { get; set; } = string.Empty;
    public string MatchedPrincipal { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class AuthorizationService
{
    private readonly AppConfig _config;

    public AuthorizationService(AppConfig config) => _config = config;

    public AuthorizationDecision Check(BitKeyBridgePermission permission)
    {
        var privilegedControl =
            permission is
                BitKeyBridgePermission.JitGrant or
                BitKeyBridgePermission.RecoveryApprove;

        var policyEnabled =
            permission switch
            {
                BitKeyBridgePermission.JitGrant =>
                    _config.JitRecoveryEnabled,
                BitKeyBridgePermission.RecoveryApprove =>
                    _config.TwoPersonApprovalEnabled,
                _ => _config.RbacEnabled
            };

        if (!OperatingSystem.IsWindows())
        {
            return new AuthorizationDecision
            {
                Allowed =
                    !privilegedControl &&
                    !_config.RbacEnabled,
                Permission =
                    permission.ToString(),
                Identity =
                    Environment.UserName,
                Reason =
                    privilegedControl
                        ? "Privileged recovery controls require a Windows identity."
                        : _config.RbacEnabled
                            ? "RBAC requires Windows identity/group membership."
                            : "RBAC is disabled."
            };
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(
                identity);

        var identityName =
            identity.Name ??
            Environment.UserName;

        if (privilegedControl &&
            !policyEnabled)
        {
            return new AuthorizationDecision
            {
                Allowed = false,
                Permission =
                    permission.ToString(),
                Identity =
                    identityName,
                Reason =
                    permission ==
                    BitKeyBridgePermission.JitGrant
                        ? "JIT recovery access is disabled."
                        : "Two-person approval is disabled."
            };
        }

        if (!privilegedControl &&
            !_config.RbacEnabled)
        {
            return new AuthorizationDecision
            {
                Allowed = true,
                Permission = permission.ToString(),
                Identity = identityName,
                Reason = "RBAC is disabled."
            };
        }

        if (_config.RbacAllowLocalAdministrators &&
            principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            return new AuthorizationDecision
            {
                Allowed = true,
                Permission = permission.ToString(),
                Identity = identityName,
                MatchedPrincipal =
                    "BUILTIN\\Administrators",
                Reason =
                    "Local Administrators bypass is enabled."
            };
        }

        var configured =
            permission switch
            {
                BitKeyBridgePermission.RecoveryRead =>
                    _config.RbacRecoveryReaders,
                BitKeyBridgePermission.Rotate =>
                    _config.RbacRotationOperators,
                BitKeyBridgePermission.JitGrant =>
                    _config.RbacJitGrantors,
                BitKeyBridgePermission.RecoveryApprove =>
                    _config.RbacRecoveryApprovers,
                _ => []
            };

        foreach (var configuredPrincipal in configured
                     .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var candidate = configuredPrincipal.Trim();

            try
            {
                var sid = ResolveSid(candidate);
                if (identity.User?.Equals(sid) == true ||
                    identity.Groups?.Contains(sid) == true)
                {
                    return new AuthorizationDecision
                    {
                        Allowed = true,
                        Permission = permission.ToString(),
                        Identity = identityName,
                        MatchedPrincipal = candidate,
                        Reason = "Current Windows identity matches an allowed user/group."
                    };
                }
            }
            catch
            {
                // Invalid or currently unresolvable principals are ignored here.
                // Validation is available through the RBAC status/GUI path.
            }
        }

        return new AuthorizationDecision
        {
            Allowed = false,
            Permission = permission.ToString(),
            Identity = identityName,
            Reason =
                $"Current Windows identity is not authorized for {permission}."
        };
    }

    public void Demand(BitKeyBridgePermission permission)
    {
        var decision = Check(permission);
        if (!decision.Allowed)
            throw new UnauthorizedAccessException(decision.Reason);
    }

    public IReadOnlyList<string> ValidateConfiguredPrincipals()
    {
        var errors = new List<string>();

        ValidateList("RecoveryReaders", _config.RbacRecoveryReaders, errors);
        ValidateList("RotationOperators", _config.RbacRotationOperators, errors);
        ValidateList("JitGrantors", _config.RbacJitGrantors, errors);
        ValidateList("RecoveryApprovers", _config.RbacRecoveryApprovers, errors);

        return errors;
    }

    public static string CurrentIdentityName()
    {
        if (!OperatingSystem.IsWindows())
            return Environment.UserName;

        using var identity = WindowsIdentity.GetCurrent();
        return identity.Name ?? Environment.UserName;
    }

    public static string CurrentIdentitySid()
    {
        if (!OperatingSystem.IsWindows())
            return string.Empty;

        using var identity =
            WindowsIdentity.GetCurrent();

        return identity.User?.Value ??
               string.Empty;
    }

    public static IReadOnlyList<string> CurrentIdentitySids()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        using var identity =
            WindowsIdentity.GetCurrent();

        var result =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (identity.User is not null)
            result.Add(identity.User.Value);

        if (identity.Groups is not null)
        {
            foreach (var sid in
                     identity.Groups)
            {
                result.Add(
                    sid.Value);
            }
        }

        return result.ToArray();
    }

    public static string ResolvePrincipalSid(
        string principal) =>
        ResolveSid(
            principal.Trim())
            .Value;

    private static void ValidateList(
        string label,
        IEnumerable<string> principals,
        ICollection<string> errors)
    {
        foreach (var value in principals
                     .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            try
            {
                _ = ResolveSid(value.Trim());
            }
            catch (Exception ex)
            {
                errors.Add($"{label}: '{value}' could not be resolved: {ex.Message}");
            }
        }
    }

    private static SecurityIdentifier ResolveSid(string principal)
    {
        if (principal.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase))
            return new SecurityIdentifier(principal);

        var account = new NTAccount(principal);
        return (SecurityIdentifier)account.Translate(
            typeof(SecurityIdentifier));
    }
}
