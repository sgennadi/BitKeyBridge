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

    # New-SelfSignedCertificate can hang inside a hosted PowerShell job.
    # Create a short-lived test CA + leaf using pure .NET APIs, then import
    # only the leaf's private key in CURRENT-USER My for SignTool.
    # PFX bytes/password never touch disk or repository artifacts.
    Write-Host 'Creating ephemeral signing CA and leaf through .NET CertificateRequest...'
    $rootKey = $null
    $leafKey = $null
    $caCertificate = $null
    $issuedCertificate = $null
    $signedLeaf = $null
    $pfxBytes = $null
    $password = [Guid]::NewGuid().ToString('N')
    $begin = [DateTimeOffset]::UtcNow.AddMinutes(-5)
    $endTime = [DateTimeOffset]::UtcNow.AddDays(2)
    try {
        $rootKey = [Security.Cryptography.RSA]::Create(2048)
        $caRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=BitKeyBridge Ephemeral CI Root', $rootKey,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $caRequest.CertificateExtensions.Add(
            [Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new(
                $true, $false, 0, $true))
        $caRequest.CertificateExtensions.Add(
            [Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
                [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign -bor
                [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::CrlSign, $true))
        $caCertificate = $caRequest.CreateSelfSigned($begin, $endTime)

        $leafKey = [Security.Cryptography.RSA]::Create(2048)
        $leafRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=BitKeyBridge Ephemeral CI Code Signing', $leafKey,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $leafRequest.CertificateExtensions.Add(
            [Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new(
                $false, $false, 0, $true))
        $leafRequest.CertificateExtensions.Add(
            [Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
                [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
                $true))
        $eku = [Security.Cryptography.OidCollection]::new()
        [void]$eku.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        $leafRequest.CertificateExtensions.Add(
            [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
                $eku, $true))
        $serial = [byte[]]::new(16)
        [Security.Cryptography.RandomNumberGenerator]::Fill($serial)
        $serial[0] = $serial[0] -band 0x7f
        $issuedCertificate = $leafRequest.Create($caCertificate, $begin, $endTime, $serial)
        # PowerShell does not bind C# extension methods as instance members.
        # Invoke the RSA certificate extension statically instead.
        $signedLeaf = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey(
            $issuedCertificate, $leafKey)
        $pfxBytes = $signedLeaf.Export(
            [Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
            $password)
        $importFlags =
            [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::UserKeySet -bor
            [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet
        $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $pfxBytes, $password, $importFlags)
        if (-not $cert.HasPrivateKey) { throw 'No private key in ephemeral signing leaf.' }
        $personal = [Security.Cryptography.X509Certificates.X509Store]::new(
            [Security.Cryptography.X509Certificates.StoreName]::My,
            [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        $personal.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        try { $personal.Add($cert) }
        finally { $personal.Close() }
        $thumbprint = $cert.Thumbprint.ToUpperInvariant()
        $public = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $caCertificate.Export(
                [Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        $rootStore = [Security.Cryptography.X509Certificates.X509Store]::new(
            [Security.Cryptography.X509Certificates.StoreName]::Root,
            [Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
        $rootStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $rootStore.Add($public)
    }
    finally {
        if ($pfxBytes) {
            [Security.Cryptography.CryptographicOperations]::ZeroMemory($pfxBytes)
        }
        if ($signedLeaf) { $signedLeaf.Dispose() }
        if ($issuedCertificate) { $issuedCertificate.Dispose() }
        if ($caCertificate) { $caCertificate.Dispose() }
        if ($leafKey) { $leafKey.Dispose() }
        if ($rootKey) { $rootKey.Dispose() }
        $password = ''
    }

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
