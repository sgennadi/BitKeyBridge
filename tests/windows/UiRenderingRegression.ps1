#requires -Version 5.1
<#
.SYNOPSIS
Read-only source regressions for BitKeyBridge's Windows 4K/RDP UI.
.DESCRIPTION
The runtime --ui-self-test validates actual WinForms clipping and layout on
Compact/150%/175%/200% scenarios. This test additionally rejects known
expensive repaint patterns at source level without time-based/flaky assertions.
No admin rights, network, file writes or recovery secrets are required.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$viewport = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.ResponsiveViewport.cs') -Raw
$theme = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiModernTheme.cs') -Raw
$base = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\DpiAwareForm.cs') -Raw
$style = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiStyle.cs') -Raw
$shell = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.Shell.cs') -Raw
$diagnostics = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.DiagnosticNavigation.cs') -Raw
$helpdesk = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.HelpdeskDashboard.cs') -Raw
$deviceWorkspace = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.DeviceWorkspace.cs') -Raw
$computerContext = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.ComputerContext.cs') -Raw
$homeSource = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.Home.cs') -Raw
$startConnectionSource = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.StartConnection.cs') -Raw
$startupWizardSource = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\StartupConnectionWizardDialog.cs') -Raw
$cloudWizardSource = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\CloudConnectionWizardDialog.cs') -Raw
$setupService = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\Services\EntraSetupService.cs') -Raw
$cloudMain = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.cs') -Raw
$intuneSetup = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\IntuneSetupWizardDialog.cs') -Raw
$deviceCodeDialog = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\DeviceCodeDialog.cs') -Raw
$helpdeskSettingsSource = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.HelpdeskUx.cs') -Raw
$layoutSelfTest = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiLayoutSelfTest.cs') -Raw
$dialogLocalizer = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiDialogLocalization.cs') -Raw
$mainLocalization = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.Localization.cs') -Raw
$headerCache = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiHeaderWidthCache.cs') -Raw
$project = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\BitKeyBridge.csproj') -Raw
$telemetry = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiLayoutTelemetry.cs') -Raw
$navigationTelemetry = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiTabNavigationTelemetry.cs') -Raw
$cleanup = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\Services\UpdateStagingCleanupService.cs') -Raw
$housekeeping = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\Services\HousekeepingService.cs') -Raw

function Assert-Source([bool] $condition, [string] $reason) {
    if (-not $condition) { throw "UI repaint regression: $reason" }
}

Assert-Source ($project.Contains('<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>')) 'PerMonitorV2 must remain enabled.'
Assert-Source ($viewport.Contains('Interval = 55')) 'Main viewport must batch WM_SIZE events.'
Assert-Source ($viewport.Contains('ClientSizeChanged += (_, _) => QueueMainViewportReflow()')) 'Main viewport must respond to client width changes.'
Assert-Source ($viewport.Contains('DpiChanged += (_, _) => QueueMainViewportReflow()')) 'Main viewport must handle monitor DPI changes.'
Assert-Source (-not [regex]::IsMatch($viewport, '(?m)^\s*UiModernTheme\.Refresh\(shell\)\s*;')) 'WM_SIZE must not refresh every themed control and GDI region.'
Assert-Source ([regex]::IsMatch($viewport, 'if\s*\(geometryChanged\)\s*shell\.PerformLayout\(\)\s*;')) 'Main layout must be forced only on true geometry changes.'
Assert-Source ($theme.Contains('if (Markers.TryGetValue(root, out _))')) 'Repeat theme application must be O(1).'
Assert-Source ($theme.Contains('root.ControlAdded += (_, args) =>') -and
               $theme.Contains('if (args.Control is { } child) Apply(child);')) 'Newly added controls must receive the theme through the null-safe, non-recursive handler.'
