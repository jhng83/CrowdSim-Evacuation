using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

public sealed class BottleneckBehaviorTests
{
    [Fact]
    public void AssertiveDelayedAgent_TransitionsToPressWithoutPanic()
    {
        (Simulation simulation, Agent focal, Exit exit) = CreateCongestedDoorway(seed: 1801);
        focal.Assertiveness = 1f;
        focal.PatienceBias = 0.65f;
        focal.UrgencyBias = 1.25f;
        focal.PanicLevel = 0f;

        AdvanceBottleneck(simulation, focal, exit, steps: 40);

        Assert.Equal(BottleneckResponseMode.Press, focal.BottleneckMode);
        Assert.True(focal.BottleneckPressure >= simulation.Config.Agent.BottleneckPressThreshold);
        Assert.True(focal.BottleneckWaitSeconds > 0f);
        Assert.InRange(focal.PanicLevel, 0f, 0.0001f);
    }

    [Fact]
    public void LowAssertivenessAgent_SeeksGapInsteadOfPressing()
    {
        (Simulation simulation, Agent focal, Exit exit) = CreateCongestedDoorway(seed: 1802);
        focal.Assertiveness = 0f;
        focal.PatienceBias = 1.35f;
        focal.UrgencyBias = 0.75f;
        focal.PanicLevel = 0f;

        AdvanceBottleneck(simulation, focal, exit, steps: 40);

        Assert.Equal(BottleneckResponseMode.SeekGap, focal.BottleneckMode);
        Assert.True(focal.BottleneckPressure >= simulation.Config.Agent.BottleneckSeekGapThreshold);
        Assert.True(focal.BottleneckLateralDirection.LengthSquared() > 0f);
    }

    [Fact]
    public void CrawlingAgent_AlwaysYieldsAtCongestedDoorway()
    {
        (Simulation simulation, Agent focal, Exit exit) = CreateCongestedDoorway(seed: 1803);
        focal.State = AgentState.Crawling;
        focal.InjurySpeedFactor = 0f;
        focal.Assertiveness = 1f;
        focal.PatienceBias = 0.65f;
        focal.PanicLevel = 1f;
        focal.BottleneckMode = BottleneckResponseMode.Press;
        focal.BottleneckModeUntilFrame = simulation.FrameIndex + 100;

        simulation.Step();
        simulation.EvacuationSystem.UpdateBottleneckInteraction(
            simulation,
            focal,
            exit,
            Vector2.Distance(focal.Position, exit.Position));

        Assert.Equal(BottleneckResponseMode.Yield, focal.BottleneckMode);
        Assert.NotEqual(BottleneckResponseMode.Press, focal.BottleneckMode);
    }

    [Fact]
    public void LeavingDoorwayInfluence_ReleasesCommittedModeAndDecaysPressure()
    {
        (Simulation simulation, Agent focal, Exit exit) = CreateCongestedDoorway(seed: 1804);
        focal.Assertiveness = 1f;
        focal.PatienceBias = 0.65f;
        focal.UrgencyBias = 1.25f;

        AdvanceBottleneck(simulation, focal, exit, steps: 40);
        Assert.Equal(BottleneckResponseMode.Press, focal.BottleneckMode);
        float previousPressure = focal.BottleneckPressure;

        focal.Position = new Vector2(
            simulation.Environment.Width - 4.8571f,
            simulation.Environment.Height - 4.8571f);
        simulation.Step();
        simulation.EvacuationSystem.UpdateBottleneckInteraction(
            simulation,
            focal,
            exit,
            Vector2.Distance(focal.Position, exit.Position));

        Assert.Equal(BottleneckResponseMode.Free, focal.BottleneckMode);
        Assert.True(focal.BottleneckPressure < previousPressure);
        Assert.Equal(Vector2.Zero, focal.BottleneckLateralDirection);
    }

    [Fact]
    public void PressingCompressesSocialSpaceWithoutRemovingBodyContact()
    {
        Simulation simulation = new(agentCount: 2, randomSeed: 1805);
        Agent agent = simulation.Agents[0];
        Agent other = simulation.Agents[1];
        agent.Position = new Vector2(20.2381f, 16.1905f);
        other.Position = new Vector2(20.6631f, 16.1905f);
        agent.Velocity = Vector2.Zero;
        other.Velocity = Vector2.Zero;
        agent.ExitTarget = new Exit("test", new Vector2(28.3333f, 16.1905f), 0.40f, "E");
        agent.PersonalSpaceBias = 1f;
        agent.PanicLevel = 0f;

        agent.BottleneckMode = BottleneckResponseMode.Free;
        agent.BottleneckPressure = 0f;
        Vector2 freeForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.Zero);

