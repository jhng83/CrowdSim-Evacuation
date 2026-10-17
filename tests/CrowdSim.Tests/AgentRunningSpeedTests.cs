using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>SI-unit ceiling, injury/fume modifiers and persistence in both scenarios.</summary>
public sealed class AgentRunningSpeedTests
{
    [Fact]
    public void DefaultCeilingPreservesExistingNormalAgentSpeeds()
    {
        AgentConfig config = new();
        Agent agent = Person(config, 0.081f);
        Assert.Equal(0.081f * config.MaximumBehaviorSpeedMultiplier, agent.CurrentDesiredSpeed, 5);
        Assert.Equal(config.MaxSpeed / AgentConfig.ReferenceStepSeconds,
            config.Behavior.MaxRunningSpeedMetersPerSecond, 5);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    public void DesiredSpeedNeverExceedsTheSelectedPhysicalCeiling(float limit)
    {
        AgentConfig config = new();
        config.Behavior.SetMaxRunningSpeed(limit);
        Agent agent = Person(config, 0.081f);
        Assert.Equal(limit, agent.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds, 4);
    }

    [Fact]
    public void RaisingTheCeilingDoesNotForceSlowerPeopleToRunFaster()
    {
        AgentConfig config = new();
        Agent agent = Person(config, 0.04f);
        float before = agent.CurrentDesiredSpeed;
        config.Behavior.SetMaxRunningSpeed(6f);
        Assert.Equal(before, agent.CurrentDesiredSpeed);
    }

    [Fact]
    public void InjuryAndFumesStillSlowAnAgentBelowTheCeiling()
    {
        AgentConfig config = new();
        config.Behavior.SetMaxRunningSpeed(1f);
        Agent agent = Person(config, 0.081f);
        agent.State = AgentState.Injured;
        agent.InjurySpeedFactor = 0.5f;
        agent.ToxicMobilityFactor = 0.4f;
        Assert.Equal(0.2f, agent.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds, 4);
    }

    [Fact]
    public void CrawlingAndSeatingDoNotAcquireRunningSpeed()
    {
        AgentConfig config = new();
        config.Behavior.SetMaxRunningSpeed(1f);
        Agent agent = Person(config, 0.081f);
        agent.State = AgentState.Crawling;
        Assert.InRange(agent.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds, 0f, 1f);
        agent.ToxicMobilityFactor = 0f;
        Assert.Equal(0f, agent.CurrentDesiredSpeed);
        agent.Posture = SeatingPosture.Seated;
        Assert.Equal(0f, agent.CurrentDesiredSpeed);
    }

    [Theory]
    [InlineData(ScenarioType.Explosion, 0.15f)]
    [InlineData(ScenarioType.Explosion, 0.075f)]
    [InlineData(ScenarioType.ToxicFume, 0.15f)]
    [InlineData(ScenarioType.ToxicFume, 0.075f)]
    public void IntegratorEnforcesMetresPerSecondRegardlessOfTimestep(ScenarioType scenario, float dt)
    {
        SimulationConfig config = new() { Agent = new AgentConfig { Dt = dt } };
        config.Agent.Behavior.SetMaxRunningSpeed(0.6f);
        Simulation sim = new(config: config, randomSeed: 2732);
        sim.SetScenarioType(scenario);
        sim.SpawnAgentsAtPositions(new[] { new Vector2(20f) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.ThreatEscape;
        agent.PanicLevel = 1f;
        agent.Velocity = new Vector2(2f, 0f);
        for (int i = 0; i < 30; i++)
        {
            // A caller cannot bypass the cap by supplying a larger limit.
            sim.EvacuationSystem.IntegrateLocomotion(sim, agent, Vector2.UnitX, 10f);
            Assert.InRange(agent.VelocityMetersPerSecond.Length(), 0f, 0.60001f);
        }
        Assert.Equal(dt, config.Agent.Dt);
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void HybridOpenSpaceMovementAlsoHonoursTheCeiling(ScenarioType scenario)
    {
        Simulation sim = new(randomSeed: 2733);
        sim.SetScenarioType(scenario);
        sim.Config.HybridEnabled = true;
        sim.Config.Agent.Behavior.SetMaxRunningSpeed(0.5f);
        Assert.True(sim.Environment.AddExitFromClick(0.3f, 15f, 5));
        sim.SpawnAgentsAtPositions(new[] { new Vector2(20f, 15f) });
        Agent agent = sim.Agents[0];
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.Evacuate;
        agent.PanicLevel = 1f;
        agent.ExitTarget = sim.Environment.Exits[0];
        sim.HybridMovement.BeginFrame(sim);
        Assert.True(sim.HybridMovement.TryAdvance(sim, agent));
        Assert.InRange(agent.VelocityMetersPerSecond.Length(), 0f, 0.50001f);
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void SaveLoadAndCheckpointRetainTheRunningCeiling(ScenarioType scenario)
    {
        Simulation sim = new(randomSeed: 2734);
        sim.SetScenarioType(scenario);
        sim.Config.Agent.Behavior.SetMaxRunningSpeed(1.25f);
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "speed-cap");
        ScenarioDefinition loaded = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(saved))!;
        Simulation restored = new(config: loaded.CreateConfig());
        loaded.ApplyLayout(restored);
        Assert.Equal(1.25f, restored.Config.Agent.Behavior.MaxRunningSpeedMetersPerSecond);
        Simulation replay = FinalizedSceneCheckpoint.Capture(restored, 0, "speed-cap").CreateReadySimulation();
        Assert.Equal(1.25f, replay.Config.Agent.Behavior.MaxRunningSpeedMetersPerSecond);
        Assert.Equal(scenario, replay.ScenarioType);
    }

    [Fact]
    public void LegacyScenarioWithoutSpeedPropertyUsesTheDefault()
    {
        BehaviorScenario legacy = JsonSerializer.Deserialize<BehaviorScenario>("{}")!;
        BehaviorTuningConfig tuning = new();
        tuning.SetMaxRunningSpeed(0.5f);
        legacy.Apply(tuning);
        Assert.Equal(BehaviorTuningConfig.DefaultMaxRunningSpeedMetersPerSecond, tuning.MaxRunningSpeedMetersPerSecond);
    }

    [Fact]
    public void ControlClampsFiniteValuesAndResetRestoresDefault()
    {
        BehaviorTuningConfig tuning = new();
        tuning.SetMaxRunningSpeed(-2f);
        Assert.Equal(0.5f, tuning.MaxRunningSpeedMetersPerSecond);
        tuning.SetMaxRunningSpeed(200f);
        Assert.Equal(6f, tuning.MaxRunningSpeedMetersPerSecond);
        tuning.Reset();
        Assert.Equal(BehaviorTuningConfig.DefaultMaxRunningSpeedMetersPerSecond, tuning.MaxRunningSpeedMetersPerSecond);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidValuesCannotEnterConfiguration(float value)
    {
        BehaviorTuningConfig tuning = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => tuning.SetMaxRunningSpeed(value));
        Assert.Throws<InvalidDataException>(() => new BehaviorScenario { MaxRunningSpeedMetersPerSecond = value }.Validate());
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(8f)]
    public void InvalidSavedCeilingsAreRejectedRatherThanSilentlyChanged(float value)
    {
        Assert.Throws<InvalidDataException>(() => new BehaviorScenario { MaxRunningSpeedMetersPerSecond = value }.Validate());
    }

    private static Agent Person(AgentConfig config, float baseSpeed) => new(
        0, new Vector2(20f), Vector2.Zero, baseSpeed, 1f, 0f, DecisionStrategy.Nearest, config)
    {
        State = AgentState.Evacuating,
        Intent = BehaviorIntent.ThreatEscape,
        PanicLevel = 1f,
    };
}
