namespace BitKeyBridge;

public sealed partial class MainForm
{
    private void CopyUnifiedDeviceSummary()
    {
        if (_unifiedResults.SelectedItems.Count == 0 ||
            _unifiedResults.SelectedItems[0].Tag is not UnifiedDeviceInfo row)
        {
            UiStyle.SetStatus(
                _unifiedStatus, "Select an exact device record first.",
                UiStatusKind.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(Device360Formatter.Format(row));
            _audit.Write(
                "CopyDevice360Summary",
                computerName: row.ComputerName,
                source: "Devices",
                details: "Non-secret summary; QualifiedId=" + row.IdentityKey);
            UiStyle.SetStatus(
                _unifiedStatus,
                "Non-secret Device 360 summary copied. No recovery keys or LAPS passwords included.",
                UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            _unifiedDiagnostics.ShowError(
                "Device summary clipboard copy failed.",
                "CopyDevice360Summary", ex);
        }
    }
}
