#Requires -Version 7.0
<#
Read-only regression: saving normal update settings may NEVER clear an
automatic-failure or deliberate-rollback hold. The separate explicit
Resume Auto-Install action retains that permission after trust checks.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ui = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../src/BitKeyBridge/UI/MainForm.Updates.cs') -Raw
$saveStart = $ui.IndexOf('private bool SaveUpdateSettings(', [StringComparison]::Ordinal)
$saveEnd = $ui.IndexOf('private async Task<bool> TryAutomaticUpdateOnStartAsync(', $saveStart + 1, [StringComparison]::Ordinal)
if ($saveStart -lt 0 -or $saveEnd -le $saveStart) {
    throw 'Cannot locate exactly bounded update settings save method.'
}
$saveBody = $ui.Substring($saveStart, $saveEnd - $saveStart)
if ($saveBody -match 'UpdateRollbackHoldService\s*\.\s*Clear\s*\(') {
    throw 'Normal update settings save cleared a failed-update or rollback hold.'
}

$resumeUi = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../src/BitKeyBridge/UI/MainForm.AdminResponsive.cs') -Raw
$resumeStart = $resumeUi.IndexOf('resumeAutoInstall.Click +=', [StringComparison]::Ordinal)
if ($resumeStart -lt 0) { throw 'Explicit Resume Auto-Install handler was removed.' }
$resumeBody = $resumeUi.Substring($resumeStart, [Math]::Min(5000, $resumeUi.Length - $resumeStart))
if ($resumeBody -notmatch 'UpdateTrustPolicy\.CanEnableUnattended' -or
    $resumeBody -notmatch 'SaveUpdateSettings\(showConfirmation: false\)' -or
    $resumeBody -notmatch 'UpdateRollbackHoldService\.Clear\s*\(') {
    throw 'Resume button must check update trust and explicitly clear the rollback hold.'
}
Write-Host 'Update holds survive ordinary settings saves; explicit Resume is preserved.'
