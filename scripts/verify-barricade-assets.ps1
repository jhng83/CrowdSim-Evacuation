param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$OnlyAssetTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-DotNet {
    param([string[]]$CommandArguments)
    & dotnet @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($CommandArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $root
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET 8 SDK must be installed and available on PATH.'
    }
    Invoke-DotNet -CommandArguments @('restore', '.\CrowdSim.sln')
    Invoke-DotNet -CommandArguments @('build', '.\CrowdSim.sln', '-c', $Configuration, '--no-restore')
    $testArguments = @('test', '.\tests\CrowdSim.Tests\CrowdSim.Tests.csproj', '-c', $Configuration, '--no-build', '--no-restore')
    if ($OnlyAssetTests) {
        $testArguments += @('--filter', 'FullyQualifiedName~BlockageAdjacencyTests|FullyQualifiedName~BarricadeTests|FullyQualifiedName~BuilderAssetSelectionTests')
    }
    Invoke-DotNet -CommandArguments $testArguments
}
finally {
    Pop-Location
}
