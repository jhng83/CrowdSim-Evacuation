using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Tests;

public sealed class PanicMovementFixTests
{
    [Theory]
    [InlineData(-0.35f, 0f)]
    [InlineData(0f, 0.35f)]
    public void ContactOutsideForwardVision_StillRepels(float x, float y)
    {
        Simulation sim = Ready(2);
        Agent a = sim.Agents[0], b = sim.Agents[1];
        a.Position = new Vector2(20f, 20f);
        b.Position = a.Position + new Vector2(x, y);
        a.Velocity = b.Velocity = Vector2.Zero;
        a.ExitTarget = new Exit("east", new Vector2(40f, 20f), side: "E");
        Vector2 force = sim.SfmEngine.ComputeForces(sim, a, Vector2.Zero);
        Assert.True(Vector2.Dot(force, a.Position - b.Position) > 1f);
        Assert.True(VectorMath.IsFinite(force));
    }

    [Fact]
    public void CoincidentBodies_GetFiniteOpposingContactForces()
    {
        Simulation sim = Ready(2);
        Agent a = sim.Agents[0], b = sim.Agents[1];
        a.Position = b.Position = new Vector2(20f, 20f);
        a.Velocity = b.Velocity = Vector2.Zero;
        Vector2 fa = sim.SfmEngine.ComputeForces(sim, a, Vector2.Zero);
        Vector2 fb = sim.SfmEngine.ComputeForces(sim, b, Vector2.Zero);
        Assert.True(fa.Length() > 1f);
        Assert.InRange((fa + fb).Length(), 0f, 0.001f);
    }

    [Fact]
    public void FreeDrivingAcceleration_IsIndependentOfMass()
    {
        Simulation sim = Ready(1);
        Agent a = sim.Agents[0];
        a.Position = new Vector2(20f, 20f);
        a.Velocity = Vector2.Zero;
        a.Mass = 40f;
        Vector2 light = sim.SfmEngine.ComputeForces(sim, a, Vector2.UnitX) / a.Mass;
        a.Mass = 80f;
        Vector2 heavy = sim.SfmEngine.ComputeForces(sim, a, Vector2.UnitX) / a.Mass;
        float expected = a.CurrentDesiredSpeed /
            (AgentConfig.ReferenceStepSeconds * sim.Config.Agent.RelaxationTime);
        Assert.InRange(MathF.Abs(light.X - expected), 0f, 0.0001f);
        Assert.InRange((light - heavy).Length(), 0f, 0.0001f);
    }

    [Fact]
    public void Locomotion_UsesOneBoundedAccelerationUpdate()
    {
        Simulation sim = Ready(1);
        Agent a = sim.Agents[0];
        a.Position = new Vector2(20f, 20f);
        a.Velocity = Vector2.Zero;
        a.State = AgentState.Evacuating;
        a.Intent = BehaviorIntent.Evacuate;
        Vector2 force = sim.SfmEngine.ComputeForces(sim, a, Vector2.UnitX);
        Vector2 expected = VectorMath.ClampMagnitude(force / a.Mass,
            sim.Config.Agent.MaxAccelerationMetersPerSecondSquared) *
            (sim.Config.Agent.Dt * AgentConfig.ReferenceStepSeconds);
        expected = VectorMath.ClampMagnitude(expected, a.CurrentDesiredSpeed);
        sim.EvacuationSystem.IntegrateLocomotion(sim, a, Vector2.UnitX, a.CurrentDesiredSpeed);
        Assert.InRange((a.Velocity - expected).Length(), 0f, 0.0001f);
    }

    [Fact]
    public void StandingBodyOverlap_IsResolvedEvenWithoutMotion()
    {
        Simulation sim = Ready(2);
        Agent a = sim.Agents[0], b = sim.Agents[1];
        a.Position = new Vector2(20f, 20f);
        b.Position = new Vector2(20.2f, 20f);
        foreach (Agent agent in sim.Agents)
        {
            agent.BeginReaction(0, 1000);
            agent.Velocity = Vector2.Zero;
        }
        sim.SetRunning(true);
        sim.Step();
        Assert.InRange(Vector2.Distance(a.Position, b.Position), 0.3999f, 0.401f);
    }

    [Fact]
    public void SocialSignal_DecreasesWithSingleSourceDistance()
    {
        float near = SocialSignalAt(0.6f);
        float far = SocialSignalAt(3.5f);
        Assert.True(near > far + 0.1f);
        Assert.InRange(far, 0f, near);
    }

    [Fact]
    public void ConstantVelocityTravel_UsesElapsedSeconds()
    {
        Vector2 normal = Travel(0.15f, 10);
        Vector2 half = Travel(0.075f, 20);
        Assert.InRange(Vector2.Distance(normal, half), 0f, 0.0001f);
        Assert.InRange(MathF.Abs(normal.X - 20.6f), 0f, 0.0001f);
    }

