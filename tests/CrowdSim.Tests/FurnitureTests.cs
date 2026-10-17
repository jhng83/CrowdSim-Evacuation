using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Public-API regression coverage for furniture, hazards, navigation and persistence.</summary>
public sealed class FurnitureTests
{
    private static Simulation Scene(FurnitureKind kind = FurnitureKind.SingleChair)
    {
        Simulation sim = new(randomSeed: 731);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(kind, new Vector2(15f, 15f)));
        sim.SpawnAgentsAtPositions(sim.Environment.GetPlannedAgentPositions());
        return sim;
    }

    [Theory]
    [InlineData(FurnitureKind.RoundTableSet, 10, 11)]
    [InlineData(FurnitureKind.SingleChair, 1, 1)]
    public void PlacementSpawnsExactlyOneOccupantPerChair(FurnitureKind kind, int people, int obstacles)
    {
        Simulation sim = Scene(kind);
        Assert.Equal(people, sim.Environment.PlannedAgentCount);
        Assert.Equal(people, sim.Agents.Count);
        Assert.Equal(obstacles, sim.Environment.CircularBlockages.Count);
        Assert.All(sim.Agents, a => { Assert.Equal(SeatingPosture.Seated, a.Posture); Assert.Equal(a.AgentId, a.Chair!.OccupantId); });
        Assert.Equal(people, sim.Agents.Select(a => a.Chair).Distinct().Count());
    }

    [Fact]
    public void SeatedOccupantsRemainStillBeforeAnyThreat()
    {
        Simulation sim = Scene(FurnitureKind.RoundTableSet);
        Vector2[] starts = sim.Agents.Select(a => a.Position).ToArray();
        sim.SetRunning(true);
        for (int i = 0; i < 40; i++) sim.Step();
        Assert.Equal(starts, sim.Agents.Select(a => a.Position).ToArray());
        Assert.All(sim.Agents, a => Assert.Equal(SeatingPosture.Seated, a.Posture));
    }

    [Fact]
    public void UndoRemovesEntireSetAndOccupantsFromPlan()
    {
        Simulation sim = new(randomSeed: 731);
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(15f, 15f)));
        Assert.True(sim.Environment.UndoLast());
        Assert.Empty(sim.Environment.Furniture);
        Assert.Empty(sim.Environment.CircularBlockages);
        Assert.Empty(sim.Environment.GetPlannedAgentPositions());
    }

    [Fact]
    public void InvalidPlacementIsAtomic()
    {
        Simulation sim = new();
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(1f, 1f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(float.NaN, 10f)));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(15f, 15f)));
        Assert.False(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(15f, 15f)));
        Assert.Equal(10, sim.Environment.PlannedAgentCount);
        Assert.Single(sim.Environment.Furniture);
    }

    [Fact]
    public void ChairBlocksWalkingButDoesNotShieldItsOccupant()
    {
        Simulation sim = Scene();
        Vector2 seat = sim.Agents[0].Position;
        Assert.True(sim.HasLineOfSight(seat - Vector2.UnitX * 2f, seat));
        Assert.False(sim.HasTraversablePath(seat - Vector2.UnitX * 2f, seat + Vector2.UnitX * 2f, sim.Config.Agent.Radius));
        Assert.False(sim.Environment.IsFumeBlocked(seat, 0.02f));
    }

    [Fact]
    public void ReactionStartsTimedRiseThenReleasesIntoExistingMovement()
    {
        Simulation sim = Scene();
        Agent a = sim.Agents[0];
        a.BeginReaction(0, 0);
        sim.SetRunning(true);
        sim.Step();
        Assert.Equal(SeatingPosture.StandingUp, a.Posture);
        Assert.InRange(a.StandUpSeconds, 0.6f, 2.1f);
        Assert.True(a.StandUpElapsed < a.StandUpSeconds);
        for (int i = 0; i < 100 && a.Posture != SeatingPosture.Walking; i++) sim.Step();
        Assert.Equal(SeatingPosture.Walking, a.Posture);
        Assert.Null(a.Chair);
        Assert.Null(sim.Environment.Furniture[0].Chairs[0].OccupantId);
    }

    [Theory]
    [InlineData(AgentState.Dead)]
    [InlineData(AgentState.Incapacitated)]
    [InlineData(AgentState.Fallen)]
    public void SeatingCannotReviveCasualties(AgentState state)
    {
        Simulation sim = Scene();
        Agent a = sim.Agents[0];
        a.State = state;
        sim.SetRunning(true);
        sim.Step();
        Assert.Equal(state, a.State);
        Assert.NotEqual(SeatingPosture.StandingUp, a.Posture);
        Assert.Null(a.Chair);
    }

    [Fact]
    public void BlastAtSeatAppliesExistingFatalityAndDestroysChair()
    {
        Simulation sim = Scene();
        sim.SetRunning(true);
        sim.TriggerExplosion(15f, 15f);
        sim.Step();
        Assert.Equal(AgentState.Dead, sim.Agents[0].State);
        Assert.Equal(ChairState.Destroyed, sim.Environment.Furniture[0].Chairs[0].State);
        Assert.Empty(sim.Environment.CircularBlockages);
    }

    [Fact]
    public void ToxicExposureReachesSeatedPersonAndStartsRise()
    {
        Simulation sim = Scene();
        sim.SetScenarioType(ScenarioType.ToxicFume);
        Assert.True(sim.ToxicFumeSystem.TrySetSource(sim, new Vector2(15.5f, 15f)));
        sim.PrecomputeToxicFume();
        sim.SetRunning(true);
        Assert.True(sim.TriggerToxicFumeRelease());
        for (int i = 0; i < 4; i++) sim.Step();
        Assert.True(sim.Agents[0].CumulativeToxicExposure > 0f);
        Assert.Equal(SeatingPosture.StandingUp, sim.Agents[0].Posture);
        Assert.True(sim.ToxicFumeSystem.IsCacheValidFor(sim));
    }

    [Fact]
    public void SaveReloadRestoresAuthoredLayoutWithoutDuplicateProxiesOrOccupants()
    {
        Simulation sim = Scene(FurnitureKind.RoundTableSet);
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "Furniture test");
        Assert.Empty(saved.CircularBlockages);
        Assert.Single(saved.Furniture);
        string json = JsonSerializer.Serialize(saved);
        ScenarioDefinition loaded = JsonSerializer.Deserialize<ScenarioDefinition>(json)!;
        Simulation restored = new(randomSeed: 731);
        loaded.ApplyLayout(restored);
        restored.SpawnAgentsAtPositions(restored.Environment.GetPlannedAgentPositions());
        Assert.Equal(10, restored.Agents.Count);
        Assert.Equal(11, restored.Environment.CircularBlockages.Count);
        Assert.All(restored.Agents, a => Assert.Equal(SeatingPosture.Seated, a.Posture));
    }

    [Fact]
    public void NearBlastFurnitureLandsWithinRoomAndKeepsObstacles()
    {
        Simulation sim = Scene(FurnitureKind.RoundTableSet);
        FurnitureSet set = sim.Environment.Furniture[0];
        Vector2 original = set.TableObstacle!.Center;
        sim.SetRunning(true);
        sim.TriggerExplosion(11f, 15f);
        for (int i = 0; i < 60; i++) sim.Step();
        Assert.NotEqual(original, set.Table!.Position);
        Assert.Equal(ChairState.Fallen, set.Table.State);
        Assert.Equal(set.Table.Position, set.TableObstacle!.Center);
        Assert.Contains(set.TableObstacle, sim.Environment.CircularBlockages);
        Assert.All(set.Chairs, c =>
        {
            Assert.NotEqual(ChairState.Airborne, c.State);
            Assert.InRange(c.Position.X, 0f, sim.Environment.Width);
            Assert.InRange(c.Position.Y, 0f, sim.Environment.Height);
            if (c.State == ChairState.Fallen) Assert.Contains(c.Obstacle!, sim.Environment.CircularBlockages);
        });
    }

    [Fact]
    public void CongestedSeatWaitsThenLeavesWhenGapOpens()
    {
        Simulation sim = Scene();
        Vector2 seat = sim.Agents[0].Position;
        List<Vector2> positions = new() { seat };
        for (int i = 0; i < 8; i++)
        {
            float angle = MathF.Tau * i / 8;
            positions.Add(seat + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 0.8f);
        }
        sim.SpawnAgentsAtPositions(positions);
        Agent occupant = sim.Agents[0];
        occupant.BeginReaction(0, 0);
        foreach (Agent blocker in sim.Agents.Skip(1)) blocker.BeginReaction(0, 10000);
        sim.SetRunning(true);
        for (int i = 0; i < 30; i++) sim.Step();
        Assert.Equal(SeatingPosture.StandingUp, occupant.Posture);
        Assert.All(sim.Agents.Skip(1), blocker => Assert.True(
            Vector2.Distance(occupant.Position, blocker.Position) >= sim.Config.Agent.Radius * 2f - 0.001f));
        foreach (Agent blocker in sim.Agents.Skip(1)) blocker.MarkExited();
        for (int i = 0; i < 50 && occupant.Posture != SeatingPosture.Walking; i++) sim.Step();
        Assert.Equal(SeatingPosture.Walking, occupant.Posture);
    }

    [Fact]
    public void RestartAfterDestructionRecreatesIntactOccupiedChair()
    {
        Simulation sim = Scene();
        sim.SetRunning(true);
        sim.TriggerExplosion(15f, 15f);
        sim.Step();
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "Restart furniture");
        Simulation restarted = new(randomSeed: sim.RandomSeed);
        saved.ApplyLayout(restarted);
        restarted.SpawnAgentsAtPositions(restarted.Environment.GetPlannedAgentPositions());
        Assert.Single(restarted.Agents);
        Assert.Equal(SeatingPosture.Seated, restarted.Agents[0].Posture);
        Assert.Equal(ChairState.Upright, restarted.Environment.Furniture[0].Chairs[0].State);
    }

    [Fact]
    public void FinalisedCheckpoint_ReplayRestoresExactReadySceneAfterRuntimeDamage()
    {
        Simulation original = Scene(FurnitureKind.RoundTableSet);
        Vector2[] authoredStarts = original.Agents.Select(agent => agent.Position).ToArray();
        FinalizedSceneCheckpoint checkpoint = FinalizedSceneCheckpoint.Capture(
            original,
            original.Agents.Count,
            "checkpoint_replay");

        original.SetRunning(true);
        original.TriggerExplosion(15f, 15f);
        for (int frame = 0; frame < 20; frame++)
        {
            original.Step();
        }

        Assert.True(original.FrameIndex > 0);
        Assert.True(original.Environment.HasExplosion());

        Simulation replay = checkpoint.CreateReadySimulation();

        Assert.False(replay.Running);
        Assert.Equal(0, replay.FrameIndex);
        Assert.False(replay.Environment.HasExplosion());
        Assert.Equal(authoredStarts, replay.Agents.Select(agent => agent.Position).ToArray());
        Assert.All(replay.Agents, agent => Assert.Equal(SeatingPosture.Seated, agent.Posture));
        Assert.All(
            replay.Environment.Furniture.SelectMany(set => set.Chairs),
            chair => Assert.Equal(ChairState.Upright, chair.State));
    }

    [Fact]
    public void FinalisedCheckpoint_ToxicReplayRebuildsReadyFumeCache()
    {
        Simulation original = Scene();
        original.SetScenarioType(ScenarioType.ToxicFume);
        Assert.True(original.ToxicFumeSystem.TrySetSource(original, new Vector2(12f, 12f)));
        original.PrecomputeToxicFume();
        FinalizedSceneCheckpoint checkpoint = FinalizedSceneCheckpoint.Capture(
            original,
            original.Agents.Count,
            "toxic_checkpoint");

        Simulation replay = checkpoint.CreateReadySimulation();

        Assert.False(replay.Running);
        Assert.Equal(0, replay.FrameIndex);
        Assert.False(replay.ToxicFumeSystem.IsReleased);
        Assert.True(replay.ToxicFumeSystem.IsCacheValidFor(replay));
    }
}
