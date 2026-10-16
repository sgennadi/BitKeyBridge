#Requires -Version 7.0
<#
.SYNOPSIS
Fail-closed gate for organization-approved, signed BitKeyBridge releases.
.DESCRIPTION
Verifies real Authenticode trust, the EXACT configured certificate thumbprint,
the PE CPU architecture and the ProductVersion of every executable extracted
from the three release ZIPs. Run only on Windows. Does not execute any EXE,
access recovery secrets, install a certificate or change system trust.
A signed manifest is NOT implied: GitHub provenance attestations protect
the resulting JSON signing report and release checksum file separately.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$ExpectedThumbprint,
    [string]$ExpectedSourceRevision,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'Authenticode release verification requires Windows.' }
if ($Tag -notmatch '^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'Invalid release tag.'
}
$thumbprint = $ExpectedThumbprint.Trim().ToUpperInvariant()
if ($thumbprint -notmatch '^[0-9A-F]{40}$') {
    throw 'Expected signing certificate thumbprint must be exactly 40 hex digits.'
}

$releaseRoot = [System.IO.Path]::GetFullPath($ReleaseDirectory)
if (-not [System.IO.Directory]::Exists($releaseRoot)) {
    throw "Release staging directory does not exist: $releaseRoot"
}
$expectedVersion = $Tag.Substring(1)
$expectedVersionRegex = '^(?i:' + [regex]::Escape($expectedVersion) +
    ')(?:\+(?<revision>[0-9a-fA-F]{7,40}))?$'
$normalizedRevision = $ExpectedSourceRevision.Trim().ToUpperInvariant()
if ($normalizedRevision -and $normalizedRevision -notmatch '^[0-9A-F]{40}$') {
    throw 'ExpectedSourceRevision must be a 40-digit Git source commit SHA.'
}
$firstRevision = $null
$arch = [ordered]@{
    'win-x64'   = [UInt16]0x8664
    'win-x86'   = [UInt16]0x014c
    'win-arm64' = [UInt16]0xaa64
}
$results = [System.Collections.Generic.List[object]]::new()
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('bkb-release-verify-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null

function Get-PeMachine {
    param([Parameter(Mandatory)][string]$File)
    $stream = [System.IO.File]::OpenRead($File)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        try {
            if ($reader.ReadUInt16() -ne 0x5a4d) {
                throw 'PE executable has an invalid MZ header.'
            }
            $stream.Position = 0x3c
            $offset = $reader.ReadInt32()
            if ($offset -lt 64 -or $offset -ge ($stream.Length - 6)) {
                throw 'PE executable has an invalid header offset.'
            }
            $stream.Position = $offset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw 'PE executable has an invalid PE signature.'
            }
            return $reader.ReadUInt16()
        }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

try {
    foreach ($rid in $arch.Keys) {
        $zipPath = Join-Path $releaseRoot "BitKeyBridge-$rid.zip"
        if (-not [System.IO.File]::Exists($zipPath)) {
            throw "Missing mandatory release architecture package: $rid"
        }

        $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $exes = @($archive.Entries | Where-Object {
                $_.FullName.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase)
            })
            if ($exes.Count -ne 1) {
                throw "$rid must contain exactly one EXE; found $($exes.Count)."
            }

            $entry = $exes[0]
            $normalized = $entry.FullName.Replace('\', '/')
            while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
                $normalized = $normalized.Substring(2)
            }
            if (-not [string]::Equals(
                    $normalized, 'BitKeyBridge.exe',
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "$rid contains an EXE outside the ZIP root."
            }
            if ($entry.Length -le 0 -or $entry.Length -gt 536870912) {
                throw "$rid EXE has invalid decompressed size."
            }

            $executable = Join-Path $tempRoot ("BitKeyBridge-" + $rid + ".exe")
            $inputStream = $entry.Open()
            try {
                $outputStream = [System.IO.File]::Create($executable)
                try { $inputStream.CopyTo($outputStream) }
                finally { $outputStream.Dispose() }
            }
            finally { $inputStream.Dispose() }
        }
        finally { $archive.Dispose() }

        $peMachine = Get-PeMachine -File $executable
        if ($peMachine -ne $arch[$rid]) {
            throw ("Wrong PE architecture inside {0}: expected 0x{1:X4}; found 0x{2:X4}." -f
                $rid, $arch[$rid], $peMachine)
        }

        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable)

        # The .NET SDK automatically appends +<source SHA> to
        # AssemblyInformationalVersion. SemVer build metadata does not alter
        # release precedence, but require hex Git identity, not arbitrary text.
        $versionMatch = [regex]::Match(
            ($versionInfo.ProductVersion ?? ''), $expectedVersionRegex)
        if (-not $versionMatch.Success) {
            throw "$rid ProductVersion '$($versionInfo.ProductVersion)' does not match $Tag (+optional Git SHA)."
        }
        $sourceRevision = $versionMatch.Groups['revision'].Value.ToUpperInvariant()
        if ($null -eq $firstRevision) {
            $firstRevision = $sourceRevision
        }
        elseif ($sourceRevision -cne $firstRevision) {
            throw "$rid was compiled from a different source revision than the other release architectures."
        }

        # Tagged production releases must exactly match the source SHA of
        # the GitHub workflow. A signed ZIP built from another commit is refused.
        if ($normalizedRevision -and $sourceRevision -cne $normalizedRevision) {
            throw "$rid source revision is not the approved GitHub release commit."
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $executable
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
            $null -eq $signature.SignerCertificate) {
            throw "$rid Authenticode is not trusted: $($signature.Status)."
        }
        $actualThumbprint = $signature.SignerCertificate.Thumbprint.ToUpperInvariant()
        if ($actualThumbprint -cne $thumbprint) {
            throw "$rid signed by an unexpected certificate: $actualThumbprint."
        }
        if ($signature.SignerCertificate.NotBefore -gt [DateTime]::Now -or
            $signature.SignerCertificate.NotAfter -lt [DateTime]::Now) {
            throw "$rid signing certificate is not currently valid."
        }

        $hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
        $results.Add([pscustomobject]@{
            Architecture = $rid
            PeMachine = ('0x{0:X4}' -f $peMachine)
            ProductVersion = $versionInfo.ProductVersion
            SourceRevision = $sourceRevision
            Sha256 = $hash
            SignerSubject = $signature.SignerCertificate.Subject
            SignerThumbprint = $actualThumbprint
            Authenticode = $signature.Status.ToString()
        })
        Write-Host "Verified $rid; signed ProductVersion=$($versionInfo.ProductVersion); SHA256=$hash"
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $report = [pscustomobject]@{
            Schema = 1
            ReleaseTag = $Tag
            VerifiedAtUtc = [DateTime]::UtcNow.ToString('O')
            ExpectedSignerThumbprint = $thumbprint
            ExpectedSourceRevision = $normalizedRevision
            Executables = @($results.ToArray())
        }
        $outFile = [System.IO.Path]::GetFullPath($OutputPath)
        $outDir = [System.IO.Path]::GetDirectoryName($outFile)
        [System.IO.Directory]::CreateDirectory($outDir) | Out-Null
        $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outFile -Encoding utf8
        Write-Host "Wrote signed-payload verification evidence: $outFile"
    }
}
finally {
    # Do not retain extracted executables or certificate metadata in temp.
    if ([System.IO.Directory]::Exists($tempRoot)) {
        [System.IO.Directory]::Delete($tempRoot, $true)
    }
}
