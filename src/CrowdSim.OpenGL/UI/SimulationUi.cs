using System.Globalization;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.OpenGL.Rendering;
using ImGuiNET;

namespace CrowdSim.OpenGL.UI;

/// <summary>
/// Full mouse/UI-driven application shell. Normal operation does not depend on keypad or
/// keyboard shortcuts: scene construction, run control, explosion placement, camera, debug
/// overlays and agent inspection are all visible controls.
/// </summary>
public sealed partial class SimulationUi
{
    public const float SidebarWidth = DashboardLayout.PreferredControlsWidth;
    public const float InspectorWidth = DashboardLayout.PreferredInspectorWidth;
    private const float TopStatusBarHeight = 88f;
    private const float BottomStatusBarHeight = 70f;

    private static readonly int[] ExitPersonOptions = { 1, 3, 5 };
    private static readonly int[] AgentPlacementOptions = { 1, 5, 15, 50 };

    private readonly RenderDebugOptions _debug = new();
    private readonly InspectorPanelState _inspector = new();
    private ControlPanelTab _controlPanelTab;
    private enum ControlPanelTab { Scene, Camera, Files }
    private int _agentCount;
    private int _agentPlacementCount = 1;
    private readonly BuilderAssetSelection _assetSelection = new();
    private bool _worldCanvasHovered;
    private int _exitPersonsWide = 1;
    private AppMode _mode = AppMode.ScenarioSelection;
    private BuilderTool _builderTool = BuilderTool.Exit;
    private WorldInteractionMode _worldMode = WorldInteractionMode.SelectAgent;
    private string _statusMessage = "Choose Explosion or Toxic Fume to begin.";
    private bool _newScenarioRequested;
    private bool _returnToSelectionConfirmation;
    private bool _sceneFinalisedRequested;
    private float _simulationSpeed = 1f;
    private bool _followSelectedAgent;
    private bool _povEnabled;
    private string _scenarioName = "scenario_1";
    private string[] _savedScenarioNames = Array.Empty<string>();
    private int _selectedScenarioIndex = -1;
    private string? _scenarioSaveRequested;
    private string? _scenarioLoadRequested;
    private readonly Dictionary<string, CaretNumberEditor> _blastRadiusEditors =
        new(StringComparer.Ordinal);
    private bool _scenarioDirty;
    private string? _blastValidationMessage;
    private bool _restartSimulationRequested;
    private bool _recordNextRun;
    private bool _stopRecordingRequested;
    private bool _recordingActive;
    private string? _lastRecordingPath;
    private RuntimePerformanceStats _performanceStats;

    public SimulationUi()
    {
        // Population is placed spatially with the mouse instead of typed as a global count.
        _agentCount = 0;
        RefreshScenarioList();
    }

    /// <summary>True when the application should close.</summary>
    public bool ExitRequested { get; private set; }

    /// <summary>Real-time multiplier applied to the fixed simulation step accumulator.</summary>
    public float SimulationSpeed => _simulationSpeed;

    /// <summary>Currently selected agent, if any.</summary>
    public int? SelectedAgentId { get; private set; }

    /// <summary>Builder agent count used when capturing a scenario before finalisation.</summary>
    public int DesiredAgentCount => _agentCount;

    /// <summary>Whether the next fresh run should be recorded automatically.</summary>
    public bool RecordNextRun => _recordNextRun;

    /// <summary>Whether an AVI recording is currently active.</summary>
    public bool RecordingActive => _recordingActive;

    /// <summary>Current scenario display name.</summary>
    public string ScenarioName => _scenarioName;

    /// <summary>Update the lightweight diagnostic timings displayed in the Debug tab.</summary>
    public void SetPerformanceStats(RuntimePerformanceStats stats)
        => _performanceStats = stats;

