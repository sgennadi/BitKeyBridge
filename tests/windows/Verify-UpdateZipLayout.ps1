#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ArchivePath,[Parameter(Mandatory)][string]$ExpectedExecutable)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$expected = Get-Item -LiteralPath $ExpectedExecutable
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
try {
    if ($archive.Entries.Count -ne 1) { throw 'Unexpected update ZIP entry count.' }
    $entry = $archive.Entries[0]
    if ($entry.FullName -cne 'BitKeyBridge.exe' -or
        $entry.Name -cne 'BitKeyBridge.exe' -or
        $entry.Length -ne $expected.Length -or
        $entry.Length -le 0 -or $entry.Length -gt 536870912) {
        throw 'Unexpected update ZIP entry, path or size.'
    }
    if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
        throw 'Update ZIP contains a symbolic link.'
    }
    $stream = $entry.Open()
    try {
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
    }
    finally { $stream.Dispose() }
    if ($actual -cne (Get-FileHash -LiteralPath $expected.FullName -Algorithm SHA256).Hash) {
        throw 'Archive contents differ from tested executable.'
    }
    Write-Host "Updater-compatible ZIP verified: $ArchivePath"
}
finally { $archive.Dispose() }