Assert-Source ($theme.Contains('IsPrimaryAction(button)')) 'Primary actions must receive centralized accent styling.'
Assert-Source ($theme.Contains('TextFormatFlags.EndEllipsis') -and $theme.Contains('textFlags |= TextFormatFlags.RightToLeft')) 'Owner-drawn Hebrew tabs must use RTL text flags without clipping.'
Assert-Source (-not [regex]::IsMatch($theme, '(?s)case DataGridView grid:.*?SetupRoundedRegion\(grid')) 'Data grids must not recreate GDI clipping regions.'
Assert-Source (-not [regex]::IsMatch($theme, '(?s)case TextBoxBase edit:.*?SetupRoundedRegion\(edit')) 'Native text controls must keep native cursor/border rendering.'
Assert-Source ($base.Contains('_responsiveLabelTimer') -and $base.Contains('_responsiveLayoutTimer')) 'Font/DPI/label reflows must be coalesced.'
Assert-Source ($base.Contains('ClientSize.Width == lastMeasuredWidth')) 'Height-only resize must not remeasure every wrapping label.'
Assert-Source ($style.Contains('var updatingWidths = false')) 'Vertical workspace resize must guard nested Layout callbacks.'
Assert-Source ($style.Contains('panel.ResumeLayout(performLayout: true)')) 'AutoSize sections must receive one final stable layout.'
Assert-Source (-not [regex]::IsMatch($theme + $viewport + $base, 'WS_EX_COMPOSITED')) 'Do not force global composited windows: this hurts Remote Desktop.'
Assert-Source ($viewport.Contains('RepairSelectedTabHierarchy(_mainTabs, changedPath)')) 'MainForm must reflow the selected visible tab chain.'
Assert-Source ($viewport.Contains('RepairSelectedTabHierarchy(') -and $viewport.Contains('nested, forceFlowLayout || pageRepaired || boundsRepaired')) 'Diagnostics, Settings and Devices nested tabs must be repaired recursively.'
Assert-Source ($viewport.Contains('flow.PerformLayout()') -and $viewport.Contains('(forceFlowLayout || pageRepaired || boundsRepaired)')) 'Visible vertical flow sections must be measured after nested-tab activation.'
Assert-Source ($style.Contains('child is TableLayoutPanel || child is FlowLayoutPanel')) 'AutoSize action flows must be horizontally constrained as well as table sections.'
Assert-Source ($style.Contains('panel.VisibleChanged += (_, _) => ResizeChildren()')) 'Previously hidden pages must remeasure widths on selection.'
Assert-Source ($shell.Contains('page.AutoScroll = false')) 'Outer TabPage AutoScroll must not fight nested Dock=Fill viewports.'
Assert-Source ($shell.Contains('nested.SelectedIndexChanged += (_, _) => QueueNavigationViewportReflow()')) 'Nested tabs must use one immediate visible-layout repair instead of WM_SIZE debounce.'
Assert-Source ($diagnostics.Contains('DiagnosticToolsWorkspace') -and $diagnostics.Contains('DiagnosticToolsActions')) 'Diagnostics layout must expose stable regression targets.'
Assert-Source ($shell.Contains('_mainTabs.ResumeLayout(performLayout: initialApply || _layoutSelfTest)')) 'Warm language changes must defer redundant TabControl-wide layout until selected-viewport reflow.'
Assert-Source ($shell.Contains('QueueNavigationViewportReflow();')) 'Warm language changes must schedule selected viewport repair after translation.'
Assert-Source ($base.Contains('RefreshResponsiveLabelWidths();')) 'Wrapping labels must be measured after the form layout.'
Assert-Source ($base.Contains('_pendingVisibleLabelWidths') -and
               $base.Contains('DrainPendingVisibleLabelWidths') -and
               $base.Contains('_visibleLabelWidthRefreshPosted') -and
               $base.Contains('if (RefreshResponsiveLabelWidth(label))') -and
               $base.Contains('RecordVisibleLabelBatch(changed, reflowed)')) 'Visible label activation must coalesce posted callbacks and reflow each parent only when widths changed.'
Assert-Source ($layoutSelfTest.Contains('VerifyVisibleLabelReflowCoalescing(failures)') -and
               $layoutSelfTest.Contains('Two visible labels scheduled more than one layout callback.') -and
               $telemetry.Contains('Visible-label width batches: batches=')) 'Native WinForms test must verify visibility bursts and non-secret batched-layout counters.'
