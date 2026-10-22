using System;
using System.Buffers;
using System.IO;
using System.Text;
using CrowdSim.Recording;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Ground = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class RecordingContractTests
    {
        [Test]
        public void UnityNativeJpegWorkerCreatesIndexedAviAndPreservesBottomUpTextureOrientation()
        {
            string path = Path.Combine(Path.GetTempPath(), "crowdsim-unity-" + Guid.NewGuid().ToString("N") + ".avi");
            Texture2D decoded = new Texture2D(2, 2);
            try
            {
                byte[] rgba = ArrayPool<byte>.Shared.Rent(VideoRecording.Width * VideoRecording.Height * 4);
                Array.Clear(rgba, 0, rgba.Length);
                for (int y = 0; y < VideoRecording.Height; y++)
                for (int x = 0; x < VideoRecording.Width; x++)
                {
                    int p = (y * VideoRecording.Width + x) * 4;
                    rgba[p] = y >= VideoRecording.Height / 2 ? (byte)255 : (byte)0;
                    rgba[p + 2] = y < VideoRecording.Height / 2 ? (byte)255 : (byte)0; rgba[p + 3] = 255;
                }
                using (VideoRecording recording = new VideoRecording(path, pixels => ImageConversion.EncodeArrayToJPG(pixels,
                    GraphicsFormat.R8G8B8A8_UNorm, VideoRecording.Width, VideoRecording.Height, 0, 78)))
                {
                    Assert.That(recording.TrySubmit(rgba, pooled: true), Is.True); recording.Dispose();
                    Assert.That(recording.Error, Is.Null); Assert.That(recording.FramesWritten, Is.EqualTo(1));
                }
                byte[] avi = File.ReadAllBytes(path);
                int chunk = Encoding.ASCII.GetString(avi).IndexOf("movi", StringComparison.Ordinal) + 4;
                int size = BitConverter.ToInt32(avi, chunk + 4);
                byte[] jpeg = new byte[size]; Buffer.BlockCopy(avi, chunk + 8, jpeg, 0, size);
                Assert.That(decoded.LoadImage(jpeg), Is.True);
                Assert.That(decoded.width, Is.EqualTo(1280)); Assert.That(decoded.height, Is.EqualTo(720));
                Assert.That(decoded.GetPixel(100, 100).b, Is.GreaterThan(.9f));
                Assert.That(decoded.GetPixel(100, 600).r, Is.GreaterThan(.9f));
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void SizedStageIsRoomBoundedSavedAsV5AndRestoredAtReplay()
        {
            WorkspaceSession s = new WorkspaceSession(); Assert.That(s.Resize(10, 12), Is.True);
            s.SelectAsset(BuilderAsset.Stage); Assert.That(s.SetStageSize(11, 4), Is.False);
            Assert.That(s.SetStageSize(4, 2), Is.True); Assert.That(s.Place(PlacementTool.Asset, new Ground(5, 7), 0), Is.True);
            Assert.That(s.SetStageSize(6, 3), Is.True);
            Assert.That(s.Place(PlacementTool.Exit, new Ground(0, 3), 1), Is.True);
            Assert.That(s.Place(PlacementTool.Agents, new Ground(3, 3), 1), Is.True);
            string json = s.ExportAuthoredJson("sized"); Assert.That(json, Does.Contain("\"version\": 5"));
            s = WorkspaceSession.FromJson(json); Assert.That(s.StageLength, Is.EqualTo(6));
            Assert.That(s.Finalise(), Is.True); Assert.That(s.SetStageSize(3, 2), Is.False);
            Assert.That(s.Replay(), Is.True); Assert.That(s.StageLength, Is.EqualTo(6)); Assert.That(s.StageWidth, Is.EqualTo(3));
        }
    }
}
