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
$layoutSelfTest = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\UI\UiLayoutSelfTest.cs') -Raw
$project = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\BitKeyBridge.csproj') -Raw

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
Assert-Source ($theme.Contains('root.ControlAdded += (_, args) => Apply(args.Control)')) 'Newly added controls must receive the theme.'
Assert-Source ($theme.Contains('IsPrimaryAction(button)')) 'Primary actions must receive centralized accent styling.'
Assert-Source ($theme.Contains('TextFormatFlags.EndEllipsis')) 'The active tab must remain bounded when translated captions do not fit.'
Assert-Source (-not [regex]::IsMatch($theme, '(?s)case DataGridView grid:.*?SetupRoundedRegion\(grid')) 'Data grids must not recreate GDI clipping regions.'
Assert-Source (-not [regex]::IsMatch($theme, '(?s)case TextBoxBase edit:.*?SetupRoundedRegion\(edit')) 'Native text controls must keep native cursor/border rendering.'
Assert-Source ($base.Contains('_responsiveLabelTimer') -and $base.Contains('_responsiveLayoutTimer')) 'Font/DPI/label reflows must be coalesced.'
Assert-Source ($base.Contains('ClientSize.Width == lastMeasuredWidth')) 'Height-only resize must not remeasure every wrapping label.'
Assert-Source ($style.Contains('var updatingWidths = false')) 'Vertical workspace resize must guard nested Layout callbacks.'
Assert-Source ($style.Contains('panel.ResumeLayout(performLayout: true)')) 'AutoSize sections must receive one final stable layout.'
Assert-Source (-not [regex]::IsMatch($theme + $viewport + $base, 'WS_EX_COMPOSITED')) 'Do not force global composited windows: this hurts Remote Desktop.'
Assert-Source ($viewport.Contains('RepairSelectedTabHierarchy(_mainTabs)')) 'MainForm must reflow the selected visible tab chain.'
Assert-Source ($viewport.Contains('RepairSelectedTabHierarchy(nested)')) 'Diagnostics, Settings and Devices nested tabs must be repaired recursively.'
Assert-Source ($viewport.Contains('flow.PerformLayout()')) 'Visible vertical flow sections must be measured after nested-tab activation.'
Assert-Source ($style.Contains('child is TableLayoutPanel || child is FlowLayoutPanel')) 'AutoSize action flows must be horizontally constrained as well as table sections.'
Assert-Source ($style.Contains('panel.VisibleChanged += (_, _) => ResizeChildren()')) 'Previously hidden pages must remeasure widths on selection.'
Assert-Source ($shell.Contains('page.AutoScroll = false')) 'Outer TabPage AutoScroll must not fight nested Dock=Fill viewports.'
Assert-Source ($shell.Contains('nested.SelectedIndexChanged += (_, _) => QueueMainViewportReflow()')) 'Nested tab switches must schedule geometry repair.'
Assert-Source ($diagnostics.Contains('DiagnosticToolsWorkspace') -and $diagnostics.Contains('DiagnosticToolsActions')) 'Diagnostics layout must expose stable regression targets.'
Assert-Source ($layoutSelfTest.Contains('VerifyNestedWorkspaceViewport(main, formName, scenario, failures)')) 'WinForms runtime must test real nested sizes after tab switches.'
Assert-Source ($layoutSelfTest.Contains('content.Size = new Size(') -and $layoutSelfTest.Contains('main.ReflowMainViewport()')) 'Runtime must reproduce a stale 200x100-style content viewport.'

Assert-Source ($helpdesk.Contains('HelpdeskOpenDeviceWorkspaceButton') -and $helpdesk.Contains('HelpdeskSelectedDeviceSummary')) 'Helpdesk must offer the canonical Start workspace and its selected-device metadata.'
Assert-Source (-not $helpdesk.Contains('HelpdeskQuickSearch')) 'Helpdesk must not have its own duplicate search input.'
Assert-Source ($shell.Contains('4 => _homeQuery')) 'Ctrl+F on Helpdesk must route to the canonical Start search.'
Assert-Source ($computerContext.Contains('GetSelectedHomeSearchResult() is { } selected')) 'Returning to Start must preserve an exact selected row, not clear its metadata after a partial query.'
Assert-Source ($deviceWorkspace.Contains('selected.IdentityAmbiguous') -and $deviceWorkspace.Contains('selected.SourceIdentity')) 'Cloud and ambiguous device identities must remain explicitly unverified.'
Assert-Source ($layoutSelfTest.Contains('PreviewHelpdeskDeviceWorkspaceForUiSelfTest()') -and $layoutSelfTest.Contains('returning from Helpdesk lost the source-qualified Start selection')) 'Runtime must verify Helpdesk navigation retains an exact selected Start record.'

Write-Host 'UI-RENDERING-REGRESSION OK: bounded nested tab geometry, wrapping diagnostics, batched resize, native controls and HiDPI.'
