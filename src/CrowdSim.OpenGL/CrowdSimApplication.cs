using System.Diagnostics;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Persistence;
using CrowdSim.OpenGL.Rendering;
using CrowdSim.OpenGL.Recording;
using CrowdSim.OpenGL.UI;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace CrowdSim.OpenGL;

/// <summary>
/// Native OpenGL application shell. Silk.NET owns the window/context and Dear ImGui owns both
/// the visible controls and projected 3D draw lists. Physics remain entirely in CrowdSim.Core.
/// </summary>
public sealed class CrowdSimApplication : IDisposable
{
    private readonly IWindow _window;
    private SimulationConfig _config = SimulationConfig.CreateDefault();
    private readonly WorldRenderer _renderer = new();
    private readonly SimulationUi _ui;
    private readonly int? _randomSeed;
    private GL? _gl;
    private IInputContext? _input;
    private ImGuiController? _imgui;
    private Simulation _simulation;
    private double _stepAccumulator;
    private bool _disposed;
    private bool _graphicsDisposed;
    private SimulationVideoRecorder? _videoRecorder;
    private RuntimePerformanceStats _lastPerformanceStats;
    private bool _undoShortcutWasDown;
    private bool _rotateShortcutWasDown;
    private bool _inspectorShortcutWasDown;
    private FinalizedSceneCheckpoint? _replayCheckpoint;

    public CrowdSimApplication(int? randomSeed)
    {
        _randomSeed = randomSeed;
        _simulation = new Simulation(0, _config, randomSeed);
        _ui = new SimulationUi();

        WindowOptions options = WindowOptions.Default;
        options.Title = "Crowd Evacuation Simulation - Explosion / Toxic Fume SFM";
        options.Size = new Vector2D<int>(1600, 900);
        options.VSync = true;
        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    /// <summary>Run the Silk.NET event loop.</summary>
    public int Run()
    {
        _window.Run();
        return 0;
    }

    private void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _input = _window.CreateInput();
        ImGuiFontConfig? font = CreateDashboardFont();
        _imgui = font is ImGuiFontConfig configuredFont
            ? new ImGuiController(_gl, _window, _input, configuredFont)
            : new ImGuiController(_gl, _window, _input);
        if (font is null) ImGui.GetIO().FontGlobalScale = 1.2f;

        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        ImGui.StyleColorsDark();
        ConfigureDashboardStyle();
    }

