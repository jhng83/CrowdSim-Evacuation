using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Regression checks for adaptive movement and shared navigation boundaries.</summary>
public sealed class HybridMovementTests
{
    private static Simulation Scene()
    {
        Simulation sim = new(randomSeed: 731);
        sim.Config.HybridEnabled = true;
        Assert.True(sim.Environment.AddExitFromClick(0.3f, 15f, 5));
        sim.SpawnAgentsAtPositions(new[] { new Vector2(15.2f, 15.3f), new Vector2(16.4f, 15.3f) });
        foreach (Agent agent in sim.Agents)
        {
            agent.State = AgentState.Evacuating;
            agent.Posture = SeatingPosture.Walking;
            agent.Intent = BehaviorIntent.Evacuate;
            agent.ExitTarget = sim.Environment.Exits[0];
        }
        return sim;
    }

    [Fact]
    public void SharedFieldReusesOneBuildAndPointsTowardExit()
    {
        Simulation sim = Scene();
        var fields = sim.HybridMovement.Fields;
        Vector2 goal = sim.Environment.Exits[0].Position;
        Assert.True(fields.TryDirection(sim, new Vector2(20f, 15f), goal, out Vector2 direction));
        Assert.True(direction.X < 0f);
        Assert.True(fields.TryDirection(sim, new Vector2(23f, 15f), goal, out _));
        Assert.Equal(1, fields.FieldBuilds);
        Assert.Equal(1, fields.GeometryRefreshes);
    }

    [Fact]
    public void NewlyBlockedSampleCannotBeUsedEvenBeforeFieldRefresh()
    {
        Simulation sim = Scene();
        Vector2 position = new(20f, 15f);
        Vector2 goal = sim.Environment.Exits[0].Position;
        Assert.True(sim.HybridMovement.Fields.TryDirection(sim, position, goal, out _));
        // Simulate a newly arrived obstacle without updating the cached raster.
        sim.Environment.CircularBlockages.Add(new CircleBlockage(position, 2f));
        Assert.False(sim.HybridMovement.Fields.TryDirection(sim, position, goal, out _));
    }

    [Fact]
    public void OpenMovementPreservesIndividualBodiesAndSpeedLimit()
    {
        Simulation sim = Scene();
        Vector2 before = sim.Agents[0].Position;
        Vector2 otherBefore = sim.Agents[1].Position;
        sim.HybridMovement.BeginFrame(sim);
        Assert.True(sim.HybridMovement.TryAdvance(sim, sim.Agents[0]));
        Assert.True(sim.Agents[0].Position.X < before.X);
        Assert.Equal(otherBefore, sim.Agents[1].Position);
        Assert.InRange(sim.Agents[0].Velocity.Length(), 0f, sim.Agents[0].CurrentDesiredSpeed + 0.00001f);
    }

    [Theory]
    [InlineData(AgentState.Injured)]
    [InlineData(AgentState.Crawling)]
    [InlineData(AgentState.Dead)]
    public void InjuryOrCasualtyRequiresDetailedTreatment(AgentState state)
    {
        Simulation sim = Scene();
        sim.Agents[0].State = state;
        Vector2 before = sim.Agents[0].Position;
        sim.HybridMovement.BeginFrame(sim);
        Assert.False(sim.HybridMovement.TryAdvance(sim, sim.Agents[0]));
        Assert.Equal(before, sim.Agents[0].Position);
        Assert.Equal(-1, sim.Agents[0].HybridGroupLeaderId);
    }

    [Fact]
    public void ObstacleForcesDetailedMovementWithoutTeleporting()
    {
        Simulation sim = Scene();
        Agent agent = sim.Agents[0];
        sim.Environment.CircularBlockages.Add(new CircleBlockage(agent.Position + Vector2.UnitX, 0.3f));
        sim.HybridMovement.BeginFrame(sim);
        Vector2 before = agent.Position;
        Assert.False(sim.HybridMovement.TryAdvance(sim, agent));
        Assert.Equal(before, agent.Position);
        Assert.Equal(-1, agent.HybridGroupLeaderId);
    }

    [Fact]
    public void GroupsAreLimitedToFiveAndSplitWhenMemberIsInjured()
    {
        Simulation sim = Scene();
        sim.Agents.Clear();
        for (int id = 0; id < 6; id++)
            sim.Agents.Add(new Agent(id, new Vector2(15.1f + id * 0.3f, 15.3f),
                Vector2.Zero, 0.15f, 1f, 0f, DecisionStrategy.Nearest, sim.Config.Agent)
            {
                State = AgentState.Evacuating,
                Posture = SeatingPosture.Walking,
                Intent = BehaviorIntent.Evacuate,
                ExitTarget = sim.Environment.Exits[0]
            });
        sim.HybridMovement.BeginFrame(sim);
        Assert.Equal(5, sim.Agents.Count(a => a.HybridGroupLeaderId == 0));
        Assert.Equal(-1, sim.Agents[5].HybridGroupLeaderId);
        sim.Agents[0].State = AgentState.Injured;
        Assert.False(sim.HybridMovement.TryAdvance(sim, sim.Agents[0]));
        Assert.All(sim.Agents, a => Assert.Equal(-1, a.HybridGroupLeaderId));
        Assert.All(sim.Agents.Take(5), a => Assert.True(a.HybridRegroupAfterFrame > sim.FrameIndex));
    }

    [Fact]
    public void DisabledModeDoesNotAdvanceAgentsAndPersistenceRetainsOptIn()
    {
        Simulation sim = Scene();
        ScenarioDefinition definition = ScenarioDefinition.Capture(sim, 2, "hybrid");
        Assert.True(definition.CreateConfig().HybridEnabled);
        Assert.False(new Simulation().Config.HybridEnabled);
        sim.Config.HybridEnabled = false;
        Vector2 before = sim.Agents[0].Position;
        sim.HybridMovement.BeginFrame(sim);
        Assert.False(sim.HybridMovement.TryAdvance(sim, sim.Agents[0]));
        Assert.Equal(before, sim.Agents[0].Position);
    }
}
