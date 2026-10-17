using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class FurnitureImpactTests
{
    [Fact]
    public void EqualMassImpactTransfersMomentumAndLosesEnergy()
    {
        FurnitureBody first = new FurnitureChair(Vector2.Zero, 0f) { Velocity = new Vector2(4f, 0f) };
        FurnitureBody second = new FurnitureChair(Vector2.UnitX, 0f);
        Simulation.ApplyFurnitureContactImpulse(first, second, Vector2.UnitX, false);
        Assert.InRange(first.Velocity.X, 1.69f, 1.71f);
        Assert.InRange(second.Velocity.X, 2.29f, 2.31f);
        Assert.InRange(first.Velocity.X + second.Velocity.X, 3.999f, 4.001f);
        Assert.True(first.Velocity.LengthSquared() + second.Velocity.LengthSquared() < 16f);
    }

    [Fact]
    public void ChairLosesMostSpeedAgainstHeavierTable()
    {
        FurnitureBody chair = new FurnitureChair(Vector2.Zero, 0f) { Velocity = new Vector2(4f, 0f) };
        FurnitureBody table = new(Vector2.UnitX, 0f, FurnitureSet.TableRadius);
        Simulation.ApplyFurnitureContactImpulse(chair, table, Vector2.UnitX, false);
        Assert.True(chair.Velocity.X < 0.1f);
        Assert.InRange(table.Velocity.X, 0.6f, 0.7f);
        Assert.InRange(chair.Velocity.X + table.Velocity.X * 6f, 3.999f, 4.001f);
    }

    [Fact]
    public void PinnedTargetDoesNotGainVelocity()
    {
        FurnitureBody chair = new FurnitureChair(Vector2.Zero, 0f) { Velocity = new Vector2(4f, 0f) };
        FurnitureBody table = new(Vector2.UnitX, 0f, FurnitureSet.TableRadius);
        Simulation.ApplyFurnitureContactImpulse(chair, table, Vector2.UnitX, true);
        Assert.Equal(Vector2.Zero, table.Velocity);
        Assert.True(chair.Velocity.X < 0f);
    }

    private static (Simulation Sim, FurnitureChair Moving, FurnitureChair Target) Scene(float height)
    {
        Simulation sim = new(randomSeed: 731);
        // SetRunning deliberately rejects a scene without an exit.
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(10f, 10f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(13f, 10f)));
        // A distant exited agent keeps the public step API active without incidental person impacts.
        sim.SpawnAgentsAtPositions(new[] { new Vector2(20f, 20f) });
        sim.Agents[0].MarkExited();
        FurnitureChair moving = sim.Environment.Furniture[0].Chairs[0];
        FurnitureChair target = sim.Environment.Furniture[1].Chairs[0];
        moving.State = ChairState.Airborne;
        moving.Height = height;
        moving.Velocity = new Vector2(12f, 0f);
        moving.BlastApplied = true;
        target.BlastApplied = true;
        sim.SetRunning(true);
        Assert.True(sim.Running, "Impact-test scene must be running before testing furniture motion.");
        return (sim, moving, target);
    }

    private static void Advance(Simulation sim, int frames)
    {
        int initialFrame = sim.FrameIndex;
        Assert.True(sim.Running);
        for (int i = 0; i < frames; i++) sim.Step();
        Assert.Equal(initialFrame + frames, sim.FrameIndex);
    }

    [Fact]
    public void LowFlightHitsChairAndTransfersMovementThroughStepPipeline()
    {
        var (sim, moving, target) = Scene(0.1f);
        Advance(sim, 20);
        Assert.True(target.Position.X > target.StartPosition.X);
        Assert.True(moving.Position.X < target.Position.X);
        Assert.True(Vector2.Distance(moving.Position, target.Position) >=
            Simulation.FurnitureCollisionRadius(moving) + Simulation.FurnitureCollisionRadius(target) - 0.01f);
        Assert.Contains(target.Obstacle!, sim.Environment.CircularBlockages);
    }

    [Fact]
    public void FurniturePinnedAgainstWallStopsIncomingMotion()
    {
        var (sim, moving, target) = Scene(0.1f);
        sim.Environment.Walls.Add(new Wall(13.30f, 8f, 13.5f, 12f));
        sim.BuildWallGrid();
        Advance(sim, 20);
        Assert.Equal(target.StartPosition, target.Position);
        Assert.True(moving.Position.X < target.Position.X);
        Assert.Equal(Vector2.Zero, target.Velocity);
    }

    [Fact]
    public void HighFlightCanPassOverChairWithoutHorizontalImpact()
    {
        var (sim, moving, target) = Scene(3f);
        Advance(sim, 3);
        Assert.True(moving.Position.X > target.Position.X);
        Assert.Equal(target.StartPosition, target.Position);
        Assert.Equal(Vector2.Zero, target.Velocity);
    }

    [Fact]
    public void DescendingBodyLandsOnFurnitureAndFallsWhenSupportIsRemoved()
    {
        var (sim, moving, target) = Scene(2f);
        moving.Position = target.Position;
        moving.Velocity = Vector2.Zero;
        Advance(sim, 15);
        Assert.Equal(ChairState.Fallen, moving.State);
        Assert.Same(target, moving.SupportBody);
        Assert.True(moving.Height >= Simulation.FurnitureBodyHeight(target));
        target.State = ChairState.Destroyed;
        sim.Environment.CircularBlockages.Remove(target.Obstacle!);
        target.Obstacle = null;
        Advance(sim, 15);
        Assert.Null(moving.SupportBody);
        Assert.Equal(0f, moving.Height);
    }
}
