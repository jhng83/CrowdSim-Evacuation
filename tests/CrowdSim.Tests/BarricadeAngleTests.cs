using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Free-angle footprint, placement, collision and navigation integration.</summary>
public sealed class BarricadeAngleTests
{
    [Theory]
    [InlineData(15f)]
    [InlineData(30f)]
    [InlineData(37.5f)]
    [InlineData(45f)]
    [InlineData(60f)]
    [InlineData(120f)]
    [InlineData(225f)]
    [InlineData(359.9f)]
    [InlineData(-22.5f)]
    [InlineData(382.5f)]
    public void MetricBaseRetainsTheAuthoredAngle(float degrees)
    {
        FurnitureSet set = new(FurnitureKind.Barricade, new Vector2(15f, 15f), Radians(degrees));
        Wall wall = Assert.IsType<Wall>(set.StaticObstacle);
        Assert.NotNull(wall.OrientedFootprint);
        OrientedRectangle shape = wall.OrientedFootprint.Value;
        float expected = (degrees % 360f + 360f) % 360f;
        Assert.InRange(MathF.Abs(set.Rotation * 180f / MathF.PI - expected), 0f, 0.0001f);
        Assert.Equal(new Vector2(0.75f, 0.20f), shape.HalfExtents);
        Span<Vector2> corners = stackalloc Vector2[4];
        shape.CopyExpandedCorners(corners, 0f);
        Assert.Equal(1.5f, Vector2.Distance(corners[0], corners[1]), 4);
        Assert.Equal(0.4f, Vector2.Distance(corners[1], corners[2]), 4);
        foreach (Vector2 corner in corners)
        {
            Assert.InRange(corner.X, wall.X1 - 0.00001f, wall.X2 + 0.00001f);
            Assert.InRange(corner.Y, wall.Y1 - 0.00001f, wall.Y2 + 0.00001f);
        }
        Assert.Equal(0, FurnitureSet.GetSeatCount(FurnitureKind.Barricade));
        Assert.Null(set.Table);
    }

    [Fact]
    public void EmptyBroadPhaseCornersDoNotBecomeInvisibleObstacles()
    {
        Simulation sim = WithBarrier(45f);
        Vector2 free = new(15.6f, 14.4f);
        Assert.False(sim.Environment.IsPointBlocked(free, sim.Config.Agent.Radius));
        Assert.True(sim.HasTraversablePath(free, free + new Vector2(0.1f, -0.1f), 0.2f));
        Assert.True(EvacuationSystem.HasTraversablePath(free, free + new Vector2(0.1f, -0.1f), sim.Environment.Walls, 0.2f));
        Assert.False(sim.HasTraversablePath(new Vector2(14f, 16f), new Vector2(16f, 14f), 0.2f));
    }

    [Theory]
    [InlineData(0.1f, true)]
    [InlineData(0.15f, false)]
    public void SweptBodyClearanceUsesRoundedCornersNotAnOversizedBox(float offset, bool blocked)
    {
        OrientedRectangle shape = new(new Vector2(10f), new Vector2(0.75f, 0.2f), Radians(37.5f));
        Vector2 point = shape.ToWorld(new Vector2(0.75f + offset, 0.2f + offset));
        Assert.Equal(blocked, shape.IntersectsSegment(point, point, 0.2f));
        Assert.Equal(blocked, shape.Contains(point, 0.2f));
    }

