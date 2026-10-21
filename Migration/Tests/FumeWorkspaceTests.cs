using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class FumeWorkspaceTests
{
    private static WorkspaceSession Builder()
    {
        WorkspaceSession s = new(ScenarioType.ToxicFume);
        Assert.True(s.Place(PlacementTool.Exit, new Vector2(0, 17), 3));
        Assert.True(s.Place(PlacementTool.Agents, new Vector2(24, 17), 15)); return s;
    }
    private static string Fixture()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "CrowdSim.sln"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root!.FullName, "tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json"));
    }
    [Fact]
    public void CompleteAuthorComputePreviewReleaseAndReplayLifecycleKeepsStartsAndAuthoredSettings()
    {
        WorkspaceSession s = Builder(); Assert.False(s.CanFinalise); Assert.False(s.ComputeFume());
        Assert.True(s.PlaceFumeSource(new Vector2(26, 17))); Assert.False(s.CanFinalise);
        Assert.True(s.SetFumeValue(4, 90)); Assert.True(s.SetFumeValue(5, .5f));
        Assert.True(s.ComputeFume()); Assert.True(s.CanFinalise); Assert.True(s.Fume.CacheReady);
        Assert.True(s.Fume.ReachableCells > 0); Assert.True(s.Fume.CacheBytes > 0); Assert.Equal(.5f, s.Fume.GridResolution);
        s.MarkSaved("test"); string authored = s.ExportAuthoredJson("test");
        var starts = s.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
        Assert.True(s.SetFumePreview(25)); Assert.Equal(25, s.Fume.DisplaySeconds); Assert.True(s.Fume.Preview);
        Assert.Equal(0, s.Tick); Assert.Equal(0, s.Fume.AverageExposure); Assert.False(s.IsDirty);
        Assert.True(s.Finalise()); Assert.False(s.Fume.Preview); Assert.Equal(0, s.Fume.DisplaySeconds);
        Assert.False(s.ReleaseFume()); s.ToggleRunning(); Assert.True(s.ReleaseFume()); Assert.False(s.ReleaseFume());
        for (int i = 0; i < 120; i++) s.Advance(s.StepSeconds);
        Assert.True(s.Fume.ElapsedSeconds > 0); Assert.True(s.Fume.PeakConcentration > 0);
        Assert.False(s.SetFumeValue(0, 2)); Assert.False(s.ResetFumeDefaults()); Assert.False(s.PlaceFumeSource(new Vector2(30, 17)));
        Assert.False(s.ComputeFume()); Assert.False(s.SetFumePreview(40));
        Assert.Equal(authored, s.ExportAuthoredJson("test")); Assert.True(s.Replay());
        Assert.True(s.HasFumeCache); Assert.False(s.FumeReleased); Assert.Equal(0, s.Tick);
        Assert.Equal(0, s.Fume.AverageExposure); Assert.Equal(starts, s.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position));
        Assert.Equal(authored, s.ExportAuthoredJson("test"));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void EveryFumeControlUsesTheActualConfigClampAndInvalidatesComputedField(int index)
    {
        WorkspaceSession s = Builder(); s.PlaceFumeSource(new Vector2(26, 17)); s.ComputeFume();
        ToxicFumeConfig expected = new(); float[] v = FumeControls.Read(expected); v[index] = FumeControls.All[index].Maximum + 100;
        FumeControls.Apply(expected, v);
        Assert.True(s.SetFumeValue(index, v[index])); Assert.Equal(FumeControls.Read(expected), s.FumeValues);
        Assert.False(s.HasFumeCache); Assert.False(s.CanFinalise); Assert.Empty(s.Fume.Cells);
        Assert.True(s.ComputeFume()); Assert.True(s.ResetFumeDefaults());
        Assert.Equal(FumeControls.Read(new ToxicFumeConfig()), s.FumeValues); Assert.False(s.HasFumeCache);
    }
    [Theory]
    [InlineData(float.NaN, 17)] [InlineData(float.PositiveInfinity, 17)] [InlineData(-1, 17)] [InlineData(0, 0)]
    public void InvalidSourcePlacementDoesNotReplaceTheValidSourceOrCache(float x, float y)
    {
        WorkspaceSession s = Builder(); s.PlaceFumeSource(new Vector2(26, 17)); s.ComputeFume(); s.MarkSaved("safe");
        var prior = s.Fume;
        Assert.False(s.CanPlaceFumeSource(new Vector2(x, y))); Assert.False(s.PlaceFumeSource(new Vector2(x, y)));
        Assert.Equal(new Vector2(26, 17), s.Fume.Source); Assert.True(s.HasFumeCache); Assert.False(s.IsDirty);
        Assert.Same(prior, s.Fume); Assert.False(s.SetFumeValue(0, float.NaN)); Assert.True(s.HasFumeCache);
    }
    [Fact]
    public void GeometryUndoSourceAndParameterChangesRequireRecomputationButBehaviourAndDisplayDoNot()
    {
        WorkspaceSession s = Builder(); s.PlaceFumeSource(new Vector2(26, 17)); s.ComputeFume();
        s.SetBehaviour(0, .2f); s.SetHybrid(true); Assert.True(s.HasFumeCache);
        s.Place(PlacementTool.Square, new Vector2(35, 20), 2); Assert.False(s.HasFumeCache); Assert.False(s.CanFinalise);
        s.ComputeFume(); Assert.True(s.Undo()); Assert.False(s.HasFumeCache);
        s.ComputeFume(); Assert.True(s.PlaceFumeSource(new Vector2(28, 17))); Assert.False(s.HasFumeCache);
        s.ComputeFume(); s.MarkSaved("safe"); s.ShowFume = false; s.ShowFumeGrid = true;
        string json = s.ExportAuthoredJson("safe"); var bodies = s.Frame.Bodies.Select(b => b.Position).ToArray();
        foreach (float time in new[] { 0, 10, 50, 10000 }) Assert.True(s.SetFumePreview(time));
        Assert.True(s.HasFumeCache); Assert.False(s.IsDirty); Assert.Equal(json, s.ExportAuthoredJson("safe"));
        Assert.Equal(bodies, s.Frame.Bodies.Select(b => b.Position)); Assert.Equal(0, s.Fume.AverageExposure);
        Assert.False(s.SetFumePreview(float.NaN)); Assert.False(s.FumeReleased); Assert.Equal(0, s.Tick);
    }
    [Fact]
    public void BlockedSourceAndResizeOutsideSourceRejectWithoutTouchingAuthoredData()
    {
        WorkspaceSession s = new(ScenarioType.ToxicFume); s.PlaceFumeSource(new Vector2(40, 20));
        Assert.False(s.Resize(20, 20)); Assert.True(s.Resize(60, 40)); Assert.Equal(new Vector2(40, 20), s.Fume.Source);
        Assert.True(s.Place(PlacementTool.Circle, new Vector2(25, 20), 1)); s.MarkSaved("safe");
        Assert.False(s.PlaceFumeSource(new Vector2(25, 20))); Assert.False(s.IsDirty);
    }
    [Fact]
    public void Saved205ShortcutStillWorksAndEditedLayoutsRequireExplicitCompute()
    {
        WorkspaceSession loaded = WorkspaceSession.FromJson(Fixture()); Assert.True(loaded.CanFinalise); Assert.False(loaded.HasFumeCache);
        loaded.ComputeFume(); Assert.True(loaded.SetFumePreview(30));
        Assert.True(loaded.Place(PlacementTool.Agents, new Vector2(30, 28), 1));
        Assert.False(loaded.CanFinalise); loaded.ComputeFume(); Assert.True(loaded.Finalise());
        Assert.Equal(206, loaded.Frame.Population); Assert.Equal(80, loaded.Frame.SeatedAgents);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PreviewAndPlumeGridVisibilityCannotChange205PersonRuntimeResultsInEitherHybridMode(bool hybrid)
    {
        WorkspaceSession a = WorkspaceSession.FromJson(Fixture()), b = WorkspaceSession.FromJson(Fixture());
        a.SetHybrid(hybrid); b.SetHybrid(hybrid); a.ComputeFume();
        foreach (float t in new[] { 0, 20, 60, 100 }) a.SetFumePreview(t);
        a.Finalise(); b.Finalise(); a.MarkSaved("same"); b.MarkSaved("same");
        a.ToggleRunning(); b.ToggleRunning(); a.ReleaseFume(); b.ReleaseFume();
        for (int i = 0; i < 120; i++)
        {
            a.ShowFume = i % 2 == 0; a.ShowFumeGrid = i % 3 == 0;
            a.Advance(a.StepSeconds); b.Advance(b.StepSeconds);
            Assert.Equal(a.Frame.Bodies.Select(p => (p.Position, p.State)), b.Frame.Bodies.Select(p => (p.Position, p.State)));
            Assert.Equal(a.Fume.AverageExposure, b.Fume.AverageExposure); Assert.Equal(a.Fume.Incapacitated, b.Fume.Incapacitated);
        }
        Assert.False(a.IsDirty); Assert.Equal(a.ExportAuthoredJson("same"), b.ExportAuthoredJson("same"));
        a.ToggleRunning(); float elapsed = a.Fume.ElapsedSeconds; a.Advance(10); Assert.Equal(elapsed, a.Fume.ElapsedSeconds);
        a.Replay(); Assert.Equal(80, a.Frame.SeatedAgents); Assert.Equal(205, a.Frame.Population);
        Assert.False(a.ShowFumeGrid); // Last tick 119: false; view state remains after replay.
    }
    [Fact]
    public void DetachedSamplesMatchActualCacheAndDoNotPaintBlockedCells()
    {
        Simulation model = new(randomSeed: 42); model.SetScenarioType(ScenarioType.ToxicFume);
        model.Environment.AddBlockageFromClick(28, 17, 2); model.BuildWallGrid();
        model.ToxicFumeSystem.TrySetSource(model, new Vector2(24, 17)); model.PrecomputeToxicFume(); model.ToxicFumeSystem.PreviewTimeSeconds = 30;
        FumeFrame frame = FumeFrame.Capture(model, true);
        Assert.NotEmpty(frame.Cells); Assert.Equal(model.ToxicFumeSystem.EnumerateCells(30, .025f, 1).Select(c => (c.Center, c.Concentration)), frame.Cells.Select(c => (c.Center, c.Concentration)));
        Assert.Equal(frame.Cells.Count * 4, frame.Layers.Sum(l => l.Quads.Count));
        Assert.All(frame.Cells, c => Assert.False(model.Environment.IsFumeBlocked(c.Center, .02f)));
        Assert.All(frame.Layers.SelectMany(l => l.Quads), p => { Assert.InRange(p.X, 0, model.Environment.Width); Assert.InRange(p.Z, 0, model.Environment.Height); });
        var first = frame.Cells[0].Concentration; model.ToxicFumeSystem.InvalidatePrecomputation(); Assert.Equal(first, frame.Cells[0].Concentration);
        Assert.False(FumeFrame.Capture(model, true).CacheReady);
    }
    [Fact]
    public void ExposureMetricsRetainPermanentIncapacityAfterFieldDecay()
    {
        Simulation model = new(agentCount: 0, randomSeed: 42); model.SetScenarioType(ScenarioType.ToxicFume);
        model.Environment.AddExitFromClick(0, 17, 3); model.SpawnAgents(1);
        var agent = model.Agents[0]; agent.Position = new Vector2(25, 17); agent.BeginReaction(0, 10000);
        model.ToxicFumeSystem.Configuration.SetValues(.6f, .3f, 12, 1, 0, 0, .12f, .55f, 1, 2);
        model.ToxicFumeSystem.TrySetSource(model, agent.Position); model.PrecomputeToxicFume(); model.SetRunning(true); model.TriggerToxicFumeRelease();
        for (int i = 0; i < 240; i++) model.Step();
        FumeFrame frame = FumeFrame.Capture(model, false);
        Assert.Equal(1, frame.Incapacitated); Assert.Equal(AgentState.Incapacitated, agent.State); Assert.Equal(0, agent.ToxicMobilityFactor);
        Assert.Equal(0, model.ToxicFumeSystem.GetCurrentConcentration(model, agent.Position)); Assert.True(frame.AverageExposure >= 2);
    }
    [Fact]
    public void BevelledPresentationMeshPreservesUnitFootprintNormalsAndOutwardWinding()
    {
        PresentationMesh mesh = PresentationAssets.BevelledBox();
        Assert.Equal(new Vector3(-.5f), mesh.Vertices.Aggregate(Vector3.Min)); Assert.Equal(new Vector3(.5f), mesh.Vertices.Aggregate(Vector3.Max));
        Assert.All(mesh.Normals, n => Assert.InRange(n.Length(), .99999f, 1.00001f));
        for (int i = 0; i < mesh.Triangles.Count; i += 3)
        {
            int a = mesh.Triangles[i], b = mesh.Triangles[i + 1], c = mesh.Triangles[i + 2];
            Vector3 cross = Vector3.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
            Assert.True(Vector3.Dot(cross, mesh.Normals[a] + mesh.Normals[b] + mesh.Normals[c]) > 0);
        }
    }
}
