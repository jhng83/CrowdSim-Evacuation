param(
    [int]$Rounds = 3,
    [double]$AllowedRegressionPercent = 0
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$results = Join-Path $root 'validation'
New-Item -ItemType Directory -Force -Path $results | Out-Null
if ($Rounds -lt 1) { throw 'Rounds must be positive.' }
function Run-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
function Get-Median($Values) {
    $sorted = @($Values | Sort-Object)
    $mid = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return [double]$sorted[$mid] }
    return ([double]$sorted[$mid - 1] + [double]$sorted[$mid]) / 2
}
Push-Location $root
try {
    Run-Dotnet -Arguments @('test', 'CrowdSim.sln', '-c', 'Release', '--logger',
        'trx;LogFileName=regression.trx', '--results-directory', $results)
    foreach ($label in @('Baseline', 'Updated')) {
        Run-Dotnet -Arguments @('build', "benchmarks/$label.csproj", '-c', 'Release',
            '-o', (Join-Path $results "$label-bin"))
    }
    for ($round = 0; $round -lt $Rounds; $round++) {
        $labels = if ($round % 2 -eq 0) { @('Baseline', 'Updated') } else { @('Updated', 'Baseline') }
        foreach ($label in $labels) {
            Write-Host "Round $($round + 1)/${Rounds}: $label (100, 200, 500, 1000 agents)"
            $dll = Join-Path $results "$label-bin/CrowdSim.Bench.$label.dll"
            $csv = Join-Path $results "$label-$round.csv"
            & dotnet $dll $csv (42 + $round) > (Join-Path $results "$label-$round.log")
            if ($LASTEXITCODE -ne 0) { throw "$label benchmark failed; see validation/$label-$round.log" }
        }
    }
    $all = @{}
    foreach ($label in @('Baseline', 'Updated')) {
        $all[$label] = @(for ($round = 0; $round -lt $Rounds; $round++) {
            Import-Csv (Join-Path $results "$label-$round.csv")
        })
    }
    $culture = [Globalization.CultureInfo]::InvariantCulture
    $summary = foreach ($group in ($all['Baseline'] | Group-Object Mode, Agents)) {
        $first = $group.Group[0]
        $candidate = @($all['Updated'] | Where-Object {
            $_.Mode -eq $first.Mode -and $_.Agents -eq $first.Agents
        })
        $oldMedian = Get-Median @($group.Group | ForEach-Object { [double]::Parse($_.MedianMs, $culture) })
        $newMedian = Get-Median @($candidate | ForEach-Object { [double]::Parse($_.MedianMs, $culture) })
        $oldP95 = Get-Median @($group.Group | ForEach-Object { [double]::Parse($_.P95Ms, $culture) })
        $newP95 = Get-Median @($candidate | ForEach-Object { [double]::Parse($_.P95Ms, $culture) })
        $medianChange = 100 * ($newMedian / [Math]::Max($oldMedian, 0.000001) - 1)
        $p95Change = 100 * ($newP95 / [Math]::Max($oldP95, 0.000001) - 1)
        [pscustomobject]@{
            Mode = $first.Mode; Agents = $first.Agents
            BaselineMedianMs = $oldMedian; UpdatedMedianMs = $newMedian
            BaselineP95Ms = $oldP95; UpdatedP95Ms = $newP95
            MedianChangePercent = [Math]::Round($medianChange, 2)
            P95ChangePercent = [Math]::Round($p95Change, 2)
            Status = if ($medianChange -le $AllowedRegressionPercent -and
                $p95Change -le $AllowedRegressionPercent) { 'PASS' } else { 'REVIEW' }
        }
    }
    $summary | Export-Csv (Join-Path $results 'comparison.csv') -NoTypeInformation
    $summary | Format-Table Mode, Agents, MedianChangePercent, P95ChangePercent, Status -AutoSize
    if (@($summary | Where-Object { $_.Status -eq 'REVIEW' }).Count -gt 0) {
        Write-Warning 'Latency gate not met. Inspect comparison.csv and logs; do not claim no regression.'
        exit 2
    }
    Write-Host 'Regression suite and CPU benchmark latency gate passed on this machine.'
    Write-Host 'GPU submission, input latency, recording, and full-window FPS still require an app check.'
}
finally {
    Pop-Location
}