    /// <summary>Render the docked operator UI and unobstructed 3D viewport.</summary>
    public void Draw(Simulation simulation, WorldRenderer renderer, Vector2 displaySize)
    {
        if (_mode == AppMode.ScenarioSelection)
        {
            DrawScenarioSelectionScreen(simulation, displaySize);
            return;
        }

        ValidateSelectedAgent(simulation);
        UiMetrics metrics = UiMetrics.Capture(simulation);

        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(displaySize, ImGuiCond.Always);

        ImGuiWindowFlags rootFlags =
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBringToFrontOnFocus;

        ImGui.Begin("##crowd-sim-root", rootFlags);

        ImGui.BeginChild(
            "##top-status-bar",
            new Vector2(0f, TopStatusBarHeight),
            ImGuiChildFlags.Borders,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawDashboardHeader(simulation, metrics);
        ImGui.EndChild();

        float remainingHeight = ImGui.GetContentRegionAvail().Y;
        float spacingY = ImGui.GetStyle().ItemSpacing.Y;
        float middleHeight = Math.Max(1f, remainingHeight - BottomStatusBarHeight - spacingY);

        ImGui.BeginChild(
            "##main-workspace",
            new Vector2(0f, middleHeight),
            ImGuiChildFlags.None,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        Vector2 workspace = ImGui.GetContentRegionAvail();
        DashboardLayout layout = DashboardLayout.Calculate(
            Math.Max(1f, workspace.X), _inspector.IsVisible, ImGui.GetStyle().ItemSpacing.X);
        ImGui.BeginChild("##left-control-panel", new Vector2(layout.ControlsWidth, 0f),
            ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawDashboardControls(simulation, renderer);
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild(
            "##world",
            new Vector2(layout.ViewportWidth, 0f),
            ImGuiChildFlags.Borders,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        DrawViewportToolbar();

        Agent? cameraAgent = SelectedAgentId is int cameraAgentId
            ? simulation.Agents.FirstOrDefault(agent => agent.AgentId == cameraAgentId && agent.IsActive)
            : null;

        renderer.Draw(
            simulation,
            builderMode: _mode == AppMode.Builder,
            builderTool: _builderTool,
            blockageCells: _assetSelection.BlockageCells,
            exitPersonsWide: _exitPersonsWide,
            agentPlacementCount: _agentPlacementCount,
            selectedAgentId: SelectedAgentId,
            cameraAgent: cameraAgent,
            followAgent: _followSelectedAgent,
            povEnabled: _povEnabled,
            simulationSpeed: _simulationSpeed,
            debug: _debug,
            furnitureRotation: _assetSelection.RotationRadians,
            snapBarricadeEnds: _assetSelection.SnapBarricadeEnds);

        _worldCanvasHovered = renderer.IsHovered;
        HandleWorldInput(simulation, renderer);
        ImGui.EndChild();

        if (_inspector.IsVisible)
        {
            ImGui.SameLine();
            ImGui.BeginChild("##right-analysis-panel", new Vector2(layout.InspectorWidth, 0f),
                ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            DrawDashboardInspector(simulation, metrics);
            ImGui.EndChild();
        }

        ImGui.EndChild();

        ImGui.BeginChild(
            "##bottom-status-bar",
            Vector2.Zero,
            ImGuiChildFlags.Borders,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawDashboardStatus(simulation);
        ImGui.EndChild();

        ImGui.End();
    }

    /// <summary>Consume the New Scenario command exactly once.</summary>
    public bool ConsumeNewScenarioRequest()
    {
        bool requested = _newScenarioRequested;
        _newScenarioRequested = false;
        return requested;
    }

    /// <summary>Consume the builder-to-ready transition exactly once.</summary>
    public bool ConsumeSceneFinalisedRequest()
    {
        bool requested = _sceneFinalisedRequested;
        _sceneFinalisedRequested = false;
        return requested;
    }

    /// <summary>Consume a Save Scenario command exactly once.</summary>
    public string? ConsumeScenarioSaveRequest()
    {
        string? request = _scenarioSaveRequested;
        _scenarioSaveRequested = null;
        return request;
    }

    /// <summary>Consume a Load Scenario command exactly once.</summary>
    public string? ConsumeScenarioLoadRequest()
    {
        string? request = _scenarioLoadRequested;
        _scenarioLoadRequested = null;
        return request;
    }

    /// <summary>Consume a restart-current-run request exactly once.</summary>
    public bool ConsumeRestartSimulationRequest()
    {
        bool requested = _restartSimulationRequested;
        _restartSimulationRequested = false;
        return requested;
    }

    /// <summary>Consume a manual stop-recording request exactly once.</summary>
    public bool ConsumeStopRecordingRequest()
    {
        bool requested = _stopRecordingRequested;
        _stopRecordingRequested = false;
        return requested;
    }

    public void NotifyScenarioSaved(string savedName, string path)
    {
        _scenarioName = savedName;
        _scenarioDirty = false;
        RefreshScenarioList(savedName);
        _statusMessage = $"Scenario '{savedName}' saved to {path}. Runtime injuries/panic/moved positions are not included.";
    }

    public void NotifyScenarioLoaded(ScenarioDefinition scenario)
    {
        int plannedCount = scenario.AgentSpawnGroups.Sum(group => group.Positions.Count) +
            scenario.Furniture.Sum(set => FurnitureSet.GetSeatCount(set.Kind));
        _agentCount = plannedCount > 0
            ? plannedCount
            : Math.Clamp(scenario.AgentCount, 0, 5000);
        _scenarioName = scenario.Name;
        _mode = AppMode.Builder;
        _builderTool = BuilderTool.Exit;
        _assetSelection.Reset();
        _worldCanvasHovered = false;
        _worldMode = WorldInteractionMode.SelectAgent;
        SelectedAgentId = null;
        _followSelectedAgent = false;
        _povEnabled = false;
        _scenarioDirty = false;
        _blastRadiusEditors.Clear();
        _blastValidationMessage = null;
        _recordNextRun = false;
        _recordingActive = false;
        RefreshScenarioList(scenario.Name);
        string fumeNote = scenario.ScenarioType == ScenarioType.ToxicFume
            ? " Recompute the toxic-fume dispersion before finalising."
            : string.Empty;
        _statusMessage =
            $"Scenario '{scenario.Name}' ({scenario.ScenarioType}) loaded with {scenario.Exits.Count} exits, " +
            $"{scenario.Blockages.Count} square blockages, {scenario.CircularBlockages.Count} circular blockages, " +
            $"{scenario.BlueWalls.Count} legacy blue walls and " +
            $"{_agentCount} planned agents. Review it, then Finalise Scene.{fumeNote}";
    }

    public void NotifyScenarioOperationFailed(string message)
        => _statusMessage = $"Scenario file operation failed: {message}";

    public void NotifySimulationRestarted()
    {
        _mode = AppMode.Simulation;
        _worldMode = WorldInteractionMode.SelectAgent;
        SelectedAgentId = null;
        _followSelectedAgent = false;
        _povEnabled = false;
        _recordingActive = false;
        _stopRecordingRequested = false;
        _blastRadiusEditors.Clear();
        _statusMessage = "Replay ready at the finalised scene checkpoint (frame 0, not started).";
    }

    public void NotifyRecordingStarted(string path)
    {
        _recordingActive = true;
        _recordNextRun = false;
        _lastRecordingPath = path;
        _statusMessage = $"Recording started: {Path.GetFileName(path)}";
    }

    public void NotifyRecordingStopped(string path)
    {
        _recordingActive = false;
        _lastRecordingPath = path;
        _statusMessage = $"Recording saved: {path}";
    }

    public void NotifyRecordingFailed(string message)
    {
        _recordingActive = false;
        _recordNextRun = false;
        _statusMessage = $"Recording failed: {message}";
    }

    /// <summary>Reset UI state after the application creates a fresh simulation.</summary>
    public void ResetForNewScenario()
    {
        _mode = AppMode.ScenarioSelection;
        _builderTool = BuilderTool.Exit;
        _assetSelection.Reset();
        _worldCanvasHovered = false;
        _worldMode = WorldInteractionMode.SelectAgent;
        _exitPersonsWide = 1;
        _agentPlacementCount = 1;
        _agentCount = 0;
        _newScenarioRequested = false;
        _returnToSelectionConfirmation = false;
        _sceneFinalisedRequested = false;
        _restartSimulationRequested = false;
        _scenarioSaveRequested = null;
        _scenarioLoadRequested = null;
        SelectedAgentId = null;
        _followSelectedAgent = false;
        _povEnabled = false;
        _scenarioDirty = false;
        _blastRadiusEditors.Clear();
        _blastValidationMessage = null;
        _recordNextRun = false;
        _recordingActive = false;
        _statusMessage = "Choose Explosion or Toxic Fume to begin the new scenario.";
    }

    private void DrawBuilderControls(Simulation simulation)
    {
        if (ImGui.CollapsingHeader("1. Room size", simulation.Environment.CanResizeRoom
                ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            DrawRoomSizeControls(simulation);
        if (ImGui.CollapsingHeader("2. Exits", ImGuiTreeNodeFlags.DefaultOpen))
            DrawExitPlacementControls(simulation);
        if (ImGui.CollapsingHeader("3. Assets & furniture", ImGuiTreeNodeFlags.DefaultOpen))
            DrawAssetPlacementControls(simulation);
        if (ImGui.CollapsingHeader("4. People", ImGuiTreeNodeFlags.DefaultOpen))
            DrawPeoplePlacementControls(simulation);
        ImGui.Spacing();
        if (DrawNeutralButton("Undo last placement (Ctrl+Z)", new Vector2(-1f, 30f)))
            UndoLastPlacement(simulation);
        if (simulation.ScenarioType == ScenarioType.ToxicFume)
            DrawToxicFumeBuilderControls(simulation);
        DrawBehaviorTuningControls(simulation);
    }

    private void DrawRoomSizeControls(Simulation simulation)
    {
        float roomWidth = simulation.Environment.Width;
        float roomLength = simulation.Environment.Height;
        bool canResize = simulation.Environment.CanResizeRoom;
        if (!canResize)
        {
            ImGui.BeginDisabled();
        }

        ImGui.Text("Room width (m)");
        ImGui.SetNextItemWidth(-1f);
        bool roomSizeChanged = ImGui.SliderFloat(
            "##RoomWidth", ref roomWidth,
            simulation.Config.World.MinWidthMeters,
            simulation.Config.World.MaxWidthMeters,
            "%.1f m");
        ImGui.Text("Room length (m)");
        ImGui.SetNextItemWidth(-1f);
        roomSizeChanged |= ImGui.SliderFloat(
            "##RoomLength", ref roomLength,
            simulation.Config.World.MinLengthMeters,
            simulation.Config.World.MaxLengthMeters,
            "%.1f m");
        if (!canResize)
        {
            ImGui.EndDisabled();
            ImGui.TextWrapped("Room size locks after your first placement. Undo the placements to resize.");
        }
        else
        {
            ImGui.TextWrapped("Dimensions are in metres. Agent height: 1.70 m.");
        }

        if (roomSizeChanged && simulation.Environment.TryResizeRoom(roomWidth, roomLength))
        {
            simulation.BuildWallGrid();
            InvalidateToxicFumeForGeometryChange(simulation);
            _scenarioDirty = true;
            _statusMessage = simulation.ScenarioType == ScenarioType.ToxicFume
                ? $"Simulation area resized to {roomWidth:F1} m x {roomLength:F1} m. Recompute the fume field if it was previously ready."
                : $"Simulation area resized to {roomWidth:F1} m x {roomLength:F1} m.";
        }
    }

    private void DrawExitPlacementControls(Simulation simulation)
    {
        ImGui.Text("EXIT");
        ImGui.SetNextItemWidth(-1f);
        string exitPreview = $"{_exitPersonsWide}-person exit";
        if (ImGui.BeginCombo("##ExitWidthCombo", exitPreview))
        {
            foreach (int persons in ExitPersonOptions)
            {
                bool selected = _exitPersonsWide == persons;
                if (ImGui.Selectable($"{persons}-person", selected))
                {
                    _exitPersonsWide = persons;
                    _statusMessage = $"Exit width set to {persons}-person. Press Place Exit, then click a boundary wall.";
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        DrawActiveButton(
            $"Place Exit ({_exitPersonsWide}-person)",
            _builderTool == BuilderTool.Exit,
            () =>
            {
                _builderTool = BuilderTool.Exit;
                _statusMessage = $"Exit tool active ({_exitPersonsWide}-person). Click close to a boundary wall.";
            });
    }

    private void DrawPeoplePlacementControls(Simulation simulation)
    {
        ImGui.Text("AGENTS");
        ImGui.TextDisabled($"Total occupants: {simulation.Environment.PlannedAgentCount}");
        ImGui.TextDisabled($"Seated: {simulation.Environment.Furniture.Sum(set => set.Chairs.Count)} | Floor placements: {simulation.Environment.AgentSpawnGroups.Sum(group => group.Count)}");
        ImGui.SetNextItemWidth(-1f);
        string agentPreview = _agentPlacementCount == 1 ? "1 agent" : $"{_agentPlacementCount} agents";
        if (ImGui.BeginCombo("##AgentPlacementCountCombo", agentPreview))
        {
            foreach (int count in AgentPlacementOptions)
            {
                bool selected = _agentPlacementCount == count;
                string label = count == 1 ? "1 agent" : $"{count} agents";
                if (ImGui.Selectable(label, selected))
                {
                    _agentPlacementCount = count;
                    _statusMessage = $"Agent group size set to {count}. Press Place Agents, then click a region in the room.";
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        DrawActiveButton(
            _agentPlacementCount == 1 ? "Place Agent (1)" : $"Place Agents ({_agentPlacementCount})",
            _builderTool == BuilderTool.Agents,
            () => ActivateAgentPlacement(_agentPlacementCount));
        ImGui.TextWrapped(
            "Click a region in the room. Multi-agent placement expands around blocked floor and existing agents instead of stacking inside walls.");
    }

    /// <summary>Fixed builder footer with readiness feedback and the finalisation action.</summary>
    private void DrawFinaliseControls(Simulation simulation)
    {
        ImGui.TextDisabled($"{simulation.Environment.Exits.Count} exits  |  {simulation.Environment.PlannedAgentCount} people placed");
        int plannedAgents = simulation.Environment.PlannedAgentCount;
        bool hasPopulation = plannedAgents > 0 || _agentCount > 0;
        bool toxicReady = simulation.ScenarioType != ScenarioType.ToxicFume ||
            (simulation.ToxicFumeSystem.SourcePosition is not null && simulation.ToxicFumeSystem.IsCacheValidFor(simulation));
        bool canFinalise = simulation.Environment.Exits.Count > 0 && hasPopulation && toxicReady;
        if (!canFinalise)
        {
            ImGui.BeginDisabled();
        }

        if (DrawActionButton(
                "Finalise Scene",
                new Vector4(0.14f, 0.52f, 0.82f, 1f),
                new Vector2(-1f, 38f)))
        {
            if (plannedAgents > 0)
            {
                simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
                _agentCount = plannedAgents;
            }
            else
            {
                // Legacy saved scenarios without mouse spawn groups retain their prior random spawn.
                simulation.SpawnAgents(_agentCount);
            }
            simulation.SetRunning(false);
            _mode = AppMode.Simulation;
            _sceneFinalisedRequested = true;
            _worldMode = WorldInteractionMode.SelectAgent;
            SelectedAgentId = null;
            _statusMessage = simulation.ScenarioType == ScenarioType.ToxicFume
                ? $"Toxic Fume scene finalised with {simulation.Agents.Count} agents and a cached plume. Press Start Simulation, then Release Toxic Fume when ready."
                : $"Explosion scene finalised with {simulation.Agents.Count} agents. Press Start Simulation.";
        }

        if (!canFinalise)
        {
            ImGui.EndDisabled();
            string reason = simulation.Environment.Exits.Count == 0
                ? "At least one exit is required."
                : !hasPopulation
                    ? "Place at least one agent group in the simulation area."
                    : simulation.ToxicFumeSystem.SourcePosition is null
                        ? "Place a toxic-fume release point before finalising."
                        : "Compute or recompute the toxic-fume dispersion before finalising.";
            DrawFittedText(reason, new Vector4(1f, 0.67f, 0.22f, 1f));
        }
    }

    private void ActivateAgentPlacement(int count)
    {
        _builderTool = BuilderTool.Agents;
        _agentPlacementCount = count;
        _statusMessage = $"Agent placement active ({count}). Click a region of clear/partly blocked floor.";
    }

    private void DrawBehaviorTuningControls(Simulation simulation)
    {
        if (!ImGui.CollapsingHeader("Behaviour tuning (advanced)"))
        {
            return;
        }

        BehaviorTuningConfig tuning = simulation.Config.Agent.Behavior;
        float traitVariation = tuning.TraitVariation;
        float reactionVariation = tuning.ReactionVariation;
        float stressSusceptibility = tuning.StressSusceptibility;
        float stressPersistence = tuning.StressPersistence;
        float crowdFollowing = tuning.CrowdFollowing;
        float personalSpace = tuning.PersonalSpace;
        float panicCompression = tuning.PanicSpaceCompression;
        float emergencyUrgency = tuning.EmergencyUrgency;
        float maxRunningSpeed = tuning.MaxRunningSpeedMetersPerSecond;
        float helping = tuning.HelpingTendency;
        float familiarity = tuning.RouteFamiliarity;
        float bottleneckAssertiveness = tuning.BottleneckAssertiveness;
        float bottleneckSensitivity = tuning.BottleneckPressureSensitivity;
        float bottleneckCompression = tuning.BottleneckSpaceCompression;

        bool changed = false;
        changed |= DrawFullWidthSlider(
            "Trait variation", "TraitVariation", ref traitVariation, 0f, 1f, "%.2f",
            "Controls how different individual agents are from the scenario averages. Low values make the crowd more uniform; high values create wider differences in following, helping, stress, urgency and assertiveness. Applied when agents are finalised.");
        changed |= DrawFullWidthSlider(
            "Reaction variation", "ReactionVariation", ref reactionVariation, 0f, 1f, "%.2f",
            "Controls how spread out reaction delays are after agents become aware of the emergency. Low values make reactions more synchronized; high values make some agents react earlier and others later.");
        changed |= DrawFullWidthSlider(
            "Stress susceptibility", "StressSusceptibility", ref stressSusceptibility, 0.60f, 1.40f, "%.2f",
            "Scales how strongly agents respond to threat and social stress cues. Lower values resist panic; higher values raise stress more easily.");
        changed |= DrawFullWidthSlider(
            "Stress persistence", "StressPersistence", ref stressPersistence, 0.50f, 2.00f, "%.2fx",
            "Controls how long elevated stress persists after the cues reduce. Lower values recover faster; higher values keep panic elevated for longer.");
        changed |= DrawFullWidthSlider(
            "Crowd following", "CrowdFollowing", ref crowdFollowing, 0f, 1f, "%.2f",
            "Sets the average tendency to follow coherent movement by nearby evacuees. Low values favor individual route choices; high values produce stronger herding/group flow.");
        changed |= DrawFullWidthSlider(
            "Personal space", "PersonalSpace", ref personalSpace, 0.85f, 1.30f, "%.2fx",
            "Scales preferred social spacing in the SFM. Lower values tolerate closer spacing; higher values produce looser crowds. Physical body size and collision radius do not change.");
        changed |= DrawFullWidthSlider(
            "Panic space compression", "PanicSpaceCompression", ref panicCompression, 0f, 0.35f, "%.2f",
            "Reduces preferred social spacing as panic rises. Higher values allow a panicked crowd to pack more tightly, but physical collision guards still prevent agents from passing through each other.");
        changed |= DrawFullWidthSlider(
            "Emergency urgency", "EmergencyUrgency", ref emergencyUrgency, 0.80f, 1.30f, "%.2fx",
            "Scales purposeful evacuation urgency and desired movement speed. Higher values make evacuation more brisk, subject to the existing maximum-speed and collision guards.");
        changed |= DrawFullWidthSlider(
            "Max agent running speed (m/s)", "MaxRunningSpeed", ref maxRunningSpeed,
            BehaviorTuningConfig.MinimumRunningSpeedLimitMetersPerSecond,
            BehaviorTuningConfig.MaximumRunningSpeedLimitMetersPerSecond, "%.2f m/s",
            "Maximum self-propelled speed in both scenarios. This is a ceiling, not a target: individual pace, injuries, fumes and congestion can make agents slower. It does not change Simulation speed or blast throw displacement. Ctrl+click the slider to type a value. Set before finalising.");
        changed |= DrawFullWidthSlider(
            "Helping tendency", "HelpingTendency", ref helping, 0f, 1f, "%.2f",
            "Sets the average willingness to assist a nearby fallen, incapacitated or crawling casualty when conditions allow. Helpers approach, pause beside the casualty for about two seconds, then resume evacuation; assistance does not medically recover the casualty.");
        changed |= DrawFullWidthSlider(
            "Route familiarity", "RouteFamiliarity", ref familiarity, 0f, 1f, "%.2f",
            "Represents how well agents know available exits. Low familiarity relies more on visible exits; high familiarity makes agents more willing to route toward exits that are not currently visible.");

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.45f, 0.78f, 1f, 1f), "BOTTLENECK INTERACTION");
        changed |= DrawFullWidthSlider(
            "Bottleneck assertiveness", "BottleneckAssertiveness", ref bottleneckAssertiveness, 0f, 1f, "%.2f",
            "Controls how assertively agents respond at a congested doorway. Low values favor yielding; high values more often seek gaps or apply bounded forward pressure.");
        changed |= DrawFullWidthSlider(
            "Pressure sensitivity", "PressureSensitivity", ref bottleneckSensitivity, 0.50f, 1.50f, "%.2fx",
            "Scales how quickly local density, delay and doorway load are interpreted as bottleneck pressure. Higher values trigger bottleneck responses earlier and more strongly.");
        changed |= DrawFullWidthSlider(
            "Pressure space compression", "PressureSpaceCompression", ref bottleneckCompression, 0f, 0.35f, "%.2f",
            "Reduces preferred social spacing when bottleneck pressure rises. Higher values allow tighter queues near exits while physical collision remains enforced.");

        if (changed)
        {
            tuning.SetMaxRunningSpeed(maxRunningSpeed);
            tuning.SetValues(
                traitVariation, reactionVariation, stressSusceptibility, stressPersistence,
                crowdFollowing, personalSpace, panicCompression, emergencyUrgency,
                helping, familiarity, bottleneckAssertiveness, bottleneckSensitivity,
                bottleneckCompression);
            _scenarioDirty = true;
            _statusMessage = "Behaviour tuning updated. Values apply when the scene is finalised.";
        }

        if (ImGui.Button("Reset Behaviour Defaults", new Vector2(-1f, 0f)))
        {
            tuning.Reset();
            _scenarioDirty = true;
            _statusMessage = "Behaviour tuning restored to validated defaults.";
        }

        ImGui.TextWrapped("Higher stress persistence means slower recovery.");
        ImGui.TextWrapped("Panic and bottleneck compression reduce social spacing only; physical collision guards remain active.");
        ImGui.TextWrapped("Doorway pressure can produce yielding, lateral gap seeking, or bounded pressing without panic.");
        ImGui.TextWrapped("Behaviour values are saved with the scenario JSON.");
        ImGui.Spacing();
    }

    private static bool DrawFullWidthSlider(
        string label,
        string id,
        ref float value,
        float minimum,
        float maximum,
        string format,
        string tooltip)
    {
        ImGui.PushID(id);
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        ImGui.TextDisabled("[?]");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
            ImGui.TextUnformatted(tooltip);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        ImGui.SetNextItemWidth(-1f);
        bool changed = ImGui.SliderFloat("##Value", ref value, minimum, maximum, format);
        ImGui.PopID();
        return changed;
    }

    private void DrawRuntimeControls(Simulation simulation)
    {
        float speed = _simulationSpeed;
        ImGui.Text("Playback speed");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderFloat("##PlaybackSpeed", ref speed, 0.25f, 4f, "%.2fx"))
        {
            _simulationSpeed = Math.Clamp(speed, 0.25f, 4f);
        }

        ImGui.Spacing();
        ImGui.Text("WORLD TOOL");
        DrawActiveButton(
            "Select Agent",
            _worldMode == WorldInteractionMode.SelectAgent,
            () =>
            {
                _worldMode = WorldInteractionMode.SelectAgent;
                _statusMessage = "Select Agent active. Click a humanoid in the 3D world.";
            });

        if (simulation.ScenarioType == ScenarioType.Explosion)
        {
            bool explosionAvailable = simulation.Running && !simulation.Environment.HasExplosion();
            if (!explosionAvailable)
            {
                ImGui.BeginDisabled();
            }
    
            bool explosionActive = _worldMode == WorldInteractionMode.PlaceExplosion;
            bool explosionUnused = !simulation.Environment.HasExplosion();
    
            // This is the key event trigger for the crowd-dispersion scenario, so make it
            // visually discoverable instead of blending in with ordinary world-tool buttons.
            if (explosionActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.82f, 0.22f, 0.06f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.96f, 0.31f, 0.08f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 0.38f, 0.10f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            }
            else if (explosionUnused)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.94f, 0.72f, 0.10f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.82f, 0.18f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.91f, 0.58f, 0.05f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.10f, 0.09f, 0.05f, 1f));
            }
    
            string explosionButtonLabel = explosionActive
                ? "Explosion Armed - Click World"
                : "Place Explosion";
            if (ImGui.Button(explosionButtonLabel, new Vector2(-1f, 34f)))
            {
                _worldMode = WorldInteractionMode.PlaceExplosion;
                _statusMessage = "Explosion tool armed. Click clear floor once to trigger crowd dispersion.";
            }
    
            if (explosionActive || explosionUnused)
            {
                ImGui.PopStyleColor(4);
            }
    
            if (explosionUnused && !explosionActive)
            {
                DrawMutedText("Start the run, then place an explosion in the scene to trigger crowd response.");
            }
    
            if (!explosionAvailable)
            {
                ImGui.EndDisabled();
                DrawMutedText(simulation.Environment.HasExplosion()
                    ? "An explosion has already been placed in this run."
                    : "Start the simulation before placing an explosion.");
            }
    
        }
        else
        {
            DrawToxicFumeRuntimeEventControls(simulation);
        }

        if (SelectedAgentId is not null && DrawNeutralButton("Clear Agent Selection", new Vector2(-1f, 0f)))
        {
            ClearSelectionAndCamera("Agent selection cleared.");
        }

    }

    private void DrawScenarioPersistenceControls(Simulation simulation, bool showHeading = true)
    {
        if (showHeading)
        {
            ImGui.Text("SCENARIO SAVE / LOAD");
        }

        DrawMutedText("Saves the authored layout and starting positions for a new run.");
        ImGui.TextColored(
            _scenarioDirty ? new Vector4(1f, 0.70f, 0.22f, 1f) : new Vector4(0.35f, 0.82f, 0.50f, 1f),
            _scenarioDirty ? "Unsaved changes: Yes" : "Unsaved changes: No");

        ImGui.Text("Scenario name");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##ScenarioName", ref _scenarioName, (uint)64);
        if (ImGui.Button("Save / Save As", new Vector2(-1f, 0f)))
        {
            _scenarioSaveRequested = string.IsNullOrWhiteSpace(_scenarioName)
                ? "scenario"
                : _scenarioName.Trim();
        }

        ImGui.Spacing();
        ImGui.Text("Saved scenarios");
        string preview = _selectedScenarioIndex >= 0 && _selectedScenarioIndex < _savedScenarioNames.Length
            ? _savedScenarioNames[_selectedScenarioIndex]
            : "<no saved scenario selected>";
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##SavedScenarioCombo", preview))
        {
            for (int index = 0; index < _savedScenarioNames.Length; index++)
            {
                bool selected = index == _selectedScenarioIndex;
                if (ImGui.Selectable(_savedScenarioNames[index], selected))
                {
                    _selectedScenarioIndex = index;
                    _scenarioName = _savedScenarioNames[index];
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        bool canLoad = !simulation.Running &&
            _selectedScenarioIndex >= 0 &&
            _selectedScenarioIndex < _savedScenarioNames.Length;
        if (!canLoad)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Load Selected Scenario", new Vector2(-1f, 0f)))
        {
            _scenarioLoadRequested = _savedScenarioNames[_selectedScenarioIndex];
        }

        if (!canLoad)
        {
            ImGui.EndDisabled();
        }

        if (ImGui.Button("Refresh Scenario List", new Vector2(-1f, 0f)))
        {
            RefreshScenarioList();
            _statusMessage = "Saved-scenario list refreshed.";
        }

        if (simulation.Running)
        {
            DrawMutedText("Pause before loading a scenario. Saving is still allowed.");
        }
        else
        {
            DrawMutedText($"Folder: {ScenarioFileStore.DefaultDirectory}");
        }
    }

    private void DrawCameraControls(WorldRenderer renderer)
    {
        if (_povEnabled)
        {
            ImGui.TextColored(new Vector4(0.25f, 0.85f, 1f, 1f), "AGENT POV ACTIVE");
            ImGui.TextWrapped("Head-height camera follows the selected agent's movement heading until the agent exits or you leave POV.");
            if (ImGui.Button("Exit Agent POV##CameraControls", new Vector2(-1f, 30f)))
            {
                _povEnabled = false;
                _followSelectedAgent = true;
                _statusMessage = "Exited Agent POV. Follow camera remains active.";
            }

            return;
        }

        if (_followSelectedAgent)
        {
            ImGui.TextColored(new Vector4(0.30f, 0.90f, 0.55f, 1f), "FOLLOW CAMERA ACTIVE");
        }

        float azimuth = renderer.Camera.AzimuthDegrees;
        ImGui.Text("Rotation (azimuth)");
        if (ImGui.SliderFloat("##Azimuth", ref azimuth, -180f, 180f, "%.0f deg"))
        {
            renderer.Camera.SetAzimuth(azimuth);
        }

        float elevation = renderer.Camera.ElevationDegrees;
        ImGui.Text("Viewing angle (elevation)");
        if (ImGui.SliderFloat("##Elevation", ref elevation, 18f, 70f, "%.0f deg"))
        {
            renderer.Camera.SetElevation(elevation);
        }

        float zoom = renderer.Camera.Zoom;
        ImGui.Text("Zoom");
        if (ImGui.SliderFloat("##Zoom", ref zoom, 0.45f, 2.5f, "%.2fx"))
        {
            renderer.Camera.SetZoom(zoom);
        }

        if (ImGui.Button("Reset Camera", new Vector2(-1f, 0f)))
        {
            renderer.Camera.Reset();
            _followSelectedAgent = false;
            _povEnabled = false;
            _statusMessage = "Camera reset to the default isometric view.";
        }

        DrawMutedText("Mouse wheel over the world also zooms.");
    }

    private void DrawDebugControls(Simulation simulation)
    {
        if (!ImGui.CollapsingHeader("DEBUG VISUALISATION"))
        {
            return;
        }

        Toggle("Desired direction", _debug.ShowDesiredDirection, value => _debug.ShowDesiredDirection = value);
        Toggle("Avoidance direction", _debug.ShowAvoidanceDirection, value => _debug.ShowAvoidanceDirection = value);
        Toggle("Herd / lane direction", _debug.ShowHerdDirection, value => _debug.ShowHerdDirection = value);
        Toggle("Total SFM force", _debug.ShowSfmForce, value => _debug.ShowSfmForce = value);
        if (simulation.ScenarioType == ScenarioType.Explosion)
        {
            Toggle("Blast direction", _debug.ShowBlastDirection, value => _debug.ShowBlastDirection = value);
        }
        Toggle("Vision cone", _debug.ShowVisionCone, value => _debug.ShowVisionCone = value);
        Toggle("Congestion heat", _debug.ShowCongestion, value => _debug.ShowCongestion = value);
        Toggle("Trajectory trails", _debug.ShowTrails, value => _debug.ShowTrails = value);
        ImGui.TextWrapped("These overlays help inspect movement and do not change the simulation.");
    }

    private static void DrawScenarioStatus(Simulation simulation)
    {
        ImGui.Text("SCENARIO STATUS");
        KeyValue("Scenario", simulation.ScenarioType == ScenarioType.Explosion ? "Explosion" : "Toxic Fume");
        KeyValue("Mode", simulation.Agents.Count == 0 ? "Builder" : "Simulation");
        KeyValue(
            "Agents",
            simulation.Agents.Count > 0
                ? simulation.Agents.Count.ToString()
                : $"{simulation.Environment.PlannedAgentCount} planned");
        KeyValue("Exits", simulation.Environment.Exits.Count.ToString());
        KeyValue("Square blockages", simulation.Environment.Blockages.Count.ToString());
        KeyValue("Circular blockages", simulation.Environment.CircularBlockages.Count.ToString());
        KeyValue("Blue walls", simulation.Environment.BlueWalls.Count.ToString());
        KeyValue("Room", $"{simulation.Environment.Width:F1} m x {simulation.Environment.Height:F1} m");
        KeyValue("Seed", simulation.RandomSeed.ToString());
        KeyValue("Frame", simulation.FrameIndex.ToString());
        KeyValue("Simulated time", $"{simulation.FrameIndex * simulation.Config.Agent.Dt:F1} s");
        KeyValue("Run state", simulation.Running ? "RUNNING" : "PAUSED");
    }

    private void DrawSelectedAgent(Simulation simulation)
    {
        if (SelectedAgentId is null)
        {
            return;
        }

        Agent? selected = simulation.Agents.FirstOrDefault(agent => agent.AgentId == SelectedAgentId.Value);
        if (selected is null)
        {
            SelectedAgentId = null;
            return;
        }

        ImGui.Text($"SELECTED AGENT {selected.AgentId}");
        KeyValue("State", selected.State.ToString());
        KeyValue("Posture", selected.Posture == SeatingPosture.StandingUp && selected.StandUpProgress >= 1f
            ? "Standing - waiting for a gap" : selected.Posture.ToString());
        KeyValue("Behaviour", selected.Intent.ToString());
        KeyValue("Panic", selected.PanicLevel.ToString("F2"));
        KeyValue("Position", $"({selected.Position.X:F1} m, {selected.Position.Y:F1} m)");
        KeyValue("Desired speed", $"{selected.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds:F2} m/s");
        KeyValue("Actual speed", $"{selected.VelocityMetersPerSecond.Length():F2} m/s");
        KeyValue("Strategy", selected.Strategy.ToString());
        KeyValue("Exit", selected.ExitTarget?.Name ?? "None");
        KeyValue("Visibility", selected.VisibilityFactor.ToString("F2"));
        KeyValue("Local congestion", selected.LocalCongestion.ToString("F2"));
        KeyValue("Threat stimulus", selected.LastThreatPanicStimulus.ToString("F2"));
        KeyValue("Social stimulus", selected.LastSocialPanicStimulus.ToString("F2"));
        KeyValue("Running stimulus", selected.LastRunningPanicStimulus.ToString("F2"));
        KeyValue("Evac awareness", selected.LastEvacuationAwarenessStimulus.ToString("F2"));
        KeyValue("Active alarm", selected.IsActiveAlarmSource(simulation.FrameIndex) ? "YES" : "No");
        if (simulation.ScenarioType == ScenarioType.ToxicFume)
        {
            KeyValue("Fume status", selected.ToxicExposureStatus.ToString());
            KeyValue("Current concentration", selected.CurrentToxicConcentration.ToString("F2", CultureInfo.InvariantCulture));
            KeyValue("Peak concentration", selected.PeakToxicConcentration.ToString("F2", CultureInfo.InvariantCulture));
            KeyValue("Cumulative exposure", selected.CumulativeToxicExposure.ToString("F2", CultureInfo.InvariantCulture));
            KeyValue("Mobility factor", selected.ToxicMobilityFactor.ToString("F2", CultureInfo.InvariantCulture));
        }

        int routeRemaining = Math.Max(0, selected.RouteWaypoints.Count - selected.RouteWaypointIndex);
        KeyValue("Route waypoints", routeRemaining.ToString());
        if (selected.CurrentRouteWaypoint is Vector2 waypoint)
        {
            KeyValue("Next waypoint", $"({waypoint.X:F1} m, {waypoint.Y:F1} m)");
        }

        KeyValue("Stuck time", $"{selected.TimeStuck:F1} s");
        if (simulation.Config.Agent.NaturalNavigationEnabled &&
            simulation.ScenarioType is ScenarioType.Explosion or ScenarioType.ToxicFume)
        {
            KeyValue("Adaptive navigation", selected.NavigationMemory.Stage.ToString());
            KeyValue("No useful progress", $"{selected.NavigationMemory.NoProgressSeconds:F1} s");
            KeyValue("Recovery attempts", selected.NavigationMemory.TotalRecoveryAttempts.ToString());
            KeyValue("Detours completed", selected.NavigationMemory.SuccessfulDetours.ToString());
            KeyValue("Exit reconsiderations", selected.NavigationMemory.ExitChanges.ToString());
            if (selected.NavigationMemory.DetourGoal is Vector2 detour)
                KeyValue("Temporary escape", $"({detour.X:F1} m, {detour.Y:F1} m)");
        }
        if (selected.LocalNavigationMode != LocalNavigationMode.Free)
        {
            string localMode = selected.LocalNavigationMode switch
            {
                LocalNavigationMode.CasualtyBypass => "Casualty bypass",
                LocalNavigationMode.WaitingForGap => "Waiting for gap",
                LocalNavigationMode.SeekingGap => "Seeking wider gap",
                LocalNavigationMode.PressingThrough => "Controlled press",
                LocalNavigationMode.CircleFollow => "Following circular obstacle",
                _ => selected.LocalNavigationMode.ToString(),
            };
            KeyValue("Local navigation", localMode);

            if (selected.LocalNavigationMode is
                LocalNavigationMode.CasualtyBypass or
                LocalNavigationMode.WaitingForGap or
                LocalNavigationMode.SeekingGap or
                LocalNavigationMode.PressingThrough)
            {
                if (selected.LocalNavigationBlockerAgentId is int localBlockerId)
                {
                    KeyValue("Blocked by", $"Casualty {localBlockerId}");
                }

                if (selected.LocalNavigationBlockedSinceFrame >= 0)
                {
                    float blockedSeconds = Math.Max(
                        0,
                        simulation.FrameIndex - selected.LocalNavigationBlockedSinceFrame) *
                        simulation.Config.Agent.Dt;
                    KeyValue("Blocked time", $"{blockedSeconds:F1} s");
                }
            }
            else if (selected.LocalNavigationMode == LocalNavigationMode.CircleFollow)
            {
                KeyValue("Obstacle", "Circular blockage");
                KeyValue(
                    "Follow side",
                    selected.CircleFollowDirection >= 0 ? "Counter-clockwise" : "Clockwise");
            }
        }

        if (selected.SlowObstacleBypassGoal is Vector2 bypassGoal &&
            selected.SlowObstacleBypassAgentId is int bypassAgentId &&
            simulation.FrameIndex <= selected.SlowObstacleBypassUntilFrame)
        {
            string blockerLabel = selected.Intent == BehaviorIntent.ThreatEscape
                ? "Casualty"
                : "Crawler";
            KeyValue("Local bypass", $"{blockerLabel} {bypassAgentId}");
            KeyValue("Bypass goal", $"({bypassGoal.X:F1} m, {bypassGoal.Y:F1} m)");
        }
        else if (selected.SlowObstacleCandidateAgentId is int observedBlockerId)
        {
            string blockerLabel = selected.Intent == BehaviorIntent.ThreatEscape
                ? "casualty"
                : "crawler";
            KeyValue("Local bypass", $"Watching {blockerLabel} {observedBlockerId}");
        }

        KeyValue("Follow tendency", selected.FollowTendency.ToString("F2"));
        KeyValue("Helping tendency", selected.HelpingTendency.ToString("F2"));
        if (selected.Intent == BehaviorIntent.AssistCasualty && selected.AssistTargetAgentId is int assistTargetId)
        {
            bool attending = selected.AssistPauseUntilFrame >= 0 &&
                             simulation.FrameIndex < selected.AssistPauseUntilFrame;
            string phase = attending ? "HELPING (at casualty)" : "GOING TO HELP";
            Agent? assistTarget = simulation.Agents.FirstOrDefault(candidate => candidate.AgentId == assistTargetId);
            KeyValue("Target casualty", $"Agent {assistTargetId}");
            KeyValue("Assist phase", phase);
            if (!attending)
            {
                KeyValue("Assist position", selected.AssistSlotIndex switch
                {
                    0 => "Side A",
                    1 => "Side B",
                    _ => "Unassigned",
                });
                KeyValue("Recovery", selected.AssistRecoveryStage switch
                {
                    1 => "Route retry",
                    2 => "Alternate side tried",
                    _ => "Normal approach",
                });
            }

            if (assistTarget is not null)
            {
                float distance = Vector2.Distance(selected.Position, assistTarget.Position);
                KeyValue("Distance to casualty", $"{distance:F2} m");
            }

            if (!attending && selected.AssistAssignedFrame >= 0)
            {
                float approachSeconds = Math.Max(0, simulation.FrameIndex - selected.AssistAssignedFrame) *
                    simulation.Config.Agent.Dt;
                KeyValue("Approach time", $"{approachSeconds:F1} s");
            }
            else if (attending && selected.AssistAssignedFrame >= 0 && selected.AssistLastProgressFrame >= 0)
            {
                float approachDuration = Math.Max(0, selected.AssistLastProgressFrame - selected.AssistAssignedFrame) *
                    simulation.Config.Agent.Dt;
                KeyValue("Approach took", $"{approachDuration:F1} s");
            }

            if (!attending && selected.AssistLastProgressFrame >= 0)
            {
                float noProgressSeconds = Math.Max(0, simulation.FrameIndex - selected.AssistLastProgressFrame) *
                    simulation.Config.Agent.Dt;
                KeyValue("No-progress time", $"{noProgressSeconds:F1} s");
                float abandonRemaining = Math.Max(0f,
                    simulation.Config.Agent.AssistanceAbandonNoProgressSeconds - noProgressSeconds);
                KeyValue("Give-up guard", $"{abandonRemaining:F1} s remaining");
            }
            else if (attending)
            {
                float remainingSeconds = Math.Max(0, selected.AssistPauseUntilFrame - simulation.FrameIndex) *
                    simulation.Config.Agent.Dt;
                KeyValue("Helping time left", $"{remainingSeconds:F1} s");
            }
        }

        bool selectedCanReceiveHelp = selected.IsActive && selected.State is
            AgentState.Fallen or AgentState.TempIncap or AgentState.Incapacitated or AgentState.Crawling;
        if (selectedCanReceiveHelp)
        {
            int helperCount = simulation.Agents.Count(helper =>
                helper.IsActive &&
                helper.Intent == BehaviorIntent.AssistCasualty &&
                helper.AssistTargetAgentId == selected.AgentId);
            int helpersAtCasualty = simulation.Agents.Count(helper =>
                helper.IsActive &&
                helper.Intent == BehaviorIntent.AssistCasualty &&
                helper.AssistTargetAgentId == selected.AgentId &&
                helper.AssistPauseUntilFrame >= 0 &&
                simulation.FrameIndex < helper.AssistPauseUntilFrame);
            KeyValue("Helpers assigned", $"{helperCount}/{simulation.Config.Agent.AssistanceMaxHelpersPerCasualty}");
            KeyValue("Helpers at casualty", helpersAtCasualty.ToString());
        }

        KeyValue("Stress susceptibility", selected.ThreatSensitivity.ToString("F2"));
        KeyValue("Stress recovery bias", selected.StressRecoveryBias.ToString("F2"));
        KeyValue("Personal-space bias", selected.PersonalSpaceBias.ToString("F2"));
        KeyValue("Urgency bias", selected.UrgencyBias.ToString("F2"));
        KeyValue("Assertiveness", selected.Assertiveness.ToString("F2"));
        KeyValue("Patience bias", selected.PatienceBias.ToString("F2"));
        KeyValue("Bottleneck mode", selected.BottleneckMode.ToString());
        KeyValue("Bottleneck pressure", selected.BottleneckPressure.ToString("F2"));
        KeyValue("Doorway wait", $"{selected.BottleneckWaitSeconds:F1} s");
        ImGui.ProgressBar(Math.Clamp(selected.PanicLevel, 0f, 1f), new Vector2(-1f, 0f), "Panic level");
        ImGui.ProgressBar(
            Math.Clamp(selected.BottleneckPressure, 0f, 1f),
            new Vector2(-1f, 0f),
            "Bottleneck pressure");
        ImGui.Spacing();

        string followLabel = _followSelectedAgent && !_povEnabled
            ? "Stop Following Agent##SelectedAgentFollow"
            : "Follow Selected Agent##SelectedAgentFollow";
        if (ImGui.Button(followLabel, new Vector2(-1f, 0f)))
        {
            if (_povEnabled)
            {
                _povEnabled = false;
                _followSelectedAgent = true;
            }
            else
            {
                _followSelectedAgent = !_followSelectedAgent;
            }

            if (_followSelectedAgent)
            {
                _worldMode = WorldInteractionMode.SelectAgent;
            }

            _statusMessage = _followSelectedAgent
                ? $"Following agent {selected.AgentId}. You can enter its first-person POV below."
                : "Follow camera disabled.";
        }

        if (_povEnabled)
        {
            DrawMutedText("POV active. Use Exit Agent POV on the Camera page to return.");
        }
        else if (ImGui.Button("Enter Agent POV##SelectedAgentPov", new Vector2(-1f, 0f)))
        {
            _povEnabled = true;
            _followSelectedAgent = true;
            _worldMode = WorldInteractionMode.SelectAgent;
            _statusMessage =
                $"Agent POV active for agent {selected.AgentId}. It will end automatically when the agent exits.";
        }

        if (ImGui.Button("Clear Selection##SelectedAgentClear", new Vector2(-1f, 0f)))
        {
            ClearSelectionAndCamera("Agent selection cleared.");
        }
    }

    private static void DrawAgentLegend()
    {
        if (!ImGui.CollapsingHeader("AGENT COLOUR LEGEND"))
        {
            return;
        }

        ImGui.TextColored(new Vector4(0.12f, 0.88f, 0.22f, 1f), "Green  - Evacuating");
        ImGui.TextColored(new Vector4(0.96f, 0.65f, 0.10f, 1f), "Amber  - Waiting / reaction freeze");
        ImGui.TextColored(new Vector4(0.08f, 0.30f, 0.95f, 1f), "Blue   - Injured");
        ImGui.TextColored(new Vector4(0.96f, 0.90f, 0.08f, 1f), "Yellow - Crawling");
        ImGui.TextColored(new Vector4(0.96f, 0.05f, 0.04f, 1f), "Red    - Fallen");
        ImGui.TextColored(new Vector4(0.55f, 0.12f, 0.12f, 1f), "Dark red - Incapacitated / temporary incapacity");
        ImGui.TextColored(new Vector4(0.35f, 0.35f, 0.35f, 1f), "Dark grey - Fatal casualty");
        ImGui.TextColored(new Vector4(0.90f, 0.90f, 0.86f, 1f), "White  - Wandering / normal");
        DrawMutedText("Body colour represents the agent's state.");
        ImGui.TextColored(new Vector4(1f, 0.24f, 0.03f, 1f), "Red/orange ground ring = panic level");
        ImGui.TextColored(new Vector4(0.96f, 0.98f, 1f, 1f), "GOING TO HELP = approaching assigned casualty");
        ImGui.TextColored(new Vector4(0.96f, 0.98f, 1f, 1f), "White cross + HELPING = in the timed assistance phase");
    }

    private static void DrawModelSummary(Simulation simulation)
    {
        if (!ImGui.CollapsingHeader("PANIC / SFM MODEL"))
        {
            return;
        }

        KeyValue("SFM A", simulation.Config.Agent.SfmA.ToString("F0"));
        KeyValue("SFM B", $"{simulation.Config.Agent.SfmB:F2} m");
        KeyValue("Repulsion soft cap", "6000");
        KeyValue("Acceleration cap", "1.21 (metric-scaled)");
        KeyValue("Agent running ceiling", $"{simulation.Config.Agent.Behavior.MaxRunningSpeedMetersPerSecond:F2} m/s");
        KeyValue("Blast velocity guard", $"{simulation.Config.Agent.MaxSpeed / AgentConfig.ReferenceStepSeconds:F2} m/s");
        KeyValue("Last tick CPU", $"{simulation.LastStepMilliseconds:F1} ms");
        KeyValue("Furniture CPU", $"{simulation.LastFurnitureMilliseconds:F1} ms");
        KeyValue("Route planning CPU", $"{simulation.LastRoutePlanningMilliseconds:F1} ms ({simulation.LastRoutePlanRequests} searches)");
        KeyValue("Panic speed gain", simulation.Config.Agent.PanicSpeedGain.ToString("F1"));
        KeyValue("Evac speed floor", $"{simulation.Config.Agent.EvacuationSpeedFloorMultiplier:F2}x");
        KeyValue("Threat gain", simulation.Config.Agent.PanicKThreat.ToString("F1"));
        KeyValue("Social contagion", simulation.Config.Agent.PanicKSocial.ToString("F2"));
        KeyValue("Running contagion", simulation.Config.Agent.PanicKRunning.ToString("F2"));
        KeyValue("Panic decay tau", $"{simulation.Config.Agent.PanicDecayTimeConstantSeconds:F1} s");
        KeyValue("Stress persistence", $"{simulation.Config.Agent.Behavior.StressPersistence:F2}x");
        KeyValue("Personal space", $"{simulation.Config.Agent.Behavior.PersonalSpace:F2}x");
        KeyValue("Panic compression", simulation.Config.Agent.Behavior.PanicSpaceCompression.ToString("F2"));
        KeyValue("Emergency urgency", $"{simulation.Config.Agent.Behavior.EmergencyUrgency:F2}x");
        KeyValue("Bottleneck assertiveness", simulation.Config.Agent.Behavior.BottleneckAssertiveness.ToString("F2"));
        KeyValue("Pressure sensitivity", $"{simulation.Config.Agent.Behavior.BottleneckPressureSensitivity:F2}x");
        KeyValue("Pressure compression", simulation.Config.Agent.Behavior.BottleneckSpaceCompression.ToString("F2"));
        KeyValue("Max herding", simulation.Config.Agent.HerdingLambdaMax.ToString("F2"));
        KeyValue("Route commitment", $"{simulation.Config.Agent.RouteCommitmentSeconds:F1} s");
        ImGui.TextWrapped("Acute stress is one bounded modifier, not the master evacuation switch. Direct/social alarm cues raise panic and time-based decay settles it after cues disappear. Purposeful evacuation remains brisk at low panic. At genuine exit bottlenecks, density, delayed progress, urgency and individual assertiveness can produce yielding, lateral gap seeking, or bounded forward pressing even after panic has decayed. Static blockage routing uses body-clear A* waypoints; SFM and physical contact guards remain authoritative.");
    }

    private void ValidateSelectedAgent(Simulation simulation)
    {
        if (SelectedAgentId is not int selectedId)
        {
            _followSelectedAgent = false;
            _povEnabled = false;
            return;
        }

        Agent? selected = simulation.Agents.FirstOrDefault(agent => agent.AgentId == selectedId);
        if (selected is not null && selected.IsActive)
        {
            return;
        }

        bool wasPov = _povEnabled;
        bool wasFollowing = _followSelectedAgent;
        SelectedAgentId = null;
        _followSelectedAgent = false;
        _povEnabled = false;

        _statusMessage = wasPov
            ? $"Agent {selectedId} exited. Agent POV ended automatically."
            : wasFollowing
                ? $"Agent {selectedId} exited. Follow camera ended automatically."
                : $"Agent {selectedId} exited. Selection cleared.";
    }

    private void RefreshScenarioList(string? preferName = null)
    {
        try
        {
            string previousSelection = _selectedScenarioIndex >= 0 &&
                _selectedScenarioIndex < _savedScenarioNames.Length
                    ? _savedScenarioNames[_selectedScenarioIndex]
                    : string.Empty;
            string target = preferName ?? previousSelection;

            _savedScenarioNames = ScenarioFileStore.ListScenarioNames().ToArray();
            _selectedScenarioIndex = Array.FindIndex(
                _savedScenarioNames,
                name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
            if (_selectedScenarioIndex < 0 && _savedScenarioNames.Length > 0)
            {
                _selectedScenarioIndex = 0;
            }
        }
        catch
        {
            _savedScenarioNames = Array.Empty<string>();
            _selectedScenarioIndex = -1;
        }
    }

    private void ClearSelectionAndCamera(string statusMessage)
    {
        SelectedAgentId = null;
        _followSelectedAgent = false;
        _povEnabled = false;
        _statusMessage = statusMessage;
    }

    private void HandleWorldInput(Simulation simulation, WorldRenderer renderer)
    {
        if (!renderer.TryMouseToWorld(out Vector2 world) || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (_mode == AppMode.Builder)
        {
            HandleBuilderClick(simulation, world);
            return;
        }

        if (_worldMode == WorldInteractionMode.PlaceExplosion)
        {
            if (!simulation.Running)
            {
                _worldMode = WorldInteractionMode.SelectAgent;
                _statusMessage = "Explosion rejected: start the simulation first.";
                return;
            }

            if (simulation.Environment.HasExplosion())
            {
                _worldMode = WorldInteractionMode.SelectAgent;
                _statusMessage = "Explosion rejected: the one-explosion guardrail is already active.";
                return;
            }

            if (!IsInsideWorld(simulation, world) ||
                simulation.Environment.IsPointBlocked(world, simulation.Config.Agent.Radius))
            {
                _statusMessage = "Explosion rejected: click clear floor inside the room, not a wall/blockage.";
                return;
            }

            simulation.TriggerExplosion(world.X, world.Y);
            _worldMode = WorldInteractionMode.SelectAgent;
            _statusMessage = simulation.Environment.HasExplosion()
                ? $"Explosion triggered at ({world.X:F1} m, {world.Y:F1} m)."
                : "Explosion placement was rejected.";
            return;
        }

        float selectionRadius = Math.Max(simulation.Config.Agent.Radius * 2.3f, renderer.AgentVisualWidth);
        Agent? selected = simulation.Agents
            .Where(agent => agent.IsActive && Vector2.Distance(agent.Position, world) <= selectionRadius)
            .OrderBy(agent => Vector2.DistanceSquared(agent.Position, world))
            .FirstOrDefault();

        if (selected is null)
        {
            ClearSelectionAndCamera("No agent selected. Click closer to a humanoid.");
            return;
        }

        SelectedAgentId = selected.AgentId;
        // Selection navigates the inspector without overriding the user's hidden-panel choice.
        _inspector.SelectTab(InspectorTab.Agent);
        _statusMessage = $"Selected agent {selected.AgentId}. Use the Agent inspector for details, follow and POV controls.";
    }

    private void HandleBuilderClick(Simulation simulation, Vector2 world)
    {
        float x = world.X;
        float y = world.Y;
        bool created;

        switch (_builderTool)
        {
            case BuilderTool.Exit:
                created = simulation.Environment.AddExitFromClick(x, y, _exitPersonsWide);
                _statusMessage = created
                    ? $"{_exitPersonsWide}-person exit placed."
                    : "Exit rejected: click close to a boundary wall and avoid overlaps.";
                break;

            case BuilderTool.Blockage:
                created = simulation.Environment.AddBlockageFromClick(x, y, _assetSelection.BlockageCells);
                _statusMessage = created
                    ? $"{_assetSelection.BlockageCells * simulation.Config.Grid.CellSizeMeters:F1} m square blockage placed on the metric grid."
                    : $"{_assetSelection.BlockageCells * simulation.Config.Grid.CellSizeMeters:F1} m square blockage rejected by room/overlap guardrails.";
                break;

            case BuilderTool.CircularBlockage:
                created = simulation.Environment.AddCircularBlockageFromClick(x, y);
                _statusMessage = created
                    ? "Circular blockage placed: Ø 2.5 m."
                    : "Circular blockage rejected by room/overlap guardrails.";
                break;

            case BuilderTool.RoundTableSet:
            case BuilderTool.SingleChair:
            case BuilderTool.RectangularTable:
            case BuilderTool.Stage:
            case BuilderTool.Barricade:
            {
                FurnitureKind kind = _builderTool switch
                {
                    BuilderTool.RoundTableSet => FurnitureKind.RoundTableSet,
                    BuilderTool.SingleChair => FurnitureKind.SingleChair,
                    BuilderTool.RectangularTable => FurnitureKind.RectangularTable,
                    BuilderTool.Stage => FurnitureKind.Stage,
                    _ => FurnitureKind.Barricade,
                };
                bool stageLimitReached = kind == FurnitureKind.Stage && simulation.Environment.HasStage;
                Vector2 center = kind == FurnitureKind.Barricade && _assetSelection.SnapBarricadeEnds
                    ? simulation.Environment.SnapBarricadePlacement(world, _assetSelection.RotationRadians)
                    : world;
                created = simulation.Environment.AddFurniture(kind, center, _assetSelection.RotationRadians);
                // Empty assets must not reset a manually selected fallback population count.
                if (created && FurnitureSet.GetSeatCount(kind) > 0)
                    _agentCount = simulation.Environment.PlannedAgentCount;
                _statusMessage = created
                    ? FurnitureSet.IsRectangular(kind)
                        ? $"{(kind == FurnitureKind.Stage ? "Stage" : kind == FurnitureKind.Barricade ? "Barricade" : "Table")} placed. No occupants added. Ctrl+Z to undo."
                        : $"Furniture placed. Total occupants: {_agentCount}. Ctrl+Z to undo."
                    : stageLimitReached
                        ? "Only one stage is allowed per scenario. Undo its placement before placing another."
                        : "Not enough clear floor. Move away from walls, exits, furniture and people.";
                break;
            }
            case BuilderTool.Agents:
                created = simulation.Environment.AddAgentSpawnGroupFromClick(x, y, _agentPlacementCount);
                if (created)
                {
                    _agentCount = simulation.Environment.PlannedAgentCount;
                }

                _statusMessage = created
                    ? $"Placed {_agentPlacementCount} agents around this region. Planned population: {_agentCount}."
                    : $"Could not fit {_agentPlacementCount} agents around that region without overlapping walls or planned agents.";
                break;

            case BuilderTool.ToxicFumeSource:
                created = simulation.ScenarioType == ScenarioType.ToxicFume &&
                    simulation.ToxicFumeSystem.TrySetSource(simulation, world);
                _statusMessage = created
                    ? $"Toxic-fume release point set at ({x:F1} m, {y:F1} m). Compute the dispersion before finalising."
                    : "Release point rejected: click clear floor inside the room, away from walls/blockages.";
                break;

            default:
                created = false;
                _statusMessage = "Builder tool is not available.";
                break;
        }

        if (created)
        {
            bool geometryChanged = _builderTool is
                BuilderTool.Exit or BuilderTool.Blockage or BuilderTool.CircularBlockage or BuilderTool.RoundTableSet or
                BuilderTool.SingleChair or BuilderTool.RectangularTable or BuilderTool.Stage or BuilderTool.Barricade;
            if (geometryChanged)
            {
                simulation.BuildWallGrid();
                InvalidateToxicFumeForGeometryChange(simulation);
            }

            _scenarioDirty = true;
        }
    }

    /// <summary>
    /// Handle a Ctrl+Z request detected by the native Silk.NET input layer.
    /// The native layer is used intentionally because Dear ImGui can own/capture keyboard
    /// input while a widget is focused, which made the previous shortcut unreliable.
    /// </summary>
    public void TryUndoLastPlacementShortcut(Simulation simulation)
    {
        if (_mode != AppMode.Builder || ImGui.GetIO().WantTextInput)
        {
            return;
        }

        UndoLastPlacement(simulation);
    }

    private void UndoLastPlacement(Simulation simulation)
    {
        int plannedBeforeUndo = simulation.Environment.PlannedAgentCount;
        bool undone = simulation.Environment.UndoLast();
        if (undone)
        {
            simulation.BuildWallGrid();
            InvalidateToxicFumeForGeometryChange(simulation);

            // Mouse-planned groups own the builder population. Legacy scenario files can still
            // carry only AgentCount, so undoing unrelated geometry must not erase that fallback.
            if (plannedBeforeUndo > 0 || simulation.Environment.PlannedAgentCount > 0)
            {
                _agentCount = simulation.Environment.PlannedAgentCount;
            }

            _scenarioDirty = true;
        }

        _statusMessage = undone
            ? "Last scene placement removed. (Ctrl+Z)"
            : "Nothing to undo.";
    }

    private static bool IsInsideWorld(Simulation simulation, Vector2 point)
        => point.X >= 0f && point.X <= simulation.Environment.Width &&
           point.Y >= 0f && point.Y <= simulation.Environment.Height;

    private static void Toggle(string label, bool currentValue, Action<bool> setter)
    {
        bool value = currentValue;
        if (ImGui.Checkbox(label, ref value))
        {
            setter(value);
        }
    }

    private static void DrawActiveButton(string label, bool active, Action onClick)
    {
        if (active)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.16f, 0.44f, 0.70f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.22f, 0.53f, 0.82f, 1f));
        }

        if (ImGui.Button(label, new Vector2(-1f, 0f)))
        {
            onClick();
        }

        if (active)
        {
            ImGui.PopStyleColor(2);
        }
    }

    private static void KeyValue(string label, string value)
    {
        float start = ImGui.GetCursorPosX();
        float width = ImGui.GetContentRegionAvail().X;
        float valueWidth = ImGui.CalcTextSize(value).X;
        float labelWidth = Math.Max(24f, width - valueWidth - 12f);
        string fittedLabel = FitText(label, labelWidth);
        ImGui.TextDisabled(fittedLabel);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(label + ": " + value);
        if (valueWidth + 24f <= width)
            ImGui.SameLine(start + width - valueWidth);
        ImGui.Text(value);
    }
}

/// <summary>Previous-frame performance counters for the diagnostics UI.</summary>
public readonly record struct RuntimePerformanceStats(
    double FrameMilliseconds,
    double PhysicsMilliseconds,
    double WorldDrawMilliseconds,
    double UiMilliseconds,
    double RecordingMilliseconds,
    long AllocatedBytes,
    int VisibleAgents,
    int ActiveAgents,
    int RecordingQueueDepth,
    int RecordingDroppedFrames);
