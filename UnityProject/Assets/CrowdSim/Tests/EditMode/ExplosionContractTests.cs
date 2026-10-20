using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using NUnit.Framework;

namespace CrowdSim.Unity.Tests
{
    public sealed class ExplosionContractTests
    {
        [Test]
        public void InvariantTextEditsBeforeDetonationSurviveSaveAndReplayWithoutCapturingMotion()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                WorkspaceSession session = WorkspaceExamples.Furniture(); session.Finalise();
                var starts = session.Frame.Bodies.Select(b => b.Position).ToArray();
                session.ToggleRunning(); for (int i = 0; i < 20; i++) session.Advance(session.StepSeconds);
                Assert.That(session.SetBlastRadius(0, "1.25"), Is.True);
                Assert.That(session.SetBlastRadius(0, "1,5"), Is.False);
                Assert.That(session.ExportAuthoredJson("test"), Does.Contain("1.25"));
                session.TriggerExplosion(new Vector2(18, 12));
                Assert.That(session.SetBlastRadius(0, "1.5"), Is.False);
                session.Replay(); Assert.That(session.BlastRadii[0], Is.EqualTo(1.25f));
                Assert.That(session.Frame.Bodies.Select(b => b.Position), Is.EqualTo(starts));
                Assert.That(session.CanEditBlastRadii, Is.True);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void StaticOcclusionClipsDetachedBandsAndCachedVisibilityNeverMutatesThem()
        {
            Simulation simulation = new Simulation(randomSeed: 42);
            simulation.Environment.AddBlockageFromClick(30, 17, 2); simulation.BuildWallGrid();
            simulation.Environment.SetExplosion(25, 17, 0);
            BlastGeometryCache cache = new BlastGeometryCache();
            var bands = cache.Get(simulation); Assert.That(bands.Count, Is.EqualTo(5));
            Assert.That(cache.Get(simulation), Is.SameAs(bands));
            Assert.That(bands.SelectMany(b => b.Quads), Is.Not.Empty);
            foreach (var point in bands.SelectMany(b => b.Quads.Concat(b.Lines)))
                Assert.That(BlastGeometry.Visible(simulation, new Vector2(25, 17), new Vector2(point.X, point.Z)), Is.True);
            Assert.That(BlastGeometry.Visible(simulation, new Vector2(25, 17), new Vector2(35, 17)), Is.False);
        }

        [Test]
        public void ArmRejectsBlockedFloorThenLocksAfterOneValidEventAndClearsOnReplay()
        {
            WorkspaceSession session = WorkspaceExamples.Furniture();
            Assert.That(session.ArmExplosion(), Is.False); session.Finalise(); session.ToggleRunning();
            Assert.That(session.ArmExplosion(), Is.True);
            Assert.That(session.TriggerExplosion(new Vector2(10, 28)), Is.False);
            Assert.That(session.ExplosionArmed, Is.True);
            Assert.That(session.TriggerExplosion(new Vector2(18, 12)), Is.True);
            Assert.That(session.TriggerExplosion(new Vector2(19, 12)), Is.False);
            Assert.That(session.ResetBlastRadii(), Is.False);
            session.Replay(); Assert.That(session.ExplosionArmed, Is.False);
            Assert.That(session.Blast.Detonated, Is.False); Assert.That(session.Blast.Zones, Is.Empty);
        }

        [Test]
        public void HostRuntimeFrontAndDustUseModelTimeAndTheActualVisibilityField()
        {
            Simulation simulation = new Simulation(config: new SimulationConfig { Agent = new AgentConfig { Dt = .075f, DebrisCount = 0 } }, randomSeed: 4);
            simulation.Environment.AddExitFromClick(0, 17, 3); simulation.SpawnAgents(1); simulation.SetRunning(true);
            simulation.TriggerExplosion(25, 17);
            for (int i = 0; i < 8; i++) simulation.Step();
            ExplosionFrame frame = ExplosionFrame.Capture(simulation, new BlastGeometryCache());
            Assert.That(frame.EventSeconds, Is.EqualTo(.6f).Within(1e-4));
            Assert.That(frame.ShockRadius, Is.EqualTo(4.84f).Within(1e-4));
            Assert.That(frame.Shock, Is.Not.Null); Assert.That(frame.Dust.Any(b => b.Quads.Count > 0), Is.True);
            Assert.That(simulation.VisibilitySystem.GetVisibilityFactor(simulation, new Vector2(25, 17)), Is.LessThan(.36f));
        }
    }
}
