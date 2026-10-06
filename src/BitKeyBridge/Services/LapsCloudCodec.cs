using System.Text.Json;

namespace BitKeyBridge;

public static class LapsCloudCodec
{
    public static LapsReadResult Read(JsonElement root, string deviceId, string deviceName, bool includeHistory)
    {
        if (root.TryGetProperty("value", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
            root = wrapped;
        var result = new LapsReadResult
        {
            ComputerId = deviceId, ComputerName = Text(root, "deviceName") ?? deviceName,
            DirectoryServer = "Microsoft Entra ID",
            Note = "Entra displays the current credential and any previous credentials returned by Microsoft Graph. " +
                "AD history retention and AD encryption-principal permissions do not apply to this source."
        };
        try
        {
            if (!root.TryGetProperty("credentials", out var credentials) || credentials.ValueKind != JsonValueKind.Array)
            {
                result.Note = "Entra did not return credentials. Check LAPS backup and DeviceLocalCredential.Read.All (ReadBasic.All cannot read passwords).";
                return result;
            }
            var values = credentials.EnumerateArray().OrderByDescending(x => Timestamp(x, "backupDateTime")).ToList();
            var currentTimestamp = values.Count == 0 ? null : Timestamp(values[0], "backupDateTime");
            foreach (var value in values)
            {
                var timestamp = Timestamp(value, "backupDateTime");
                var history = timestamp != currentTimestamp;
                if (history && !includeHistory) continue;
                var row = new LapsPasswordEntry
                {
                    Source = "Windows LAPS (Entra)", Attribute = "deviceLocalCredentials",
                    AccountName = Text(value, "accountName") ?? "Unknown account",
                    AccountSid = Text(value, "accountSid") ?? string.Empty,
                    UpdatedAtUtc = timestamp, IsHistory = history,
                    Status = LapsPasswordStatus.NotReturned,
                    StatusDetail = "The credential was returned without a password."
                };
                result.Entries.Add(row);
                if (Text(value, "passwordBase64") is { Length: > 0 } password)
                {
                    try
                    {
                        row.SetPassword(LapsSecretCodec.DecodeEntraPassword(password));
                        row.Status = row.HasPassword ? LapsPasswordStatus.Available : LapsPasswordStatus.NotReturned;
                        row.StatusDetail = row.HasPassword ? "Password read successfully." : "An empty password was returned.";
                    }
                    catch (FormatException)
                    {
                        row.Status = LapsPasswordStatus.InvalidData;
                        row.StatusDetail = "The Entra LAPS password has an invalid encoding.";
                    }
                }
            }
            return result;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            result.Dispose();
            throw new FormatException("Entra returned an invalid LAPS credential record.");
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTime? Timestamp(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var time)
            ? time.UtcDateTime : null;
}
