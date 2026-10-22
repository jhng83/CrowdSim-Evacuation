using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using CrowdSim.OpenGL.UI;
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
    public sealed class RecordingInteractionTests
    {
        [UnityTest]
        public IEnumerator StageFieldsResizeThePlacedConcertStageAndRejectOversize()
        {
            yield return Open();
            WorkspaceController c = UnityEngine.Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement;
            root.Q<DropdownField>("asset-picker").value = BuilderAssetSelection.Label(BuilderAsset.Stage, 1.2f);
            root.Q<FloatField>("stage-length").value = 8; root.Q<FloatField>("stage-width").value = 4;
            c.HandleWorldClick(new Ground(20, 15));
            Assert.That(c.Session.HasStage, Is.True); Assert.That(root.Q<FloatField>("stage-length").enabledInHierarchy, Is.True);
            root.Q<FloatField>("stage-length").value = 10; Assert.That(c.Session.StageLength, Is.EqualTo(10));
            root.Q<FloatField>("stage-length").value = 1000;
            Assert.That(root.Q<FloatField>("stage-length").value, Is.EqualTo(10));
            Assert.That(c.Session.StageLength, Is.EqualTo(10)); yield return null;
            Assert.That(c.GetComponentInChildren<WorkspaceWorldView>().GetComponentsInChildren<Transform>().Any(t => t.name.Contains("-led-screen")), Is.True);
            LogAssert.NoUnexpectedReceived(); yield return SceneManager.LoadSceneAsync("Bootstrap");
        }

        [UnityTest]
        public IEnumerator ArmedFullWindowRecordingContainsCorrectlyOrientedUiAndFinalizesOnReplayAndSceneExit()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Complete-window end-of-frame capture requires an interactive Game view and graphics device.");
            yield return Open();
            WorkspaceController c = UnityEngine.Object.FindFirstObjectByType<WorkspaceController>();
            VisualElement root = c.GetComponent<UIDocument>().rootVisualElement;
            c.Session.Place(CrowdSim.Workspace.PlacementTool.Exit, new Ground(0, 17), 5);
            c.Session.Place(CrowdSim.Workspace.PlacementTool.Agents, new Ground(24, 17), 5);
            Click(root, "Recording"); root.Q<Toggle>("record-next").value = true;
            Click(root, "Finalise scene");
            VisualElement red = Marker(root, Color.red, true), blue = Marker(root, Color.blue, false);
            Click(root, "Start"); Assert.That(c.IsRecording, Is.True); string path = c.RecordingPath;
            float until = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < until) yield return null;
            Click(root, "Pause"); int tick = c.Session.Tick;
            yield return null; Assert.That(c.Session.Tick, Is.EqualTo(tick));
            Click(root, "Replay from start"); Assert.That(c.IsRecording, Is.False);
            byte[] avi = File.ReadAllBytes(path); string ascii = Encoding.ASCII.GetString(avi);
            int avih = ascii.IndexOf("avih", StringComparison.Ordinal);
            Assert.That(BitConverter.ToInt32(avi, avih + 24), Is.GreaterThan(0)); Assert.That(ascii, Does.Contain("idx1"));
            int frame = ascii.IndexOf("movi", StringComparison.Ordinal) + 4;
            int size = BitConverter.ToInt32(avi, frame + 4); byte[] jpeg = new byte[size]; Buffer.BlockCopy(avi, frame + 8, jpeg, 0, size);
            Texture2D decoded = new Texture2D(2, 2);
            Assert.That(decoded.LoadImage(jpeg), Is.True);
            float scale = Mathf.Min(1280f / Screen.width, 720f / Screen.height);
            int x = Mathf.RoundToInt((1280 - Screen.width * scale) / 2 + Screen.width * .025f * scale);
            int y = Mathf.RoundToInt((720 - Screen.height * scale) / 2 + Screen.height * .025f * scale);
            Assert.That(decoded.GetPixel(x, 720 - 1 - y).r, Is.GreaterThan(.8f), "Top-left UI marker must remain at the top.");
            Assert.That(decoded.GetPixel(x, y).b, Is.GreaterThan(.8f), "Bottom-left UI marker must remain at the bottom.");
            UnityEngine.Object.Destroy(decoded); red.RemoveFromHierarchy(); blue.RemoveFromHierarchy(); File.Delete(path);
            c.StartRecording(); Assert.That(c.IsRecording, Is.True); path = c.RecordingPath;
            until = Time.realtimeSinceStartup + .2f; while (Time.realtimeSinceStartup < until) yield return null;
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            using (FileStream exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.That(exclusive.Length, Is.GreaterThan(224));
            File.Delete(path); LogAssert.NoUnexpectedReceived();
        }
        private static VisualElement Marker(VisualElement root, Color color, bool top)
        {
            VisualElement marker = new VisualElement { pickingMode = PickingMode.Ignore };
            marker.style.position = Position.Absolute; marker.style.left = 0; marker.style.width = Length.Percent(5); marker.style.height = Length.Percent(5);
            if (top) marker.style.top = 0; else marker.style.bottom = 0;
            marker.style.backgroundColor = color; root.Add(marker); return marker;
        }
        private static IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
            Click(UnityEngine.Object.FindFirstObjectByType<BootstrapController>().GetComponent<UIDocument>().rootVisualElement.Q<Button>("open-explosion-workspace"));
            yield return null;
        }
        private static void Click(VisualElement root, string text) => Click(root.Query<Button>().ToList().First(b => b.text == text));
        private static void Click(Button button)
        { using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); } }
    }
}
