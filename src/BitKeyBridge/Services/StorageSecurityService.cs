using System.Security.AccessControl;
using System.Security.Principal;

namespace BitKeyBridge;

public sealed class StorageSecurityService
{
    private const string SystemSidValue = "S-1-5-18";
    private const string AdministratorsSidValue = "S-1-5-32-544";

    private readonly string _incidentsDirectory;
    private readonly string _backupsDirectory;
    private readonly string _transitionsDirectory;
    private readonly bool _serviceIdentityOverrideSpecified;
    private readonly string _serviceIdentityOverride;

    private sealed record DirectoryPolicy(
        string Name,
        string Path,
        FileSystemRights ServiceRights,
        string ServiceAccessMode);

    public StorageSecurityService(
        string? incidentsDirectory = null,
        string? backupsDirectory = null,
        string? transitionsDirectory = null,
        string? serviceIdentityOverride = null)
    {
        _incidentsDirectory =
            Path.GetFullPath(
                incidentsDirectory ??
                AppPaths.IncidentsDirectory);
        _backupsDirectory =
            Path.GetFullPath(
                backupsDirectory ??
                AppPaths.BackupsDirectory);
        _transitionsDirectory =
            Path.GetFullPath(
                transitionsDirectory ??
                AppPaths.AuditSigningTransitionsDirectory);

        _serviceIdentityOverrideSpecified =
            serviceIdentityOverride is not null;
        _serviceIdentityOverride =
            serviceIdentityOverride ?? string.Empty;
    }

    public StorageSecurityStatus Check(
        bool persist = true)
    {
        var status =
            new StorageSecurityStatus
            {
                CheckedAtUtc = DateTime.UtcNow
            };

        var serviceIdentity =
            ResolveServiceIdentity();

        status.ServiceIdentity =
            serviceIdentity;

        SecurityIdentifier? serviceSid = null;
        if (!string.IsNullOrWhiteSpace(
                serviceIdentity))
        {
            try
            {
                serviceSid =
                    ResolveSid(
                        serviceIdentity);
            }
            catch (Exception ex)
            {
                status.Errors.Add(
                    "Service identity could not be resolved: " +
                    ex.Message);
            }
        }

        foreach (var policy in
                 GetPolicies())
        {
            status.Directories.Add(
                CheckDirectory(
                    policy,
                    serviceIdentity,
                    serviceSid));
        }

        if (persist)
            TryPersist(status);

        return status;
    }

