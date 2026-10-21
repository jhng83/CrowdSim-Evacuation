using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using NUnit.Framework;

namespace CrowdSim.Unity.Tests
{
    public sealed class FumeContractTests
    {
        private static WorkspaceSession Builder()
        {
            WorkspaceSession s = new WorkspaceSession(ScenarioType.ToxicFume);
            s.Place(PlacementTool.Exit, new Vector2(0, 17), 3); s.Place(PlacementTool.Agents, new Vector2(24, 17), 15);
            return s;
        }
        [Test]
        public void PreviewDoesNotMovePeopleChangeAuthoredDataOrExposeThem()
        {
            WorkspaceSession s = Builder(); s.PlaceFumeSource(new Vector2(26, 17)); Assert.That(s.Finalise(), Is.False);
            Assert.That(s.ComputeFume(), Is.True); s.MarkSaved("preview");
            string json = s.ExportAuthoredJson("preview"); var bodies = s.Frame.Bodies.Select(b => b.Position).ToArray();
            Assert.That(s.SetFumePreview(30), Is.True); Assert.That(s.Fume.Cells, Is.Not.Empty);
            Assert.That(s.Tick, Is.Zero); Assert.That(s.Fume.AverageExposure, Is.Zero); Assert.That(s.IsDirty, Is.False);
            Assert.That(s.ExportAuthoredJson("preview"), Is.EqualTo(json)); Assert.That(s.Frame.Bodies.Select(b => b.Position), Is.EqualTo(bodies));
            s.Finalise(); s.ToggleRunning(); s.ReleaseFume(); for (int i = 0; i < 30; i++) s.Advance(s.StepSeconds);
            Assert.That(s.Fume.ElapsedSeconds, Is.GreaterThan(0)); s.Replay();
            Assert.That(s.FumeReleased, Is.False); Assert.That(s.HasFumeCache, Is.True); Assert.That(s.Fume.AverageExposure, Is.Zero);
        }
        [Test]
        public void SourceAndLayoutEditsInvalidateTheActualCacheAndRuntimeSetupLocks()
        {
            WorkspaceSession s = Builder(); s.PlaceFumeSource(new Vector2(26, 17)); s.ComputeFume();
            s.Place(PlacementTool.Circle, new Vector2(35, 20), 1);
            Assert.That(s.HasFumeCache, Is.False); Assert.That(s.CanFinalise, Is.False); s.ComputeFume();
            s.PlaceFumeSource(new Vector2(28, 17)); Assert.That(s.HasFumeCache, Is.False); s.ComputeFume(); s.Finalise();
            Assert.That(s.SetFumeValue(0, 2), Is.False); Assert.That(s.ResetFumeDefaults(), Is.False); Assert.That(s.ComputeFume(), Is.False);
        }
        [Test]
        public void ParameterDependenciesAndFiniteChecksUseTheReferenceConfiguration()
        {
            WorkspaceSession s = Builder();
            s.SetFumeValue(0, 5); s.SetFumeValue(2, 2); Assert.That(s.FumeValues[2], Is.EqualTo(5));
            s.SetFumeValue(6, .8f); s.SetFumeValue(7, .1f); Assert.That(s.FumeValues[7], Is.EqualTo(.85f).Within(1e-5));
            s.SetFumeValue(8, 100); s.SetFumeValue(9, 2); Assert.That(s.FumeValues[9], Is.EqualTo(101));
            Assert.That(s.SetFumeValue(1, float.NaN), Is.False); Assert.That(s.PlaceFumeSource(new Vector2(float.NaN, 17)), Is.False);
        }
        [Test]
        public void DetachedFieldSamplesMatchTheHostRuntimeCacheAndObstacles()
        {
            Simulation model = new Simulation(randomSeed: 42); model.SetScenarioType(ScenarioType.ToxicFume);
            model.Environment.AddBlockageFromClick(28, 17, 2); model.BuildWallGrid();
            model.ToxicFumeSystem.TrySetSource(model, new Vector2(24, 17)); model.PrecomputeToxicFume(); model.ToxicFumeSystem.PreviewTimeSeconds = 30;
            FumeFrame frame = FumeFrame.Capture(model, true);
            Assert.That(frame.Cells.Count, Is.EqualTo(model.ToxicFumeSystem.EnumerateCells(30, .025f, 1).Count()));
            Assert.That(frame.Cells.All(c => !model.Environment.IsFumeBlocked(c.Center, .02f)), Is.True);
            Assert.That(frame.Layers.Sum(l => l.Quads.Count), Is.EqualTo(frame.Cells.Count * 4));
        }
        [Test]
        public void RefinedFurnitureDisplayMeshKeepsExactUnitBoundsAndFiniteNormals()
        {
            PresentationMesh mesh = PresentationAssets.BevelledBox();
            Assert.That(mesh.Vertices.Aggregate(Vector3.Min), Is.EqualTo(new Vector3(-.5f)));
            Assert.That(mesh.Vertices.Aggregate(Vector3.Max), Is.EqualTo(new Vector3(.5f)));
            Assert.That(mesh.Normals.All(n => float.IsFinite(n.X) && System.Math.Abs(n.Length() - 1) < 1e-4), Is.True);
        }
    }
}