    [Fact]
    public void FiveLaneExit_HasFiveAdmissionsRatherThanThree()
    {
        Simulation sim = Ready(8, lanes: 5);
        Exit exit = sim.Environment.Exits.Single();
        foreach (Agent a in sim.Agents)
        {
            a.Position = exit.Position;
            a.State = AgentState.Evacuating;
            a.ExitTarget = exit;
            a.PanicLevel = 1f;
        }
        sim.EvacuationSystem.ApplyExitFlows(sim);
        Assert.Equal(5, sim.Agents.Count(a => a.State == AgentState.Exited));
        sim.EvacuationSystem.ApplyExitFlows(sim);
        Assert.Equal(5, sim.Agents.Count(a => a.State == AgentState.Exited));
    }

    [Fact]
    public void SpatialQueries_MatchBruteForceAcrossCellBoundaries()
    {
        Simulation sim = Ready(500);
        sim.SetRunning(true);
        sim.RefreshSpatialIndex();
        List<Agent> actual = new();
        foreach (Vector2 point in new[] { new Vector2(2.43f, 2.43f), new Vector2(28f, 17f) })
        {
            foreach (float radius in new[] { 0.4f, 1.82f, 3.85f, 7f })
            {
                sim.CollectAgentsNearPoint(point, radius, actual);
                int[] expected = sim.Agents.Where(a => a.IsActive &&
                    Vector2.DistanceSquared(a.Position, point) <= radius * radius)
                    .Select(a => a.AgentId).OrderBy(id => id).ToArray();
                Assert.Equal(expected, actual.Select(a => a.AgentId).OrderBy(id => id).ToArray());
            }
        }
    }

    [Fact]
    public void MovingAgents_KeepSpatialIndexCurrent()
    {
        Simulation sim = Ready(60);
        sim.SetRunning(true);
        List<Agent> neighbours = new();
        for (int tick = 0; tick < 40; tick++)
        {
            sim.Step();
            foreach (Agent a in sim.Agents.Where(a => a.IsActive))
            {
                sim.CollectAgentsNearPoint(a.Position, 0.01f, neighbours);
                Assert.Contains(a, neighbours);
            }
        }
    }

    [Fact]
    public void WallQueries_HonourRadiiLargerThanOneCell()
    {
        Simulation sim = Ready(1);
        Wall wall = new(23f, 19f, 24f, 21f);
        sim.Environment.Walls.Add(wall);
        sim.BuildWallGrid();
        List<Wall> actual = new();
        sim.CollectNearbyWalls(new Vector2(20f, 20f), 3.5f, actual, new HashSet<Wall>());
        Assert.Contains(wall, actual);
    }

    [Fact]
    public void SeededEmergencyRun_RemainsFiniteAndReproducible()
    {
        Simulation a = Ready(200), b = Ready(200);
        a.SetRunning(true);
        b.SetRunning(true);
        a.TriggerExplosion(28f, 17f);
        b.TriggerExplosion(28f, 17f);
        for (int tick = 0; tick < 160; tick++)
        {
            a.Step();
            b.Step();
        }
        for (int i = 0; i < a.Agents.Count; i++)
        {
            Assert.True(VectorMath.IsFinite(a.Agents[i].Position));
            Assert.True(VectorMath.IsFinite(a.Agents[i].Velocity));
            Assert.InRange(a.Agents[i].PanicLevel, 0f, 1f);
            Assert.Equal(a.Agents[i].Position, b.Agents[i].Position);
            Assert.Equal(a.Agents[i].State, b.Agents[i].State);
        }
    }

    private static float SocialSignalAt(float distance)
    {
        Simulation sim = Ready(2);
        Agent observer = sim.Agents[0], source = sim.Agents[1];
        observer.Position = new Vector2(20f, 20f);
        source.Position = observer.Position + new Vector2(distance, 0f);
        source.State = AgentState.Evacuating;
        source.PanicLevel = 0.8f;
        source.ActivateDirectAlarm(0, 30, 0.5f, 123);
        sim.EvacuationSystem.UpdatePanic(sim);
        return observer.LastSocialPanicStimulus;
    }

    private static Vector2 Travel(float dt, int ticks)
    {
        SimulationConfig config = new() { Agent = new AgentConfig { Dt = dt } };
        Simulation sim = new(1, config, 10);
        Agent a = sim.Agents[0];
        a.Position = new Vector2(20f, 20f);
        a.Velocity = new Vector2(0.06f, 0f);
        for (int i = 0; i < ticks; i++)
        {
            sim.MoveAgent(a);
        }
        return a.Position;
    }

    private static Simulation Ready(int count, int lanes = 3)
    {
        Simulation sim = new(0, randomSeed: 20260905);
        Assert.True(sim.Environment.AddExitFromClick(28f, 0.3f, lanes));
        sim.SpawnAgents(count);
        return sim;
    }
}
