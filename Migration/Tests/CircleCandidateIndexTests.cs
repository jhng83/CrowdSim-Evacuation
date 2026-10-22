using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class CircleCandidateIndexTests
{
    [Fact]
    public void BroadPhaseIncludesEveryExactContactInOriginalOrderWithoutDuplicates()
    {
        Random random = new(731);
        List<CircleBlockage> circles = Enumerable.Range(0, 352).Select(_ =>
            new CircleBlockage(new Vector2(Next(-5, 165), Next(-5, 125)), Next(.1f, 3))).ToList();
        CircleCandidateIndex index = new(); index.Rebuild(circles);
        int returned = 0;
        for (int sample = 0; sample < 5000; sample++)
        {
            Vector2 point = new(Next(-5, 165), Next(-5, 125)); float margin = Next(0, 3);
            List<int> candidates = Candidates(index, circles.Count, point, margin);
            Assert.Equal(candidates.Order(), candidates); Assert.Equal(candidates.Distinct(), candidates);
            for (int i = 0; i < circles.Count; i++)
                if (Vector2.Distance(point, circles[i].Center) <= circles[i].Radius + margin)
                    Assert.Contains(i, candidates);
            returned += candidates.Count;
        }
        Assert.True(returned < 5000 * circles.Count / 10); // meaningful candidate culling, not elapsed time
        float Next(float low, float high) => low + (float)random.NextDouble() * (high - low);
    }

    [Theory]
    [InlineData(-2f)] [InlineData(0f)] [InlineData(2f)] [InlineData(160f)]
    public void CellEdgesAndTouchingRadiiRemainConservative(float boundary)
    {
        List<CircleBlockage> circles = FarCircles();
        foreach (float offset in new[] { -.0001f, 0f, .0001f })
            circles.Add(new CircleBlockage(new Vector2(boundary + offset, 2), .4f));
        CircleCandidateIndex index = new(); index.Rebuild(circles);
        foreach (float margin in new[] { 0f, .3f, 2f })
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector2 point = new(boundary + sign * (.4f + margin), 2);
                List<int> candidates = Candidates(index, circles.Count, point, margin);
                for (int i = 0; i < circles.Count; i++)
                    if (CircleGeometry.CouldTouch(point, circles[i].Center, circles[i].Radius + margin))
                        Assert.Contains(i, candidates);
            }
    }

    [Fact]
    public void OrderedProjectionRequeriesAfterMovementAndMatchesFullScanInDenseOverlap()
    {
        Random random = new(1731);
        List<CircleBlockage> circles = Enumerable.Range(0, 128).Select(_ =>
            new CircleBlockage(new Vector2((float)random.NextDouble() * 8, (float)random.NextDouble() * 8), .4f)).ToList();
        CircleCandidateIndex index = new(); index.Rebuild(circles);
        for (int sample = 0; sample < 2000; sample++)
        {
            Vector2 indexed = new((float)random.NextDouble() * 8, (float)random.NextDouble() * 8), linear = indexed;
            List<int> actual = new(), expected = new();
            for (int i = 0; i < circles.Count; i++) Project(ref linear, circles[i], i, expected);
            for (int i = index.Next(indexed, .3f, 0); i < circles.Count; i = index.Next(indexed, .3f, i + 1))
                Project(ref indexed, circles[i], i, actual);
            Assert.Equal(expected, actual); Assert.Equal(linear, indexed);
        }
    }

    [Fact]
    public void RebuildReflectsMovementRemovalReplacementAndReorderingWithSameCount()
    {
        List<CircleBlockage> circles = FarCircles();
        CircleCandidateIndex index = new(); index.Rebuild(circles);
        Assert.Empty(Candidates(index, circles.Count, Vector2.Zero, .3f));
        circles.RemoveAt(0); circles.Add(new CircleBlockage(Vector2.Zero, .4f)); index.Rebuild(circles);
        Assert.Contains(circles.Count - 1, Candidates(index, circles.Count, Vector2.Zero, .3f));
        circles.Reverse(); index.Rebuild(circles);
        Assert.Contains(0, Candidates(index, circles.Count, Vector2.Zero, .3f));
        circles[0] = new CircleBlockage(new Vector2(20, 20), .4f); index.Rebuild(circles);
        Assert.Empty(Candidates(index, circles.Count, Vector2.Zero, .3f));
        Assert.Contains(0, Candidates(index, circles.Count, new Vector2(20, 20), .3f));
    }

    [Fact]
    public void InvalidOrHugeGeometryAndQueriesFallBackToAuthoritativeOrder()
    {
        CircleCandidateIndex index = new(); List<CircleBlockage> circles = FarCircles();
        foreach (CircleBlockage unusual in new[] { new CircleBlockage(Vector2.Zero, 100000),
            new CircleBlockage(new Vector2(float.NaN, 0), 1), new CircleBlockage(Vector2.Zero, float.NaN) })
        {
            circles[0] = unusual; index.Rebuild(circles);
            Assert.Equal(Enumerable.Range(0, circles.Count), Candidates(index, circles.Count, Vector2.Zero, .3f));
        }
        circles = FarCircles(); index.Rebuild(circles);
        Assert.Equal(Enumerable.Range(0, circles.Count), Candidates(index, circles.Count, new Vector2(float.NaN, 0), .3f));
        Assert.Equal(Enumerable.Range(0, circles.Count), Candidates(index, circles.Count, Vector2.Zero, float.PositiveInfinity));
        index.Rebuild(circles.Take(2).ToArray());
        Assert.Equal(new[] { 0, 1 }, Candidates(index, 2, Vector2.Zero, .3f));
    }

    [Fact]
    public void CompletedStepCannotHideADirectObstacleReplacementFromLiveCollisionChecks()
    {
        Simulation model = new(randomSeed: 731);
        Assert.True(model.Environment.AddExitFromClick(.3f, 17, 3)); model.BuildWallGrid();
        for (int i = 0; i < 32; i++)
            model.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(40, 2 + i * .5f), .1f));
        model.SpawnAgentsAtPositions(new[] { new Vector2(10, 10) }); model.SetRunning(true); model.Step();
        Agent agent = model.Agents[0]; agent.State = AgentState.Evacuating;
        Vector2 center = agent.Position;
        model.Environment.CircularBlockages[0] = new CircleBlockage(center, 1f);
        // Direct authoring/test calls between steps must use current geometry even though this
        // replacement preserves count and deliberately bypasses the environment version helper.
        model.ResolveSolidCollisions(agent);
        Assert.True(Vector2.Distance(agent.Position, center) >= 1f + model.Config.Agent.Radius);
        agent.Position = center; model.Step();
        Assert.True(Vector2.Distance(agent.Position, center) >= 1f + model.Config.Agent.Radius);
    }

    private static List<CircleBlockage> FarCircles() => Enumerable.Range(0, 32).Select(i =>
        new CircleBlockage(new Vector2(80 + i, 80), .2f)).ToList();

    private static List<int> Candidates(CircleCandidateIndex index, int count, Vector2 point, float margin)
    {
        List<int> result = new();
        for (int i = index.Next(point, margin, 0); i < count; i = index.Next(point, margin, i + 1)) result.Add(i);
        return result;
    }

    private static void Project(ref Vector2 position, CircleBlockage circle, int i, List<int> touched)
    {
        float minimumDistance = circle.Radius + .3f;
        if (!CircleGeometry.CouldTouch(position, circle.Center, minimumDistance)) return;
        Vector2 offset = position - circle.Center; float distance = offset.Length();
        if (!float.IsFinite(distance) || distance >= minimumDistance) return;
        Vector2 normal = distance > 1e-6f ? offset / distance : Vector2.UnitX;
        position += normal * (minimumDistance - Math.Max(0f, distance) + .001f); touched.Add(i);
    }
}
