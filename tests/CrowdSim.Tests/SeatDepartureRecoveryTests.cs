using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class SeatDepartureRecoveryTests
{
    private static Simulation Scene()
    {
        Simulation sim = new(randomSeed: 731);
        Assert.True(sim.Environment.AddExitFromClick(sim.Environment.Width / 2f, 0.3f, 5));
        Assert.True(sim.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(15f, 15f)));
        sim.SpawnAgentsAtPositions(sim.Environment.GetPlannedAgentPositions());
        Agent person = sim.Agents[0];
        person.State = AgentState.Evacuating;
        person.Posture = SeatingPosture.StandingUp;
        person.StandUpSeconds = 1f;
        person.StandUpElapsed = 1f;
        sim.SetRunning(true);
        Assert.True(sim.Running);
        return sim;
    }

    [Fact]
    public void DepartingPersonMakesPartialProgressThenReleasesSeat()
    {
        Simulation sim = Scene();
        Agent person = sim.Agents[0];
        Vector2 start = person.Position;
        sim.Step();
        Assert.True(Vector2.Distance(start, person.Position) > 0f);
        Assert.Equal(SeatingPosture.StandingUp, person.Posture);
        for (int i = 0; i < 30 && person.Posture != SeatingPosture.Walking; i++) sim.Step();
        Assert.Equal(SeatingPosture.Walking, person.Posture);
        Assert.Null(person.Chair);
        Assert.Null(sim.Environment.Furniture[0].Chairs[0].OccupantId);
    }

    [Fact]
    public void DestroyedSeatDoesNotKeepAgentWaitingForDeparturePath()
    {
        Simulation sim = Scene();
        FurnitureChair chair = sim.Environment.Furniture[0].Chairs[0];
        sim.Environment.CircularBlockages.Remove(chair.Obstacle!);
        chair.Obstacle = null;
        chair.State = ChairState.Destroyed;
        sim.Step();
        Assert.Equal(SeatingPosture.Walking, sim.Agents[0].Posture);
        Assert.Null(sim.Agents[0].Chair);
    }

    [Fact]
    public void CompletedStandUpCanPushLooseChairButNotOccupiedSeat()
    {
        Simulation sim = Scene();
        Agent person = sim.Agents[0];
        FurnitureChair loose = new(person.Position + new Vector2(0.7f, 0f), 0f);
        sim.Environment.CircularBlockages.Add(loose.Obstacle!);
        Assert.True(sim.TryPushLooseChair(person, loose.Obstacle!, Vector2.UnitX));
        FurnitureChair occupied = sim.Environment.Furniture[0].Chairs[0];
        Assert.False(sim.TryPushLooseChair(person, occupied.Obstacle!, Vector2.UnitX));
    }
}
