using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Placement, occupancy, persistence and path safety for unoccupied rectangular assets.</summary>
public sealed class FixedAssetTests
{
    [Theory]
    [InlineData(FurnitureKind.RectangularTable, 0f, 3f, 1f)]
    [InlineData(FurnitureKind.RectangularTable, 90f, 1f, 3f)]
    [InlineData(FurnitureKind.RectangularTable, 180f, 3f, 1f)]
    [InlineData(FurnitureKind.Stage, 0f, 15f, 6f)]
    [InlineData(FurnitureKind.Stage, 90f, 6f, 15f)]
    [InlineData(FurnitureKind.Stage, 270f, 6f, 15f)]
    public void ExactFootprintsNeverCreateChairsOrPeople(
        FurnitureKind kind, float degrees, float width, float length)
    {
        Simulation sim = new(randomSeed: 171);
        Assert.True(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f), degrees * MathF.PI / 180f));
        FurnitureSet set = Assert.Single(sim.Environment.Furniture);
        Wall footprint = Assert.IsType<Wall>(set.StaticObstacle);
        Assert.Equal(width, footprint.X2 - footprint.X1, 4);
        Assert.Equal(length, footprint.Y2 - footprint.Y1, 4);
        Assert.True(footprint.IsFurniture);
        Assert.Contains(footprint, sim.Environment.Walls);
        Assert.Empty(set.Chairs);
        if (kind == FurnitureKind.RectangularTable)
        {
            RectangularFurnitureBody body = Assert.IsType<RectangularFurnitureBody>(set.Table);
            Assert.Same(footprint, body.RectangleObstacle);
            Assert.True(footprint.IsMovableFurniture);
            Assert.Null(body.Obstacle);
        }
        else
        {
            Assert.Null(set.Table);
            Assert.False(footprint.IsMovableFurniture);
        }
        Assert.Empty(sim.Environment.CircularBlockages);
        Assert.Empty(sim.Environment.GetPlannedAgentPositions());
        Assert.Empty(sim.Agents);
        Assert.Equal(0, sim.Environment.PlannedAgentCount);
        Assert.Equal(0, FurnitureSet.GetSeatCount(kind));
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void StageLimitIsOnePerScenarioAndUndoReleasesIt(ScenarioType scenario)
    {
        Simulation sim = new(randomSeed: 171);
        sim.SetScenarioType(scenario);
        int initialWalls = sim.Environment.Walls.Count;
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        Assert.True(sim.Environment.HasStage);
        Assert.False(sim.Environment.CanPlaceFurniture(FurnitureKind.Stage, new Vector2(35f, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(35f, 15f)));
        Assert.Single(sim.Environment.Furniture);
        Assert.Equal(initialWalls + 1, sim.Environment.Walls.Count);
        Assert.True(sim.Environment.UndoLast());
        Assert.False(sim.Environment.HasStage);
        Assert.Equal(initialWalls, sim.Environment.Walls.Count);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(35f, 15f)));
    }

    [Fact]
    public void StandaloneTablesCanBePlacedMoreThanOnceWithoutChangingPopulation()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddAgentSpawnGroupFromClick(35f, 20f, 5));
        Vector2[] people = sim.Environment.GetPlannedAgentPositions().ToArray();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(10f, 10f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(14f, 10f)));
        Assert.Equal(2, sim.Environment.Furniture.Count);
        Assert.Equal(people, sim.Environment.GetPlannedAgentPositions().ToArray());
        Assert.Equal(5, sim.Environment.PlannedAgentCount);
    }

    [Theory]
    [InlineData(FurnitureKind.RectangularTable)]
    [InlineData(FurnitureKind.Stage)]
    public void InvalidCoordinatesAndOutOfBoundsPlacementsAreAtomic(FurnitureKind kind)
    {
        Simulation sim = new();
        int version = sim.Environment.WallLayoutVersion;
        Assert.False(sim.Environment.AddFurniture(kind, new Vector2(1f, 1f)));
        Assert.False(sim.Environment.AddFurniture(kind, new Vector2(float.NaN, 15f)));
        Assert.False(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f), float.PositiveInfinity));
        Assert.Empty(sim.Environment.Furniture);
        Assert.Equal(version, sim.Environment.WallLayoutVersion);
    }

    [Fact]
    public void RectangularPlacementUsesItsShortSideNotAnOversizedCircle()
    {
        Simulation sim = new();
        // The table's half-width is 0.5 m; a bounding circle would wrongly reject this.
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(10f, 1.25f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(14f, 1.25f), MathF.PI / 2f));
    }

    [Fact]
    public void RectangleRotationSnapsConsistentlyForPreviewAndPlacement()
    {
        Simulation sim = new();
        float angle = 68f * MathF.PI / 180f;
        FurnitureSet preview = new(FurnitureKind.RectangularTable, new Vector2(15f, 15f), angle);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, preview.Center, angle));
        Assert.Equal(MathF.PI / 2f, preview.Rotation, 4);
        Assert.Equal(preview.StaticObstacle, Assert.Single(sim.Environment.Furniture).StaticObstacle);
    }

    [Theory]
    [InlineData(FurnitureKind.RectangularTable)]
    [InlineData(FurnitureKind.Stage)]
    public void ExistingPeoplePreventOverlappingAssetPlacement(FurnitureKind kind)
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddAgentSpawnGroupFromClick(15f, 15f, 1));
        Assert.False(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f)));
        Assert.Empty(sim.Environment.Furniture);
    }

    [Fact]
    public void RectanglesCannotOverlapEachOtherAndDoNotLoseChairDepartureClearance()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(19f, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(19f, 15f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(30f, 20f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(32f, 20f)));
        Assert.Equal(10, sim.Environment.PlannedAgentCount);
    }

    [Theory]
    [InlineData(FurnitureKind.RectangularTable)]
    [InlineData(FurnitureKind.Stage)]
    public void AssetBlocksMovementButNotFullHeightSightOrHazardFields(FurnitureKind kind)
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f)));
        sim.BuildWallGrid();
        Vector2 start = new(15f, 8f);
        Vector2 end = new(15f, 22f);
        Assert.True(sim.Environment.IsPointBlocked(new Vector2(15f, 15f)));
        Assert.False(sim.HasTraversablePath(start, end, sim.Config.Agent.Radius));
        Assert.True(sim.HasLineOfSight(start, end));
        Assert.True(EvacuationSystem.HasLineOfSight(start, end, sim.Environment.Walls));
        Assert.False(ExplosionSystem.BlastPathIsBlocked(start, end, sim.Environment.Walls));
        Assert.False(sim.Environment.IsFumeBlocked(new Vector2(15f, 15f), 0.02f));
    }

    [Theory]
    [InlineData(FurnitureKind.RectangularTable)]
    [InlineData(FurnitureKind.Stage)]
    public void GlobalPlannerFindsBodyClearAlternativeAroundAsset(FurnitureKind kind)
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f)));
        sim.BuildWallGrid();
        Vector2 start = new(15f, 8f);
        Vector2 goal = new(15f, 22f);
        VisibilityGraphPlanner planner = new(sim.Config.Agent);
        IReadOnlyList<Vector2> route = planner.Plan(sim, start, goal);
        Assert.NotEmpty(route);
        Assert.Equal(goal, route[^1]);
        Vector2 previous = start;
        foreach (Vector2 point in route)
        {
            Assert.True(sim.HasTraversablePath(previous, point, sim.Config.Agent.Radius));
            previous = point;
        }
    }

    [Fact]
    public void SpawnPlacementNearStageNeverPlacesPeopleInsideItsFootprint()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        Assert.True(sim.Environment.AddAgentSpawnGroupFromClick(15f, 15f, 50));
        Assert.Equal(50, sim.Environment.PlannedAgentCount);
        Assert.All(sim.Environment.GetPlannedAgentPositions(), point =>
            Assert.False(sim.Environment.IsPointBlocked(point, sim.Config.Agent.Radius)));
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void AssetOnlySaveLoadAndReplayKeepZeroAddedOccupants(ScenarioType scenario)
    {
        Simulation sim = new(randomSeed: 171);
        sim.SetScenarioType(scenario);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f), MathF.PI / 2f));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(25f, 15f)));
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "fixed-assets");
        Assert.Equal(0, saved.AgentCount);
        Assert.Empty(saved.Blockages); // no duplicate serialized collision proxies
        Assert.Empty(saved.CircularBlockages);
        ScenarioDefinition loaded = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(saved))!;
        Simulation restored = new(randomSeed: 171);
        loaded.ApplyLayout(restored);
        Assert.Equal(scenario, restored.ScenarioType);
        Assert.Equal(2, restored.Environment.Furniture.Count);
        Assert.Equal(0, restored.Environment.PlannedAgentCount);
        Assert.Equal(2, restored.Environment.Walls.Count(wall => wall.IsFurniture));
        Assert.True(restored.Environment.HasStage);
        Assert.False(restored.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(40f, 15f)));
        Simulation replay = FinalizedSceneCheckpoint.Capture(restored, 0, "replay").CreateReadySimulation();
        Assert.Empty(replay.Agents);
        Assert.Equal(2, replay.Environment.Furniture.Count);
        Assert.True(replay.Environment.HasStage);
        Assert.False(replay.Running);
    }

    [Fact]
    public void DuplicateStageCannotEnterThroughScenarioLoadingOrRestore()
    {
        ScenarioDefinition invalid = new()
        {
            Furniture = new List<FurnitureScenario>
            {
                new() { Kind = FurnitureKind.Stage, X = 15f, Y = 15f },
                new() { Kind = FurnitureKind.Stage, X = 35f, Y = 15f },
            },
        };
        Assert.Throws<InvalidDataException>(() => invalid.ValidateBasic());
        Simulation sim = new();
        Assert.True(sim.Environment.RestoreFurniture(FurnitureKind.Stage, new Vector2(15f, 15f), 0f));
        Assert.False(sim.Environment.RestoreFurniture(FurnitureKind.Stage, new Vector2(35f, 15f), 0f));
        Assert.Single(sim.Environment.Furniture);
    }

    [Fact]
    public void SavedStandingAgentCannotLoadInsideStage()
    {
        ScenarioDefinition invalid = new()
        {
            AgentCount = 1,
            Furniture = new() { new() { Kind = FurnitureKind.Stage, X = 15f, Y = 15f } },
            AgentSpawnGroups = new()
            {
                new() { CenterX = 15f, CenterY = 15f,
                    Positions = new() { new() { X = 15f, Y = 15f } } },
            },
        };
        Assert.Throws<InvalidOperationException>(() => invalid.ApplyLayout(new Simulation()));
    }

    [Fact]
    public void MixedFurnitureSavePreservesElevenSeatedPeopleNotThirteen()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(25f, 15f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(38f, 20f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(45f, 20f)));
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "mixed");
        saved.ValidateBasic();
        Simulation restored = new();
        saved.ApplyLayout(restored);
        restored.SpawnAgentsAtPositions(restored.Environment.GetPlannedAgentPositions());
        Assert.Equal(11, restored.Agents.Count);
        Assert.All(restored.Agents, agent => Assert.Equal(SeatingPosture.Seated, agent.Posture));
    }

    [Fact]
    public void StageStaysFixedWhileNearbyRectangularTableIsDestroyed()
    {
        Simulation sim = new(randomSeed: 171);
        // Regression: without an exit, SetRunning(true) correctly refuses to start.
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(25f, 15f)));
        FurnitureSet stage = sim.Environment.Furniture[0];
        RectangularFurnitureBody table = Assert.IsType<RectangularFurnitureBody>(sim.Environment.Furniture[1].Table);
        Wall stageFootprint = Assert.IsType<Wall>(stage.StaticObstacle);
        Wall tableFootprint = Assert.IsType<Wall>(table.RectangleObstacle);
        int wallVersion = sim.Environment.WallLayoutVersion;
        int navigationVersion = sim.Environment.NavigationLayoutVersion;
        sim.SpawnAgentsAtPositions(new[] { new Vector2(40f, 25f) });
        sim.Agents[0].MarkExited(); // isolate asset mechanics, but keep the public step API active
        sim.SetRunning(true);
        Assert.True(sim.Running, "The regression must execute a running simulation.");
        sim.TriggerExplosion(25f, 15f);
        Assert.NotNull(sim.Environment.Explosion);
        for (int i = 0; i < 30; i++) sim.Step();
        Assert.Equal(30, sim.FrameIndex);
        Assert.Same(stageFootprint, stage.StaticObstacle);
        Assert.Contains(stageFootprint, sim.Environment.Walls);
        Assert.Null(stage.Table);
        Assert.Equal(ChairState.Destroyed, table.State);
        Assert.Null(table.RectangleObstacle);
        Assert.DoesNotContain(tableFootprint, sim.Environment.Walls);
        Assert.Equal(wallVersion, sim.Environment.WallLayoutVersion);
        Assert.True(sim.Environment.NavigationLayoutVersion > navigationVersion);
    }

    [Fact]
    public void OnlyFixedAssetsStillPreventRoomResizeUntilUndone()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.CanResizeRoom);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        Assert.False(sim.Environment.CanResizeRoom);
        Assert.False(sim.Environment.TryResizeRoom(10f, 10f));
        Assert.True(sim.Environment.UndoLast());
        Assert.True(sim.Environment.CanResizeRoom);
    }

    [Fact]
    public void ClearingTheLayoutRemovesFixedProxiesAndResetsStageLimit()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        sim.Environment.ReplaceLayout(Array.Empty<Exit>(), Array.Empty<Wall>());
        Assert.False(sim.Environment.HasStage);
        Assert.DoesNotContain(sim.Environment.Walls, wall => wall.IsFurniture);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(35f, 15f)));
    }

    [Fact]
    public void EnlargedStageCannotBePlacedInRoomThatOnlyFitsTheOldStage()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.TryResizeRoom(12f, 12f));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(6f, 6f)));
        Assert.Empty(sim.Environment.Furniture);
        Assert.False(sim.Environment.HasStage);
    }

    [Fact]
    public void OriginalFurnitureEnumValuesAndOccupancyAreUnchanged()
    {
        Assert.Equal(0, (int)FurnitureKind.RoundTableSet);
        Assert.Equal(1, (int)FurnitureKind.SingleChair);
        Assert.Equal(10, FurnitureSet.GetSeatCount(FurnitureKind.RoundTableSet));
        Assert.Equal(1, FurnitureSet.GetSeatCount(FurnitureKind.SingleChair));
    }

    [Fact]
    public void LowStageRemainsVisibleAcrossSpatialHashSightBranch()
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(15f, 15f)));
        // More than 48 walls selects the spatial-hash LOS branch instead of the simple scan.
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 7; x++)
            sim.Environment.Blockages.Add(new Wall(40f + x * 2f, 2f + y * 3f,
                40.5f + x * 2f, 2.5f + y * 3f));
        sim.Environment.RebuildWalls();
        sim.BuildWallGrid();
        Assert.True(sim.Environment.Walls.Count > 48);
        Assert.True(sim.HasLineOfSight(new Vector2(15f, 8f), new Vector2(15f, 22f)));
        Assert.False(sim.HasTraversablePath(new Vector2(15f, 8f), new Vector2(15f, 22f), sim.Config.Agent.Radius));
    }

    [Theory]
    [InlineData(FurnitureKind.RectangularTable)]
    [InlineData(FurnitureKind.Stage)]
    public void SharedExitGuidanceIncludesFixedFootprintAndReusesItsField(FurnitureKind kind)
    {
        Simulation sim = new();
        Assert.True(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f)));
        sim.BuildWallGrid();
        SharedExitFields fields = new();
        Vector2 goal = new(15f, 22f);
        Assert.True(fields.TryDirection(sim, new Vector2(15f, 10f), goal, out Vector2 direction));
        Assert.True(float.IsFinite(direction.X) && float.IsFinite(direction.Y));
        int builds = fields.FieldBuilds;
        Assert.True(fields.TryDirection(sim, new Vector2(16f, 10f), goal, out _));
        Assert.Equal(builds, fields.FieldBuilds);
    }

    [Fact]
    public void ManyRectangularTablesStillAllowBoundedGridRouting()
    {
        Simulation sim = new();
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 8; x++)
            Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable,
                new Vector2(5f + x * 5f, 5f + y * 5f)));
        sim.BuildWallGrid();
        Assert.Equal(24, sim.Environment.Furniture.Count);
        Vector2 start = new(10f, 2f);
        Vector2 goal = new(10f, 22f);
        IReadOnlyList<Vector2> route = new VisibilityGraphPlanner(sim.Config.Agent).Plan(sim, start, goal);
        Assert.NotEmpty(route);
        Vector2 previous = start;
        foreach (Vector2 point in route)
        {
            Assert.True(sim.HasTraversablePath(previous, point, sim.Config.Agent.Radius));
            previous = point;
        }
        Assert.Equal(goal, previous);
    }

}
