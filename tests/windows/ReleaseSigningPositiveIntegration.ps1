#Requires -Version 7.0
<#
Ephemeral CI ONLY: positive Authenticode release-verifier integration.
Builds a short-lived test-only signer (temporary PEM/PFX under RUNNER_TEMP),
signs copies of all 3 architecture payloads, validates, and
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
$publisherStore = $null
$publisherPublic = $null
$testPfx = ''
$testPfxPassword = ''
$thumbprint = ''
$tag = 'v0.30.0-rc.2'
$rids = @('win-x64', 'win-x86', 'win-arm64')
$original = @{}
$rootTrustAdded = $false
$publisherTrustAdded = $false

function Invoke-BoundedCertUtil {
    param([Parameter(Mandatory)][string[]]$Arguments)
    # X509Store.Add may invoke an invisible trust confirmation on a
    # noninteractive hosted Windows session. Use a bounded native command
    # instead, forcing noninteractive CURRENT-USER stores only.
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = Join-Path $env:WINDIR 'System32\certutil.exe'
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $false
    $info.RedirectStandardError = $false
    foreach ($arg in $Arguments) { [void]$info.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::Start($info)
    if (-not $process) { throw 'Could not start Windows certutil.' }
    try {
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            throw "CurrentUser certutil operation timed out: $($Arguments -join ' ')"
        }
        if ($process.ExitCode -ne 0) {
            throw "CurrentUser certutil operation failed (exit $($process.ExitCode)): $($Arguments -join ' ')"
        }
    }
    finally { $process.Dispose() }
}

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

    # Windows runner providers have repeatedly hung creating test certs.
    # Generate temporary test-only RSA keys and certificate with the
    # OpenSSL CLI on THIS SAME disposable runner. Nothing is uploaded.
    # SignTool will consume a password-protected PFX that lives only
    # under RUNNER_TEMP and is deleted by the cleanup block.
    $opensslCmd = Get-Command openssl.exe -ErrorAction SilentlyContinue
    $openssl = if ($opensslCmd) { $opensslCmd.Source } else {
        'C:\Program Files\Git\usr\bin\openssl.exe'
    }
    if (-not (Test-Path -LiteralPath $openssl -PathType Leaf)) {
        throw 'Git for Windows OpenSSL is unavailable on the hosted runner.'
    }
    function Invoke-BoundedOpenSsl {
        param([Parameter(Mandatory)][string[]]$Arguments)
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = $openssl
        $info.UseShellExecute = $false
        # OpenSSL prints RSA-key generation progress to stderr. Waiting for
        # process exit BEFORE reading the redirected pipe can deadlock when
        # its bounded OS pipe buffer fills up. Keep console streams inherited.
        $info.RedirectStandardOutput = $false
        $info.RedirectStandardError = $false
        foreach ($arg in $Arguments) {
            [void]$info.ArgumentList.Add($arg)
        }
        $process = [Diagnostics.Process]::Start($info)
        if (-not $process) { throw 'Could not start test certificate OpenSSL.' }
        try {
            if (-not $process.WaitForExit(30000)) {
                $process.Kill($true)
                throw 'Ephemeral OpenSSL certificate operation exceeded 30 seconds.'
            }
            if ($process.ExitCode -ne 0) {
                throw "Ephemeral OpenSSL failed with exit code $($process.ExitCode). See the preceding GitHub Actions console output."
            }
        }
        finally { $process.Dispose() }
    }
    $caKey = Join-Path $root 'ephemeral-ca.key'
    $caPem = Join-Path $root 'ephemeral-ca.pem'
    $caDer = Join-Path $root 'ephemeral-ca.cer'
    $leafKey = Join-Path $root 'ephemeral-signer.key'
    $leafCsr = Join-Path $root 'ephemeral-signer.csr'
    $leafPem = Join-Path $root 'ephemeral-signer.pem'
    $leafDer = Join-Path $root 'ephemeral-signer.cer'
    $leafExtensions = Join-Path $root 'ephemeral-signer.ext'
    $testPfx = Join-Path $root 'ephemeral-signer.pfx'
    $testPfxPassword = [Guid]::NewGuid().ToString('N')
    Set-Content -LiteralPath $leafExtensions -Encoding ascii -Value @(
        'basicConstraints=critical,CA:FALSE',
        'keyUsage=critical,digitalSignature',
        'extendedKeyUsage=codeSigning',
        'subjectKeyIdentifier=hash',
        'authorityKeyIdentifier=keyid,issuer'
    )

    Write-Host 'Generating ephemeral test CA via local OpenSSL...'
    Invoke-BoundedOpenSsl -Arguments @(
        'req','-x509','-newkey','rsa:2048','-nodes',
        '-keyout',$caKey,'-out',$caPem,'-days','2',
        '-sha256','-subj','/CN=BitKeyBridge Ephemeral CI Root',
        '-addext','basicConstraints=critical,CA:TRUE',
        '-addext','keyUsage=critical,keyCertSign,cRLSign'
    )
    Write-Host 'Generating ephemeral test code-signing leaf via local OpenSSL...'
    Invoke-BoundedOpenSsl -Arguments @(
        'req','-new','-newkey','rsa:2048','-nodes',
        '-keyout',$leafKey,'-out',$leafCsr,
        '-sha256','-subj','/CN=BitKeyBridge Ephemeral CI Code Signing'
    )
    Write-Host 'Ephemeral leaf request finished; issuing test signer from local test CA...'
    Invoke-BoundedOpenSsl -Arguments @(
        'x509','-req','-in',$leafCsr,'-CA',$caPem,'-CAkey',$caKey,
        '-CAcreateserial','-out',$leafPem,'-days','2',
        '-sha256','-extfile',$leafExtensions
    )
    Write-Host 'Ephemeral test certificate issued; bundling signer for SignTool...'
    Invoke-BoundedOpenSsl -Arguments @(
        'pkcs12','-export','-inkey',$leafKey,'-in',$leafPem,
        '-certfile',$caPem,'-out',$testPfx,
        '-passout',('pass:' + $testPfxPassword)
    )
    Invoke-BoundedOpenSsl -Arguments @(
        'x509','-in',$caPem,'-outform','DER','-out',$caDer
    )
    Invoke-BoundedOpenSsl -Arguments @(
        'x509','-in',$leafPem,'-outform','DER','-out',$leafDer
    )

    Write-Host 'Ephemeral OpenSSL signing certificate created; importing PUBLIC trust for current user...'
    $public = [Security.Cryptography.X509Certificates.X509Certificate2]::new($caDer)
    $publisherPublic = [Security.Cryptography.X509Certificates.X509Certificate2]::new($leafDer)
    $thumbprint = $publisherPublic.Thumbprint.ToUpperInvariant()
    Write-Host 'Importing test CA into CurrentUser Root with bounded certutil...'
    Invoke-BoundedCertUtil -Arguments @('-user', '-f', '-addstore', 'Root', $caDer)
    $rootTrustAdded = $true
    Write-Host 'Importing code-signing leaf into CurrentUser TrustedPublisher...'
    Invoke-BoundedCertUtil -Arguments @('-user', '-f', '-addstore', 'TrustedPublisher', $leafDer)
    $publisherTrustAdded = $true
    Write-Host 'Ephemeral CA and publisher trust imported only for this disposable runner.'

    foreach ($rid in $rids) {
        $folder = Join-Path $root ("stage-" + $rid)
        $exe = Join-Path $folder 'BitKeyBridge.exe'
        Write-Host "Signing $rid using temporary Windows certificate..."
        $signOut = Join-Path $root ("sign-" + $rid + ".stdout")
        $signErr = Join-Path $root ("sign-" + $rid + ".stderr")
        $signArgs = @('sign', '/f', $testPfx, '/p', $testPfxPassword, '/fd', 'SHA256', $exe)
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
    # Remove ephemeral trust in reverse order; no signing key leaves CI.
    if ($publisherTrustAdded -and $publisherPublic) {
        try {
            Invoke-BoundedCertUtil -Arguments @('-user', '-delstore', 'TrustedPublisher', $publisherPublic.Thumbprint)
        }
        catch { Write-Warning ('Could not remove ephemeral publisher trust: ' + $_.Exception.Message) }
    }
    if ($rootTrustAdded -and $public) {
        try {
            Invoke-BoundedCertUtil -Arguments @('-user', '-delstore', 'Root', $public.Thumbprint)
        }
        catch { Write-Warning ('Could not remove ephemeral root trust: ' + $_.Exception.Message) }
    }
    if ($publisherPublic) { $publisherPublic.Dispose() }
    $testPfxPassword = ''
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
