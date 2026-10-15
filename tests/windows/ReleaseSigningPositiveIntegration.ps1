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
    # Inspect only version/x64/signTool.exe slots; recursive SDK scans
    # can be unexpectedly slow on some GitHub-hosted Windows images.
    $candidate = $null
    foreach ($versionFolder in (Get-ChildItem -LiteralPath $kitRoot -Directory -ErrorAction Stop | Sort-Object Name -Descending)) {
        $candidatePath = Join-Path (Join-Path $versionFolder.FullName 'x64') 'signtool.exe'
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            $candidate = $candidatePath
            break
        }
    }
    if (-not $candidate) { throw 'Windows SDK x64 SignTool is missing.' }
    $signtool = $candidate
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
        $p = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -PassThru -NoNewWindow -RedirectStandardOutput $out -RedirectStandardError $err
        try {
            if (-not $p.WaitForExit(120000)) {
                $p.Kill($true)
                throw "Negative signing case timed out rather than correctly rejected: $Case"
            }
            if ($p.ExitCode -eq 0) { throw "SECURITY FAILURE: $Case was accepted." }
        }
        finally { $p.Dispose() }
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
        Write-Host "Extracting $rid for isolated signature verification..."
        [IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($zip), $folder)
        if (-not (Test-Path -LiteralPath (Join-Path $folder 'BitKeyBridge.exe') -PathType Leaf)) { throw "No executable inside $rid" }
    }

    Write-Host 'Creating ephemeral, non-exportable signing key (bounded to 120 seconds)...'
    $certJob = Start-Job -ScriptBlock {
        $ErrorActionPreference = 'Stop'
        $newCert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=BitKeyBridge Ephemeral CI Code Signing' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyExportPolicy NonExportable -HashAlgorithm SHA256 -NotAfter (Get-Date).AddDays(3)
        if (-not $newCert -or -not $newCert.HasPrivateKey) { throw 'Ephemeral certificate creation failed.' }
        Write-Output $newCert.Thumbprint
    }
    try {
        if (-not (Wait-Job -Job $certJob -Timeout 120)) {
            Stop-Job -Job $certJob -ErrorAction SilentlyContinue
            throw 'Ephemeral signing certificate creation timed out after 120 seconds.'
        }
        $generated = @(Receive-Job -Job $certJob -ErrorAction Stop | Where-Object { $_ })
        if ($generated.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$generated[0])) {
            throw 'Ephemeral signing certificate did not return one thumbprint.'
        }
        $cert = Get-Item -LiteralPath ("Cert:\CurrentUser\My\" + [string]$generated[0]) -ErrorAction Stop
        if (-not $cert.HasPrivateKey) { throw 'Ephemeral signing certificate has no usable private key.' }
    }
    finally { Remove-Job -Job $certJob -Force -ErrorAction SilentlyContinue }
    $thumbprint = $cert.Thumbprint.ToUpperInvariant()
    $public = [Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    $rootStore = [Security.Cryptography.X509Certificates.X509Store]::new([Security.Cryptography.X509Certificates.StoreName]::Root, [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $rootStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $rootStore.Add($public)

    foreach ($rid in $rids) {
        $folder = Join-Path $root ("stage-" + $rid)
        $exe = Join-Path $folder 'BitKeyBridge.exe'
        Write-Host "Signing $rid using temporary Windows certificate..."
        $signOut = Join-Path $root ("sign-" + $rid + ".stdout")
        $signErr = Join-Path $root ("sign-" + $rid + ".stderr")
        $signArgs = @('sign', '/s', 'My', '/sha1', $thumbprint, '/fd', 'SHA256', $exe)
        $signProcess = Start-Process -FilePath $signtool -ArgumentList $signArgs -NoNewWindow -PassThru -RedirectStandardOutput $signOut -RedirectStandardError $signErr
        try {
            if (-not $signProcess.WaitForExit(120000)) {
                $signProcess.Kill($true)
                throw "Ephemeral SignTool timed out for $rid after 120 seconds."
            }
            if ($signProcess.ExitCode -ne 0) {
                $signError = if (Test-Path -LiteralPath $signErr) { Get-Content -LiteralPath $signErr -Raw } else { '' }
                throw "Ephemeral SignTool failed for $rid (exit $($signProcess.ExitCode)): $signError"
            }
        }
        finally { $signProcess.Dispose() }
        # Avoid a redundant SignTool network/trust lookup. The actual
        # release validator performs Authenticode validation below.
        # Store-only ZIP is sufficient to exercise archive verification and
        # avoids CPU-heavy recompression of three large single-file EXEs.
        Write-Host "Ephemeral signer prepared $rid; packaging without compression."
        [IO.Compression.ZipFile]::CreateFromDirectory($folder, (Join-Path $release ("BitKeyBridge-" + $rid + ".zip")), [IO.Compression.CompressionLevel]::NoCompression, $false)
    }

    Write-Host 'Verifying signed x64, x86 and ARM64 release packages (bounded to 240 seconds)...'
    $reportPath = Join-Path $root 'approved-signing.json'
    $positiveOut = Join-Path $root 'positive-signature.stdout'
    $positiveErr = Join-Path $root 'positive-signature.stderr'
    $positiveArgs = @('-NoProfile', '-NonInteractive', '-File', $validator, '-ReleaseDirectory', $release, '-Tag', $tag, '-ExpectedThumbprint', $thumbprint, '-OutputPath', $reportPath)
    $positive = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $positiveArgs -NoNewWindow -PassThru -RedirectStandardOutput $positiveOut -RedirectStandardError $positiveErr
    try {
        if (-not $positive.WaitForExit(240000)) {
            $positive.Kill($true)
            throw 'Full signed release verification timed out after 240 seconds.'
        }
        if ($positive.ExitCode -ne 0) {
            $diagnostic = if (Test-Path -LiteralPath $positiveErr) { Get-Content -LiteralPath $positiveErr -Raw } else { '' }
            throw "Full signed release verification failed (exit $($positive.ExitCode)): $diagnostic"
        }
    }
    finally { $positive.Dispose() }
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
