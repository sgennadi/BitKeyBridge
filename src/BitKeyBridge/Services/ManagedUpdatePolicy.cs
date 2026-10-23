using Microsoft.Win32;
using System.Text.Json;

namespace BitKeyBridge;

/// <summary>
/// Read-only restrictions from HKLM policies. A central GPO/Intune setting may
/// DISABLE an existing local unsigned/signed opt-in but can NEVER enable it.
/// Unknown/malformed registry values fail closed for automatic installation.
/// </summary>
internal static class ManagedUpdatePolicy
{
    internal const string RegistryPath = @"SOFTWARE\Policies\BitKeyBridge";

    internal sealed record Snapshot(
        bool Readable, bool DisableAutomaticUpdates,
        bool DisableUnsignedAutomaticUpdates, bool RequireSignedAutomaticUpdates)
    {
        internal static Snapshot Default => new(true, false, false, false);
        internal static Snapshot Denied => new(false, true, true, true);
    }

    internal static bool IsPermitted(AppConfig config) =>
        Allows(config, Read());

    internal static bool Allows(AppConfig config, Snapshot policy)
    {
        if (!policy.Readable || policy.DisableAutomaticUpdates)
            return false;
        if (policy.RequireSignedAutomaticUpdates &&
            !UpdateTrustPolicy.CanEnableUnattended(
                config.RequireTrustedUpdateSignature, config.TrustedUpdatePublisher))
            return false;
        if (policy.DisableUnsignedAutomaticUpdates &&
            UpdateTrustPolicy.UseUnsignedOptInChannel(config))
            return false;
        return true;
    }

    private static bool Flag(RegistryKey key, string name)
    {
        if (key.GetValue(name) is null)
            return false;
        if (key.GetValueKind(name) != RegistryValueKind.DWord ||
            key.GetValue(name) is not int value || (value != 0 && value != 1))
            throw new InvalidDataException("Invalid managed update policy type/value: " + name);
        return value == 1;
    }

    internal static Snapshot Read()
    {
        if (!OperatingSystem.IsWindows())
            return Snapshot.Default;
        try
        {
            var view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64 : RegistryView.Registry32;
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = hive.OpenSubKey(RegistryPath, writable: false);
            if (key is null)
                return Snapshot.Default;
            return new Snapshot(true,
                Flag(key, "DisableAutomaticUpdates"),
                Flag(key, "DisableUnsignedAutomaticUpdates"),
                Flag(key, "RequireSignedAutomaticUpdates"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
                                   UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Deny, don't silently fall back to permissive configuration.
            WindowsEventLogService.TryWrite(
                "Managed auto-update policy could not be verified: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name),
                EventLogSeverity.Warning, 4108, "Update");
            return Snapshot.Denied;
        }
    }

    internal static int PrintStatus(AppConfig config)
    {
        var policy = Read();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            PolicySource = @"HKLM\" + RegistryPath,
            policy.Readable,
            policy.DisableAutomaticUpdates,
            policy.DisableUnsignedAutomaticUpdates,
            policy.RequireSignedAutomaticUpdates,
            AutomaticInstallationPermitted =
                config.CheckForUpdatesOnStart &&
                config.AutoInstallUpdatesOnStart &&
                UpdateTrustPolicy.CanInstallUnattended(config) &&
                !UpdateRollbackHoldService.IsActiveForCurrentBuild(),
            Note = "Machine restrictions can deny auto-install; they never consent to unsigned updates."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return policy.Readable ? 0 : 2;
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var errors = new List<string>();
        var unsigned = new AppConfig
        {
            UpdateRepository = UpdateTrustPolicy.OfficialRepository,
            AutoInstallUpdatesOnStart = true,
            AllowUnsignedUnattendedUpdates = true
        };
        var signed = new AppConfig
        {
            RequireTrustedUpdateSignature = true,
            TrustedUpdatePublisher = "Example Trusted Publisher"
        };
        if (!Allows(unsigned, Snapshot.Default) ||
            Allows(unsigned, new Snapshot(true, true, false, false)) ||
            Allows(unsigned, new Snapshot(true, false, true, false)) ||
            Allows(unsigned, new Snapshot(true, false, false, true)) ||
            Allows(unsigned, Snapshot.Denied) ||
            !Allows(signed, new Snapshot(true, false, true, true)) ||
            Allows(signed, new Snapshot(true, true, false, false)))
            errors.Add("Managed update restriction precedence or fail-closed behavior was broken.");
        // Presence of a machine restriction never implies local opt-in.
        var unapproved = new AppConfig();
        if (UpdateTrustPolicy.CanEnableUnattended(
                unapproved.RequireTrustedUpdateSignature,
                unapproved.TrustedUpdatePublisher,
                unapproved.AllowUnsignedUnattendedUpdates,
                unapproved.UpdateRepository))
            errors.Add("Managed update policy test detected implicit unsigned consent.");
        return errors;
    }
}
