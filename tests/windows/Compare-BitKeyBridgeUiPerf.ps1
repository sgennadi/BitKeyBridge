#requires -Version 5.1
<#
.SYNOPSIS
Compare non-secret UI timing reports from BitKeyBridge 0.33.1 and 0.34.
.DESCRIPTION
Reads only allowlisted numeric timing lines. It never emits raw report text,
names, device identities, account data, tokens, paths or recovery material.
Missing percentiles in older builds remain N/A; no percentiles are inferred.
Run this offline as a standard user, with no admin rights or network access.
.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compare-BitKeyBridgeUiPerf.ps1 -BaselinePath .\stable-ui.txt -PreviewPath .\preview-ui.txt
.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compare-BitKeyBridgeUiPerf.ps1 -SelfTest
#>
[CmdletBinding(DefaultParameterSetName = 'Compare')]
param(
    [Parameter(ParameterSetName = 'Compare', Mandatory = $true)]
    [string] $BaselinePath,
    [Parameter(ParameterSetName = 'Compare', Mandatory = $true)]
    [string] $PreviewPath,
    [Parameter(ParameterSetName = 'Compare')]
    [ValidateRange(1, 128)]
    [int] $MinimumSamples = 5,
    [Parameter(ParameterSetName = 'Compare')]
    [string] $OutputCsv,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory = $true)]
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:Invariant = [System.Globalization.CultureInfo]::InvariantCulture

# Fixed source-code phase labels only. Never use a label from the input as an
# output value, even if the input is a copied diagnostic or clipboard text.
$script:Phases = @(
    'Main window viewport',
    'Full DPI/font refresh (all)',
    'Initial DPI/font refresh',
    'Subsequent DPI/font refresh',
    'DPI phase - WinForms PerformLayout',
    'DPI phase - label widths',
    'Initial language labels',
    'Language changes (warm)',
    'Language EN/RU -> Hebrew',
    'Language Hebrew -> EN/RU',
    'Language EN <-> RU',
    'Language chrome / tab mirroring',
    'Tab RTL direction setter',
    'Tab native mirror setter',
    'Tab RTL setter - actual warm changes',
    'Tab native mirror - actual warm changes',
    'Language captions',
    'Language layout resume',
    'Language after-apply UI dispatch',
    'Language selected viewport reflow',
    'Language to selected viewport ready'
)
foreach ($page in @('Start', 'BitLocker', 'LAPS', 'Devices', 'Helpdesk', 'Diagnostics', 'Settings')) {
    $script:Phases += $page
    $script:Phases += "$page - selection to UI callback"
    $script:Phases += "$page - viewport reflow"
}

$script:Allowed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($phase in $script:Phases) {
    [void] $script:Allowed.Add($phase)
}

function Convert-Numeric([string] $value) {
    return [double]::Parse(
        $value, [System.Globalization.NumberStyles]::Float, $script:Invariant)
}

function Get-SafeMetrics([string[]] $lines) {
    $values = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($line in $lines) {
        if ($line.Length -gt 1024) { continue }
        $name = $null
        $runs = 0
        $mean = $null
        $p50 = $null
        $p95 = $null

        # Layout counters: v0.33.1 mean/max, v0.34 optional p50/p95.
        if ($line -match '^(?<name>[^:]{1,96}): runs=(?<runs>[0-9]{1,9}), mean=(?<mean>[0-9]+(?:\.[0-9]+)?) ms, max=[0-9]+(?:\.[0-9]+)? ms(?:,|$)') {
            $name = $Matches['name']
            $runs = [int]::Parse($Matches['runs'], $script:Invariant)
            $mean = Convert-Numeric $Matches['mean']
        }
        # Navigation metrics: p50/p95 exist in both 0.33.1 and 0.34.
        elseif ($line -match '^(?<name>[^:]{1,96}): runs=(?<runs>[0-9]{1,9}), (?:window=[0-9]{1,9}, )?p50=(?<p50>[0-9]+(?:\.[0-9]+)?) ms, p95=(?<p95>[0-9]+(?:\.[0-9]+)?) ms, max=[0-9]+(?:\.[0-9]+)? ms(?:,|$)') {
            $name = $Matches['name']
            $runs = [int]::Parse($Matches['runs'], $script:Invariant)
            $p50 = Convert-Numeric $Matches['p50']
            $p95 = Convert-Numeric $Matches['p95']
        }
        if ($null -eq $name -or -not $script:Allowed.Contains($name)) { continue }
        if ($null -ne $mean) {
            $m50 = [regex]::Match($line, '(?:^|,\s*)p50=([0-9]+(?:\.[0-9]+)?) ms')
            $m95 = [regex]::Match($line, '(?:^|,\s*)p95=([0-9]+(?:\.[0-9]+)?) ms')
            if ($m50.Success) { $p50 = Convert-Numeric $m50.Groups[1].Value }
            if ($m95.Success) { $p95 = Convert-Numeric $m95.Groups[1].Value }
        }
        $values[$name] = @{
            Runs = $runs
            Mean = $mean
            P50 = $p50
            P95 = $p95
        }
    }
    return $values
}

