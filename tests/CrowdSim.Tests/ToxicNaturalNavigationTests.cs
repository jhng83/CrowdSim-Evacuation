using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Scenario integration, perception limits and bounded-work regression specifications.</summary>
public sealed class ToxicNaturalNavigationTests
{
    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void GenericRecoveryIsEnabledInBothScenarios(ScenarioType scenario)
    {
        Simulation sim = Scene(1, scenario: scenario);
        Assert.True(sim.EvacuationSystem.UsesNaturalNavigation(sim));
        sim.EvacuationSystem.RequestNaturalRecovery(sim, sim.Agents[0], force: true);
        Assert.Equal(1, sim.EvacuationSystem.NaturalPendingRecoveries);
    }

    [Fact]
    public void PrecomputedButUnreleasedFumeDoesNotAffectNavigation()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Prepare(sim, new Vector2(29.75f, 17.25f));
        Vector2 goal = agent.Position + new Vector2(2f, 0f);
        Assert.True(sim.ToxicFumeSystem.GetConcentration(new Vector2(29.75f, 17.25f), 0f) > 0.8f);
        Assert.Equal(0f, sim.EvacuationSystem.NaturalToxicPathPenalty(sim, agent, goal));
        Assert.Equal(Vector2.UnitX,
            sim.EvacuationSystem.ApplyNaturalToxicAvoidance(sim, agent, Vector2.UnitX, false));
        Assert.False(agent.NavigationMemory.RecoveryRequested);
    }

    [Fact]
    public void RecoveryPrefersAClearCandidateOverContaminatedExitwardSpace()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Prepare(sim, new Vector2(29.75f, 17.25f));
        Release(sim);
        agent.NavigationMemory.RecoveryAttempts = 1;
        float forwardPenalty = sim.EvacuationSystem.NaturalToxicPathPenalty(
            sim, agent, agent.Position + new Vector2(1.6f, 0f));
        Assert.True(forwardPenalty > 0f);
        Assert.True(sim.EvacuationSystem.TryChooseNaturalRecoveryGoal(sim, agent, out Vector2 goal));
        Assert.True(sim.EvacuationSystem.NaturalToxicPathPenalty(sim, agent, goal) < forwardPenalty);
        Assert.True(sim.HasTraversablePath(agent.Position, goal, sim.Config.Agent.Radius));
    }

    [Fact]
    public void FuturePlumeAndPreviewTimeAreNotReadAsCurrentHazards()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Vector2 goal = agent.Position + new Vector2(2f, 0f);
        Prepare(sim, new Vector2(35.25f, 17.25f));
        Release(sim);
        sim.ToxicFumeSystem.PreviewTimeSeconds = 40f;
        Assert.True(sim.ToxicFumeSystem.GetConcentration(goal, 40f) > 0f);
        Assert.Equal(0f, sim.EvacuationSystem.SampleNaturalFumeRisk(sim, agent, goal).Peak);
    }

    [Fact]
    public void FumeObservationDoesNotReachBeyondTheConfiguredPerceptionHorizon()
    {
        Simulation sim = Scene(1, new AgentConfig { NaturalPerceptionRadiusMeters = 1f });
        Agent agent = sim.Agents[0];
        Vector2 source = new(31.25f, 17.25f);
        Prepare(sim, source);
        Release(sim);
        Assert.True(sim.ToxicFumeSystem.GetCurrentConcentration(sim, source) > 0.8f);
        var risk = sim.EvacuationSystem.SampleNaturalFumeRisk(sim, agent, source);
        Assert.Equal(0f, risk.Peak);
        Assert.InRange(risk.ObservedDistance, 0f, 1f);
    }

    [Fact]
    public void FumeObservationStopsAtAnOccludingWall()
    {
        Simulation sim = Scene(1);
        sim.Environment.Blockages.Add(new Wall(28.9f, 15f, 29.1f, 20f));
        sim.Environment.RebuildWalls();
        sim.BuildWallGrid();
        Prepare(sim, new Vector2(30.25f, 17.25f));
        Release(sim);
        var risk = sim.EvacuationSystem.SampleNaturalFumeRisk(sim, sim.Agents[0], new Vector2(32.25f, 17.25f));
        Assert.Equal(0f, risk.Peak);
        Assert.True(risk.ObservedDistance < 0.7f);
    }

    [Fact]
    public void NewlyContaminatedDetourIsAbandonedAndRemembered()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Vector2 goal = agent.Position + new Vector2(2f, 0f);
        Prepare(sim, new Vector2(29.75f, 17.25f));
        agent.NavigationMemory.DetourGoal = goal;
        agent.NavigationMemory.DetourUntilFrame = 100;
        agent.NavigationMemory.DetourBestDistance = 2f;
        // The previously clear intention has zero remembered concentration.
        Assert.False(sim.EvacuationSystem.NaturalToxicDetourWorsened(sim, agent, goal));
        Release(sim);
        Assert.True(sim.EvacuationSystem.NaturalToxicDetourWorsened(sim, agent, goal));
        sim.EvacuationSystem.UpdateEvacAgent(sim, agent);
        Assert.Null(agent.NavigationMemory.DetourGoal);
        Assert.True(agent.NavigationMemory.RecoveryRequested);
        Assert.True(agent.NavigationMemory.FailurePenalty(goal, sim.FrameIndex) > 0f);
    }

    [Fact]
    public void RisingLocalExposureDoesNotCancelAnImprovingRetreat()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Prepare(sim, agent.Position);
        Release(sim);
        Vector2 retreat = agent.Position - new Vector2(1.6f, 0f);
        var risk = sim.EvacuationSystem.SampleNaturalFumeRisk(sim, agent, retreat);
        Assert.True(risk.Start > 0.8f);
        Assert.True(risk.End < risk.Start);
        Assert.False(sim.EvacuationSystem.NaturalToxicDetourWorsened(sim, agent, retreat));
    }

    [Fact]
    public void UsefulDetourIsNotOverriddenByACompetingFumeSteeringGoal()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Prepare(sim, new Vector2(29.75f, 17.25f));
        Release(sim);
        agent.NavigationMemory.DetourGoal = agent.Position + Vector2.UnitY;
        Vector2 result = sim.EvacuationSystem.ApplyNaturalToxicAvoidance(sim, agent, Vector2.UnitY, false);
        Assert.Equal(Vector2.UnitY, result);
        Assert.False(agent.NavigationMemory.RecoveryRequested);
    }

    [Fact]
    public void WorseningFumeCanChangeExitWithoutWaitingForAStuckTimer()
    {
        Simulation sim = Scene(1);
        Assert.True(sim.Environment.AddExitFromClick(0.3f, 17f, 4));
        Agent agent = sim.Agents[0];
        Exit initial = agent.ExitTarget!;
        ToxicFumeConfig fume = sim.ToxicFumeSystem.Configuration;
        fume.SetValues(5f, fume.DispersionSpeedMetersPerSecond, fume.MaximumExtentMeters,
            fume.ReleaseDurationSeconds, fume.DriftDirectionDegrees, fume.DriftBias,
            fume.AwarenessConcentration, fume.DangerConcentration,
            fume.ImpairmentExposure, fume.IncapacitationExposure);
        Prepare(sim, new Vector2(34.25f, 17.25f));
        Release(sim);
        Assert.Equal(0f, agent.NavigationMemory.NoProgressSeconds);
        sim.EvacuationSystem.MaybeReselectExit(sim, agent);
        Assert.NotSame(initial, agent.ExitTarget);
        Assert.Equal(1, agent.NavigationMemory.ExitChanges);
        Assert.Null(agent.CurrentRouteWaypoint);
        Exit selected = agent.ExitTarget!;
        for (int i = 0; i < 10; i++) sim.EvacuationSystem.MaybeReselectExit(sim, agent);
        Assert.Same(selected, agent.ExitTarget);
        Assert.Equal(1, agent.NavigationMemory.ExitChanges);
    }

    [Fact]
    public void HybridOpenSpaceMovementStillChecksEachAgentsFumeHazard()
    {
        Simulation sim = Scene(1, hybrid: true);
        Agent agent = sim.Agents[0];
        Prepare(sim, new Vector2(29.75f, 17.25f));
        Release(sim);
        sim.HybridMovement.BeginFrame(sim);
        Assert.True(sim.HybridMovement.TryAdvance(sim, agent));
        Assert.True(agent.NavigationMemory.RecoveryRequested);
        Assert.True(agent.LastDesiredDirection.X < 0f);
    }

    [Fact]
    public void ToxicFumeDoesNotReuseStaleFlyingFurnitureAvoidance()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        agent.NavigationMemory.FurnitureAvoidance = Vector2.UnitY;
        agent.NavigationMemory.NextHazardCheckFrame = 100;
        Assert.Equal(Vector2.UnitX,
            sim.EvacuationSystem.ApplyNaturalMovingFurnitureAvoidance(sim, agent, Vector2.UnitX));
    }

    [Fact]
    public void IncapacitationCancelsQueuedRecoveryWithoutMovingTheBody()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Prepare(sim, new Vector2(45f, 28f));
        sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, true);
        agent.MarkIncapacitated();
        Vector2 start = agent.Position;
        Release(sim);
        sim.Step();
        sim.Step();
        Assert.Equal(AgentState.Incapacitated, agent.State);
        Assert.Equal(start, agent.Position);
        Assert.Equal(0, agent.NavigationMemory.TotalRecoveryAttempts);
        Assert.Equal(0, sim.EvacuationSystem.NaturalPendingRecoveries);
    }

    [Fact]
    public void ToxicRecoveryDeduplicatesSixHundredRequests()
    {
        Simulation sim = Scene(600);
        foreach (Agent agent in sim.Agents)
        {
            sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, true);
            sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, true);
        }
        Assert.Equal(600, sim.EvacuationSystem.NaturalPendingRecoveries);
    }

    [Fact]
    public void ToxicRecoveryRespectsTheBudgetAndServesLaterAgentIds()
    {
        Simulation sim = Scene(8, new AgentConfig { NaturalRecoveryProbesPerStep = 2 });
        Prepare(sim, new Vector2(45f, 28f));
        Vector2[] anchors = sim.Agents.Select(a => a.Position).ToArray();
        foreach (Agent agent in sim.Agents) sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, true);
        Release(sim);
        for (int frame = 0; frame < 7; frame++)
        {
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                sim.Agents[i].Position = anchors[i];
                sim.Agents[i].Velocity = Vector2.Zero;
            }
            sim.Step();
            Assert.InRange(sim.EvacuationSystem.LastNaturalRecoveryProbes, 0, 2);
            Assert.InRange(sim.EvacuationSystem.LastNaturalRoutePlans, 0, sim.Config.Agent.NaturalRoutePlansPerStep);
        }
        Assert.All(sim.Agents, a => Assert.True(a.NavigationMemory.TotalRecoveryAttempts > 0));
    }

    [Fact]
    public void ToxicRecoveryCanRetreatFromADeadEndWithoutTeleportation()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        agent.Position = new Vector2(12f, 10f);
        agent.NavigationMemory.RecoveryAttempts = 3;
        sim.Environment.Blockages.Add(new Wall(12.6f, 8.8f, 13f, 11.2f));
        sim.Environment.Blockages.Add(new Wall(9f, 8.8f, 13f, 9.1f));
        sim.Environment.Blockages.Add(new Wall(9f, 10.9f, 13f, 11.2f));
        sim.Environment.RebuildWalls();
        sim.BuildWallGrid();
        Prepare(sim, new Vector2(45f, 28f));
        Release(sim);
        Vector2 before = agent.Position;
        Assert.True(sim.EvacuationSystem.TryChooseNaturalRecoveryGoal(sim, agent, out Vector2 goal));
        Assert.True(goal.X < before.X);
        Assert.Equal(before, agent.Position);
        Assert.True(sim.HasTraversablePath(before, goal, sim.Config.Agent.Radius));
    }

    [Fact]
    public void SameSeedReproducesToxicRecoveryDecisions()
    {
        Simulation left = Scene(5);
        Simulation right = Scene(5);
        foreach (Simulation sim in new[] { left, right })
        {
            Prepare(sim, new Vector2(29.75f, 17.25f));
            Release(sim);
        }
        for (int frame = 0; frame < 80; frame++) { left.Step(); right.Step(); }
        for (int i = 0; i < left.Agents.Count; i++)
        {
            Assert.Equal(left.Agents[i].Position, right.Agents[i].Position);
            Assert.Equal(left.Agents[i].NavigationMemory.TotalRecoveryAttempts,
                right.Agents[i].NavigationMemory.TotalRecoveryAttempts);
            Assert.Equal(left.Agents[i].ExitTarget?.Name, right.Agents[i].ExitTarget?.Name);
        }
    }

    private static Simulation Scene(int count, AgentConfig? agentConfig = null,
        ScenarioType scenario = ScenarioType.ToxicFume, bool hybrid = false)
    {
        Simulation sim = new(config: new SimulationConfig
        {
            Agent = agentConfig ?? new AgentConfig(),
            HybridEnabled = hybrid,
        }, randomSeed: 4173);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width - 0.3f, 17f, 4));
        sim.SetScenarioType(scenario);
        List<Vector2> positions = new(count);
        for (int i = 0; i < count; i++)
            positions.Add(new Vector2(28.25f + (i % 30) * 0.65f, 17.25f + (i / 30) * 0.65f));
        sim.SpawnAgentsAtPositions(positions);
        foreach (Agent agent in sim.Agents)
        {
            agent.State = AgentState.Evacuating;
            agent.Posture = SeatingPosture.Walking;
            agent.Intent = BehaviorIntent.Evacuate;
            agent.ExitTarget = sim.Environment.Exits[0];
            agent.FollowTendency = 0f;
            agent.HelpingTendency = 0f;
            agent.AssistanceDecisionMade = true;
        }
        return sim;
    }

    private static void Prepare(Simulation sim, Vector2 source)
    {
        Assert.True(sim.ToxicFumeSystem.TrySetSource(sim, source));
        sim.PrecomputeToxicFume();
    }

    private static void Release(Simulation sim)
    {
        sim.SetRunning(true);
        Assert.True(sim.Running);
        Assert.True(sim.TriggerToxicFumeRelease());
    }
}