    /// <summary>
    /// Poll native keyboard state so Ctrl+Z is independent of Dear ImGui focus/key ownership.
    /// The rising-edge latch guarantees one undo per physical key press.
    /// </summary>
    private void HandlePhysicalKeyboardShortcuts()
    {
        if (_input is null)
        {
            _undoShortcutWasDown = false;
            _rotateShortcutWasDown = false;
            _inspectorShortcutWasDown = false;
            return;
        }

        bool ctrlZDown = false;
        bool rotateDown = false;
        bool reverseRotation = false;
        bool inspectorDown = false;
        foreach (IKeyboard keyboard in _input.Keyboards)
        {
            bool ctrlDown =
                keyboard.IsKeyPressed(Key.ControlLeft) ||
                keyboard.IsKeyPressed(Key.ControlRight);

            if (ctrlDown && keyboard.IsKeyPressed(Key.Z)) ctrlZDown = true;
            inspectorDown |= keyboard.IsKeyPressed(Key.F4) && !keyboard.IsKeyPressed(Key.AltLeft) &&
                !keyboard.IsKeyPressed(Key.AltRight);
            bool modifierDown = ctrlDown || keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight);
            if (!modifierDown && keyboard.IsKeyPressed(Key.R))
            {
                rotateDown = true;
                reverseRotation |= keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);
            }
        }

        if (ctrlZDown && !_undoShortcutWasDown)
        {
            _ui.TryUndoLastPlacementShortcut(_simulation);
        }

        if (rotateDown && !_rotateShortcutWasDown) _ui.TryRotateSelectedAssetShortcut(_simulation, reverseRotation);
        _rotateShortcutWasDown = rotateDown;
        _undoShortcutWasDown = ctrlZDown;
        if (inspectorDown && !_inspectorShortcutWasDown) _ui.TryToggleInspectorShortcut();
        _inspectorShortcutWasDown = inspectorDown;
    }

    /// <summary>Use the platform's interface font without bundling or downloading font assets.</summary>
    private static ImGuiFontConfig? CreateDashboardFont()
    {
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates =
        {
            Path.Combine(fonts, "segoeui.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
        };
        foreach (string path in candidates)
            if (File.Exists(path)) return new ImGuiFontConfig(path, 16);
        return null;
    }

    private static void ConfigureDashboardStyle()
    {
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 6f;
        style.ChildRounding = 6f;
        style.FrameRounding = 4f;
        style.GrabRounding = 4f;
        style.WindowPadding = new Vector2(12f, 12f);
        style.FramePadding = new Vector2(10f, 6f);
        style.ItemSpacing = new Vector2(10f, 8f);
        style.ItemInnerSpacing = new Vector2(6f, 5f);
        style.ScrollbarSize = 16f;
        style.WindowBorderSize = 1f;
        style.ChildBorderSize = 1f;
        style.FrameBorderSize = 1f;
        style.DisabledAlpha = 0.48f;

        style.Colors[(int)ImGuiCol.Text] = new Vector4(0.88f, 0.92f, 0.97f, 1f);
        style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.57f, 0.65f, 0.75f, 1f);
        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.045f, 0.059f, 0.082f, 1f);
        style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.065f, 0.082f, 0.110f, 1f);
        style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.10f, 0.14f, 1f);
        style.Colors[(int)ImGuiCol.Border] = new Vector4(0.17f, 0.22f, 0.29f, 1f);
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.09f, 0.11f, 0.14f, 1f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.13f, 0.17f, 0.22f, 1f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.16f, 0.22f, 0.30f, 1f);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.12f, 0.17f, 0.23f, 1f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.18f, 0.28f, 0.39f, 1f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.15f, 0.38f, 0.58f, 1f);
        style.Colors[(int)ImGuiCol.Header] = new Vector4(0.13f, 0.26f, 0.38f, 1f);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.18f, 0.36f, 0.52f, 1f);
        style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.16f, 0.44f, 0.66f, 1f);
        style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(0.24f, 0.72f, 1f, 1f);
        style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.22f, 0.65f, 0.92f, 1f);
        style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.32f, 0.78f, 1f, 1f);
        style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.17f, 0.22f, 0.29f, 1f);
        style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.05f, 0.07f, 0.10f, 1f);
        style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.22f, 0.29f, 0.38f, 1f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.28f, 0.37f, 0.48f, 1f);
    }

    private void OnRender(double deltaSeconds)
    {
        if (_gl is null || _imgui is null)
        {
            return;
        }

        long frameStart = Stopwatch.GetTimestamp();
        long allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        long physicsStart = Stopwatch.GetTimestamp();
        AdvanceSimulation(deltaSeconds);
        double physicsMilliseconds = ElapsedMilliseconds(physicsStart);
        _imgui.Update((float)Math.Max(1e-6, deltaSeconds));

        HandlePhysicalKeyboardShortcuts();

        _gl.ClearColor(0.035f, 0.040f, 0.050f, 1f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit);

        Vector2D<int> size = _window.Size;
        bool runningBeforeUi = _simulation.Running;
        _ui.SetPerformanceStats(_lastPerformanceStats);
        long uiStart = Stopwatch.GetTimestamp();
        _ui.Draw(_simulation, _renderer, new Vector2(Math.Max(1, size.X), Math.Max(1, size.Y)));
        double uiMilliseconds = ElapsedMilliseconds(uiStart);

        if (_ui.ConsumeSceneFinalisedRequest())
        {
            try
            {
                _replayCheckpoint = FinalizedSceneCheckpoint.Capture(
                    _simulation,
                    _ui.DesiredAgentCount,
                    _ui.ScenarioName);
            }
            catch (Exception exception)
            {
                _replayCheckpoint = null;
                _ui.NotifyScenarioOperationFailed(
                    $"Could not create the replay checkpoint: {exception.Message}");
            }
        }

        string? saveScenarioName = _ui.ConsumeScenarioSaveRequest();
        if (saveScenarioName is not null)
        {
            try
            {
                string safeName = ScenarioFileStore.SanitizeName(saveScenarioName);
                ScenarioDefinition scenario = ScenarioDefinition.Capture(
                    _simulation,
                    _ui.DesiredAgentCount,
                    safeName);
                string path = ScenarioFileStore.Save(scenario);
                _ui.NotifyScenarioSaved(safeName, path);
            }
            catch (Exception exception)
            {
                _ui.NotifyScenarioOperationFailed(exception.Message);
            }
        }

        if (_ui.ConsumeStopRecordingRequest())
        {
            StopRecording();
        }

        string? loadScenarioName = _ui.ConsumeScenarioLoadRequest();
        if (loadScenarioName is not null)
        {
            try
            {
                StopRecording();
                ScenarioDefinition scenario = ScenarioFileStore.Load(loadScenarioName);
                SimulationConfig loadedConfig = scenario.CreateConfig();
                Simulation loadedSimulation = new(0, loadedConfig, scenario.RandomSeed);
                scenario.ApplyLayout(loadedSimulation);

                _config = loadedConfig;
                _simulation = loadedSimulation;
                _replayCheckpoint = null;
                _renderer.ResetVisualState();
                _ui.NotifyScenarioLoaded(scenario);
                _stepAccumulator = 0d;
            }
            catch (Exception exception)
            {
                _ui.NotifyScenarioOperationFailed(exception.Message);
            }
        }

        if (_ui.ConsumeRestartSimulationRequest())
        {
            try
            {
                StopRecording();
                _replayCheckpoint ??= FinalizedSceneCheckpoint.Capture(
                    _simulation,
                    _ui.DesiredAgentCount,
                    _ui.ScenarioName);
                Simulation restarted = _replayCheckpoint.CreateReadySimulation();

                _config = restarted.Config;
                _simulation = restarted;
                _renderer.ResetVisualState();
                _ui.NotifySimulationRestarted();
                _stepAccumulator = 0d;
            }
            catch (Exception exception)
            {
                _ui.NotifyScenarioOperationFailed($"Replay failed: {exception.Message}");
            }
        }

        if (_ui.ConsumeNewScenarioRequest())
        {
            StopRecording();
            _config = SimulationConfig.CreateDefault();
            _simulation = new Simulation(0, _config, _randomSeed);
            _replayCheckpoint = null;
            _renderer.ResetVisualState();
            _ui.ResetForNewScenario();
            _stepAccumulator = 0d;
        }

        bool startedThisFrame = !runningBeforeUi && _simulation.Running;
        bool pausedThisFrame = runningBeforeUi && !_simulation.Running;
        if (startedThisFrame && _ui.RecordNextRun && _videoRecorder is null)
        {
            StartRecording();
        }
        else if (pausedThisFrame && _videoRecorder is not null)
        {
            StopRecording();
        }

        _imgui.Render();

        double recordingMilliseconds = 0d;
        if (_videoRecorder is not null && _simulation.Running)
        {
            long recordingStart = Stopwatch.GetTimestamp();
            try
            {
                _videoRecorder.Capture(_gl, Math.Max(1, size.X), Math.Max(1, size.Y), deltaSeconds);
            }
            catch (Exception exception)
            {
                StopRecording(exception.Message);
            }
            recordingMilliseconds = ElapsedMilliseconds(recordingStart);
        }

        int activeAgents = 0;
        foreach (var agent in _simulation.Agents)
        {
            if (agent.IsActive)
            {
                activeAgents++;
            }
        }

        long allocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedStart);
        _lastPerformanceStats = new RuntimePerformanceStats(
            FrameMilliseconds: ElapsedMilliseconds(frameStart),
            PhysicsMilliseconds: physicsMilliseconds,
            WorldDrawMilliseconds: _renderer.LastDrawMilliseconds,
            UiMilliseconds: uiMilliseconds,
            RecordingMilliseconds: recordingMilliseconds,
            AllocatedBytes: allocatedBytes,
            VisibleAgents: _renderer.LastVisibleAgentCount,
            ActiveAgents: activeAgents,
            RecordingQueueDepth: _videoRecorder?.QueuedFrames ?? 0,
            RecordingDroppedFrames: _videoRecorder?.DroppedFrames ?? 0);

        if (_ui.ExitRequested)
        {
            _window.Close();
        }
    }

    private void StartRecording()
    {
        try
        {
            string directory = Path.Combine(Environment.CurrentDirectory, "recordings");
            Directory.CreateDirectory(directory);
            string scenario = ScenarioFileStore.SanitizeName(_ui.ScenarioName);
            string path = Path.Combine(
                directory,
                $"{scenario}_{DateTime.Now:yyyyMMdd_HHmmss}.avi");
            _videoRecorder = new SimulationVideoRecorder(path);
            _ui.NotifyRecordingStarted(path);
        }
        catch (Exception exception)
        {
            _videoRecorder?.Dispose();
            _videoRecorder = null;
            _ui.NotifyRecordingFailed(exception.Message);
        }
    }

    private void StopRecording(string? error = null)
    {
        if (_videoRecorder is null)
        {
            return;
        }

        string path = _videoRecorder.OutputPath;
        try
        {
            _videoRecorder.Dispose();
            if (error is null)
            {
                _ui.NotifyRecordingStopped(path);
            }
            else
            {
                _ui.NotifyRecordingFailed(error);
            }
        }
        catch (Exception exception)
        {
            _ui.NotifyRecordingFailed(exception.Message);
        }
        finally
        {
            _videoRecorder = null;
        }
    }

    private void AdvanceSimulation(double deltaSeconds)
    {
        if (!_simulation.Running)
        {
            _stepAccumulator = 0d;
            return;
        }

        double fixedStep = Math.Max(1e-6, _simulation.Config.Agent.Dt);
        // A stalled frame is not simulation time owed: admitting seconds of wall time here
        // produces a visible jump when rendering resumes. Preserve fixed physics steps.
        _stepAccumulator += Math.Clamp(deltaSeconds, 0d, fixedStep * 2d) * _ui.SimulationSpeed;

        // Protect the UI from a spiral-of-death after a breakpoint or window stall. The physics
        // step is still fixed; this only limits how many catch-up steps may run in one render tick.
        int steps = 0;
        const int maxCatchUpSteps = 8;
        long updateStarted = Stopwatch.GetTimestamp();
        while (_stepAccumulator >= fixedStep && steps < maxCatchUpSteps)
        {
            _simulation.Step();
            _stepAccumulator -= fixedStep;
            steps++;
            if (ElapsedMilliseconds(updateStarted) >= 12d)
            {
                // Yield to rendering/input instead of doing several expensive steps unseen.
                _stepAccumulator = Math.Min(_stepAccumulator, fixedStep);
                break;
            }
        }

        if (steps == maxCatchUpSteps && _stepAccumulator > fixedStep * maxCatchUpSteps)
        {
            _stepAccumulator = fixedStep;
        }
    }

    private static double ElapsedMilliseconds(long startTimestamp)
        => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

    private void OnFramebufferResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
    }

    private void OnClosing()
    {
        StopRecording();
        DisposeGraphicsResources();
    }

    private void DisposeGraphicsResources()
    {
        if (_graphicsDisposed)
        {
            return;
        }

        _graphicsDisposed = true;
        _imgui?.Dispose();
        _input?.Dispose();
        _gl?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopRecording();
        DisposeGraphicsResources();
        _window.Dispose();
    }
}
