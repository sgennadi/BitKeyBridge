#Requires -Version 7.0
<#
.SYNOPSIS
Exercise the real elevated update and rollback path with published legacy
v0.30.1 and v0.30.3 binaries, pinned ZIP digests and synthetic DPAPI/config.
Only runs on a disposable GitHub-hosted Windows CI runner.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$CandidateZip,
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$ExpectedCandidateVersion,
    [switch]$EphemeralHostedRunner
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# By default, compare the candidate binary against the EXACT version in
# the checked-out source, not the historic v0.30.5 release. Explicit
# -ExpectedCandidateVersion remains available for pinned RC regression jobs.
# Never infer the expected version from the candidate EXE being tested.
if (-not $PSBoundParameters.ContainsKey('ExpectedCandidateVersion')) {
    $projectPath = Join-Path $PSScriptRoot '../../src/BitKeyBridge/BitKeyBridge.csproj'
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw 'Expected candidate version cannot be resolved: project file is missing.'
    }
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $versionsFromSource = @($project.SelectNodes('/Project/PropertyGroup/Version'))
    if ($versionsFromSource.Count -ne 1) {
        throw 'Expected exactly one Version element in the candidate project.'
    }
    $ExpectedCandidateVersion = [string]$versionsFromSource[0].InnerText.Trim()
}
if ($ExpectedCandidateVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?

if (-not $EphemeralHostedRunner -or
    $env:GITHUB_ACTIONS -ne 'true' -or
    $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or
    -not [Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Legacy upgrade tests are allowed only as administrator on disposable GitHub-hosted Windows.'
}

$root = Join-Path $env:RUNNER_TEMP ('bkb-legacy-upgrade-' + [guid]::NewGuid().ToString('N'))
$machineRoot = Join-Path $env:ProgramData 'BitKeyBridge'
$profileRoot = Join-Path $env:LOCALAPPDATA 'BitKeyBridge'
$machineConfig = Join-Path $machineRoot 'appsettings.json'
$profileConfig = Join-Path $profileRoot 'cloud_auth_config.json'
$fixtureSecret = Join-Path $machineRoot 'Secrets\upgrade-fixture.dpapi'
$createdMachineConfig = $false
$createdProfileConfig = $false
$createdSecret = $false

function Assert-Equal([string]$Actual, [string]$Expected, [string]$Description) {
    if (-not [string]::Equals($Actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        # Diagnostics contain only SHA-256 of synthetic data or signed-off binaries.
        Write-Host ("CHECK FAILED: {0} Actual={1} Expected={2}" -f $Description, $Actual, $Expected)
        throw "$Description mismatch."
    }
}

function Hash-Of([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Invoke-Apply([string]$Tool, [string]$Stage, [string]$Version,
                      [string]$PreviousVersion, [string]$Target,
                      [bool]$Rollback, [string]$Working) {
    $planPath = Join-Path $Working 'apply.json'
    $plan = @{
        StagedExecutable = $Stage
        StagedExecutableSha256 = Hash-Of $Stage
        ExpectedVersion = $Version
        PreviousVersion = $PreviousVersion
        Automatic = $false
        ScheduledBackground = $false
        Rollback = $Rollback
        RollbackSource = $(if ($Rollback) { 'Local' } else { '' })
        TargetExecutables = @($Target)
        WaitForProcessId = 0
        RestartService = $false
        RestartGui = $false
        CleanupDirectory = ''
    }
    $plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding utf8
    $planHash = Hash-Of $planPath
    # BitKeyBridge is a WinExe: PowerShell may return immediately from a
    # GUI-subsystem native process. Wait explicitly for the REAL process
    # before checking changed files, backup data or the exit code.
    $start = [Diagnostics.ProcessStartInfo]::new($Tool)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    [void]$start.ArgumentList.Add('--apply-update-plan')
    [void]$start.ArgumentList.Add($planPath)
    [void]$start.ArgumentList.Add('--apply-update-plan-sha256')
    [void]$start.ArgumentList.Add($planHash)
    [void]$start.ArgumentList.Add('--no-elevation')
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) {
        throw 'Failed to start real BitKeyBridge apply helper.'
    }
    try {
        if (-not $process.WaitForExit(240000)) {
            try { $process.Kill($true) }
            catch [InvalidOperationException] {
                Write-Warning 'Updater exited during test timeout cleanup.'
            }
            throw 'BitKeyBridge apply helper exceeded four-minute test timeout.'
        }
        if ($process.ExitCode -ne 0) {
            # On an ephemeral CI machine, show only the updater's already
            # sanitized failure reason and synthetic executable hash state.
            # Do not dump appsettings, cloud config, DPAPI blobs or raw logs.
            # Never retry a rejected update here: a trust/policy rejection
            # must remain a hard regression failure.
            $historyPath = Join-Path $env:ProgramData 'BitKeyBridge\Updates\update-history.json'
            try {
                if (Test-Path -LiteralPath $historyPath) {
                    $rows = @(Get-Content -LiteralPath $historyPath -Raw | ConvertFrom-Json)
                    $action = if ($Rollback) { 'Rollback' } else { 'Install' }
                    $failed = $rows |
                        Where-Object { $_.Result -eq 'Failed' -and $_.Action -eq $action } |
                        Sort-Object TimestampUtc -Descending |
                        Select-Object -First 1
                    if ($null -ne $failed) {
                        # The application sanitized Details before writing
                        # it. Further redact absolute paths and cap output.
                        $reason = [string]$failed.Details
                        $reason = $reason -replace '(?i)[A-Z]:\\[^;\s]+', '[PATH]'
                        $reason = $reason -replace '(?i)(password|token|secret|credential)\s*[:=]\s*[^;\s]+', '$1=[REDACTED]'
                        if ($reason.Length -gt 350) { $reason = $reason.Substring(0, 350) }
                        Write-Host ("CI-only sanitized apply failure: {0}" -f $reason)
                    }
                    else {
                        Write-Host 'CI-only apply failure: no matching persisted failure entry.'
                    }
                }
            }
            catch {
                Write-Host ("CI-only update failure history unavailable: {0}" -f
                    $_.Exception.GetType().Name)
            }
            $targetPresent = Test-Path -LiteralPath $Target
            $backupPresent = Test-Path -LiteralPath ($Target + '.bak')
            Write-Host ("CI-only file state: TargetPresent={0} BackupPresent={1}" -f
                $targetPresent, $backupPresent)
            throw "Actual BitKeyBridge apply helper failed. Rollback=$Rollback Version=$Version ExitCode=$($process.ExitCode)"
        }
    }
    finally {
        $process.Dispose()
    }
}

$versions = @(
    @{ Tag = 'v0.30.1'; Sha256 = '8c19b1acd5e4dee6a743b2bbb6180f76943c1513306b58b3456a3fc7a464a0f7' },
    @{ Tag = 'v0.30.3'; Sha256 = 'b816ed4583081fb663c679048f808e658c5765f462f0b36dfa0bdcc4569a53b7' }
)
try {
    foreach ($file in @($machineConfig, $profileConfig, $fixtureSecret)) {
        if (Test-Path -LiteralPath $file) {
            throw "CI fixture path already exists; refusing to overwrite machine state: $file"
        }
    }
    New-Item -ItemType Directory -Path $root, $machineRoot, $profileRoot,
        (Split-Path $fixtureSecret -Parent) -Force | Out-Null

    $candidateFolder = Join-Path $root 'candidate'
    Expand-Archive -LiteralPath $CandidateZip -DestinationPath $candidateFolder
    $current = Join-Path $candidateFolder 'BitKeyBridge.exe'
    if (-not (Test-Path -LiteralPath $current)) {
        throw 'Candidate x64 executable is missing.'
    }
    $candidateVersion = ((& $current --version --no-elevation | Out-String).Trim())
    if ($LASTEXITCODE -ne 0 -or $candidateVersion -ne $ExpectedCandidateVersion) {
        throw "Expected candidate $ExpectedCandidateVersion. Received $candidateVersion."
    }
    $currentHash = Hash-Of $current

    # Synthetic, non-functional settings and credential ciphertext only.
    # Preserve the bytes AND the NTFS ACL across the executable update.
    # Since RC5 now enforces prerelease consent at the final privileged apply
    # boundary (including MANUAL upgrades), a disposable CI fixture must
    # explicitly authorize that preview channel. Stable candidates still
    # test the default stable-only policy. Never relax the production gate.
    $allowPreviewCandidate = [bool]($ExpectedCandidateVersion -match '-')
    $testMachineSettings = @{
        SchemaVersion = 5
        UpdateRepository = 'sgennadi/BitKeyBridge'
        RequireTrustedUpdateSignature = $false
        TrustedUpdatePublisher = ''
        AutoInstallUpdatesOnStart = $false
        AllowPrereleaseUpdates = $allowPreviewCandidate
    }
    $testMachineSettings | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath $machineConfig -Encoding utf8
    $createdMachineConfig = $true
    Write-Host ('Legacy migration uses explicit prerelease consent={0} on disposable CI only.' -f
        $allowPreviewCandidate)
    '{"AuthMode":"DeviceCode","TenantId":"synthetic-fixture.invalid"}' |
        Set-Content -LiteralPath $profileConfig -Encoding utf8
    $createdProfileConfig = $true

    $fixtureData = [Security.Cryptography.RandomNumberGenerator]::GetBytes(64)
    $encrypted = [Security.Cryptography.ProtectedData]::Protect(
        $fixtureData, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($fixtureSecret, $encrypted)
    $createdSecret = $true
    [Array]::Clear($fixtureData)

    $expectedHashes = @{}
    $expectedAcls = @{}
    foreach ($file in @($machineConfig, $profileConfig, $fixtureSecret)) {
        $expectedHashes[$file] = Hash-Of $file
        $expectedAcls[$file] = (Get-Acl -LiteralPath $file).Sddl
    }

    function Assert-FixturePreserved {
        foreach ($file in @($machineConfig, $profileConfig, $fixtureSecret)) {
            Assert-Equal (Hash-Of $file) $expectedHashes[$file] 'Protected fixture bytes'
            Assert-Equal (Get-Acl -LiteralPath $file).Sddl $expectedAcls[$file] 'NTFS ACL'
        }
        $cipher = [IO.File]::ReadAllBytes($fixtureSecret)
        $plain = [Security.Cryptography.ProtectedData]::Unprotect(
            $cipher, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        if ($plain.Length -ne 64) { throw 'Synthetic DPAPI data is not decryptable.' }
        [Array]::Clear($plain)
    }

    foreach ($legacy in $versions) {
        $version = $legacy.Tag.TrimStart('v')
        $legacyFolder = Join-Path $root $legacy.Tag
        New-Item -ItemType Directory -Path $legacyFolder | Out-Null
        $archive = Join-Path $legacyFolder 'BitKeyBridge-win-x64.zip'
        & gh release download $legacy.Tag --repo sgennadi/BitKeyBridge --pattern 'BitKeyBridge-win-x64.zip' --dir $legacyFolder
        if ($LASTEXITCODE -ne 0) { throw "Could not retrieve published $($legacy.Tag)." }
        Assert-Equal (Hash-Of $archive) $legacy.Sha256 'Pinned published legacy archive'
        $previous = Join-Path $legacyFolder 'extracted'
        Expand-Archive -LiteralPath $archive -DestinationPath $previous
        $oldExe = Join-Path $previous 'BitKeyBridge.exe'
        $oldVersion = ((& $oldExe --version --no-elevation | Out-String).Trim())
        if ($LASTEXITCODE -ne 0 -or $oldVersion -ne $version) {
            throw "Published $($legacy.Tag) executable has an unexpected version."
        }
        $oldHash = Hash-Of $oldExe

        $install = Join-Path $legacyFolder 'install'
        New-Item -ItemType Directory -Path $install | Out-Null
        $target = Join-Path $install 'BitKeyBridge.exe'
        Copy-Item -LiteralPath $oldExe -Destination $target
        Assert-Equal (Hash-Of $target) $oldHash 'Initial version'

        $applyDir = Join-Path $legacyFolder 'apply'
        New-Item -ItemType Directory -Path $applyDir | Out-Null
        Invoke-Apply $current $current $candidateVersion $version $target $false $applyDir
        Write-Host ("Candidate hash={0}; target hash={1}; target version={2}" -f
            $currentHash, (Hash-Of $target),
            ((& $target --version --no-elevation | Out-String).Trim()))
        Assert-Equal (Hash-Of $target) $currentHash 'Installed newer executable'
        Assert-Equal (Hash-Of ($target + '.bak')) $oldHash 'Rollback backup'
        Assert-FixturePreserved

        # Do not open the .bak as a staged file and overwrite it while the
        # helper is reading it. Stage an INDEPENDENT copy before rollback.
        $rollbackStage = Join-Path $legacyFolder 'rollback-stage.exe'
        Copy-Item -LiteralPath ($target + '.bak') -Destination $rollbackStage
        Invoke-Apply $current $rollbackStage $version $candidateVersion $target $true $applyDir
        Assert-Equal (Hash-Of $target) $oldHash 'Restored original executable'
        Assert-FixturePreserved
        Write-Host "$($legacy.Tag) -> $candidateVersion -> $version PASS: content, ACL, DPAPI, backup and rollback."
    }
}
finally {
    if ($createdSecret) { Remove-Item -LiteralPath $fixtureSecret -Force -ErrorAction SilentlyContinue }
    if ($createdProfileConfig) { Remove-Item -LiteralPath $profileConfig -Force -ErrorAction SilentlyContinue }
    if ($createdMachineConfig) { Remove-Item -LiteralPath $machineConfig -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $root -Force -Recurse -ErrorAction SilentlyContinue
}
) {
    throw "Invalid or missing source-pinned candidate version: $ExpectedCandidateVersion"
}
Write-Host "Migration candidate version pinned to source: $ExpectedCandidateVersion"

if (-not $EphemeralHostedRunner -or
    $env:GITHUB_ACTIONS -ne 'true' -or
    $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or
    -not [Security.Principal.WindowsPrincipal]::new(
        [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Legacy upgrade tests are allowed only as administrator on disposable GitHub-hosted Windows.'
}

$root = Join-Path $env:RUNNER_TEMP ('bkb-legacy-upgrade-' + [guid]::NewGuid().ToString('N'))
$machineRoot = Join-Path $env:ProgramData 'BitKeyBridge'
$profileRoot = Join-Path $env:LOCALAPPDATA 'BitKeyBridge'
$machineConfig = Join-Path $machineRoot 'appsettings.json'
$profileConfig = Join-Path $profileRoot 'cloud_auth_config.json'
$fixtureSecret = Join-Path $machineRoot 'Secrets\upgrade-fixture.dpapi'
$createdMachineConfig = $false
$createdProfileConfig = $false
$createdSecret = $false

function Assert-Equal([string]$Actual, [string]$Expected, [string]$Description) {
    if (-not [string]::Equals($Actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        # Diagnostics contain only SHA-256 of synthetic data or signed-off binaries.
        Write-Host ("CHECK FAILED: {0} Actual={1} Expected={2}" -f $Description, $Actual, $Expected)
        throw "$Description mismatch."
    }
}

function Hash-Of([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Invoke-Apply([string]$Tool, [string]$Stage, [string]$Version,
                      [string]$PreviousVersion, [string]$Target,
                      [bool]$Rollback, [string]$Working) {
    $planPath = Join-Path $Working 'apply.json'
    $plan = @{
        StagedExecutable = $Stage
        StagedExecutableSha256 = Hash-Of $Stage
        ExpectedVersion = $Version
        PreviousVersion = $PreviousVersion
        Automatic = $false
        ScheduledBackground = $false
        Rollback = $Rollback
        RollbackSource = $(if ($Rollback) { 'Local' } else { '' })
        TargetExecutables = @($Target)
        WaitForProcessId = 0
        RestartService = $false
        RestartGui = $false
        CleanupDirectory = ''
    }
    $plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding utf8
    $planHash = Hash-Of $planPath
    # BitKeyBridge is a WinExe: PowerShell may return immediately from a
    # GUI-subsystem native process. Wait explicitly for the REAL process
    # before checking changed files, backup data or the exit code.
    $start = [Diagnostics.ProcessStartInfo]::new($Tool)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    [void]$start.ArgumentList.Add('--apply-update-plan')
    [void]$start.ArgumentList.Add($planPath)
    [void]$start.ArgumentList.Add('--apply-update-plan-sha256')
    [void]$start.ArgumentList.Add($planHash)
    [void]$start.ArgumentList.Add('--no-elevation')
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) {
        throw 'Failed to start real BitKeyBridge apply helper.'
    }
    try {
        if (-not $process.WaitForExit(240000)) {
            try { $process.Kill($true) }
            catch [InvalidOperationException] {
                Write-Warning 'Updater exited during test timeout cleanup.'
            }
            throw 'BitKeyBridge apply helper exceeded four-minute test timeout.'
        }
        if ($process.ExitCode -ne 0) {
            # On an ephemeral CI machine, show only the updater's already
            # sanitized failure reason and synthetic executable hash state.
            # Do not dump appsettings, cloud config, DPAPI blobs or raw logs.
            # Never retry a rejected update here: a trust/policy rejection
            # must remain a hard regression failure.
            $historyPath = Join-Path $env:ProgramData 'BitKeyBridge\Updates\update-history.json'
            try {
                if (Test-Path -LiteralPath $historyPath) {
                    $rows = @(Get-Content -LiteralPath $historyPath -Raw | ConvertFrom-Json)
                    $action = if ($Rollback) { 'Rollback' } else { 'Install' }
                    $failed = $rows |
                        Where-Object { $_.Result -eq 'Failed' -and $_.Action -eq $action } |
                        Sort-Object TimestampUtc -Descending |
                        Select-Object -First 1
                    if ($null -ne $failed) {
                        # The application sanitized Details before writing
                        # it. Further redact absolute paths and cap output.
                        $reason = [string]$failed.Details
                        $reason = $reason -replace '(?i)[A-Z]:\\[^;\s]+', '[PATH]'
                        $reason = $reason -replace '(?i)(password|token|secret|credential)\s*[:=]\s*[^;\s]+', '$1=[REDACTED]'
                        if ($reason.Length -gt 350) { $reason = $reason.Substring(0, 350) }
                        Write-Host ("CI-only sanitized apply failure: {0}" -f $reason)
                    }
                    else {
                        Write-Host 'CI-only apply failure: no matching persisted failure entry.'
                    }
                }
            }
            catch {
                Write-Host ("CI-only update failure history unavailable: {0}" -f
                    $_.Exception.GetType().Name)
            }
            $targetPresent = Test-Path -LiteralPath $Target
            $backupPresent = Test-Path -LiteralPath ($Target + '.bak')
            Write-Host ("CI-only file state: TargetPresent={0} BackupPresent={1}" -f
                $targetPresent, $backupPresent)
            throw "Actual BitKeyBridge apply helper failed. Rollback=$Rollback Version=$Version ExitCode=$($process.ExitCode)"
        }
    }
    finally {
        $process.Dispose()
    }
}

$versions = @(
    @{ Tag = 'v0.30.1'; Sha256 = '8c19b1acd5e4dee6a743b2bbb6180f76943c1513306b58b3456a3fc7a464a0f7' },
    @{ Tag = 'v0.30.3'; Sha256 = 'b816ed4583081fb663c679048f808e658c5765f462f0b36dfa0bdcc4569a53b7' }
)
try {
    foreach ($file in @($machineConfig, $profileConfig, $fixtureSecret)) {
        if (Test-Path -LiteralPath $file) {
            throw "CI fixture path already exists; refusing to overwrite machine state: $file"
        }
    }
    New-Item -ItemType Directory -Path $root, $machineRoot, $profileRoot,
        (Split-Path $fixtureSecret -Parent) -Force | Out-Null

    $candidateFolder = Join-Path $root 'candidate'
    Expand-Archive -LiteralPath $CandidateZip -DestinationPath $candidateFolder
    $current = Join-Path $candidateFolder 'BitKeyBridge.exe'
    if (-not (Test-Path -LiteralPath $current)) {
        throw 'Candidate x64 executable is missing.'
    }
    $candidateVersion = ((& $current --version --no-elevation | Out-String).Trim())
    if ($LASTEXITCODE -ne 0 -or $candidateVersion -ne $ExpectedCandidateVersion) {
        throw "Expected candidate $ExpectedCandidateVersion. Received $candidateVersion."
    }
    $currentHash = Hash-Of $current

    # Synthetic, non-functional settings and credential ciphertext only.
    # Preserve the bytes AND the NTFS ACL across the executable update.
    # Since RC5 now enforces prerelease consent at the final privileged apply
    # boundary (including MANUAL upgrades), a disposable CI fixture must
    # explicitly authorize that preview channel. Stable candidates still
    # test the default stable-only policy. Never relax the production gate.
    $allowPreviewCandidate = [bool]($ExpectedCandidateVersion -match '-')
    $testMachineSettings = @{
        SchemaVersion = 5
        UpdateRepository = 'sgennadi/BitKeyBridge'
        RequireTrustedUpdateSignature = $false
        TrustedUpdatePublisher = ''
        AutoInstallUpdatesOnStart = $false
        AllowPrereleaseUpdates = $allowPreviewCandidate
    }
    $testMachineSettings | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath $machineConfig -Encoding utf8
    $createdMachineConfig = $true
    Write-Host ('Legacy migration uses explicit prerelease consent={0} on disposable CI only.' -f
        $allowPreviewCandidate)
    '{"AuthMode":"DeviceCode","TenantId":"synthetic-fixture.invalid"}' |
        Set-Content -LiteralPath $profileConfig -Encoding utf8
    $createdProfileConfig = $true

    $fixtureData = [Security.Cryptography.RandomNumberGenerator]::GetBytes(64)
    $encrypted = [Security.Cryptography.ProtectedData]::Protect(
        $fixtureData, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($fixtureSecret, $encrypted)
    $createdSecret = $true
    [Array]::Clear($fixtureData)

    $expectedHashes = @{}
    $expectedAcls = @{}
    foreach ($file in @($machineConfig, $profileConfig, $fixtureSecret)) {
        $expectedHashes[$file] = Hash-Of $file
        $expectedAcls[$file] = (Get-Acl -LiteralPath $file).Sddl
    }

    function Assert-FixturePreserved {
        foreach ($file in @($machineConfig, $profileConfig, $fixtureSecret)) {
            Assert-Equal (Hash-Of $file) $expectedHashes[$file] 'Protected fixture bytes'
            Assert-Equal (Get-Acl -LiteralPath $file).Sddl $expectedAcls[$file] 'NTFS ACL'
        }
        $cipher = [IO.File]::ReadAllBytes($fixtureSecret)
        $plain = [Security.Cryptography.ProtectedData]::Unprotect(
            $cipher, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        if ($plain.Length -ne 64) { throw 'Synthetic DPAPI data is not decryptable.' }
        [Array]::Clear($plain)
    }

    foreach ($legacy in $versions) {
        $version = $legacy.Tag.TrimStart('v')
        $legacyFolder = Join-Path $root $legacy.Tag
        New-Item -ItemType Directory -Path $legacyFolder | Out-Null
        $archive = Join-Path $legacyFolder 'BitKeyBridge-win-x64.zip'
        & gh release download $legacy.Tag --repo sgennadi/BitKeyBridge --pattern 'BitKeyBridge-win-x64.zip' --dir $legacyFolder
        if ($LASTEXITCODE -ne 0) { throw "Could not retrieve published $($legacy.Tag)." }
        Assert-Equal (Hash-Of $archive) $legacy.Sha256 'Pinned published legacy archive'
        $previous = Join-Path $legacyFolder 'extracted'
        Expand-Archive -LiteralPath $archive -DestinationPath $previous
        $oldExe = Join-Path $previous 'BitKeyBridge.exe'
        $oldVersion = ((& $oldExe --version --no-elevation | Out-String).Trim())
        if ($LASTEXITCODE -ne 0 -or $oldVersion -ne $version) {
            throw "Published $($legacy.Tag) executable has an unexpected version."
        }
        $oldHash = Hash-Of $oldExe

        $install = Join-Path $legacyFolder 'install'
        New-Item -ItemType Directory -Path $install | Out-Null
        $target = Join-Path $install 'BitKeyBridge.exe'
        Copy-Item -LiteralPath $oldExe -Destination $target
        Assert-Equal (Hash-Of $target) $oldHash 'Initial version'

        $applyDir = Join-Path $legacyFolder 'apply'
        New-Item -ItemType Directory -Path $applyDir | Out-Null
        Invoke-Apply $current $current $candidateVersion $version $target $false $applyDir
        Write-Host ("Candidate hash={0}; target hash={1}; target version={2}" -f
            $currentHash, (Hash-Of $target),
            ((& $target --version --no-elevation | Out-String).Trim()))
        Assert-Equal (Hash-Of $target) $currentHash 'Installed newer executable'
        Assert-Equal (Hash-Of ($target + '.bak')) $oldHash 'Rollback backup'
        Assert-FixturePreserved

        # Do not open the .bak as a staged file and overwrite it while the
        # helper is reading it. Stage an INDEPENDENT copy before rollback.
        $rollbackStage = Join-Path $legacyFolder 'rollback-stage.exe'
        Copy-Item -LiteralPath ($target + '.bak') -Destination $rollbackStage
        Invoke-Apply $current $rollbackStage $version $candidateVersion $target $true $applyDir
        Assert-Equal (Hash-Of $target) $oldHash 'Restored original executable'
        Assert-FixturePreserved
        Write-Host "$($legacy.Tag) -> $candidateVersion -> $version PASS: content, ACL, DPAPI, backup and rollback."
    }
}
finally {
    if ($createdSecret) { Remove-Item -LiteralPath $fixtureSecret -Force -ErrorAction SilentlyContinue }
    if ($createdProfileConfig) { Remove-Item -LiteralPath $profileConfig -Force -ErrorAction SilentlyContinue }
    if ($createdMachineConfig) { Remove-Item -LiteralPath $machineConfig -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $root -Force -Recurse -ErrorAction SilentlyContinue
}
