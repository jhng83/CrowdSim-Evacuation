using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

public sealed class ToxicFumeScenarioTests
{
    [Fact]
    public void Precompute_BuildsDeterministicArrivalFieldBeforeRuntime()
    {
        Simulation simulation = CreateToxicSimulation(agentCount: 0);
        Vector2 source = new(28f, 17f);
        Assert.True(simulation.ToxicFumeSystem.TrySetSource(simulation, source));

        simulation.PrecomputeToxicFume();

        Assert.True(simulation.ToxicFumeSystem.IsPrecomputed);
        Assert.True(simulation.ToxicFumeSystem.IsCacheValidFor(simulation));
        Assert.True(simulation.ToxicFumeSystem.ReachableCellCount > 0);
        Assert.True(simulation.ToxicFumeSystem.GetConcentration(source, 0f) > 0.8f);
        Assert.True(simulation.ToxicFumeSystem.GetConcentration(source, 2f) > 0.8f);

        Vector2 downstream = source + new Vector2(3f, 0f);
        Assert.Equal(0f, simulation.ToxicFumeSystem.GetConcentration(downstream, 1f));
        Assert.True(simulation.ToxicFumeSystem.GetConcentration(downstream, 20f) > 0f);
    }

    [Fact]
    public void RuntimeRelease_UsesCachedFieldAndAccumulatesAgentExposure()
    {
        Simulation simulation = CreateToxicSimulation(agentCount: 1);
        Vector2 source = new(28f, 17f);
        Agent agent = simulation.Agents.Single();
        agent.Position = source;
        agent.BeginReaction(0, 1000); // Hold position so this test isolates exposure from locomotion.

        Assert.True(simulation.ToxicFumeSystem.TrySetSource(simulation, source));
        simulation.PrecomputeToxicFume();
        simulation.SetRunning(true);
        Assert.True(simulation.Running);
        Assert.True(simulation.TriggerToxicFumeRelease());

        for (int i = 0; i < 20; i++)
        {
            simulation.Step();
        }

        Assert.True(agent.CumulativeToxicExposure > 0f);
        Assert.True(agent.PeakToxicConcentration > 0f);
        Assert.True(agent.FirstToxicExposureFrame >= 0);
        Assert.NotEqual(ToxicExposureStatus.Clear, agent.ToxicExposureStatus);
    }

    [Fact]
    public void FumeIncapacitation_RemainsPermanentAfterPlumeClears()
    {
        Simulation simulation = CreateToxicSimulation(agentCount: 1);
        Vector2 source = new(28f, 17f);
        Agent agent = simulation.Agents.Single();
        agent.Position = source;
        agent.BeginReaction(0, 1000); // Hold position so exposure progression is deterministic.

        ToxicFumeConfig config = simulation.ToxicFumeSystem.Configuration;
        config.SetValues(
            config.InitialRadiusMeters,
            config.DispersionSpeedMetersPerSecond,
            config.MaximumExtentMeters,
            1.0f, // Short release so the local concentration clears during this test.
            config.DriftDirectionDegrees,
            config.DriftBias,
            config.AwarenessConcentration,
            config.DangerConcentration,
            1.0f,
            2.0f);

        Assert.True(simulation.ToxicFumeSystem.TrySetSource(simulation, source));
        simulation.PrecomputeToxicFume();
        simulation.SetRunning(true);
        Assert.True(simulation.TriggerToxicFumeRelease());

        for (int i = 0; i < 30 && agent.State != AgentState.Incapacitated; i++)
        {
            simulation.Step();
        }

        Assert.Equal(AgentState.Incapacitated, agent.State);
        Assert.Equal(ToxicExposureStatus.Incapacitated, agent.ToxicExposureStatus);
        Assert.Equal(0f, agent.ToxicMobilityFactor);
        Assert.Equal(Vector2.Zero, agent.Velocity);

        // Continue well beyond release + post-release decay. Incapacitation is latched for
        // the remainder of the run and must never recover to impaired/normal movement.
        for (int i = 0; i < 420; i++)
        {
            simulation.Step();
        }

        Assert.Equal(0f, simulation.ToxicFumeSystem.GetCurrentConcentration(simulation, source), 3);
        Assert.Equal(AgentState.Incapacitated, agent.State);
        Assert.Equal(ToxicExposureStatus.Incapacitated, agent.ToxicExposureStatus);
        Assert.Equal(0f, agent.ToxicMobilityFactor);
        Assert.Equal(Vector2.Zero, agent.Velocity);
    }

    [Fact]
    public void GeometryChange_MakesPrecomputedCacheUnusableUntilRecomputed()
    {
        Simulation simulation = CreateToxicSimulation(agentCount: 1);
        Assert.True(simulation.ToxicFumeSystem.TrySetSource(simulation, new Vector2(28f, 17f)));
        simulation.PrecomputeToxicFume();
        Assert.True(simulation.ToxicFumeSystem.IsCacheValidFor(simulation));

        Assert.True(simulation.Environment.AddBlockageFromClick(8f, 8f, cells: 1));
        simulation.BuildWallGrid();

        Assert.False(simulation.ToxicFumeSystem.IsCacheValidFor(simulation));
        simulation.SetRunning(true);
        Assert.False(simulation.Running);
    }


    [Fact]
    public void ConfigurationChange_InvalidatesPrecomputedCacheByRevision()
    {
        Simulation simulation = CreateToxicSimulation(agentCount: 0);
        Assert.True(simulation.ToxicFumeSystem.TrySetSource(simulation, new Vector2(28f, 17f)));
        simulation.PrecomputeToxicFume();
        Assert.True(simulation.ToxicFumeSystem.IsCacheValidFor(simulation));

        ToxicFumeConfig config = simulation.ToxicFumeSystem.Configuration;
        config.SetValues(
            config.InitialRadiusMeters,
            config.DispersionSpeedMetersPerSecond + 0.05f,
            config.MaximumExtentMeters,
            config.ReleaseDurationSeconds,
            config.DriftDirectionDegrees,
            config.DriftBias,
            config.AwarenessConcentration,
            config.DangerConcentration,
            config.ImpairmentExposure,
            config.IncapacitationExposure);

        Assert.False(simulation.ToxicFumeSystem.IsCacheValidFor(simulation));
        simulation.SetRunning(true);
        Assert.False(simulation.Running);
    }

    [Fact]
    public void Version2Scenario_UpgradesToExplosionMode()
    {
        ScenarioDefinition legacy = new() { Version = 2, Name = "legacy" };

        ScenarioDefinition upgraded = ScenarioDefinition.UpgradeLegacyV2(legacy);

        Assert.Equal(ScenarioDefinition.CurrentVersion, upgraded.Version);
        Assert.Equal(ScenarioType.Explosion, upgraded.ScenarioType);
        Assert.NotNull(upgraded.ToxicFume);
    }

    private static Simulation CreateToxicSimulation(int agentCount)
    {
        Simulation simulation = new(agentCount, randomSeed: 1234);
        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Config.World.WidthMeters / 2f,
            simulation.Environment.BoundaryThickness / 2f,
            personsWide: 3));
        simulation.SetScenarioType(ScenarioType.ToxicFume);
        return simulation;
    }
}
