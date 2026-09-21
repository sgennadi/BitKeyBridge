using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitKeyBridge;

public sealed class RecoveryIncidentVerificationService
{
    private static readonly Regex RecoveryPasswordPattern =
        new(
            @"\b\d{6}(?:-\d{6}){7}\b",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private readonly string _incidentDirectory;
    private readonly string _auditPath;

    public RecoveryIncidentVerificationService(
        string? incidentDirectory = null,
        string? auditPath = null)
    {
        _incidentDirectory =
            string.IsNullOrWhiteSpace(incidentDirectory)
                ? AppPaths.IncidentsDirectory
                : Path.GetFullPath(incidentDirectory);

        _auditPath =
            string.IsNullOrWhiteSpace(auditPath)
                ? AppPaths.AuditLogFile
                : Path.GetFullPath(auditPath);
    }

    public RecoveryIncidentVerificationResult Verify(
        string sessionId)
    {
        var normalized =
            NormalizeSessionId(sessionId);

        var result =
            new RecoveryIncidentVerificationResult
            {
                SessionId = normalized
            };

        var incident =
            new RecoveryIncidentService(
                _incidentDirectory)
                .Read(normalized);

        if (incident is null)
        {
            result.Status = "BundleMissing";
            result.Errors.Add(
                "Recovery incident bundle was not found.");
            return result;
        }

        result.BundleFound = true;
        result.ActionsTotal =
            incident.Actions.Count;

        var serialized =
            JsonSerializer.Serialize(incident);

        if (RecoveryPasswordPattern.IsMatch(
                serialized))
        {
            result.SensitiveDataDetected = true;
            result.Errors.Add(
                "Incident bundle contains a value matching the BitLocker 48-digit recovery-password format.");
        }

        if (!string.Equals(
                normalized,
                NormalizeSessionId(
                    incident.SessionId),
                StringComparison.OrdinalIgnoreCase))
        {
            result.MetadataMismatches++;
            result.Errors.Add(
                "Bundle SessionId does not match the requested session.");
        }

        var before =
            AuditIntegrityService.Verify(
                _auditPath);

        result.AuditChainValid =
            before.Valid;

        if (!before.Valid)
        {
            result.Status = "AuditInvalid";
            result.Errors.Add(
                "Audit chain verification failed: " +
                before.FirstError);
            return result;
        }

        var hashes =
            incident.Actions
                .Select(x => x.AuditEntryHash)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        Dictionary<string, AuditEntry> entries;
        DateTime? earliestRetainedUtc;
        DateTime? latestRetainedUtc;

        try
        {
            entries =
                AuditIntegrityService.ReadEntriesByHash(
                    _auditPath,
                    hashes,
                    out earliestRetainedUtc,
                    out latestRetainedUtc);
        }
        catch (Exception ex)
        {
            result.Status = "AuditReadError";
            result.Errors.Add(ex.Message);
            return result;
        }

        var after =
            AuditIntegrityService.Verify(
                _auditPath);

        result.StableAuditSnapshot =
            after.Valid &&
            before.TotalEntries ==
            after.TotalEntries &&
            before.ChainedEntries ==
            after.ChainedEntries &&
            string.Equals(
                before.LastHash,
                after.LastHash,
                StringComparison.OrdinalIgnoreCase);

        if (!after.Valid)
        {
            result.AuditChainValid = false;
            result.Errors.Add(
                "Audit chain changed to an invalid state during incident verification: " +
                after.FirstError);
        }

        if (!result.StableAuditSnapshot)
        {
            result.Errors.Add(
                "Audit changed while the incident was being verified. Retry verification against a stable audit snapshot.");
        }

        foreach (var action in incident.Actions)
        {
            if (string.IsNullOrWhiteSpace(
                    action.AuditEntryHash))
            {
                result.MetadataMismatches++;
                result.Errors.Add(
                    $"Incident action {action.Action} has no AuditEntryHash.");
                continue;
            }

            if (!entries.TryGetValue(
                    action.AuditEntryHash,
                    out var entry))
            {
                var actionUtc =
                    action.TimestampUtc.ToUniversalTime();

                if (earliestRetainedUtc is not null &&
                    actionUtc <
                    earliestRetainedUtc.Value)
                {
                    result.ActionsMissingFromRetainedAudit++;
                    result.Warnings.Add(
                        $"Audit anchor for {action.Action} at {actionUtc:O} is older than the retained audit window beginning {earliestRetainedUtc:O}.");
                }
                else
                {
                    result.Errors.Add(
                        $"Audit anchor is missing for action {action.Action}, hash {action.AuditEntryHash}.");
                }

                continue;
            }

            result.ActionsAnchored++;

            ValidateActionMetadata(
                incident,
                action,
                entry,
                result);
        }

        ValidateRotationState(
            incident,
            result);

        if (result.SensitiveDataDetected)
        {
            result.Status =
                "SensitiveDataDetected";
        }
        else if (!result.AuditChainValid)
        {
            result.Status =
                "AuditInvalid";
        }
        else if (!result.StableAuditSnapshot)
        {
            result.Status =
                "AuditChangedDuringVerification";
        }
        else if (result.MetadataMismatches > 0)
        {
            result.Status =
                "MetadataMismatch";
        }
        else if (!result.RotationStateValid)
        {
            result.Status =
                "RotationStateMismatch";
        }
        else if (result.ActionsAnchored ==
                 result.ActionsTotal &&
                 result.ActionsTotal > 0)
        {
            result.Status =
                "Valid";
        }
        else if (result.ActionsMissingFromRetainedAudit > 0 &&
                 result.ActionsAnchored +
                 result.ActionsMissingFromRetainedAudit ==
                 result.ActionsTotal)
        {
            result.Status =
                "NotFullyRetained";
        }
        else
        {
            result.Status =
                "MissingAuditAnchor";
        }

        return result;
    }

    private static void ValidateActionMetadata(
        RecoveryIncidentBundle incident,
        RecoveryIncidentAction action,
        AuditEntry entry,
        RecoveryIncidentVerificationResult result)
    {
        Check(
            string.Equals(
                entry.EntryHash,
                action.AuditEntryHash,
                StringComparison.OrdinalIgnoreCase),
            action,
            "AuditEntryHash",
            result);

        Check(
            string.Equals(
                entry.CorrelationId,
                incident.SessionId,
                StringComparison.OrdinalIgnoreCase),
            action,
            "CorrelationId",
            result);

        Check(
            string.Equals(
                entry.Action,
                action.Action,
                StringComparison.Ordinal),
            action,
            "Action",
            result);

        Check(
            string.Equals(
                entry.Result,
                action.Result,
                StringComparison.Ordinal),
            action,
            "Result",
            result);

        Check(
            string.Equals(
                entry.Source,
                action.Source,
                StringComparison.Ordinal),
            action,
            "Source",
            result);

        Check(
            string.Equals(
                entry.AuthMode,
                action.AuthMode,
                StringComparison.Ordinal),
            action,
            "AuthMode",
            result);

        var delta =
            Math.Abs(
                (entry.TimestampUtc.ToUniversalTime() -
                 action.TimestampUtc.ToUniversalTime())
                .TotalSeconds);

        Check(
            delta <= 1,
            action,
            "TimestampUtc",
            result);

        if (!string.IsNullOrWhiteSpace(
                incident.Operator))
        {
            Check(
                string.Equals(
                    entry.User,
                    incident.Operator,
                    StringComparison.OrdinalIgnoreCase),
                action,
                "Operator",
                result);
        }

        if (!string.IsNullOrWhiteSpace(
                incident.Host))
        {
            Check(
                string.Equals(
                    entry.Host,
                    incident.Host,
                    StringComparison.OrdinalIgnoreCase),
                action,
                "Host",
                result);
        }

        if (!string.IsNullOrWhiteSpace(
                incident.ComputerName))
        {
            Check(
                string.Equals(
                    entry.ComputerName,
                    incident.ComputerName,
                    StringComparison.OrdinalIgnoreCase),
                action,
                "ComputerName",
                result);
        }

        if (!string.IsNullOrWhiteSpace(
                incident.RecoveryId))
        {
            Check(
                string.Equals(
                    entry.RecoveryId,
                    incident.RecoveryId,
                    StringComparison.OrdinalIgnoreCase),
                action,
                "RecoveryId",
                result);
        }

        if (!string.IsNullOrWhiteSpace(
                incident.Reference))
        {
            Check(
                string.Equals(
                    entry.Reference,
                    incident.Reference,
                    StringComparison.Ordinal),
                action,
                "Reference",
                result);
        }

        if (!string.IsNullOrWhiteSpace(
                incident.Reason))
        {
            Check(
                string.Equals(
                    entry.Reason,
                    incident.Reason,
                    StringComparison.Ordinal),
                action,
                "Reason",
                result);
        }
    }

    private static void ValidateRotationState(
        RecoveryIncidentBundle incident,
        RecoveryIncidentVerificationResult result)
    {
        var rotateActions =
            incident.Actions
                .Where(x =>
                    string.Equals(
                        x.Action,
                        "RotateBitLockerKey",
                        StringComparison.Ordinal))
                .ToArray();

        var anyRequested =
            rotateActions.Length > 0;

        var anySucceeded =
            rotateActions.Any(x =>
                string.Equals(
                    x.Result,
                    "Success",
                    StringComparison.OrdinalIgnoreCase));

        if (incident.RotationSucceeded &&
            !incident.RotationRequested)
        {
            result.RotationStateValid = false;
            result.Errors.Add(
                "RotationSucceeded is true while RotationRequested is false.");
        }

        if (incident.RotationRequested !=
            anyRequested)
        {
            result.RotationStateValid = false;
            result.Errors.Add(
                "RotationRequested does not match recorded rotation actions.");
        }

        if (incident.RotationSucceeded !=
            anySucceeded)
        {
            result.RotationStateValid = false;
            result.Errors.Add(
                "RotationSucceeded does not match successful rotation actions.");
        }
    }

    private static void Check(
        bool condition,
        RecoveryIncidentAction action,
        string field,
        RecoveryIncidentVerificationResult result)
    {
        if (condition)
            return;

        result.MetadataMismatches++;
        result.Errors.Add(
            $"Incident action {action.Action} does not match audit field {field}.");
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
}
