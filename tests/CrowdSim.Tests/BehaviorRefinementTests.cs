using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class BehaviorRefinementTests
{
    [Fact]
    public void IndividualRecoveryBias_ChangesStressDecayRate()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1701);
        Agent fasterRecovery = simulation.Agents[0];
        Agent slowerRecovery = simulation.Agents[1];
        Exit exit = simulation.Environment.Exits.Single();

        fasterRecovery.Position = new Vector2(8.0952f, 8.0952f);
        slowerRecovery.Position = new Vector2(44.5238f, 26.3095f);
        foreach (Agent agent in simulation.Agents)
        {
            agent.State = AgentState.Evacuating;
            agent.Intent = BehaviorIntent.Evacuate;
            agent.ExitTarget = exit;
            agent.PanicLevel = 0.80f;
            agent.Velocity = Vector2.Zero;
        }

        fasterRecovery.StressRecoveryBias = 0.70f;
        slowerRecovery.StressRecoveryBias = 1.30f;

        simulation.SetRunning(true);
        simulation.Step();

        Assert.True(slowerRecovery.PanicLevel > fasterRecovery.PanicLevel);
        Assert.InRange(fasterRecovery.PanicLevel, 0f, 1f);
        Assert.InRange(slowerRecovery.PanicLevel, 0f, 1f);
    }

    [Fact]
    public void PanicCompressesSocialSpaceWithoutDisablingPhysicalContact()
    {
        Simulation simulation = new(agentCount: 2, randomSeed: 1702);
        Agent agent = simulation.Agents[0];
        Agent other = simulation.Agents[1];
        agent.Position = new Vector2(20.2381f, 16.1905f);
        other.Position = new Vector2(20.6631f, 16.1905f);
        agent.Velocity = Vector2.Zero;
        other.Velocity = Vector2.Zero;
        agent.ExitTarget = new Exit("test", new Vector2(28.3333f, 16.1905f), 0.40f, "E");
        agent.PersonalSpaceBias = 1f;

        agent.PanicLevel = 0f;
        Vector2 calmForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.Zero);

        agent.PanicLevel = 1f;
        Vector2 compressedForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.Zero);

        Assert.True(VectorMath.IsFinite(calmForce));
        Assert.True(VectorMath.IsFinite(compressedForce));
        Assert.True(compressedForce.Length() < calmForce.Length());

        other.Position = new Vector2(20.6226f, 16.1905f);
        Vector2 contactForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.Zero);
        Assert.True(VectorMath.IsFinite(contactForce));
        Assert.True(contactForce.Length() > 0f);
    }

    [Fact]
    public void EmergencyUrgencyTuning_ChangesPurposefulSpeedWithinCap()
    {
        SimulationConfig defaultConfig = SimulationConfig.CreateDefault();
        SimulationConfig urgentConfig = SimulationConfig.CreateDefault();
        urgentConfig.Agent.Behavior.SetValues(
            traitVariation: BehaviorTuningConfig.DefaultTraitVariation,
            reactionVariation: BehaviorTuningConfig.DefaultReactionVariation,
            stressSusceptibility: BehaviorTuningConfig.DefaultStressSusceptibility,
            stressPersistence: BehaviorTuningConfig.DefaultStressPersistence,
            crowdFollowing: BehaviorTuningConfig.DefaultCrowdFollowing,
            personalSpace: BehaviorTuningConfig.DefaultPersonalSpace,
            panicSpaceCompression: BehaviorTuningConfig.DefaultPanicSpaceCompression,
            emergencyUrgency: 1.20f,
            helpingTendency: BehaviorTuningConfig.DefaultHelpingTendency,
            routeFamiliarity: BehaviorTuningConfig.DefaultRouteFamiliarity);

        Simulation normal = new(agentCount: 1, config: defaultConfig, randomSeed: 1703);
        Simulation urgent = new(agentCount: 1, config: urgentConfig, randomSeed: 1703);
        Agent normalAgent = normal.Agents.Single();
        Agent urgentAgent = urgent.Agents.Single();
        normalAgent.State = AgentState.Evacuating;
        urgentAgent.State = AgentState.Evacuating;
        normalAgent.Intent = BehaviorIntent.Evacuate;
        urgentAgent.Intent = BehaviorIntent.Evacuate;
        normalAgent.PanicLevel = 0f;
        urgentAgent.PanicLevel = 0f;
        normalAgent.UrgencyBias = 1f;
        urgentAgent.UrgencyBias = 1f;

        Assert.Equal(normalAgent.BaseSpeed, urgentAgent.BaseSpeed);
        Assert.True(urgentAgent.CurrentDesiredSpeed > normalAgent.CurrentDesiredSpeed);
        Assert.True(
            urgentAgent.CurrentDesiredSpeed <=
            urgentAgent.BaseSpeed * urgentConfig.Agent.MaximumBehaviorSpeedMultiplier + 0.0001f);
    }

    [Fact]
    public void ZeroTraitVariation_ProducesConfiguredPopulationCentres()
    {
        SimulationConfig config = SimulationConfig.CreateDefault();
        config.Agent.Behavior.SetValues(
            traitVariation: 0f,
            reactionVariation: 0f,
            stressSusceptibility: 1.20f,
            stressPersistence: 1.10f,
            crowdFollowing: 0.70f,
            personalSpace: 1.05f,
            panicSpaceCompression: 0.18f,
            emergencyUrgency: 1.05f,
            helpingTendency: 0.30f,
            routeFamiliarity: 0.90f,
            bottleneckAssertiveness: 0.62f,
            bottleneckPressureSensitivity: 1.10f,
            bottleneckSpaceCompression: 0.24f);

        Simulation simulation = new(agentCount: 4, config: config, randomSeed: 1704);
        foreach (Agent agent in simulation.Agents)
        {
            Assert.InRange(MathF.Abs(agent.ThreatSensitivity - 1.20f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.FollowTendency - 0.70f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.HelpingTendency - 0.30f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.RouteFamiliarity - 0.90f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.UrgencyBias - 1f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.StressRecoveryBias - 1f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.PersonalSpaceBias - 1f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.Assertiveness - 0.62f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.PatienceBias - 1f), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(agent.ReactionDelayFrames - 35f), 0f, 0.0001f);
        }
    }

    private static Simulation CreateReadySimulation(int count, int seed)
    {
        Simulation simulation = new(agentCount: 0, randomSeed: seed);
        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Config.World.WidthMeters / 2,
            simulation.Environment.BoundaryThickness / 2,
            personsWide: 3));
        simulation.SpawnAgents(count);
        return simulation;
    }
}
