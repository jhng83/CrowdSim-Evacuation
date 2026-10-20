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
    public sealed class ExplosionInteractionTests
    {
        [UnityTest]
        public IEnumerator RadiusTextCommitsCancelsIncrementsResetsAndLocksOnDetonation()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-furnished-workspace"));
            yield return null;
            WorkspaceController controller = Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
            Click(root, "Hazard"); TextField kill = root.Q<TextField>("blast-radius-0");
            kill.Focus(); yield return null; kill.value = "1.25";
            Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.58f));
            Key(kill, KeyCode.Return); Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.25f));
            kill.value = "999"; Key(kill, KeyCode.Return);
            Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.25f));
            Assert.That(root.Q<Label>("blast-validation").text, Does.Contain("between"));
            Key(kill, KeyCode.Escape); Assert.That(kill.value, Is.EqualTo("1.25"));
            Click(root.Q<Button>("blast-plus-0")); Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.35f).Within(1e-5));
            kill.Focus(); kill.value = "1.4"; root.Q<Button>("blast-plus-1").Focus(); yield return null;
            Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.4f));
            Click(root, "Reset Zone Defaults"); Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.58f));
            Click(root, "Finalise scene"); Click(root, "Start");
            kill.value = "1.25"; Key(kill, KeyCode.Return); Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.25f));
            Click(root, "Arm Explosion"); Assert.That(controller.Session.ExplosionArmed, Is.True);
            controller.HandleWorldClick(new GroundPoint(10, 28)); Assert.That(controller.Session.Blast.Detonated, Is.False);
            controller.HandleWorldClick(new GroundPoint(18, 12));
            Assert.That(controller.Session.Blast.Detonated, Is.True); Assert.That(kill.enabledInHierarchy, Is.False);
            Assert.That(controller.Session.ExplosionArmed, Is.False);
            Assert.That(controller.Session.SetBlastRadius(0, "1.5"), Is.False);
            Click(root, "Replay from start");
            Assert.That(controller.Session.Tick, Is.Zero); Assert.That(kill.enabledInHierarchy, Is.True);
            Assert.That(controller.Session.BlastRadii[0], Is.EqualTo(1.25f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActualMeshesSurviveHiddenInspectorTogglesAndCleanUpAfterReplayAndSceneExit()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-furnished-workspace"));
            yield return null;
            WorkspaceController controller = Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
            Click(root, "Finalise scene"); Click(root, "Start"); Click(root, "Arm Explosion");
            Click(root, "Cancel Explosion"); Assert.That(controller.Session.ExplosionArmed, Is.False);
            Click(root, "Arm Explosion"); controller.HandleWorldClick(new GroundPoint(18, 12));
            Click(root, "Pause"); Click(root, "Hazard");
            int tick = controller.Session.Tick; var bodies = controller.Session.Frame.Bodies.Select(b => b.Position).ToArray();
            WorkspaceExplosionView view = controller.GetComponentInChildren<WorkspaceExplosionView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<MeshFilter>().Any(f => f.sharedMesh.vertexCount > 0), Is.True);
            MeshRenderer fill = view.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "Fill" && r.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0);
            Assert.That(fill.sharedMaterial.renderQueue, Is.EqualTo(3000));
            Assert.That(fill.sharedMaterial.GetFloat("_ZWrite"), Is.Zero);
            root.Q<Toggle>("blast-zone-0").value = false;
            root.Q<Toggle>("blast-zones").value = false;
            root.Q<Toggle>("blast-zones").value = true;
            Click(root, "Hide inspector (F4)"); yield return null;
            Assert.That(controller.Session.ZoneVisibility[0], Is.False);
            Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0));
            Assert.That(controller.Session.Tick, Is.EqualTo(tick));
            Assert.That(controller.Session.Frame.Bodies.Select(b => b.Position), Is.EqualTo(bodies));
            Click(root, "Replay from start"); yield return null;
            Assert.That(view.GetComponentsInChildren<MeshRenderer>(), Is.Empty);
            Click(root, "Return to startup"); Click(root, "Discard session and continue"); yield return null;
            Assert.That(Object.FindFirstObjectByType<WorkspaceExplosionView>(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
        private static void Key(VisualElement field, KeyCode code)
        {
            using (KeyDownEvent key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = code }))
            { key.target = field; field.SendEvent(key); }
        }
        private static void Click(VisualElement root, string text) => Click(root.Query<Button>().ToList().Single(b => b.text == text));
        private static void Click(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            { submit.target = button; button.SendEvent(submit); }
        }
    }
}
