using System.Security.Principal;

namespace BitKeyBridge;

public enum BitKeyBridgePermission
{
    RecoveryRead,
    Rotate
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
        if (!OperatingSystem.IsWindows())
        {
            return new AuthorizationDecision
            {
                Allowed = !_config.RbacEnabled,
                Permission = permission.ToString(),
                Identity = Environment.UserName,
                Reason = _config.RbacEnabled
                    ? "RBAC requires Windows identity/group membership."
                    : "RBAC is disabled."
            };
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var identityName = identity.Name ?? Environment.UserName;

        if (!_config.RbacEnabled)
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
            principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            return new AuthorizationDecision
            {
                Allowed = true,
                Permission = permission.ToString(),
                Identity = identityName,
                MatchedPrincipal = "BUILTIN\\Administrators",
                Reason = "Local Administrators bypass is enabled."
            };
        }

        var configured = permission switch
        {
            BitKeyBridgePermission.RecoveryRead =>
                _config.RbacRecoveryReaders,
            BitKeyBridgePermission.Rotate =>
                _config.RbacRotationOperators,
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

        return errors;
    }

    public static string CurrentIdentityName()
    {
        if (!OperatingSystem.IsWindows())
            return Environment.UserName;

        using var identity = WindowsIdentity.GetCurrent();
        return identity.Name ?? Environment.UserName;
    }

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
