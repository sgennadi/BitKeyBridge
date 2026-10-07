using System.Windows.Forms;

namespace BitKeyBridge;

public static class SecureClipboard
{
    public const string ExcludeFromMonitorFormat =
        "ExcludeClipboardContentFromMonitorProcessing";
    public const string IncludeInHistoryFormat =
        "CanIncludeInClipboardHistory";
    public const string UploadToCloudFormat =
        "CanUploadToCloudClipboard";

    public static void SetSensitiveText(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException(
                "Sensitive clipboard text cannot be empty.",
                nameof(text));

        var data =
            CreateSensitiveTextDataObject(text);

        Clipboard.SetDataObject(
            data,
            copy: true);
    }

    internal static DataObject CreateSensitiveTextDataObject(
        string text)
    {
        var data =
            new DataObject();

        data.SetText(
            text,
            TextDataFormat.UnicodeText);

        // Windows recognizes these registered clipboard format names.
        // ExcludeClipboardContentFromMonitorProcessing prevents the entire
        // clipboard item from entering local history or cloud synchronization.
        data.SetData(
            ExcludeFromMonitorFormat,
            autoConvert: false,
            new MemoryStream([1]));

        // Defense in depth for Windows versions that honor the more granular
        // history/cloud controls independently.
        data.SetData(
            IncludeInHistoryFormat,
            autoConvert: false,
            new MemoryStream(BitConverter.GetBytes(0u)));

        data.SetData(
            UploadToCloudFormat,
            autoConvert: false,
            new MemoryStream(BitConverter.GetBytes(0u)));

        return data;
    }

    public static bool ClearIfMatches(
        string? expected)
    {
        if (string.IsNullOrEmpty(expected) ||
            !Clipboard.ContainsText())
        {
            return false;
        }

        if (!string.Equals(
                Clipboard.GetText(),
                expected,
                StringComparison.Ordinal))
        {
            return false;
        }

        Clipboard.Clear();
        return true;
    }
}