function Get-SafeComparison([string[]] $baselineLines,
                            [string[]] $previewLines,
                            [int] $minSamples) {
    $baseline = Get-SafeMetrics $baselineLines
    $preview = Get-SafeMetrics $previewLines
    if ($baseline.Count -eq 0 -or $preview.Count -eq 0) {
        throw 'No recognized UI timing counters in one or both reports.'
    }

    foreach ($phase in $script:Phases) {
        if (-not ($baseline.ContainsKey($phase) -or $preview.ContainsKey($phase))) {
            continue
        }
        $b = if ($baseline.ContainsKey($phase)) { $baseline[$phase] } else { $null }
        $p = if ($preview.ContainsKey($phase)) { $preview[$phase] } else { $null }

        $bRuns = if ($null -ne $b) { [int]$b.Runs } else { 0 }
        $pRuns = if ($null -ne $p) { [int]$p.Runs } else { 0 }
        $basis = 'N/A'
        $delta = 'N/A'
        if ($null -ne $b -and $null -ne $p -and
            $bRuns -ge $minSamples -and $pRuns -ge $minSamples) {
            if ($null -ne $b.Mean -and $null -ne $p.Mean -and $b.Mean -gt 0) {
                $basis = 'mean'
                $delta = ((($p.Mean - $b.Mean) / $b.Mean) * 100).ToString('+#0.0;-#0.0;0.0', $script:Invariant) + '%'
            }
            elseif ($null -ne $b.P95 -and $null -ne $p.P95 -and $b.P95 -gt 0) {
                $basis = 'p95'
                $delta = ((($p.P95 - $b.P95) / $b.P95) * 100).ToString('+#0.0;-#0.0;0.0', $script:Invariant) + '%'
            }
        }

        [pscustomobject]@{
            Phase = $phase
            StableRuns = $bRuns
            PreviewRuns = $pRuns
            StableMeanMs = $(if ($null -ne $b -and $null -ne $b.Mean) { $b.Mean } else { $null })
            PreviewMeanMs = $(if ($null -ne $p -and $null -ne $p.Mean) { $p.Mean } else { $null })
            StableP95Ms = $(if ($null -ne $b -and $null -ne $b.P95) { $b.P95 } else { 'N/A' })
            PreviewP95Ms = $(if ($null -ne $p -and $null -ne $p.P95) { $p.P95 } else { 'N/A' })
            DeltaBasis = $basis
            DeltaPercent = $delta
        }
    }
}

if ($SelfTest) {
    $baseline = @(
        'Language changes (warm): runs=10, mean=100.0 ms, max=200.0 ms, over 50 ms=7',
        'Start: runs=8, window=8, p50=20.0 ms, p95=60.0 ms, max=80.0 ms',
        'user@example.invalid: runs=99, mean=999.0 ms, max=999.0 ms',
        'Device secret-key-123: runs=9, mean=999.0 ms, max=999.0 ms'
    )
    $preview = @(
        'Language changes (warm): runs=10, mean=80.0 ms, max=120.0 ms, over 50 ms=5, recent=10/128, p50=70.0 ms, p95=110.0 ms',
        'Start: runs=8, window=8, p50=15.0 ms, p95=40.0 ms, max=55.0 ms',
        'Language EN/RU -> Hebrew: runs=3, mean=200.0 ms, max=260.0 ms, over 50 ms=3, recent=3/128, p50=190.0 ms, p95=260.0 ms'
    )
    $rows = @(Get-SafeComparison $baseline $preview 5)
    $language = @($rows | Where-Object { $_.Phase -eq 'Language changes (warm)' })
    $start = @($rows | Where-Object { $_.Phase -eq 'Start' })
    $hebrew = @($rows | Where-Object { $_.Phase -eq 'Language EN/RU -> Hebrew' })
    if ($rows.Count -ne 3 -or $language.Count -ne 1 -or $start.Count -ne 1 -or
        $hebrew.Count -ne 1 -or
        $language[0].DeltaBasis -ne 'mean' -or
        $language[0].DeltaPercent -ne '-20.0%' -or
        $language[0].StableP95Ms -ne 'N/A' -or
        $start[0].DeltaBasis -ne 'p95' -or
        $start[0].DeltaPercent -ne '-33.3%' -or
        $hebrew[0].DeltaBasis -ne 'N/A' -or
        $hebrew[0].StableP95Ms -ne 'N/A' -or
        $hebrew[0].DeltaPercent -ne 'N/A') {
        throw 'UI comparison parsing, missing-data or threshold regression.'
    }
    $safe = $rows | ConvertTo-Json -Depth 4 -Compress | Out-String
    if ($safe.Contains('user@example.invalid') -or $safe.Contains('secret-key-123')) {
        throw 'UI comparison output retained untrusted identity or secret text.'
    }
    Write-Output 'UI LATENCY COMPARATOR SELF-TEST OK'
    exit 0
}

foreach ($file in @($BaselinePath, $PreviewPath)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw 'One requested UI performance report file does not exist.'
    }
    if ((Get-Item -LiteralPath $file).Length -gt 131072) {
        throw 'UI timing report exceeds the maximum supported input size (128 KiB).'
    }
}

$stable = @(Get-Content -LiteralPath $BaselinePath -Encoding UTF8)
$preview = @(Get-Content -LiteralPath $PreviewPath -Encoding UTF8)
$rows = @(Get-SafeComparison $stable $preview $MinimumSamples)
Write-Output 'BitKeyBridge local UI timing comparison (recognized numeric phases only).'
Write-Output 'Negative deltas mean lower measured latency. N/A is not a zero or a pass.'
Write-Output 'Comparisons require enough completed samples in BOTH versions; no native paint/RDP transport is measured.'
$rows | Format-Table Phase,StableRuns,PreviewRuns,StableMeanMs,PreviewMeanMs,StableP95Ms,PreviewP95Ms,DeltaBasis,DeltaPercent -AutoSize | Out-String -Width 240 | Write-Output

if (-not [string]::IsNullOrWhiteSpace($OutputCsv)) {
    if (Test-Path -LiteralPath $OutputCsv) {
        throw 'Refusing to overwrite an existing CSV report.'
    }
    # Only fixed metric labels and numeric values go to CSV.
    $rows | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
    Write-Output 'Sanitized numeric CSV report created.'
}
