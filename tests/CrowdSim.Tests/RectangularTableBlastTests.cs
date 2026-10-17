using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Exercise live rectangular furniture through the public fixed-step pipeline.</summary>
public sealed class RectangularTableBlastTests
{
    private static (Simulation Sim, RectangularFurnitureBody Table) Scene(
        float angle = 0f, Vector2? center = null, SimulationConfig? config = null)
    {
        Simulation sim = new(config: config, randomSeed: 731);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable,
            center ?? new Vector2(15f, 15f), angle));
        // The asset is unoccupied. An unrelated exited agent keeps the public timestep
        // active without random person contacts affecting these mechanics tests.
        sim.SpawnAgentsAtPositions(new[] { new Vector2(40f, 25f) });
        sim.Agents[0].MarkExited();
        sim.SetRunning(true);
        Assert.True(sim.Running);
        return (sim, Assert.IsType<RectangularFurnitureBody>(sim.Environment.Furniture[0].Table));
    }

    private static void Advance(Simulation sim, int frames = 120)
    {
        int start = sim.FrameIndex;
        Assert.True(sim.Running);
        for (int i = 0; i < frames; i++) sim.Step();
        Assert.Equal(start + frames, sim.FrameIndex);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    public void NearBlastMovesTableThenLeavesAnExactSettledRectangle(float degrees)
    {
        var (sim, table) = Scene(degrees * MathF.PI / 180f);
        Wall original = Assert.IsType<Wall>(table.RectangleObstacle);
        int staticVersion = sim.Environment.WallLayoutVersion;
        sim.TriggerExplosion(11f, 15f);
        Assert.NotNull(sim.Environment.Explosion);
        bool sawFlight = false;
        for (int i = 0; i < 120; i++)
        {
            sim.Step();
            sawFlight |= table.State == ChairState.Airborne && table.Height > 0f;
        }
        Assert.Equal(120, sim.FrameIndex);
        Assert.True(sawFlight);
        Assert.True(table.Position.X > table.StartPosition.X + 0.1f);
        Assert.Equal(ChairState.Fallen, table.State);
        Assert.Equal(0f, table.Height);
        Assert.Equal(Vector2.Zero, table.Velocity);
        Assert.Equal(table.StartHeading, table.Heading);
        Wall landed = Assert.IsType<Wall>(table.RectangleObstacle);
        Assert.Equal(table.Position.X - table.HalfExtents.X, landed.X1, 4);
        Assert.Equal(table.Position.Y - table.HalfExtents.Y, landed.Y1, 4);
        Assert.Equal(table.HalfExtents.X * 2f, landed.X2 - landed.X1, 4);
        Assert.Equal(table.HalfExtents.Y * 2f, landed.Y2 - landed.Y1, 4);
        Assert.DoesNotContain(original, sim.Environment.Walls);
        Assert.Contains(landed, sim.Environment.Walls);
        Assert.Empty(sim.Environment.CircularBlockages);
        Assert.Null(table.Obstacle);
        Assert.Equal(staticVersion, sim.Environment.WallLayoutVersion);
        Vector2 position = table.Position;
        int navigationVersion = sim.Environment.NavigationLayoutVersion;
        Advance(sim);
        Assert.Equal(position, table.Position);
        Assert.Equal(navigationVersion, sim.Environment.NavigationLayoutVersion);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    public void CentralBlastRemovesTheTableAndAllItsNavigationGeometry(float degrees)
    {
        var (sim, table) = Scene(degrees * MathF.PI / 180f);
        Wall original = Assert.IsType<Wall>(table.RectangleObstacle);
        sim.TriggerExplosion(15f, 15f);
        Advance(sim, 10);
        Assert.True(table.BlastApplied);
        Assert.Equal(ChairState.Destroyed, table.State);
        Assert.Null(table.RectangleObstacle);
        Assert.Null(sim.Environment.Furniture[0].StaticObstacle);
        Assert.DoesNotContain(original, sim.Environment.Walls);
        Assert.Empty(sim.Environment.CircularBlockages);
        Assert.False(sim.Environment.IsPointBlocked(table.StartPosition, sim.Config.Agent.Radius));
        Assert.True(sim.HasTraversablePath(new Vector2(15f, 12f), new Vector2(15f, 18f), sim.Config.Agent.Radius));
    }

    [Fact]
    public void DestructionUsesTheConfiguredFurnitureZoneNotAHardCodedDistance()
    {
        SimulationConfig config = new();
        config.Agent.SetBlastZoneRadii(1f, 4f, 5f, 6f, 10f);
        var (sim, table) = Scene(config: config);
        sim.TriggerExplosion(11f, 15f); // exactly the configured inner furniture boundary
        Advance(sim, 15);
        Assert.Equal(ChairState.Destroyed, table.State);
        Assert.Null(table.RectangleObstacle);
    }

    [Fact]
    public void ABlastBeyondTheOuterZoneLeavesTheTableIntact()
    {
        var (sim, table) = Scene();
        sim.TriggerExplosion(2f, 2f);
        Advance(sim);
        Assert.Equal(ChairState.Upright, table.State);
        Assert.Equal(table.StartPosition, table.Position);
        Assert.NotNull(table.RectangleObstacle);
    }

    [Theory]
    [InlineData(11f)]
    [InlineData(12.5f)]
    public void FullHeightWallShieldsAgainstImpulseAndDestruction(float blastX)
    {
        var (sim, table) = Scene();
        sim.Environment.Walls.Add(new Wall(13f, 10f, 13.2f, 20f));
        sim.BuildWallGrid();
        sim.TriggerExplosion(blastX, 15f);
        Advance(sim);
        Assert.True(table.BlastApplied);
        Assert.Equal(ChairState.Upright, table.State);
        Assert.Equal(table.StartPosition, table.Position);
    }

    [Fact]
    public void SweptRectangleStopsBeforeItsLongEdgePassesThroughWall()
    {
        var (sim, table) = Scene();
        sim.Environment.Walls.Add(new Wall(17f, 10f, 17.2f, 20f));
        sim.BuildWallGrid();
        sim.TriggerExplosion(11f, 15f);
        for (int i = 0; i < 120; i++)
        {
            sim.Step();
            Assert.True(table.Position.X + table.HalfExtents.X <= 17.001f);
        }
        Assert.True(table.Position.X > table.StartPosition.X);
        Assert.Equal(ChairState.Fallen, table.State);
    }

    [Fact]
    public void ShortSideCanSlideAlongBoundaryWithoutAnOversizedCircularCollider()
    {
        var (sim, table) = Scene(center: new Vector2(10f, 1.25f));
        sim.TriggerExplosion(6f, 1.25f);
        Advance(sim);
        Assert.True(table.Position.X > 10.1f);
        Assert.Equal(1.25f, table.Position.Y, 4);
        Assert.True(table.RectangleObstacle!.Y1 >= sim.Environment.BoundaryThickness);
    }

    [Fact]
    public void TableDoesNotSlideThroughTheFixedFifteenBySixStage()
    {
        var (sim, table) = Scene(center: new Vector2(12f, 15f));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(22f, 15f)));
        Wall stage = Assert.IsType<Wall>(sim.Environment.Furniture[1].StaticObstacle);
        sim.BuildWallGrid();
        sim.TriggerExplosion(8f, 15f);
        for (int i = 0; i < 120; i++)
        {
            sim.Step();
            Assert.True(table.Position.X + table.HalfExtents.X <= stage.X1 + 0.001f);
        }
        Assert.Equal(15f, stage.X2 - stage.X1, 4);
        Assert.Equal(6f, stage.Y2 - stage.Y1, 4);
        Assert.Same(stage, sim.Environment.Furniture[1].StaticObstacle);
        Assert.True(table.Position.X > table.StartPosition.X);
    }

    [Fact]
    public void ExactContactsDoNotHitPeopleOrChairsInClearSpaceBesideTheTable()
    {
        RectangularFurnitureBody table = new(new Vector2(15f, 15f), 0f, new Vector2(1.5f, 0.5f));
        FurnitureChair chair = new(new Vector2(15f, 16f), 0f);
        Assert.False(Simulation.FurnitureOverlapsDisc(table, table.Position, chair.Position, 0.2f));
        Assert.False(Simulation.FurnitureFootprintsOverlap(table, table.Position, chair, chair.Position));
        Assert.False(Simulation.FurnitureFootprintsOverlap(chair, chair.Position, table, table.Position));
        Assert.True(Simulation.FurnitureOverlapsDisc(table, table.Position, new Vector2(15f, 15.6f), 0.2f));
    }

    [Fact]
    public void LongTablesTransferMotionWithoutMissingContactsAcrossGridCells()
    {
        var (sim, table) = Scene(center: new Vector2(10f, 10f));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(14.5f, 10f)));
        RectangularFurnitureBody target = Assert.IsType<RectangularFurnitureBody>(sim.Environment.Furniture[1].Table);
        sim.BuildWallGrid();
        table.BlastApplied = target.BlastApplied = true;
        table.State = ChairState.Airborne;
        table.Height = 0.05f;
        table.Velocity = new Vector2(12f, 0f);
        for (int i = 0; i < 20; i++)
        {
            sim.Step();
            Assert.False(Simulation.FurnitureFootprintsOverlap(table, table.Position, target, target.Position));
        }
        Assert.True(target.Position.X > target.StartPosition.X);
        Assert.True(table.Position.X < target.Position.X);
    }

    [Fact]
    public void DestructionRemovesTheOldFootprintFromTheSpatialHashToo()
    {
        var (sim, table) = Scene();
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 7; x++)
            sim.Environment.Walls.Add(new Wall(40f + x * 2f, 2f + y * 3f,
                40.5f + x * 2f, 2.5f + y * 3f));
        sim.BuildWallGrid();
        Assert.True(sim.Environment.Walls.Count > 48);
        Vector2 start = new(15f, 12f), goal = new(15f, 18f);
        Assert.False(sim.HasTraversablePath(start, goal, sim.Config.Agent.Radius));
        sim.TriggerExplosion(15f, 15f);
        Advance(sim, 10);
        Assert.True(sim.HasTraversablePath(start, goal, sim.Config.Agent.Radius));
    }

    [Fact]
    public void MovingTableUpdatesSpatialHashWithoutLeavingItsOldFootprintBehind()
    {
        var (sim, table) = Scene();
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 7; x++)
            sim.Environment.Walls.Add(new Wall(40f + x * 2f, 2f + y * 3f,
                40.5f + x * 2f, 2.5f + y * 3f));
        sim.BuildWallGrid();
        Wall original = Assert.IsType<Wall>(table.RectangleObstacle);
        Vector2 originalLaneStart = new(original.X1 + 0.25f, 12f);
        Vector2 originalLaneEnd = new(originalLaneStart.X, 18f);
        Assert.False(sim.HasTraversablePath(originalLaneStart, originalLaneEnd, sim.Config.Agent.Radius));
        sim.TriggerExplosion(11f, 15f);
        Advance(sim);
        Assert.True(table.Position.X > table.StartPosition.X + 0.5f);
        Assert.True(sim.HasTraversablePath(originalLaneStart, originalLaneEnd, sim.Config.Agent.Radius));
        Assert.False(sim.HasTraversablePath(table.Position - Vector2.UnitY * 3f,
            table.Position + Vector2.UnitY * 3f, sim.Config.Agent.Radius));
        Assert.DoesNotContain(original, sim.Environment.Walls);
    }

    [Fact]
    public void SharedFieldClearsDestroyedTableCellsWithoutInvalidatingStaticWalls()
    {
        var (sim, table) = Scene();
        SharedExitFields fields = new();
        Vector2 goal = new(15f, 22f);
        Assert.True(fields.TryDirection(sim, new Vector2(15f, 10f), goal, out _));
        Assert.False(fields.TryDirection(sim, table.Position, goal, out _));
        int wallVersion = sim.Environment.WallLayoutVersion;
        int oldBuilds = fields.FieldBuilds;
        sim.TriggerExplosion(15f, 15f);
        Advance(sim, 10); // also pass the bounded shared-field refresh interval
        Assert.True(fields.TryDirection(sim, table.StartPosition, goal, out _));
        Assert.True(fields.FieldBuilds > oldBuilds);
        Assert.Equal(wallVersion, sim.Environment.WallLayoutVersion);
    }

    [Fact]
    public void ToxicFumeReleaseNeitherLaunchesNorDestroysTheUnoccupiedTable()
    {
        var (sim, table) = Scene();
        sim.SetRunning(false);
        sim.SetScenarioType(ScenarioType.ToxicFume);
        Assert.True(sim.ToxicFumeSystem.TrySetSource(sim, new Vector2(12f, 12f)));
        sim.PrecomputeToxicFume();
        sim.SetRunning(true);
        Assert.True(sim.Running);
        Assert.True(sim.TriggerToxicFumeRelease());
        Wall original = Assert.IsType<Wall>(table.RectangleObstacle);
        Advance(sim, 10);
        Assert.Equal(ChairState.Upright, table.State);
        Assert.Equal(table.StartPosition, table.Position);
        Assert.Same(original, table.RectangleObstacle);
        Assert.True(sim.ToxicFumeSystem.IsCacheValidFor(sim));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveAndReplayRestoreAuthoredTableNotDisplacedOrDestroyedState(bool destroy)
    {
        var (sim, table) = Scene();
        sim.TriggerExplosion(destroy ? 15f : 11f, 15f);
        Advance(sim);
        Assert.NotEqual(ChairState.Upright, table.State);
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "rectangular-table-replay");
        Assert.Empty(saved.Blockages);
        Assert.Empty(saved.CircularBlockages);
        ScenarioDefinition copy = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(saved))!;
        Simulation replay = new(randomSeed: 731);
        copy.ApplyLayout(replay);
        RectangularFurnitureBody restored = Assert.IsType<RectangularFurnitureBody>(replay.Environment.Furniture[0].Table);
        Assert.Equal(table.StartPosition, restored.Position);
        Assert.Equal(ChairState.Upright, restored.State);
        Assert.NotNull(restored.RectangleObstacle);
        Assert.Empty(replay.Environment.GetPlannedAgentPositions());
        Assert.Single(replay.Environment.Walls.Where(wall => wall.IsMovableFurniture));
    }

    [Fact]
    public void SameSeedAndLayoutProduceTheSameRectangularTableTrajectory()
    {
        var first = Scene();
        var second = Scene();
        first.Sim.TriggerExplosion(11f, 15f);
        second.Sim.TriggerExplosion(11f, 15f);
        for (int i = 0; i < 90; i++)
        {
            first.Sim.Step();
            second.Sim.Step();
            Assert.Equal(first.Table.Position, second.Table.Position);
            Assert.Equal(first.Table.Height, second.Table.Height);
            Assert.Equal(first.Table.State, second.Table.State);
        }
    }
}
