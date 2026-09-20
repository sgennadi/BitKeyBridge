using System.Diagnostics;

namespace BitKeyBridge;

public enum EventLogSeverity
{
    Information,
    Warning,
    Error
}

public static class WindowsEventLogService
{
    public const string SourceName = "BitKeyBridge";
    public const string LogName = "Application";

    public static void EnsureSource()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (EventLog.SourceExists(SourceName)) return;
        EventLog.CreateEventSource(new EventSourceCreationData(SourceName, LogName));
    }

    public static void TryWrite(
        string message,
        EventLogSeverity severity,
        int eventId,
        string? category = null)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            if (!EventLog.SourceExists(SourceName)) return;
            var type = severity switch
            {
                EventLogSeverity.Error => EventLogEntryType.Error,
                EventLogSeverity.Warning => EventLogEntryType.Warning,
                _ => EventLogEntryType.Information
            };

            var safe = Sanitize(message);
            if (!string.IsNullOrWhiteSpace(category))
                safe = $"[{category}] {safe}";

            EventLog.WriteEntry(
                SourceName,
                safe,
                type,
                Math.Clamp(eventId, 1, 65535));
        }
        catch
        {
            // Event Log must never break BitKeyBridge's primary workflow.
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\b\d{6}(?:-\d{6}){7}\b",
            "[REDACTED-BITLOCKER-KEY]");
    }
}
