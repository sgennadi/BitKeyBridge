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

function Invoke-StandardUserStartupDiagnostics {
    param([Parameter(Mandatory)][pscredential]$Credential)
    $nonce = [guid]::NewGuid().ToString('N')
    $outPath = Join-Path $env:RUNNER_TEMP ("startup-standard-" + $nonce + ".json")
    $errPath = Join-Path $env:RUNNER_TEMP ("startup-standard-" + $nonce + ".err")
    try {
        $process = Start-Process -FilePath $standardExecutable -WorkingDirectory (Join-Path $env:WINDIR 'System32') -ArgumentList @('--startup-diagnostics', '--no-elevation') -Credential $Credential -LoadUserProfile -Wait -PassThru -RedirectStandardOutput $outPath -RedirectStandardError $errPath
        if ($process.ExitCode -ne 0) {
            $stderr = if (Test-Path -LiteralPath $errPath) {
                Get-Content -LiteralPath $errPath -Raw
            } else { '' }
            throw "Startup diagnostics returned $($process.ExitCode): $stderr"
        }
        $report = Get-Content -LiteralPath $outPath -Raw | ConvertFrom-Json
        if ($report.Mode -ne 'NonElevatingStartupDiagnostics' -or
            $report.WindowsToken -ne 'StandardOrFiltered' -or
            -not $report.ProcessArchitecture) {
            throw 'Standard Windows token was not validated by startup diagnostics.'
        }
        $config = @($report.Checks | Where-Object Check -eq 'MachineConfiguration')
        if ($config.Count -ne 1 -or $config[0].Status -ne 'Readable') {
            throw 'Protected machine configuration must be readable by standard users, not writable.'
        }
        Write-Host 'Standard-user startup diagnostics passed without elevation or secret reads.'
    }
    finally {
        Remove-Item -LiteralPath $outPath, $errPath -Force -ErrorAction SilentlyContinue
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

function Invoke-StandardUserPreflight {
    param(
        [Parameter(Mandatory)][pscredential]$Credential,
        [Parameter(Mandatory)][int]$ExpectedExitCode,
        [Parameter(Mandatory)][string]$ExpectedReaderStatus,
        [switch]$WithProbe
    )
    $nonce = [guid]::NewGuid().ToString('N')
    $stdoutPath = Join-Path $env:RUNNER_TEMP ("broker-preflight-" + $nonce + ".json")
    $stderrPath = Join-Path $env:RUNNER_TEMP ("broker-preflight-" + $nonce + ".err")
    try {
        $args = @('--audit-broker-preflight', '--no-elevation')
        if ($WithProbe) { $args += '--probe' }
        $process = Start-Process -FilePath $standardExecutable -ArgumentList $args -WorkingDirectory (Join-Path $env:WINDIR 'System32') -Credential $Credential -LoadUserProfile -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        if ($process.ExitCode -ne $ExpectedExitCode) {
            $stderr = if (Test-Path $stderrPath) {
                Get-Content -LiteralPath $stderrPath -Raw
            } else { '' }
            throw "Readiness CLI returned $($process.ExitCode) expected $ExpectedExitCode. $stderr"
        }
        $report = Get-Content -LiteralPath $stdoutPath -Raw | ConvertFrom-Json
        if ($report.IsLocalAdministrator) {
            throw 'Standard-user broker preflight unexpectedly reports local admin.'
        }
        if ($report.ProbeRequested -ne [bool]$WithProbe) {
            throw 'Readiness CLI ignored the explicit --probe switch.'
        }
        $role = @($report.Checks | Where-Object Check -eq 'CallerEnabledRecoveryReader')
        if ($role.Count -ne 1 -or $role[0].Status -ne $ExpectedReaderStatus) {
            throw 'Readiness CLI reported an incorrect enabled-token reader role.'
        }
        $audit = @($report.Checks | Where-Object Check -eq 'AuthenticatedDurableAuditProbe')
        $expectedProbeStatus = if (-not $WithProbe) {
            'NotTested'
        } elseif ($ExpectedExitCode -eq 0) {
            'Pass'
        } else {
            'Fail'
        }
        if ($audit.Count -ne 1 -or $audit[0].Status -ne $expectedProbeStatus) {
            throw 'Readiness CLI reported an unexpected durable ACK status.'
        }
        Write-Host "Readiness CLI passed (role=$ExpectedReaderStatus, probe=$expectedProbeStatus)."
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-UnsafeAclFailsClosed {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Journal,
        [Parameter(Mandatory)][pscredential]$ReaderCredential
    )
    $sections = [System.Security.AccessControl.AccessControlSections]::Owner -bor
        [System.Security.AccessControl.AccessControlSections]::Access
    $originalSddl = (Get-Acl -LiteralPath $Path).GetSecurityDescriptorSddlForm(
        $sections)
    $beforeJournalHash = (Get-FileHash -LiteralPath $Journal -Algorithm SHA256).Hash

    try {
        # TEMPORARY on a disposable GitHub-hosted runner ONLY. Simulate a
        # user-writable ancestor or file and demand strict rejection.
        $unsafeAcl = Get-Acl -LiteralPath $Path
        $usersSid = [System.Security.Principal.SecurityIdentifier]::new(
            'S-1-5-32-545')
        $writeRule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $usersSid,
            [System.Security.AccessControl.FileSystemRights]::WriteData,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $unsafeAcl.AddAccessRule($writeRule)
        Set-Acl -LiteralPath $Path -AclObject $unsafeAcl

        $rules = (Get-Acl -LiteralPath $Path).GetAccessRules(
            $true, $true, [System.Security.Principal.SecurityIdentifier])
        $unsafeRuleExists = @($rules | Where-Object {
            $_.IdentityReference.Value -eq $usersSid.Value -and
            $_.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow -and
            ($_.FileSystemRights -band
                [System.Security.AccessControl.FileSystemRights]::WriteData) -ne 0
        }).Count -gt 0
        if (-not $unsafeRuleExists) {
            throw "Unsafe ACL simulation failed to apply: $Path"
        }

        Invoke-StandardUserProbe -Credential $ReaderCredential -ExpectedExitCode 3

        # Administrative diagnostics must reject the same unsafe ACL.
        $verification = Start-Process -FilePath $Executable -ArgumentList @('--audit-broker-verify', '--no-elevation') -Wait -PassThru
        if ($verification.ExitCode -eq 0) {
            throw "Admin verifier accepted unsafe audit ACL: $Path"
        }
        if ((Get-FileHash -LiteralPath $Journal -Algorithm SHA256).Hash -ne
            $beforeJournalHash) {
            throw "Protected journal changed during ACL rejection: $Path"
        }
    }
    finally {
        # Always restore the *exact* pre-test owner and DACL. Never try this
        # on production machines, domain controllers or persistent runners.
        $restoredAcl = Get-Acl -LiteralPath $Path
        $restoredAcl.SetSecurityDescriptorSddlForm($originalSddl, $sections)
        Set-Acl -LiteralPath $Path -AclObject $restoredAcl
    }

    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')
    Invoke-StandardUserProbe -Credential $ReaderCredential -ExpectedExitCode 0
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

    # Exercise process creation and machine policy READ access with two
    # separate true non-admin Windows tokens, not the privileged CI runner.
    Invoke-StandardUserStartupDiagnostics -Credential $reader
    Invoke-StandardUserStartupDiagnostics -Credential $denied

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
    $deniedSid = (Get-LocalUser -Name $deniedName).SID.Value
    $journalEntries = @(
        Get-Content -LiteralPath $journal |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_ | ConvertFrom-Json }
    )
    if (@($journalEntries | Where-Object {
        $_.Action -eq 'AuditBrokerHealthProbe' -and
        $_.Details -match [regex]::Escape("SID=$readerSid")
    }).Count -eq 0) {
        throw 'Protected journal does not contain the authenticated reader SID.'
    }
    if (@($journalEntries | Where-Object {
        $_.Details -match [regex]::Escape("SID=$deniedSid")
    }).Count -ne 0) {
        throw 'Unauthorized SID appeared in the protected journal.'
    }
    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')

    # Read-only diagnostics must not touch the protected journal.
    $beforePreflight = (Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash
    Invoke-StandardUserPreflight -Credential $reader -ExpectedExitCode 0 -ExpectedReaderStatus Pass
    Invoke-StandardUserPreflight -Credential $denied -ExpectedExitCode 3 -ExpectedReaderStatus Fail
    if ((Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash -ne $beforePreflight) {
        throw 'Read-only broker preflight unexpectedly modified the journal.'
    }

    # Only the explicit --probe switch may write a harmless health event.
    Invoke-StandardUserPreflight -Credential $reader -ExpectedExitCode 0 -ExpectedReaderStatus Pass -WithProbe
    if ((Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash -eq $beforePreflight) {
        throw 'The explicit broker preflight probe did not commit a health event.'
    }
    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')

    # An administrator-induced corruption must be refused immediately. The
    # SYSTEM service must never append to a damaged journal or ACK access.
    $originalJournalBytes = [IO.File]::ReadAllBytes($journal)
    try {
        [IO.File]::AppendAllText($journal,
            '{"forged":true}' + [Environment]::NewLine)
        $corruptHash = (Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash
        Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 3
        if ((Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash -ne
            $corruptHash) {
            throw 'The corrupted journal was modified during a denied request.'
        }
    }
    finally {
        [IO.File]::WriteAllBytes($journal, $originalJournalBytes)
    }
    Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')
    Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 0

    # The ACL chain matters all the way back to the machine policy root.
    # Run these invasive simulations on this disposable runner only.
    $machineRoot = Join-Path $env:ProgramData 'BitKeyBridge'
    $protectedDirectory = Join-Path $machineRoot 'ProtectedAudit'
    $machineConfig = Join-Path $machineRoot 'appsettings.json'
    Assert-UnsafeAclFailsClosed -Path $machineRoot -Journal $journal -ReaderCredential $reader
    Assert-UnsafeAclFailsClosed -Path $protectedDirectory -Journal $journal -ReaderCredential $reader
    Assert-UnsafeAclFailsClosed -Path $machineConfig -Journal $journal -ReaderCredential $reader
    Assert-UnsafeAclFailsClosed -Path $journal -Journal $journal -ReaderCredential $reader

    # The interprocess lock and optional rotated archive are also protected.
    $journalLock = "$journal.lock"
    if (-not (Test-Path -LiteralPath $journalLock -PathType Leaf)) {
        throw 'Protected journal lock file is missing.'
    }
    Assert-UnsafeAclFailsClosed -Path $journalLock -Journal $journal -ReaderCredential $reader

    $rotatedJournal = "$journal.old"
    if (Test-Path -LiteralPath $rotatedJournal) {
        throw 'Unexpected journal archive on a disposable CI runner.'
    }
    try {
        [IO.File]::WriteAllText($rotatedJournal, '')
        Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')
        Assert-UnsafeAclFailsClosed -Path $rotatedJournal -Journal $journal -ReaderCredential $reader
    }
    finally {
        Remove-Item -LiteralPath $rotatedJournal -Force -ErrorAction SilentlyContinue
    }

    # A trusted administrator may edit machine policy without changing ACL.
    # The running service must refuse *all* requests until a restart rather
    # than continuing to use its cached RBAC reader list.
    $configFile = Join-Path $env:ProgramData 'BitKeyBridge\appsettings.json'
    $savedPolicy = [IO.File]::ReadAllBytes($configFile)
    $savedJournalHash = (Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash
    try {
        [IO.File]::AppendAllText($configFile, [Environment]::NewLine)
        Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 3
        if ((Get-FileHash -LiteralPath $journal -Algorithm SHA256).Hash -ne
            $savedJournalHash) {
            throw 'Policy drift was accepted and appended to protected audit.'
        }
    }
    finally {
        [IO.File]::WriteAllBytes($configFile, $savedPolicy)
    }
    Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 0

    # Simulate an overflowing local SIEM outbox while strict protected audit
    # remains on. The SYSTEM service must not ACK a recovery operation if
    # the event cannot be enqueued, even though the hash-chained journal is
    # durably appended. Runs ONLY on a disposable hosted Windows runner.
    $savedBrokerConfig = [IO.File]::ReadAllBytes($configFile)
    $outbox = Join-Path $env:ProgramData 'BitKeyBridge\SIEM\Outbox'
    try {
        Invoke-ElevatedTestCommand -Arguments @('--stop-service', '--no-elevation')
        $siemConfig = Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
        $siemConfig.SiemEnabled = $true
        $siemConfig.SiemFailClosed = $true
        $siemConfig.SiemMode = 'FileJsonl'
        $siemConfig.SiemMaxOutboxEvents = 100
        $siemConfig | ConvertTo-Json -Depth 40 |
            Set-Content -LiteralPath $configFile -Encoding utf8
        New-Item -ItemType Directory -Path $outbox -Force | Out-Null
        if (@(Get-ChildItem -LiteralPath $outbox -File -Filter '*.json').Count -ne 0) {
            throw 'Expected an empty SIEM outbox on the disposable runner.'
        }
        for ($i = 0; $i -lt 100; $i++) {
            [IO.File]::WriteAllText(
                (Join-Path $outbox ('ci-block-' + $i.ToString('D3') + '.json')),
                '{}')
        }
        Invoke-ElevatedTestCommand -Arguments @('--start-service', '--no-elevation')
        Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 3

        # The pre-read journal can retain a denied audit request; it must
        # remain hash-valid and must NEVER imply that a key was revealed.
        Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')
        Get-ChildItem -LiteralPath $outbox -Filter 'ci-block-*.json' -File |
            Remove-Item -Force
        Invoke-StandardUserProbe -Credential $reader -ExpectedExitCode 0
        Invoke-ElevatedTestCommand -Arguments @('--audit-broker-verify', '--no-elevation')
    }
    finally {
        try { Invoke-ElevatedTestCommand -Arguments @('--stop-service', '--no-elevation') }
        catch { Write-Warning ('SIEM test service stop: ' + $_.Exception.Message) }
        [IO.File]::WriteAllBytes($configFile, $savedBrokerConfig)
        if (Test-Path -LiteralPath $outbox -PathType Container) {
            Get-ChildItem -LiteralPath $outbox -Filter 'ci-block-*.json' -File |
                Remove-Item -Force -ErrorAction SilentlyContinue
        }
        Invoke-ElevatedTestCommand -Arguments @('--start-service', '--no-elevation')
    }
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
