using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Road-barrier geometry, snapping, navigation, persistence and hazard integration.</summary>
public sealed class BarricadeTests
{
    [Theory]
    [InlineData(0f, 1.5f, 0.4f)]
    [InlineData(90f, 0.4f, 1.5f)]
    [InlineData(180f, 1.5f, 0.4f)]
    [InlineData(270f, 0.4f, 1.5f)]
    public void BarrierHasExactMetricFootprintAndNoOccupants(float degrees, float xSize, float ySize)
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f), degrees * MathF.PI / 180f));
        FurnitureSet set = Assert.Single(sim.Environment.Furniture);
        Wall bounds = Assert.IsType<Wall>(set.StaticObstacle);
        Assert.Equal(xSize, bounds.X2 - bounds.X1, 4);
        Assert.Equal(ySize, bounds.Y2 - bounds.Y1, 4);
        Assert.Equal(1f, FurnitureSet.BarricadeHeight);
        Assert.True(bounds.IsFurniture);
        Assert.False(bounds.IsMovableFurniture);
        Assert.Null(set.Table);
        Assert.Empty(set.Chairs);
        Assert.Equal(0, sim.Environment.PlannedAgentCount);
        Assert.Equal(0, FurnitureSet.GetSeatCount(FurnitureKind.Barricade));
        Assert.Empty(sim.Environment.CircularBlockages);
    }

    [Fact]
    public void ExistingPersistedKindNumbersAreUnchanged()
    {
        Assert.Equal(0, (int)FurnitureKind.RoundTableSet);
        Assert.Equal(1, (int)FurnitureKind.SingleChair);
        Assert.Equal(2, (int)FurnitureKind.RectangularTable);
        Assert.Equal(3, (int)FurnitureKind.Stage);
        Assert.Equal(4, (int)FurnitureKind.Barricade);
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void MultipleBarriersCanTouchAndUndoRemovesOnlyTheLast(ScenarioType scenario)
    {
        Simulation sim = new(agentCount: 0);
        sim.SetScenarioType(scenario);
        for (int i = 0; i < 8; i++)
            Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(10f + 1.5f * i, 10f)));
        Assert.Equal(8, sim.Environment.Furniture.Count);
        Assert.False(sim.Environment.HasStage);
        Wall last = Assert.IsType<Wall>(sim.Environment.Furniture[^1].StaticObstacle);
        Assert.True(sim.Environment.UndoLast());
        Assert.Equal(7, sim.Environment.Furniture.Count);
        Assert.DoesNotContain(last, sim.Environment.Walls);
        Assert.Equal(0, sim.Environment.PlannedAgentCount);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    public void NearbyParallelBarrierSnapsFlushUsingTheSamePreviewPoint(float degrees)
    {
        Simulation sim = new(agentCount: 0);
        float angle = degrees * MathF.PI / 180f;
        Vector2 axis = degrees == 0f ? Vector2.UnitX : Vector2.UnitY;
        Vector2 center = new(15f, 15f), expected = center + axis * 1.5f;
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, center, angle));
        int version = sim.Environment.WallLayoutVersion;
        Vector2 snapped = sim.Environment.SnapBarricadePlacement(expected + new Vector2(0.10f, 0.08f), angle);
        Assert.Equal(expected, snapped);
        Assert.Equal(version, sim.Environment.WallLayoutVersion); // preview is read-only
        Assert.True(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade, snapped, angle));
        FurnitureSet preview = new(FurnitureKind.Barricade, snapped, angle);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, snapped, angle));
        Assert.Equal(preview.StaticObstacle, sim.Environment.Furniture[^1].StaticObstacle);
    }

    [Fact]
    public void SnappingDoesNotReachDistantOrPerpendicularBarriers()
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f)));
        Vector2 far = new(17.5f, 15f);
        Assert.Equal(far, sim.Environment.SnapBarricadePlacement(far, 0f));
        Vector2 near = new(16.6f, 15f);
        Assert.Equal(near, sim.Environment.SnapBarricadePlacement(near, MathF.PI / 2f));
    }

    [Fact]
    public void SnapNeverPullsThePreviewIntoAnOccupiedEndpoint()
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f)));
        sim.Environment.Blockages.Add(new Wall(16f, 14f, 17f, 16f));
        sim.Environment.RebuildWalls();
        Vector2 requested = new(16.6f, 15.05f);
        Assert.Equal(requested, sim.Environment.SnapBarricadePlacement(requested, 0f));
        Assert.False(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade, requested));
    }

    [Fact]
    public void FootprintUsesTheThinBaseNotABoundingCircle()
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 0.85f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(19f, 0.85f), MathF.PI / 2f));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 0.85f)));
    }

    [Theory]
    [InlineData(FurnitureKind.SingleChair)]
    [InlineData(FurnitureKind.RectangularTable)]
    [InlineData(FurnitureKind.Stage)]
    public void BarrierCannotOverlapOtherAssets(FurnitureKind other)
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddFurniture(other, new Vector2(15f, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f)));
        Assert.Single(sim.Environment.Furniture);
    }

    [Fact]
    public void PlannedPeopleAndInvalidCoordinatesStillPreventPlacement()
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddAgentSpawnGroupFromClick(15f, 15f, 1));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(float.NaN, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f), float.PositiveInfinity));
        Assert.Empty(sim.Environment.Furniture);
    }

    [Fact]
    public void BarrierBlocksWalkingButIsNotAFullHeightBlastOrFumeWall()
    {
        Simulation sim = new(agentCount: 0);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f)));
        sim.BuildWallGrid();
        Vector2 a = new(15f, 13f), b = new(15f, 17f);
        Assert.False(sim.HasTraversablePath(a, b, sim.Config.Agent.Radius));
        Assert.True(sim.HasLineOfSight(a, b));
        Assert.False(ExplosionSystem.BlastPathIsBlocked(a, b, sim.Environment.Walls));
        Assert.False(sim.Environment.IsFumeBlocked(new Vector2(15f, 15f), 0.02f));
        VisibilityGraphPlanner planner = new(sim.Config.Agent);
        IReadOnlyList<Vector2> route = planner.Plan(sim, a, b);
        Assert.NotEmpty(route);
        Assert.Equal(b, route[^1]);
        foreach (Vector2 point in route)
        {
            Assert.True(sim.HasTraversablePath(a, point, sim.Config.Agent.Radius));
            a = point;
        }
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void SaveLoadAndReplayPreserveBarrierOrientationAndStageLimit(ScenarioType scenario)
    {
        Simulation sim = new(agentCount: 0, randomSeed: 890);
        sim.SetScenarioType(scenario);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(10f, 10f), MathF.PI / 2f));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(10f, 11.5f), MathF.PI / 2f));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(30f, 20f)));
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "road-barriers");
        Assert.Empty(saved.Blockages); // no duplicate rectangular proxies
        Assert.Empty(saved.CircularBlockages);
        ScenarioDefinition loaded = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(saved))!;
        Simulation restored = new(agentCount: 0);
        loaded.ApplyLayout(restored);
        Assert.Equal(3, restored.Environment.Furniture.Count);
        Assert.Equal(2, restored.Environment.Furniture.Count(set => set.Kind == FurnitureKind.Barricade));
        Assert.Equal(MathF.PI / 2f, restored.Environment.Furniture[0].Rotation, 4);
        Assert.False(restored.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(45f, 10f)));
        Simulation replay = FinalizedSceneCheckpoint.Capture(restored, 0, "road-barriers").CreateReadySimulation();
        Assert.Equal(3, replay.Environment.Furniture.Count);
        Assert.Empty(replay.Agents);
        Assert.Equal(sim.Environment.Furniture[0].StaticObstacle, replay.Environment.Furniture[0].StaticObstacle);
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void FixedBarrierRemainsPresentAfterTheHazardActuallyRuns(ScenarioType scenario)
    {
        Simulation sim = new(agentCount: 0, randomSeed: 890);
        sim.SetScenarioType(scenario);
        Assert.True(sim.Environment.AddExitFromClick(28f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 15f)));
        FurnitureSet barrier = sim.Environment.Furniture[0];
        Wall bounds = Assert.IsType<Wall>(barrier.StaticObstacle);
        sim.SpawnAgentsAtPositions(new[] { new Vector2(40f, 25f) });
        sim.Agents[0].MarkExited(); // valid population without random crowd contacts
        if (scenario == ScenarioType.ToxicFume)
        {
            Assert.True(sim.ToxicFumeSystem.TrySetSource(sim, new Vector2(15f, 13f)));
            sim.PrecomputeToxicFume();
        }
        sim.SetRunning(true);
        Assert.True(sim.Running);
        if (scenario == ScenarioType.Explosion)
        {
            sim.TriggerExplosion(15f, 15f);
            Assert.NotNull(sim.Environment.Explosion);
        }
        else Assert.True(sim.TriggerToxicFumeRelease());
        for (int i = 0; i < 40; i++) sim.Step();
        Assert.Equal(40, sim.FrameIndex);
        Assert.Same(bounds, barrier.StaticObstacle);
        Assert.Contains(bounds, sim.Environment.Walls);
        Assert.True(sim.Environment.IsPointBlocked(barrier.Center));
    }
}
