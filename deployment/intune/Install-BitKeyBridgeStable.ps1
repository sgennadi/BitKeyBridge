#Requires -Version 5.1
<#
.SYNOPSIS
Deploy the exactly pinned unsigned BitKeyBridge v0.32.0 Stable package.
.DESCRIPTION
Native Windows PowerShell, no third-party utilities or cloud credentials.
Validate is non-elevating; Install/Rollback require an elevated admin/SYSTEM.
Never changes Group Policy, update consent, scheduled tasks, audit data,
AD permissions, credentials or CyberArk/WDAC/AppLocker security settings.
Package files must come from the published GitHub v0.32.0 release.
#>
[CmdletBinding()]
param(
    [ValidateSet('Validate','Install','Rollback')]
    [string]$Mode = 'Validate',
    [ValidateSet('Auto','win-x64','win-x86','win-arm64')]
    [string]$Architecture = 'Auto',
    [string]$PackagePath = '',
    [switch]$ApproveUnsigned,
    [ValidateSet('Canary','Broad')]
    [string]$Ring = 'Canary',
    [switch]$AuthorizeBroadRollout
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$releaseVersion = '0.32.0'
$releaseTag = 'v0.32.0'
$releaseSourceSha = '09cb98c7983de3a8339bf4802918bbdb378a1be8'
$pins = @{
    'win-x64'   = '1cecd4940e50b26f9c09823a9372a04f9aa3c01780702b66d6f615aa81da7a55'
    'win-x86'   = '97b0d8f3737ae22c99fb41567783ff390a17f3fdddd67da1918130e4e4d5cbcd'
    'win-arm64' = '8a1db7457d4765be1622de028aaed78916f4bd4f4fbb35334a4183183e1585ed'
}
$machines = @{ 'win-x64' = 0x8664; 'win-x86' = 0x014c; 'win-arm64' = 0xaa64 }
$script:staged = $null
$script:gate = $null
$script:logPath = $null

function Write-DeploymentMessage([string]$Message) {
    $line = ('{0} {1} {2}' -f ([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')), $Mode, $Message)
    Write-Output $line
    if ($null -ne $script:logPath) {
        Add-Content -LiteralPath $script:logPath -Value $line -Encoding UTF8
    }
}
function Assert-NoReparsePoint([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        $attributes = [IO.File]::GetAttributes($Path)
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'A managed deployment path is a reparse point; refusing to write.'
        }
    }
}
function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Managed installation and rollback require an elevated administrator or SYSTEM.'
    }
}
function Assert-UnsignedPolicy {
    $policyPath = 'HKLM:\SOFTWARE\Policies\BitKeyBridge'
    if (Test-Path -LiteralPath $policyPath) {
        $policy = Get-ItemProperty -LiteralPath $policyPath -ErrorAction Stop
        foreach ($flag in @('DisableAutomaticUpdates','DisableUnsignedAutomaticUpdates','RequireSignedAutomaticUpdates')) {
            if ($null -ne $policy.PSObject.Properties[$flag] -and [int]$policy.$flag -ne 0) {
                throw ('The configured machine security restriction {0} denies managed unsigned deployment.' -f $flag)
            }
        }
    }
    $config = Join-Path $env:ProgramData 'BitKeyBridge\appsettings.json'
    Assert-NoReparsePoint $config
    if (Test-Path -LiteralPath $config -PathType Leaf) {
        try {
            $settings = Get-Content -LiteralPath $config -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
            if ($settings.RequireTrustedUpdateSignature -eq $true -or
                -not [string]::IsNullOrWhiteSpace([string]$settings.TrustedUpdatePublisher) -or
                -not [string]::IsNullOrWhiteSpace([string]$settings.TrustedUpdateSignerThumbprint)) {
                throw 'Existing machine settings require a trusted publisher/signature.'
            }
        }
        catch {
            throw 'Machine signing settings deny unsigned deployment or cannot be read safely.'
        }
    }
}
function Get-PeMachine([string]$Path) {
    $file = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($file.Length -lt 256) { throw 'Executable is shorter than a valid PE header.' }
        $reader = New-Object IO.BinaryReader($file)
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw 'Executable has no MZ header.' }
        $file.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 64 -or ($peOffset + 6) -gt $file.Length) {
            throw 'Executable contains an invalid PE offset.'
        }
        $file.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw 'Executable has no PE signature.' }
        return [int]$reader.ReadUInt16()
    }
    finally { $file.Dispose() }
}
function Assert-Executable([string]$Path,[string]$ExpectedArchitecture,[string]$ExpectedVersion) {
    $machine = Get-PeMachine $Path
    if ($machine -ne $machines[$ExpectedArchitecture]) {
        throw ('PE machine does not match the approved {0} package.' -f $ExpectedArchitecture)
    }
    $details = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    if ($details.FileVersion -ne ($ExpectedVersion + '.0')) {
        throw ('FileVersion does not match the approved {0} release.' -f $ExpectedVersion)
    }
    if ($details.ProductVersion -notmatch ('^' + [regex]::Escape($ExpectedVersion) + '(?:\+[^ ]+)?$')) {
        throw ('ProductVersion does not match the approved {0} release.' -f $ExpectedVersion)
    }
}
function Get-DeviceArchitecture {
    $osArch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    switch ($osArch) {
        'X64' { return 'win-x64' }
        'X86' { return 'win-x86' }
        'Arm64' { return 'win-arm64' }
        default { throw ('Unsupported Windows OS architecture: {0}' -f $osArch) }
    }
}
function Assert-ProcessStopped {
    $active = @(Get-Process -Name 'BitKeyBridge','BitKeyBridge-Updater' -ErrorAction SilentlyContinue)
    if ($active.Count -ne 0) {
        throw 'BitKeyBridge or its updater is running. Close the active recovery session before deployment.'
    }
}
function Validate-Package([string]$Zip,[string]$Rid,[string]$StageDirectory) {
    if (-not (Test-Path -LiteralPath $Zip -PathType Leaf)) {
        throw ('The architecture package is missing: {0}' -f $Rid)
    }
    Assert-NoReparsePoint $Zip
    $actual = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash
    if (-not [string]::Equals($actual, $pins[$Rid], [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Official {0} ZIP SHA-256 mismatch; refusing untrusted or modified package.' -f $releaseTag)
    }
    Add-Type -AssemblyName System.IO.Compression
    $zipFile = [IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        if ($zipFile.Entries.Count -ne 1) { throw 'Package must contain exactly one file.' }
        $entry = $zipFile.Entries[0]
        if ($entry.FullName -cne 'BitKeyBridge.exe' -or
            $entry.Name -cne 'BitKeyBridge.exe' -or
            $entry.Length -le 0 -or
            $entry.Length -gt 536870912 -or
            ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000)) {
            throw 'Package entry layout, size, or type is not allowed.'
        }
        $script:staged = Join-Path $StageDirectory ('.BitKeyBridge-stage-' + [guid]::NewGuid().ToString('N') + '.exe')
        $inputStream = $entry.Open()
        try {
            $outputStream = [IO.File]::Open($script:staged, [IO.FileMode]::CreateNew,
                [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $inputStream.CopyTo($outputStream) }
            finally { $outputStream.Dispose() }
        }
        finally { $inputStream.Dispose() }
        if ((Get-Item -LiteralPath $script:staged).Length -ne $entry.Length) {
            throw 'Extracted executable length mismatch.'
        }
    }
    finally { $zipFile.Dispose() }
    Assert-Executable $script:staged $Rid $releaseVersion
    Write-DeploymentMessage ('Verified {0} / {1} / source {2} / ZIP SHA-256 {3}' -f
        $releaseTag, $Rid, $releaseSourceSha, $pins[$Rid])
}
function Get-ManagedDirectory {
    $root = if ([Environment]::Is64BitOperatingSystem) {
        [Environment]::GetEnvironmentVariable('ProgramW6432')
    } else { $env:ProgramFiles }
    if ([string]::IsNullOrWhiteSpace($root)) { throw 'Program Files location unavailable.' }
    Assert-NoReparsePoint $root
    return (Join-Path $root 'BitKeyBridge')
}

try {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        throw 'This managed deployment script is Windows-only.'
    }
    $detected = Get-DeviceArchitecture
    $rid = if ($Architecture -eq 'Auto') { $detected } else { $Architecture }
    if ($Mode -ne 'Validate' -and $rid -ne $detected) {
        throw ('Cannot install architecture {0} on OS architecture {1}.' -f $rid,$detected)
    }
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        $PackagePath = Join-Path $PSScriptRoot ('BitKeyBridge-' + $rid + '.zip')
    }
    $PackagePath = [IO.Path]::GetFullPath($PackagePath)

    if ($Mode -eq 'Validate') {
        Validate-Package $PackagePath $rid ([IO.Path]::GetTempPath())
        Write-DeploymentMessage 'Validation passed. No machine changes were made.'
        exit 0
    }

    Assert-Administrator
    if (-not $ApproveUnsigned) {
        throw 'Unsigned managed deployment requires explicit -ApproveUnsigned.'
    }
    if ($Ring -eq 'Broad' -and -not $AuthorizeBroadRollout) {
        throw 'Broad rollout requires explicit -AuthorizeBroadRollout after managed pilot approval.'
    }
    Assert-UnsignedPolicy
    $targetDir = Get-ManagedDirectory
    Assert-NoReparsePoint $targetDir
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    $targetExe = Join-Path $targetDir 'BitKeyBridge.exe'
    $backupExe = $targetExe + '.bak'
    foreach ($item in @($targetDir,$targetExe,$backupExe)) { Assert-NoReparsePoint $item }

    # Lock only this administrator-owned Program Files deployment folder.
    $lockFile = Join-Path $targetDir '.managed-deploy.lock'
    Assert-NoReparsePoint $lockFile
    $script:gate = [IO.File]::Open($lockFile,[IO.FileMode]::OpenOrCreate,
        [IO.FileAccess]::ReadWrite,[IO.FileShare]::None)

    $logDir = Join-Path $env:WINDIR 'Logs\BitKeyBridge'
    Assert-NoReparsePoint $logDir
    if (-not (Test-Path -LiteralPath $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
    $script:logPath = Join-Path $logDir 'ManagedDeployment.log'
    Assert-NoReparsePoint $script:logPath

    Assert-ProcessStopped
    if ($Mode -eq 'Install') {
        Validate-Package $PackagePath $rid $targetDir
        if (Test-Path -LiteralPath $targetExe -PathType Leaf) {
            $installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($targetExe).FileVersion
            $currentVersion = New-Object Version($installedVersion)
            $newVersion = New-Object Version('0.32.0.0')
            if ($currentVersion -gt $newVersion) {
                throw 'Refusing an automatic downgrade from an installed newer version.'
            }
            if ($currentVersion -eq $newVersion) {
                if ((Get-FileHash -LiteralPath $targetExe -Algorithm SHA256).Hash -ne
                    (Get-FileHash -LiteralPath $script:staged -Algorithm SHA256).Hash) {
                    throw 'Installed version has different bytes; refusing silent same-version replacement.'
                }
                Write-DeploymentMessage 'Already at the approved exact release; no changes required.'
                exit 0
            }
            [IO.File]::Replace($script:staged, $targetExe, $backupExe, $false)
            $script:staged = $null
        } else {
            [IO.File]::Move($script:staged, $targetExe)
            $script:staged = $null
        }
        try {
            Assert-Executable $targetExe $rid $releaseVersion
        }
        catch {
            if (Test-Path -LiteralPath $backupExe -PathType Leaf) {
                [IO.File]::Replace($backupExe,$targetExe,$null,$false)
            } else {
                Remove-Item -LiteralPath $targetExe -Force
            }
            throw 'Installed executable failed post-install verification; restored previous binary.'
        }
        Write-DeploymentMessage ('Installed pinned unsigned {0} on {1} ring. Previous binary retained if present.' -f $releaseTag,$Ring)
    }
    elseif ($Mode -eq 'Rollback') {
        if (-not (Test-Path -LiteralPath $targetExe -PathType Leaf) -or
            -not (Test-Path -LiteralPath $backupExe -PathType Leaf)) {
            throw 'Rollback requires the installed executable and an existing local .bak file.'
        }
        $previous = [Diagnostics.FileVersionInfo]::GetVersionInfo($backupExe).FileVersion
        if ($previous -notmatch '^\d+\.\d+\.\d+\.0$') {
            throw 'Backup executable version is invalid.'
        }
        if ((Get-PeMachine $backupExe) -ne $machines[$rid]) {
            throw 'Backup architecture is incompatible.'
        }
        [IO.File]::Replace($backupExe,$targetExe,$null,$false)
        Write-DeploymentMessage ('Rolled back to preserved local binary version {0}.' -f $previous)
    }
}
catch {
    Write-Error ('Managed deployment refused: {0}' -f $_.Exception.Message)
    exit 1
}
finally {
    if ($null -ne $script:staged -and (Test-Path -LiteralPath $script:staged -PathType Leaf)) {
        Remove-Item -LiteralPath $script:staged -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $script:gate) { $script:gate.Dispose() }
}