Assert-Source ($base.Contains('Widths are computed in RefreshResponsiveLabelWidths()')) 'Cold DPI registration must not measure the same wrapping label twice.'
Assert-Source ($base.Contains('_responsiveLayoutInitialized') -and
               $base.Contains('UiLayoutTelemetry.RecordResponsiveForm(started)') -and
               [regex]::IsMatch($base, 'UiLayoutTelemetry\.RecordResponsivePhase\s*\(\s*started\s*,\s*initialRefresh\s*,\s*this\s+is\s+MainForm\s*\)') -and
               $telemetry.Contains('ColdMainForm.Summarize("Initial DPI - main window")') -and
               $telemetry.Contains('ColdOtherForms.Summarize("Initial DPI - other windows")')) 'Initial and subsequent DPI/font timings must distinguish the main window from other forms.'
foreach ($phase in @('ThemeInstall', 'ControlDefaults', 'WorkArea', 'Layout', 'LabelWidths', 'ThemeRefresh')) {
    Assert-Source ($base.Contains('UiLayoutTelemetry.DpiPhase.' + $phase)) "Missing non-secret DPI timing phase: $phase"
}
Assert-Source ($telemetry.Contains('DpiThemeInstall.Summarize("DPI phase - theme installation")') -and
               $telemetry.Contains('DpiControlDefaults.Summarize("DPI phase - control defaults")') -and
               $telemetry.Contains('DpiLayout.Summarize("DPI phase - WinForms PerformLayout")') -and
               $telemetry.Contains('DpiLabelWidths.Summarize("DPI phase - label widths")')) 'The DPI report must include all expensive layout-phase counters.'
Assert-Source ($style.Contains('if (label.StatusKind == kind') -and $layoutSelfTest.Contains('Unchanged status text/kind forced a repaint')) 'Repeated unchanged status polls must not repaint.'
Assert-Source ($viewport.Contains('_lastRepairedTabPath') -and $viewport.Contains('UiLayoutTelemetry.RecordViewport(viewportStarted)')) 'Selected-tab layout must avoid repeat repair and expose timing metrics.'
Assert-Source ($viewport.Contains('_navigationReflowPosted') -and
               $viewport.Contains('_navigationReflowLatestIndex') -and
               $viewport.Contains('UiTabNavigationTelemetry.RecordCoalescedReflow()') -and
               $viewport.Contains('_navigationReflowPostCount++') -and
               $layoutSelfTest.Contains('Rapid tab navigation posted redundant WinForms callbacks.') -and
               $layoutSelfTest.Contains('Navigation coalescing lost the latest viewport repair.') -and
               $layoutSelfTest.Contains('Main/nested tab burst lost final selection or timing.') -and
               $layoutSelfTest.Contains('Rapid real-tab navigation lost page state or recreated tabs.') -and
               $viewport.Contains('_navigationReflowLastDispatchedIndex = selectedIndex') -and
               $viewport.Contains('_navigationReflowLatestIndex != selectedIndex') -and
               $navigationTelemetry.Contains('Navigation reflow callbacks coalesced:')) 'Rapid navigation must coalesce UI callbacks, preserve final selected tab and report only aggregate metrics.'
Assert-Source ($viewport.Contains('UiTabNavigationTelemetry.RecordDispatch') -and
               $viewport.Contains('UiTabNavigationTelemetry.RecordLayout') -and
               $shell.Contains('RecordMainTabHandleCreated') -and
               $shell.Contains('RecordMainTabHandleDestroyed') -and
               $telemetry.Contains('Tab RTL setter - actual warm changes') -and
               $telemetry.Contains('Tab native mirror - actual warm changes')) 'UI timing must distinguish dispatch delay, actual RTL setter changes, and native tab handle churn.'

