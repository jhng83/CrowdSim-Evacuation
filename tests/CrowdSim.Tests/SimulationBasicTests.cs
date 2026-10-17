using System.Numerics;
using Xunit;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Tests;

public sealed class SimulationBasicTests
{
    [Fact]
    public void InitialState_StartsWithWanderingAgents()
    {
        Simulation simulation = new(agentCount: 10, randomSeed: 7);

        Assert.Equal(10, simulation.Agents.Count);
        Assert.Equal(0, simulation.FrameIndex);
        Assert.All(simulation.Agents, agent => Assert.Equal(AgentState.Wandering, agent.State));
    }

    [Fact]
    public void RunningGuardrail_RequiresAtLeastOneExit()
    {
        Simulation simulation = new(agentCount: 10, randomSeed: 7);

        Assert.False(simulation.ToggleRunning());
        Assert.False(simulation.Running);

        Assert.True(AddNorthExit(simulation));
        Assert.True(simulation.ToggleRunning());
        Assert.True(simulation.Running);
    }

    [Fact]
    public void Explosion_StartlesAgentsThenTheyReact()
    {
        Simulation simulation = CreateReadySimulation(10);
        Vector2 centre = new(simulation.Config.World.WidthMeters / 2f, simulation.Config.World.LengthMeters / 2f);
        for (int i = 0; i < simulation.Agents.Count; i++)
        {
            simulation.Agents[i].Position = centre + new Vector2(7.2857f + i * 0.081f, 0f);
        }

        Assert.True(simulation.ToggleRunning());
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear(); // isolate startle/front timing from debris-impact outcomes
        Assert.DoesNotContain(simulation.Agents, agent => agent.State == AgentState.WaitingToReact);

        // At ~7.3 m and 1.21 m/step, the abstract shock front reaches these agents after
        // several simulation frames; startle is therefore synchronized to front arrival.
        for (int frame = 0; frame < 8; frame++)
        {
            simulation.Step();
        }

        Assert.Contains(
            simulation.Agents,
            agent => agent.State is AgentState.WaitingToReact or AgentState.Injured or AgentState.Evacuating);

        for (int frame = 0; frame < 100; frame++)
        {
            simulation.Step();
        }

        Assert.Contains(
            simulation.Agents,
            agent => agent.State is AgentState.Evacuating or AgentState.Injured or AgentState.Exited);
    }

    [Fact]
    public void ExitFlow_AllowsAgentToLeaveThroughTrueOpening()
    {
        Simulation simulation = CreateReadySimulation(1);
        Exit exit = simulation.Environment.Exits.Single();
        Agent agent = simulation.Agents.Single();
        agent.Position = exit.Position;
        agent.ExitTarget = exit;
        agent.State = AgentState.Evacuating;

        simulation.EvacuationSystem.ApplyExitFlows(simulation);

        Assert.Equal(AgentState.Exited, agent.State);
        Assert.Equal(Vector2.Zero, agent.Velocity);
    }

    private static Simulation CreateReadySimulation(int count)
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 1234);
        Assert.True(AddNorthExit(simulation));
        simulation.SpawnAgents(count);
        return simulation;
    }

    private static bool AddNorthExit(Simulation simulation)
        => simulation.Environment.AddExitFromClick(
            simulation.Config.World.WidthMeters / 2,
            simulation.Environment.BoundaryThickness / 2,
            personsWide: 3);
}
