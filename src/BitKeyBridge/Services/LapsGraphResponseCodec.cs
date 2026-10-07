using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public static class LapsGraphResponseCodec
{
    public static JsonDocument ParseJsonResponse(
        byte[] body,
        string? charset)
    {
        if (body.Length == 0)
        {
            throw new JsonException(
                "The response body was empty.");
        }

        var offset =
            0;

        if (body.Length >= 3 &&
            body[0] == 0xEF &&
            body[1] == 0xBB &&
            body[2] == 0xBF)
        {
            offset =
                3;
        }

        var utf16Le =
            body.Length - offset >= 2 &&
            body[offset] == 0xFF &&
            body[offset + 1] == 0xFE;
        var utf16Be =
            body.Length - offset >= 2 &&
            body[offset] == 0xFE &&
            body[offset + 1] == 0xFF;
        var declaredUtf16 =
            !string.IsNullOrWhiteSpace(
                charset) &&
            charset.Contains(
                "utf-16",
                StringComparison.OrdinalIgnoreCase);

        if (utf16Le ||
            utf16Be ||
            declaredUtf16)
        {
            Encoding encoding =
                utf16Be
                    ? Encoding.BigEndianUnicode
                    : Encoding.Unicode;

            if (utf16Le ||
                utf16Be)
            {
                offset +=
                    2;
            }

            var text =
                encoding.GetString(
                    body,
                    offset,
                    body.Length - offset)
                    .TrimStart(
                        '\uFEFF');

            return JsonDocument.Parse(
                text);
        }

        return JsonDocument.Parse(
            body.AsMemory(
                offset));
    }
}
