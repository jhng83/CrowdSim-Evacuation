using System.Globalization;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.OpenGL.Rendering;
using ImGuiNET;

namespace CrowdSim.OpenGL.UI;

public sealed partial class SimulationUi
{
    private static readonly Vector4 BuilderColor = new(0.55f, 0.62f, 0.70f, 1f);
    private static readonly Vector4 ReadyColor = new(0.14f, 0.43f, 0.73f, 1f);
    private static readonly Vector4 RunningColor = new(0.18f, 0.82f, 0.42f, 1f);
    private static readonly Vector4 PausedColor = new(0.95f, 0.63f, 0.15f, 1f);
    private static readonly Vector4 ErrorColor = new(0.95f, 0.25f, 0.20f, 1f);
    private static readonly Vector4 InfoColor = new(0.30f, 0.72f, 1f, 1f);

    /// <summary>Edit a validated integer without committing incomplete text.</summary>
    private bool DrawCaretIntegerField(
        string label,
        CaretNumberEditor editor,
        int currentValue,
        int minimum,
        int maximum,
        out int committedValue)
    {
        committedValue = currentValue;
        editor.Synchronize(currentValue);

        ImGui.Text(label);
        ImGui.SetNextItemWidth(-1f);

        bool hasError = editor.Error is not null;
        if (hasError)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.30f, 0.07f, 0.07f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Border, ErrorColor);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        }

        bool enterPressed = ImGui.InputText(
            $"##CaretInteger{label}",
            ref editor.Text,
            16,
            ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);
        bool isActive = ImGui.IsItemActive();
        bool escapePressed = editor.WasActive && ImGui.IsKeyPressed(ImGuiKey.Escape);
        bool deactivated = editor.WasActive && !isActive;

        if (hasError)
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Single-click places the caret. Double-click or Ctrl+A selects the complete value. " +
                "Enter or click elsewhere to commit; Escape cancels.");
        }

        bool committed = false;
        if (escapePressed)
        {
            editor.Reset(currentValue);
        }
        else if (enterPressed || deactivated)
        {
            if (int.TryParse(editor.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
                parsed >= minimum && parsed <= maximum)
            {
                committedValue = parsed;
                editor.Reset(parsed);
                committed = true;
            }
            else
            {
                editor.Error = $"Enter a whole number from {minimum} to {maximum}.";
            }
        }

        editor.WasActive = isActive && !escapePressed;
        if (editor.Error is not null)
        {
            ImGui.TextColored(ErrorColor, editor.Error);
        }

        return committed;
    }

    private void DrawBlastInspectorTab(Simulation simulation)
    {
        ImGui.Text("BLAST ZONES");
        ImGui.TextDisabled("Persistent, wall-clipped post-event analysis overlays.");
        ImGui.TextDisabled("Single-click a radius field to place the blinking text caret.");
        ImGui.Separator();

        bool showZones = _debug.ShowBlastZones;
        if (ImGui.Checkbox("Show blast zones##InspectorMasterBlastZones", ref showZones))
        {
            _debug.ShowBlastZones = showZones;
        }

        AgentConfig config = simulation.Config.Agent;
        bool radiiLocked = simulation.Environment.HasExplosion();

        bool radiusChanged = false;
        radiusChanged |= DrawCaretBlastZoneRow(
            "Kill",
            "Kill",
            BlastZoneStyle.Kill,
            _debug.ShowKillZone,
            value => _debug.ShowKillZone = value,
            config.HardKillRadius,
            0.10f,
            config.HeatIncapacityRadius - 0.10f,
            radiiLocked,
            value => config.SetBlastZoneRadii(
                value,
                config.HeatIncapacityRadius,
                config.FallenRadius,
                config.InjuryRadius,
                config.HeatRadius));

        radiusChanged |= DrawCaretBlastZoneRow(
            "Incapacitation",
            "Incap",
            BlastZoneStyle.Incapacitation,
            _debug.ShowIncapacitationZone,
            value => _debug.ShowIncapacitationZone = value,
            config.HeatIncapacityRadius,
            config.HardKillRadius + 0.10f,
            config.FallenRadius - 0.10f,
            radiiLocked,
            value => config.SetBlastZoneRadii(
                config.HardKillRadius,
                value,
                config.FallenRadius,
                config.InjuryRadius,
                config.HeatRadius));

        radiusChanged |= DrawCaretBlastZoneRow(
            "Fallen / severe",
            "Fallen",
            BlastZoneStyle.Fallen,
            _debug.ShowFallenZone,
            value => _debug.ShowFallenZone = value,
            config.FallenRadius,
            config.HeatIncapacityRadius + 0.10f,
            config.InjuryRadius - 0.10f,
            radiiLocked,
            value => config.SetBlastZoneRadii(
                config.HardKillRadius,
                config.HeatIncapacityRadius,
                value,
                config.InjuryRadius,
                config.HeatRadius));

        radiusChanged |= DrawCaretBlastZoneRow(
            "Injury",
            "Injury",
            BlastZoneStyle.Injury,
            _debug.ShowInjuryZone,
            value => _debug.ShowInjuryZone = value,
            config.InjuryRadius,
            config.FallenRadius + 0.10f,
            config.HeatRadius - 0.10f,
            radiiLocked,
            value => config.SetBlastZoneRadii(
                config.HardKillRadius,
                config.HeatIncapacityRadius,
                config.FallenRadius,
                value,
                config.HeatRadius));

        radiusChanged |= DrawCaretBlastZoneRow(
            "Panic / threat",
            "Panic",
            BlastZoneStyle.Panic,
            _debug.ShowPanicZone,
            value => _debug.ShowPanicZone = value,
            config.HeatRadius,
            config.InjuryRadius + 0.10f,
            200f,
            radiiLocked,
            value => config.SetBlastZoneRadii(
                config.HardKillRadius,
                config.HeatIncapacityRadius,
                config.FallenRadius,
                config.InjuryRadius,
                value));

        if (radiusChanged)
        {
            _scenarioDirty = true;
            _statusMessage = "Blast-zone radii updated. The validated inner-to-outer order remains intact.";
            ResetBlastRadiusEditors(config);
        }

        if (_blastValidationMessage is not null)
        {
            ImGui.Spacing();
            ImGui.TextColored(ErrorColor, _blastValidationMessage);
        }

        ImGui.Separator();
        if (radiiLocked)
        {
            ImGui.BeginDisabled();
        }

        if (DrawNeutralButton("Reset Zone Defaults", new Vector2(-1f, 0f)))
        {
            config.ResetBlastZoneRadii();
            ResetBlastRadiusEditors(config);
            _scenarioDirty = true;
            _statusMessage = "Blast-zone radii restored to validated defaults.";
        }

        if (radiiLocked)
        {
            ImGui.EndDisabled();
            ImGui.TextColored(
                new Vector4(1f, 0.70f, 0.22f, 1f),
                "Radii are read-only after detonation. Visibility remains adjustable.");
        }
        else
        {
            ImGui.TextDisabled("Enter or click elsewhere to commit. Escape restores the previous value.");
        }

        ImGui.TextDisabled("Guardrail: Kill < Incap < Fallen < Injury < Panic");
        ImGui.Separator();
        DrawBlastLegend();
    }

    private bool DrawCaretBlastZoneRow(
        string label,
        string id,
        Vector4 color,
        bool currentVisibility,
        Action<bool> visibilitySetter,
        float currentRadius,
        float minimum,
        float maximum,
        bool locked,
        Action<float> commit)
    {
        CaretNumberEditor editor = GetBlastRadiusEditor(id, currentRadius);
        editor.Synchronize(currentRadius);

        bool visible = currentVisibility;
        if (ImGui.Checkbox($"##ShowBlastInspector{id}", ref visible))
        {
            visibilitySetter(visible);
        }

        ImGui.SameLine();
        ImGui.TextColored(color, $"{label} (m)");
        ImGui.SameLine(154f);

        if (locked)
        {
            ImGui.BeginDisabled();
        }

        bool committed = false;
        if (ImGui.SmallButton($"-##BlastMinus{id}"))
        {
            float next = Math.Clamp(editor.LastCommittedValue - 0.10f, minimum, maximum);
            commit(next);
            editor.Reset(next);
            _blastValidationMessage = null;
            committed = true;
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(82f);

        bool hasError = editor.Error is not null;
        if (hasError)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.30f, 0.07f, 0.07f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Border, ErrorColor);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        }

        bool enterPressed = ImGui.InputText(
            $"##BlastCaretRadius{id}",
            ref editor.Text,
            16,
            ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);
        bool isActive = ImGui.IsItemActive();
        bool escapePressed = editor.WasActive && ImGui.IsKeyPressed(ImGuiKey.Escape);
        bool deactivated = editor.WasActive && !isActive;

        if (hasError)
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Single-click places the caret. Double-click or Ctrl+A selects the complete value. " +
                "Enter or click elsewhere to commit; Escape cancels.");
        }

        if (locked)
        {
            editor.Reset(currentRadius);
        }
        else if (escapePressed)
        {
            editor.Reset(currentRadius);
            _blastValidationMessage = null;
        }
        else if (enterPressed || deactivated)
        {
            if (TryParseBoundedFloat(editor.Text, minimum, maximum, out float parsed, out string error))
            {
                commit(parsed);
                editor.Reset(parsed);
                _blastValidationMessage = null;
                committed = true;
            }
            else
            {
                editor.Error = error;
                _blastValidationMessage = $"{label}: {error}";
            }
        }

        editor.WasActive = !locked && isActive && !escapePressed;

        ImGui.SameLine();
        if (ImGui.SmallButton($"+##BlastPlus{id}"))
        {
            float next = Math.Clamp(editor.LastCommittedValue + 0.10f, minimum, maximum);
            commit(next);
            editor.Reset(next);
            _blastValidationMessage = null;
            committed = true;
        }

        if (locked)
        {
            ImGui.EndDisabled();
        }

        return committed;
    }

    private void DrawAgentInspectorTab(Simulation simulation)
    {
        if (SelectedAgentId is null)
        {
            ImGui.Text("NO AGENT SELECTED");
            ImGui.TextWrapped("Choose Select Agent on the Run page, then click a person in the scene.");
            ImGui.Spacing();
            DrawAgentLegend();
            return;
        }

        DrawSelectedAgent(simulation);
    }

    private static void DrawMetricsInspectorTab(Simulation simulation, UiMetrics metrics)
    {
        ImGui.Text("KEY METRICS");
        KeyValue("In room", metrics.InRoom.ToString(CultureInfo.InvariantCulture));
        KeyValue("Mobile / responsive", metrics.Mobile.ToString(CultureInfo.InvariantCulture));
        KeyValue("Evacuated", metrics.Exited.ToString(CultureInfo.InvariantCulture));
        KeyValue("Injured", metrics.Injured.ToString(CultureInfo.InvariantCulture));
        KeyValue("Incapacitated", (metrics.TempIncap + metrics.Incapacitated).ToString(CultureInfo.InvariantCulture));
        KeyValue("Fatal", metrics.Dead.ToString(CultureInfo.InvariantCulture));
        KeyValue("Going to assist", metrics.GoingToAssist.ToString(CultureInfo.InvariantCulture));
        KeyValue("Currently helping", metrics.CurrentlyHelping.ToString(CultureInfo.InvariantCulture));
        KeyValue("Average panic", metrics.AveragePanic.ToString("F2", CultureInfo.InvariantCulture));
        KeyValue("Maximum panic", metrics.MaximumPanic.ToString("F2", CultureInfo.InvariantCulture));
        KeyValue("Average congestion", metrics.AverageCongestion.ToString("F2", CultureInfo.InvariantCulture));
        KeyValue("Elapsed time", $"{metrics.ElapsedSeconds:F1} s");

        if (simulation.ScenarioType == ScenarioType.Explosion)
        {
            KeyValue("Explosion", simulation.Environment.HasExplosion() ? "TRIGGERED" : "Not triggered");
        }
        else
        {
            KeyValue("Fume release", simulation.ToxicFumeSystem.IsReleased ? "ACTIVE" : "Not released");
            KeyValue("Fume exposed", metrics.ToxicExposed.ToString(CultureInfo.InvariantCulture));
            KeyValue("Fume impaired", metrics.ToxicImpaired.ToString(CultureInfo.InvariantCulture));
            KeyValue("Fume incapacitated", metrics.ToxicIncapacitated.ToString(CultureInfo.InvariantCulture));
            KeyValue("Avg. cum. exposure", metrics.AverageToxicExposure.ToString("F2", CultureInfo.InvariantCulture));
        }

        ImGui.Separator();
        ImGui.Text("EXIT FLOW");
        foreach (KeyValuePair<string, ExitMetrics> pair in simulation.ExitMetrics.OrderBy(pair => pair.Key))
        {
            ImGui.TextDisabled(pair.Key);
            ImGui.SameLine(150f);
            ImGui.Text($"density {pair.Value.Density:F0} | {pair.Value.Flow:F2}/s");
        }

        if (ImGui.CollapsingHeader("ADVANCED METRICS"))
        {
            KeyValue("Crawling", metrics.Crawling.ToString(CultureInfo.InvariantCulture));
            KeyValue("Fallen", metrics.Fallen.ToString(CultureInfo.InvariantCulture));
            KeyValue("Temp. incap.", metrics.TempIncap.ToString(CultureInfo.InvariantCulture));
            KeyValue("Avg. visibility", metrics.AverageVisibility.ToString("F2", CultureInfo.InvariantCulture));
            KeyValue("Run-contagion agents", metrics.RunningContagion.ToString(CultureInfo.InvariantCulture));
            KeyValue("Threat-escape", metrics.ThreatEscape.ToString(CultureInfo.InvariantCulture));
            KeyValue("Following crowd", metrics.FollowingCrowd.ToString(CultureInfo.InvariantCulture));
            if (simulation.ScenarioType == ScenarioType.Explosion && simulation.Environment.HasExplosion())
            {
                KeyValue("Debris hits", simulation.DebrisHits.ToString(CultureInfo.InvariantCulture));
                KeyValue("Debris injuries", simulation.DebrisInjuries.ToString(CultureInfo.InvariantCulture));
                KeyValue("Debris incap.", simulation.DebrisIncapacitations.ToString(CultureInfo.InvariantCulture));
                KeyValue("Debris fatal", simulation.DebrisFatalities.ToString(CultureInfo.InvariantCulture));
            }
            else if (simulation.ScenarioType == ScenarioType.ToxicFume)
            {
                KeyValue("Peak concentration", metrics.PeakToxicConcentration.ToString("F2", CultureInfo.InvariantCulture));
                KeyValue("Currently high conc.", metrics.ToxicHighConcentration.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (ImGui.CollapsingHeader("SCENARIO STATUS"))
        {
            DrawScenarioStatus(simulation);
        }
    }

    private void DrawDebugInspectorTab(Simulation simulation)
    {
        ImGui.Text("PERFORMANCE");
        bool hybrid = simulation.Config.HybridEnabled;
        ImGui.BeginDisabled(simulation.Running);
        if (ImGui.Checkbox("Hybrid crowd mode (experimental)", ref hybrid))
            simulation.Config.HybridEnabled = hybrid;
        ImGui.EndDisabled();
        KeyValue("Open / detailed", $"{simulation.HybridMovement.OpenSpaceAgents} / {simulation.HybridMovement.DetailedAgents}");
        KeyValue("Grouped agents", simulation.HybridMovement.GroupedAgents.ToString());
        KeyValue("Shared field builds", simulation.HybridMovement.Fields.FieldBuilds.ToString());
        KeyValue("Frame", $"{_performanceStats.FrameMilliseconds:F1} ms");
        KeyValue("Physics", $"{_performanceStats.PhysicsMilliseconds:F1} ms");
        KeyValue("World draw", $"{_performanceStats.WorldDrawMilliseconds:F1} ms");
        KeyValue("UI total", $"{_performanceStats.UiMilliseconds:F1} ms");
        KeyValue("Recording", $"{_performanceStats.RecordingMilliseconds:F1} ms");
        KeyValue("Allocated/frame", FormatBytes(_performanceStats.AllocatedBytes));
        KeyValue(
            "Visible agents",
            $"{_performanceStats.VisibleAgents}/{_performanceStats.ActiveAgents}");
        if (_recordingActive || _performanceStats.RecordingQueueDepth > 0 ||
            _performanceStats.RecordingDroppedFrames > 0)
        {
            KeyValue("Record queue", _performanceStats.RecordingQueueDepth.ToString(CultureInfo.InvariantCulture));
            KeyValue("Dropped frames", _performanceStats.RecordingDroppedFrames.ToString(CultureInfo.InvariantCulture));
        }

        ImGui.Separator();
        DrawDebugControls(simulation);
        ImGui.Separator();
        DrawAgentLegend();
        ImGui.Separator();
        DrawModelSummary(simulation);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double kib = bytes / 1024.0;
        if (kib < 1024.0)
        {
            return $"{kib:F1} KiB";
        }

        return $"{kib / 1024.0:F2} MiB";
    }

    private void DrawBlastLegend()
    {
        ImGui.Text("ZONE LEGEND");
        ImGui.TextColored(BlastZoneStyle.Kill, "Kill / terminal direct outcome");
        ImGui.TextColored(BlastZoneStyle.Incapacitation, "Incapacitation");
        ImGui.TextColored(BlastZoneStyle.Fallen, "Fallen / severe");
        ImGui.TextColored(BlastZoneStyle.Injury, "Injury");
        ImGui.TextColored(BlastZoneStyle.Panic, "Panic / threat influence");
    }

    private CaretNumberEditor GetBlastRadiusEditor(string id, float currentValue)
    {
        if (_blastRadiusEditors.TryGetValue(id, out CaretNumberEditor? editor))
        {
            return editor;
        }

        editor = new CaretNumberEditor(currentValue);
        _blastRadiusEditors.Add(id, editor);
        return editor;
    }

    private void ResetBlastRadiusEditors(AgentConfig config)
    {
        ResetBlastRadiusEditor("Kill", config.HardKillRadius);
        ResetBlastRadiusEditor("Incap", config.HeatIncapacityRadius);
        ResetBlastRadiusEditor("Fallen", config.FallenRadius);
        ResetBlastRadiusEditor("Injury", config.InjuryRadius);
        ResetBlastRadiusEditor("Panic", config.HeatRadius);
        _blastValidationMessage = null;
    }

    private void ResetBlastRadiusEditor(string id, float value)
    {
        if (_blastRadiusEditors.TryGetValue(id, out CaretNumberEditor? editor))
        {
            editor.Reset(value);
        }
    }

    private static bool TryParseBoundedFloat(
        string text,
        float minimum,
        float maximum,
        out float parsed,
        out string error)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
            !float.IsFinite(parsed))
        {
            error = "Enter a finite numeric value.";
            return false;
        }

        if (parsed < minimum || parsed > maximum)
        {
            error = $"Value must be between {minimum:0.##} and {maximum:0.##}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static (string Label, Vector4 Color) GetModePresentation(Simulation simulation)
    {
        if (simulation.Agents.Count == 0)
        {
            return ("BUILDER", BuilderColor);
        }

        if (simulation.Running)
        {
            return ("RUNNING", RunningColor);
        }

        return simulation.FrameIndex == 0
            ? ("READY", ReadyColor)
            : ("PAUSED", PausedColor);
    }

    private string GetActiveToolLabel(Simulation simulation)
    {
        if (_mode == AppMode.Builder)
        {
            return _builderTool switch
            {
                BuilderTool.Exit => "PLACE EXIT",
                BuilderTool.Blockage => $"PLACE BLOCKAGE - {_assetSelection.BlockageCells * simulation.Config.Grid.CellSizeMeters:F1} m SQUARE",
                BuilderTool.CircularBlockage => "PLACE BLOCKAGE - CIRCULAR Ø 2.5 m",
                BuilderTool.Agents => $"PLACE {_agentPlacementCount} AGENT{(_agentPlacementCount == 1 ? string.Empty : "S")}",
                BuilderTool.RoundTableSet => "PLACE ROUND TABLE + 10 PEOPLE",
                BuilderTool.SingleChair => "PLACE CHAIR + 1 PERSON",
                BuilderTool.RectangularTable => "PLACE TABLE - 3 m x 1 m",
                BuilderTool.Stage => simulation.Environment.HasStage ? "STAGE LIMIT REACHED (1/1)" : "PLACE STAGE - 15 m x 6 m",
                BuilderTool.Barricade => $"PLACE ORANGE BARRICADE - 1.5 m | {_assetSelection.RotationDegrees:0.0} deg",
                BuilderTool.ToxicFumeSource => "SET TOXIC-FUME RELEASE POINT",
                _ => "BUILDER",
            };
        }

        if (simulation.ScenarioType == ScenarioType.Explosion &&
            _worldMode == WorldInteractionMode.PlaceExplosion && !simulation.Environment.HasExplosion())
        {
            return "EXPLOSION ARMED - CLICK WORLD";
        }

        return SelectedAgentId is int selectedId ? $"AGENT {selectedId} SELECTED" : "SELECT AGENT";
    }

    private string GetActiveToolInstruction(Simulation simulation)
    {
        if (_mode == AppMode.Builder)
        {
            return _builderTool switch
            {
                BuilderTool.Exit => "Click near a boundary wall to place an exit.",
                BuilderTool.Blockage => $"Click clear floor to place a {_assetSelection.BlockageCells * simulation.Config.Grid.CellSizeMeters:F1} m square blockage.",
                BuilderTool.CircularBlockage => "Click clear floor to place a circular blockage with 2.5 m diameter.",
                BuilderTool.Agents => "Click the region where this population group should start; blocked cells are skipped automatically.",
                BuilderTool.RoundTableSet => "Click clear floor to place the round table, ten chairs and ten occupants.",
                BuilderTool.SingleChair => "Click clear floor to place a chair and its occupant.",
                BuilderTool.RectangularTable => "Click clear floor for a blast-movable table. No chairs or occupants are added.",
                BuilderTool.Stage => simulation.Environment.HasStage
                    ? "Only one stage is allowed. Undo its placement before placing another."
                    : "Click clear floor for one fixed stage. Agents go around it, not onto it.",
                BuilderTool.Barricade => "R: +15 deg; Shift+R: -15 deg. Set any angle in ASSETS. Parallel barricades can snap end-to-end.",
                BuilderTool.ToxicFumeSource => "Click clear floor to set the toxic-fume release point.",
                _ => "Choose a builder tool.",
            };
        }

        if (simulation.ScenarioType == ScenarioType.Explosion &&
            _worldMode == WorldInteractionMode.PlaceExplosion && !simulation.Environment.HasExplosion())
        {
            return "Click clear floor once to trigger the explosion and begin crowd dispersion.";
        }

        return "Click a humanoid to inspect it; use the mouse wheel over the world to zoom.";
    }

    private static Vector4 GetStatusMessageColor(string message)
    {
        if (message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("rejected", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("required", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorColor;
        }

        if (message.Contains("saved", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("loaded", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("running", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("placed", StringComparison.OrdinalIgnoreCase))
        {
            return RunningColor;
        }

        return InfoColor;
    }

    private static bool DrawNeutralButton(string label, Vector2 size)
        => ImGui.Button(label, size);

    private static bool DrawActionButton(string label, Vector4 color, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            new Vector4(
                Math.Clamp(color.X + 0.10f, 0f, 1f),
                Math.Clamp(color.Y + 0.10f, 0f, 1f),
                Math.Clamp(color.Z + 0.10f, 0f, 1f),
                color.W));
        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            new Vector4(color.X * 0.82f, color.Y * 0.82f, color.Z * 0.82f, color.W));
        // Amber/green actions need dark text; blue actions use the normal light text.
        float luminance = 0.2126f * color.X + 0.7152f * color.Y + 0.0722f * color.Z;
        ImGui.PushStyleColor(ImGuiCol.Text, luminance > 0.55f
            ? new Vector4(0.08f, 0.10f, 0.14f, 1f)
            : new Vector4(0.94f, 0.96f, 0.99f, 1f));
        bool clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(4);
        return clicked;
    }

    private readonly record struct UiMetrics(
        int InRoom,
        int Mobile,
        int Exited,
        int Injured,
        int Crawling,
        int Fallen,
        int TempIncap,
        int Incapacitated,
        int Dead,
        float AveragePanic,
        float MaximumPanic,
        float AverageVisibility,
        float AverageCongestion,
        int RunningContagion,
        int ThreatEscape,
        int FollowingCrowd,
        int GoingToAssist,
        int CurrentlyHelping,
        int ToxicExposed,
        int ToxicImpaired,
        int ToxicIncapacitated,
        int ToxicHighConcentration,
        float AverageToxicExposure,
        float PeakToxicConcentration,
        float ElapsedSeconds)
    {
        public static UiMetrics Capture(Simulation simulation)
        {
            int inRoom = 0;
            int mobile = 0;
            int exited = 0;
            int injured = 0;
            int crawling = 0;
            int fallen = 0;
            int tempIncap = 0;
            int incapacitated = 0;
            int dead = 0;
            int runningContagion = 0;
            int threatEscape = 0;
            int followingCrowd = 0;
            int goingToAssist = 0;
            int currentlyHelping = 0;
            int toxicExposed = 0;
            int toxicImpaired = 0;
            int toxicIncapacitated = 0;
            int toxicHighConcentration = 0;
            float toxicExposureSum = 0f;
            float peakToxicConcentration = 0f;
            int toxicExposureSamples = 0;
            float panicSum = 0f;
            float maximumPanic = 0f;
            float visibilitySum = 0f;
            float congestionSum = 0f;

            foreach (Agent agent in simulation.Agents)
            {
                switch (agent.State)
                {
                    case AgentState.Exited: exited++; break;
                    case AgentState.Injured: injured++; break;
                    case AgentState.Crawling: crawling++; break;
                    case AgentState.Fallen: fallen++; break;
                    case AgentState.TempIncap: tempIncap++; break;
                    case AgentState.Incapacitated: incapacitated++; break;
                    case AgentState.Dead: dead++; break;
                }

                if (simulation.ScenarioType == ScenarioType.ToxicFume)
                {
                    switch (agent.ToxicExposureStatus)
                    {
                        case ToxicExposureStatus.Exposed:
                            toxicExposed++;
                            break;
                        case ToxicExposureStatus.Impaired:
                            toxicExposed++;
                            toxicImpaired++;
                            break;
                        case ToxicExposureStatus.Incapacitated:
                            toxicExposed++;
                            toxicIncapacitated++;
                            break;
                    }

                    if (agent.IsActive &&
                        agent.CurrentToxicConcentration >= simulation.ToxicFumeSystem.Configuration.DangerConcentration)
                    {
                        toxicHighConcentration++;
                    }

                    toxicExposureSum += agent.CumulativeToxicExposure;
                    peakToxicConcentration = Math.Max(peakToxicConcentration, agent.PeakToxicConcentration);
                    toxicExposureSamples++;
                }

                if (!agent.IsActive)
                {
                    continue;
                }

                inRoom++;
                bool isMobile = agent.State is not
                    (AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap or AgentState.Fallen);
                if (!isMobile)
                {
                    continue;
                }

                mobile++;
                float panic = agent.PanicLevel;
                panicSum += panic;
                maximumPanic = Math.Max(maximumPanic, panic);
                visibilitySum += agent.VisibilityFactor;
                congestionSum += agent.LocalCongestion;
                if (agent.LastRunningPanicStimulus > 0.05f) runningContagion++;

                switch (agent.Intent)
                {
                    case BehaviorIntent.ThreatEscape:
                        threatEscape++;
                        break;
                    case BehaviorIntent.FollowCrowd:
                        followingCrowd++;
                        break;
                    case BehaviorIntent.AssistCasualty:
                        if (agent.AssistPauseUntilFrame >= 0 && simulation.FrameIndex < agent.AssistPauseUntilFrame)
                            currentlyHelping++;
                        else
                            goingToAssist++;
                        break;
                }
            }

            float inverseMobile = mobile > 0 ? 1f / mobile : 0f;
            return new UiMetrics(
                InRoom: inRoom,
                Mobile: mobile,
                Exited: exited,
                Injured: injured,
                Crawling: crawling,
                Fallen: fallen,
                TempIncap: tempIncap,
                Incapacitated: incapacitated,
                Dead: dead,
                AveragePanic: panicSum * inverseMobile,
                MaximumPanic: maximumPanic,
                AverageVisibility: mobile == 0 ? 1f : visibilitySum * inverseMobile,
                AverageCongestion: congestionSum * inverseMobile,
                RunningContagion: runningContagion,
                ThreatEscape: threatEscape,
                FollowingCrowd: followingCrowd,
                GoingToAssist: goingToAssist,
                CurrentlyHelping: currentlyHelping,
                ToxicExposed: toxicExposed,
                ToxicImpaired: toxicImpaired,
                ToxicIncapacitated: toxicIncapacitated,
                ToxicHighConcentration: toxicHighConcentration,
                AverageToxicExposure: toxicExposureSamples > 0 ? toxicExposureSum / toxicExposureSamples : 0f,
                PeakToxicConcentration: peakToxicConcentration,
                ElapsedSeconds: simulation.FrameIndex * simulation.Config.Agent.Dt);
        }
    }

}