Assert-Source ($diagnostics.Contains('RunUiPerformanceDiagnosticsButton') -and $diagnostics.Contains('UiTabNavigationTelemetry.Report()') -and $telemetry.Contains('Interlocked.Increment')) 'Diagnostics must expose bounded navigation and non-secret repaint timings.'
Assert-Source ($shell.Contains('ApplyLocalizedShellCaptions(rtl)') -and $shell.Contains('RightToLeftLayout = false')) 'Hebrew selection must not re-create all native MainForm controls.'
Assert-Source ($layoutSelfTest.Contains('Language switch left an empty or stale caption')) 'Native UI tests must reject blank/stale captions after Hebrew-Russian-English switches.'
Assert-Source ($dialogLocalizer.Contains('TranslatePreservedHint(') -and
               $dialogLocalizer.Contains('OriginalPlaceholder') -and
               $dialogLocalizer.Contains('LastLocalizedPlaceholder') -and
               $dialogLocalizer.Contains('OriginalToolTip') -and
               $dialogLocalizer.Contains('LastLocalizedToolTip')) 'Modal localization must round-trip known placeholder and menu tooltip text across EN/RU/HE.'
Assert-Source ($layoutSelfTest.Contains('RU/HE/EN modal hint roundtrip or live data regression') -and
               $layoutSelfTest.Contains('Modal localizer rewrote externally updated hints.') -and
               $layoutSelfTest.Contains('Modal localizer did not adopt new known English hints.')) 'Runtime localization test must preserve user text and dynamically replaced hints.'
Assert-Source ($mainLocalization.Contains('if (!captionChanged && !widthChanged)') -and
               $mainLocalization.Contains('if (suspended)') -and
               $mainLocalization.Contains('grid.ResumeLayout(performLayout: grid.Visible)')) 'Localized DataGridView must skip no-op layout while remeasuring real text or DPI changes.'
Assert-Source ($mainLocalization.Contains('private readonly UiHeaderWidthCache _headerWidthCache = new();') -and
               $mainLocalization.Contains('UiHeaderWidthCache.HeaderKind.DataGrid') -and
               $mainLocalization.Contains('UiHeaderWidthCache.HeaderKind.ListView')) 'MainForm header localization must reuse fixed-caption measurements for both WinForms grid and list headers.'
Assert-Source ($mainLocalization.Contains('if (!textChanged && !widthChanged)') -and
               $mainLocalization.Contains('list.BeginUpdate();') -and
               $mainLocalization.Contains('if (updating && !list.IsDisposed)') -and
               -not $mainLocalization.Contains('.Concat(_localizedListViewHeaders.Keys')) 'No-op and hidden ListView headers must not force native repaints or parent layout passes.'
Assert-Source ($layoutSelfTest.Contains('Export scope ListView headers were not localized:')) 'Native RU/HE/EN tests must continue verifying ListView caption translations.'
Assert-Source ($headerCache.Contains('UiStaticCaptionCatalog.Contains(sourceEnglish)') -and
               $headerCache.Contains('UiStaticCaptionCatalog.TranslateOrOriginal("ru", sourceEnglish)') -and
               $headerCache.Contains('UiStaticCaptionCatalog.TranslateOrOriginal("he", sourceEnglish)') -and
               $headerCache.Contains('DefaultMaxMeasurements = 512') -and
               $headerCache.Contains('if (_widths.Count >= _capacity)') -and
               $headerCache.Contains('font.GdiCharSet') -and
               $headerCache.Contains('font.GdiVerticalFont')) 'Fixed header cache must reject arbitrary values, bound retention, and differentiate font/DPI contexts.'
Assert-Source ($layoutSelfTest.Contains('VerifyFixedHeaderWidthCaching(failures)') -and
               $layoutSelfTest.Contains('Fixed header width cache accepted arbitrary live metadata.') -and
               $layoutSelfTest.Contains('Fixed header width cache accepted an untrusted caption value.') -and
               $layoutSelfTest.Contains('Fixed header cache did not evict the oldest measurement.')) 'Native WinForms tests must verify cache identity, rejection, eviction, and DPI/font separation.'
