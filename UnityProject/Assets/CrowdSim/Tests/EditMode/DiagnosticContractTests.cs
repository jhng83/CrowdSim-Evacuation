using System;
using System.Linq;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using NUnit.Framework;
using Ground = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class DiagnosticContractTests
    {
        private static Simulation Model()
        {
            Simulation model = new Simulation(randomSeed: 731); model.Environment.AddExitFromClick(.3f, 17, 5);
            model.SpawnAgentsAtPositions(new[] { new Ground(15, 17), new Ground(16, 17) }); return model;
        }
        [Test]
        public void MetricsRespectMobileAndExitedDenominators()
        {
            Simulation model = Model(); model.Agents[0].State = AgentState.Dead; model.Agents[0].PanicLevel = 1;
            model.Agents[1].PanicLevel = .2f;
            PopulationMetrics metrics = PopulationMetrics.Capture(model);
            Assert.That(metrics.Mobile, Is.EqualTo(1)); Assert.That(metrics.AveragePanic, Is.EqualTo(.2f));
        }
        [Test]
        public void SelectedInspectorExposesNavigationAssistanceAndSpeedUnits()
        {
            Simulation model = Model(); Agent a = model.Agents[0]; a.Velocity = new Ground(.15f, 0);
            a.Intent = BehaviorIntent.AssistCasualty; a.AssistTargetAgentId = 1; a.AssistPauseUntilFrame = 10;
            var rows = AgentInspection.Capture(model, 0);
            Assert.That(rows.Any(r => r.Label == "Actual speed" && r.Value == 1f.ToString("F2") + " m/s"), Is.True);
            Assert.That(rows.Any(r => r.Label == "Assist phase"), Is.True);
            Assert.That(rows.Any(r => r.Label == "Adaptive navigation"), Is.True);
        }
        [Test]
        public void BatchedArrowsRetainMetricScaleAndPovExcludesItsOwnAgent()
        {
            Simulation model = Model(); model.Agents[0].LastDesiredDirection = Ground.UnitX;
            DiagnosticFrame frame = new DiagnosticFrame(model);
            var mesh = DiagnosticOverlays.Build(frame, DiagnosticOverlay.Desired, new DiagnosticTrails());
            Assert.That(mesh.Lines.Count, Is.EqualTo(6));
            Assert.That(System.Numerics.Vector3.Distance(mesh.Lines[0], mesh.Lines[1]), Is.EqualTo(32 * 1.7f / 42).Within(.000002));
            Assert.That(DiagnosticOverlays.Build(frame, DiagnosticOverlay.Desired, new DiagnosticTrails(), 0).Lines.Count, Is.Zero);
        }
        [Test]
        public void TrailsDoNotDuplicateOnRedrawAndRetireExitedPeople()
        {
            Simulation model = Model(); DiagnosticTrails trails = new DiagnosticTrails();
            trails.Observe(new DiagnosticFrame(model), true); trails.Observe(new DiagnosticFrame(model), true);
            Assert.That(trails.Points(0).Count, Is.EqualTo(1));
            model.Agents[0].State = AgentState.Exited; trails.Observe(new DiagnosticFrame(model), true);
            Assert.That(trails.Points(0).Count, Is.Zero);
        }
        [Test]
        public void PausedHybridRebasesReplayAndRunningRejectsChanges()
        {
            WorkspaceSession session = WorkspaceSession.ScaleExample(220, false, false, ScenarioType.Explosion);
            session.Finalise(); Assert.That(session.SetPausedHybrid(true), Is.True);
            session.ToggleRunning(); Assert.That(session.SetPausedHybrid(false), Is.False);
            session.ToggleRunning(); session.Replay(); Assert.That(session.Hybrid, Is.True);
        }
        [Test]
        public void AllComparisonSizesHaveExactDeterministicStartsAndSeats()
        {
            foreach (int count in new[] { 220, 500, 1000 })
            foreach (bool furnished in new[] { false, true })
            {
                Simulation a = CrowdScaleLayout.Create(count, furnished, false), b = CrowdScaleLayout.Create(count, furnished, true);
                Assert.That(a.Environment.PlannedAgentCount, Is.EqualTo(count));
                Assert.That(a.Environment.GetPlannedAgentPositions(), Is.EqualTo(b.Environment.GetPlannedAgentPositions()));
                Assert.That(a.Environment.Furniture.Sum(f => f.Chairs.Count), Is.EqualTo(furnished ? 80 : 0));
            }
        }
        [Test]
        public void FrameRateIsWallTimeAndSamplesAreBounded()
        {
            FrameStatistics stats = new FrameStatistics();
            for (int i = 0; i < 150; i++) stats.Add(20, 2, 3, 1, 100);
            Assert.That(stats.Samples, Is.EqualTo(120)); Assert.That(stats.FramesPerSecond, Is.EqualTo(50).Within(.001));
            Assert.That(stats.PhysicsCommandsMilliseconds, Is.EqualTo(2));
        }
        [Test]
        public void PillarPresentationTagsLeaveThePhysicalBlockageIntactAndGridDefaultsOn()
        {
            Simulation model = Model(); model.Environment.AddBlockageFromClick(25, 20, 1);
            Assert.That(PresentationFrame.Capture(model).Bodies.Any(b => b.IsPillar), Is.True);
            Assert.That(model.Environment.IsPointBlocked(new Ground(25, 20), .2f), Is.True);
            Assert.That(new WorkspaceSession().ShowGrid, Is.True);
        }
    }
}
