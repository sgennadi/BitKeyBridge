#Requires -Version 7.0
<#
 Read-only published-EXE regression. This NEVER installs, enables a task,
 clears a rollback hold, discloses a recovery secret or downloads an update.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$exe = (Resolve-Path -LiteralPath $Executable).Path
$out = Join-Path $env:RUNNER_TEMP ("bitkeybridge-center-" + [guid]::NewGuid().ToString('N') + '.json')
$err = $out + '.err'
try {
    $proc = Start-Process -FilePath $exe -ArgumentList @(
        '--update-center-status', '--no-elevation'
    ) -PassThru -Wait -RedirectStandardOutput $out -RedirectStandardError $err
    if ($proc.ExitCode -ne 0) {
        throw "Update Center CLI returned $($proc.ExitCode): $(Get-Content -LiteralPath $err -Raw)"
    }

    $json = Get-Content -LiteralPath $out -Raw | ConvertFrom-Json
    if ($null -eq $json.currentVersion -or
        [string]::IsNullOrWhiteSpace([string]$json.currentVersion) -or
        $null -eq $json.channel -or
        $null -eq $json.installationState -or
        $null -eq $json.task -or
        [string]::IsNullOrWhiteSpace([string]$json.task.status)) {
        throw 'Update Center CLI JSON is missing required read-only state.'
    }

    if ($json.automaticInstallationPermitted -and
        -not $json.automaticInstallConfigured) {
        throw 'Update Center incorrectly reported automatic install permission.'
    }
    # Non-mutating manual Retry is security-relevant: it must not save any
    # unrelated unsigned consent, publisher or channel fields from the GUI.
    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../src/BitKeyBridge/UI/MainForm.UpdateCenter.cs') -Raw
    $start = $source.IndexOf('private async Task RetryUpdateCenterCheckAsync()', [StringComparison]::Ordinal)
    $end = $source.IndexOf('private void TrySetUpdateCenterAutoInstall(', $start + 1, [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -le $start) {
        throw 'Update Center Retry handler could not be isolated.'
    }
    $body = $source.Substring($start, $end - $start)
    if ($body -match 'SaveUpdateSettings\s*\(' -or
        $body -match 'ConfigService\.SaveAppConfig\s*\(' -or
        $body -notmatch 'ConfigService\.LoadAppConfig\s*\(' -or
        $body -notmatch 'updater\.CheckAsync\s*\(') {
        throw 'Update Center Retry must read saved configuration and never rewrite policy.'
    }

    Write-Host 'Update Center CLI JSON, task query and non-mutating Retry passed.'
}
finally {
    Remove-Item -LiteralPath $out, $err -Force -ErrorAction SilentlyContinue
}
