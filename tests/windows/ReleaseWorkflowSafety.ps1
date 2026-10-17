#Requires -Version 7.0
<#
.SYNOPSIS
Fail CI if release publication could precede staging tests or delete branches.
.DESCRIPTION
Read-only release-workflow regression. Does not contact GitHub or modify files.
#>
[CmdletBinding()]
param(
    [string]$WorkflowPath = (Join-Path $PSScriptRoot '../../.github/workflows/build.yml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workflow = Get-Content -LiteralPath $WorkflowPath -Raw
$releaseMatch = [regex]::Match($workflow, '(?m)^  release:\s*$')
if (-not $releaseMatch.Success) {
    throw 'Missing release job.'
}
$release = $workflow.Substring($releaseMatch.Index)

# Parse the exact PowerShell release approval step on EVERY CI build.
# A malformed expression in a release-only step cannot be caught by a PR
# build that does not trigger publishing unless we parse it explicitly.
$approvalMatch = [regex]::Match(
    $release,
    '(?ms)^      - name: Enforce protected audit release approval\r?\n.*?^        run: \|\r?\n(?<script>.*?)(?=^      - name: Download x64)'
)
if (-not $approvalMatch.Success) {
    throw 'Release approval script was not found.'
}
$approvalScript = $approvalMatch.Groups['script'].Value -replace '(?m)^          ', ''
$approvalScript = $approvalScript -replace '\$\{\{[^}]+\}\}', 'test-value'
$parseTokens = $null
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseInput(
    $approvalScript, [ref]$parseTokens, [ref]$parseErrors
) | Out-Null
if ($parseErrors.Count -gt 0) {
    throw "Release approval step is invalid PowerShell: $($parseErrors[0].Message)"
}


$stageName = '      - name: Smoke-test staged updater preferences (no network install)'
$publishName = '      - name: Create GitHub Release'
$stageIndex = $release.IndexOf($stageName, [StringComparison]::Ordinal)
$publishIndex = $release.IndexOf($publishName, [StringComparison]::Ordinal)
if ($stageIndex -lt 0 -or $publishIndex -lt 0 -or
    $stageIndex -ge $publishIndex) {
    throw 'Release must test staged updater controls BEFORE publication.'
}

if ($release -notmatch 'Verify-ReleaseSigning\.ps1' -or
    $release -notmatch 'PROTECTED_AUDIT_APPROVED' -or
    $release -notmatch 'ExpectedSourceRevision') {
    throw 'Missing protected audit signing, approval or source revision gate.'
}

# Stable unsigned release requires an explicit branch and two real non-admin
# tokens. The temporary code-signing tests are NOT release signer approval.
if ($workflow -notmatch "refs/heads/release/v0\.30\.3" -or
    $release -notmatch "needs\['audit-broker-integration'\]\.result == 'success'" -or
    $release -notmatch "github\.ref != 'refs/heads/release/v0\.30\.3'" -or
    $release -notmatch 'SHA256SUMS.txt' -or
    $release -notmatch 'BitKeyBridge-win-arm64.zip') {
    throw 'Unsigned v0.30.3 release lacks its exact branch, broker, or checksum gates.'
}

# v0.30.4 is another exact owner-approved unsigned stable branch; it must
# retain full architecture, two-token broker, and SHA-256/provenance gates.
if ($workflow -notmatch "refs/heads/release/v0\.30\.4" -or
    $release -notmatch "github\.event\.head_commit\.message == 'Release v0\.30\.4'" -or
    $release -notmatch "needs\['audit-broker-integration'\]\.result == 'success'" -or
    $release -notmatch "github\.ref != 'refs/heads/release/v0\.30\.4'" -or
    $release -notmatch 'SHA256SUMS.txt' -or
    $release -notmatch "needs\['legacy-upgrade-regression'\]\.result == 'success'" -or
    $release -notmatch 'needs: \[build, laps-regression, audit-broker-integration, legacy-upgrade-regression\]') {
    throw 'Stable unsigned v0.30.4 lacks exact commit, broker, legacy upgrade, or checksum gates.'
}

# The v0.30.4 reliability PR must run the TWO-identity protected audit
# broker integration test, not just compile the broker code.
if ($workflow -notmatch
    "github\.head_ref == 'feature/v0\.30\.4-reliability-20261017'") {
    throw 'Reliability PR must execute the protected audit broker integration.'
}

# The ZIP uploaded to Releases must be consumable by legacy v0.30.3.
# Packaging the entire publish directory can include self-test logs/sidecars.
if ($workflow -match '(?m)^\s*run:\s*tar\s+-a\s+-c\s+-f\b' -or
    $workflow -notmatch 'Compress-Archive -LiteralPath \$exe -DestinationPath \$zip' -or
    $workflow -notmatch "Entries\.Count -ne 1" -or
    $workflow -notmatch "FullName -cne 'BitKeyBridge\.exe'") {
    throw 'Release packaging is not restricted to one root BitKeyBridge.exe.'
}

# Never delete operator release branches or install online updates from CI.
$forbidden = @(
    '(?im)^\s*gh\s+api\s+--method\s+DELETE\b',
    '(?im)^\s*git\s+push\b[^\r\n]*:refs/heads/',
    '(?im)^\s*- name:\s*Archive and remove stale release branches\b',
    '(?im)^\s*\$updateOutput\s*=\s*&\s*\$exe\s+--update\b',
    '(?im)^\s*gh\s+release\s+delete\b'
)
foreach ($pattern in $forbidden) {
    if ($release -match $pattern) {
        throw "Forbidden release workflow operation: $pattern"
    }
}

$afterPublish = $release.Substring($publishIndex + $publishName.Length)
if ($afterPublish -match '(?m)^\s*- name:') {
    throw 'GitHub Release creation must be the FINAL workflow step.'
}
Write-Host 'Release workflow order, signing, approval and branch safety passed.'
