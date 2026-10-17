#Requires -Version 7.0
<#
.SYNOPSIS
Regression tests for the exact single-EXE ZIP format required by legacy updaters.
.DESCRIPTION
Uses synthetic, non-executable bytes and temporary files only. No network,
release publication, elevation or installation is performed.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

$validator = Join-Path $PSScriptRoot 'Verify-UpdateZipLayout.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw 'Update ZIP validator was not found.'
}

function New-TestArchive {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [object[]]$Entries
    )
    $zip = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($spec in $Entries) {
            $entry = $zip.CreateEntry([string]$spec.Path, [IO.Compression.CompressionLevel]::Optimal)
            if ($spec.IsLink) {
                # Unix symbolic-link file mode stored in the ZIP high 16 bits.
                $entry.ExternalAttributes = -1610612736
            }
            $payload = [byte[]]$spec.Data
            $stream = $entry.Open()
            try {
                if ($payload.Length -ne 0) {
                    $stream.Write($payload, 0, $payload.Length)
                }
            }
            finally { $stream.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ('BitKeyBridge-ZipTest-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
try {
    $expectedExe = Join-Path $root 'BitKeyBridge.exe'
    $bytes = [Text.Encoding]::UTF8.GetBytes('BitKeyBridge synthetic test bytes 2026')
    [IO.File]::WriteAllBytes($expectedExe, $bytes)
    $changed = [byte[]]$bytes.Clone()
    $changed[0] = [byte]($changed[0] -bxor 1)

    $exe = @{ Path = 'BitKeyBridge.exe'; Data = $bytes; IsLink = $false }
    $scenarios = @(
        @{ Name = 'single-root-exe'; ShouldPass = $true; Entries = @($exe) },
        @{ Name = 'extra-test-log'; ShouldPass = $false; Entries = @(
            $exe, @{ Path = 'ui-self-test.log'; Data = $bytes; IsLink = $false }
        ) },
        @{ Name = 'nested-path'; ShouldPass = $false; Entries = @(
            @{ Path = 'publish/BitKeyBridge.exe'; Data = $bytes; IsLink = $false }
        ) },
        @{ Name = 'tar-root-marker'; ShouldPass = $false; Entries = @(
            @{ Path = './'; Data = [byte[]]@(); IsLink = $false }, $exe
        ) },
        @{ Name = 'duplicate-exe'; ShouldPass = $false; Entries = @($exe, $exe) },
        @{ Name = 'mismatched-hash'; ShouldPass = $false; Entries = @(
            @{ Path = 'BitKeyBridge.exe'; Data = $changed; IsLink = $false }
        ) },
        @{ Name = 'empty-exe'; ShouldPass = $false; Entries = @(
            @{ Path = 'BitKeyBridge.exe'; Data = [byte[]]@(); IsLink = $false }
        ) },
        @{ Name = 'symlink-exe'; ShouldPass = $false; Entries = @(
            @{ Path = 'BitKeyBridge.exe'; Data = $bytes; IsLink = $true }
        ) }
    )

    foreach ($scenario in $scenarios) {
        $archivePath = Join-Path $root ($scenario.Name + '.zip')
        New-TestArchive -Path $archivePath -Entries $scenario.Entries
        $rejected = $false
        try {
            & $validator -ArchivePath $archivePath -ExpectedExecutable $expectedExe | Out-Null
        }
        catch {
            $rejected = $true
        }
        if ([bool]$scenario.ShouldPass -eq $rejected) {
            throw "Unexpected ZIP validation outcome: $($scenario.Name), rejected=$rejected."
        }
        Write-Host "ZIP regression passed: $($scenario.Name)"
    }
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