    [Theory]
    [InlineData(22.5f)]
    [InlineData(45f)]
    [InlineData(70f)]
    public void DiagonalRowsSnapFlushAndRejectRealOverlap(float degrees)
    {
        Simulation sim = new(agentCount: 0);
        Vector2 origin = new(5f, 5f);
        OrientedRectangle shape = new(origin, new Vector2(0.75f, 0.2f), Radians(degrees));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, origin, Radians(degrees)));
        for (int i = 1; i < 8; i++)
        {
            Vector2 expected = origin + shape.AxisX * (1.5f * i);
            Vector2 requested = expected + shape.AxisY * 0.10f;
            // Reverse facing must still join the same row.
            float angle = Radians(i % 2 == 0 ? degrees + 180f : degrees);
            int version = sim.Environment.WallLayoutVersion;
            Vector2 snapped = sim.Environment.SnapBarricadePlacement(requested, angle);
            Assert.InRange(Vector2.Distance(expected, snapped), 0f, 0.00002f);
            Assert.Equal(version, sim.Environment.WallLayoutVersion);
            Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, snapped, angle));
        }
        Assert.False(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade,
            origin - shape.AxisX * 1.49f, Radians(degrees)));
        Assert.Equal(8, sim.Environment.Furniture.Count);
    }

    [Fact]
    public void DifferentAnglesDoNotSnapOrSilentlyChangeTheAngle()
    {
        Simulation sim = WithBarrier(30f);
        Vector2 requested = new Vector2(15f, 15f) + new Vector2(MathF.Cos(Radians(30f)), MathF.Sin(Radians(30f))) * 1.5f;
        Assert.Equal(requested, sim.Environment.SnapBarricadePlacement(requested, Radians(45f)));
    }

    [Fact]
    public void PositiveOverlapIsRejectedEvenWhenAnglesDiffer()
    {
        Simulation sim = WithBarrier(30f);
        Assert.False(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade, new Vector2(15f), Radians(120f)));
        Assert.True(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade, new Vector2(18f, 15f), Radians(120f)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnoccupiedCornerCanContainASmallWallOrCircle(bool circle)
    {
        Simulation sim = new(agentCount: 0);
        Vector2 corner = new(15.55f, 14.45f);
        if (circle) sim.Environment.CircularBlockages.Add(new CircleBlockage(corner, 0.08f));
        else sim.Environment.Blockages.Add(new Wall(corner.X - 0.06f, corner.Y - 0.06f,
            corner.X + 0.06f, corner.Y + 0.06f));
        sim.Environment.RebuildWalls();
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f), Radians(45f)));
    }

    [Fact]
    public void SpawnAndRestoreUseTheRealFootprint()
    {
        Simulation sim = WithBarrier(45f);
        Vector2 point = new(15.6f, 14.4f);
        sim.Environment.AgentSpawnGroups.Add(new AgentSpawnGroup(point, new[] { point }));
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 1, "free-corner");
        Simulation restored = new(agentCount: 0, config: saved.CreateConfig());
        saved.ApplyLayout(restored);
        restored.SpawnAgentsAtPositions(restored.Environment.GetPlannedAgentPositions());
        Assert.Equal(point, Assert.Single(restored.Agents).Position);
    }

    [Theory]
    [InlineData(15f)]
    [InlineData(37.5f)]
    [InlineData(135f)]
    public void CollisionPushesAlongTheRotatedSurfaceNormal(float degrees)
    {
        Simulation sim = WithBarrier(degrees);
        OrientedRectangle shape = sim.Environment.Furniture[0].StaticObstacle!.OrientedFootprint!.Value;
        sim.SpawnAgentsAtPositions(new[] { new Vector2(20f) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        agent.Position = shape.ToWorld(new Vector2(0f, 0.30f));
        agent.Velocity = -shape.AxisY * 0.05f;
        sim.ResolveSolidCollisions(agent);
        Vector2 local = shape.ToLocal(agent.Position);
        Assert.InRange(local.Y, 0.39999f, 0.401f);
        Assert.InRange(MathF.Abs(local.X), 0f, 0.0001f);
        Assert.True(Vector2.Dot(agent.Velocity, shape.AxisY) >= -0.00001f);
    }

    [Fact]
    public void FinalContainmentDoesNotTeleportAnAgentInAFreeCorner()
    {
        Simulation sim = WithBarrier(45f);
        sim.SpawnAgentsAtPositions(new[] { new Vector2(15.6f, 14.4f) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        agent.Velocity = Vector2.Zero;
        Vector2 before = agent.Position;
        sim.MoveAgent(agent);
        Assert.InRange(Vector2.Distance(before, agent.Position), 0f, 0.00001f);
    }

    [Theory]
    [InlineData(30f)]
    [InlineData(45f)]
    public void MovingAgentCannotCrossTheThinRotatedBase(float degrees)
    {
        Simulation sim = WithBarrier(degrees);
        OrientedRectangle shape = sim.Environment.Furniture[0].StaticObstacle!.OrientedFootprint!.Value;
        sim.SpawnAgentsAtPositions(new[] { shape.ToWorld(new Vector2(0f, -0.75f)) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        for (int i = 0; i < 8; i++)
        {
            agent.Velocity = shape.AxisY * 0.2f;
            sim.MoveAgent(agent);
            Assert.True(shape.ToLocal(agent.Position).Y <= -0.399f);
        }
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void SavedAnglesAndCheckpointReplayDoNotRequantize(ScenarioType scenario)
    {
        Simulation sim = WithBarrier(37.5f);
        sim.SetScenarioType(scenario);
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "angled-barrier");
        ScenarioDefinition loaded = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(saved))!;
        Simulation restored = new(agentCount: 0, config: loaded.CreateConfig());
        loaded.ApplyLayout(restored);
        Simulation replay = FinalizedSceneCheckpoint.Capture(restored, 0, "angled-barrier").CreateReadySimulation();
        Assert.Equal(Radians(37.5f), Assert.Single(replay.Environment.Furniture).Rotation, 5);
        Assert.Equal(sim.Environment.Furniture[0].StaticObstacle, replay.Environment.Furniture[0].StaticObstacle);
        Assert.False(replay.Environment.IsPointBlocked(new Vector2(15.6f, 14.4f), 0.2f));
    }

    [Fact]
    public void FineGridCanConnectFromAFreeCornerInsideTheBroadPhaseBox()
    {
        Simulation sim = WithBarrier(45f);
        Vector2 start = new(15.5f, 14.5f), goal = new(17f, 13f);
        FurnitureGridPlanner planner = new(0.25f);
        IReadOnlyList<Vector2> route = planner.Plan(sim, start, goal, 0.216f);
        Assert.NotEmpty(route);
        Assert.Equal(goal, route[^1]);
        foreach (Vector2 point in route)
        {
            Assert.True(sim.HasTraversablePath(start, point, 0.216f));
            start = point;
        }
    }

    [Theory]
    [InlineData(15f)]
    [InlineData(37.5f)]
    [InlineData(75f)]
    public void VisibilityRouteGoesAroundActualAngledCorners(float degrees)
    {
        Simulation sim = WithBarrier(degrees);
        OrientedRectangle shape = sim.Environment.Furniture[0].StaticObstacle!.OrientedFootprint!.Value;
        Vector2 start = shape.ToWorld(new Vector2(0f, -2f)), goal = shape.ToWorld(new Vector2(0f, 2f));
        IReadOnlyList<Vector2> route = new VisibilityGraphPlanner(sim.Config.Agent).Plan(sim, start, goal);
        Assert.NotEmpty(route);
        Assert.Equal(goal, route[^1]);
        foreach (Vector2 point in route)
        {
            Assert.True(sim.HasTraversablePath(start, point, sim.Config.Agent.Radius));
            start = point;
        }
    }

    [Fact]
    public void AngledGeometryStillReusesTheSharedExitField()
    {
        Simulation sim = WithBarrier(37.5f);
        SharedExitFields fields = new();
        Vector2 goal = new(20f, 10f);
        Assert.True(fields.TryDirection(sim, new Vector2(15.5f, 14.5f), goal, out _));
        Assert.True(fields.TryDirection(sim, new Vector2(16f, 13.5f), goal, out _));
        Assert.Equal(1, fields.FieldBuilds);
        Assert.Equal(1, fields.GeometryRefreshes);
    }

    [Fact]
    public void DiagonalBarrierDoesNotBecomeABlastOrFumeWall()
    {
        Simulation sim = WithBarrier(37.5f);
        Assert.True(sim.HasLineOfSight(new Vector2(14f), new Vector2(16f)));
        Assert.False(ExplosionSystem.BlastPathIsBlocked(new Vector2(14f), new Vector2(16f), sim.Environment.Walls));
        Assert.False(sim.Environment.IsFumeBlocked(new Vector2(15f), 0.2f));
    }

    [Fact]
    public void OffFloorRotatedCornersAreRejected()
    {
        Simulation sim = new(agentCount: 0);
        Assert.False(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade, new Vector2(15f, 0.75f), Radians(45f)));
        Assert.True(sim.Environment.CanPlaceFurniture(FurnitureKind.Barricade, new Vector2(15f, 1.4f), Radians(45f)));
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;

    private static Simulation WithBarrier(float degrees)
    {
        Simulation sim = new(agentCount: 0, randomSeed: 2731);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f), Radians(degrees)));
        sim.BuildWallGrid();
        return sim;
    }
}