# Protect caller-owned data after an initial known UI caption was translated.
# Native regression exercises real menu items and ListView/DataGridView headers.
Assert-Source ($mainLocalization.Contains('LastGenerated') -and
               $mainLocalization.Contains('ToolStripSeparator or ToolStripTextBox or ToolStripComboBox') -and
               $mainLocalization.Contains('headers[column] = (null, source.MinimumWidth, null)') -and
               $mainLocalization.Contains('sources[i] = (null, source.InitialWidth, null)') -and
               $layoutSelfTest.Contains('Language switch rewrote runtime menu caption or editable value:') -and
               $layoutSelfTest.Contains('Main grid/list localization overwrote live header data:') -and
               $layoutSelfTest.Contains('Main grid/list failed to adopt replacement English captions:')) 'Runtime ToolStrip and native columns must not relocalize caller-owned values or cache sensitive metadata.'
# Direction-aware p50/p95 timings expose slow native Hebrew mirroring
# without retaining any text, device identifiers, tenant IDs or keys.
Assert-Source ($telemetry.Contains('RecentCapacity = 128') -and
               $telemetry.Contains('Array.Sort(snapshot)') -and
               $telemetry.Contains('RecentPercentiles()') -and
               $telemetry.Contains('Language EN/RU -> Hebrew') -and
               $telemetry.Contains('Language Hebrew -> EN/RU') -and
               $telemetry.Contains('Language EN <-> RU') -and
               $telemetry.Contains('WarmLanguageSwitch = new(recentPercentiles: true)') -and
               $telemetry.Contains('RunOfflineTests()') -and
               $shell.Contains('_lastAppliedShellRtl = rtl;') -and
               $shell.Contains('started, initialApply, previousRtl, rtl') -and
               $layoutSelfTest.Contains('"p50=", "p95=", "recent="')) 'UI latency report must distinguish Hebrew direction and retain bounded non-secret p50/p95 tests.'
Assert-Source ($telemetry.Contains('Fixed-header width cache: hits=') -and
               $telemetry.Contains('RecordHeaderWidthCacheHit()') -and
               $telemetry.Contains('RecordHeaderWidthCacheMiss()')) 'Header-cache diagnostics must aggregate only hit/miss counts without retaining any live captions.'
Assert-Source ($cleanup.Contains('LastModifiedTreeUtcOrRejectReparse') -and $cleanup.Contains('UpdateRollbackHoldService.IsActiveForVersion') -and $housekeeping.Contains('UpdateStagingCleanupService.Run(')) 'Daily housekeeping must safely prune only old updater staging folders, preserving rollback and reparse-point protections.'
Assert-Source ($layoutSelfTest.Contains('VerifyNestedWorkspaceViewport(main, formName, scenario, failures)')) 'WinForms runtime must test real nested sizes after tab switches.'
Assert-Source ($layoutSelfTest.Contains('content.Size = new Size(') -and $layoutSelfTest.Contains('main.ReflowMainViewport()')) 'Runtime must reproduce a stale 200x100-style content viewport.'

Assert-Source ($helpdesk.Contains('HelpdeskOpenDeviceWorkspaceButton') -and $helpdesk.Contains('HelpdeskSelectedDeviceSummary')) 'Helpdesk must offer the canonical Start workspace and its selected-device metadata.'
Assert-Source (-not $helpdesk.Contains('HelpdeskQuickSearch')) 'Helpdesk must not have its own duplicate search input.'
Assert-Source ($shell.Contains('4 => _homeQuery')) 'Ctrl+F on Helpdesk must route to the canonical Start search.'
Assert-Source ($computerContext.Contains('GetSelectedHomeSearchResult() is { } selected')) 'Returning to Start must preserve an exact selected row, not clear its metadata after a partial query.'
Assert-Source ($deviceWorkspace.Contains('selected.IdentityAmbiguous') -and $deviceWorkspace.Contains('selected.SourceIdentity')) 'Cloud and ambiguous device identities must remain explicitly unverified.'
Assert-Source ($deviceWorkspace.Contains('GetSelectedHomeSearchResult() is not null') -and $deviceWorkspace.Contains('_homeSearchResults.Focus()')) 'Returning to Start with a selected device must focus its row, not restart the search.'
Assert-Source ($layoutSelfTest.Contains('PreviewHelpdeskDeviceWorkspaceForUiSelfTest()') -and $layoutSelfTest.Contains('returning from Helpdesk lost the source-qualified Start selection')) 'Runtime must verify Helpdesk navigation retains an exact selected Start record.'

