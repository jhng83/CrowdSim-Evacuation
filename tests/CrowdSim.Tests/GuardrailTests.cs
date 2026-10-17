using System.Numerics;
using Xunit;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;

namespace CrowdSim.Tests;

public sealed class GuardrailTests
{
    [Fact]
    public void SfmForce_IsFiniteAndPairRepulsionIsSoftCapped()
    {
        Simulation simulation = new(agentCount: 2, randomSeed: 42);
        Agent a = simulation.Agents[0];
        Agent b = simulation.Agents[1];
        a.Position = new Vector2(20.2381f, 16.1905f);
        b.Position = new Vector2(20.2385f, 16.1905f);
        a.Velocity = Vector2.Zero;
        b.Velocity = Vector2.Zero;
        a.ExitTarget = new Exit("test", new Vector2(24.2857f, 16.1905f), 0.40f, "E");

        Vector2 force = simulation.SfmEngine.ComputeForces(simulation, a, Vector2.Zero);

        Assert.True(VectorMath.IsFinite(force));
        Assert.True(force.Length() <= SfmEngine.MaxRepulsion + 5f);
    }

    [Fact]
    public void Shockwave_PropagatesBeforeApplyingPhysicalOutcome()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 5);
        Vector2 centre = new(27.9286f, 17f);
        Agent agent = simulation.Agents.Single();
        agent.Position = centre + new Vector2(4.0476f, 0f);

        simulation.SetRunning(true);
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear();

        simulation.ExplosionSystem.ApplyExplosionEffects(simulation);
        Assert.False(agent.ShockwaveHit);

        for (int i = 0; i < 5; i++)
        {
            simulation.Step();
        }

        Assert.True(agent.ShockwaveHit);
        Assert.Equal(AgentState.Fallen, agent.State);
    }

    [Fact]
    public void BlastZoneRadii_AreConfigurableAndRemainStrictlyOrdered()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 41);
        var config = simulation.Config.Agent;

        config.SetBlastZoneRadii(60f, 50f, 40f, 30f, 20f);

        Assert.Equal(60f, config.HardKillRadius);
        Assert.True(config.HeatIncapacityRadius > config.HardKillRadius);
        Assert.True(config.FallenRadius > config.HeatIncapacityRadius);
        Assert.True(config.InjuryRadius > config.FallenRadius);
        Assert.True(config.HeatRadius > config.InjuryRadius);

        config.ResetBlastZoneRadii();

        Assert.Equal(CrowdSim.Core.Configuration.AgentConfig.DefaultHardKillRadius, config.HardKillRadius);
        Assert.Equal(CrowdSim.Core.Configuration.AgentConfig.DefaultHeatIncapacityRadius, config.HeatIncapacityRadius);
        Assert.Equal(CrowdSim.Core.Configuration.AgentConfig.DefaultFallenRadius, config.FallenRadius);
        Assert.Equal(CrowdSim.Core.Configuration.AgentConfig.DefaultInjuryRadius, config.InjuryRadius);
        Assert.Equal(CrowdSim.Core.Configuration.AgentConfig.DefaultHeatRadius, config.HeatRadius);
    }

    [Fact]
    public void ExplosionZones_AreAppliedInOrderedNonOverlappingBands()
    {
        Simulation simulation = CreateReadySimulation(5, seed: 5);
        Vector2 centre = new(27.9286f, 17f);
        float[] distances = { 0.81f, 2.43f, 4.86f, 8.10f, 12.14f };
        for (int i = 0; i < distances.Length; i++)
        {
            simulation.Agents[i].Position = centre + new Vector2(distances[i], 0f);
        }

        simulation.SetRunning(true);
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear();

        for (int frame = 0; frame < 12; frame++)
        {
            simulation.Step();
        }

        Assert.Equal(AgentState.Dead, simulation.Agents[0].State);
        Assert.Equal(AgentState.TempIncap, simulation.Agents[1].State);
        Assert.Equal(AgentState.Fallen, simulation.Agents[2].State);
        Assert.True(simulation.Agents[3].State is AgentState.Injured or AgentState.WaitingToReact or AgentState.Evacuating);
        Assert.False(simulation.Agents[4].State is AgentState.Dead or AgentState.Incapacitated or AgentState.Fallen);
        Assert.True(simulation.Agents[4].PanicLevel >= 0.5f);
    }

    [Fact]
    public void BlastShielding_DetectsBlockingRectangle()
    {
        Wall shield = new(1.619f, -0.4048f, 2.4286f, 0.4048f);

        Assert.True(ExplosionSystem.BlastPathIsBlocked(
            new Vector2(0f, 0f),
            new Vector2(4.0476f, 0f),
            new[] { shield }));
        Assert.False(ExplosionSystem.BlastPathIsBlocked(
            new Vector2(0f, 1.2143f),
            new Vector2(4.0476f, 1.2143f),
            new[] { shield }));
    }

    [Fact]
    public void EnvironmentExit_IsARealHoleInBoundaryWall()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 3);
        Assert.True(simulation.Environment.AddExitFromClick(27.93f, 0.28f, 3));
        Exit exit = simulation.Environment.Exits.Single();

        Assert.False(simulation.Environment.IsPointBlocked(exit.Position));
        Assert.True(simulation.Environment.Walls.All(wall => !wall.Contains(exit.Position)));
    }

    [Fact]
    public void LineOfSight_IsBlockedByObstacleAndClearOtherwise()
    {
        Wall obstacle = new(1.619f, -0.4048f, 2.4286f, 0.4048f);

        Assert.False(EvacuationSystem.HasLineOfSight(
            new Vector2(0f, 0f),
            new Vector2(4.0476f, 0f),
            new[] { obstacle }));
        Assert.True(EvacuationSystem.HasLineOfSight(
            new Vector2(0f, 1.2143f),
            new Vector2(4.0476f, 1.2143f),
            new[] { obstacle }));
    }

    [Fact]
    public void MoveAgent_RecoversFromNonFiniteState()
    {
        Simulation simulation = new(agentCount: 1, randomSeed: 9);
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(float.NaN, float.PositiveInfinity);
        agent.Velocity = new Vector2(float.NaN, float.NegativeInfinity);

        simulation.ResolveSolidCollisions(agent);

        Assert.True(VectorMath.IsFinite(agent.Position));
        Assert.True(VectorMath.IsFinite(agent.Velocity));
    }

    [Fact]
    public void PanicLevel_IncreasesDesiredSfmDriveWithoutBypassingFiniteGuardrails()
    {
        Simulation simulation = new(agentCount: 1, randomSeed: 17);
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(27.9286f, 17f);
        agent.Velocity = Vector2.Zero;

        agent.PanicLevel = 0f;
        Vector2 calmForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.UnitX);

        agent.PanicLevel = 1f;
        agent.Velocity = Vector2.Zero;
        Vector2 panicForce = simulation.SfmEngine.ComputeForces(simulation, agent, Vector2.UnitX);

        Assert.True(VectorMath.IsFinite(calmForce));
        Assert.True(VectorMath.IsFinite(panicForce));
        Assert.True(panicForce.X > calmForce.X);
        float expectedDesiredSpeed = agent.BaseSpeed * (1f + simulation.Config.Agent.PanicSpeedGain);
        Assert.InRange(MathF.Abs(agent.CurrentDesiredSpeed - expectedDesiredSpeed), 0f, 0.0001f);
    }

    [Fact]
    public void CoherentRunningCrowd_CreatesPanicContagionSignal()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 21);
        Agent observer = simulation.Agents[0];
        observer.Position = new Vector2(24.2857f, 17f);
        observer.PanicLevel = 0f;
        observer.State = AgentState.Wandering;

        for (int i = 1; i < 4; i++)
        {
            Agent runner = simulation.Agents[i];
            runner.Position = new Vector2(24.8929f + i * 0.3238f, 17f + i * 0.0405f);
            runner.State = AgentState.Evacuating;
            runner.PanicLevel = 0.70f;
            runner.ActivateDirectAlarm(
                simulation.FrameIndex,
                durationFrames: 30,
                panicFloor: 0.50f,
                episodeId: 1001);
            runner.Velocity = Vector2.UnitX * (runner.BaseSpeed * 2.5f);
        }

        simulation.SetRunning(true);
        simulation.Step();

        Assert.True(observer.LastRunningPanicStimulus > 0.05f);
        Assert.True(observer.PanicLevel > 0f);
    }


    [Fact]
    public void CalmFastEvacuees_DoNotCreateRunningPanicWithoutActiveAlarm()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 26);
        Agent observer = simulation.Agents[0];
        observer.Position = new Vector2(24.2857f, 17f);
        observer.PanicLevel = 0f;
        observer.State = AgentState.Wandering;

        for (int i = 1; i < 4; i++)
        {
            Agent runner = simulation.Agents[i];
            runner.Position = new Vector2(24.8929f + i * 0.3238f, 17f + i * 0.0405f);
            runner.State = AgentState.Evacuating;
            runner.PanicLevel = 0.05f;
            runner.Velocity = Vector2.UnitX * (runner.BaseSpeed * 2.5f);
        }

        simulation.SetRunning(true);
        simulation.Step();

        Assert.InRange(observer.LastRunningPanicStimulus, 0f, 0.0001f);
        Assert.InRange(observer.PanicLevel, 0f, 0.0001f);
    }

    [Fact]
    public void RelayedAlarm_CannotReactivateFromSameEpisodeAfterExpiry()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 27);
        Agent agent = simulation.Agents.Single();

        Assert.True(agent.TryActivateRelayedAlarm(
            currentFrame: 0,
            durationFrames: 3,
            panicFloor: 0.30f,
            episodeId: 2001));
        Assert.True(agent.IsActiveAlarmSource(2));

        agent.ExpireAlarmIfNeeded(4);
        Assert.False(agent.IsActiveAlarmSource(4));
        Assert.False(agent.TryActivateRelayedAlarm(
            currentFrame: 4,
            durationFrames: 3,
            panicFloor: 0.30f,
            episodeId: 2001));
    }

    [Fact]
    public void ResidualLowPanic_DecaysInsteadOfMutuallyReAmplifying()
    {
        Simulation simulation = CreateReadySimulation(3, seed: 24);
        Exit exit = simulation.Environment.Exits.Single();

        for (int i = 0; i < simulation.Agents.Count; i++)
        {
            Agent agent = simulation.Agents[i];
            agent.Position = new Vector2(24.2857f + i * 0.3238f, 17f);
            agent.State = AgentState.Evacuating;
            agent.ExitTarget = exit;
            agent.PanicLevel = 0.18f;
            agent.Velocity = Vector2.Zero;
        }

        simulation.SetRunning(true);
        simulation.Step();

        foreach (Agent agent in simulation.Agents)
        {
            Assert.True(agent.PanicLevel < 0.18f);
            Assert.InRange(agent.LastSocialPanicStimulus, 0f, 0.0001f);
            Assert.InRange(agent.LastRunningPanicStimulus, 0f, 0.0001f);
        }
    }

    [Fact]
    public void MeaningfullyPanickedNeighbour_CanStillRaiseSocialPanic()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 25);
        Exit exit = simulation.Environment.Exits.Single();
        Agent observer = simulation.Agents[0];
        Agent source = simulation.Agents[1];

        observer.Position = new Vector2(24.2857f, 17f);
        observer.State = AgentState.Evacuating;
        observer.ExitTarget = exit;
        observer.PanicLevel = 0.05f;
        observer.Velocity = Vector2.Zero;

        source.Position = new Vector2(25.0952f, 17f);
        source.State = AgentState.Evacuating;
        source.ExitTarget = exit;
        source.PanicLevel = 0.80f;
        source.Velocity = Vector2.Zero;
        source.ActivateDirectAlarm(
            simulation.FrameIndex,
            durationFrames: 30,
            panicFloor: 0.50f,
            episodeId: 1002);

        simulation.SetRunning(true);
        simulation.Step();

        Assert.True(observer.LastSocialPanicStimulus > 0.50f);
        Assert.True(observer.PanicLevel > 0.30f);
    }

    [Fact]
    public void RelayedSocialAlarm_TriggersReactionAndMeaningfulPanic()
    {
        Simulation simulation = CreateReadySimulation(2, seed: 29);
        Agent observer = simulation.Agents[0];
        Agent source = simulation.Agents[1];

        observer.Position = new Vector2(24.2857f, 17f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        source.Position = new Vector2(25.0952f, 17f);
        source.State = AgentState.WaitingToReact;
        source.ReactionStartedFrame = simulation.FrameIndex;
        source.PanicLevel = 0.50f;
        source.ActivateDirectAlarm(
            simulation.FrameIndex,
            durationFrames: 30,
            panicFloor: 0.50f,
            episodeId: 3001);

        simulation.SetRunning(true);
        simulation.Step();

        Assert.Equal(AgentState.WaitingToReact, observer.State);
        Assert.Equal(0, observer.ReactionStartedFrame);
        Assert.Equal(3001, observer.LastRelayedAlarmEpisodeId);
        Assert.True(observer.PanicLevel >= simulation.Config.Agent.RelayedAlarmPanicFloor);
        Assert.True(observer.PanicLevel > simulation.Config.Agent.PanicActivationThreshold);
    }

    [Fact]
    public void RelayedSocialAlarm_PropagatesAcrossMultipleHops()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 30);
        Agent source = simulation.Agents[0];
        source.Position = new Vector2(20.2381f, 17f);
        source.State = AgentState.WaitingToReact;
        source.ReactionStartedFrame = simulation.FrameIndex;
        source.PanicLevel = 0.50f;
        source.ActivateDirectAlarm(
            simulation.FrameIndex,
            durationFrames: 40,
            panicFloor: 0.50f,
            episodeId: 3002);

        for (int i = 1; i < simulation.Agents.Count; i++)
        {
            Agent observer = simulation.Agents[i];
            observer.Position = new Vector2(20.2381f + 3.6429f * i, 17f);
            observer.State = AgentState.Wandering;
            observer.PanicLevel = 0f;
        }

        simulation.SetRunning(true);
        simulation.Step();
        simulation.Step();
        simulation.Step();

        for (int i = 1; i < simulation.Agents.Count; i++)
        {
            Agent observer = simulation.Agents[i];
            Assert.True(observer.ReactionStartedFrame >= 0);
            Assert.NotEqual(AgentState.Wandering, observer.State);
            Assert.Equal(3002, observer.LastRelayedAlarmEpisodeId);
        }
    }

    [Fact]
    public void DirectAlarmWindow_MaintainsVisiblePanicAfterShockArrival()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 28);
        Vector2 centre = new(27.9286f, 17f);
        Agent observer = simulation.Agents.Single();
        observer.Position = centre + new Vector2(12.1429f, 0f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        simulation.SetRunning(true);
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear();

        for (int i = 0; i < 20; i++)
        {
            simulation.Step();
        }

        Assert.True(observer.ShockwaveHit);
        Assert.True(observer.IsActiveAlarmSource(simulation.FrameIndex));
        Assert.True(observer.PanicLevel >= simulation.Config.Agent.DirectAlarmPanicFloor - 0.001f);
    }

    [Fact]
    public void DirectThreatPanic_WaitsForShockFrontArrival()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 23);
        Vector2 centre = new(27.9286f, 17f);
        Agent observer = simulation.Agents.Single();
        observer.Position = centre + new Vector2(7.2857f, 0f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        simulation.SetRunning(true);
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear();

        simulation.EvacuationSystem.UpdatePanic(simulation);
        Assert.False(observer.ShockwaveHit);
        Assert.InRange(observer.LastThreatPanicStimulus, 0f, 0.0001f);

        for (int i = 0; i < 8; i++)
        {
            simulation.Step();
        }

        Assert.True(observer.ShockwaveHit);
        Assert.True(observer.ShockwaveHitFrame >= 0);
        Assert.True(observer.LastThreatPanicStimulus > 0f);
    }

    [Fact]
    public void WallBlocksRunningCrowdContagion()
    {
        Simulation simulation = CreateReadySimulation(4, seed: 22);
        Wall shield = new(25.2976f, 14.5714f, 25.9048f, 19.4286f);
        simulation.Environment.Blockages.Add(shield);
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();

        Agent observer = simulation.Agents[0];
        observer.Position = new Vector2(24.2857f, 17f);
        observer.State = AgentState.Wandering;
        observer.PanicLevel = 0f;

        for (int i = 1; i < 4; i++)
        {
            Agent runner = simulation.Agents[i];
            runner.Position = new Vector2(26.7143f + i * 0.2024f, 17f + i * 0.0405f);
            runner.State = AgentState.Evacuating;
            runner.PanicLevel = 0.70f;
            runner.ActivateDirectAlarm(
                simulation.FrameIndex,
                durationFrames: 30,
                panicFloor: 0.50f,
                episodeId: 1003);
            runner.Velocity = Vector2.UnitX * (runner.BaseSpeed * 2.5f);
        }

        simulation.SetRunning(true);
        simulation.Step();

        Assert.InRange(observer.LastRunningPanicStimulus, 0f, 0.0001f);
    }

    [Fact]
    public void TemporaryVisibilityField_DegradesThenRecovers()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 30);
        Vector2 centre = new(27.9286f, 17f);
        Vector2 observerPoint = centre + new Vector2(3.6429f, 0f);
        simulation.Agents[0].Position = observerPoint;
        simulation.SetRunning(true);
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear();

        for (int i = 0; i < 5; i++)
        {
            simulation.Step();
        }

        float degraded = simulation.VisibilitySystem.GetVisibilityFactor(simulation, observerPoint);
        Assert.True(degraded < 0.95f);

        for (int i = 0; i < 90; i++)
        {
            simulation.Step();
        }

        float recovered = simulation.VisibilitySystem.GetVisibilityFactor(simulation, observerPoint);
        Assert.True(recovered > degraded);
        Assert.True(recovered > 0.95f);
    }


    [Fact]
    public void WallShieldsTemporaryVisibilityField()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 31);
        Vector2 centre = new(24.2857f, 17f);
        Vector2 observerPoint = new(28.7381f, 17f);
        simulation.Environment.Blockages.Add(new Wall(26.3095f, 14.5714f, 26.9167f, 19.4286f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();
        simulation.Agents[0].Position = observerPoint;
        simulation.SetRunning(true);
        simulation.TriggerExplosion(centre.X, centre.Y);
        simulation.Debris.Clear();

        for (int i = 0; i < 12; i++)
        {
            simulation.Step();
        }

        float visibility = simulation.VisibilitySystem.GetVisibilityFactor(simulation, observerPoint);
        Assert.InRange(visibility, 0.999f, 1.001f);
        Assert.True(simulation.Agents[0].BlastShielded);
    }

    [Fact]
    public void DynamicRerouting_ChangesExitWhenCurrentApproachIsMateriallyWorse()
    {
        // Retained reference scorer: it intentionally has global doorway-load knowledge.
        // Natural mode instead has separate perceived-congestion/recovery tests.
        Simulation simulation = new(agentCount: 0, randomSeed: 41,
            config: new SimulationConfig { Agent = new AgentConfig { NaturalNavigationEnabled = false } });
        Assert.True(simulation.Environment.AddExitFromClick(16.19f, 0.28f, 3));
        Assert.True(simulation.Environment.AddExitFromClick(40.48f, 0.28f, 3));
        simulation.SpawnAgents(15);

        Exit congestedExit = simulation.Environment.Exits[0];
        Exit clearExit = simulation.Environment.Exits[1];
        Agent agent = simulation.Agents[0];
        agent.Position = new Vector2(20.2381f, 12.1429f);
        agent.State = AgentState.Evacuating;
        agent.ExitTarget = congestedExit;
        agent.ExitCommitUntilFrame = 0;
        agent.LastExitReselectFrame = -999;
        agent.VisibilityFactor = 1f;

        for (int i = 1; i < simulation.Agents.Count; i++)
        {
            Agent casualty = simulation.Agents[i];
            casualty.Position = congestedExit.Position + new Vector2((i % 5 - 2) * 0.2024f, 1.2143f + (i / 5) * 0.2024f);
            casualty.MarkDead();
        }

        simulation.EvacuationSystem.MaybeReselectExit(simulation, agent);

        Assert.Same(clearExit, agent.ExitTarget);
        Assert.True(agent.ExitCommitUntilFrame > simulation.FrameIndex);
    }

    [Fact]
    public void ExitCongestion_ModeratePanicYieldsAfterOneSetback()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 71);
        Assert.True(simulation.Environment.AddExitFromClick(27.93f, 0.28f, 1));
        simulation.SpawnAgents(2);

        Exit exit = simulation.Environment.Exits.Single();
        Agent blocker = simulation.Agents[0];
        Agent follower = simulation.Agents[1];
        blocker.Position = exit.Position;
        blocker.State = AgentState.Evacuating;
        blocker.ExitTarget = exit;
        follower.Position = exit.Position + new Vector2(0f, 0.1619f);
        follower.State = AgentState.Evacuating;
        follower.ExitTarget = exit;
        follower.PanicLevel = 0.50f;

        simulation.EvacuationSystem.ApplyExitFlows(simulation);

        Assert.Equal(AgentState.Exited, blocker.State);
        Assert.True(follower.ExitCongestionEpisodeActive);
        Assert.True(follower.ExitYielding);
        Assert.True(follower.ExitRetryAfterFrame > simulation.FrameIndex);
        Vector2 firstHoldingPosition = follower.Position;

        // Refill the doorway while the follower is still in its yield interval. The
        // follower should remain stationary rather than immediately lunging/rebounding.
        blocker.State = AgentState.Evacuating;
        blocker.Position = exit.Position;
        blocker.ExitTarget = exit;
        follower.Velocity = Vector2.UnitY * -2f;
        simulation.EvacuationSystem.UpdateEvacAgent(simulation, follower);

        Assert.Equal(firstHoldingPosition, follower.Position);
        Assert.Equal(Vector2.Zero, follower.Velocity);
    }

    [Fact]
    public void ExitCongestion_HighPanicMaintainsPressureWithoutRepeatedSetback()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 72);
        Assert.True(simulation.Environment.AddExitFromClick(27.93f, 0.28f, 1));
        simulation.SpawnAgents(2);

        Exit exit = simulation.Environment.Exits.Single();
        Agent blocker = simulation.Agents[0];
        Agent pusher = simulation.Agents[1];
        blocker.Position = exit.Position;
        blocker.State = AgentState.Evacuating;
        blocker.ExitTarget = exit;
        pusher.Position = exit.Position + new Vector2(0f, 0.1619f);
        pusher.State = AgentState.Evacuating;
        pusher.ExitTarget = exit;
        pusher.PanicLevel = 1.0f;

        simulation.EvacuationSystem.ApplyExitFlows(simulation);

        Assert.True(pusher.ExitCongestionEpisodeActive);
        Assert.False(pusher.ExitYielding);
        Vector2 firstHoldingPosition = pusher.Position;

        // Simulate another failed admission attempt in the same congestion episode.
        // There must be no cumulative second pushback/bounce.
        blocker.State = AgentState.Evacuating;
        blocker.Position = exit.Position;
        blocker.ExitTarget = exit;
        pusher.Position = exit.Position + new Vector2(0f, 0.1619f);
        simulation.EvacuationSystem.ApplyExitFlows(simulation);

        Assert.False(pusher.ExitYielding);
        Assert.Equal(firstHoldingPosition, pusher.Position);
    }

    [Fact]
    public void NavigationClearance_DistinguishesVisualSightFromBodyTraversability()
    {
        Wall wall = new(4.0476f, 4.0476f, 8.0952f, 4.8571f);
        Vector2 start = new(3.6429f, 5.019f);
        Vector2 end = new(8.5f, 5.019f);

        Assert.True(EvacuationSystem.HasLineOfSight(start, end, new[] { wall }));
        Assert.False(EvacuationSystem.HasTraversablePath(start, end, new[] { wall }, 0.20f));
    }

    [Fact]
    public void Exploration_ContinuesWhenExitIsVisibleButBodyClearanceIsBlocked()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 80);
        float eastX = simulation.Config.World.WidthMeters -
            simulation.Environment.BoundaryThickness / 2f;
        Assert.True(simulation.Environment.AddExitFromClick(eastX, 17.97f, 3));
        simulation.Environment.Blockages.Add(new Wall(21.8571f, 8.0952f, 23.4762f, 17.8095f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();
        simulation.SpawnAgents(1);

        Exit exit = simulation.Environment.Exits.Single();
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(21.0476f, 17.9714f);
        agent.State = AgentState.Evacuating;
        agent.ExitTarget = exit;
        float clearance = simulation.Config.Agent.Radius *
            simulation.Config.Agent.NavigationClearanceFactor;

        Assert.True(EvacuationSystem.HasLineOfSight(
            agent.Position,
            exit.Position,
            simulation.Environment.Walls));
        Assert.False(EvacuationSystem.HasTraversablePath(
            agent.Position,
            exit.Position,
            simulation.Environment.Walls,
            clearance));

        simulation.EvacuationSystem.EnterExploration(agent, simulation);

        Assert.True(agent.Exploring);
        Assert.NotNull(agent.TempGoal);
        Assert.True(EvacuationSystem.HasTraversablePath(
            agent.Position,
            agent.TempGoal!.Value,
            simulation.Environment.Walls,
            clearance));
    }

    [Fact]
    public void Exploration_ChoosesClearWaypointThatRecoversExitSightAroundWallEnd()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 81);
        Assert.True(simulation.Environment.AddExitFromClick(
            simulation.Config.World.WidthMeters - simulation.Environment.BoundaryThickness / 2,
            12.14f,
            3));
        simulation.Environment.Blockages.Add(new Wall(21.8571f, 8.0952f, 23.4762f, 17.8095f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();
        simulation.SpawnAgents(1);

        Exit exit = simulation.Environment.Exits.Single();
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(21.0476f, 18.2143f);
        agent.State = AgentState.Evacuating;
        agent.ExitTarget = exit;

        Assert.False(EvacuationSystem.HasLineOfSight(
            agent.Position,
            exit.Position,
            simulation.Environment.Walls));

        simulation.EvacuationSystem.EnterExploration(agent, simulation);

        Assert.True(agent.Exploring);
        Assert.NotNull(agent.TempGoal);
        Vector2 goal = agent.TempGoal!.Value;
        Assert.True(EvacuationSystem.HasLineOfSight(
            agent.Position,
            goal,
            simulation.Environment.Walls));
        Assert.True(EvacuationSystem.HasLineOfSight(
            goal,
            exit.Position,
            simulation.Environment.Walls));
        Assert.False(simulation.Environment.IsPointBlocked(
            goal,
            0.5f * simulation.Config.Agent.Radius));
    }

    [Fact]
    public void Exploration_StopsImmediatelyWhenExitBecomesVisible()
    {
        Simulation simulation = new(agentCount: 0, randomSeed: 82);
        Assert.True(simulation.Environment.AddExitFromClick(28.33f, 0.28f, 3));
        simulation.SpawnAgents(1);

        Agent agent = simulation.Agents.Single();
        Exit exit = simulation.Environment.Exits.Single();
        agent.Position = new Vector2(28.3333f, 8.9048f);
        agent.State = AgentState.Evacuating;
        agent.ExitTarget = exit;
        agent.Exploring = true;
        agent.TempGoal = agent.Position + Vector2.UnitX * 48f;
        agent.ExplorationReplanAfterFrame = simulation.FrameIndex + 100;

        Assert.True(EvacuationSystem.HasLineOfSight(
            agent.Position,
            exit.Position,
            simulation.Environment.Walls));

        simulation.EvacuationSystem.UpdateEvacAgent(simulation, agent);

        Assert.False(agent.Exploring);
        Assert.Null(agent.TempGoal);
        Assert.Equal(0, agent.ExplorationReplanAfterFrame);
    }

    [Fact]
    public void HighEnergyDebris_CanIncapacitateOrKillAndIsConsumedOnImpact()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 55);
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(28.3333f, 17f);
        agent.State = AgentState.Evacuating;
        simulation.Debris.Clear();
        simulation.Debris.Add(new Debris(
            agent.Position - new Vector2(1.2143f, 0f),
            new Vector2(1.4167f, 0f),
            ttl: 10,
            radius: 6f,
            energy: 1f));

        simulation.ExplosionSystem.UpdateDebris(simulation);

        Assert.Equal(1, simulation.DebrisHits);
        Assert.True(agent.State is AgentState.Dead or AgentState.Incapacitated);
        Assert.Empty(simulation.Debris);
    }

    [Fact]
    public void WallStopsDebrisBeforeItCanHitAgent()
    {
        Simulation simulation = CreateReadySimulation(1, seed: 56);
        Agent agent = simulation.Agents.Single();
        agent.Position = new Vector2(28.3333f, 17f);
        agent.State = AgentState.Evacuating;
        simulation.Environment.Blockages.Add(new Wall(27.3214f, 15.7857f, 27.7262f, 18.2143f));
        simulation.Environment.RebuildWalls();
        simulation.BuildWallGrid();
        simulation.Debris.Clear();
        simulation.Debris.Add(new Debris(
            new Vector2(26.3095f, 17f),
            new Vector2(1.4167f, 0f),
            ttl: 10,
            radius: 6f,
            energy: 1f));

        simulation.ExplosionSystem.UpdateDebris(simulation);

        Assert.Equal(0, simulation.DebrisHits);
        Assert.Equal(AgentState.Evacuating, agent.State);
        Assert.Empty(simulation.Debris);
    }

    private static Simulation CreateReadySimulation(int count, int seed)
    {
        Simulation simulation = new(agentCount: 0, randomSeed: seed);
        Assert.True(simulation.Environment.AddExitFromClick(27.93f, 0.28f, 3));
        simulation.SpawnAgents(count);
        return simulation;
    }
}
