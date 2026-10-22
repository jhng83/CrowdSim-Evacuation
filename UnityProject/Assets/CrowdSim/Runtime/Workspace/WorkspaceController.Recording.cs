using System;
using System.Buffers;
using System.Collections;
using System.IO;
using CrowdSim.Recording;
using CrowdSim.Workspace;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private VideoRecording _recording;
        private CaptureClock _captureClock;
        private RenderTexture _screenCapture, _videoCapture;
        private Texture2D _readback;
        private Toggle _recordNext;
        private Label _recordStatus, _recordBadge;
        private Button _recordStart, _recordStop;
        private bool _recordArmed;
        private string _recordResult = "No recording yet.", _recordPath = "";
        private int _lastFrames, _lastDropped;
        private double _captureMilliseconds, _encodeMilliseconds;
        private float _recordRefresh;
        public bool IsRecording => _recording != null;
        public string RecordingPath => _recordPath;
        private string RecordingDirectory => Path.Combine(Application.persistentDataPath, "recordings");

        private void BuildRecordingPage()
        {
            Label(_page, "Video recording", "heading");
            Label(_page, "Record the full application window, including controls, inspector and overlays. AVI · MJPEG · 1280 × 720 · 20 fps · no audio.", "muted");
            _recordNext = new Toggle("Record next Start") { value = _recordArmed, name = "record-next" };
            _recordNext.RegisterValueChangedCallback(evt => { _recordArmed = evt.newValue; UpdateRecordingText(); }); _page.Add(_recordNext);
            Label(_page, "Arm in the builder or at frame zero. Press Start after finalising. Resume does not restart a stopped recording.", "muted");
            _recordStart = Button(_page, "Start recording now", StartRecording); _recordStart.name = "record-start";
            _recordStop = Button(_page, "Stop and save video", () => StopRecording("Stopped and saved.")); _recordStop.name = "record-stop";
            _recordStatus = Label(_page, "", "diagnostic-card"); _recordStatus.name = "recording-status";
            Label(_page, "Output directory:\n" + RecordingDirectory, "file-path");
            Button(_page, "Open recording folder", () =>
            {
                try { Directory.CreateDirectory(RecordingDirectory); Application.OpenURL(new Uri(RecordingDirectory + Path.DirectorySeparatorChar).AbsoluteUri); }
                catch (Exception exception) { _recordResult = "Cannot open folder: " + exception.Message; UpdateRecordingText(); }
            });
            Label(_page, "Resize freely: the entire window is fitted with letterboxing. Pause remains in the video. Replay, scenario replacement and exit stop and finalize it. A bounded 3-frame encoding queue drops excess frames; dropped slots shorten the video rather than slowing simulation time. Stop before closing the Editor. Limit: 1 GiB / 100,000 frames per file.", "muted");
            UpdateRecordingText();
        }

        private void ToggleTransport()
        {
            bool starting = Session.Phase == WorkspacePhase.Ready;
            if (Session.ToggleRunning() && starting && Session.Running && _recordArmed)
            { _recordArmed = false; StartRecording(); }
            UpdateRecordingText();
        }

        /// <summary>Runtime capture uses no UnityEditor or Editor Recorder API.</summary>
        public void StartRecording()
        {
            if (_recording != null || Session == null || Session.IsBuilding) return;
            try
            {
                Directory.CreateDirectory(RecordingDirectory);
                _recordPath = Path.Combine(RecordingDirectory, "CrowdSim-" + Session.Scenario + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".avi");
                // Unity documents EncodeArrayToJPG as thread-safe. Texture/GPU APIs stay on the main thread.
                _recording = new VideoRecording(_recordPath, pixels => ImageConversion.EncodeArrayToJPG(pixels,
                    GraphicsFormat.R8G8B8A8_UNorm, VideoRecording.Width, VideoRecording.Height, 0, 78));
                _captureClock = new CaptureClock(); _recordArmed = false;
                _lastFrames = _lastDropped = 0; _captureMilliseconds = _encodeMilliseconds = 0;
                _recordResult = "Recording the full window.";
            }
            catch (Exception exception) { _recordResult = "Recording could not start: " + exception.Message; }
            UpdateRecordingText();
        }

        public void StopRecording(string reason = "Stopped and saved.")
        {
            VideoRecording recording = _recording; _recording = null; _captureClock = null; _recordArmed = false;
            if (recording != null)
            {
                recording.Dispose();
                _lastFrames = recording.FramesWritten; _lastDropped = recording.DroppedFrames;
                _encodeMilliseconds = recording.LastEncodingMilliseconds;
                _recordResult = recording.Error == null ? reason + (_lastFrames == 0 ? " No frames were captured." : "")
                    : "Recording stopped with an error: " + recording.Error.Message;
            }
            ReleaseCapture(); UpdateRecordingText();
        }

        private IEnumerator CaptureEndOfFrames()
        {
            WaitForEndOfFrame end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (_recording == null) continue;
                if (_recording.Error != null) { StopRecording(); continue; }
                if (!_captureClock.Due(Time.realtimeSinceStartupAsDouble)) continue;
                try
                {
                    _recording.CountSkipped(_captureClock.Skipped);
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    // Avoid readback/allocation when the bounded encoder queue is full.
                    if (_recording.QueueCount >= VideoRecording.QueueCapacity) _recording.CountSkipped(1);
                    else _recording.TrySubmit(CaptureWindowPixels(), pooled: true);
                    _captureMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                }
                catch (Exception exception) { StopRecording("Capture failed: " + exception.Message); }
                UpdateRecordingText();
            }
        }

        private byte[] CaptureWindowPixels()
        {
            int width = Screen.width, height = Screen.height;
            if (width <= 0 || height <= 0 || (long)width * height > 16777216 || width > 8192 || height > 8192)
                throw new InvalidOperationException("Recording supports windows up to 16 million pixels and 8192 pixels per side.");
            if (_screenCapture == null || _screenCapture.width != width || _screenCapture.height != height)
            {
                if (_screenCapture != null) { _screenCapture.Release(); Destroy(_screenCapture); }
                _screenCapture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
                _screenCapture.Create();
            }
            if (_videoCapture == null)
            {
                _videoCapture = new RenderTexture(VideoRecording.Width, VideoRecording.Height, 0, RenderTextureFormat.ARGB32);
                _videoCapture.Create();
                _readback = new Texture2D(VideoRecording.Width, VideoRecording.Height, TextureFormat.RGBA32, false);
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenCapture);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _videoCapture; GL.Clear(true, true, Color.black);
                float scale = Mathf.Min((float)VideoRecording.Width / width, (float)VideoRecording.Height / height);
                float fitWidth = width * scale, fitHeight = height * scale;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, VideoRecording.Width, VideoRecording.Height, 0);
                    Graphics.DrawTexture(new Rect((VideoRecording.Width - fitWidth) / 2, (VideoRecording.Height - fitHeight) / 2, fitWidth, fitHeight), _screenCapture);
                }
                finally { GL.PopMatrix(); }
                _readback.ReadPixels(new Rect(0, 0, VideoRecording.Width, VideoRecording.Height), 0, 0, false);
                byte[] pixels = ArrayPool<byte>.Shared.Rent(VideoRecording.Width * VideoRecording.Height * 4);
                try
                {
                    Unity.Collections.NativeArray<byte>.Copy(_readback.GetRawTextureData<byte>(), pixels, VideoRecording.Width * VideoRecording.Height * 4);
                    return pixels;
                }
                catch { ArrayPool<byte>.Shared.Return(pixels); throw; }
            }
            finally { RenderTexture.active = previous; }
        }

        private void ReleaseCapture()
        {
            foreach (RenderTexture texture in new[] { _screenCapture, _videoCapture })
                if (texture != null) { texture.Release(); Destroy(texture); }
            if (_readback != null) Destroy(_readback);
            _screenCapture = _videoCapture = null; _readback = null;
        }
        private void UpdateRecordingText()
        {
            if (_recordBadge != null) _recordBadge.text = IsRecording ? "● RECORDING" : _recordArmed ? "VIDEO ARMED" : "VIDEO IDLE";
            if (_activePage != "Recording" || _recordStatus == null) return;
            _recordNext.SetValueWithoutNotify(_recordArmed);
            _recordNext.SetEnabled(!IsRecording && (Session.IsBuilding || Session.Phase == WorkspacePhase.Ready));
            _recordStart.SetEnabled(!IsRecording && !Session.IsBuilding); _recordStop.SetEnabled(IsRecording);
            _recordStatus.text = _recordResult + "\nFrames " + (_recording?.FramesWritten ?? _lastFrames) +
                " · dropped " + (_recording?.DroppedFrames ?? _lastDropped) + " · queue " + (_recording?.QueueCount ?? 0) + "/3\n" +
                "Capture CPU " + _captureMilliseconds.ToString("F2") + " ms · last encode/write " + (_recording?.LastEncodingMilliseconds ?? _encodeMilliseconds).ToString("F2") +
                " ms\n" + (string.IsNullOrEmpty(_recordPath) ? "Path appears when recording starts." : _recordPath);
        }
        private void PollRecording()
        {
            if (_recording != null && _recording.Error != null) StopRecording();
            if (Time.unscaledTime >= _recordRefresh) { _recordRefresh = Time.unscaledTime + .25f; UpdateRecordingText(); }
        }
        private void OnApplicationQuit() => StopRecording("Saved on application exit.");
        private void OnDisable() => StopRecording("Saved when workspace closed.");
    }
}
