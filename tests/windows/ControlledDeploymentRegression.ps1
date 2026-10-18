#Requires -Version 7.0
<#
.SYNOPSIS
Offline, non-elevated regression for the GPO/Intune approved-release installer.
No real EXE is installed, no GPO is linked, and no auto-update consent changes.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$installer = Join-Path $PSScriptRoot '../../deployment/gpo/Deploy-BitKeyBridge.cmd'
if (-not (Test-Path -LiteralPath $installer -PathType Leaf))
{
    throw 'Controlled GPO installer missing.'
}
$sourceText = Get-Content -LiteralPath $installer -Raw
foreach ($forbidden in @(
    '--accept-unsigned-update-risk',
    '--auto-update-enable',
    '--update-task-install',
    'schtasks /create',
    'Stop-Process',
    'taskkill'
))
{
    if ($sourceText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Installer unexpectedly contains forbidden unattended consent/force-stop command."
    }
}

function Invoke-Installer {
    param(
        [string[]]$Arguments,
        [int]$ExpectedExit,
        [string]$ExpectedMessage = ''
    )
    $output = (& $installer @Arguments 2>&1 | Out-String)
    $actual = $LASTEXITCODE
    if ($actual -ne $ExpectedExit -or
        ($ExpectedMessage -and
         -not $output.Contains($ExpectedMessage, [StringComparison]::OrdinalIgnoreCase)))
    {
        throw "Installer regression returned $actual instead of $ExpectedExit."
    }
}

$root = Join-Path ([IO.Path]::GetTempPath()) (
    'BKB-ManagedDeploymentTest-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try
{
    $candidate = Join-Path $root 'BitKeyBridge.exe'
    [IO.File]::WriteAllBytes($candidate,
        [Text.Encoding]::UTF8.GetBytes('synthetic non-executable approved hash input'))
    $pin = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
    $allow = Join-Path $root 'PilotComputers.txt'
    [IO.File]::WriteAllText($allow, [Environment]::MachineName + [Environment]::NewLine,
        [Text.Encoding]::ASCII)

    Invoke-Installer -Arguments @($candidate, $pin, $allow,
        '0.30.5', 'win-x64', '/verify-only') -ExpectedExit 0 -ExpectedMessage 'PASS:'
    Invoke-Installer -Arguments @($candidate, ('0' * 64), $allow,
        '0.30.5', 'win-x64', '/verify-only') -ExpectedExit 2 -ExpectedMessage 'SHA256'

    Invoke-Installer -Arguments @($candidate, $pin, $allow,
        '0.30.5', 'win-arm64', '/verify-only') -ExpectedExit 2 -ExpectedMessage 'architecture'

    $other = Join-Path $root 'OtherDevices.txt'
    [IO.File]::WriteAllText($other, 'ANOTHER-COMPUTER' + [Environment]::NewLine,
        [Text.Encoding]::ASCII)
    Invoke-Installer -Arguments @($candidate, $pin, $other,
        '0.30.5', 'win-x64', '/verify-only') -ExpectedExit 0 -ExpectedMessage 'SKIP:'

    [IO.File]::AppendAllText($candidate, '-TAMPERED')
    Invoke-Installer -Arguments @($candidate, $pin, $allow,
        '0.30.5', 'win-x64', '/verify-only') -ExpectedExit 2 -ExpectedMessage 'SHA256'

    foreach ($test in @(
        @{Old='0.30.3'; New='0.30.5'; Code=0},
        @{Old='0.30.4'; New='0.30.5'; Code=0},
        @{Old='0.30.5'; New='0.30.5'; Code=1},
        @{Old='0.31.0'; New='0.30.5'; Code=1},
        @{Old='0.32.0-dev.1'; New='0.30.5'; Code=1},
        @{Old='not-a-version'; New='0.30.5'; Code=1}
    ))
    {
        Invoke-Installer -Arguments @('/test-version-order',
            $test.Old, $test.New) -ExpectedExit $test.Code
    }
    Write-Host 'Canary allowlist, exact SHA pin, tamper rejection and rollback prevention passed.'
}
finally
{
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
