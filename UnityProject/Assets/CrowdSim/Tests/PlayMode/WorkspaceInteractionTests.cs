using System.Collections;
using System.Linq;
using CrowdSim.Unity.Workspace;
using CrowdSim.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GroundPoint = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class WorkspaceInteractionTests
    {
        [UnityTest]
        public IEnumerator BootstrapWorkspaceAuthorsFinalisesRunsPausesAndRestoresStartup()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            yield return null;
            UIDocument startup = Object.FindFirstObjectByType<CrowdSim.Unity.Foundation.BootstrapController>().GetComponent<UIDocument>();
            Click(startup.rootVisualElement.Q<Button>("open-explosion-workspace"));
            yield return null;
            WorkspaceController controller = Object.FindFirstObjectByType<WorkspaceController>();
            Assert.That(controller, Is.Not.Null);
            UIDocument document = controller.GetComponent<UIDocument>();
            Assert.That(document.parentUI, Is.Null, "Workspace must own an independent panel.");
            VisualElement root = document.rootVisualElement;
            Assert.That(controller.Session.CanFinalise, Is.False);
            controller.HandleWorldClick(new GroundPoint(0, 17));
            DropdownField people = root.Query<DropdownField>().ToList().Single(d => d.label == "People per click");
            people.value = "15";
            controller.HandleWorldClick(new GroundPoint(25, 17));
            Assert.That(controller.Session.PlannedCount, Is.EqualTo(15));
            Assert.That(controller.Session.CanResize, Is.False);
            Click(root, "Finalise scene");
            Assert.That(controller.Session.Phase, Is.EqualTo(WorkspacePhase.Ready));
            Click(root, "Start");
            for (int i = 0; i < 180 && controller.Session.Tick == 0; i++) yield return null;
            Assert.That(controller.Session.Tick, Is.GreaterThan(0));
            Click(root, "Arm Explosion");
            controller.HandleWorldClick(new GroundPoint(28, 17));
            Assert.That(controller.Session.Frame.HazardReleased, Is.True);
            Click(root, "Pause");
            int tick = controller.Session.Tick;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(controller.Session.Tick, Is.EqualTo(tick));
            Click(root, "Debug");
            root.Query<Toggle>().ToList().Single(t => t.label == "Show room grid").value = true;
            Click(root, "Hide inspector (F4)");
            yield return null;
            Assert.That(controller.Session.InspectorVisible, Is.False);
            Assert.That(controller.Session.ShowGrid, Is.True);
            Assert.That(controller.Session.InspectorTab, Is.EqualTo(3));
            Click(root, "Return to startup");
            Click(root, "Discard session and continue");
            yield return null;
            Assert.That(Object.FindFirstObjectByType<WorkspaceController>(), Is.Null);
            Assert.That(startup.rootVisualElement.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            LogAssert.NoUnexpectedReceived();
        }
        private static void Click(VisualElement root, string text)
            => Click(root.Query<Button>().ToList().Single(b => b.text == text));
        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            { submit.target = button; button.SendEvent(submit); }
        }
    }
}
