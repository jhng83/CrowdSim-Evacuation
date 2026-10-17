using CrowdSim.Core;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Regression tests for reliable, repeatable scenario persistence.</summary>
public sealed class ScenarioFileStoreTests
{
    [Fact]
    public void Save_CreatesLoadableFileAndRefreshableName()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            Simulation simulation = CreateScenario();
            ScenarioDefinition scenario = ScenarioDefinition.Capture(
                simulation,
                simulation.Environment.PlannedAgentCount,
                "save works");

            string path = ScenarioFileStore.Save(scenario, directory);

            Assert.True(File.Exists(path));
            Assert.Equal("save_works.json", Path.GetFileName(path));
            Assert.Contains("save_works", ScenarioFileStore.ListScenarioNames(directory));
            ScenarioDefinition loaded = ScenarioFileStore.Load("save_works", directory);
            Assert.Equal(scenario.AgentCount, loaded.AgentCount);
            Assert.Single(loaded.Furniture);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveAs_AtomicallyOverwritesAndLeavesNoTemporaryFiles()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            Simulation simulation = CreateScenario();
            ScenarioDefinition first = ScenarioDefinition.Capture(simulation, 0, "overwrite");
            ScenarioFileStore.Save(first, directory);
            Assert.True(simulation.Environment.AddFurniture(
                CrowdSim.Core.Model.FurnitureKind.SingleChair,
                new System.Numerics.Vector2(20f, 15f)));
            ScenarioDefinition second = ScenarioDefinition.Capture(simulation, 0, "overwrite");

            ScenarioFileStore.Save(second, directory);

            ScenarioDefinition loaded = ScenarioFileStore.Load("overwrite", directory);
            Assert.Equal(2, loaded.Furniture.Count);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DefaultDirectory_IsStablePerUserLocationWhenAvailable()
    {
        string localData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        Assert.True(Path.IsPathFullyQualified(ScenarioFileStore.DefaultDirectory));
        if (!string.IsNullOrWhiteSpace(localData))
        {
            Assert.StartsWith(
                Path.GetFullPath(localData),
                Path.GetFullPath(ScenarioFileStore.DefaultDirectory),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
    }

    private static Simulation CreateScenario()
    {
        Simulation simulation = new(randomSeed: 991);
        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Environment.Width / 2f,
            simulation.Environment.BoundaryThickness / 2f,
            personsWide: 3));
        Assert.True(simulation.Environment.AddFurniture(
            CrowdSim.Core.Model.FurnitureKind.SingleChair,
            new System.Numerics.Vector2(15f, 15f)));
        return simulation;
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"crowdsim-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
