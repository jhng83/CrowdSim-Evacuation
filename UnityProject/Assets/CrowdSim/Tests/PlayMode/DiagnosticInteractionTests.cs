using System.Collections;
using System.Linq;
using CrowdSim.Diagnostics;
using CrowdSim.Core.Model;
using CrowdSim.Unity.Foundation;
using CrowdSim.Unity.Workspace;
using CrowdSim.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Ground = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class DiagnosticInteractionTests
    {
        private static IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-explosion-workspace"));
            yield return null;
        }
        [UnityTest]
        public IEnumerator LightWorkspaceUsesBlackTextOffWhiteFloorAndOutlinedPillarsWithoutCoveringTheViewport()
        {
            yield return Open();
            WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>();
            c.Session.Place(PlacementTool.Square, new Ground(25, 20), 1); c.Session.Place(PlacementTool.Circle, new Ground(35, 20), 1);
            yield return null;
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Label>().ToList().First(l => l.ClassListContains("brand")).resolvedStyle.color, Is.EqualTo(Color.black));
            Assert.That(root.resolvedStyle.backgroundColor.a, Is.Zero, "An opaque UI root would cover the simulation camera.");
            WorkspaceWorldView world = c.GetComponentInChildren<WorkspaceWorldView>();
            WorkspacePillarOutline[] pillars = world.GetComponentsInChildren<WorkspacePillarOutline>(); Assert.That(pillars.Length, Is.EqualTo(2));
            foreach (WorkspacePillarOutline pillar in pillars)
            {
                Color color = pillar.GetComponent<MeshRenderer>().sharedMaterial.color;
                Assert.That(color.r, Is.GreaterThan(.93f)); Assert.That(color.b, Is.LessThan(color.r));
                Assert.That(pillar.transform.Find("Pillar black outline").GetComponent<MeshRenderer>().sharedMaterial.color, Is.EqualTo(Color.black));
            }
            Color floor = world.transform.Find("floor").GetComponent<MeshRenderer>().sharedMaterial.color;
            Assert.That(floor.r, Is.GreaterThan(.93f)); Assert.That(c.Session.ShowGrid, Is.True);
            Assert.That(world.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("grid-")), Is.True);
            Assert.That(c.Session.Frame.Bodies.Count(b => b.IsPillar), Is.EqualTo(2)); LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator AllOverlayControlsAndMetricsSurviveHidingAndReplayWithoutMovingThePausedCrowd()
        {
            yield return Open(); WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>();
            c.Session.Place(PlacementTool.Exit, new Ground(0, 17), 5); c.Session.Place(PlacementTool.Agents, new Ground(24, 17), 15); c.Session.Finalise();
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement; Click(root, "Debug");
            foreach (Toggle toggle in root.Query<Toggle>().ToList().Where(t => t.name != null && t.name.StartsWith("overlay-"))) toggle.value = true;
            var positions = c.Session.Frame.Bodies.Select(b => b.Position).ToArray();
            Click(root, "Hide inspector (F4)"); yield return null;
            Assert.That(c.Session.Overlays, Is.EqualTo((DiagnosticOverlay)255));
            WorkspaceDiagnosticView view = c.GetComponentInChildren<WorkspaceDiagnosticView>();
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Any(m => m.sharedMesh.vertexCount > 0), Is.True);
            Assert.That(c.Session.Frame.Bodies.Select(b => b.Position), Is.EqualTo(positions));
            Click(root, "Show inspector (F4)"); yield return new WaitForSecondsRealtime(.35f);
            Assert.That(root.Q<Label>("performance-metrics").text, Does.Contain("GPU time: not measured"));
            Click(root, "Replay from start"); yield return null;
            Assert.That(c.Session.Tick, Is.Zero); Assert.That(c.Session.Overlays, Is.EqualTo((DiagnosticOverlay)255));
            Assert.That(c.Session.Frame.Bodies.Select(b => b.Position), Is.EqualTo(positions)); LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator PausedHybridControlLocksDuringRunAndReplayPreservesTheChosenMode()
        {
            yield return Open(); WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>();
            c.Session.Place(PlacementTool.Exit, new Ground(0, 17), 5); c.Session.Place(PlacementTool.Agents, new Ground(24, 17), 15); c.Session.Finalise();
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement; Click(root, "Debug");
            Toggle hybrid = root.Q<Toggle>("runtime-hybrid"); hybrid.value = true; Assert.That(c.Session.Hybrid, Is.True);
            Click(root, "Start"); yield return null; Assert.That(hybrid.enabledInHierarchy, Is.False);
            Click(root, "Pause"); Assert.That(hybrid.enabledInHierarchy, Is.True); hybrid.value = false;
            Click(root, "Replay from start"); Assert.That(c.Session.Hybrid, Is.False); LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ComparisonCreationUsesTheExistingDirtyPromptAndExactPopulationIncludingSeats()
        {
            yield return Open(); WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>();
            c.Session.Place(PlacementTool.Exit, new Ground(0, 17), 5);
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement; Click(root, "Debug");
            root.Query<DropdownField>().ToList().Single(d => d.label == "Population").value = "500";
            root.Query<Toggle>().ToList().Single(t => t.label == "Furnished comparison (80 seated)").value = true;
            Click(root, "New comparison layout"); Assert.That(root.Q<VisualElement>("file-decision"), Is.Not.Null);
            Click(root, "Discard session and continue");
            Assert.That(c.Session.PlannedCount, Is.EqualTo(500)); Assert.That(c.Session.Frame.SeatedAgents, Is.EqualTo(80));
            Click(root, "Finalise scene"); yield return null;
            Assert.That(c.Session.Frame.Population, Is.EqualTo(500)); Assert.That(c.Session.Frame.SeatedAgents, Is.EqualTo(80));
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Assert.That(Object.FindFirstObjectByType<WorkspaceDiagnosticView>(), Is.Null); LogAssert.NoUnexpectedReceived();
        }
        private static void Click(VisualElement root, string text) => Click(root.Query<Button>().ToList().First(b => b.text == text));
        private static void Click(Button button)
        { using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
