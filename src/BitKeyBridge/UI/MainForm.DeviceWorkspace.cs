namespace BitKeyBridge;

/// <summary>
/// One metadata-only device context for the Start and Helpdesk workspaces.
/// Never copies recovery secrets or authorizes access by hostname. The selected
/// HomeSearchResult remains the only source of current identity and metadata.
/// </summary>
public sealed partial class MainForm
{
    private UiStatusLabel? _helpdeskSelectedDeviceSummary;

    // Navigation never searches a second time, reveals a password, or
    // changes the stored AD / Entra connection preference.
    private void OpenHelpdeskDeviceWorkspace()
    {
        if (_homeWorkspaceTab is null || _homeQuery.IsDisposed)
            return;

        _mainTabs.SelectedTab = _homeWorkspaceTab;
        if (_layoutSelfTest)
            return;

        // A selected result is the workspace, not a new search. Returning
        // from Helpdesk focuses the actual row instead of highlighting
        // the query and inviting an accidental second lookup.
        if (GetSelectedHomeSearchResult() is not null &&
            _homeSearchResults.CanFocus)
        {
            _homeSearchResults.Focus();
            return;
        }

        if (_homeQuery.CanFocus)
        {
            _homeQuery.Focus();
            _homeQuery.SelectAll();
        }
    }

    // A harmless navigation-only test hook. The actual control uses the same
    // handler; this exercises it when a headless WinForms button cannot focus.
    internal void PreviewHelpdeskDeviceWorkspaceForUiSelfTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Only available in UI tests.");
        OpenHelpdeskDeviceWorkspace();
    }

    private void UpdateHelpdeskSelectedDeviceSummary()
    {
        if (_helpdeskSelectedDeviceSummary is null ||
            _helpdeskSelectedDeviceSummary.IsDisposed)
            return;

        var selected = GetSelectedHomeSearchResult();
        if (selected is null)
        {
            UiStyle.SetStatus(_helpdeskSelectedDeviceSummary,
                "No device selected. Search once on Start, then select an exact result.",
                UiStatusKind.Neutral);
            return;
        }

        // A hostname is display-only metadata. Never infer an AD object
        // from an ambiguous name or route a cloud result into AD recovery.
        if (selected.IdentityAmbiguous ||
            (selected.IsCloudRecord &&
             string.IsNullOrWhiteSpace(selected.SourceIdentity)))
        {
            UiStyle.SetStatus(_helpdeskSelectedDeviceSummary,
                "Device identity is ambiguous or unverified. Return to Start " +
                "and select an exact AD / Entra record before recovery.",
                UiStatusKind.Warning);
            return;
        }

        var source = string.IsNullOrWhiteSpace(selected.SourceSummary)
            ? (selected.IsCloudRecord ? "Entra" : "AD")
            : selected.SourceSummary;
        UiStyle.SetStatus(_helpdeskSelectedDeviceSummary,
            $"Selected: {selected.ComputerName} | {source} | " +
            $"BitLocker: {selected.BitLockerStatus} | LAPS: {selected.LapsStatus}. " +
            "Opening this page does not retrieve a password.",
            UiStatusKind.Neutral);
    }
}
