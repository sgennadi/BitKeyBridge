namespace BitKeyBridge;

public sealed partial class MainForm
{
    // Isolated UI-only regression: no directory, Graph, MFA, network or
    // protected recovery action is executed by this fixture.
    internal bool PreviewTargetedSourceRetryForUiTest()
    {
        if (!_layoutSelfTest)
            throw new InvalidOperationException("Offline UI self-test only.");

        var oldReport = _unifiedSourceReport;
        var oldQuery = _unifiedQuery.Text;
        var oldConnected = _directoryConnected;
        var oldLanguage = _uiLanguageCode;
        try
        {
            _unifiedQuery.Text = "UI-SOURCE-RETRY";
            _directoryConnected = true;

            var failedAd = new MetadataLookupResult<AdComputerInfo>(
                [], "Synthetic disconnected", MetadataFailureKind.SourceOffline);
            var healthyIntune = new MetadataLookupResult<ManagedDeviceInfo>([], null);
            var healthyEntra = new MetadataLookupResult<CloudRecoveryMetadata>([], null);
            var report = UnifiedDeviceSearchReport.Combine(
                failedAd, healthyIntune, healthyEntra,
                cloudAttempted: false);
            report.SourceState = new UnifiedSourceRetryState(
                "UI-SOURCE-RETRY", 250, false,
                failedAd, healthyIntune, healthyEntra);
            _unifiedSourceReport = report;
            RefreshUnifiedSourceRetryButton();
            if (!_unifiedRetryButton.Enabled ||
                !CanRetryUnifiedSource(UnifiedMetadataSource.Ad) ||
                CanRetryUnifiedSource(UnifiedMetadataSource.Entra))
                return false;

            foreach (var language in new[] { "en", "ru", "he" })
            {
                _uiLanguageCode = language;
                RefreshUnifiedSourceRetryButton();
                if (_unifiedRetryButton.Text != UiLanguageService.Translate(
                        language, "DevicesRetryFailedSource"))
                    return false;
            }

            _directoryConnected = false;
            RefreshUnifiedSourceRetryButton();
            if (_unifiedRetryButton.Enabled)
                return false;

            _directoryConnected = true;
            var deniedAd = new MetadataLookupResult<AdComputerInfo>(
                [], "Synthetic 403", MetadataFailureKind.AccessDenied);
            report = UnifiedDeviceSearchReport.Combine(
                deniedAd, healthyIntune, healthyEntra, cloudAttempted: false);
            report.SourceState = new UnifiedSourceRetryState(
                "UI-SOURCE-RETRY", 250, false,
                deniedAd, healthyIntune, healthyEntra);
            _unifiedSourceReport = report;
            RefreshUnifiedSourceRetryButton();
            return !_unifiedRetryButton.Enabled;
        }
        finally
        {
            _unifiedSourceReport = oldReport;
            _unifiedQuery.Text = oldQuery;
            _directoryConnected = oldConnected;
            _uiLanguageCode = oldLanguage;
            RefreshUnifiedSourceRetryButton();
        }
    }
}
