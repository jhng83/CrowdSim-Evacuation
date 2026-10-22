using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class PerformancePatchTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(255)] [InlineData(256)] [InlineData(257)]
    [InlineData(1023)] [InlineData(120000)]
    public void InstancingSplitsEveryVolumeExactlyOnceWithoutLosingTheLastPartialBlock(int instances)
    {
        int batches = InstanceBatchPlan.BatchCount(instances), submitted = 0;
        for (int i = 0; i < batches; i++)
        {
            int count = InstanceBatchPlan.CountInBatch(instances, i);
            Assert.InRange(count, 1, 256); submitted += count;
        }
        Assert.Equal(instances, submitted);
        Assert.Throws<ArgumentOutOfRangeException>(() => InstanceBatchPlan.CountInBatch(instances, batches));
        Assert.Throws<ArgumentOutOfRangeException>(() => InstanceBatchPlan.BatchCount(-1));
    }

    [Fact]
    public void CircleBroadphaseNeverRejectsAContactAcceptedByTheOriginalDistanceGuard()
    {
        Random random = new(731);
        for (int i = 0; i < 50000; i++)
        {
            Vector2 center = new((float)random.NextDouble() * 160, (float)random.NextDouble() * 120);
            float reach = .001f + (float)random.NextDouble() * 5;
            float angle = (float)random.NextDouble() * MathF.PI * 2;
            float range = reach * (i % 3 == 0 ? 1 : (float)random.NextDouble() * 2);
            Vector2 point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * range;
            if (Vector2.Distance(point, center) <= reach) Assert.True(CircleGeometry.CouldTouch(point, center, reach));
        }
        Assert.True(CircleGeometry.CouldTouch(new Vector2(float.NaN, 0), Vector2.Zero, 1));
        Assert.True(CircleGeometry.CouldTouch(Vector2.Zero, Vector2.Zero, float.NaN));
    }

    [Theory]
    [InlineData(ScenarioType.Explosion, false)] [InlineData(ScenarioType.Explosion, true)]
    [InlineData(ScenarioType.ToxicFume, false)] [InlineData(ScenarioType.ToxicFume, true)]
    public void DenseFixtureHasExactAuthoredPopulationSeatsAndStableStartsInBothModes(ScenarioType scenario, bool hybrid)
    {
        WorkspaceSession s = WorkspaceSession.DenseExample(hybrid, scenario);
        Assert.Equal(800, s.PlannedCount); Assert.Equal(32, s.FurnitureCount); Assert.Equal(320, s.Frame.SeatedAgents);
        WorkspaceSession loaded = WorkspaceSession.FromJson(s.ExportAuthoredJson("dense"));
        Assert.Equal(800, s.Frame.Bodies.Count(b => b.Kind == ViewKind.Agent));
        Assert.Equal(s.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(a => a.Position), loaded.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(a => a.Position));
        Assert.Equal(800, loaded.PlannedCount); Assert.Equal(hybrid, loaded.Hybrid);
        if (scenario == ScenarioType.ToxicFume) Assert.True(s.ComputeFume());
        Assert.True(s.Finalise()); Assert.Equal(800, s.Frame.Population); Assert.True(s.Replay());
        Assert.Equal(800, s.Frame.Population); Assert.Equal(320, s.Frame.SeatedAgents);
    }

    [Fact]
    public void SightSnapshotDoesNotSurviveATickOrHideNewArchitectureOrIncludeLowFurniture()
    {
        Simulation s = new(randomSeed: 731);
        Assert.True(s.Environment.AddExitFromClick(.3f, 17, 3));
        Assert.True(s.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(20, 15)));
        Assert.True(s.Environment.AddAgentSpawnGroupFromClick(8, 8, 5)); s.BuildWallGrid();
        s.SpawnAgentsAtPositions(s.Environment.GetPlannedAgentPositions()); s.SetRunning(true); s.Step();
        Assert.True(s.HasLineOfSight(new Vector2(18, 15), new Vector2(22, 15))); // furniture stays transparent
        s.Environment.Walls.Add(new Wall(19, 13, 21, 17));
        Assert.False(s.HasLineOfSight(new Vector2(18, 15), new Vector2(22, 15))); // outside-Step queries use live lists
        s.Step(); Assert.False(s.HasLineOfSight(new Vector2(18, 15), new Vector2(22, 15)));
        s.Environment.CircularBlockages.Add(new CircleBlockage(new Vector2(30, 15), 1));
        Assert.False(s.HasLineOfSight(new Vector2(28, 15), new Vector2(32, 15)));
    }

    [Theory]
    [InlineData(ScenarioType.Explosion)] [InlineData(ScenarioType.ToxicFume)]
    public void PhaseReadoutsAreDetachedAndDoNotChangeReplayOrFixedTickBehavior(ScenarioType scenario)
    {
        var s = WorkspaceSession.ScaleExample(220, true, false, scenario);
        if (scenario == ScenarioType.ToxicFume) Assert.True(s.ComputeFume());
        Assert.True(s.Finalise()); string authored = s.ExportAuthoredJson("same"); s.ToggleRunning(); s.Advance(s.StepSeconds);
        DiagnosticFrame frame = s.Diagnostics;
        double[] timing = { frame.GridMilliseconds, frame.HazardMilliseconds, frame.ResponseMilliseconds, frame.FurnitureMilliseconds,
            frame.PreparationMilliseconds, frame.MovementMilliseconds, frame.ContactsMilliseconds, frame.ExitMilliseconds,
            frame.SfmMilliseconds, frame.CollisionMilliseconds };
        Assert.All(timing, t => Assert.True(double.IsFinite(t) && t >= 0));
        double frozen = frame.PhysicsMilliseconds;
        s.Advance(s.StepSeconds); Assert.Equal(frozen, frame.PhysicsMilliseconds);
        Assert.Equal(2, s.Tick); Assert.Equal(.15f, s.StepSeconds); Assert.Equal(authored, s.ExportAuthoredJson("same"));
        Assert.True(s.Replay()); Assert.Equal(0, s.Tick);
    }
}
