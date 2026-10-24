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
    $release -notmatch 'needs: \[build, laps-regression, audit-broker-integration, legacy-upgrade-regression, codeql-stable-release\]') {
    throw 'Stable unsigned v0.30.4 lacks exact commit, broker, legacy upgrade, or checksum gates.'
}

# v0.30.5 must use its explicitly approved release branch and safe ZIP build.
$pushBranches = @($workflow -split '\n' | Where-Object { $_ -like '    branches: *' })
if ($pushBranches.Count -ne 1 -or
    -not $pushBranches[0].Contains('"release/v0.30.5"') -or
    -not $release.Contains("github.event.head_commit.message == 'Release v0.30.5'") -or
    -not $release.Contains("github.ref != 'refs/heads/release/v0.30.5'") -or
    -not $release.Contains("needs['legacy-upgrade-regression'].result == 'success'") -or
    -not $release.Contains("needs['audit-broker-integration'].result == 'success'") -or
    -not $workflow.Contains('Compress-Archive -LiteralPath $exe -DestinationPath $zip') -or
    -not $workflow.Contains('Verify-UpdateZipLayout.ps1') -or
    -not $workflow.Contains('UpdateZipLayoutRegression.ps1')) {
    throw 'v0.30.5 trigger, ZIP regression, broker or upgrade gates are missing.'
}

# Exact unsigned v0.30.5 must NOT attempt to attest absent signing.json.
# Keep Authenticode verification/evidence mandatory for other protected
# audit releases that require a genuine organization signing identity.
$signingEvidence = [regex]::Match($release,
    '(?ms)^      - name: Attest protected audit signature verification evidence\r?\n(?<step>.*?)(?=^      - name: Attest release SBOM)')
if (-not $signingEvidence.Success -or
    $signingEvidence.Groups['step'].Value -notmatch "github\.ref != 'refs/heads/release/v0\.30\.5'") {
    throw 'v0.30.5 unsigned stable release must skip signed-only attestation evidence.'
}
# v0.32.0 stable requires exact release commit and same-SHA CodeQL.
if (-not $pushBranches[0].Contains('"release/v0.32.0"') -or
    -not $release.Contains("github.event.head_commit.message == 'Release v0.32.0'") -or
    -not $release.Contains("github.ref != 'refs/heads/release/v0.32.0'") -or
    -not $release.Contains("needs['codeql-stable-release'].result == 'success'") -or
    -not $release.Contains('docs/release-authorization-v0.32.0.md') -or
    -not $release.Contains('docs/release-notes-v0.32.0.md') -or
    -not $workflow.Contains('  codeql-stable-release:')) {
    throw 'Stable v0.32.0 exact-source security and release guards are missing.'
}
# v0.32.1 unsigned Stable requires the exact release source with CodeQL,
# protected audit, migration tests, and an independently SHA-256-pinned
# administrator-managed canary installer before GitHub publication.
if (-not $pushBranches[0].Contains('"release/v0.32.1"') -or
    -not $release.Contains("github.event.head_commit.message == 'Release v0.32.1'") -or
    -not $release.Contains("github.ref != 'refs/heads/release/v0.32.1'") -or
    -not $release.Contains("needs['codeql-stable-release'].result == 'success'") -or
    -not $release.Contains("needs['legacy-upgrade-regression'].result == 'success'") -or
    -not $release.Contains("needs['audit-broker-integration'].result == 'success'") -or
    -not $release.Contains('docs/release-authorization-v0.32.1.md') -or
    -not $release.Contains('docs/release-notes-v0.32.1.md') -or
    -not $release.Contains('Generate v0.32.1 canary installer pinned to actual built ZIPs') -or
    -not $release.Contains('Verify v0.32.1 canary installer and reject tampered ZIP') -or
    -not $release.Contains('Attest pinned canary installer')) {
    throw 'Missing exact v0.32.1 release authorization, on-SHA checks or canary security gates.'
}
if ($release.IndexOf('Generate v0.32.1 canary installer pinned to actual built ZIPs') -ge
    $release.IndexOf('Generate SHA256SUMS') -or
    $release.IndexOf('Verify v0.32.1 canary installer and reject tampered ZIP') -ge
    $release.IndexOf('Generate SHA256SUMS')) {
    throw 'The canary installer must pass tamper checks before checksum publication.'
}
# The v0.30.4 reliability PR must run the TWO-identity protected audit
# broker integration test, not just compile the broker code.
if ($workflow -notmatch
    "github\.head_ref == 'feature/v0\.30\.4-reliability-20261017'") {
    throw 'Reliability PR must execute the protected audit broker integration.'
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
