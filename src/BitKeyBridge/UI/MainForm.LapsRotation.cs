namespace BitKeyBridge;

public sealed partial class MainForm
{
    /// <summary>
    /// Optional operator-controlled AD write. Disabled by default on every
    /// application startup and requires an exact fresh AD object identity.
    /// </summary>
    private async Task RequestSelectedAdLapsRotationAsync()
    {
        if (!_lapsCoverageRotationOptIn.Checked ||
            !_directoryConnected ||
            _lapsCoverageCurrent is null)
        {
            UiStyle.SetStatus(_lapsCoverageStatus,
                "Connect AD, scan, and enable manual expiration requests for this session.",
                UiStatusKind.Warning);
            return;
        }

        var row = SelectedLapsCoverageDevice();
        if (row is null || !row.CanRequestAdExpiration ||
            _lapsCoverageCurrent.Truncated)
        {
            UiStyle.SetStatus(_lapsCoverageStatus,
                "Select a unique Windows or Legacy LAPS record with an AD objectGUID. " +
                "Mixed or unverified policies cannot be modified.",
                UiStatusKind.Warning);
            return;
        }

        if (DateTime.UtcNow - _lapsCoverageCurrent.GeneratedAtUtc >
            TimeSpan.FromMinutes(30))
        {
            UiStyle.SetStatus(_lapsCoverageStatus,
                "LAPS scan is older than 30 minutes. Scan again before writing to AD.",
                UiStatusKind.Warning);
            return;
        }

        if (!AuthorizeAction(BitKeyBridgePermission.Rotate,
                "RequestAdLapsExpiration", row.ComputerName,
                row.ObjectGuid, "AD LAPS"))
            return;

        var context = GetOrRequestRecoveryAccessContext(
            "AD LAPS", row.ObjectGuid, row.ComputerName,
            allowRotationReminder: false);
        if (context is null)
            return;

        var attribute = AdLapsExpirationService.AttributeFor(row);
        var answer = MessageBox.Show(
            this,
            $"Request expiration of {row.BackupEvidence} LAPS for {row.ComputerName}?" +
            Environment.NewLine +
            $"Exact AD objectGUID: {row.ObjectGuid}" + Environment.NewLine +
            $"Distinguished name: {row.DistinguishedName}" + Environment.NewLine +
            $"Attribute: {attribute}" + Environment.NewLine + Environment.NewLine +
            "This writes an expiration timestamp to AD. The password does NOT " +
            "rotate immediately; the computer must next process LAPS policy." +
            Environment.NewLine +
            "Continue only if authorized to expire this computer's password.",
            "Confirm AD LAPS expiration request",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;

        _lapsCoverageRotateButton.Enabled = false;
        try
        {
            var operation = new AdLapsExpirationService(_config);
            await operation.RequestExpirationAsync(row);

            WriteRecoveryAudit(
                "RequestAdLapsExpiration", context,
                computerName: row.ComputerName,
                recoveryId: row.ObjectGuid,
                source: "AD LAPS",
                details: "Expiration attribute requested; " +
                         "policy cycle required; DN=" + row.DistinguishedName,
                rotationRequested: true,
                rotationSucceeded: true);
            UiStyle.SetStatus(_lapsCoverageStatus,
                "AD LAPS expiry timestamp was updated. Rotation awaits the " +
                "computer's next LAPS policy processing cycle. Scan later to verify.",
                UiStatusKind.Success);
        }
        catch (Exception ex)
        {
            WriteRecoveryAudit(
                "RequestAdLapsExpiration", context, "Failed",
                row.ComputerName, row.ObjectGuid, "AD LAPS",
                details: DiagnosticRedaction.Sanitize(ex.Message),
                rotationRequested: true,
                rotationSucceeded: false);
            _appDiagnostics.ShowError(
                "AD LAPS expiration request failed.",
                "RequestAdLapsExpiration", ex,
                ("Computer", row.ComputerName),
                ("ObjectGuid", row.ObjectGuid));
            UiStyle.SetStatus(_lapsCoverageStatus,
                "LAPS expiration request failed. Verify AD delegated reset " +
                "permissions and scan exact computer again.",
                UiStatusKind.Error);
        }
        finally
        {
            RefreshLapsCoverageRotationButton();
        }
    }
}
