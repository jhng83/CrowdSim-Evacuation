using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;
using CrowdSim.Diagnostics;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class FramePerformanceTests
{
    [Fact]
    public void SightBoundsPreserveOriginalStrictWallEdgeResultsIncludingDegenerateRays()
    {
        Random random = new(731);
        for (int i = 0; i < 100000; i++)
        {
            float x = Next(160), y = Next(120);
            Wall wall = new(x, y, x + Next(10), y + Next(10));
            Vector2 a = new(Next(180) - 10, Next(140) - 10), b = new(Next(180) - 10, Next(140) - 10);
            if (i % 7 == 0) a = new Vector2(wall.X1, wall.Y1);
            if (i % 11 == 0) b = a;
            if (i % 13 == 0) { a.Y = wall.Y1; b.Y = wall.Y1; }
            if (i % 17 == 0) { a.X = wall.X2 + .00001f; b.X = a.X; }
            Assert.Equal(Original(a, b, wall), EvacuationSystem.LineIntersectsRect(a, b, wall));
        }
        Wall rotated = new(10, 10, 20, 20) { OrientedFootprint = new OrientedRectangle(new Vector2(15), new Vector2(4, 1), .73f) };
        Assert.Equal(rotated.OrientedFootprint.Value.IntersectsSegment(Vector2.Zero, new Vector2(30)),
            EvacuationSystem.LineIntersectsRect(Vector2.Zero, new Vector2(30), rotated));
        float Next(float max) => (float)random.NextDouble() * max;
    }

    // Unmodified reference predicate: preserve strict crossing, rather than replacing it with
    // an inclusive slab intersection that would change collinear/endpoint sight behavior.
    private static bool Original(Vector2 a, Vector2 b, Wall w)
    {
        Vector2 p = new(w.X1, w.Y1), q = new(w.X2, w.Y1), r = new(w.X2, w.Y2), s = new(w.X1, w.Y2);
        return Crosses(a, b, p, q) || Crosses(a, b, q, r) || Crosses(a, b, r, s) || Crosses(a, b, s, p);
    }
    private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 p, Vector2 q, Vector2 r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WarmPhysicalSegmentQueriesDoNotAllocateClosuresWithOrWithoutWalls(bool withoutWalls)
    {
        Simulation model = new();
        if (withoutWalls) model.Environment.Walls.Clear();
        model.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(20, 20), 1));
        Vector2 a = new(2, 2), b = new(3, 3);
        for (int i = 0; i < 100; i++) model.HasTraversablePath(a, b, .4f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool clear = true;
        for (int i = 0; i < 1000; i++) clear &= model.HasTraversablePath(a, b, .4f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(clear); Assert.Equal(0, allocated);
        Assert.False(model.HasTraversablePath(new Vector2(18, 20), new Vector2(22, 20), .4f));
        model.Environment.CircularBlockages.Clear();
        model.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(20, 20), 1) { IsFurniture = true });
        Assert.True(model.HasLineOfSight(new Vector2(18, 20), new Vector2(22, 20)));
        Assert.False(model.HasTraversablePath(new Vector2(18, 20), new Vector2(22, 20), .4f));
    }

    [Fact]
    public void ViewReuseComparesExactDetachedValuesAndInvalidatesEveryVisualInput()
    {
        ViewBody Body(ViewShape shape = ViewShape.Box, ViewKind kind = ViewKind.Table, Vector3? position = null,
            Vector3? size = null, float angle = 0, AgentState state = AgentState.Wandering,
            SeatingPosture posture = SeatingPosture.Walking, float rise = 0, ChairState motion = ChairState.Upright,
            float energy = 1, bool pillar = false)
            => new("same-id", shape, kind, position ?? Vector3.One, size ?? new Vector3(2), angle, state, posture, rise, motion, energy, pillar);
        ViewBody original = Body();
        Assert.True(original.HasSameVisuals(Body()));
        ViewBody[] changed = { Body(shape: ViewShape.Cylinder), Body(kind: ViewKind.Chair), Body(position: new Vector3(1.000001f, 1, 1)),
            Body(size: new Vector3(3)), Body(angle: .001f), Body(state: AgentState.Injured), Body(posture: SeatingPosture.Seated),
            Body(rise: .2f), Body(motion: ChairState.Airborne), Body(energy: .8f), Body(pillar: true) };
        Assert.All(changed, body => Assert.False(original.HasSameVisuals(body)));
        Assert.False(original.HasSameVisuals(null!));
    }

    [Theory]
    [InlineData(.01f)] [InlineData(.08f)] [InlineData(.5f)]
    public void ScalarCircleLoopMatchesOriginalVectorMathIncludingContactClampsAndFarFloor(float decay)
    {
        Random random = new(731);
        for (int trial = 0; trial < 60; trial++)
        {
            Simulation model = new(config: new SimulationConfig { Agent = new AgentConfig { SfmB = decay } }, randomSeed: 731);
            model.Environment.Walls.Clear(); model.SpawnAgentsAtPositions(new[] { new Vector2(28, 17) });
            Agent agent = model.Agents[0]; agent.Velocity = new Vector2(.03f, -.02f);
            Vector2 expected = Vector2.Zero;
            for (int i = 0; i < 80; i++)
            {
                Vector2 center = i == 0 ? agent.Position : i == 1 ? agent.Position + new Vector2(.1f) :
                    new Vector2((float)random.NextDouble() * 56, (float)random.NextDouble() * 34);
                CircleBlockage circle = new(center, .25f + (float)random.NextDouble());
                model.Environment.CircularBlockages.Add(circle);
                Vector2 difference = agent.Position - center; float distance = difference.Length();
                Vector2 normal = distance > VectorMath.Epsilon ? difference / distance : Vector2.UnitX;
                float surface = distance - circle.Radius, radius = model.Config.Agent.Radius;
                float exponent = Math.Clamp((radius - surface) / Math.Max(decay, 1e-5f), -60f, 20f);
                Vector2 force = model.Config.Agent.SfmA * MathF.Exp(exponent) * normal;
                if (surface < radius)
                {
                    float overlap = radius - surface; Vector2 tangent = VectorMath.Perpendicular(normal);
                    force += model.Config.Agent.SfmK * overlap * normal + model.Config.Agent.SfmKappa * overlap *
                        Vector2.Dot(-agent.Velocity / CrowdSim.Core.Configuration.AgentConfig.ReferenceStepSeconds, tangent) * tangent;
                }
                if (!VectorMath.IsFinite(force)) force = Vector2.Zero;
                else
                {
                    float length = force.Length();
                    if (length > SfmEngine.MaxRepulsion && length > VectorMath.Epsilon) force = force / length * SfmEngine.MaxRepulsion;
                }
                expected += force;
            }
            Vector2 desired = agent.Mass * -agent.Velocity /
                (CrowdSim.Core.Configuration.AgentConfig.ReferenceStepSeconds * Math.Max(.20f, model.Config.Agent.RelaxationTime));
            Assert.Equal(desired + expected, model.SfmEngine.ComputeForces(model, agent, Vector2.Zero));
        }
    }
}
