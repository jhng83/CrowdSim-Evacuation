using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using Xunit;

namespace CrowdSim.Portable.Tests;

public sealed class FurniturePresentationTests
{
    private static Simulation ChairScene()
    {
        Simulation simulation = new(randomSeed: 731);
        Assert.True(simulation.Environment.AddExitFromClick(0, 17, 5));
        Assert.True(simulation.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(15, 15), .65f));
        simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
        return simulation;
    }

    [Theory]
    [InlineData(ChairState.Upright)] [InlineData(ChairState.Airborne)]
    [InlineData(ChairState.Fallen)] [InlineData(ChairState.Destroyed)]
    public void FurnitureViewsFollowThePhysicalAnchorHeadingAndStateAndStayDetached(ChairState state)
    {
        Simulation simulation = ChairScene();
        FurnitureChair chair = simulation.Environment.Furniture[0].Chairs[0];
        chair.Position = new Vector2(16, 17); chair.Height = 1.25f; chair.Heading = .4f; chair.State = state;
        PresentationFrame frame = PresentationFrame.Capture(simulation);
        FurnitureBodyFrame status = Assert.Single(frame.FurnitureBodies);
        Assert.Equal(new Vector3(16, 1.25f, 17), status.Position);
        Assert.Equal(.4f, status.Heading); Assert.Equal(state, status.State);
        ViewBody[] parts = frame.Bodies.Where(b => b.Kind == ViewKind.Chair).ToArray();
        Assert.Equal(state == ChairState.Destroyed ? 0 : state == ChairState.Upright ? 6 : 2, parts.Length);
        Assert.All(parts, part => { Assert.Equal(.4f, part.GroundAngle); Assert.Equal(state, part.MotionState); Assert.True(part.Position.Y > 1.25f); });
        chair.Position = new Vector2(25, 25);
        Assert.Equal(new Vector3(16, 1.25f, 17), status.Position);
        Assert.Throws<NotSupportedException>(() => ((IList<FurnitureBodyFrame>)frame.FurnitureBodies).Clear());
    }

    [Fact]
    public void ActualReactionRaisesTheSeatedViewThenReleasesTheSeatWithoutMovingPhysicsFromRendering()
    {
        Simulation simulation = ChairScene();
        Agent agent = simulation.Agents[0];
        PresentationFrame seated = PresentationFrame.Capture(simulation);
        Assert.Equal(SeatingPosture.Seated, seated.Bodies.Single(b => b.Id == "agent-0").Posture);
        Assert.Equal(1, seated.SeatedAgents);
        Vector2 physical = agent.Position;
        agent.BeginReaction(0, 0); simulation.SetRunning(true); simulation.Step();
        PresentationFrame rising = PresentationFrame.Capture(simulation);
        ViewBody torso = rising.Bodies.Single(b => b.Id == "agent-0");
        Assert.Equal(SeatingPosture.StandingUp, torso.Posture);
        Assert.InRange(torso.RiseProgress, .001f, .999f);
        Assert.True(torso.Position.Y > seated.Bodies.Single(b => b.Id == "agent-0").Position.Y);
        Assert.Equal(physical, agent.Position);
        for (int i = 0; i < 100 && agent.Posture != SeatingPosture.Walking; i++) simulation.Step();
        PresentationFrame walking = PresentationFrame.Capture(simulation);
        Assert.Equal(SeatingPosture.Walking, agent.Posture);
        Assert.Equal(SeatingPosture.Walking, walking.Bodies.Single(b => b.Id == "agent-0").Posture);
        Assert.DoesNotContain(walking.Bodies, b => b.Kind == ViewKind.AgentDetail);
        Assert.Null(walking.FurnitureBodies[0].OccupantId);
    }

    [Theory]
    [InlineData(0f)] [InlineData(90f)]
    public void RectangularTableTopMatchesWorldFootprintAndMotionUsesLivePositionAndHeight(float degrees)
    {
        Simulation simulation = new();
        Assert.True(simulation.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(20, 17), degrees * MathF.PI / 180));
        RectangularFurnitureBody table = (RectangularFurnitureBody)simulation.Environment.Furniture[0].Table!;
        PresentationFrame upright = PresentationFrame.Capture(simulation);
        ViewBody top = upright.Bodies.Single(b => b.Id == "furniture-0-table");
        float cosine = MathF.Abs(MathF.Cos(top.GroundAngle)), sine = MathF.Abs(MathF.Sin(top.GroundAngle));
        Assert.InRange(MathF.Abs(top.Size.X * cosine + top.Size.Z * sine - table.HalfExtents.X * 2), 0, .00001f);
        Assert.InRange(MathF.Abs(top.Size.X * sine + top.Size.Z * cosine - table.HalfExtents.Y * 2), 0, .00001f);
        Assert.Equal(5, upright.Bodies.Count(b => b.Kind == ViewKind.Table));
        table.Position = new Vector2(22, 18); table.Height = 2; table.State = ChairState.Airborne;
        PresentationFrame flying = PresentationFrame.Capture(simulation);
        ViewBody flyingTop = Assert.Single(flying.Bodies, b => b.Kind == ViewKind.Table);
        Assert.Equal(new Vector3(22, 2.10f, 18), flyingTop.Position);
        Assert.Equal(.16f, flyingTop.Size.Y, 5);
        table.State = ChairState.Destroyed;
        Assert.DoesNotContain(PresentationFrame.Capture(simulation).Bodies, b => b.Kind == ViewKind.Table);
    }

    [Fact]
    public void ActualExplosionDestroysTheChairAndRemovesAllItsVisualParts()
    {
        Simulation simulation = ChairScene();
        simulation.SetRunning(true); simulation.TriggerExplosion(15, 15); simulation.Step();
        PresentationFrame frame = PresentationFrame.Capture(simulation);
        Assert.Equal(ChairState.Destroyed, frame.FurnitureBodies[0].State);
        Assert.DoesNotContain(frame.Bodies, b => b.Kind == ViewKind.Chair);
        Assert.Empty(simulation.Environment.CircularBlockages);
    }
    [Theory]
    [InlineData(ChairState.Upright)] [InlineData(ChairState.Airborne)] [InlineData(ChairState.Fallen)]
    public void RoundTableUsesLiveHeightAndTheReferenceTippedProfile(ChairState state)
    {
        Simulation simulation = new();
        Assert.True(simulation.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(15, 15)));
        FurnitureBody table = simulation.Environment.Furniture[0].Table!;
        table.Height = 1; table.State = state;
        ViewBody top = PresentationFrame.Capture(simulation).Bodies.Single(b => b.Id == "furniture-0-table");
        Assert.Equal(state == ChairState.Upright ? 1.75f : 1.15f, top.Position.Y, 4);
        Assert.Equal(new Vector3(1.8f, .06f, 1.8f), top.Size);
        Assert.Equal(state, top.MotionState);
    }

}
