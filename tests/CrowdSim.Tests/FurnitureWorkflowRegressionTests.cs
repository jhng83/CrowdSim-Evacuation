using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Regression cases for the reported 205-person furniture scene and navigation changes.</summary>
public sealed class FurnitureWorkflowRegressionTests
{
    private static ScenarioDefinition LoadFixture() => JsonSerializer.Deserialize<ScenarioDefinition>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "toxic_fume_205.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static Simulation CreateScene()
    {
        ScenarioDefinition saved = LoadFixture();
        Simulation sim = new(0, saved.CreateConfig(), saved.RandomSeed);
        saved.ApplyLayout(sim);
        sim.SpawnAgentsAtPositions(sim.Environment.GetPlannedAgentPositions());
        return sim;
    }

    [Fact]
    public void ReportedSceneLoadsWithoutMovingOrDuplicatingOccupants()
    {
        Simulation sim = CreateScene();
        Assert.Equal(205, sim.Agents.Count);
        Assert.Equal(8, sim.Environment.Furniture.Count);
        Assert.Equal(92, sim.Environment.CircularBlockages.Count);
        Assert.Equal(80, sim.Agents.Count(a => a.Posture == SeatingPosture.Seated));
        Assert.Equal(sim.Environment.GetPlannedAgentPositions(), sim.Agents.Select(a => a.Position).ToList());
    }

    [Fact]
    public void ReportedSceneSurvivesFileSaveLoadRoundTrip()
    {
        string directory = Path.Combine(Path.GetTempPath(), "crowdsim-regression-" + Guid.NewGuid().ToString("N"));
        try
        {
            ScenarioDefinition original = LoadFixture();
            ScenarioFileStore.Save(original, directory);
            ScenarioDefinition loaded = ScenarioFileStore.Load(original.Name, directory);
            Simulation sim = new(0, loaded.CreateConfig(), loaded.RandomSeed);
            loaded.ApplyLayout(sim);
            Assert.Equal(205, sim.Environment.PlannedAgentCount);
            Assert.Equal(8, sim.Environment.Furniture.Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void ReportedSceneReplaysAtFrameZeroAfterFumeAndChairDeparture()
    {
        Simulation sim = CreateScene();
        Vector2[] starts = sim.Agents.Select(a => a.Position).ToArray();
        var checkpoint = FinalizedSceneCheckpoint.Capture(sim, sim.Agents.Count, "replay_regression");
        sim.PrecomputeToxicFume();
        sim.SetRunning(true);
        Assert.True(sim.TriggerToxicFumeRelease());
        for (int frame = 0; frame < 120; frame++) sim.Step();
        Assert.Contains(sim.Environment.Furniture.SelectMany(f => f.Chairs), c => c.State == ChairState.Fallen);
        Simulation replay = checkpoint.CreateReadySimulation();
        Assert.False(replay.Running);
        Assert.False(replay.ToxicFumeSystem.IsReleased);
        Assert.Equal(0, replay.FrameIndex);
        Assert.Equal(starts, replay.Agents.Select(a => a.Position).ToArray());
        Assert.Equal(80, replay.Agents.Count(a => a.Posture == SeatingPosture.Seated));
        Assert.All(replay.Environment.Furniture.SelectMany(f => f.Chairs), c => Assert.Equal(ChairState.Upright, c.State));
    }

    [Fact]
    public void RestoreStillRejectsStandingPersonInsidePhysicalChair()
    {
        ScenarioDefinition saved = LoadFixture();
        FurnitureScenario first = saved.Furniture[0];
        saved.AgentSpawnGroups.Add(new AgentSpawnGroupScenario
        {
            CenterX = first.X + FurnitureSet.SeatRingRadius, CenterY = first.Y,
            Positions = new() { new AgentStartScenario { X = first.X + FurnitureSet.SeatRingRadius, Y = first.Y } },
        });
        Simulation sim = new(0, saved.CreateConfig(), saved.RandomSeed);
        Assert.Throws<InvalidOperationException>(() => saved.ApplyLayout(sim));
    }

    [Fact]
    public void RestoreStillRejectsOverlappingFurniture()
    {
        ScenarioDefinition saved = LoadFixture();
        saved.Furniture.Add(saved.Furniture[0]);
        Simulation sim = new(0, saved.CreateConfig(), saved.RandomSeed);
        Assert.Throws<InvalidOperationException>(() => saved.ApplyLayout(sim));
    }

    [Fact]
    public void HeavyFurnitureRoutesRemainBodyClearBeforeAndAfterToppling()
    {
        Simulation sim = CreateScene();
        VerifyRoute(sim);
        sim.PrecomputeToxicFume();
        sim.SetRunning(true);
        sim.TriggerToxicFumeRelease();
        for (int frame = 0; frame < 110; frame++) sim.Step();
        VerifyRoute(sim);
    }

    private static void VerifyRoute(Simulation sim)
    {
        Vector2 start = new(3f, 12f), goal = new(30f, 12f);
        float clearance = sim.Config.Agent.Radius * sim.Config.Agent.NavigationClearanceFactor;
        Assert.False(sim.HasTraversablePath(start, goal, clearance));
        IReadOnlyList<Vector2> route = sim.EvacuationSystem.PlanBodyClearRoute(sim, start, goal);
        Assert.NotEmpty(route);
        Assert.Equal(goal, route[^1]);
        foreach (Vector2 next in route)
        {
            Assert.True(sim.HasTraversablePath(start, next, clearance));
            start = next;
        }
    }
}
