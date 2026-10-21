using System.Collections;
using System.Linq;
using CrowdSim.Unity.Foundation;
using CrowdSim.Unity.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GroundPoint = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class FumeInteractionTests
    {
        [UnityTest]
        public IEnumerator ActualSourceSettingsComputePreviewAndReleaseControlsCompleteAnAuthoredRun()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-fume-workspace")); yield return null;
            WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>(); VisualElement root = c.GetComponent<UIDocument>().rootVisualElement;
            c.HandleWorldClick(new GroundPoint(0, 17)); root.Query<DropdownField>().ToList().Single(d => d.label == "People per click").value = "15";
            c.HandleWorldClick(new GroundPoint(24, 17)); Click(root, "Hazard"); Click(root.Q<Button>("place-fume-source")); c.HandleWorldClick(new GroundPoint(26, 17));
            Assert.That(c.Session.CanFinalise, Is.False); root.Q<Slider>("fume-setting-0").value = 1;
            Click(root.Q<Button>("compute-fume")); Assert.That(c.Session.HasFumeCache, Is.True);
            c.Session.MarkSaved("test"); string json = c.Session.ExportAuthoredJson("test");
            root.Q<Slider>("fume-preview").value = 30;
            Assert.That(c.Session.Fume.DisplaySeconds, Is.EqualTo(30)); Assert.That(c.Session.IsDirty, Is.False);
            Assert.That(c.Session.Fume.AverageExposure, Is.Zero); Assert.That(c.Session.ExportAuthoredJson("test"), Is.EqualTo(json));
            root.Q<Slider>("fume-setting-1").value = .5f; Assert.That(c.Session.HasFumeCache, Is.False);
            Click(root.Q<Button>("compute-fume")); Click(root, "Finalise scene"); Click(root, "Start"); Click(root, "Release Toxic Fume");
            for (int i = 0; i < 30; i++) c.Session.Advance(c.Session.StepSeconds);
            Click(root, "Pause"); yield return null;
            Assert.That(c.Session.FumeReleased, Is.True); Assert.That(c.Session.Fume.ElapsedSeconds, Is.GreaterThan(0));
            Assert.That(root.Q<Slider>("fume-setting-0").enabledInHierarchy, Is.False);
            Click(root, "Replay from start"); Assert.That(c.Session.Tick, Is.Zero); Assert.That(c.Session.FumeReleased, Is.False);
            Assert.That(c.Session.Fume.AverageExposure, Is.Zero); LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator Example205PreviewMeshesGridHiddenInspectorAndReplayRemainIndependent()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-fume-example")); yield return null;
            WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>(); VisualElement root = c.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(c.Session.PlannedCount, Is.EqualTo(205)); Click(root, "Hazard"); Click(root.Q<Button>("compute-fume"));
            root.Q<Slider>("fume-preview").value = 30; root.Q<Toggle>("show-fume-grid").value = true;
            WorkspaceFumeView view = c.GetComponentInChildren<WorkspaceFumeView>();
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Any(m => m.sharedMesh.vertexCount > 0), Is.True);
            Assert.That(view.GetComponentsInChildren<Collider>(), Is.Empty);
            root.Q<Toggle>("show-fume").value = false;
            Assert.That(view.transform.Cast<Transform>().Where(t => t.name.StartsWith("Fume concentration")).All(t => !t.gameObject.activeSelf), Is.True);
            root.Q<Toggle>("show-fume").value = true; Click(root, "Hide inspector (F4)"); yield return null;
            Assert.That(c.Session.ShowFumeGrid, Is.True); Assert.That(c.Session.Tick, Is.Zero);
            Click(root, "Finalise scene"); Click(root, "Start"); Click(root, "Release Toxic Fume");
            for (int i = 0; i < 30; i++) c.Session.Advance(c.Session.StepSeconds);
            Click(root, "Replay from start"); yield return null;
            Assert.That(c.Session.Frame.Population, Is.EqualTo(205)); Assert.That(c.Session.Frame.SeatedAgents, Is.EqualTo(80));
            Assert.That(c.Session.Fume.AverageExposure, Is.Zero); Assert.That(c.Session.HasFumeCache && !c.Session.FumeReleased, Is.True);
            Assert.That(c.Session.ShowFumeGrid, Is.True); LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator FurnishedAssetsUseOwnedLitSurfacesSoftEdgesAndPreserveDimensions()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-furnished-workspace")); yield return null;
            WorkspaceController c = Object.FindFirstObjectByType<WorkspaceController>();
            WorkspaceWorldView world = c.GetComponentInChildren<WorkspaceWorldView>();
            MeshFilter bevel = world.GetComponentsInChildren<MeshFilter>().First(m => m.sharedMesh.name == "CrowdSim / chamfered box");
            Assert.That(bevel.sharedMesh.bounds.size, Is.EqualTo(Vector3.one));
            Assert.That(bevel.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(world.GetComponentsInChildren<Light>().Length, Is.EqualTo(2));
            Assert.That(c.Session.PlannedCount, Is.EqualTo(26)); Assert.That(c.Session.Frame.SeatedAgents, Is.EqualTo(11));
            var positions = c.Session.Frame.Bodies.Select(b => b.Position).ToArray();
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement; Click(root, "Hide inspector (F4)"); yield return null;
            Assert.That(c.Session.Frame.Bodies.Select(b => b.Position), Is.EqualTo(positions));
            Material owned = bevel.GetComponent<Renderer>().sharedMaterial;
            Click(root, "Return to startup"); Click(root, "Discard session and continue"); yield return null;
            Assert.That(owned == null, Is.True); LogAssert.NoUnexpectedReceived();
        }
        private static void Click(VisualElement root, string text) => Click(root.Query<Button>().ToList().Single(b => b.text == text));
        private static void Click(Button button)
        { using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
