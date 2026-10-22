using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Unity.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Ground = System.Numerics.Vector2;
using Object = UnityEngine.Object;

namespace CrowdSim.Unity.Tests
{
    public sealed class StandaloneLifecycleTests
    {
        private GameObject _host;
        private string _directory, _recordingPath;

        [TearDown]
        public void CleanupOnFailure()
        {
            if (_host != null) { _host.GetComponent<WorkspaceController>().StopRecording(); Object.Destroy(_host); }
            if (!string.IsNullOrEmpty(_recordingPath) && File.Exists(_recordingPath)) File.Delete(_recordingPath);
            if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
        [UnityTest]
        public IEnumerator QuitDecisionCanCancelThenSaveWithoutClosingTheEditor()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "M11-quit-" + Guid.NewGuid().ToString("N"));
            GameObject host = new GameObject("M11 quit transaction");
            _host = host; _directory = directory;
            WorkspaceController workspace = host.AddComponent<WorkspaceController>(); workspace.Open(ScenarioType.Explosion, storageDirectory: directory);
            yield return null;
            Assert.That(workspace.Session.Place(CrowdSim.Workspace.PlacementTool.Exit, new Ground(.3f, 17), 3), Is.True);
            Assert.That(workspace.Session.Place(CrowdSim.Workspace.PlacementTool.Agents, new Ground(8, 8), 5), Is.True);
            Assert.That(workspace.Files.Finalise(), Is.True); workspace.Session.ToggleRunning();
            string authored = workspace.Session.ExportAuthoredJson("same"); int approved = 0;
            workspace.RequestQuit(() => approved++);
            Assert.That(workspace.Session.Running, Is.False);
            VisualElement root = host.GetComponent<UIDocument>().rootVisualElement;
            Click(root, "Keep working"); Assert.That(approved, Is.Zero);
            Assert.That(workspace.Session.ExportAuthoredJson("same"), Is.EqualTo(authored));
            workspace.RequestQuit(() => approved++);
            root.Q<TextField>("decision-save-name").value = "quit_saved";
            Click(root, "Save and continue"); Assert.That(approved, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(directory, "quit_saved.json")), Is.True);
            Assert.That(workspace.Session.IsDirty, Is.False);
            Object.Destroy(host); yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NormalQuitFinalizesRecordingBeforeInvokingTheExitOwner()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "M11-record-quit-" + Guid.NewGuid().ToString("N"));
            GameObject host = new GameObject("M11 recording exit");
            _host = host; _directory = directory;
            WorkspaceController workspace = host.AddComponent<WorkspaceController>(); workspace.Open(ScenarioType.Explosion, storageDirectory: directory);
            yield return null;
            workspace.Session.Place(CrowdSim.Workspace.PlacementTool.Exit, new Ground(.3f, 17), 3);
            workspace.Session.Place(CrowdSim.Workspace.PlacementTool.Agents, new Ground(8, 8), 1);
            Assert.That(workspace.Files.Save("clean"), Is.True); Assert.That(workspace.Files.Finalise(), Is.True);
            workspace.StartRecording(); Assert.That(workspace.IsRecording, Is.True);
            string path = workspace.RecordingPath;
            _recordingPath = path;
            yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame();
            bool approved = false;
            workspace.RequestQuit(() =>
            {
                Assert.That(workspace.IsRecording, Is.False);
                Assert.That(File.Exists(path), Is.True); Assert.That(new FileInfo(path).Length, Is.GreaterThan(256));
                approved = true;
            });
            Assert.That(approved, Is.True);
            Object.Destroy(host); yield return null;
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            LogAssert.NoUnexpectedReceived();
        }

        private static void Click(VisualElement root, string text)
        {
            Button button = root.Query<Button>().ToList().First(b => b.text == text);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            { submit.target = button; button.SendEvent(submit); }
        }
    }
}
