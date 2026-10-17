using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Blast furniture lifecycle checks through the public simulation API.</summary>
public sealed class FurnitureBlastTests
{
    private static (Simulation Sim, FurnitureBody Body) Scene(FurnitureKind kind)
    {
        Simulation sim = new(randomSeed: 731);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f)));
        sim.SpawnAgentsAtPositions(sim.Environment.GetPlannedAgentPositions());
        // Keep the timestep active but isolate furniture mechanics from casualty randomness.
        foreach (Agent agent in sim.Agents) agent.MarkExited();
        FurnitureSet set = sim.Environment.Furniture[0];
        sim.SetRunning(true);
        return (sim, set.Table ?? set.Chairs[0]);
    }

    private static void Advance(Simulation sim, int frames = 120)
    {
        for (int i = 0; i < frames; i++) sim.Step();
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet)]
    [InlineData(FurnitureKind.SingleChair)]
    public void CenterBlastRemovesBodyAndCollisionProxy(FurnitureKind kind)
    {
        var (sim, body) = Scene(kind);
        CircleBlockage original = body.Obstacle!;
        sim.TriggerExplosion(15f, 15f);
        Advance(sim);
        Assert.Equal(ChairState.Destroyed, body.State);
        Assert.Null(body.Obstacle);
        Assert.DoesNotContain(original, sim.Environment.CircularBlockages);
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet)]
    [InlineData(FurnitureKind.SingleChair)]
    public void NearBlastFliesOutwardThenLandsAsPersistentObstacle(FurnitureKind kind)
    {
        var (sim, body) = Scene(kind);
        CircleBlockage original = body.Obstacle!;
        sim.TriggerExplosion(11f, 15f);
        bool airborne = false;
        for (int i = 0; i < 120; i++)
        {
            sim.Step();
            airborne |= body.State == ChairState.Airborne && body.Height > 0f;
        }
        Assert.True(airborne);
        Assert.True(body.Position.X > body.StartPosition.X + 0.1f);
        Assert.Equal(ChairState.Fallen, body.State);
        Assert.Equal(0f, body.Height);
        Assert.Equal(Vector2.Zero, body.Velocity);
        Assert.Equal(body.Position, body.Obstacle!.Center);
        Assert.DoesNotContain(original, sim.Environment.CircularBlockages);
        Assert.Contains(body.Obstacle, sim.Environment.CircularBlockages);
        Assert.False(sim.HasTraversablePath(body.Position - Vector2.UnitX * 2f,
            body.Position + Vector2.UnitX * 2f, sim.Config.Agent.Radius));
        Vector2 landed = body.Position;
        int version = sim.Environment.NavigationLayoutVersion;
        Advance(sim);
        Assert.Equal(landed, body.Position);
        Assert.Equal(version, sim.Environment.NavigationLayoutVersion);
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet)]
    [InlineData(FurnitureKind.SingleChair)]
    public void WallShieldsFurnitureFromImpulse(FurnitureKind kind)
    {
        var (sim, body) = Scene(kind);
        sim.Environment.Walls.Add(new Wall(13f, 10f, 13.2f, 20f));
        sim.BuildWallGrid();
        sim.TriggerExplosion(11f, 15f);
        Advance(sim);
        Assert.Equal(ChairState.Upright, body.State);
        Assert.Equal(body.StartPosition, body.Position);
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet)]
    [InlineData(FurnitureKind.SingleChair)]
    public void SweptFlightStopsBeforeWall(FurnitureKind kind)
    {
        var (sim, body) = Scene(kind);
        sim.Environment.Walls.Add(new Wall(16.5f, 10f, 16.7f, 20f));
        sim.BuildWallGrid();
        sim.TriggerExplosion(11f, 15f);
        Advance(sim);
        Assert.Equal(ChairState.Fallen, body.State);
        Assert.True(body.Position.X + body.Obstacle!.Radius <= 16.501f);
        Assert.True(body.Position.X > body.StartPosition.X);
    }

    [Fact]
    public void DistantBlastLeavesTableIntact()
    {
        var (sim, table) = Scene(FurnitureKind.RoundTableSet);
        sim.TriggerExplosion(2f, 2f);
        Advance(sim);
        Assert.Equal(ChairState.Upright, table.State);
        Assert.Equal(table.StartPosition, table.Position);
    }

    [Fact]
    public void SaveAfterTableDestructionRestoresAuthoredTableOnReplay()
    {
        var (sim, table) = Scene(FurnitureKind.RoundTableSet);
        sim.TriggerExplosion(15f, 15f);
        Advance(sim);
        Assert.Null(table.Obstacle);
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "Table replay");
        Simulation replay = new(randomSeed: 731);
        saved.ApplyLayout(replay);
        FurnitureBody restored = replay.Environment.Furniture[0].Table!;
        Assert.Equal(new Vector2(15f, 15f), restored.Position);
        Assert.Equal(ChairState.Upright, restored.State);
        Assert.Contains(restored.Obstacle!, replay.Environment.CircularBlockages);
    }

    [Fact]
    public void ToxicModeDoesNotLaunchOrDestroyTable()
    {
        var (sim, table) = Scene(FurnitureKind.RoundTableSet);
        sim.SetRunning(false);
        sim.SetScenarioType(ScenarioType.ToxicFume);
        Assert.True(sim.ToxicFumeSystem.TrySetSource(sim, new Vector2(12f, 12f)));
        sim.PrecomputeToxicFume();
        sim.SetRunning(true);
        Assert.True(sim.TriggerToxicFumeRelease());
        Advance(sim, 10);
        Assert.Equal(ChairState.Upright, table.State);
        Assert.Equal(table.StartPosition, table.Position);
        Assert.Contains(table.Obstacle!, sim.Environment.CircularBlockages);
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet)]
    [InlineData(FurnitureKind.SingleChair)]
    public void ConfiguredIncapacitationBoundaryDestroysFurniture(FurnitureKind kind)
    {
        var (sim, body) = Scene(kind);
        sim.Config.Agent.SetBlastZoneRadii(2f, 5f, 7f, 10f, 18f);
        sim.TriggerExplosion(10f, 15f); // exactly the configured incap boundary
        Advance(sim);
        Assert.Equal(ChairState.Destroyed, body.State);
        Assert.Null(body.Obstacle);
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet)]
    [InlineData(FurnitureKind.SingleChair)]
    public void CloserSurvivingFurnitureTravelsFarther(FurnitureKind kind)
    {
        var near = Scene(kind);
        var far = Scene(kind);
        // Compare unobstructed travel; impacts can legitimately shorten the nearer flight.
        if (kind == FurnitureKind.RoundTableSet)
        {
            foreach (Simulation scene in new[] { near.Sim, far.Sim })
            {
                foreach (FurnitureChair chair in scene.Environment.Furniture[0].Chairs)
                    scene.Environment.CircularBlockages.Remove(chair.Obstacle!);
                scene.Environment.Furniture[0].Chairs.Clear();
            }
        }
        near.Sim.TriggerExplosion(11f, 15f); // 4 metres
        far.Sim.TriggerExplosion(3f, 15f); // 12 metres: outside the old launch radius
        Advance(near.Sim);
        Advance(far.Sim);
        float nearTravel = Vector2.Distance(near.Body.StartPosition, near.Body.Position);
        float farTravel = Vector2.Distance(far.Body.StartPosition, far.Body.Position);
        Assert.True(farTravel > 0f);
        Assert.True(nearTravel > farTravel);
    }

    [Fact]
    public void SameSeedProducesIdenticalFurnitureLanding()
    {
        var first = Scene(FurnitureKind.RoundTableSet);
        var second = Scene(FurnitureKind.RoundTableSet);
        first.Sim.TriggerExplosion(11f, 15f);
        second.Sim.TriggerExplosion(11f, 15f);
        Advance(first.Sim);
        Advance(second.Sim);
        Assert.Equal(first.Body.Position, second.Body.Position);
        Assert.Equal(first.Body.State, second.Body.State);
    }
}
