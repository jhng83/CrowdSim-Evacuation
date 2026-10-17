using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Regression specifications for bounded, persistent, physically constrained recovery.</summary>
public sealed class NaturalNavigationTests
{
    [Fact]
    public void ReplanningOrChangingTargetsDoesNotPretendStationaryAgentMadeProgress()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        Vector2 position = agent.Position;
        for (int frame = 0; frame <= 30; frame += 2)
        {
            agent.SetRoute(new[] { new Vector2(30f + frame % 3, 10f) }, "exit:test", frame + 100);
            agent.NavigationMemory.Observe(position, agent.CurrentRouteWaypoint!.Value, frame, 0.15f);
            agent.ClearRoute();
        }
        Assert.True(agent.NavigationMemory.NoProgressSeconds >= 4.4f);
    }

    [Fact]
    public void UsefulPhysicalProgressResetsTheClock()
    {
        NaturalNavigationMemory memory = new();
        Vector2 target = new(10f, 0f);
        memory.Observe(Vector2.Zero, target, 0, 0.15f);
        memory.Observe(Vector2.Zero, target, 20, 0.15f);
        Assert.True(memory.NoProgressSeconds >= 3f);
        memory.Observe(new Vector2(0.2f, 0f), target, 22, 0.15f);
        Assert.Equal(0f, memory.NoProgressSeconds);
    }

    [Fact]
    public void DecreasingRouteCostRecognisesLegitimateMovementAwayFromExit()
    {
        NaturalNavigationMemory memory = new();
        for (int frame = 0; frame <= 20; frame += 2)
        {
            memory.Observe(new Vector2(-frame * 0.1f, 0f), new Vector2(10f, 0f), frame, 0.15f,
                remainingFieldCost: 30f - frame * 0.2f, fieldVersion: 1);
        }
        Assert.InRange(memory.NoProgressSeconds, 0f, 0.31f);
    }

    [Fact]
    public void FullOrbitIsDetectedEvenWhenSomeSamplesMoveCloserToExit()
    {
        NaturalNavigationMemory memory = new();
        for (int i = 0; i <= 18; i++)
        {
            float angle = i * MathF.Tau / 18f;
            memory.Observe(new Vector2(MathF.Cos(angle), MathF.Sin(angle)),
                new Vector2(10f, 0f), i * 2, 0.15f);
        }
        Assert.True(memory.LoopDetected);
    }

    [Fact]
    public void FailedLocationsAreRememberedButEventuallyExpire()
    {
        NaturalNavigationMemory memory = new();
        memory.RememberFailure(new Vector2(4f, 5f), 20);
        Assert.True(memory.FailurePenalty(new Vector2(4f, 5f), 10) > 0.9f);
        Assert.Equal(0f, memory.FailurePenalty(new Vector2(10f, 5f), 10));
        Assert.Equal(0f, memory.FailurePenalty(new Vector2(4f, 5f), 21));
    }

    [Theory]
    [InlineData(0f, 0f, 3f, 0f, 1.5f, 0f, 0.5f, false)]
    [InlineData(0f, 0f, 3f, 0f, 1.5f, 1f, 0.5f, true)]
    [InlineData(0.4f, 0f, 1.2f, 0f, 0f, 0f, 0.5f, true)]
    [InlineData(0.4f, 0f, -1.2f, 0f, 0f, 0f, 0.5f, false)]
    public void SweptClearanceNeverCutsThroughBodyButAllowsSeparation(
        float sx, float sy, float ex, float ey, float cx, float cy, float radius, bool expected)
    {
        Assert.Equal(expected, EvacuationSystem.SeparatingOrClearSegment(
            new Vector2(sx, sy), new Vector2(ex, ey), new Vector2(cx, cy), radius));
    }

    [Fact]
    public void DeadEndCanChooseSidewaysOrBackwardEscapeRatherThanOnlyExitwardMotion()
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
        Vector2 before = agent.Position;
        Assert.True(sim.EvacuationSystem.TryChooseNaturalRecoveryGoal(sim, agent, out Vector2 goal));
        Assert.True(goal.X < before.X, $"Expected retreat through the open left side, got {goal}.");
        Assert.Equal(before, agent.Position); // planning must never teleport the pedestrian
        Assert.True(sim.HasTraversablePath(before, goal, sim.Config.Agent.Radius));
    }

    [Fact]
    public void SealedPocketDoesNotInventAnEscapeThroughWalls()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        agent.Position = new Vector2(10f, 10f);
        sim.Environment.Blockages.Add(new Wall(9.4f, 9.4f, 9.7f, 10.6f));
        sim.Environment.Blockages.Add(new Wall(10.3f, 9.4f, 10.6f, 10.6f));
        sim.Environment.Blockages.Add(new Wall(9.4f, 9.4f, 10.6f, 9.7f));
        sim.Environment.Blockages.Add(new Wall(9.4f, 10.3f, 10.6f, 10.6f));
        sim.Environment.RebuildWalls();
        Assert.False(sim.EvacuationSystem.TryChooseNaturalRecoveryGoal(sim, agent, out _));
        Assert.Equal(new Vector2(10f, 10f), agent.Position);
    }

    [Fact]
    public void MovingObstacleInvalidatesACommittedDetour()
    {
        Simulation sim = Scene(1);
        Agent agent = sim.Agents[0];
        agent.Position = new Vector2(10f, 10f);
        agent.NavigationMemory.DetourGoal = new Vector2(12f, 10f);
        agent.NavigationMemory.DetourUntilFrame = 100;
        agent.NavigationMemory.DetourBestDistance = 2f;
        sim.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(11f, 10f), 0.35f));
        sim.EvacuationSystem.UpdateEvacAgent(sim, agent);
        Assert.Null(agent.NavigationMemory.DetourGoal);
        Assert.True(agent.NavigationMemory.RecoveryRequested);
        Assert.True(agent.NavigationMemory.FailurePenalty(new Vector2(12f, 10f), sim.FrameIndex) > 0f);
    }

    [Fact]
    public void NaturalModeDoesNotPollUnseenRemoteDoorwayCongestion()
    {
        Simulation sim = Scene(3);
        Assert.True(sim.Environment.AddExitFromClick(0.3f, 10f, 3));
        Agent agent = sim.Agents[0];
        Exit original = agent.ExitTarget!;
        agent.Position = new Vector2(28f, 20f);
        sim.Agents[1].Position = original.Position + new Vector2(-0.6f, 0f);
        sim.Agents[1].MarkCrawling();
        sim.EvacuationSystem.MaybeReselectExit(sim, agent);
        Assert.Same(original, agent.ExitTarget);
    }

    [Fact]
    public void PersistentFailedSolutionsEventuallyReconsiderAnotherKnownExit()
    {
        Simulation sim = Scene(1);
        Assert.True(sim.Environment.AddExitFromClick(0.3f, 10f, 3));
        Agent agent = sim.Agents[0];
        Vector2 anchor = new(28f, 10f);
        agent.Position = anchor;
        sim.SetRunning(true);
        // An external constraint holds this test pedestrian in place. We test decision
        // escalation, not evacuation time or a fictitious physical escape guarantee.
        for (int i = 0; i < 90; i++)
        {
            agent.Position = anchor;
            agent.Velocity = Vector2.Zero;
            sim.Step();
        }
        Assert.True(agent.NavigationMemory.TotalRecoveryAttempts >= 2);
        Assert.True(agent.NavigationMemory.ExitChanges >= 1);
    }

    [Fact]
    public void RecoveryQueueIsBoundedAndDoesNotDuplicate600Requests()
    {
        Simulation sim = Scene(600);
        foreach (Agent agent in sim.Agents)
        {
            sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, force: true);
            sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, force: true);
        }
        Assert.Equal(600, sim.EvacuationSystem.NaturalPendingRecoveries);
    }

    [Fact]
    public void RecoveryBudgetServesHighIdsAsWellAsLowIds()
    {
        Simulation sim = Scene(8, new AgentConfig { NaturalRecoveryProbesPerStep = 2 });
        Vector2[] initial = sim.Agents.Select(a => a.Position).ToArray();
        foreach (Agent agent in sim.Agents) sim.EvacuationSystem.RequestNaturalRecovery(sim, agent, force: true);
        sim.SetRunning(true);
        for (int frame = 0; frame < 7; frame++)
        {
            for (int i = 0; i < sim.Agents.Count; i++)
            {
                sim.Agents[i].Position = initial[i];
                sim.Agents[i].Velocity = Vector2.Zero;
            }
            sim.Step();
            Assert.InRange(sim.EvacuationSystem.LastNaturalRecoveryProbes, 0, 2);
            Assert.InRange(sim.EvacuationSystem.LastNaturalRoutePlans, 0, sim.Config.Agent.NaturalRoutePlansPerStep);
        }
        Assert.All(sim.Agents, agent => Assert.True(agent.NavigationMemory.TotalRecoveryAttempts > 0));
    }

    [Fact]
    public void SameSeedAndLayoutReproduceAdaptiveDecisions()
    {
        Simulation left = Scene(5);
        Simulation right = Scene(5);
        foreach (Simulation sim in new[] { left, right })
        {
            sim.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(13f, 10f), 1.2f));
            sim.SetRunning(true);
        }
        for (int frame = 0; frame < 90; frame++)
        {
            left.Step();
            right.Step();
        }
        for (int i = 0; i < left.Agents.Count; i++)
        {
            Assert.Equal(left.Agents[i].Position, right.Agents[i].Position);
            Assert.Equal(left.Agents[i].NavigationMemory.TotalRecoveryAttempts,
                right.Agents[i].NavigationMemory.TotalRecoveryAttempts);
        }
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)]
    [InlineData(ScenarioType.ToxicFume)]
    public void RecoveryIsDisabledByTheReferenceToggleInEitherScenario(ScenarioType scenario)
    {
        Simulation legacy = Scene(1, new AgentConfig { NaturalNavigationEnabled = false });
        legacy.SetScenarioType(scenario);
        legacy.EvacuationSystem.RequestNaturalRecovery(legacy, legacy.Agents[0], force: true);
        Assert.Equal(0, legacy.EvacuationSystem.NaturalPendingRecoveries);
    }

    private static Simulation Scene(int count, AgentConfig? config = null)
    {
        Simulation sim = new(config: new SimulationConfig { Agent = config ?? new AgentConfig() }, randomSeed: 4173);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width - 0.3f, 10f, 4));
        List<Vector2> positions = new(count);
        for (int i = 0; i < count; i++) positions.Add(new Vector2(10f + (i % 30) * 0.65f, 10f + (i / 30) * 0.65f));
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
}
