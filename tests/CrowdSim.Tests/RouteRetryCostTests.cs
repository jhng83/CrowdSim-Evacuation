using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class RouteRetryCostTests
{
    [Fact]
    public void FailedRouteIsNotSearchedRepeatedlyButClearDirectPathRecoversImmediately()
    {
        Simulation sim = new(randomSeed: 731);
        sim.SpawnAgentsAtPositions(new[] { new Vector2(10f, 10f) });
        Agent agent = sim.Agents[0];
        Vector2 goal = new(12f, 10f);
        CircleBlockage obstacle = new(goal, 1f);
        sim.Environment.CircularBlockages.Add(obstacle);
        Assert.Equal(Vector2.Zero, sim.EvacuationSystem.ResolveStaticRouteDirection(sim, agent, goal, "test"));
        Assert.Equal(1, sim.LastRoutePlanRequests);
        Assert.True(agent.FailedRouteRetryFrame > sim.FrameIndex);
        Assert.Equal(Vector2.Zero, sim.EvacuationSystem.ResolveStaticRouteDirection(sim, agent, goal, "test"));
        Assert.Equal(1, sim.LastRoutePlanRequests);
        sim.Environment.CircularBlockages.Remove(obstacle);
        Vector2 recovered = sim.EvacuationSystem.ResolveStaticRouteDirection(sim, agent, goal, "test");
        Assert.True(recovered.X > 0.9f);
        Assert.Equal(1, sim.LastRoutePlanRequests);
    }
}
