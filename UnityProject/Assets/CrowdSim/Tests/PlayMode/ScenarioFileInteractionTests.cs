using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Unity.Workspace;
using CrowdSim.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CrowdSim.Unity.Tests
{
    public sealed class ScenarioFileInteractionTests
    {
        [UnityTest]
        public IEnumerator ActualFilesPageSavesPromptsForDirtyChangesAndLoadsPaused()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            string temporary = Path.Combine(Path.GetTempPath(), "crowdsim-ui-save-" + Guid.NewGuid().ToString("N"));
            GameObject host = new GameObject("M5 files test");
            WorkspaceController controller = host.AddComponent<WorkspaceController>();
            try
            {
                controller.Open(ScenarioType.Explosion, furnished: true, storageDirectory: temporary);
                yield return null;
                VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
                Click(root, "Files"); root.Q<TextField>("save-name").value = "ui-save"; Click(root, "Save");
                string saved = Path.Combine(temporary, "ui-save.json");
                Assert.That(File.Exists(saved), Is.True);
                Click(root, "Build / Run");
                root.Query<DropdownField>().ToList().Single(d => d.label == "People per click").value = "1";
                Click(root, "Place people");
                controller.HandleWorldClick(new System.Numerics.Vector2(48, 30));
                Assert.That(controller.Session.PlannedCount, Is.EqualTo(27));
                Click(root, "Files"); Click(root, "New Scenario");
                Assert.That(root.Q<VisualElement>("file-decision"), Is.Not.Null);
                Assert.That(controller.Files.Pending, Is.EqualTo(FileTransition.New));
                Click(root, "Keep working");
                Assert.That(controller.Session.PlannedCount, Is.EqualTo(27));
                Click(root, "New Scenario"); Click(root, "Save and continue");
                Assert.That(controller.Session.PlannedCount, Is.Zero);
                Assert.That(controller.Session.CanReplay, Is.False);
                Click(root, "Files");
                root.Q<DropdownField>("scenario-list").value = controller.Files.Entries.Single(e => e.Path == saved).Label;
                Click(root, "Load selected (paused)");
                Assert.That(controller.Session.IsBuilding && !controller.Session.Running, Is.True);
                Assert.That(controller.Session.PlannedCount, Is.EqualTo(27));
                Click(root, "Finalise scene"); Click(root, "Start");
                Click(root, "Files");
                Assert.That(root.Query<Button>().ToList().Single(b => b.text == "Load selected (paused)").enabledInHierarchy, Is.False);
                Click(root, "Replay from start");
                Assert.That(controller.Session.Tick, Is.Zero);
                Assert.That(controller.Session.Frame.Population, Is.EqualTo(27));
                Assert.That(controller.Session.Running, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); }
            yield return null;
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }

        [UnityTest]
        public IEnumerator ReadOnlyFumeImportReplaysAndSavesACopyAndMalformedLoadKeepsTheScene()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            string temporary = Path.Combine(Path.GetTempPath(), "crowdsim-ui-import-" + Guid.NewGuid().ToString("N"));
            GameObject host = new GameObject("M5 import test");
            WorkspaceController controller = host.AddComponent<WorkspaceController>();
            string source = Path.Combine(Application.streamingAssetsPath, "Scenarios", "toxic_fume_205.json");
            string original = File.ReadAllText(source);
            try
            {
                controller.Open(ScenarioType.Explosion, storageDirectory: temporary);
                yield return null;
                VisualElement root = controller.GetComponent<UIDocument>().rootVisualElement;
                Click(root, "Files"); root.Q<TextField>("import-path").value = source; Click(root, "Import path (paused)");
                Assert.That(controller.Files.CurrentEntry.ReadOnly, Is.True);
                Assert.That(controller.Session.PlannedCount, Is.EqualTo(205));
                Click(root, "Finalise scene"); Click(root, "Start"); Click(root, "Release Toxic Fume");
                Assert.That(controller.Session.FumeReleased, Is.True);
                Click(root, "Replay from start");
                Assert.That(controller.Session.HasFumeCache && !controller.Session.FumeReleased, Is.True);
                Assert.That(controller.Session.Frame.SeatedAgents, Is.EqualTo(80));
                Click(root, "Files"); root.Q<TextField>("save-name").value = "fume-copy"; Click(root, "Save As");
                Assert.That(File.Exists(Path.Combine(temporary, "fume-copy.json")), Is.True);
                Assert.That(File.ReadAllText(source), Is.EqualTo(original));
                Click(root, "Save As");
                Assert.That(root.Q<VisualElement>("file-decision"), Is.Not.Null);
                Click(root, "Back / keep existing save");
                string bad = Path.Combine(temporary, "bad.json"); File.WriteAllText(bad, "{broken}");
                WorkspaceSession retained = controller.Session;
                root.Q<TextField>("import-path").value = bad; Click(root, "Import path (paused)");
                Assert.That(controller.Session, Is.SameAs(retained));
                Assert.That(controller.Files.Message, Does.Contain("failed"));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(host); }
            yield return null;
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
        private static void Click(VisualElement root, string text)
            => Click(root.Query<Button>().ToList().Single(b => b.text == text));
        private static void Click(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            { submit.target = button; button.SendEvent(submit); }
        }
    }
}