Assert-Source ($shell.Contains('InvalidateHomeSelectionOnQueryEdit()')) 'Start search edits must drop previous selected-device metadata.'
Assert-Source ($homeSource.Contains('_homeSearchCancellation?.Cancel()') -and $homeSource.Contains('Search text changed. Previous device selection cleared.')) 'Typing a new query must cancel old search work and remove stale device actions.'
Assert-Source ($layoutSelfTest.Contains('editing Start search retained stale device metadata')) 'Runtime UI test must reject old device selection remaining after query edit.'

Assert-Source ($startConnectionSource.Contains('HomeConnectionPrimaryActions') -and $startConnectionSource.Contains('HomeConnectionOptionsToggle') -and $startConnectionSource.Contains('HomeConnectionExtraActions') -and $shell.Contains('HomeConnectionDirectActions')) 'Connect must be the primary entry point; legacy direct actions must remain available behind More options.'
Assert-Source ($startConnectionSource.Contains('_startAdIdentityPanel.Visible = ad && _connectionOptionsExpanded') -and $startConnectionSource.Contains('_startDirectActionsPanel.Visible = _connectionOptionsExpanded')) 'Extra AD controls must stay collapsed until the operator expands Connection options.'
Assert-Source ($startupWizardSource.Contains('Start without a connection') -and $startupWizardSource.Contains('UiStyle.CreateActionButton("Cancel", DialogResult.Cancel)')) 'Offline entry and cancellation must be distinct connection wizard actions.'
Assert-Source ($cloudWizardSource.Contains('CloudWizardPermissionsToggle') -and $cloudWizardSource.Contains('if (_adminSetup.Checked)') -and $cloudWizardSource.Contains('_tenant.Enabled = _approvedApp.Checked')) 'Cloud permissions guidance must be expandable and administrator bootstrap cannot require an existing app.'
Assert-Source ($setupService.Contains('ValidateTenantForAdministrativeSetup(tenant)') -and $setupService.Contains('throw new ArgumentException(tenantError, nameof(tenant))')) 'Privileged Entra setup must reject ambiguous tenant before Graph.'
Assert-Source ($intuneSetup.Contains('PreviewTenantValidationForUiSelfTest()') -and $intuneSetup.Contains('if (!ValidateTenantForSetup())')) 'Intune setup must validate tenant both before Next and Configure & Verify.'
Assert-Source ($cloudMain.Contains('var tenantProblem = EntraSetupService.ValidateTenantForAdministrativeSetup(')) 'First-Run / Repair must not persist or consent to ambiguous tenant.'
Assert-Source ($cloudWizardSource.Contains('applicationId == Guid.Empty')) 'Ordinary Cloud sign-in wizard must reject zero application GUID.'
Assert-Source ($layoutSelfTest.Contains('invalidAdminTenants') -and $layoutSelfTest.Contains('Administrative Graph preflight')) 'Offline UI self-test must reject alias, malformed and empty admin tenants.'
Assert-Source ($cloudMain.Contains('if (dialog.ShowDialog(this) != DialogResult.OK)') -and $cloudMain.Contains('throw new OperationCanceledException(')) 'Closing the Device Code dialog must cancel token polling rather than leave a progress marquee running.'
Assert-Source ($deviceCodeDialog.Contains('DialogResult.Cancel') -and $deviceCodeDialog.Contains('CancelButton = cancel') -and $deviceCodeDialog.Contains('DeviceCodeActions')) 'Device Code must have an explicit Cancel action in a non-wrapping footer.'
Assert-Source ($cloudMain.Contains('_cloudProgress.MarqueeAnimationSpeed = 0;') -and $cloudMain.Contains('_cloudProgress.Visible = false;') -and $cloudMain.Contains('cancellation.IsCancellationRequested')) 'Cancel must stop the Graph marquee immediately and suppress duplicate cancellation.'
Assert-Source ($cloudMain.Contains('!ReferenceEquals(_cloudMaintenanceCancellation, cancellation)') -and $intuneSetup.Contains('!ReferenceEquals(_cancellation, cancellation)')) 'Stale queued Entra progress callbacks must not revive busy status after operation completion.'
Assert-Source ($intuneSetup.Contains('RequestSetupCancellation()') -and $intuneSetup.Contains('_progress.MarqueeAnimationSpeed = 0;')) 'Intune setup cancellation must stop its modal marquee too.'
Assert-Source ($layoutSelfTest.Contains('VerifyDeviceCodeCancellation(failures)') -and $layoutSelfTest.Contains('PreviewCloudCancellationForUiSelfTest()')) 'Native WinForms tests must exercise cancellation without connecting to Microsoft Graph.'
Assert-Source ($helpdeskSettingsSource.Contains('HelpdeskProfilesToggle') -and $helpdeskSettingsSource.Contains('HelpdeskRecoveryPolicyToggle')) 'Infrequent settings must use compact expandable groups.'
Assert-Source ($helpdeskSettingsSource.Contains('HelpdeskRecoveryPolicyOverview') -and $helpdeskSettingsSource.Contains('Saved recovery policy:')) 'Collapsed recovery policies must leave the saved audit gate visible.'
Assert-Source ($helpdesk.Contains('_setHelpdeskRecoveryPolicyExpanded?.Invoke(true)') -and $helpdeskSettingsSource.Contains('SetRecoveryPolicyExpanded(bool expanded)')) 'Helpdesk Setup & Permissions must reveal the policy without simulating a hidden-button click.'
Assert-Source ($layoutSelfTest.Contains('expanded policy was squeezed or clipped by its viewport') -and $layoutSelfTest.Contains('repeated Settings navigation collapsed an open policy')) 'Native WinForms tests must verify expanded policy viewport and idempotent navigation at 100-200% scaling.'
Assert-Source ($layoutSelfTest.Contains('compact Settings must display saved audit policy') -and $layoutSelfTest.Contains('Admin setup must not require existing tenant')) 'Native WinForms UI tests must cover new Settings and Cloud wizard behavior.'

