namespace BitKeyBridge;

public sealed class JitRecoveryService
{
    private readonly AppConfig _config;
    private readonly string _directory;
    private readonly string _auditPath;

    public JitRecoveryService(
        AppConfig config,
        string? directory = null,
        string? auditPath = null)
    {
        _config = config;
        _directory =
            Path.GetFullPath(
                directory ??
                AppPaths.JitGrantsDirectory);
        _auditPath =
            Path.GetFullPath(
                auditPath ??
                AppPaths.AuditLogFile);
    }

    public JitRecoveryGrant CreateGrant(
        string subject,
        string? reason = null,
        AuditService? audit = null)
    {
        new AuthorizationService(
            _config)
            .Demand(
                BitKeyBridgePermission.JitGrant);

        if (string.IsNullOrWhiteSpace(
                subject))
        {
            throw new ArgumentException(
                "JIT grant subject is required.");
        }

        var now =
            DateTime.UtcNow;

        var grant =
            new JitRecoveryGrant
            {
                Subject =
                    subject.Trim(),
                SubjectSid =
                    AuthorizationService
                        .ResolvePrincipalSid(
                            subject.Trim()),
                GrantedBy =
                    AuthorizationService
                        .CurrentIdentityName(),
                GrantedBySid =
                    AuthorizationService
                        .CurrentIdentitySid(),
                GrantedAtUtc =
                    now,
                ExpiresAtUtc =
                    now.AddMinutes(
                        Math.Clamp(
                            _config.JitRecoveryGrantMinutes,
                            1,
                            1440)),
                Reason =
                    Sanitize(reason)
            };

        var writer =
            audit ??
            new AuditService(
                _auditPath);

        var entry =
            writer.Write(
                "JitRecoveryGrant",
                result:
                    "Success",
                source:
                    "Local",
                details:
                    BuildCanonicalDetails(
                        grant),
                reason:
                    grant.Reason,
                correlationId:
                    grant.GrantId)
            ?? throw new InvalidOperationException(
                "JIT grant was not saved because its audit anchor could not be written.");

        grant.AuditEntryHash =
            entry.EntryHash;

        Directory.CreateDirectory(
            _directory);

        JsonStore.WriteAtomic(
            GetGrantPath(
                grant.GrantId),
            grant);

        return grant;
    }

    public bool RevokeGrant(
        string grantId,
        string? reason = null,
        AuditService? audit = null)
    {
        new AuthorizationService(
            _config)
            .Demand(
                BitKeyBridgePermission.JitGrant);

        var normalized =
            NormalizeId(
                grantId);

        var path =
            GetGrantPath(
                normalized);

        if (!File.Exists(path))
            return false;

        var writer =
            audit ??
            new AuditService(
                _auditPath);

        var entry =
            writer.Write(
                "JitRecoveryRevoke",
                result:
                    "Authorized",
                source:
                    "Local",
                details:
                    $"GrantId={normalized}",
                reason:
                    Sanitize(reason),
                correlationId:
                    normalized);

        if (entry is null)
        {
            throw new InvalidOperationException(
                "JIT grant was not revoked because its audit authorization could not be written.");
        }

        File.Delete(path);
        return true;
    }

