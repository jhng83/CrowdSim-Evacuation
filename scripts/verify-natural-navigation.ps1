[CmdletBinding()]
param(
    [switch]$NavigationOnly,
    [switch]$Benchmark,
    [int]$Frames = 400,
    [int]$Agents = 600,
    [switch]$Hybrid,
    [ValidateSet('Explosion', 'ToxicFume')]
    [string]$Scenario = 'Explosion'
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'A .NET 8 SDK is required. Install it, reopen PowerShell, and run dotnet --info.'
}
function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE." }
}
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Invoke-DotNet -Arguments @('restore', 'CrowdSim.sln')
    Invoke-DotNet -Arguments @('build', 'CrowdSim.sln', '-c', 'Release', '--no-restore')
    $testArguments = @('test', 'tests/CrowdSim.Tests/CrowdSim.Tests.csproj', '-c', 'Release', '--no-build', '--no-restore', '--logger', 'trx;LogFileName=natural-navigation.trx', '--results-directory', 'validation/natural-navigation')
    if ($NavigationOnly) { $testArguments += @('--filter', 'FullyQualifiedName~NaturalNavigationTests|FullyQualifiedName~ToxicFumeScenarioTests') }
    Invoke-DotNet -Arguments $testArguments
    if ($Benchmark) {
        $hybridArgument = $Hybrid.IsPresent.ToString().ToLowerInvariant()
        Invoke-DotNet -Arguments @('run', '--project', 'benchmarks/NaturalNavigationLatency/NaturalNavigationLatency.csproj', '-c', 'Release', '--', "benchmark-results/natural-navigation-$Scenario", $Frames.ToString(), $Agents.ToString(), $hybridArgument, $Scenario)
    }
}
finally { Pop-Location }
