using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

public sealed class Fix15Tests
{
    [Fact]
    public void CalmEvacuatingAgent_HasUrgencySpeedFloor()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1501);
        Agent agent = simulation.Agents.Single();
        agent.State = AgentState.Evacuating;
        agent.PanicLevel = 0f;
        agent.InjurySpeedFactor = 1f;

        float expected =
            agent.BaseSpeed *
            simulation.Config.Agent.EvacuationSpeedFloorMultiplier *
            Math.Clamp(agent.UrgencyBias, 0.75f, 1.25f);

        Assert.InRange(MathF.Abs(agent.CurrentDesiredSpeed - expected), 0f, 0.0001f);
        Assert.True(agent.CurrentDesiredSpeed > agent.BaseSpeed);
    }

    [Fact]
    public void ActiveAlarmSocialSignal_RemainsMeaningfulWhenObserverAlreadyHasPanic()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1502);
        Exit exit = simulation.Environment.Exits.Single();
        Agent observer = simulation.Agents[0];
        Agent source = simulation.Agents[1];

        observer.Position = new Vector2(24.2857f, 17f);
        observer.State = AgentState.Evacuating;
        observer.ExitTarget = exit;
        observer.PanicLevel = 0.45f;
        observer.Velocity = Vector2.Zero;

        source.Position = new Vector2(25.0952f, 17f);
        source.State = AgentState.Evacuating;
        source.ExitTarget = exit;
        source.PanicLevel = 0.50f;
        source.Velocity = Vector2.Zero;
        source.ActivateDirectAlarm(
            simulation.FrameIndex,
            durationFrames: 30,
            panicFloor: 0.50f,
            episodeId: 1502);

        simulation.SetRunning(true);
        simulation.Step();

        // A single source must attenuate with distance rather than cancelling its own weight.
        float expectedSignal = 0.50f * MathF.Exp(-0.8095f / simulation.Config.Agent.PanicThreatScale);
        Assert.InRange(MathF.Abs(observer.LastSocialPanicStimulus - expectedSignal), 0f, 0.0001f);
        Assert.True(observer.PanicLevel > 0.45f);
        Assert.InRange(observer.PanicLevel, 0f, 0.60f);
        Assert.Equal(1502, observer.LastRelayedAlarmEpisodeId);
    }

    [Fact]
    public void CoherentEvacuationFlow_TriggersAwarenessWithoutCreatingPanicAlarm()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 1503);
        Agent observer = simulation.Agents[0];
        observer.Position = new Vector2(32.381f, 24.2857f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        for (int i = 1; i < 4; i++)
        {
            Agent evacuee = simulation.Agents[i];
            evacuee.Position = new Vector2(32.9881f + i * 0.3238f, 24.2857f + i * 0.0405f);
            evacuee.State = AgentState.Evacuating;
            evacuee.ExitTarget = simulation.Environment.Exits.Single();
            evacuee.PanicLevel = 0f;
            evacuee.Velocity = Vector2.UnitX * (evacuee.BaseSpeed * 1.2f);
        }

        // The event exists so evacuation behaviour has emergency context, but it is far
        // enough away that no direct shock/panic can reach this local test group.
        simulation.Environment.SetExplosion(4.05f, 4.05f, simulation.FrameIndex);
        simulation.SetRunning(true);
        simulation.Step();

        Assert.Equal(AgentState.WaitingToReact, observer.State);
        Assert.True(observer.LastEvacuationAwarenessStimulus > 0f);
        Assert.InRange(observer.PanicLevel, 0f, 0.0001f);
        Assert.Equal(-1, observer.ActiveAlarmEpisodeId);
        Assert.Equal(-1, observer.LastRelayedAlarmEpisodeId);
    }

    [Fact]
    public void ObviousEvacuatingCrowd_TriggersAwarenessEvenWhenMovementIsSlow()
    {
        Simulation simulation = CreateReadySimulation(5, seed: 1504);
        Agent observer = simulation.Agents[0];
        observer.Position = new Vector2(32.381f, 24.2857f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        for (int i = 1; i < 5; i++)
        {
            Agent evacuee = simulation.Agents[i];
            evacuee.Position = observer.Position + new Vector2(0.4048f * i, 0.1619f * (i % 2));
            evacuee.State = AgentState.Evacuating;
            evacuee.ExitTarget = simulation.Environment.Exits.Single();
            evacuee.PanicLevel = 0f;
            // Even a temporarily stalled evacuation crowd is strong behavioural evidence.
            evacuee.Velocity = Vector2.Zero;
        }

        simulation.Environment.SetExplosion(4.05f, 4.05f, simulation.FrameIndex);
        simulation.SetRunning(true);
        simulation.Step();

        Assert.Equal(AgentState.WaitingToReact, observer.State);
        Assert.True(observer.LastEvacuationAwarenessStimulus >= 0.50f);
        Assert.InRange(observer.PanicLevel, 0f, 0.0001f);
    }

    [Fact]
    public void EvacuationAwarenessAlone_DoesNotCreateLatePanicEpisode()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 1505);
        Agent observer = simulation.Agents[0];
        observer.Position = new Vector2(36.4286f, 26.3095f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        for (int i = 1; i < 4; i++)
        {
            Agent evacuee = simulation.Agents[i];
            evacuee.Position = new Vector2(37.0357f + i * 0.2833f, 26.3095f + i * 0.0405f);
            evacuee.State = AgentState.Evacuating;
            evacuee.ExitTarget = simulation.Environment.Exits.Single();
            evacuee.PanicLevel = 0f;
            evacuee.Velocity = Vector2.UnitX * (evacuee.BaseSpeed * 1.2f);
        }

        simulation.Environment.SetExplosion(4.05f, 4.05f, simulation.FrameIndex);
        simulation.SetRunning(true);
        for (int i = 0; i < 25; i++)
        {
            simulation.Step();
        }

        Assert.NotEqual(AgentState.Wandering, observer.State);
        Assert.InRange(observer.PanicLevel, 0f, 0.0001f);
        Assert.Equal(-1, observer.ActiveAlarmEpisodeId);
        Assert.Equal(-1, observer.LastRelayedAlarmEpisodeId);
    }

    [Fact]
    public void ScenarioJson_RoundTripPreservesGeometryRadiiAgentCountAndSeed()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"crowdsim-fix15-{Guid.NewGuid():N}");
        try
        {
            Simulation source = new(agentCount: 0, randomSeed: 4242);
            source.Config.Agent.SetBlastZoneRadii(1.01f, 2.43f, 4.45f, 7.29f, 12.95f);
            source.Config.Agent.Behavior.SetValues(
                traitVariation: 0.80f,
                reactionVariation: 0.60f,
                stressSusceptibility: 1.15f,
                stressPersistence: 1.35f,
                crowdFollowing: 0.65f,
                personalSpace: 1.10f,
                panicSpaceCompression: 0.25f,
                emergencyUrgency: 1.12f,
                helpingTendency: 0.40f,
                routeFamiliarity: 0.75f,
                bottleneckAssertiveness: 0.68f,
                bottleneckPressureSensitivity: 1.22f,
                bottleneckSpaceCompression: 0.26f);
            Assert.True(source.Environment.AddExitFromClick(
                source.Config.World.WidthMeters / 2,
                source.Environment.BoundaryThickness / 2,
                personsWide: 3));
            Assert.True(source.Environment.AddExitFromClick(
                source.Config.World.WidthMeters - source.Environment.BoundaryThickness / 2,
                source.Config.World.LengthMeters / 2,
                personsWide: 1));
            Assert.True(source.Environment.AddBlockageFromClick(24.29f, 16.19f));

            ScenarioDefinition captured = ScenarioDefinition.Capture(
                source,
                requestedAgentCount: 321,
                name: "fix15_roundtrip");
            ScenarioFileStore.Save(captured, directory);
            ScenarioDefinition loaded = ScenarioFileStore.Load("fix15_roundtrip", directory);

            Simulation restored = new(0, loaded.CreateConfig(), loaded.RandomSeed);
            loaded.ApplyLayout(restored);

            Assert.Equal(ScenarioDefinition.CurrentVersion, loaded.Version);
            Assert.Equal(321, loaded.AgentCount);
            Assert.Equal(4242, loaded.RandomSeed);
            Assert.Equal(source.Environment.Exits.Count, restored.Environment.Exits.Count);
            Assert.Equal(source.Environment.Blockages.Count, restored.Environment.Blockages.Count);
            Assert.Equal(source.Config.Agent.HardKillRadius, restored.Config.Agent.HardKillRadius);
            Assert.Equal(source.Config.Agent.HeatIncapacityRadius, restored.Config.Agent.HeatIncapacityRadius);
            Assert.Equal(source.Config.Agent.FallenRadius, restored.Config.Agent.FallenRadius);
            Assert.Equal(source.Config.Agent.InjuryRadius, restored.Config.Agent.InjuryRadius);
            Assert.Equal(source.Config.Agent.HeatRadius, restored.Config.Agent.HeatRadius);
            Assert.Equal(source.Config.Agent.Behavior.TraitVariation, restored.Config.Agent.Behavior.TraitVariation);
            Assert.Equal(source.Config.Agent.Behavior.ReactionVariation, restored.Config.Agent.Behavior.ReactionVariation);
            Assert.Equal(source.Config.Agent.Behavior.StressSusceptibility, restored.Config.Agent.Behavior.StressSusceptibility);
            Assert.Equal(source.Config.Agent.Behavior.StressPersistence, restored.Config.Agent.Behavior.StressPersistence);
            Assert.Equal(source.Config.Agent.Behavior.CrowdFollowing, restored.Config.Agent.Behavior.CrowdFollowing);
            Assert.Equal(source.Config.Agent.Behavior.PersonalSpace, restored.Config.Agent.Behavior.PersonalSpace);
            Assert.Equal(source.Config.Agent.Behavior.PanicSpaceCompression, restored.Config.Agent.Behavior.PanicSpaceCompression);
            Assert.Equal(source.Config.Agent.Behavior.EmergencyUrgency, restored.Config.Agent.Behavior.EmergencyUrgency);
            Assert.Equal(source.Config.Agent.Behavior.HelpingTendency, restored.Config.Agent.Behavior.HelpingTendency);
            Assert.Equal(source.Config.Agent.Behavior.RouteFamiliarity, restored.Config.Agent.Behavior.RouteFamiliarity);
            Assert.Equal(source.Config.Agent.Behavior.BottleneckAssertiveness, restored.Config.Agent.Behavior.BottleneckAssertiveness);
            Assert.Equal(source.Config.Agent.Behavior.BottleneckPressureSensitivity, restored.Config.Agent.Behavior.BottleneckPressureSensitivity);
            Assert.Equal(source.Config.Agent.Behavior.BottleneckSpaceCompression, restored.Config.Agent.Behavior.BottleneckSpaceCompression);

            for (int i = 0; i < source.Environment.Exits.Count; i++)
            {
                Exit expected = source.Environment.Exits[i];
                Exit actual = restored.Environment.Exits[i];
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Side, actual.Side);
                Assert.Equal(expected.Size, actual.Size);
                Assert.Equal(expected.Position, actual.Position);
            }

            Assert.Equal(source.Environment.Blockages[0], restored.Environment.Blockages[0]);

            // The stored seed is part of the scenario contract: a fresh finalisation of
            // the same loaded geometry should reproduce the same initial population.
            source.SpawnAgents(8);
            restored.SpawnAgents(8);
            for (int i = 0; i < source.Agents.Count; i++)
            {
                Assert.Equal(source.Agents[i].Position, restored.Agents[i].Position);
                Assert.Equal(source.Agents[i].BaseSpeed, restored.Agents[i].BaseSpeed);
                Assert.Equal(source.Agents[i].Strategy, restored.Agents[i].Strategy);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
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