$layoutReset = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.UserWorkspace.cs') -Raw
$paneDrag = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.HomePaneDrag.cs') -Raw
Assert-Source ($helpdeskSettingsSource.Contains('ResetVisualLayoutButton') -and $layoutReset.Contains('private void ResetVisualLayout()')) 'Settings must offer an explicit visual-only reset.'
Assert-Source ($paneDrag.Contains('RestoreDefaultHomePaneOrder()') -and $layoutReset.Contains('UiWorkspacePreferenceService.Save(_workspacePreferences)')) 'Visual reset must restore pane order and persist the current-user window layout.'
Assert-Source ($layoutSelfTest.Contains('reset did not restore the default three-pane order') -and $layoutSelfTest.Contains('reset did not restore compact Updates') -and $layoutSelfTest.Contains('visual reset changed authentication source or recovery policy') -and $layoutSelfTest.Contains('visual reset lost the exact selected device metadata')) 'Native UI tests must verify visual reset preserves selected device and all nonvisual security policy.'
Assert-Source ($layoutReset.Contains('_setConnectionOptionsExpanded?.Invoke(false)') -and $layoutReset.Contains('_setHelpdeskProfilesExpanded?.Invoke(false)') -and $layoutReset.Contains('_setHelpdeskRecoveryPolicyExpanded?.Invoke(false)') -and $layoutSelfTest.Contains('visual reset did not collapse optional Connection and Settings sections')) 'Visual reset must also collapse optional Connection and Settings controls safely.'

Write-Host 'UI-RENDERING-REGRESSION OK: bounded nested tab geometry, wrapping diagnostics, batched resize, native controls and HiDPI.'