    public JitRecoveryStatus GetCurrentStatus()
    {
        var status =
            new JitRecoveryStatus
            {
                Enabled =
                    _config.JitRecoveryEnabled,
                GrantRequired =
                    _config.JitRecoveryEnabled
            };

        if (!_config.JitRecoveryEnabled)
        {
            status.Status =
                "Disabled";
            status.GrantValid =
                true;
            return status;
        }

        if (!OperatingSystem.IsWindows())
        {
            status.Status =
                "WindowsIdentityRequired";
            status.Error =
                "JIT recovery access requires Windows identity.";
            return status;
        }

        var identitySids =
            new HashSet<string>(
                AuthorizationService
                    .CurrentIdentitySids(),
                StringComparer.OrdinalIgnoreCase);

        var candidates =
            ReadAllGrants()
                .Where(x =>
                    identitySids.Contains(
                        x.SubjectSid))
                .OrderByDescending(
                    x => x.ExpiresAtUtc)
                .ToArray();

        if (candidates.Length == 0)
        {
            status.Status =
                "GrantMissing";
            return status;
        }

        status.GrantFound =
            true;

        var integrity =
            AuditIntegrityService.Verify(
                _auditPath);

        if (!integrity.Valid)
        {
            status.Status =
                "AuditInvalid";
            status.Error =
                integrity.FirstError;
            return status;
        }

        foreach (var grant in
                 candidates)
        {
            if (grant.ExpiresAtUtc.ToUniversalTime() <=
                DateTime.UtcNow)
            {
                continue;
            }

            if (!ValidateGrantAnchor(
                    grant,
                    out var error))
            {
                status.Error =
                    error;
                continue;
            }

            status.GrantValid =
                true;
            status.GrantId =
                grant.GrantId;
            status.Subject =
                grant.Subject;
            status.ExpiresAtUtc =
                grant.ExpiresAtUtc.ToUniversalTime();
            status.MinutesRemaining =
                Math.Max(
                    0,
                    (status.ExpiresAtUtc.Value -
                     DateTime.UtcNow)
                    .TotalMinutes);
            status.Status =
                "Valid";
            status.Error =
                string.Empty;

            return status;
        }

        status.Status =
            candidates.Any(x =>
                x.ExpiresAtUtc.ToUniversalTime() <=
                DateTime.UtcNow)
                ? "Expired"
                : "InvalidAnchor";

        return status;
    }

    public IReadOnlyList<JitRecoveryGrant> ReadAllGrants()
    {
        if (!Directory.Exists(
                _directory))
        {
            return [];
        }

        var result =
            new List<JitRecoveryGrant>();

        foreach (var path in
                 Directory.EnumerateFiles(
                     _directory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                var grant =
                    JsonStore.Read<JitRecoveryGrant>(
                        path);

                if (grant is not null)
                    result.Add(grant);
            }
            catch
            {
            }
        }

        return result;
    }

    public int CountActiveValidGrants()
    {
        if (!_config.JitRecoveryEnabled)
            return 0;

        var integrity =
            AuditIntegrityService.Verify(
                _auditPath);

        if (!integrity.Valid)
            return 0;

        var count = 0;

        foreach (var grant in
                 ReadAllGrants())
        {
            if (grant.ExpiresAtUtc.ToUniversalTime() <=
                DateTime.UtcNow)
            {
                continue;
            }

            if (ValidateGrantAnchor(
                    grant,
                    out _))
            {
                count++;
            }
        }

        return count;
    }

    private bool ValidateGrantAnchor(
        JitRecoveryGrant grant,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(
                grant.AuditEntryHash))
        {
            error =
                "JIT grant has no audit anchor.";
            return false;
        }

        var entries =
            AuditIntegrityService
                .ReadEntriesByHash(
                    _auditPath,
                    [grant.AuditEntryHash],
                    out _,
                    out _);

        if (!entries.TryGetValue(
                grant.AuditEntryHash,
                out var entry))
        {
            error =
                "JIT grant audit anchor is not retained.";
            return false;
        }

        if (!string.Equals(
                entry.Action,
                "JitRecoveryGrant",
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Result,
                "Success",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.CorrelationId,
                grant.GrantId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                entry.Details,
                BuildCanonicalDetails(
                    grant),
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Reason,
                Sanitize(
                    grant.Reason),
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.User,
                grant.GrantedBy,
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                "JIT grant metadata does not match its audit anchor.";
            return false;
        }

        return true;
    }

    private static string BuildCanonicalDetails(
        JitRecoveryGrant grant) =>
        $"GrantId={grant.GrantId}; " +
        $"SubjectSid={grant.SubjectSid}; " +
        $"Subject={Sanitize(grant.Subject)}; " +
        $"GrantedBySid={grant.GrantedBySid}; " +
        $"ExpiresAtUtc={grant.ExpiresAtUtc.ToUniversalTime():O}";

    private string GetGrantPath(
        string grantId) =>
        Path.Combine(
            _directory,
            NormalizeId(
                grantId) +
            ".json");

    private static string NormalizeId(
        string value)
    {
        var normalized =
            new string(
                (value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .ToArray());

        if (normalized.Length is < 16 or > 64)
        {
            throw new ArgumentException(
                "JIT grant ID is invalid.");
        }

        return normalized;
    }

    private static string Sanitize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex
            .Replace(
                value,
                @"\b\d{6}(?:-\d{6}){7}\b",
                "[REDACTED-BITLOCKER-KEY]");
    }
}
