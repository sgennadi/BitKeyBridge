using System.Text.Json;

namespace BitKeyBridge;

public sealed class RecoveryIncidentService
{
    private static readonly object Sync = new();
    private readonly string _directory;

    public RecoveryIncidentService(
        string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? AppPaths.IncidentsDirectory
            : Path.GetFullPath(directory);
    }

    public RecoveryIncidentBundle? Append(
        RecoveryAccessContext context,
        AuditEntry? auditEntry,
        bool rotationRequested = false,
        bool rotationSucceeded = false)
    {
        if (auditEntry is null ||
            string.IsNullOrWhiteSpace(context.SessionId))
        {
            return null;
        }

        var sessionId = NormalizeSessionId(
            context.SessionId);
        var path = GetPath(sessionId);

        lock (Sync)
        {
            RecoveryIncidentBundle bundle;
            try
            {
                bundle =
                    JsonStore.Read<RecoveryIncidentBundle>(path)
                    ?? CreateBundle(
                        sessionId,
                        context,
                        auditEntry);
            }
            catch
            {
                bundle =
                    CreateBundle(
                        sessionId,
                        context,
                        auditEntry);
            }

            bundle.UpdatedAtUtc = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(bundle.ComputerName))
                bundle.ComputerName = auditEntry.ComputerName;
            if (string.IsNullOrWhiteSpace(bundle.RecoveryId))
                bundle.RecoveryId = auditEntry.RecoveryId;
            if (string.IsNullOrWhiteSpace(bundle.Reference))
                bundle.Reference = Sanitize(context.Reference);
            if (string.IsNullOrWhiteSpace(bundle.Reason))
                bundle.Reason = Sanitize(context.Reason);

            bundle.RotationRequested |= rotationRequested;
            bundle.RotationSucceeded |= rotationSucceeded;

            bundle.Actions.Add(
                new RecoveryIncidentAction
                {
                    TimestampUtc =
                        auditEntry.TimestampUtc,
                    Action =
                        auditEntry.Action,
                    Result =
                        auditEntry.Result,
                    Source =
                        auditEntry.Source,
                    AuthMode =
                        auditEntry.AuthMode,
                    AuditEntryHash =
                        auditEntry.EntryHash,
                    Details =
                        Sanitize(auditEntry.Details)
                });

            JsonStore.WriteAtomic(
                path,
                bundle);

            return bundle;
        }
    }

    public RecoveryIncidentBundle? Read(
        string sessionId)
    {
        var path = GetPath(
            NormalizeSessionId(sessionId));

        try
        {
            return JsonStore.Read<RecoveryIncidentBundle>(
                path);
        }
        catch
        {
            return null;
        }
    }

    public string GetPathForSession(
        string sessionId) =>
        GetPath(NormalizeSessionId(sessionId));

    private static RecoveryIncidentBundle CreateBundle(
        string sessionId,
        RecoveryAccessContext context,
        AuditEntry entry)
    {
        var now = DateTime.UtcNow;

        return new RecoveryIncidentBundle
        {
            SessionId = sessionId,
            CreatedAtUtc =
                context.CreatedAtUtc == default
                    ? now
                    : context.CreatedAtUtc,
            UpdatedAtUtc = now,
            Operator = entry.User,
            Host = entry.Host,
            ComputerName = entry.ComputerName,
            RecoveryId = entry.RecoveryId,
            Reference = Sanitize(context.Reference),
            Reason = Sanitize(context.Reason)
        };
    }

    private string GetPath(
        string sessionId)
    {
        Directory.CreateDirectory(
            _directory);

        return Path.Combine(
            _directory,
            sessionId + ".json");
    }

    private static string NormalizeSessionId(
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
                "Recovery session ID is invalid.");
        }

        return normalized;
    }

    private static string Sanitize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\b\d{6}(?:-\d{6}){7}\b",
            "[REDACTED-BITLOCKER-KEY]");
    }
}
