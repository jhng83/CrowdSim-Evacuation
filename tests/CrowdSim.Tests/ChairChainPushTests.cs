using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class ChairChainPushTests
{
    private static (Simulation Sim, List<FurnitureChair> Chairs) Scene(int count)
    {
        Simulation sim = new(randomSeed: 731);
        sim.SpawnAgentsAtPositions(new[] { new Vector2(9.3f, 10f) });
        sim.Agents[0].State = AgentState.Evacuating;
        List<FurnitureChair> chairs = new();
        for (int i = 0; i < count; i++)
        {
            FurnitureChair chair = new(new Vector2(10f + 0.6f * i, 10f), 0f);
            chairs.Add(chair);
            sim.Environment.CircularBlockages.Add(chair.Obstacle!);
        }
        return (sim, chairs);
    }

    [Theory]
    [InlineData(2, 0.08f)]
    [InlineData(3, 0.06f)]
    public void SmallChainTranslatesTogetherWithAddedResistance(int count, float travel)
    {
        var (sim, chairs) = Scene(count);
        Assert.True(sim.TryTranslateChairGroup(chairs[0], new Vector2(0.12f, 0f)));
        foreach (FurnitureChair chair in chairs)
        {
            Assert.InRange(chair.Position.X - chair.StartPosition.X, travel - 0.0001f, travel + 0.0001f);
            Assert.Equal(chair.Position, chair.Obstacle!.Center);
            Assert.Contains(chair.Obstacle, sim.Environment.CircularBlockages);
            Assert.Equal(sim.FrameIndex, chair.LastPushFrame);
        }
        Assert.False(sim.TryTranslateChairGroup(chairs[0], new Vector2(0.12f, 0f)));
    }

    [Fact]
    public void FourChairChainIsRejectedWithoutMovingAnyChair()
    {
        var (sim, chairs) = Scene(4);
        Assert.False(sim.TryTranslateChairGroup(chairs[0], new Vector2(0.12f, 0f)));
        Assert.All(chairs, chair => Assert.Equal(chair.StartPosition, chair.Position));
    }

    [Fact]
    public void OccupiedChairStopsEntireChain()
    {
        var (sim, chairs) = Scene(3);
        chairs[2].OccupantId = 999;
        Assert.False(sim.TryTranslateChairGroup(chairs[0], new Vector2(0.12f, 0f)));
        Assert.All(chairs, chair => Assert.Equal(chair.StartPosition, chair.Position));
    }

    [Fact]
    public void WallStopsEntireChain()
    {
        var (sim, chairs) = Scene(2);
        sim.Environment.Walls.Add(new Wall(10.92f, 8f, 11.1f, 12f));
        Assert.False(sim.TryTranslateChairGroup(chairs[0], new Vector2(0.12f, 0f)));
        Assert.All(chairs, chair => Assert.Equal(chair.StartPosition, chair.Position));
    }

    [Fact]
    public void CrawlingPersonStopsEntireChain()
    {
        var (sim, chairs) = Scene(2);
        sim.SpawnAgentsAtPositions(new[] { new Vector2(9.3f, 10f), new Vector2(11.15f, 10f) });
        sim.Agents[1].MarkCrawling();
        Assert.False(sim.TryTranslateChairGroup(chairs[0], new Vector2(0.12f, 0f)));
        Assert.All(chairs, chair => Assert.Equal(chair.StartPosition, chair.Position));
    }

    [Fact]
    public void NormalPushFallsBackToChainInNarrowPassage()
    {
        var (sim, chairs) = Scene(2);
        Agent person = sim.Agents[0];
        person.Velocity = new Vector2(0.15f, 0f);
        sim.Environment.Walls.Add(new Wall(8f, 9.45f, 14f, 9.65f));
        sim.Environment.Walls.Add(new Wall(8f, 10.35f, 14f, 10.55f));
        Assert.True(sim.TryPushLooseChair(person, chairs[0].Obstacle!, Vector2.UnitX));
        Assert.All(chairs, chair => Assert.True(chair.Position.X > chair.StartPosition.X));
    }
}
