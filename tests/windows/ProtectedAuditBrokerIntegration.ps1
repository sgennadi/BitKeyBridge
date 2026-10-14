#Requires -Version 7.0
<#
Windows-hosted GitHub runner only. Creates temporary LOCAL standard users,
installs the test-only BitKeyBridge service as SYSTEM, checks a real Named Pipe
ACK and fails closed for nonreaders, then removes the service and users.
NEVER run on a production workstation or self-hosted runner.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,
    [switch]$EphemeralHostedRunner
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $EphemeralHostedRunner -or
    $env:GITHUB_ACTIONS -ne 'true' -or
    $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or
    -not $env:RUNNER_TEMP) {
    throw 'Integration test requires an explicit, ephemeral GitHub-hosted runner opt-in.'
}
if (-not $IsWindows) { throw 'Windows is required.' }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Executable not found: $Executable"
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
# Standard users should run the installed, machine-readable binary, not an
# Actions runner temporary file with unrelated runner-account-only ACLs.
$standardExecutable = Join-Path $env:ProgramFiles 'BitKeyBridge\BitKeyBridge.exe'
if (Get-Service -Name BitKeyBridge -ErrorAction SilentlyContinue) {
    throw 'Refusing test: BitKeyBridge Windows Service already exists.'
}
if (Test-Path (Join-Path $env:ProgramData 'BitKeyBridge')) {
    throw 'Refusing test: machine BitKeyBridge configuration already exists.'
}

$testSuffix = [guid]::NewGuid().ToString('N').Substring(0, 7)
$readerName = 'bkbreader' + $testSuffix
$deniedName = 'bkbdenied' + $testSuffix
$accounts = @()
$installed = $false

