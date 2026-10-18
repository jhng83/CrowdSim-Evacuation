using System.Collections;
using System.Linq;
using CrowdSim.Unity.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Tests
{
    public sealed class DiagnosticSceneTests
    {
        [UnityTest]
        public IEnumerator ActualDiagnosticSceneStepsPausesAndCompletesTheReferenceCheck()
        {
            yield return SceneManager.LoadSceneAsync("SimulationParity", LoadSceneMode.Single);
            yield return null;
            SimulationParityController controller = Object.FindFirstObjectByType<SimulationParityController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Tick, Is.Zero);
            VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
            Click(root, "Step one tick");
            Assert.That(controller.Tick, Is.EqualTo(1));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(controller.Tick, Is.EqualTo(1), "Paused scheduling must not advance physics.");
            Click(root, "Run full 120-tick check");
            for (int i = 0; i < 600 && controller.Tick < 120; i++) yield return null;
            Assert.That(controller.Tick, Is.EqualTo(120));
            Assert.That(controller.CheckPassed, Is.True);
            Assert.That(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length, Is.GreaterThan(40));
            Click(root, "Load / reset selected");
            Assert.That(controller.Tick, Is.Zero);
            yield return null;
            LogAssert.NoUnexpectedReceived();
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
        }

        private static void Click(VisualElement root, string text)
        {
            Button button = root.Query<Button>().ToList().Single(b => b.text == text);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
