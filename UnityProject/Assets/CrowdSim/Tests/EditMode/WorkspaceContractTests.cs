using System.Linq;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using NUnit.Framework;
using GroundPoint = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class WorkspaceContractTests
    {
        [Test]
        public void AuthorPreviewUndoFinaliseAndRunPreserveTheExactStarts()
        {
            WorkspaceSession session = new WorkspaceSession();
            Assert.That(session.Finalise(), Is.False);
            Assert.That(session.Resize(60, 40), Is.True);
            Assert.That(session.Preview(PlacementTool.Agents, new GroundPoint(25, 20), 15).Valid, Is.True);
            Assert.That(session.PlannedCount, Is.Zero);
            Assert.That(session.Place(PlacementTool.Exit, new GroundPoint(0, 20), 3), Is.True);
            Assert.That(session.Place(PlacementTool.Agents, new GroundPoint(25, 20), 15), Is.True);
            var starts = session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
            Assert.That(session.CanResize, Is.False);
            Assert.That(session.Finalise(), Is.True);
            Assert.That(session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position), Is.EqualTo(starts));
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.ToggleRunning(), Is.True);
            session.Advance(session.StepSeconds);
            Assert.That(session.Tick, Is.EqualTo(1));
            session.ToggleRunning();
            Assert.That(session.Advance(10), Is.Zero);
            Assert.That(session.Tick, Is.EqualTo(1));
        }

        [Test]
        public void AllFourteenTuningControlsHaveBoundsAndRestoreDefaults()
        {
            WorkspaceSession session = new WorkspaceSession();
            float[] defaults = session.BehaviourValues.ToArray();
            Assert.That(BehaviourControls.All.Count, Is.EqualTo(14));
            for (int i = 0; i < 14; i++)
            {
                session.SetBehaviour(i, 100);
                Assert.That(session.BehaviourValues[i], Is.EqualTo(BehaviourControls.All[i].Maximum));
            }
            session.ResetBehaviour();
            Assert.That(session.BehaviourValues, Is.EqualTo(defaults));
        }

        [Test]
        public void InspectorHiddenStateRetainsTabAndGridAndGivesSpaceToTheViewport()
        {
            WorkspaceSession session = new WorkspaceSession();
            session.SelectInspectorTab(3); session.ShowGrid = true;
            session.ToggleInspector();
            Assert.That(session.InspectorTab, Is.EqualTo(3));
            Assert.That(session.ShowGrid, Is.True);
            DashboardLayout shown = DashboardLayout.Calculate(1280, true, 8);
            DashboardLayout hidden = DashboardLayout.Calculate(1280, false, 8);
            Assert.That(hidden.ViewportWidth - shown.ViewportWidth, Is.EqualTo(shown.InspectorWidth + shown.Gap).Within(.001));
        }

        [Test]
        public void ShortcutPolicyRetainsTextEditingAndAltF4()
        {
            Assert.That(WorkspaceShortcuts.Resolve("F4", false, true, false), Is.EqualTo(WorkspaceShortcut.None));
            Assert.That(WorkspaceShortcuts.Resolve("Z", true, false, true), Is.EqualTo(WorkspaceShortcut.None));
            Assert.That(WorkspaceShortcuts.Resolve("Space", false, false, true), Is.EqualTo(WorkspaceShortcut.None));
            Assert.That(WorkspaceShortcuts.Resolve("F4", false, false, false), Is.EqualTo(WorkspaceShortcut.ToggleInspector));
        }
    }
}
