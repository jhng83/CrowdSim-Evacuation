using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;
using Xunit;

namespace CrowdSim.Tests;

public sealed class BehaviorRebuildTests
{
    [Fact]
    public void PanicDecay_IsTimeBasedAndRemainsVisibleForSeveralSeconds()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1601);
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(27.9286f, 28.3333f);
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.Evacuate;
        agent.ExitTarget = simulation.Environment.Exits.Single();
        agent.PanicLevel = 0.80f;

        simulation.SetRunning(true);
        for (int i = 0; i < 20; i++)
        {
            simulation.Step();
        }

        Assert.InRange(agent.PanicLevel, 0.45f, 0.70f);
        Assert.InRange(agent.LastSocialPanicStimulus, 0f, 0.0001f);
        Assert.InRange(agent.LastRunningPanicStimulus, 0f, 0.0001f);
    }

    [Fact]
    public void DirectlyExposedAgent_CannotBeRelayedSameEpisodeAfterDirectWindowExpires()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1610);
        Agent agent = simulation.Agents.Single();

        agent.ActivateDirectAlarm(
            currentFrame: 0,
            durationFrames: 2,
            panicFloor: 0.50f,
            episodeId: 1610);
        agent.ExpireAlarmIfNeeded(3);

        Assert.False(agent.IsActiveAlarmSource(3));
        Assert.False(agent.TryActivateRelayedAlarm(
            currentFrame: 3,
            durationFrames: 10,
            panicFloor: 0.45f,
            episodeId: 1610));
    }

    [Fact]
    public void PersistentSocialAlarm_ApproachesBoundedStressInsteadOfSaturating()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1608);
        Exit exit = simulation.Environment.Exits.Single();
        Agent observer = simulation.Agents[0];
        Agent source = simulation.Agents[1];

        observer.Position = new Vector2(24.2857f, 17f);
        observer.State = AgentState.Evacuating;
        observer.Intent = BehaviorIntent.Evacuate;
        observer.ExitTarget = exit;
        observer.PanicLevel = 0.45f;

        source.Position = new Vector2(25.0952f, 17f);
        source.State = AgentState.Evacuating;
        source.Intent = BehaviorIntent.Evacuate;
        source.ExitTarget = exit;
        source.PanicLevel = 0.50f;
        source.ActivateDirectAlarm(
            simulation.FrameIndex,
            durationFrames: 40,
            panicFloor: 0.50f,
            episodeId: 1608);

        simulation.SetRunning(true);
        for (int i = 0; i < 10; i++)
        {
            simulation.Step();
        }

        Assert.True(observer.LastSocialPanicStimulus > 0.40f);
        Assert.InRange(observer.PanicLevel, 0.45f, 0.65f);
    }

    [Fact]
    public void DirectlyExposedMobileAgent_BeginsWithThreatEscapeIntent()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1602);
        Agent agent = simulation.Agents.Single();
        agent.State = AgentState.Evacuating;
        agent.ShockwaveHit = true;
        agent.BlastShielded = false;
        simulation.Environment.SetExplosion(27.93f, 17.00f, simulation.FrameIndex);

        simulation.EvacuationSystem.BeginPurposefulResponse(simulation, agent);

        Assert.Equal(BehaviorIntent.ThreatEscape, agent.Intent);
        Assert.True(agent.IntentUntilFrame > simulation.FrameIndex);
    }

    [Fact]
    public void DirectFront_CanSwitchAlreadyEvacuatingAgentToThreatEscape()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1609);
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(38.4524f, 17f);
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.Evacuate;
        agent.ExitTarget = simulation.Environment.Exits.Single();

        simulation.SetRunning(true);
        simulation.TriggerExplosion(27.93f, 17.00f);
        simulation.Debris.Clear();

        for (int i = 0; i < 12 && !agent.ShockwaveHit; i++)
        {
            simulation.Step();
        }

        Assert.True(agent.ShockwaveHit);
        Assert.Equal(BehaviorIntent.ThreatEscape, agent.Intent);
    }

    [Fact]
    public void HighHelpingTrait_CanAttendNearbyCasualtyWithoutChangingCasualtyOutcome()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1603);
        Exit exit = simulation.Environment.Exits.Single();
        Agent helper = simulation.Agents[0];
        Agent casualty = simulation.Agents[1];

        helper.Position = new Vector2(24.2857f, 17f);
        helper.State = AgentState.Evacuating;
        helper.Intent = BehaviorIntent.FollowCrowd;
        helper.IntentUntilFrame = 0;
        helper.ExitTarget = exit;
        helper.HelpingTendency = 1f;
        helper.PanicLevel = 0.10f;

        casualty.Position = new Vector2(25.7024f, 17f);
        casualty.MarkFallen(simulation.FrameIndex, simulation.Random);

        simulation.SetRunning(true);
        simulation.Step();

        Assert.Equal(BehaviorIntent.AssistCasualty, helper.Intent);
        Assert.Equal(casualty.AgentId, helper.AssistTargetAgentId);
        Assert.True(helper.AssistAssignedFrame >= 0);
        Assert.True(helper.AssistLastProgressFrame >= 0);
        Assert.True(float.IsFinite(helper.AssistBestDistanceMeters));
        Assert.InRange(helper.AssistSlotIndex, 0, 1);
        Assert.Equal(AgentState.Fallen, casualty.State);
    }

    [Fact]
    public void TwoHelpers_AssignedToSameCasualty_UseOppositeAssistanceSlots()
    {
        Simulation simulation = CreateReadySimulation(3, seed: 1611);
        Exit exit = simulation.Environment.Exits.Single();
        Agent first = simulation.Agents[0];
        Agent second = simulation.Agents[1];
        Agent casualty = simulation.Agents[2];

        first.Position = new Vector2(24.0f, 16.4f);
        second.Position = new Vector2(24.0f, 17.6f);
        foreach (Agent helper in new[] { first, second })
        {
            helper.State = AgentState.Evacuating;
            helper.Intent = BehaviorIntent.FollowCrowd;
            helper.IntentUntilFrame = 0;
            helper.ExitTarget = exit;
            helper.HelpingTendency = 1f;
            helper.PanicLevel = 0.05f;
        }

        casualty.Position = new Vector2(25.4f, 17.0f);
        casualty.ExitTarget = exit;
        casualty.MarkFallen(simulation.FrameIndex, simulation.Random);

        simulation.SetRunning(true);
        simulation.Step();

        Assert.Equal(BehaviorIntent.AssistCasualty, first.Intent);
        Assert.Equal(BehaviorIntent.AssistCasualty, second.Intent);
        Assert.Equal(casualty.AgentId, first.AssistTargetAgentId);
        Assert.Equal(casualty.AgentId, second.AssistTargetAgentId);
        Assert.NotEqual(first.AssistSlotIndex, second.AssistSlotIndex);
    }

    [Fact]
    public void BlockedHelper_AbandonsAssistanceAfterBoundedNoProgressWindow()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1612);
        Exit exit = simulation.Environment.Exits.Single();
        Agent helper = simulation.Agents[0];
        Agent casualty = simulation.Agents[1];

        helper.Position = new Vector2(10.0f, 10.0f);
        helper.State = AgentState.Evacuating;
        helper.Intent = BehaviorIntent.AssistCasualty;
        helper.ExitTarget = exit;
        helper.AssistanceDecisionMade = true;

        casualty.Position = new Vector2(12.0f, 10.0f);
        casualty.ExitTarget = exit;
        casualty.MarkFallen(simulation.FrameIndex, simulation.Random);

        helper.AssistTargetAgentId = casualty.AgentId;
        helper.AssistSlotIndex = 0;
        helper.AssistAssignedFrame = simulation.FrameIndex;
        helper.AssistLastProgressFrame = simulation.FrameIndex;
        helper.AssistProgressAnchorPosition = helper.Position;
        helper.AssistBestDistanceMeters = Vector2.Distance(helper.Position, casualty.Position);

        // Enclose the helper with body-clear static walls so it cannot make physical progress
        // toward either assistance side. This exercises the bounded recovery/abandon path.
        simulation.Environment.Blockages.Add(new Wall(9.35f, 9.35f, 9.45f, 10.65f));
        simulation.Environment.Blockages.Add(new Wall(10.55f, 9.35f, 10.65f, 10.65f));
        simulation.Environment.Blockages.Add(new Wall(9.35f, 9.35f, 10.65f, 9.45f));
        simulation.Environment.Blockages.Add(new Wall(9.35f, 10.55f, 10.65f, 10.65f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();

        simulation.SetRunning(true);

        // The helper can still make a small amount of genuine goal-distance progress inside
        // the enclosure and may try the alternate side at the 6-second recovery stage. The
        // contract is therefore bounded *no-progress*, not a fixed 10-second assignment
        // lifetime. Give the complete retry/alternate-side sequence time to settle while
        // still proving that the helper cannot remain assigned indefinitely.
        float maximumObservedApproachSeconds =
            simulation.Config.Agent.AssistanceAbandonNoProgressSeconds +
            simulation.Config.Agent.AssistanceAlternateSideNoProgressSeconds +
            4.0f;
        int steps = (int)MathF.Ceiling(maximumObservedApproachSeconds / simulation.Config.Agent.Dt);
        for (int i = 0; i < steps && helper.Intent == BehaviorIntent.AssistCasualty; i++)
        {
            simulation.Step();
        }

        Assert.NotEqual(BehaviorIntent.AssistCasualty, helper.Intent);
        Assert.Null(helper.AssistTargetAgentId);
        Assert.Equal(-1, helper.AssistSlotIndex);
    }

    [Fact]
    public void VisibilityGraphPlanner_FindsBodyClearRouteAroundBlockage()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 1604);
        float eastX = simulation.Config.World.WidthMeters - simulation.Environment.BoundaryThickness / 2f;
        Assert.True(simulation.Environment.AddExitFromClick(eastX, 12.14f, personsWide: 3));
        simulation.Environment.Blockages.Add(new Wall(21.8571f, 8.0952f, 23.4762f, 17.8095f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();

        Vector2 start = new(20.2381f, 12.1429f);
        Exit exit = simulation.Environment.Exits.Single();
        float clearance = simulation.Config.Agent.Radius *
            simulation.Config.Agent.NavigationClearanceFactor;

        Assert.False(EvacuationSystem.HasTraversablePath(
            start,
            exit.Position,
            simulation.Environment.Walls,
            clearance));

        IReadOnlyList<Vector2> route = simulation.EvacuationSystem.PlanBodyClearRoute(
            simulation,
            start,
            exit.Position);

        Assert.True(route.Count >= 2);
        Vector2 previous = start;
        foreach (Vector2 waypoint in route)
        {
            Assert.True(EvacuationSystem.HasTraversablePath(
                previous,
                waypoint,
                simulation.Environment.Walls,
                clearance));
            previous = waypoint;
        }

        Assert.Equal(exit.Position, route[^1]);
    }

    [Fact]
    public void EvacuationRouting_UsesWaypointBeforeLegacyExploration()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 1605);
        float eastX = simulation.Config.World.WidthMeters - simulation.Environment.BoundaryThickness / 2f;
        Assert.True(simulation.Environment.AddExitFromClick(eastX, 12.14f, personsWide: 3));
        simulation.Environment.Blockages.Add(new Wall(21.8571f, 8.0952f, 23.4762f, 17.8095f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();
        simulation.SpawnAgents(1);

        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(20.2381f, 12.1429f);
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.Evacuate;
        agent.ExitTarget = simulation.Environment.Exits.Single();
        agent.PanicLevel = 0.20f;

        simulation.EvacuationSystem.UpdateEvacAgent(simulation, agent);

        Assert.False(agent.Exploring);
        Assert.True(agent.CurrentRouteWaypoint is not null ||
            (simulation.HybridMovement.Fields.FieldBuilds > 0 && agent.NavigationMemory.GuidancePoint is not null),
            "Use static waypoints or shared exit guidance before legacy random exploration.");
        Assert.True(agent.LastDesiredDirection.LengthSquared() > 0.1f);
    }

    [Fact]
    public void CrawlingAgent_DoesNotActAsHerdMovementLeader()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1613);
        Exit exit = simulation.Environment.Exits.Single();
        Agent observer = simulation.Agents[0];
        Agent crawler = simulation.Agents[1];

        observer.Position = new Vector2(28.0f, 20.0f);
        observer.State = AgentState.Evacuating;
        observer.Intent = BehaviorIntent.Evacuate;
        observer.ExitTarget = exit;

        crawler.Position = new Vector2(28.0f, 19.3f);
        crawler.ExitTarget = exit;
        crawler.MarkCrawling();
        crawler.Velocity = new Vector2(0.025f, 0f);

        Vector2 herd = simulation.EvacuationSystem.ComputeHerdDirection(simulation, observer);

        Assert.True(herd.LengthSquared() < 0.0001f);
        Assert.True(crawler.CanSignalPanic);
    }

    [Fact]
    public void MobileAgent_BehindSlowCrawler_ChoosesLateralBypassWhenSideSpaceIsClear()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 1614);
        Exit exit = simulation.Environment.Exits.Single();
        Agent pedestrian = simulation.Agents[0];
        Agent crawler = simulation.Agents[1];

        pedestrian.Position = new Vector2(28.0f, 20.0f);
        pedestrian.Velocity = Vector2.Zero;
        pedestrian.State = AgentState.Evacuating;
        pedestrian.Intent = BehaviorIntent.Evacuate;
        pedestrian.IntentUntilFrame = -1;
        pedestrian.ExitTarget = exit;
        pedestrian.HelpingTendency = 0f;
        pedestrian.FollowTendency = 0f;
        pedestrian.PanicLevel = 0f;

        crawler.Position = new Vector2(28.0f, 19.25f);
        crawler.Velocity = Vector2.Zero;
        crawler.ExitTarget = exit;
        crawler.MarkCrawling();
        crawler.PanicLevel = 0f;

        simulation.SetRunning(true);
        for (int i = 0; i < 10 && pedestrian.SlowObstacleBypassGoal is null; i++)
        {
            simulation.Step();
        }

        Assert.Equal(crawler.AgentId, pedestrian.SlowObstacleBypassAgentId);
        Assert.NotNull(pedestrian.SlowObstacleBypassGoal);
        Vector2 bypassGoal = pedestrian.SlowObstacleBypassGoal!.Value;
        Assert.True(MathF.Abs(bypassGoal.X - crawler.Position.X) >= 0.50f);
        Assert.True(MathF.Abs(pedestrian.LastDesiredDirection.X) > 0.10f);
    }

    [Fact]
    public void ThreatEscape_CasualtyCluster_ChoosesClearLocalDetourInsteadOfPressingForward()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 1615);
        Exit exit = simulation.Environment.Exits.Single();
        Agent pedestrian = simulation.Agents[0];
        Agent centreCrawler = simulation.Agents[1];
        Agent upperCrawler = simulation.Agents[2];
        Agent lowerCrawler = simulation.Agents[3];

        // Explosion is behind the pedestrian, so the nominal ThreatEscape direction is +X.
        // Three crawling casualties occupy the forward sector while lateral/back-side floor
        // remains open. The local bypass should therefore deviate materially from +X.
        simulation.Environment.SetExplosion(18.0f, 20.0f, simulation.FrameIndex);
        pedestrian.Position = new Vector2(20.0f, 20.0f);
        pedestrian.Velocity = Vector2.Zero;
        pedestrian.State = AgentState.Evacuating;
        pedestrian.Intent = BehaviorIntent.ThreatEscape;
        pedestrian.IntentUntilFrame = 100;
        pedestrian.ExitTarget = exit;
        pedestrian.FollowTendency = 0f;
        pedestrian.HelpingTendency = 0f;
        pedestrian.PanicLevel = 0.80f;

        centreCrawler.Position = new Vector2(20.55f, 20.00f);
        upperCrawler.Position = new Vector2(20.62f, 20.34f);
        lowerCrawler.Position = new Vector2(20.62f, 19.66f);
        foreach (Agent crawler in new[] { centreCrawler, upperCrawler, lowerCrawler })
        {
            crawler.Velocity = Vector2.Zero;
            crawler.ExitTarget = exit;
            crawler.MarkCrawling();
            crawler.PanicLevel = 0f;
        }

        simulation.EvacuationSystem.UpdateEvacAgent(simulation, pedestrian);

        Assert.Equal(centreCrawler.AgentId, pedestrian.SlowObstacleBypassAgentId);
        Assert.NotNull(pedestrian.SlowObstacleBypassGoal);
        Vector2 bypassDirection = Vector2.Normalize(
            pedestrian.SlowObstacleBypassGoal!.Value - new Vector2(20.0f, 20.0f));
        Assert.True(
            Vector2.Dot(bypassDirection, Vector2.UnitX) < 0.85f,
            $"Expected a meaningful local detour, got {bypassDirection}.");
        Assert.True(
            MathF.Abs(bypassDirection.Y) > 0.30f,
            $"Expected lateral clearance around the casualty cluster, got {bypassDirection}.");
    }

    [Fact]
    public void ThreatEscape_WhenForwardAndSidewaysAreBlocked_AllowsBoundedBackSideStep()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 1616);
        Exit exit = simulation.Environment.Exits.Single();
        Agent pedestrian = simulation.Agents[0];
        Agent centreCrawler = simulation.Agents[1];
        Agent upperSideCrawler = simulation.Agents[2];
        Agent lowerSideCrawler = simulation.Agents[3];

        simulation.Environment.SetExplosion(18.0f, 20.0f, simulation.FrameIndex);
        pedestrian.Position = new Vector2(20.0f, 20.0f);
        pedestrian.Velocity = Vector2.Zero;
        pedestrian.State = AgentState.Evacuating;
        pedestrian.Intent = BehaviorIntent.ThreatEscape;
        pedestrian.IntentUntilFrame = 100;
        pedestrian.ExitTarget = exit;
        pedestrian.FollowTendency = 0f;
        pedestrian.HelpingTendency = 0f;
        pedestrian.PanicLevel = 0.80f;

        // The centre body blocks shallow forward detours. Two additional casualties occupy
        // the +/-60 and +/-90 degree side corridors, leaving the bounded +/-120 degree
        // back-side candidates as the first body-clear local escape choices.
        centreCrawler.Position = new Vector2(20.55f, 20.00f);
        upperSideCrawler.Position = new Vector2(20.22f, 20.55f);
        lowerSideCrawler.Position = new Vector2(20.22f, 19.45f);
        foreach (Agent crawler in new[] { centreCrawler, upperSideCrawler, lowerSideCrawler })
        {
            crawler.Velocity = Vector2.Zero;
            crawler.ExitTarget = exit;
            crawler.MarkCrawling();
            crawler.PanicLevel = 0f;
        }

        simulation.EvacuationSystem.UpdateEvacAgent(simulation, pedestrian);

        Assert.NotNull(pedestrian.SlowObstacleBypassGoal);
        Vector2 bypassDirection = Vector2.Normalize(
            pedestrian.SlowObstacleBypassGoal!.Value - new Vector2(20.0f, 20.0f));
        Assert.True(
            Vector2.Dot(bypassDirection, Vector2.UnitX) < -0.10f,
            $"Expected a bounded back-side step when the forward hemisphere is blocked, got {bypassDirection}.");
        Assert.True(
            MathF.Abs(bypassDirection.Y) > 0.50f,
            $"Expected the back-step to remain lateral rather than reverse directly toward the blast, got {bypassDirection}.");
    }

    [Fact]
    public void PurposefulEvacuation_RemainsBriskAfterAcutePanicSubsides()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 1606);
        Agent agent = simulation.Agents.Single();
        agent.State = AgentState.Evacuating;
        agent.Intent = BehaviorIntent.Evacuate;
        agent.PanicLevel = 0f;
        agent.UrgencyBias = 1f;

        Assert.InRange(
            MathF.Abs(
                agent.CurrentDesiredSpeed -
                agent.BaseSpeed * simulation.Config.Agent.EvacuationSpeedFloorMultiplier),
            0f,
            0.0001f);
        Assert.True(agent.CurrentDesiredSpeed >= agent.BaseSpeed * 1.8f);
    }

    [Fact]
    public void SameSeed_ReproducesBehaviorTraits()
    {
        Simulation first = new(agentCount: 6, randomSeed: 1607);
        Simulation second = new(agentCount: 6, randomSeed: 1607);

        for (int i = 0; i < first.Agents.Count; i++)
        {
            Agent a = first.Agents[i];
            Agent b = second.Agents[i];
            Assert.Equal(a.ThreatSensitivity, b.ThreatSensitivity);
            Assert.Equal(a.FollowTendency, b.FollowTendency);
            Assert.Equal(a.HelpingTendency, b.HelpingTendency);
            Assert.Equal(a.RouteFamiliarity, b.RouteFamiliarity);
            Assert.Equal(a.UrgencyBias, b.UrgencyBias);
            Assert.Equal(a.StressRecoveryBias, b.StressRecoveryBias);
            Assert.Equal(a.PersonalSpaceBias, b.PersonalSpaceBias);
            Assert.Equal(a.Assertiveness, b.Assertiveness);
            Assert.Equal(a.PatienceBias, b.PatienceBias);
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
