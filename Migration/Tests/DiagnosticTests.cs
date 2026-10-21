using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class DiagnosticTests
{
    private static Simulation Model(int count = 1)
    {
        Simulation m = new(randomSeed: 731);
        Assert.True(m.Environment.AddExitFromClick(.3f, 17, 5));
        m.SpawnAgentsAtPositions(Enumerable.Range(0, count).Select(i => new Vector2(15 + i, 17)).ToArray());
        foreach (Agent a in m.Agents)
        {
            a.State = AgentState.Evacuating; a.ExitTarget = m.Environment.Exits[0];
            a.LastDesiredDirection = Vector2.UnitX; a.LastAvoidanceDirection = Vector2.UnitY;
            a.LastHerdDirection = Vector2.UnitX; a.LastSfmForce = new Vector2(16000, 0); a.LocalCongestion = .7f;
        }
        return m;
    }
    [Fact]
    public void PopulationDenominatorsExcludeProneCasualtiesAndExitedButIncludeCrawlers()
    {
        Simulation m = Model(5);
        m.Agents[0].State = AgentState.Evacuating; m.Agents[0].PanicLevel = .2f; m.Agents[0].VisibilityFactor = .6f;
        m.Agents[1].State = AgentState.Crawling; m.Agents[1].PanicLevel = .8f; m.Agents[1].VisibilityFactor = 1;
        m.Agents[1].Intent = BehaviorIntent.AssistCasualty; m.Agents[1].AssistPauseUntilFrame = 5;
        m.Agents[2].State = AgentState.Dead; m.Agents[2].PanicLevel = 1;
        m.Agents[3].State = AgentState.Exited; m.Agents[4].State = AgentState.Fallen;
        PopulationMetrics p = PopulationMetrics.Capture(m);
        Assert.Equal(4, p.InRoom); Assert.Equal(2, p.Mobile); Assert.Equal(1, p.Crawling); Assert.Equal(1, p.Dead);
        Assert.Equal(1, p.Fallen); Assert.Equal(1, p.Exited); Assert.Equal(.5f, p.AveragePanic); Assert.Equal(.8f, p.MaximumPanic);
        Assert.Equal(.8f, p.AverageVisibility); Assert.Equal(1, p.CurrentlyHelping); Assert.Equal(0, p.GoingToAssist);
        m.Agents.ForEach(a => a.State = AgentState.Dead);
        Assert.Equal(1, PopulationMetrics.Capture(m).AverageVisibility); Assert.Equal(0, PopulationMetrics.Capture(m).AveragePanic);
    }
    [Fact]
    public void FumePopulationRetainsExitedExposureAndSeparatesImpairedAndIncapacitated()
    {
        Simulation m = Model(3); m.SetScenarioType(ScenarioType.ToxicFume);
        m.Agents[0].ToxicExposureStatus = ToxicExposureStatus.Exposed;
        m.Agents[1].ToxicExposureStatus = ToxicExposureStatus.Impaired;
        m.Agents[2].ToxicExposureStatus = ToxicExposureStatus.Incapacitated; m.Agents[2].State = AgentState.Exited;
        m.Agents[2].CumulativeToxicExposure = 9; m.Agents[2].PeakToxicConcentration = .9f;
        m.Agents[0].CurrentToxicConcentration = 1;
        PopulationMetrics p = PopulationMetrics.Capture(m);
        Assert.Equal(3, p.ToxicExposed); Assert.Equal(1, p.ToxicImpaired); Assert.Equal(1, p.ToxicIncapacitated);
        Assert.Equal(3, p.AverageToxicExposure); Assert.Equal(.9f, p.PeakToxicConcentration); Assert.Equal(1, p.ToxicHighConcentration);
    }
    [Fact]
    public void DiagnosticRowsAndExitsAreDetachedAndSelectedNavigationAndSpeedsUseNativeUnits()
    {
        Simulation m = Model(); Agent a = m.Agents[0]; a.Velocity = new Vector2(.15f, 0);
        a.SetRoute(new[] { new Vector2(10, 17), new Vector2(5, 17) }, "exit", 10);
        m.ExitMetrics["Z"] = new ExitMetrics(3, 2); m.ExitMetrics["A"] = new ExitMetrics(1, .5f);
        DiagnosticFrame saved = new(m, 0);
        Assert.Equal(new[] { "A", "Z" }, saved.Exits.Select(x => x.Name)); Assert.Equal(3, saved.Exits[1].Density);
        Assert.Contains(saved.Selected, r => r.Label == "Actual speed" && r.Value == 1f.ToString("F2") + " m/s");
        Assert.Contains(saved.Selected, r => r.Label == "Route waypoints" && r.Value == "2");
        Assert.Contains(saved.Selected, r => r.Label == "Adaptive navigation"); Assert.Contains(saved.Model, r => r.Label == "SFM A");
        a.Position = Vector2.Zero; a.LastDesiredDirection = Vector2.Zero; m.ExitMetrics.Clear();
        Assert.Equal(new Vector2(15, 17), saved.Agents[0].Position); Assert.Equal(Vector2.UnitX, saved.Agents[0].Desired);
        Assert.Equal(2, saved.Exits.Count); Assert.Throws<NotSupportedException>(() => ((IList<ExitDiagnostic>)saved.Exits).Clear());
    }
    [Fact]
    public void AssistanceRowsDistinguishApproachAttendanceTargetsAndRecovery()
    {
        Simulation m = Model(2); Agent helper = m.Agents[0];
        helper.Intent = BehaviorIntent.AssistCasualty; helper.AssistTargetAgentId = 1; helper.AssistAssignedFrame = 0;
        helper.AssistLastProgressFrame = 0; helper.AssistRecoveryStage = 2; helper.AssistSlotIndex = 1;
        var rows = AgentInspection.Capture(m, 0);
        Assert.Contains(rows, r => r.Label == "Assist phase" && r.Value == "GOING TO HELP");
        Assert.Contains(rows, r => r.Label == "Recovery" && r.Value == "Alternate side tried");
        helper.AssistPauseUntilFrame = 10;
        rows = AgentInspection.Capture(m, 0);
        Assert.Contains(rows, r => r.Label == "Helping time left" && r.Value == 1.5f.ToString("F1") + " s");
        Assert.DoesNotContain(rows, r => r.Label == "Recovery");
    }
    [Theory]
    [InlineData(DiagnosticOverlay.Desired, 6)] [InlineData(DiagnosticOverlay.Avoidance, 6)]
    [InlineData(DiagnosticOverlay.Herd, 6)] [InlineData(DiagnosticOverlay.Sfm, 6)]
    [InlineData(DiagnosticOverlay.Blast, 6)] [InlineData(DiagnosticOverlay.Vision, 40)]
    [InlineData(DiagnosticOverlay.Congestion, 0)] [InlineData(DiagnosticOverlay.Trails, 0)]
    public void EveryOverlayBuildsFiniteDetachedBatchesWithPovSelfExclusion(DiagnosticOverlay kind, int expectedLineVertices)
    {
        Simulation m = Model(); m.SetRunning(true); m.TriggerExplosion(25, 17);
        DiagnosticFrame frame = new(m); DiagnosticTrails trails = new();
        BlastMeshData mesh = DiagnosticOverlays.Build(frame, kind, trails);
        Assert.Equal(expectedLineVertices, mesh.Lines.Count); Assert.All(mesh.Lines.Concat(mesh.Quads), p => Assert.True(float.IsFinite(p.X + p.Y + p.Z)));
        if (kind == DiagnosticOverlay.Congestion) Assert.Equal(64, mesh.Quads.Count);
        BlastMeshData hidden = DiagnosticOverlays.Build(frame, kind, trails, 0); Assert.Empty(hidden.Lines); Assert.Empty(hidden.Quads);
        Assert.Equal(new Vector2(15, 17), m.Agents[0].Position);
    }
    [Theory]
    [InlineData(DiagnosticOverlay.Desired, 32)] [InlineData(DiagnosticOverlay.Avoidance, 30)]
    [InlineData(DiagnosticOverlay.Herd, 28)] [InlineData(DiagnosticOverlay.Sfm, 45)] [InlineData(DiagnosticOverlay.Blast, 26)]
    public void VectorLengthsMatchNativeMetricScalingAndCaps(DiagnosticOverlay kind, int legacyLength)
    {
        Simulation m = Model(); m.SetRunning(true); m.TriggerExplosion(25, 17);
        BlastMeshData mesh = DiagnosticOverlays.Build(new DiagnosticFrame(m), kind, new DiagnosticTrails());
        Assert.InRange(MathF.Abs(legacyLength * 1.70f / 42 - Vector3.Distance(mesh.Lines[0], mesh.Lines[1])), 0, .000002f);
        if (kind == DiagnosticOverlay.Blast) Assert.True(mesh.Lines[1].X < mesh.Lines[0].X);
    }
    [Fact]
    public void VisionUsesConfiguredRangeAndAngleAndZeroOrNonfiniteVectorsHaveNoArrows()
    {
        Simulation m = Model(); Agent a = m.Agents[0]; a.Velocity = Vector2.UnitX;
        DiagnosticFrame frame = new(m); BlastMeshData cone = DiagnosticOverlays.Build(frame, DiagnosticOverlay.Vision, new DiagnosticTrails());
        Assert.Equal(frame.VisionRange, Vector3.Distance(cone.Lines[0], cone.Lines[1]), 5);
        float theta = MathF.Atan2(cone.Lines[1].Z - a.Position.Y, cone.Lines[1].X - a.Position.X);
        Assert.Equal(-frame.VisionHalfAngle * MathF.PI / 180, theta, 5);
        a.LastDesiredDirection = Vector2.Zero; a.LastSfmForce = new Vector2(float.NaN, 0);
        Assert.Empty(DiagnosticOverlays.Build(new DiagnosticFrame(m), DiagnosticOverlay.Desired, new DiagnosticTrails()).Lines);
        Assert.Empty(DiagnosticOverlays.Build(new DiagnosticFrame(m), DiagnosticOverlay.Sfm, new DiagnosticTrails()).Lines);
        a.LastDesiredDirection = new Vector2(float.MaxValue, float.MaxValue);
        Assert.Empty(DiagnosticOverlays.Build(new DiagnosticFrame(m), DiagnosticOverlay.Desired, new DiagnosticTrails()).Lines);
    }
    [Fact]
    public void TrailsFollowTickCadenceAndCapThenClearOnReplayAndExit()
    {
        Simulation m = Model(); m.Agents[0].State = AgentState.Dead; m.SetRunning(true); DiagnosticTrails history = new();
        for (int i = 0; i < 160; i++) { DiagnosticFrame frame = new(m); history.Observe(frame, true); history.Observe(frame, true); m.Step(); }
        Assert.Equal(40, history.Points(0).Count);
        Simulation replay = Model(); history.Observe(new DiagnosticFrame(replay), true); Assert.Single(history.Points(0));
        replay.Agents[0].State = AgentState.Exited; history.Observe(new DiagnosticFrame(replay), true); Assert.Empty(history.Points(0));
    }
    [Fact]
    public void PausedHybridChangesOnlyTheFlagAndRebasesAuthoredReplayWithoutCapturingDamage()
    {
        WorkspaceSession s = Ready(ScenarioType.Explosion, false); s.ToggleRunning(); s.TriggerExplosion(new Vector2(28, 17));
        for (int i = 0; i < 10; i++) s.Advance(s.StepSeconds);
        Assert.False(s.SetPausedHybrid(true)); s.ToggleRunning();
        var positions = s.Frame.Bodies.Select(b => (b.Position, b.State)).ToArray(); int tick = s.Tick;
        Assert.True(s.SetPausedHybrid(true)); Assert.True(s.Hybrid); Assert.Equal(tick, s.Tick);
        Assert.Equal(positions, s.Frame.Bodies.Select(b => (b.Position, b.State)));
        WorkspaceSession loaded = WorkspaceSession.FromJson(s.ExportAuthoredJson("changed")); Assert.True(loaded.Hybrid);
        Assert.True(s.Replay()); Assert.Equal(0, s.Tick); Assert.True(s.Hybrid); Assert.False(s.Frame.HazardReleased);
    }
    private static WorkspaceSession Ready(ScenarioType scenario, bool hybrid)
    {
        WorkspaceSession s = new(scenario); s.SetHybrid(hybrid);
        s.Place(PlacementTool.Exit, new Vector2(0, 17), 5); s.Place(PlacementTool.Agents, new Vector2(24, 17), 15);
        if (scenario == ScenarioType.ToxicFume) { s.PlaceFumeSource(new Vector2(26, 17)); s.ComputeFume(); }
        Assert.True(s.Finalise()); return s;
    }
    [Theory]
    [InlineData(ScenarioType.Explosion, false)] [InlineData(ScenarioType.Explosion, true)]
    [InlineData(ScenarioType.ToxicFume, false)] [InlineData(ScenarioType.ToxicFume, true)]
    public void AllDiagnosticsAndHiddenOverlaysLeaveSeededPhysicsExposureAndAuthoredExportsIdentical(ScenarioType scenario, bool hybrid)
    {
        WorkspaceSession shown = Ready(scenario, hybrid), control = Ready(scenario, hybrid);
        foreach (WorkspaceSession s in new[] { shown, control }) { s.ToggleRunning(); if (scenario == ScenarioType.Explosion) s.TriggerExplosion(new Vector2(28, 17)); else s.ReleaseFume(); }
        shown.Overlays = (DiagnosticOverlay)255; shown.ToggleInspector(); DiagnosticTrails trails = new();
        for (int i = 0; i < 45; i++)
        {
            shown.SelectAgentById(shown.Frame.Agents.FirstOrDefault()?.Id);
            trails.Observe(shown.Diagnostics, true);
            foreach (DiagnosticOverlay kind in Enum.GetValues<DiagnosticOverlay>()) if (kind != DiagnosticOverlay.None) DiagnosticOverlays.Build(shown.Diagnostics, kind, trails);
            shown.Advance(shown.StepSeconds); control.Advance(control.StepSeconds);
        }
        Assert.False(shown.InspectorVisible); Assert.Equal((DiagnosticOverlay)255, shown.Overlays);
        Assert.Equal(control.Frame.Bodies.Select(b => (b.Position, b.State, b.Energy)), shown.Frame.Bodies.Select(b => (b.Position, b.State, b.Energy)));
        Assert.Equal(control.Frame.MaximumExposure, shown.Frame.MaximumExposure); Assert.Equal(control.ExportAuthoredJson("same"), shown.ExportAuthoredJson("same"));
        Assert.True(shown.Replay()); Assert.Equal((DiagnosticOverlay)255, shown.Overlays);
    }
    [Theory]
    [InlineData(220, false)] [InlineData(500, false)] [InlineData(1000, false)]
    [InlineData(220, true)] [InlineData(500, true)] [InlineData(1000, true)]
    public void ScaleFixturesHaveExactPopulationAndIdenticalHybridLayoutsWithNoOverlaps(int count, bool furniture)
    {
        Simulation a = CrowdScaleLayout.Create(count, furniture, false), b = CrowdScaleLayout.Create(count, furniture, true);
        Assert.Equal(count, a.Environment.PlannedAgentCount); Assert.Equal(4, a.Environment.Exits.Count);
        var starts = a.Environment.GetPlannedAgentPositions(); Assert.Equal(starts, b.Environment.GetPlannedAgentPositions());
        Assert.Equal(furniture ? 8 : 0, a.Environment.Furniture.Count);
        a.SpawnAgentsAtPositions(starts); Assert.Equal(furniture ? 80 : 0, a.Agents.Count(p => p.Posture == SeatingPosture.Seated));
        for (int i = 0; i < starts.Count; i++)
        for (int j = i + 1; j < starts.Count; j++) Assert.True(Vector2.DistanceSquared(starts[i], starts[j]) >= .4f * .4f);
        Assert.Equal(count, a.Agents.Count); Assert.All(a.Agents, p => Assert.True(p.Position.X > 0 && p.Position.Y > 0));
        Assert.Equal(count, WorkspaceSession.ScaleExample(count, furniture, false, ScenarioType.Explosion).PlannedCount);
    }
    [Fact]
    public void FrameStatisticsKeepBoundedWallFrameRateDistinctFromCpuAndRejectInvalidIntervals()
    {
        FrameStatistics s = new();
        for (int i = 0; i < 240; i++) s.Add(20, 2, 3, 1, 500);
        s.Add(double.NaN, 100, 100, 100, 1); s.Add(0, 0, 0, 0, 0);
        Assert.Equal(120, s.Samples); Assert.Equal(50, s.FramesPerSecond, 6); Assert.Equal(20, s.Percentile(.95));
        Assert.Equal(2, s.PhysicsCommandsMilliseconds); Assert.Equal(3, s.WorldCommandsMilliseconds); Assert.Equal(500, s.ManagedBytes);
        s.Clear(); Assert.Equal(0, s.Samples); Assert.Equal(0, s.FramesPerSecond); Assert.Equal(0, s.ManagedBytes);
    }
    [Fact]
    public void ArchitecturalPillarTagsAndDefaultGridDoNotChangeThePhysicalGeometry()
    {
        Simulation model = Model(); int perimeter = model.Environment.Walls.Count;
        Assert.True(model.Environment.AddBlockageFromClick(25, 20, 1)); Assert.True(model.Environment.AddCircularBlockageFromClick(35, 20));
        var frame = PresentationFrame.Capture(model); var pillars = frame.Bodies.Where(b => b.IsPillar).ToArray();
        Assert.Equal(2, pillars.Length); Assert.Contains(pillars, p => p.Shape == ViewShape.Box); Assert.Contains(pillars, p => p.Shape == ViewShape.Cylinder);
        Assert.Equal(perimeter, model.Environment.Walls.Count - model.Environment.Blockages.Count - model.Environment.BlueWalls.Count);
        Assert.True(model.Environment.IsPointBlocked(new Vector2(25, 20), .2f)); Assert.True(model.Environment.IsPointBlocked(new Vector2(35, 20), .2f)); Assert.True(new WorkspaceSession().ShowGrid);
        Assert.DoesNotContain(frame.Bodies, b => b.Id.StartsWith("perimeter"));
    }
}
