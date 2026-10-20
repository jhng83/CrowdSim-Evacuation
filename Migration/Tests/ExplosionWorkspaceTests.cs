using System.Globalization;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class ExplosionWorkspaceTests
{
    [Theory]
    [InlineData(0, "NaN")]
    [InlineData(0, "Infinity")]
    [InlineData(0, "")]
    [InlineData(0, "3.16")]
    [InlineData(1, "1.58")]
    [InlineData(4, "200.01")]
    [InlineData(0, "1,5")]
    public void InvalidRadiusCommitsLeaveAuthoredDataAndDirtyFlagUntouched(int index, string value)
    {
        WorkspaceSession session = WorkspaceExamples.Furniture(); session.MarkSaved("test");
        string initial = session.ExportAuthoredJson("test");
        Assert.False(session.SetBlastRadius(index, value));
        Assert.Equal(initial, session.ExportAuthoredJson("test")); Assert.False(session.IsDirty);
        Assert.False(string.IsNullOrWhiteSpace(session.Message));
    }

    [Fact]
    public void RadiiEditThroughReadyAndRunningThenLockUntilReplayAndRetainOriginalStarts()
    {
        WorkspaceSession session = WorkspaceExamples.Furniture();
        Assert.True(session.SetBlastRadius(0, "1.25"));
        Assert.True(session.Finalise());
        var starts = Positions(session.Frame); var furniture = session.Frame.FurnitureBodies.Select(b => b.Position).ToArray();
        Assert.True(session.SetBlastRadius(1, "3.5"));
        session.ToggleRunning(); for (int i = 0; i < 20; i++) session.Advance(session.StepSeconds);
        Assert.True(session.SetBlastRadius(4, "22.5")); Assert.True(session.IsDirty);
        Assert.Equal(22.5f, (float)JObject.Parse(session.ExportAuthoredJson("test"))["blastZones"]!["panic"]!);
        Assert.True(session.TriggerExplosion(new Vector2(18, 12)));
        Assert.False(session.SetBlastRadius(4, "25")); Assert.False(session.ResetBlastRadii());
        Assert.False(session.AdjustBlastRadius(0, 1));
        for (int i = 0; i < 120; i++) session.Advance(session.StepSeconds);
        Assert.True(session.Replay()); Assert.True(session.CanEditBlastRadii);
        Assert.Equal(new[] { 1.25f, 3.5f, 6.31f, 9.47f, 22.5f }, session.BlastRadii);
        Assert.Equal(starts, Positions(session.Frame));
        Assert.Equal(furniture, session.Frame.FurnitureBodies.Select(b => b.Position));
        Assert.Equal(11, session.Frame.SeatedAgents); Assert.Empty(session.Blast.Zones);
        Assert.Equal(0, session.Blast.DebrisHits); Assert.False(session.ExplosionArmed);
    }

    [Fact]
    public void IncrementUsesCommittedValueClampsNeighborsAndResetRestoresDefaults()
    {
        WorkspaceSession session = new();
        Assert.True(session.SetBlastRadius(0, "3.06"));
        Assert.True(session.AdjustBlastRadius(0, 1));
        Assert.InRange(session.BlastRadii[0], 3.05999f, 3.06001f);
        Assert.True(session.SetBlastRadius(4, "200")); Assert.True(session.AdjustBlastRadius(4, 1));
        Assert.Equal(200, session.BlastRadii[4]);
        Assert.True(session.ResetBlastRadii());
        Assert.Equal(new[] { 1.58f, 3.16f, 6.31f, 9.47f, 18.21f }, session.BlastRadii);
    }

    [Fact]
    public void ArmCancelPauseBlockedClicksAndOneEventGuardRetainState()
    {
        WorkspaceSession session = WorkspaceExamples.Furniture();
        Assert.False(session.ArmExplosion());
        session.Finalise(); Assert.False(session.ArmExplosion());
        session.ToggleRunning(); Assert.True(session.ArmExplosion());
        Assert.False(session.TriggerExplosion(new Vector2(10, 28))); // Square block.
        Assert.False(session.TriggerExplosion(new Vector2(20, 28))); // Circle.
        Assert.False(session.TriggerExplosion(new Vector2(42, 22))); // Stage/furniture footprint.
        Assert.False(session.TriggerExplosion(new Vector2(0, 0))); // Physical perimeter.
        Assert.False(session.TriggerExplosion(new Vector2(float.NaN, 12)));
        Assert.False(session.TriggerExplosion(new Vector2(-1, 12)));
        Assert.True(session.ExplosionArmed); Assert.False(session.Blast.Detonated);
        session.CancelExplosion(); Assert.False(session.ExplosionArmed);
        session.ArmExplosion(); session.ToggleRunning(); Assert.False(session.ExplosionArmed);
        session.ToggleRunning(); session.ArmExplosion();
        Assert.True(session.TriggerExplosion(new Vector2(18, 12))); Assert.False(session.ExplosionArmed);
        Assert.False(session.ArmExplosion()); Assert.False(session.TriggerExplosion(new Vector2(19, 12)));
        Assert.Equal(new Vector2(18, 12), session.Blast.Origin);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DisplayFlagsAndRepeatedSnapshotsNeverChangePhysicalResultsOrSavedData(bool hybrid)
    {
        WorkspaceSession a = WorkspaceExamples.Furniture(), b = WorkspaceExamples.Furniture();
        a.SetHybrid(hybrid); b.SetHybrid(hybrid); a.Finalise(); b.Finalise(); a.MarkSaved("same"); b.MarkSaved("same");
        a.ToggleRunning(); b.ToggleRunning(); a.TriggerExplosion(new Vector2(18, 12)); b.TriggerExplosion(new Vector2(18, 12));
        var zones = a.Blast.Zones; int cacheBuilds = a.BlastGeometryBuildCount;
        for (int i = 0; i < 5; i++) a.SetZoneVisible(i, false);
        a.ShowBlastZones = a.ShowShockFront = a.ShowDust = false;
        Assert.Same(zones, a.Blast.Zones); Assert.Equal(cacheBuilds, a.BlastGeometryBuildCount); Assert.False(a.IsDirty);
        for (int i = 0; i < 120; i++)
        {
            a.ShowBlastZones = i % 2 == 0; a.SetZoneVisible(i % 5, i % 3 == 0);
            a.Advance(a.StepSeconds); b.Advance(b.StepSeconds);
            Assert.Equal(Positions(a.Frame), Positions(b.Frame));
            Assert.Equal(a.Frame.Bodies.Select(x => x.State), b.Frame.Bodies.Select(x => x.State));
            Assert.Equal(a.Frame.FurnitureBodies.Select(x => (x.Position, x.State)), b.Frame.FurnitureBodies.Select(x => (x.Position, x.State)));
            Assert.Equal(a.Blast.DebrisHits, b.Blast.DebrisHits); Assert.Equal(a.Blast.ShockHits, b.Blast.ShockHits);
        }
        Assert.Equal(a.ExportAuthoredJson("same"), b.ExportAuthoredJson("same"));
        Assert.False(a.IsDirty); Assert.NotEmpty(a.Blast.Zones); Assert.Null(a.Blast.Shock);
        bool[] flags = a.ZoneVisibility.ToArray();
        a.ToggleInspector(); a.SelectInspectorTab(1); a.ToggleInspector();
        Assert.Equal(flags, a.ZoneVisibility);
    }

    [Fact]
    public void AllGroundVerticesAndSegmentMidpointsUseTheSameRoomAndStaticOcclusionRules()
    {
        Simulation simulation = new(randomSeed: 42);
        Assert.True(simulation.Environment.AddBlockageFromClick(30, 17, 2));
        Assert.True(simulation.Environment.AddCircularBlockageFromClick(26, 22)); simulation.BuildWallGrid();
        Vector2 origin = new(25, 17);
        BlastMeshData band = BlastGeometry.Band(simulation, origin, 0, 12, BlastGeometry.ZoneColor(4));
        Assert.NotEmpty(band.Quads); Assert.NotEmpty(band.Lines);
        foreach (var vertex in band.Quads.Concat(band.Lines))
            Assert.True(BlastGeometry.Visible(simulation, origin, new Vector2(vertex.X, vertex.Z)));
        for (int i = 0; i < band.Lines.Count; i += 2)
        {
            var midpoint = (band.Lines[i] + band.Lines[i + 1]) * .5f;
            Assert.True(BlastGeometry.Visible(simulation, origin, new Vector2(midpoint.X, midpoint.Z)));
        }
        Assert.False(BlastGeometry.Visible(simulation, origin, new Vector2(35, 17)));
        BlastMeshData nearBoundary = BlastGeometry.Band(simulation, new Vector2(1, 1), 0, 5, BlastGeometry.ZoneColor(0));
        Assert.NotEmpty(nearBoundary.Quads); Assert.True(nearBoundary.Quads.Count < 128 * 4);
    }

    [Fact]
    public void GeometryCacheInvalidatesForSourceRadiiLayoutAndNewModelButNotTicksOrVisibility()
    {
        Simulation simulation = new(randomSeed: 42); simulation.Environment.SetExplosion(25, 17, 0);
        BlastGeometryCache cache = new(); var first = cache.Get(simulation);
        Assert.Same(first, cache.Get(simulation)); Assert.Equal(1, cache.BuildCount);
        simulation.Config.Agent.SetBlastZoneRadii(1, 3, 6, 9, 20);
        Assert.NotSame(first, cache.Get(simulation)); Assert.Equal(2, cache.BuildCount);
        simulation.Environment.AddBlockageFromClick(35, 17, 1); simulation.BuildWallGrid(); cache.Get(simulation);
        Assert.Equal(3, cache.BuildCount);
        Simulation other = new(randomSeed: 42); other.Environment.SetExplosion(10, 10, 0); cache.Get(other);
        Assert.Equal(4, cache.BuildCount);
        Simulation fresh = new(randomSeed: 42); Assert.Empty(cache.Get(fresh));
    }

    [Theory]
    [InlineData(.15f)] [InlineData(.075f)]
    public void FrontAndDustUseConfiguredStepAndActualVisibilityField(float dt)
    {
        Simulation simulation = new(config: new SimulationConfig { Agent = new AgentConfig { Dt = dt, DebrisCount = 0 } }, randomSeed: 4);
        simulation.Environment.AddExitFromClick(0, 17, 3); simulation.SpawnAgents(1); simulation.SetRunning(true);
        simulation.TriggerExplosion(25, 17); BlastGeometryCache cache = new();
        Assert.Equal(0, ExplosionFrame.Capture(simulation, cache).ShockRadius);
        int ticks = (int)MathF.Round(.6f / dt);
        for (int i = 0; i < ticks; i++) simulation.Step();
        ExplosionFrame frame = ExplosionFrame.Capture(simulation, cache);
        Assert.Equal(.6f, frame.EventSeconds, 4); Assert.Equal(4.84f, frame.ShockRadius, 4); Assert.NotNull(frame.Shock);
        Assert.Contains(frame.Dust, layer => layer.Quads.Count > 0);
        Assert.InRange(simulation.VisibilitySystem.GetVisibilityFactor(simulation, new Vector2(25, 17)), .35f, .36f);
        Assert.Equal(1, simulation.VisibilitySystem.GetVisibilityFactor(simulation, new Vector2(40, 17)));
        for (int i = 0; i < (int)(15 / dt); i++) simulation.Step();
        frame = ExplosionFrame.Capture(simulation, cache); Assert.Null(frame.Shock); Assert.Empty(frame.Dust);
        Assert.NotEmpty(frame.Zones);
    }

    [Fact]
    public void UnrepresentableExtremeRadiiAreRejectedBeforeChangingConfigCheckpointOrDirtyState()
    {
        WorkspaceSession session = WorkspaceExamples.Furniture(); session.Finalise();
        Assert.True(session.SetBlastRadius(4, "200")); Assert.True(session.SetBlastRadius(3, "199.9"));
        session.MarkSaved("extreme"); string saved = session.ExportAuthoredJson("extreme");
        Assert.False(session.SetBlastRadius(2, "199.8"));
        Assert.Contains("between", session.Message);
        Assert.False(session.IsDirty); Assert.Equal(saved, session.ExportAuthoredJson("extreme"));
        for (int i = 0; i < 5; i++)
        {
            Assert.True(session.BlastMinimum(i) <= session.BlastMaximum(i));
            Assert.True(session.SetBlastRadius(i, session.BlastRadii[i].ToString("R", CultureInfo.InvariantCulture)));
            Assert.False(session.IsDirty);
        }
        Assert.True(session.Replay()); Assert.Equal(saved, session.ExportAuthoredJson("extreme"));
    }

    [Fact]
    public void FumeSessionCannotEditArmOrShowExplosionGeometry()
    {
        WorkspaceSession session = new(ScenarioType.ToxicFume);
        Assert.False(session.CanEditBlastRadii); Assert.False(session.SetBlastRadius(0, "1"));
        Assert.False(session.ResetBlastRadii()); Assert.False(session.ArmExplosion());
        Assert.Empty(session.Blast.Zones); Assert.Empty(session.Blast.Dust); Assert.Null(session.Blast.Shock);
    }
    [Theory]
    [InlineData(0f)] [InlineData(.5f)] [InlineData(1f)]
    public void DebrisViewsUseDetachedReferenceShapesHeadingAndEnergy(float energy)
    {
        Simulation simulation = new(randomSeed: 42);
        Debris debris = new(new Vector2(12, 10), new Vector2(0, 1), 50, .24f, energy);
        simulation.Debris.Add(debris);
        ViewBody body = Assert.Single(PresentationFrame.Capture(simulation).Bodies, b => b.Kind == ViewKind.Debris);
        Assert.Equal(ViewShape.Box, body.Shape); Assert.Equal(MathF.PI / 2, body.GroundAngle, 5);
        Assert.Equal(energy, body.Energy); Assert.True(body.Size.X > body.Size.Z);
        Assert.Equal(.48f * (.50f + .35f * energy), body.Size.X, 5);
        debris.Position = new Vector2(40, 30); debris.Energy = 0;
        Assert.Equal(12, body.Position.X); Assert.Equal(10, body.Position.Z); Assert.Equal(energy, body.Energy);
    }

    private static Vector3[] Positions(PresentationFrame frame) => frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
}
