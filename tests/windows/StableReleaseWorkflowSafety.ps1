#Requires -Version 7.0
# CI-only regression: stable publishing must remain gated, signed and non-destructive.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$workflow = Get-Content -LiteralPath (Join-Path $root '.github\workflows\build.yml') -Raw
$project = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\BitKeyBridge.csproj') -Raw
$config = Get-Content -LiteralPath (Join-Path $root 'src\BitKeyBridge\Core\AppConfig.cs') -Raw

$required = @(
  'BITKEYBRIDGE_STABLE_030_APPROVED',
  'BITKEYBRIDGE_SIGNING_THUMBPRINT',
  'Verify-ReleaseSigning.ps1',
  'ExpectedSourceRevision',
  'BitKeyBridge.signatures.json',
  'Smoke-test staged updater preferences (no network install)',
  'Create GitHub Release'
)
if ($workflow -notmatch '(?m)^permissions:\r?\n  contents: read(?:\r?\n|$)') {
  throw 'CI jobs must have read-only default repository permissions.'
}
foreach ($token in $required) {
  if (-not $workflow.Contains($token)) { throw "Missing release safety requirement: $token" }
}
$approval = $workflow.IndexOf('Require signed managed-workstation acceptance', [StringComparison]::Ordinal)
$verify = $workflow.IndexOf('Verify trusted signed x64 x86 ARM64 executables', [StringComparison]::Ordinal)
$smoke = $workflow.IndexOf('Smoke-test staged updater preferences (no network install)', [StringComparison]::Ordinal)
$publish = $workflow.IndexOf('Create GitHub Release', [StringComparison]::Ordinal)
if ($approval -lt 0 -or $verify -lt $approval -or $smoke -lt $verify -or $publish -lt $smoke) {
  throw 'Publication gate / signature / preflight ordering is unsafe.'
}
if ($workflow -match 'Archive and remove stale release branches|gh api --method DELETE|--update --no-elevation') {
  throw 'Release workflow must not install a remote update or delete repository branches.'
}
if ($project -notmatch '<Version>0\.30\.0</Version>' -or
    $project -notmatch '<InformationalVersion>0\.30\.0</InformationalVersion>') {
  throw 'Stable release version identity mismatch.'
}
if ($config -notmatch 'AutoInstallUpdatesOnStart\s*\{\s*get;\s*set;\s*\}\s*=\s*false;') {
  throw 'Safe new-install automatic-update default has regressed.'
}
# Public unsigned technician downloads must not become legacy auto-updates.
$manualJobStart = $workflow.IndexOf('  public-manual-stable:', [StringComparison]::Ordinal)
$signedJobStart = $workflow.IndexOf('  release:', [StringComparison]::Ordinal)
if ($manualJobStart -lt 0 -or $signedJobStart -le $manualJobStart) {
  throw 'Public manual-only release job must be present before signed release job.'
}
$manualJob = $workflow.Substring($manualJobStart, $signedJobStart - $manualJobStart)
foreach ($requiredManual in @('Publish public stable v0.30.0', 'needs: [build, laps-regression]', 'MANUAL-UNSIGNED.zip', 'sha256sum -c SHA256SUMS.txt', '--latest')) {
  if (-not $manualJob.Contains($requiredManual)) {
    throw ('Public manual-only release requirement missing: ' + $requiredManual)
  }
}
if ($manualJob.Contains('public-release/BitKeyBridge-win-x64.zip') -or
    $manualJob.Contains('public-release/BitKeyBridge-win-x86.zip') -or
    $manualJob.Contains('public-release/BitKeyBridge-win-arm64.zip') -or
    $manualJob.Contains('--update --no-elevation')) {
  throw 'Unsigned manual public release cannot expose canonical unattended-update assets.'
}

Write-Host 'Stable v0.30.0 publication safety invariants passed.'
