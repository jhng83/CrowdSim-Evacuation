using System.Collections;
using CrowdSim.Unity.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Tests
{
    public sealed class StartupInteractionTests
    {
        [UnityTest]
        public IEnumerator ScenarioOverviewsResetAndRepeatedEnableDoesNotDuplicateTheInterface()
        {
            GameObject host = new GameObject("M1 test host");
            BootstrapController controller = host.AddComponent<BootstrapController>();
            try
            {
                yield return null;
                UIDocument document = host.GetComponent<UIDocument>();
                VisualElement root = document.rootVisualElement;
                Assert.That(controller.SelectedOverview, Is.EqualTo("Welcome"));
                Click(root.Q<Button>("explosion-overview"));
                Assert.That(controller.SelectedOverview, Is.EqualTo("Explosion"));
                Assert.That(root.Q<Label>("overview-description").text, Does.Contain("five zone radii"));
                Click(root.Q<Button>("fume-overview"));
                Assert.That(controller.SelectedOverview, Is.EqualTo("Toxic Fume"));
                Click(root.Q<Button>("reset-overview"));
                Assert.That(controller.SelectedOverview, Is.EqualTo("Welcome"));
                controller.enabled = false;
                controller.enabled = true;
                yield return null;
                root = document.rootVisualElement;
                Assert.That(root.Query<Button>("explosion-overview").ToList().Count, Is.EqualTo(1));
                Click(root.Q<Button>("fume-overview"));
                Assert.That(controller.SelectedOverview, Is.EqualTo("Toxic Fume"));
            }
            finally
            {
                Object.Destroy(host);
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
