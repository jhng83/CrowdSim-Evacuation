using System.Collections;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Unity.Foundation;
using CrowdSim.Unity.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Ground = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class HumanoidInteractionTests
    {
        [UnityTest]
        public IEnumerator ActualFollowPovHiddenInspectorReplayAndBundledFontWorkTogether()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            VisualElement startup = Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement;
            Click(startup.Q<Button>("open-explosion-workspace")); yield return null;
            WorkspaceController controller = Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
            controller.HandleWorldClick(new Ground(0, 17));
            root.Query<DropdownField>().ToList().Single(d => d.label == "People per click").value = "15";
            controller.HandleWorldClick(new Ground(24, 17)); Click(root, "Finalise scene");
            controller.Session.SelectAgentById(0); yield return null;
            WorkspaceWorldView world = Object.FindFirstObjectByType<WorkspaceWorldView>();
            Assert.That(world.Humans.ActiveCount, Is.EqualTo(15));
            Assert.That(world.Humans.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(Resources.Load<Font>("CrowdSim/Fonts/AtkinsonHyperlegible-Regular"), Is.Not.Null);
            // Session commands expose exactly the controls' state path; actual buttons are exercised below.
            Click(root, "Show selected agent"); Click(root, "Follow selected agent"); yield return null;
            Assert.That(controller.Session.CameraMode, Is.EqualTo(AgentCameraMode.Follow));
            Assert.That(world.ViewCamera.orthographic, Is.True);
            Ground selected = controller.Session.SelectedVisual.Position;
            Vector3 center = world.ViewCamera.WorldToViewportPoint(new Vector3(selected.X, 0, selected.Y));
            Assert.That(center.x, Is.EqualTo(.5f).Within(.02f)); Assert.That(center.y, Is.EqualTo(.5f).Within(.02f));
            Click(root, "Enter agent POV"); yield return null;
            Assert.That(world.ViewCamera.orthographic, Is.False); Assert.That(world.ViewCamera.transform.position.y, Is.EqualTo(1.496f).Within(.0001f));
            Assert.That(world.transform.Find("Humanoid presentation/Human 0").gameObject.activeSelf, Is.False);
            Click(root, "Hide inspector (F4)"); yield return null;
            Assert.That(controller.Session.SelectedAgentId, Is.EqualTo(0)); Assert.That(controller.Session.CameraMode, Is.EqualTo(AgentCameraMode.Pov));
            Click(root, "Exit POV → Follow"); yield return null;
            Assert.That(world.ViewCamera.orthographic, Is.True); Assert.That(controller.Session.InspectorVisible, Is.False);
            Click(root, "Replay from start"); yield return null;
            Assert.That(controller.Session.SelectedAgentId, Is.Null); Assert.That(controller.Session.CameraMode, Is.EqualTo(AgentCameraMode.Overview));
            Assert.That(world.Humans.ActiveCount, Is.EqualTo(15)); LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RenderedVolumesArePickableForStandingSeatedCrawlingAndProneAndLeaveNoActiveGhosts()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            GameObject host = new GameObject("M8 view test");
            WorkspaceWorldView world = host.AddComponent<WorkspaceWorldView>(); world.Initialise();
            world.Fit(30, 20, new Rect(.1f, .1f, .8f, .8f));
            AgentVisualFrame[] samples = {
                new AgentVisualFrame(0, new Ground(5, 10), Ground.UnitX, 0, AgentState.Wandering),
                new AgentVisualFrame(1, new Ground(12, 10), Ground.UnitX, 0, AgentState.Wandering, SeatingPosture.Seated),
                new AgentVisualFrame(2, new Ground(19, 10), Ground.UnitX, 0, AgentState.Crawling),
                new AgentVisualFrame(3, new Ground(26, 10), Ground.UnitX, 0, AgentState.Dead) };
            world.Humans.RenderSamples(samples, 0, 0, false, 1, .15f, null, AgentCameraMode.Overview); yield return null;
            foreach (AgentVisualFrame sample in samples)
            {
                float height = sample.Pose == HumanPose.Prone ? .17f : sample.Pose == HumanPose.Crawling ? .34f : sample.Pose == HumanPose.Seated ? .8f : 1.1f;
                Vector3 screen = world.ViewCamera.WorldToScreenPoint(new Vector3(sample.Position.X, height, sample.Position.Y));
                Assert.That(world.Humans.Pick(screen), Is.EqualTo(sample.Id));
            }
            Assert.That(world.Humans.Pick(new Vector2(-20, -20)), Is.Null);
            world.Humans.RenderSamples(new AgentVisualFrame[0], 1, 0, false, 1, .15f, null, AgentCameraMode.Overview);
            Assert.That(world.Humans.ActiveCount, Is.Zero); Assert.That(world.Humans.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
            world.Humans.RenderSamples(samples, 0, 0, false, 1, .15f, null, AgentCameraMode.Overview);
            Assert.That(world.Humans.ActiveCount, Is.EqualTo(4));
            Object.Destroy(host); yield return null; LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator PoseReferenceSceneUsesTheProductionHumanoidRenderer()
        {
            yield return SceneManager.LoadSceneAsync("HumanoidPresentation"); yield return null;
            WorkspaceHumanoidView humans = Object.FindFirstObjectByType<WorkspaceHumanoidView>();
            Assert.That(humans, Is.Not.Null); Assert.That(humans.ActiveCount, Is.EqualTo(14));
            Assert.That(humans.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(humans.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial.shader.name != "Hidden/InternalErrorShader"), Is.True);
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Assert.That(Object.FindFirstObjectByType<WorkspaceHumanoidView>(), Is.Null); LogAssert.NoUnexpectedReceived();
        }
        private static void Click(VisualElement root, string text) => Click(root.Query<Button>().ToList().First(b => b.text == text));
        private static void Click(Button button)
        { using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