    public StorageSecurityStatus Repair()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Storage ACL hardening is only supported on Windows.");
        }

        if (!SecurityContext.IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator rights are required to repair BitKeyBridge storage ACLs.");
        }

        var serviceIdentity =
            ResolveServiceIdentity();
        SecurityIdentifier? serviceSid = null;

        if (!string.IsNullOrWhiteSpace(
                serviceIdentity))
        {
            serviceSid =
                ResolveSid(
                    serviceIdentity);
        }

        var repairErrors =
            new List<string>();

        foreach (var policy in
                 GetPolicies())
        {
            try
            {
                RepairDirectory(
                    policy,
                    serviceSid);
            }
            catch (Exception ex)
            {
                repairErrors.Add(
                    $"{policy.Name}: {ex.Message}");
            }
        }

        var status =
            Check(
                persist: false);

        status.RepairAttempted = true;
        status.RepairSucceeded =
            repairErrors.Count == 0 &&
            status.Valid;

        status.Errors.InsertRange(
            0,
            repairErrors);

        TryPersist(status);

        WindowsEventLogService.TryWrite(
            $"Storage ACL repair completed. Success={status.RepairSucceeded}; " +
            $"Directories={status.Directories.Count}; Errors={status.Errors.Count}.",
            status.RepairSucceeded
                ? EventLogSeverity.Information
                : EventLogSeverity.Warning,
            4620,
            "StorageSecurity");

        return status;
    }

    private IEnumerable<DirectoryPolicy> GetPolicies()
    {
        yield return new DirectoryPolicy(
            "Incidents",
            _incidentsDirectory,
            FileSystemRights.Modify,
            "Modify");

        yield return new DirectoryPolicy(
            "Backups",
            _backupsDirectory,
            FileSystemRights.Modify,
            "Modify");

        yield return new DirectoryPolicy(
            "AuditSigningTransitions",
            _transitionsDirectory,
            FileSystemRights.ReadAndExecute |
            FileSystemRights.ListDirectory,
            "Read");
    }

    private static StorageAclDirectoryStatus CheckDirectory(
        DirectoryPolicy policy,
        string serviceIdentity,
        SecurityIdentifier? serviceSid)
    {
        var result =
            new StorageAclDirectoryStatus
            {
                Name = policy.Name,
                Path = policy.Path,
                Exists =
                    Directory.Exists(
                        policy.Path),
                ServiceIdentity =
                    serviceIdentity,
                ServiceAccessMode =
                    policy.ServiceAccessMode
            };

        var serviceAccessRequired =
            serviceSid is not null &&
            !SidEquals(
                serviceSid,
                SystemSidValue) &&
            !SidEquals(
                serviceSid,
                AdministratorsSidValue);

        result.ServiceAccessRequired =
            serviceAccessRequired;

        if (!result.Exists)
        {
            result.Problems.Add(
                "Directory does not exist.");
            return result;
        }

        try
        {
            var security =
                new DirectoryInfo(
                    policy.Path)
                    .GetAccessControl(
                        AccessControlSections.Access);

            result.InheritanceProtected =
                security.AreAccessRulesProtected;

            if (!result.InheritanceProtected)
            {
                result.Problems.Add(
                    "ACL inheritance is enabled.");
            }

            var rules =
                security.GetAccessRules(
                    includeExplicit: true,
                    includeInherited: true,
                    targetType:
                        typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference
                    is not SecurityIdentifier sid)
                {
                    continue;
                }

                if (rule.AccessControlType !=
                    AccessControlType.Allow)
                {
                    continue;
                }

                if (SidEquals(
                        sid,
                        SystemSidValue))
                {
                    if (HasRights(
                            rule.FileSystemRights,
                            FileSystemRights.FullControl))
                    {
                        result.SystemFullControl =
                            true;
                    }

                    continue;
                }

                if (SidEquals(
                        sid,
                        AdministratorsSidValue))
                {
                    if (HasRights(
                            rule.FileSystemRights,
                            FileSystemRights.FullControl))
                    {
                        result.AdministratorsFullControl =
                            true;
                    }

                    continue;
                }

                if (serviceAccessRequired &&
                    serviceSid is not null &&
                    sid.Equals(serviceSid))
                {
                    if (HasRights(
                            rule.FileSystemRights,
                            policy.ServiceRights))
                    {
                        result.ServiceAccessPresent =
                            true;
                    }

                    continue;
                }

                result.UnexpectedAllowRules++;
                result.Problems.Add(
                    $"Unexpected Allow ACE: {sid.Value} [{rule.FileSystemRights}] Inherited={rule.IsInherited}");
            }

            if (!result.SystemFullControl)
            {
                result.Problems.Add(
                    "SYSTEM FullControl ACE is missing.");
            }

            if (!result.AdministratorsFullControl)
            {
                result.Problems.Add(
                    "BUILTIN\\Administrators FullControl ACE is missing.");
            }

            if (serviceAccessRequired &&
                !result.ServiceAccessPresent)
            {
                result.Problems.Add(
                    $"Service identity {serviceIdentity} is missing required {policy.ServiceAccessMode} access.");
            }
        }
        catch (Exception ex)
        {
            result.Problems.Add(
                ex.Message);
        }

        return result;
    }

    private static void RepairDirectory(
        DirectoryPolicy policy,
        SecurityIdentifier? serviceSid)
    {
        Directory.CreateDirectory(
            policy.Path);

        var systemSid =
            new SecurityIdentifier(
                SystemSidValue);
        var administratorsSid =
            new SecurityIdentifier(
                AdministratorsSidValue);

        var security =
            new DirectorySecurity();

        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        AddRule(
            security,
            systemSid,
            FileSystemRights.FullControl);

        AddRule(
            security,
            administratorsSid,
            FileSystemRights.FullControl);

        if (serviceSid is not null &&
            !serviceSid.Equals(systemSid) &&
            !serviceSid.Equals(administratorsSid))
        {
            AddRule(
                security,
                serviceSid,
                policy.ServiceRights);
        }

        new DirectoryInfo(
            policy.Path)
            .SetAccessControl(
                security);
    }

    private static void AddRule(
        DirectorySecurity security,
        SecurityIdentifier sid,
        FileSystemRights rights)
    {
        security.AddAccessRule(
            new FileSystemAccessRule(
                sid,
                rights,
                InheritanceFlags.ContainerInherit |
                InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
    }

    private string ResolveServiceIdentity()
    {
        if (_serviceIdentityOverrideSpecified)
        {
            return NormalizeServiceIdentity(
                _serviceIdentityOverride);
        }

        try
        {
            var info =
                WindowsServiceHost.GetInfo();

            return NormalizeServiceIdentity(
                info.Identity);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeServiceIdentity(
        string? identity)
    {
        var value =
            (identity ?? string.Empty)
            .Trim();

        return value.ToLowerInvariant() switch
        {
            "localsystem" =>
                @"NT AUTHORITY\SYSTEM",
            "localservice" =>
                @"NT AUTHORITY\LOCAL SERVICE",
            "networkservice" =>
                @"NT AUTHORITY\NETWORK SERVICE",
            _ => value
        };
    }

    private static SecurityIdentifier ResolveSid(
        string identity)
    {
        if (identity.StartsWith(
                "S-1-",
                StringComparison.OrdinalIgnoreCase))
        {
            return new SecurityIdentifier(
                identity);
        }

        return (SecurityIdentifier)
            new NTAccount(identity)
                .Translate(
                    typeof(SecurityIdentifier));
    }

    private static bool HasRights(
        FileSystemRights granted,
        FileSystemRights required) =>
        (granted & required) ==
        required;

    private static bool SidEquals(
        SecurityIdentifier sid,
        string value) =>
        string.Equals(
            sid.Value,
            value,
            StringComparison.OrdinalIgnoreCase);

    private static void TryPersist(
        StorageSecurityStatus status)
    {
        try
        {
            JsonStore.WriteAtomic(
                AppPaths.StorageSecurityStatusFile,
                status);
        }
        catch
        {
        }
    }
}
