using System.Collections;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
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
    public sealed class FurnitureInteractionTests
    {
        [UnityTest]
        public IEnumerator ActualPickerAuthorsAllKindsAndFinalisesSeatsWithoutAddingASecondStage()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-explosion-workspace"));
            yield return null;
            WorkspaceController controller = Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
            controller.HandleWorldClick(new GroundPoint(0, 17));
            DropdownField picker = root.Q<DropdownField>("asset-picker");
            picker.value = BuilderAssetSelection.Label(BuilderAsset.RoundTableSet, 1.2f);
            controller.HandleWorldClick(new GroundPoint(12, 12));
            picker.value = BuilderAssetSelection.Label(BuilderAsset.SingleChair, 1.2f);
            root.Q<FloatField>("asset-angle-exact").value = 37.5f;
            controller.HandleWorldClick(new GroundPoint(20, 12));
            picker.value = BuilderAssetSelection.Label(BuilderAsset.RectangularTable, 1.2f);
            Click(root, "Rotate 90 degrees (R)");
            controller.HandleWorldClick(new GroundPoint(28, 12));
            picker.value = BuilderAssetSelection.Label(BuilderAsset.Barricade, 1.2f);
            root.Q<FloatField>("asset-angle-exact").value = 37.5f;
            Click(root, "+15 degrees"); Click(root, "-15 degrees");
            Assert.That(controller.Session.AssetRotationDegrees, Is.EqualTo(37.5f));
            controller.HandleWorldClick(new GroundPoint(30, 22));
            picker.value = BuilderAssetSelection.Label(BuilderAsset.Stage, 1.2f);
            controller.HandleWorldClick(new GroundPoint(42, 22));
            Assert.That(root.Q<Button>("place-asset").enabledInHierarchy, Is.False);
            controller.HandleWorldClick(new GroundPoint(42, 10));
            Assert.That(controller.Session.FurnitureCount, Is.EqualTo(5));
            Click(root, "Undo last placement (Ctrl+Z)");
            Assert.That(root.Q<Button>("place-asset").enabledInHierarchy, Is.True);
            controller.HandleWorldClick(new GroundPoint(42, 22));
            Click(root, "Finalise scene");
            Assert.That(controller.Session.Frame.Population, Is.EqualTo(11));
            Assert.That(controller.Session.Frame.SeatedAgents, Is.EqualTo(11));
            yield return null;
            Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length, Is.GreaterThan(100));
            WorkspaceWorldView world = controller.GetComponentInChildren<WorkspaceWorldView>();
            world.Draw(new[] { new ViewBody("shape", ViewShape.Cylinder, ViewKind.Table, System.Numerics.Vector3.Zero, System.Numerics.Vector3.One) }, "probe-");
            world.Draw(new[] { new ViewBody("shape", ViewShape.Box, ViewKind.Table, System.Numerics.Vector3.Zero, System.Numerics.Vector3.One) }, "probe-");
            yield return null;
            Transform probe = world.GetComponentsInChildren<Transform>().Single(t => t.name == "probe-shape");
            Assert.That(probe.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("Cube"));
            world.Draw(new ViewBody[0], "probe-");
            LogAssert.NoUnexpectedReceived();
            yield return SceneManager.LoadSceneAsync("Bootstrap");
        }

        [UnityTest]
        public IEnumerator FurnishedEntryStartsAnEditableExampleAndRunsThePhysicalEvent()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-furnished-workspace"));
            yield return null;
            WorkspaceController controller = Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(controller.Session.PlannedCount, Is.EqualTo(26));
            Assert.That(controller.Session.FurnitureCount, Is.EqualTo(5));
            Assert.That(controller.Session.Frame.SeatedAgents, Is.EqualTo(11));
            Click(root, "Finalise scene");
            root.Q<Slider>("playback-speed").value = 4;
            Click(root, "Start"); Click(root, "Arm Explosion");
            controller.HandleWorldClick(new GroundPoint(18, 12));
            for (int i = 0; i < 900 && controller.Session.Tick < 2; i++) yield return null;
            Assert.That(controller.Session.Tick, Is.GreaterThanOrEqualTo(2));
            Assert.That(controller.Session.Frame.HazardReleased, Is.True);
            Click(root, "Pause");
            int tick = controller.Session.Tick;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(controller.Session.Tick, Is.EqualTo(tick));
            LogAssert.NoUnexpectedReceived();
            yield return SceneManager.LoadSceneAsync("Bootstrap");
        }

        private static void Click(VisualElement root, string label) => Click(root.Query<Button>().ToList().Single(b => b.text == label));
        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            { submit.target = button; button.SendEvent(submit); }
        }
    }
}