function Invoke-ElevatedTestCommand {
    param([Parameter(Mandatory)][string[]]$Arguments)
    # WinExe is a graphical subsystem executable, so PowerShell's native
    # $LASTEXITCODE is not reliable. Always wait and examine Process.ExitCode.
    $nonce = [guid]::NewGuid().ToString('N')
    $stdoutPath = Join-Path $env:RUNNER_TEMP ("broker-admin-" + $nonce + ".out")
    $stderrPath = Join-Path $env:RUNNER_TEMP ("broker-admin-" + $nonce + ".err")
    try {
        $process = Start-Process -FilePath $Executable -ArgumentList $Arguments `
            -Wait -PassThru -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath
        $stdout = if (Test-Path $stdoutPath) { Get-Content $stdoutPath -Raw } else { '' }
        $stderr = if (Test-Path $stderrPath) { Get-Content $stderrPath -Raw } else { '' }
        if ($stdout) { Write-Host $stdout }
        if ($process.ExitCode -ne 0) {
            throw "BitKeyBridge failed with exit $($process.ExitCode) for: " +
                "$($Arguments -join ' '); $stderr"
        }
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-StandardUserProbe {
    param(
        [Parameter(Mandatory)][pscredential]$Credential,
        [Parameter(Mandatory)][int]$ExpectedExitCode
    )
    $nonce = [guid]::NewGuid().ToString('N')
    $outPath = Join-Path $env:RUNNER_TEMP ("audit-broker-" + $nonce + ".out")
    $errPath = Join-Path $env:RUNNER_TEMP ("audit-broker-" + $nonce + ".err")
    try {
        $process = Start-Process -FilePath $standardExecutable `
            -WorkingDirectory (Join-Path $env:WINDIR 'System32') `
            -ArgumentList @('--audit-broker-status', '--no-elevation') `
            -Credential $Credential -LoadUserProfile -Wait -PassThru `
            -RedirectStandardOutput $outPath -RedirectStandardError $errPath
        $stdout = if (Test-Path $outPath) { Get-Content $outPath -Raw } else { '' }
        $stderr = if (Test-Path $errPath) { Get-Content $errPath -Raw } else { '' }
        Write-Host "Probe exit: $($process.ExitCode) / expected: $ExpectedExitCode"
        Write-Host $stdout
        if ($process.ExitCode -ne $ExpectedExitCode) {
            throw "Wrong probe exit code. Standard error: $stderr"
        }
        if ($ExpectedExitCode -eq 0 -and $stdout -notmatch 'BrokerProbe=Committed') {
            throw 'Exit 0 without a durable authenticated broker ACK.'
        }
        if ($ExpectedExitCode -ne 0 -and $stdout -match 'BrokerProbe=Committed') {
            throw 'Denied identity received a misleading committed ACK.'
        }
    }
    finally {
        Remove-Item -LiteralPath $outPath, $errPath -Force -ErrorAction SilentlyContinue
    }
}

try {
    $seclogon = Get-Service -Name seclogon -ErrorAction SilentlyContinue
    if ($seclogon -and $seclogon.Status -ne 'Running') {
        Start-Service -Name seclogon
    }

    foreach ($name in @($readerName, $deniedName)) {
        # Never print or persist the randomly generated local test password.
        $rawPassword = ([guid]::NewGuid().ToString('N') +
            'Ab1!' + [guid]::NewGuid().ToString('N'))
        $securePassword = ConvertTo-SecureString $rawPassword -AsPlainText -Force
        $credential = [pscredential]::new(
            "$env:COMPUTERNAME\$name", $securePassword)
        New-LocalUser -Name $name -Password $securePassword `
            -AccountNeverExpires -PasswordNeverExpires | Out-Null
        $accounts += [pscustomobject]@{
            Name = $name
            Credential = $credential
        }
        $rawPassword = $null
    }

    $readerSid = (Get-LocalUser -Name $readerName).SID.Value

    $installed = $true  # Ensure cleanup even when service setup partially fails.
    Invoke-ElevatedTestCommand -Arguments @('--install-service', '--no-elevation')
    if (-not (Test-Path -LiteralPath $standardExecutable -PathType Leaf)) {
        throw 'The installed service executable is not readable from Program Files.'
    }
    Invoke-ElevatedTestCommand -Arguments @(
        '--rbac-enable', '--rbac-admin-bypass', 'off',
        '--rbac-reader-add', $readerSid, '--no-elevation'
    )
    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-provision', '--no-elevation')
    Invoke-ElevatedTestCommand -Arguments @('--stop-service', '--no-elevation')
    Invoke-ElevatedTestCommand -Arguments @('--start-service', '--no-elevation')

    $reader = ($accounts | Where-Object Name -eq $readerName).Credential
    $denied = ($accounts | Where-Object Name -eq $deniedName).Credential
    $accepted = $false
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        try {
            Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 0
            $accepted = $true
            break
        }
        catch {
            Write-Host "Reader connection warmup $($attempt + 1) / 12: $($_.Exception.Message)"
            Start-Sleep -Seconds 2
        }
    }
    if (-not $accepted) {
        throw 'No authenticated protected journal ACK for standard RecoveryReader.'
    }

    # A user outside the configured reader allowlist must be denied without UAC.
    Invoke-StandardUserProbe -Credential $denied -ExpectedExitCode 3

    $journal = Join-Path $env:ProgramData 'BitKeyBridge\ProtectedAudit\audit.jsonl'
    if (-not (Test-Path -LiteralPath $journal)) {
        throw 'SYSTEM-owned protected journal is missing.'
    }
    $audit = Get-Content -LiteralPath $journal -Raw
    if ($audit -notmatch 'AuditBrokerHealthProbe') {
        throw 'Protected journal is missing the synthetic health event.'
    }
    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')

    # An administrator-induced corruption must be refused immediately. The
    # SYSTEM service must never append to a damaged journal or ACK access.
    $originalJournalBytes = [IO.File]::ReadAllBytes($journal)
    try {
        [IO.File]::AppendAllText($journal,
            '{"forged":true}' + [Environment]::NewLine)
        Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 3
    }
    finally {
        [IO.File]::WriteAllBytes($journal, $originalJournalBytes)
    }
    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')
    Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 0

    # Verify stopped service cannot satisfy strict audit. No fallback is allowed.
    Invoke-ElevatedTestCommand -Arguments @('--stop-service', '--no-elevation')
    Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 3

    Write-Host 'Protected audit standard-user integration checks PASSED.'
}
finally {
    if ($installed) {
        try {
            $cleanup = Start-Process -FilePath $Executable `
                -ArgumentList @('--uninstall-service', '--no-elevation') `
                -Wait -PassThru
            Write-Host "Service cleanup exit code: $($cleanup.ExitCode)"
        }
        catch {
            Write-Warning "Service cleanup: $($_.Exception.Message)"
        }
    }
    foreach ($account in $accounts) {
        try { Remove-LocalUser -Name $account.Name -ErrorAction Stop }
        catch { Write-Warning "Local test user cleanup ($($account.Name)): $($_.Exception.Message)" }
    }
}
