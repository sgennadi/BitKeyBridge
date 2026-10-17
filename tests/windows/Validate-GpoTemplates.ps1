#Requires -Version 7.0
<#
.SYNOPSIS
Validate localized, restriction-only BitKeyBridge Administrative Templates.
.DESCRIPTION
CI-only parser. No GPO linking, AD contact or endpoint changes.
A real Windows GPMC Central Store acceptance test is still required.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Join-Path (Get-Location).Path 'deployment/gpo'
$admxPath = Join-Path $root 'BitKeyBridge.admx'
if (-not (Test-Path -LiteralPath $admxPath -PathType Leaf)) {
    throw 'BitKeyBridge ADMX template is missing.'
}

$raw = Get-Content -LiteralPath $admxPath -Raw -Encoding UTF8
[xml]$admx = $raw
if ($admx.DocumentElement.LocalName -ne 'policyDefinitions') {
    throw 'Unexpected ADMX XML document root.'
}

$policyNames = @(
    'DisableAutomaticUpdates',
    'DisableUnsignedAutomaticUpdates',
    'RequireSignedAutomaticUpdates'
)
$policies = @($admx.policyDefinitions.policies.policy)
if ($policies.Count -ne $policyNames.Count) {
    throw 'Unexpected number of GPO machine restriction policies.'
}

foreach ($name in $policyNames) {
    $matched = @($policies | Where-Object { $_.name -ceq $name })
    if ($matched.Count -ne 1) {
        throw "GPO policy '$name' is missing or duplicated."
    }
    $policy = $matched[0]
    if ($policy.class -ne 'Machine' -or
        $policy.key -cne 'SOFTWARE\Policies\BitKeyBridge' -or
        $policy.valueName -cne $name -or
        $policy.enabledValue.decimal.value -ne '1' -or
        $policy.disabledValue.decimal.value -ne '0') {
        throw "GPO policy '$name' is not an exact fail-closed machine restriction."
    }
}

$requiredResources = @(
    [regex]::Matches($raw, '\$\(string\.([^)]+)\)') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
)
if ($requiredResources.Count -lt 7) {
    throw 'ADMX resource references were not found.'
}

foreach ($language in @('en-US', 'ru-RU', 'he-IL')) {
    $resourceFile = Join-Path $root "$language/BitKeyBridge.adml"
    if (-not (Test-Path -LiteralPath $resourceFile -PathType Leaf)) {
        throw "ADML resources are missing for $language."
    }

    [xml]$adml = Get-Content -LiteralPath $resourceFile -Raw -Encoding UTF8
    if ($adml.DocumentElement.LocalName -ne 'policyDefinitionResources') {
        throw "Unexpected ADML document root for $language."
    }
    $strings = @($adml.policyDefinitionResources.resources.stringTable.string)
    $seen = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $strings) {
        if (-not $seen.Add($entry.id) -or
            [string]::IsNullOrWhiteSpace($entry.InnerText)) {
            throw "Empty or repeated ADML string in $language."
        }
    }

    foreach ($resource in $requiredResources) {
        if (-not $seen.Contains($resource)) {
            throw "ADML $language lacks reference '$resource'."
        }
    }
}

$wrapperPath = Join-Path $root 'Collect-Fleet.cmd'
if (-not (Test-Path -LiteralPath $wrapperPath -PathType Leaf)) {
    throw 'Native GPO fleet collector wrapper is missing.'
}
$wrapper = Get-Content -LiteralPath $wrapperPath -Raw
$activeLines = ($wrapper -split '\r?\n' |
    Where-Object { $_ -notmatch '(?i)^\s*(?:rem\b|::)' }) -join "`n"
if ($activeLines -notmatch '--fleet-export' -or
    $activeLines -match '(?i)\b(?:powershell|pwsh)(?:\.exe)?\b' -or
    $activeLines -notmatch '--no-elevation') {
    throw 'Fleet GPO wrapper must use native, non-elevating export only.'
}

Write-Host 'GPO policy restrictions, 3-language ADML, and native collector validated.'