        agent.BottleneckMode = BottleneckResponseMode.Press;
        agent.BottleneckPressure = 1f;
        Vector2 compressedForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.Zero);

        Assert.True(compressedForce.Length() < freeForce.Length());

        other.Position = new Vector2(20.6226f, 16.1905f);
        Vector2 contactForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.Zero);
        Assert.True(float.IsFinite(contactForce.X));
        Assert.True(float.IsFinite(contactForce.Y));
        Assert.True(contactForce.Length() > 0f);
    }

    [Fact]
    public void ScenarioRoundTrip_PreservesBottleneckTuning()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"crowdsim-bottleneck-{Guid.NewGuid():N}");

        try
        {
            Simulation source = new(agentCount: 0, randomSeed: 1806);
            source.Config.Agent.Behavior.SetValues(
                traitVariation: 0.70f,
                reactionVariation: 0.65f,
                stressSusceptibility: 1.05f,
                stressPersistence: 1.20f,
                crowdFollowing: 0.55f,
                personalSpace: 1.05f,
                panicSpaceCompression: 0.22f,
                emergencyUrgency: 1.08f,
                helpingTendency: 0.35f,
                routeFamiliarity: 0.72f,
                bottleneckAssertiveness: 0.68f,
                bottleneckPressureSensitivity: 1.22f,
                bottleneckSpaceCompression: 0.26f);
            Assert.True(source.Environment.AddExitFromClick(
                source.Config.World.WidthMeters / 2,
                source.Environment.BoundaryThickness / 2,
                personsWide: 1));

            ScenarioDefinition captured = ScenarioDefinition.Capture(
                source,
                requestedAgentCount: 40,
                name: "bottleneck_roundtrip");
            ScenarioFileStore.Save(captured, directory);
            ScenarioDefinition loaded = ScenarioFileStore.Load(
                "bottleneck_roundtrip",
                directory);
            Simulation restored = new(0, loaded.CreateConfig(), loaded.RandomSeed);

            Assert.Equal(
                source.Config.Agent.Behavior.BottleneckAssertiveness,
                restored.Config.Agent.Behavior.BottleneckAssertiveness);
            Assert.Equal(
                source.Config.Agent.Behavior.BottleneckPressureSensitivity,
                restored.Config.Agent.Behavior.BottleneckPressureSensitivity);
            Assert.Equal(
                source.Config.Agent.Behavior.BottleneckSpaceCompression,
                restored.Config.Agent.Behavior.BottleneckSpaceCompression);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static (Simulation Simulation, Agent Focal, Exit Exit) CreateCongestedDoorway(int seed)
    {
        Simulation simulation = new(agentCount: 0, randomSeed: seed);
        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Config.World.WidthMeters / 2,
            simulation.Environment.BoundaryThickness / 2,
            personsWide: 1));
        simulation.SpawnAgents(10);
        Exit exit = simulation.Environment.Exits.Single();

        for (int index = 0; index < simulation.Agents.Count; index++)
        {
            Agent agent = simulation.Agents[index];
            int column = index % 5;
            int row = index / 5;
            agent.Position = new Vector2(
                exit.Position.X + (column - 2) * 0.3643f,
                simulation.Environment.BoundaryThickness + 1.2143f + row * 0.4452f);
            agent.Velocity = Vector2.Zero;
            agent.State = AgentState.Evacuating;
            agent.Intent = BehaviorIntent.Evacuate;
            agent.ExitTarget = exit;
            agent.PanicLevel = 0f;
            agent.StunnedUntilFrame = int.MaxValue;
            agent.ResetBottleneckInteraction();
        }

        simulation.SetRunning(true);
        return (simulation, simulation.Agents[0], exit);
    }

    private static void AdvanceBottleneck(
        Simulation simulation,
        Agent focal,
        Exit exit,
        int steps)
    {
        for (int index = 0; index < steps; index++)
        {
            simulation.Step();
            simulation.EvacuationSystem.UpdateBottleneckInteraction(
                simulation,
                focal,
                exit,
                Vector2.Distance(focal.Position, exit.Position));
        }
    }
}
