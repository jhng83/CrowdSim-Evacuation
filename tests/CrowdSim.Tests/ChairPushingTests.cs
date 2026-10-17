using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class ChairPushingTests
{
    private static (Simulation Sim, FurnitureChair Chair, Agent Pusher) Scene()
    {
        Simulation sim = new(randomSeed: 731);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(15f, 15f)));
        sim.SpawnAgentsAtPositions(new[] { new Vector2(14.3f, 15f) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        agent.Velocity = new Vector2(0.15f, 0f);
        return (sim, sim.Environment.Furniture[0].Chairs[0], agent);
    }

    [Theory]
    [InlineData(ChairState.Upright)]
    [InlineData(ChairState.Fallen)]
    public void LooseChairMovesAndRetainsObstacle(ChairState state)
    {
        var (sim, chair, agent) = Scene();
        chair.State = state;
        if (state == ChairState.Fallen)
        {
            sim.Environment.CircularBlockages.Remove(chair.Obstacle!);
            chair.Obstacle = chair.Obstacle! with { Radius = 0.40f };
            sim.Environment.CircularBlockages.Add(chair.Obstacle);
        }
        CircleBlockage old = chair.Obstacle!;
        Assert.True(sim.TryPushLooseChair(agent, old, Vector2.UnitX));
        Assert.True(chair.Position.X > chair.StartPosition.X);
        Assert.InRange(Vector2.Distance(chair.StartPosition, chair.Position), 0.001f, 0.1201f);
        Assert.Equal(chair.Position, chair.Obstacle!.Center);
        Assert.Contains(chair.Obstacle, sim.Environment.CircularBlockages);
        Assert.DoesNotContain(old, sim.Environment.CircularBlockages);
    }

    [Fact]
    public void InjuredWalkerPushesGentlyEvenWithoutMomentum()
    {
        var (sim, chair, agent) = Scene();
        agent.State = AgentState.Injured;
        agent.InjurySpeedFactor = 0.25f;
        agent.Velocity = Vector2.Zero;
        Assert.True(sim.TryPushChairTowardGoal(agent, Vector2.UnitX));
        float displacement = Vector2.Distance(chair.StartPosition, chair.Position);
        Assert.InRange(displacement, 0.001f, 0.301f * sim.Config.Agent.Dt);
        Assert.Equal(AgentState.Injured, agent.State);
        Assert.Equal(0.25f, agent.InjurySpeedFactor);
    }

    [Fact]
    public void InjuredWalkerCanClearChairDuringFailedRouteCooldown()
    {
        var (sim, chair, agent) = Scene();
        agent.State = AgentState.Injured;
        agent.Velocity = Vector2.Zero;
        agent.FailedRouteKey = "blocked";
        agent.FailedRouteRetryFrame = sim.FrameIndex + 10;
        Vector2 direction = sim.EvacuationSystem.ResolveStaticRouteDirection(
            sim, agent, new Vector2(20f, 15f), "blocked");
        Assert.True(chair.Position.X > chair.StartPosition.X);
        Assert.True(direction.X > 0.9f);
        Assert.Equal(0, sim.LastRoutePlanRequests);
    }

    [Fact]
    public void MovementPipelinePushesBeforeHardCollision()
    {
        var (sim, chair, agent) = Scene();
        sim.MoveAgent(agent);
        Assert.True(chair.Position.X > chair.StartPosition.X);
        Assert.True(Vector2.Distance(agent.Position, chair.Position) >=
            sim.Config.Agent.Radius + chair.Obstacle!.Radius - 0.001f);
    }

    [Fact]
    public void WaitingAgentCanShiftChairWithoutRunningMomentum()
    {
        var (sim, chair, agent) = Scene();
        agent.Velocity = Vector2.Zero;
        agent.LocalNavigationMode = LocalNavigationMode.WaitingForGap;
        Assert.True(sim.TryPushChairTowardGoal(agent, Vector2.UnitX));
        Assert.True(chair.Position.X > chair.StartPosition.X);
    }

    [Fact]
    public void MultiplePushesInOneTickCannotMultiplyChairSpeed()
    {
        var (sim, chair, agent) = Scene();
        Assert.True(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
        Vector2 position = chair.Position;
        Assert.False(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
        Assert.Equal(position, chair.Position);
    }

    [Fact]
    public void OccupiedChairCannotBePushed()
    {
        var (sim, chair, agent) = Scene();
        chair.OccupantId = 123;
        Assert.False(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
        Assert.Equal(chair.StartPosition, chair.Position);
    }

    [Theory]
    [InlineData(ChairState.Airborne)]
    [InlineData(ChairState.Destroyed)]
    public void AirborneOrDestroyedChairCannotBePushed(ChairState state)
    {
        var (sim, chair, agent) = Scene();
        chair.State = state;
        Assert.False(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
    }

    [Fact]
    public void TrappedChairCannotPassThroughWallOrFurniture()
    {
        var (sim, chair, agent) = Scene();
        // Forward wall and two lateral table-size footprints block all five push directions.
        sim.Environment.Walls.Add(new Wall(15.30f, 13f, 15.5f, 17f));
        sim.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(15f, 16.2f), 0.90f) { IsFurniture = true });
        sim.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(15f, 13.8f), 0.90f) { IsFurniture = true });
        Assert.False(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
        Assert.Equal(chair.StartPosition, chair.Position);
    }

    [Fact]
    public void ChairShiftsSidewaysWithoutEnteringCrawler()
    {
        var (sim, chair, _) = Scene();
        float gap = chair.Obstacle!.Radius + sim.Config.Agent.Radius * 1.25f;
        sim.SpawnAgentsAtPositions(new[] { new Vector2(14.3f, 15f), new Vector2(15f + gap, 15f) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        Agent crawler = sim.Agents[1];
        crawler.State = AgentState.Crawling;
        Assert.True(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
        Assert.True(Vector2.Distance(chair.Position, crawler.Position) >= gap - 0.0001f);
        Assert.NotEqual(15f, chair.Position.Y);
    }

    [Fact]
    public void CrawlingPersonCannotShoveFurnitureWithRunningMomentum()
    {
        var (sim, chair, agent) = Scene();
        agent.State = AgentState.Crawling;
        Assert.False(sim.TryPushLooseChair(agent, chair.Obstacle!, Vector2.UnitX));
    }
}
