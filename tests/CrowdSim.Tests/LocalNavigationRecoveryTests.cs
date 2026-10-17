using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>
/// Regression coverage for local obstacle/casualty recovery added after visual QA of the
/// explosion scenario. These tests protect behaviour commitment and hard body separation.
/// </summary>
public sealed class LocalNavigationRecoveryTests
{
    [Fact]
    public void CircularBlockageContact_LatchesTangentFollowInsteadOfRepeatedInwardDrive()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1701);
        Agent agent = simulation.Agents.Single();
        Exit exit = simulation.Environment.Exits.Single();
        CircleBlockage circle = new(new Vector2(20f, 20f), CircleBlockage.DefaultRadiusMeters);
        simulation.Environment.CircularBlockages.Add(circle);

        float bodyRadius = simulation.Config.Agent.Radius;
        agent.Position = circle.Center + new Vector2(circle.Radius + bodyRadius - 0.05f, 0f);
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.Evacuate;
        agent.ExitTarget = exit;
        agent.HelpingTendency = 0f;
        agent.FollowTendency = 0f;
        agent.LastDesiredDirection = -Vector2.UnitX;
        agent.Velocity = -Vector2.UnitX * 0.05f;

        simulation.ResolveSolidCollisions(agent);

        Assert.Equal(LocalNavigationMode.CircleFollow, agent.LocalNavigationMode);
        Assert.NotNull(agent.CircleFollowCenter);
        Assert.Equal(circle.Center, agent.CircleFollowCenter!.Value);
        Assert.NotEqual(0, agent.CircleFollowDirection);
        Vector2 radial = Vector2.Normalize(agent.Position - circle.Center);
        Assert.True(
            Vector2.Dot(agent.Velocity, -radial) <= 0.0001f,
            "Collision resolution must remove inward velocity before tangent following begins.");

        simulation.EvacuationSystem.UpdateEvacAgent(simulation, agent);

        Assert.Equal(LocalNavigationMode.CircleFollow, agent.LocalNavigationMode);
        Assert.True(
            MathF.Abs(agent.LastDesiredDirection.Y) > 0.50f,
            $"Expected a committed tangential direction, got {agent.LastDesiredDirection}.");
        Assert.True(
            Vector2.Dot(agent.LastDesiredDirection, radial) > 0f,
            "Circle following should retain a small outward component rather than push into the surface.");
    }

    [Fact]
    public void CasualtyStillBlockingAfterFailedBypass_SeeksClearSpacePromptly()
    {
        SimulationConfig config = new()
        {
            Agent = new AgentConfig
            {
                SlowObstacleBypassObservationSeconds = AgentConfig.ReferenceStepSeconds,
                CasualtyGapWaitMinSeconds = 1.0f,
                CasualtyGapWaitMaxSeconds = 1.0f,
            },
        };
        Simulation simulation = CreateReadySimulation(2, seed: 1702, config);
        Exit exit = simulation.Environment.Exits.Single();
        Agent pedestrian = simulation.Agents[0];
        Agent crawler = simulation.Agents[1];

        pedestrian.Position = new Vector2(28f, 20f);
        pedestrian.Velocity = Vector2.Zero;
        pedestrian.State = AgentState.Evacuating;
        pedestrian.Intent = BehaviorIntent.Evacuate;
        pedestrian.ExitTarget = exit;
        pedestrian.HelpingTendency = 0f;
        pedestrian.FollowTendency = 0f;
        pedestrian.PanicLevel = 0f;
        pedestrian.PatienceBias = 1f;
        // Model the case seen in QA: one local bypass was already tried but the same casualty
        // still occupies the forward corridor. Free space should trigger a committed detour.
        pedestrian.LocalNavigationBypassAttempts = 1;

        crawler.Position = new Vector2(28f, 19.30f);
        crawler.Velocity = Vector2.Zero;
        crawler.ExitTarget = exit;
        crawler.MarkCrawling();
        crawler.PanicLevel = 0f;

        simulation.SetRunning(true);
        simulation.Step(); // observe the slower casualty
        simulation.Step(); // observation window elapsed; blockage remains

        Assert.Equal(LocalNavigationMode.SeekingGap, pedestrian.LocalNavigationMode);
        Assert.Equal(crawler.AgentId, pedestrian.LocalNavigationBlockerAgentId);
        Assert.NotNull(pedestrian.SlowObstacleBypassGoal);
        Assert.True(pedestrian.LastDesiredDirection.LengthSquared() > 0.1f);
        Vector2 committed = pedestrian.SlowObstacleBypassGoal!.Value;
        simulation.Step();
        Assert.Equal(committed, pedestrian.SlowObstacleBypassGoal);
    }

    [Fact]
    public void ControlledPress_NeverDisablesHardCasualtyBodySeparation()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1703);
        Agent pedestrian = simulation.Agents[0];
        Agent casualty = simulation.Agents[1];

        pedestrian.Position = new Vector2(20f, 20f);
        pedestrian.State = AgentState.Evacuating;
        pedestrian.Intent = BehaviorIntent.Evacuate;
        pedestrian.LocalNavigationMode = LocalNavigationMode.PressingThrough;
        pedestrian.TimeStuck = 10f;
        // A waiting/pressing pedestrian may be completely stationary; the low-speed
        // collision fast path must still enforce the larger casualty body envelope.
        pedestrian.Velocity = Vector2.Zero;

        casualty.Position = new Vector2(20.30f, 20f);
        casualty.Velocity = Vector2.Zero;
        casualty.MarkCrawling();

        simulation.ResolveSolidCollisions(pedestrian);

        float minimumSeparation =
            simulation.Config.Agent.Radius + 1.25f * simulation.Config.Agent.Radius;
        float actualSeparation = Vector2.Distance(pedestrian.Position, casualty.Position);
        Assert.True(
            actualSeparation >= minimumSeparation - 0.002f,
            $"Controlled press may reduce social spacing but must retain hard body separation. " +
            $"Expected >= {minimumSeparation:F3} m, got {actualSeparation:F3} m.");
    }

    private static Simulation CreateReadySimulation(
        int count,
        int seed,
        SimulationConfig? config = null)
    {
        Simulation simulation = new(agentCount: 0, config: config, randomSeed: seed);
        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Config.World.WidthMeters / 2f,
            simulation.Environment.BoundaryThickness / 2f,
            personsWide: 3));
        simulation.SpawnAgents(count);
        return simulation;
    }
}
