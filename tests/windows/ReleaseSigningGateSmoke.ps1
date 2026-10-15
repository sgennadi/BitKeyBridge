#Requires -Version 7.0
<#
Negative security regression test. Uses an unsigned/incorrectly signed
local build under a fake x64/x86/ARM64 release layout. The release gate
must reject the candidate. No trust, certs or release settings are altered.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Executable)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Release gate smoke test requires Windows.' }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw 'Build executable is missing.'
}
$validator = Join-Path $PSScriptRoot 'Verify-ReleaseSigning.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) ('bkb-signing-smoke-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $root 'content'
$releaseDir = Join-Path $root 'release'
[System.IO.Directory]::CreateDirectory($source) | Out-Null
[System.IO.Directory]::CreateDirectory($releaseDir) | Out-Null

try {
    Copy-Item -LiteralPath $Executable -Destination (Join-Path $source 'BitKeyBridge.exe')
    # Three x64 test payloads deliberately cannot constitute a valid
    # multi-architecture release, regardless of Authenticode configuration.
    foreach ($rid in @('win-x64', 'win-x86', 'win-arm64')) {
        $zip = Join-Path $releaseDir ("BitKeyBridge-" + $rid + ".zip")
        [System.IO.Compression.ZipFile]::CreateFromDirectory($source, $zip)
    }
    $report = Join-Path $root 'signatures.json'
    $args = @(
        '-NoProfile', '-NonInteractive', '-File', $validator,
        '-ReleaseDirectory', $releaseDir,
        '-Tag', 'v0.30.0-rc.2',
        '-ExpectedThumbprint', ('0' * 40),
        '-OutputPath', $report
    )
    & (Get-Command pwsh).Source @args 2>&1 | Out-Null
    $exit = $LASTEXITCODE
    if ($exit -eq 0) {
        throw 'SECURITY REGRESSION: unsigned/mismatched release package passed signing verification.'
    }
    if (Test-Path -LiteralPath $report) {
        throw 'SECURITY REGRESSION: failed signature verification produced an approval report.'
    }

    # An unset/invalid fingerprint may not mean "allow any signer."
    $args = @(
        '-NoProfile', '-NonInteractive', '-File', $validator,
        '-ReleaseDirectory', $releaseDir,
        '-Tag', 'v0.30.0-rc.2',
        '-ExpectedThumbprint', 'not-a-sha1'
    )
    & (Get-Command pwsh).Source @args 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        throw 'SECURITY REGRESSION: invalid signing certificate thumbprint was accepted.'
    }

    # Expected child-process failures must not make the outer pwsh step fail.
    # Release approval was correctly denied above; this CI regression PASSES.
    $global:LASTEXITCODE = 0
    Write-Host 'Release signing gate negative regressions passed.'
}
finally {
    if ([System.IO.Directory]::Exists($root)) {
        [System.IO.Directory]::Delete($root, $true)
    }
}
