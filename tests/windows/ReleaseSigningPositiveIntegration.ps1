#Requires -Version 7.0
<#
Ephemeral CI ONLY: positive Authenticode release-verifier integration.
Builds an intentionally short-lived, non-exportable CURRENT-USER test
certificate, signs copies of all 3 architecture payloads, validates, and
tests signer, ProductVersion, PE architecture and unsigned failure cases.
Never uploads signing keys, changes LocalMachine trust, or publishes assets.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArtifactRoot,
    [switch]$EphemeralHostedRunner
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $EphemeralHostedRunner -or -not $IsWindows -or $env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or -not $env:RUNNER_TEMP) {
    throw 'Signing integration requires explicit GitHub-hosted ephemeral runner opt-in.'
}

$validator = Join-Path $PSScriptRoot 'Verify-ReleaseSigning.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw 'Signature verifier is missing.' }
$tool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if ($tool) {
    $signtool = $tool.Source
} else {
    $kitRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $candidate = Get-ChildItem -LiteralPath $kitRoot -Filter signtool.exe -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like '*\x64\signtool.exe' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $candidate) { throw 'Windows SDK SignTool is missing.' }
    $signtool = $candidate.FullName
}

$root = Join-Path $env:RUNNER_TEMP ('bkb-signing-positive-' + [guid]::NewGuid().ToString('N'))
$release = Join-Path $root 'release'
[IO.Directory]::CreateDirectory($release) | Out-Null
$cert = $null
$public = $null
$rootStore = $null
$personal = $null
$thumbprint = ''
$tag = 'v0.30.0-rc.2'
$rids = @('win-x64', 'win-x86', 'win-arm64')
$original = @{}

function Require-Rejection {
    param([string]$Case, [string]$Pin, [string]$Version)
    $base = Join-Path $root ('negative-' + [guid]::NewGuid().ToString('N'))
    $out = "$base.stdout"
    $err = "$base.stderr"
    $report = "$base.json"
    try {
        $arguments = @('-NoProfile', '-NonInteractive', '-File', $validator, '-ReleaseDirectory', $release, '-Tag', $Version, '-ExpectedThumbprint', $Pin, '-OutputPath', $report)
        $p = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -Wait -PassThru -NoNewWindow -RedirectStandardOutput $out -RedirectStandardError $err
        if ($p.ExitCode -eq 0) { throw "SECURITY FAILURE: $Case was accepted." }
        if (Test-Path -LiteralPath $report) { throw "SECURITY FAILURE: $Case generated approval evidence." }
        Write-Host "Correctly refused $Case"
    } finally {
        Remove-Item -LiteralPath $out, $err, $report -Force -ErrorAction SilentlyContinue
    }
}

try {
    foreach ($rid in $rids) {
        $zip = Join-Path (Join-Path $ArtifactRoot $rid) ("BitKeyBridge-" + $rid + ".zip")
        if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { throw "Missing test artifact $rid" }
        $original[$rid] = [IO.Path]::GetFullPath($zip)
        $folder = Join-Path $root ("stage-" + $rid)
        [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($zip), $folder)
        if (-not (Test-Path -LiteralPath (Join-Path $folder 'BitKeyBridge.exe') -PathType Leaf)) { throw "No executable inside $rid" }
    }

    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=BitKeyBridge Ephemeral CI Code Signing' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyExportPolicy NonExportable -HashAlgorithm SHA256 -NotAfter (Get-Date).AddDays(3)
    if (-not $cert -or -not $cert.HasPrivateKey) { throw 'Could not create non-exportable CI signing key.' }
    $thumbprint = $cert.Thumbprint.ToUpperInvariant()
    $public = [Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    $rootStore = [Security.Cryptography.X509Certificates.X509Store]::new([Security.Cryptography.X509Certificates.StoreName]::Root, [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $rootStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $rootStore.Add($public)

    foreach ($rid in $rids) {
        $folder = Join-Path $root ("stage-" + $rid)
        $exe = Join-Path $folder 'BitKeyBridge.exe'
        & $signtool sign /s My /sha1 $thumbprint /fd SHA256 "$exe"
        if ($LASTEXITCODE -ne 0) { throw "Ephemeral signer could not sign $rid." }
        # Avoid a redundant SignTool network/trust lookup. The actual
        # release validator performs Authenticode validation below.
        # Store-only ZIP is sufficient to exercise archive verification and
        # avoids CPU-heavy recompression of three large single-file EXEs.
        Write-Host "Ephemeral signer prepared $rid; packaging without compression."
        [IO.Compression.ZipFile]::CreateFromDirectory($folder, (Join-Path $release ("BitKeyBridge-" + $rid + ".zip")), [IO.Compression.CompressionLevel]::NoCompression, $false)
    }

    $reportPath = Join-Path $root 'approved-signing.json'
    & $validator -ReleaseDirectory $release -Tag $tag -ExpectedThumbprint $thumbprint -OutputPath $reportPath
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw 'Signed release failed to generate evidence.' }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.ReleaseTag -ne $tag -or @($report.Executables).Count -ne 3) {
        throw 'Signed release evidence is incomplete.'
    }
    foreach ($row in $report.Executables) {
        if ($row.SignerThumbprint -cne $thumbprint -or $row.ProductVersion -ne $tag.Substring(1) -or $row.Authenticode -ne 'Valid') {
            throw "Incorrect positive signing evidence for $($row.Architecture)"
        }
    }
    Write-Host 'Positive signed x64/x86/ARM64 verification PASSED.'

    $wrongPin = '0' * 40
    if ($wrongPin -ceq $thumbprint) { $wrongPin = 'F' * 40 }
    Require-Rejection -Case 'wrong signer thumbprint' -Pin $wrongPin -Version $tag
    Require-Rejection -Case 'wrong ProductVersion' -Pin $thumbprint -Version 'v0.30.0-rc.3'

    $arm = Join-Path $release 'BitKeyBridge-win-arm64.zip'
    $armBackup = Join-Path $root 'original-arm64.zip'
    Copy-Item -LiteralPath $arm -Destination $armBackup
    try {
        Copy-Item -LiteralPath (Join-Path $release 'BitKeyBridge-win-x64.zip') -Destination $arm -Force
        Require-Rejection -Case 'wrong ARM64 PE machine' -Pin $thumbprint -Version $tag
    } finally {
        Copy-Item -LiteralPath $armBackup -Destination $arm -Force
    }

    $x64 = Join-Path $release 'BitKeyBridge-win-x64.zip'
    $x64Backup = Join-Path $root 'signed-x64.zip'
    Copy-Item -LiteralPath $x64 -Destination $x64Backup
    try {
        Copy-Item -LiteralPath $original['win-x64'] -Destination $x64 -Force
        Require-Rejection -Case 'unsigned x64 replacement' -Pin $thumbprint -Version $tag
    } finally {
        Copy-Item -LiteralPath $x64Backup -Destination $x64 -Force
    }

    Write-Host 'Ephemeral signature validation positive/negative integration PASSED.'
} finally {
    # Remove ephemeral trust in reverse order; no signing certificate leaves CI.
    if ($rootStore) {
        try {
            if ($public) { $rootStore.Remove($public) }
        } finally { $rootStore.Close() }
    }
    if ($cert) {
        $personal = [Security.Cryptography.X509Certificates.X509Store]::new([Security.Cryptography.X509Certificates.StoreName]::My, [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        try {
            $personal.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $personal.Remove($cert)
        } finally { $personal.Close() }
    }
    if ($public) { $public.Dispose() }
    if ($cert) { $cert.Dispose() }
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
}
