param([switch]$OnlyNewTests)
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & dotnet restore .\CrowdSim.sln
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $LASTEXITCODE" }
    & dotnet build .\CrowdSim.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    $testArgs = @("test", ".\tests\CrowdSim.Tests\CrowdSim.Tests.csproj", "-c", "Release", "--no-build", "--no-restore")
    if ($OnlyNewTests) {
        $testArgs += @("--filter", "FullyQualifiedName~BarricadeAngleTests|FullyQualifiedName~AgentRunningSpeedTests|FullyQualifiedName~BuilderBarricadeAngleTests")
    }
    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
}
finally { Pop-Location }
