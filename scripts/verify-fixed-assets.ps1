param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$AssetTestsOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    & dotnet restore .\CrowdSim.sln
    if ($LASTEXITCODE -ne 0) { throw "Restore failed ($LASTEXITCODE)." }
    & dotnet build .\CrowdSim.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
    $arguments = @("test", ".\tests\CrowdSim.Tests\CrowdSim.Tests.csproj", "-c", $Configuration,
        "--no-build", "--no-restore")
    if ($AssetTestsOnly) { $arguments += @("--filter", "FullyQualifiedName~FixedAssetTests|FullyQualifiedName~RectangularTableBlastTests") }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Tests failed ($LASTEXITCODE)." }
}
finally {
    Pop-Location
}
