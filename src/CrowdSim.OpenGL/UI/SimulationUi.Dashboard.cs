using System.Globalization;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.OpenGL.Rendering;
using ImGuiNET;

namespace CrowdSim.OpenGL.UI;

public sealed partial class SimulationUi
{
    /// <summary>Keep the panel toggle outside the panel it controls, including while hidden.</summary>
    private void DrawDashboardHeader(Simulation simulation, UiMetrics metrics)
    {
        float rowStart = ImGui.GetCursorPosX();
        float rowWidth = ImGui.GetContentRegionAvail().X;
        string toggleLabel = _inspector.IsVisible ? "Hide inspector" : "Show inspector";
        float toggleWidth = ImGui.CalcTextSize(toggleLabel).X + 28f;

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(InfoColor, "CROWD SIM");
        ImGui.SameLine();
        (string modeLabel, Vector4 modeColor) = GetModePresentation(simulation);
        DrawBadge(modeLabel, modeColor);
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), rowStart + rowWidth - toggleWidth));
        if (DrawNeutralButton(toggleLabel + "##InspectorVisibility", new Vector2(toggleWidth, 30f)))
            _inspector.ToggleVisibility();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Show or hide the right inspector. Your selected agent, settings and overlays are retained. Shortcut: F4.");

        string scenario = simulation.ScenarioType == ScenarioType.Explosion ? "Explosion" : "Toxic Fume";
        string name = string.IsNullOrWhiteSpace(_scenarioName) ? "Untitled scene" : _scenarioName;
        string summary = $"{scenario}  /  {name}{(_scenarioDirty ? "  [unsaved]" : string.Empty)}";
        if (rowWidth >= 1000f)
            summary += $"    |    {metrics.ElapsedSeconds:F1} s    |    {(_mode == AppMode.Builder ? simulation.Environment.PlannedAgentCount : metrics.InRoom)} in room    |    {metrics.Exited} evacuated";
        DrawFittedText(summary, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
    }

    /// <summary>Scene actions stay fixed; only the selected control page scrolls.</summary>
    private void DrawDashboardControls(Simulation simulation, WorldRenderer renderer)
    {
        ImGui.Text(_mode == AppMode.Builder ? "SCENE BUILDER" : "SIMULATION CONTROL");
        ImGui.TextDisabled(_mode == AppMode.Builder ? "Prepare the scene, then run it." : "Run, inspect and replay your scene.");
        ImGui.Spacing();
        DrawCompactSessionControls(simulation);
        ImGui.Spacing();

        float buttonWidth = Math.Max(1f, (ImGui.GetContentRegionAvail().X - 2f * ImGui.GetStyle().ItemSpacing.X) / 3f);
        DrawControlPageButton(_mode == AppMode.Builder ? "Build" : "Run", ControlPanelTab.Scene, buttonWidth);
        ImGui.SameLine();
        DrawControlPageButton("Camera", ControlPanelTab.Camera, buttonWidth);
        ImGui.SameLine();
        DrawControlPageButton("Files", ControlPanelTab.Files, buttonWidth);
        ImGui.Spacing();

        // The primary action cannot disappear below a long list of asset/tuning controls.
        float footerHeight = _mode == AppMode.Builder ? 122f : 114f;
        float pageHeight = Math.Max(1f, ImGui.GetContentRegionAvail().Y - footerHeight - ImGui.GetStyle().ItemSpacing.Y);
        ImGui.BeginChild("##control-page-" + _controlPanelTab, new Vector2(0f, pageHeight),
            ImGuiChildFlags.AlwaysUseWindowPadding);
        ImGui.PushItemWidth(-1f);
        switch (_controlPanelTab)
        {
            case ControlPanelTab.Scene:
                if (_mode == AppMode.Builder) DrawBuilderControls(simulation);
                else DrawRuntimeControls(simulation);
                break;
            case ControlPanelTab.Camera:
                ImGui.Text("CAMERA & VIEW");
                ImGui.TextWrapped("Adjust the view here. Select a person in the scene to use Follow or Agent POV in the inspector.");
                ImGui.Spacing();
                DrawCameraControls(renderer);
                break;
            case ControlPanelTab.Files:
                DrawScenarioPersistenceControls(simulation);
                if (!string.IsNullOrWhiteSpace(_lastRecordingPath))
                {
                    ImGui.Spacing();
                    ImGui.Text("LAST RECORDING");
                    ImGui.TextWrapped(_lastRecordingPath);
                }
                ImGui.Spacing();
                ImGui.Separator();
                if (DrawNeutralButton("Exit application", new Vector2(-1f, 32f))) ExitRequested = true;
                break;
        }
        ImGui.PopItemWidth();
        ImGui.EndChild();
        ImGui.Separator();
        if (_mode == AppMode.Builder) DrawFinaliseControls(simulation);
        else DrawRunTransportControls(simulation);
    }

    private void DrawControlPageButton(string label, ControlPanelTab tab, float width)
    {
        if (DrawNavigationButton(label + "##ControlPage", _controlPanelTab == tab, new Vector2(width, 32f)))
            _controlPanelTab = tab;
    }

    private void DrawCompactSessionControls(Simulation simulation)
    {
        float width = Math.Max(1f, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f);
        if (DrawNeutralButton("Save scene", new Vector2(width, 30f)))
        {
            _scenarioSaveRequested = string.IsNullOrWhiteSpace(_scenarioName) ? "scenario" : _scenarioName.Trim();
            _statusMessage = "Saving authored scene configuration...";
        }
        ImGui.SameLine();
        if (DrawNeutralButton("New scenario", new Vector2(width, 30f)))
        {
            if (_scenarioDirty) _returnToSelectionConfirmation = true;
            else
            {
                simulation.SetRunning(false);
                _newScenarioRequested = true;
            }
        }
        if (_returnToSelectionConfirmation)
        {
            ImGui.TextWrapped("Discard unsaved scene changes and return to scenario selection?");
            if (DrawNeutralButton("Discard changes", new Vector2(width, 30f)))
            {
                simulation.SetRunning(false);
                _newScenarioRequested = true;
                _returnToSelectionConfirmation = false;
            }
            ImGui.SameLine();
            if (DrawNeutralButton("Keep scene", new Vector2(width, 30f))) _returnToSelectionConfirmation = false;
        }
        if (_mode == AppMode.Simulation && DrawNeutralButton("Replay from scene start", new Vector2(-1f, 30f)))
        {
            simulation.SetRunning(false);
            _restartSimulationRequested = true;
            _statusMessage = "Restoring the finalised, not-yet-started scene...";
        }
    }

    /// <summary>Transport remains usable even when the Camera or Files page is selected.</summary>
    private void DrawRunTransportControls(Simulation simulation)
    {
        if (!simulation.Running && simulation.FrameIndex == 0)
        {
            bool record = _recordNextRun;
            if (ImGui.Checkbox("Record this run", ref record))
            {
                _recordNextRun = record;
                _statusMessage = record ? "Recording will start with this run." : "Recording disarmed.";
            }
        }
        else if (_recordingActive)
        {
            ImGui.TextColored(ErrorColor, "REC");
            ImGui.SameLine();
            if (DrawNeutralButton("Stop recording", new Vector2(-1f, 26f))) _stopRecordingRequested = true;
        }
        else ImGui.TextDisabled($"Elapsed {simulation.FrameIndex * simulation.Config.Agent.Dt:F1} s  |  Playback {_simulationSpeed:F2}x");

        string label = simulation.Running ? "Pause simulation" : simulation.FrameIndex == 0 ? "Start simulation" : "Resume simulation";
        Vector4 color = simulation.Running ? PausedColor : ReadyColor;
        if (DrawActionButton(label, color, new Vector2(-1f, 40f)))
        {
            bool run = !simulation.Running;
            simulation.SetRunning(run);
            _statusMessage = simulation.Running ? "Simulation running." : !run ? "Simulation paused."
                : simulation.ScenarioType == ScenarioType.ToxicFume && !simulation.ToxicFumeSystem.IsCacheValidFor(simulation)
                    ? "Compute the fume dispersion before starting."
                    : "The scene needs agents and at least one exit before it can run.";
        }
    }

    private void DrawViewportToolbar()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(_povEnabled ? "AGENT POV" : _followSelectedAgent ? "FOLLOW VIEW" : "SCENE VIEW");
        if (SelectedAgentId is int id)
        {
            ImGui.SameLine();
            if (DrawNeutralButton($"Agent {id}##RevealAgentInspector", new Vector2(0f, 26f)))
                _inspector.Show(InspectorTab.Agent);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open this person's details in the right inspector.");
        }
        ImGui.Separator();
    }

    /// <summary>Explicit navigation keeps the active inspector page stable across hide/show.</summary>
    private void DrawDashboardInspector(Simulation simulation, UiMetrics metrics)
    {
        ImGui.Text("INSPECTOR");
        ImGui.TextDisabled("Overview, hazard, agent and debug.");
        ImGui.Spacing();
        float width = Math.Max(1f, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f);
        DrawInspectorPageButton("Overview", InspectorTab.Overview, width);
        ImGui.SameLine();
        DrawInspectorPageButton(simulation.ScenarioType == ScenarioType.Explosion ? "Blast" : "Fume", InspectorTab.Hazard, width);
        DrawInspectorPageButton(SelectedAgentId is int id ? $"Agent {id}" : "Agent", InspectorTab.Agent, width);
        ImGui.SameLine();
        DrawInspectorPageButton("Debug", InspectorTab.Debug, width);
        ImGui.Spacing();
        ImGui.Separator();

        ImGui.BeginChild("##inspector-page-" + _inspector.ActiveTab, Vector2.Zero,
            ImGuiChildFlags.AlwaysUseWindowPadding);
        ImGui.PushItemWidth(-1f);
        switch (_inspector.ActiveTab)
        {
            case InspectorTab.Overview:
                DrawOverviewCards(simulation, metrics);
                ImGui.Spacing();
                DrawMetricsInspectorTab(simulation, metrics);
                break;
            case InspectorTab.Hazard:
                if (simulation.ScenarioType == ScenarioType.Explosion) DrawBlastInspectorTab(simulation);
                else DrawToxicFumeInspectorTab(simulation);
                break;
            case InspectorTab.Agent:
                DrawAgentInspectorTab(simulation);
                break;
            case InspectorTab.Debug:
                DrawDebugInspectorTab(simulation);
                break;
        }
        ImGui.PopItemWidth();
        ImGui.EndChild();
    }

    private void DrawInspectorPageButton(string label, InspectorTab tab, float width)
    {
        if (DrawNavigationButton(label + "##InspectorPage-" + tab, _inspector.ActiveTab == tab, new Vector2(width, 32f)))
            _inspector.SelectTab(tab);
    }

    private static void DrawOverviewCards(Simulation simulation, UiMetrics metrics)
    {
        float width = Math.Max(1f, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f);
        DrawMetricCard("IN ROOM", metrics.InRoom, InfoColor, width);
        ImGui.SameLine();
        DrawMetricCard("EVACUATED", metrics.Exited, RunningColor, width);
        DrawMetricCard("ASSISTING", metrics.CurrentlyHelping, new Vector4(0.72f, 0.60f, 0.98f, 1f), width);
        ImGui.SameLine();
        int affected = simulation.ScenarioType == ScenarioType.ToxicFume
            ? metrics.ToxicExposed : metrics.Injured + metrics.Fallen + metrics.Crawling + metrics.TempIncap + metrics.Incapacitated + metrics.Dead;
        DrawMetricCard("AFFECTED", affected, PausedColor, width);
    }

    private static void DrawMetricCard(string label, int value, Vector4 color, float width)
    {
        ImGui.BeginChild("##metric-card-" + label, new Vector2(width, 72f),
            ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.TextDisabled(label);
        ImGui.TextColored(color, value.ToString(CultureInfo.InvariantCulture));
        ImGui.EndChild();
    }

    private void DrawDashboardStatus(Simulation simulation)
    {
        DrawFittedText(_statusMessage, GetStatusMessageColor(_statusMessage));
        DrawFittedText(GetActiveToolLabel(simulation) + "  |  " + GetActiveToolInstruction(simulation),
            ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
    }

    private static void DrawFittedText(string text, Vector4 color)
    {
        string fitted = FitText(text, Math.Max(1f, ImGui.GetContentRegionAvail().X));
        ImGui.TextColored(color, fitted);
        if (fitted != text && ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    /// <summary>Keep guidance readable at the available panel width.</summary>
    private static void DrawMutedText(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static string FitText(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        const string suffix = "...";
        int length = text.Length;
        while (length > 0 && ImGui.CalcTextSize(text[..length] + suffix).X > width) length--;
        return text[..length] + suffix;
    }

    private static void DrawBadge(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(color.X, color.Y, color.Z, 0.16f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(color.X, color.Y, color.Z, 0.16f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(color.X, color.Y, color.Z, 0.16f));
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.Button(text + "##ModeBadge", new Vector2(0f, 30f));
        ImGui.PopStyleColor(4);
    }

    private static bool DrawNavigationButton(string label, bool selected, Vector2 size)
    {
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.13f, 0.29f, 0.44f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.64f, 0.86f, 1f, 1f));
        }
        bool clicked = ImGui.Button(label, size);
        if (selected) ImGui.PopStyleColor(2);
        return clicked;
    }

    /// <summary>Native shortcut; toolbar buttons remain the primary visible control.</summary>
    public void TryToggleInspectorShortcut()
    {
        if (_mode != AppMode.ScenarioSelection && !ImGui.GetIO().WantTextInput)
            _inspector.ToggleVisibility();
    }
}
