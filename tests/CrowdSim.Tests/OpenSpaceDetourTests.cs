using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class OpenSpaceDetourTests
{
    private static Simulation Scene(IReadOnlyList<Vector2> positions)
    {
        Simulation sim = new(randomSeed: 731);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        sim.SpawnAgentsAtPositions(positions);
        sim.Agents[0].State = AgentState.Evacuating;
        foreach (Agent person in sim.Agents.Skip(1)) person.MarkCrawling();
        return sim;
    }

    [Fact]
    public void CloseCrawlerDoesNotRejectEverySeparatingDirection()
    {
        Simulation sim = Scene(new[] { new Vector2(10f, 10f), new Vector2(10.46f, 10f) });
        Agent person = sim.Agents[0];
        Assert.True(sim.EvacuationSystem.TryChooseEarlyOpenSpaceGoal(sim, person, Vector2.UnitX, false, out Vector2 goal));
        Assert.True(Vector2.Distance(goal, sim.Agents[1].Position) > 0.475f);
        Assert.True(sim.HasTraversablePath(person.Position, goal, sim.Config.Agent.Radius));
    }

    [Fact]
    public void ForwardWallAllowsTemporaryBackwardDetour()
    {
        Simulation sim = Scene(new[] { new Vector2(10f, 10f), new Vector2(10.5f, 10f) });
        // A narrow pocket is open only behind the agent.
        sim.Environment.Walls.Add(new Wall(10.7f, 8f, 10.9f, 12f));
        sim.Environment.Walls.Add(new Wall(9f, 10.25f, 10.9f, 10.45f));
        sim.Environment.Walls.Add(new Wall(9f, 9.55f, 10.9f, 9.75f));
        sim.BuildWallGrid();
        Assert.True(sim.EvacuationSystem.TryChooseEarlyOpenSpaceGoal(sim, sim.Agents[0], Vector2.UnitX, false, out Vector2 goal));
        Assert.True(goal.X < 10f);
    }

    [Fact]
    public void FullySurroundedAgentCannotSelectPathThroughPeople()
    {
        List<Vector2> positions = new() { new Vector2(10f, 10f) };
        for (int i = 0; i < 8; i++)
        {
            float angle = MathF.Tau * i / 8f;
            positions.Add(positions[0] + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 0.56f);
        }
        Simulation sim = Scene(positions);
        Assert.False(sim.EvacuationSystem.TryChooseEarlyOpenSpaceGoal(sim, sim.Agents[0], Vector2.UnitX, false, out _));
    }
}
