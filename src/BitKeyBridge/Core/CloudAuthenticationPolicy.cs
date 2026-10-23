namespace BitKeyBridge;

/// <summary>
/// Restricts cloud authentication to delegated Device Code and certificate auth.
/// Legacy ROPC or unknown stored modes require a new interactive sign-in.
/// </summary>
public static class CloudAuthenticationPolicy
{
    public static string Normalize(string? mode) =>
        string.Equals(mode?.Trim(), "Certificate", StringComparison.OrdinalIgnoreCase)
            ? "Certificate"
            : "DeviceCode";
}
