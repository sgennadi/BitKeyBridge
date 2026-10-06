using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public static class LapsSecretCodec
{
    private static readonly Encoding StrictUnicode = new UnicodeEncoding(false, false, true);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static (DateTime? TimestampUtc, byte[] Payload) UnpackEncryptedBlob(byte[] value)
    {
        // MS-ADA2 2.66: FILETIME (8), encrypted size (4), reserved (4), DPAPI-NG payload.
        if (value.Length < 17 || value.Length > 1024 * 1024)
            throw new FormatException("Invalid Windows LAPS encrypted header length.");
        var span = value.AsSpan();
        var size = BinaryPrimitives.ReadUInt32LittleEndian(span[8..12]);
        if (size != value.Length - 16 || BinaryPrimitives.ReadUInt32LittleEndian(span[12..16]) != 0)
            throw new FormatException("Invalid Windows LAPS encrypted header fields.");
        var time = FileTimeUtc(BinaryPrimitives.ReadInt64LittleEndian(span[..8]));
        return (time, value[16..]);
    }

    public static void ReadPasswordJson(string json, LapsPasswordEntry entry)
    {
        // Do not include JSON, attribute values, or parser exception text in diagnostics.
        try
        {
            using var document = JsonDocument.Parse(json.TrimEnd('\0'));
            var root = document.RootElement;
            var name = root.GetProperty("n").GetString();
            var password = root.GetProperty("p").GetString();
            var timestamp = root.GetProperty("t").GetString();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password) ||
                !ulong.TryParse(timestamp, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var time) ||
                time > long.MaxValue || FileTimeUtc((long)time) is not { } updated)
                throw new FormatException();
            entry.AccountName = name;
            entry.UpdatedAtUtc = updated;
            entry.SetPassword(password);
            entry.Status = LapsPasswordStatus.Available;
            entry.StatusDetail = "Password read successfully.";
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            entry.Dispose();
            throw new FormatException("Invalid Windows LAPS password JSON.");
        }
    }

    public static void ReadDecryptedPassword(byte[] plaintext, LapsPasswordEntry entry)
    {
        try
        {
            // Windows LAPS stores UTF-16LE JSON. Also accept UTF-8 payloads explicitly.
            var unicode = plaintext.Length >= 2 &&
                ((plaintext[0] == 0xff && plaintext[1] == 0xfe) || plaintext[1] == 0);
            var text = (unicode ? StrictUnicode : StrictUtf8).GetString(plaintext).TrimStart('\uFEFF');
            ReadPasswordJson(text, entry);
        }
        catch (DecoderFallbackException)
        {
            throw new FormatException("Invalid Windows LAPS password encoding.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static string DecodeEntraPassword(string base64)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException) { throw new FormatException("Invalid Entra LAPS password encoding."); }
        try
        {
            if (bytes.Length == 0 || bytes.Length % 2 != 0)
                throw new FormatException("Invalid Entra LAPS password encoding.");
            return StrictUnicode.GetString(bytes).TrimEnd('\0');
        }
        catch (DecoderFallbackException)
        {
            throw new FormatException("Invalid Entra LAPS password encoding.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static DateTime? FileTimeUtc(long value)
    {
        if (value <= 0) return null;
        try { return DateTime.FromFileTimeUtc(value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
