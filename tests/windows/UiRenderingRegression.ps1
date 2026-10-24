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
$helpdeskSettingsSource = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\MainForm.HelpdeskUx.cs') -Raw
$layoutSelfTest = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiLayoutSelfTest.cs') -Raw
$project = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\BitKeyBridge.csproj') -Raw
$telemetry = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiLayoutTelemetry.cs') -Raw
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
Assert-Source ($base.Contains('_responsiveLayoutInitialized') -and
               $base.Contains('UiLayoutTelemetry.RecordResponsiveForm(started)') -and
               [regex]::IsMatch($base, 'UiLayoutTelemetry\.RecordResponsivePhase\s*\(\s*started\s*,\s*initialRefresh\s*,\s*this\s+is\s+MainForm\s*\)') -and
               $telemetry.Contains('ColdMainForm.Summarize("Initial DPI - main window")') -and
               $telemetry.Contains('ColdOtherForms.Summarize("Initial DPI - other windows")')) 'Initial and subsequent DPI/font timings must distinguish the main window from other forms.'
Assert-Source (
    @('ThemeInstall', 'ControlDefaults', 'WorkArea', 'Layout', 'LabelWidths', 'ThemeRefresh') |
        ForEach-Object { $base.Contains('UiLayoutTelemetry.DpiPhase.' + $_) } |
        Where-Object { -not $_ } |
        Measure-Object |
        Select-Object -ExpandProperty Count |
        ForEach-Object { $_ -eq 0 }
) 'Every full DPI phase must retain its independent non-secret timing counter.'
Assert-Source ($telemetry.Contains('DpiThemeInstall.Summarize("DPI phase - theme installation")') -and
               $telemetry.Contains('DpiControlDefaults.Summarize("DPI phase - control defaults")') -and
               $telemetry.Contains('DpiLayout.Summarize("DPI phase - WinForms PerformLayout")') -and
               $telemetry.Contains('DpiLabelWidths.Summarize("DPI phase - label widths")')) 'The DPI report must include all expensive layout-phase counters.'
Assert-Source ($style.Contains('if (label.StatusKind == kind') -and $layoutSelfTest.Contains('Unchanged status text/kind forced a repaint')) 'Repeated unchanged status polls must not repaint.'
Assert-Source ($viewport.Contains('_lastRepairedTabPath') -and $viewport.Contains('UiLayoutTelemetry.RecordViewport(viewportStarted)')) 'Selected-tab layout must avoid repeat repair and expose timing metrics.'
Assert-Source ($diagnostics.Contains('RunUiPerformanceDiagnosticsButton') -and $diagnostics.Contains('UiTabNavigationTelemetry.Report()') -and $telemetry.Contains('Interlocked.Increment')) 'Diagnostics must expose bounded navigation and non-secret repaint timings.'
Assert-Source ($shell.Contains('ApplyLocalizedShellCaptions(rtl)') -and $shell.Contains('RightToLeftLayout = false')) 'Hebrew selection must not re-create all native MainForm controls.'
Assert-Source ($layoutSelfTest.Contains('Language switch left an empty or stale caption')) 'Native UI tests must reject blank/stale captions after Hebrew-Russian-English switches.'
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
