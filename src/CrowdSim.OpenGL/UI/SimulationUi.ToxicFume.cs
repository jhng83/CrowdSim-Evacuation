using System.Globalization;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using ImGuiNET;

namespace CrowdSim.OpenGL.UI;

public sealed partial class SimulationUi
{
    private void DrawScenarioSelectionScreen(Simulation simulation, Vector2 displaySize)
    {
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(displaySize, ImGuiCond.Always);
        ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;
        ImGui.Begin("##scenario-selection", flags);
        Vector2 available = ImGui.GetContentRegionAvail();
        float width = Math.Min(1040f, Math.Max(1f, available.X - 48f));
        float height = Math.Min(650f, Math.Max(1f, available.Y - 32f));
        ImGui.SetCursorPos(new Vector2(
            ImGui.GetCursorPosX() + Math.Max(0f, (available.X - width) * 0.5f),
            ImGui.GetCursorPosY() + Math.Max(0f, (available.Y - height) * 0.45f)));
        ImGui.BeginChild("##scenario-selection-card", new Vector2(width, height), ImGuiChildFlags.Borders);
        ImGui.TextColored(InfoColor, "CROWD SIM");
        ImGui.Spacing();
        ImGui.SetWindowFontScale(1.35f);
        ImGui.Text("Create your simulation workspace");
        ImGui.SetWindowFontScale(1f);
        ImGui.TextWrapped("Build a scene, run an event, and inspect how the crowd responds.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("CHOOSE A SCENARIO");
        DrawMutedText("Both scenarios share your scene tools and crowd behaviour controls.");
        ImGui.Spacing();

        float contentWidth = ImGui.GetContentRegionAvail().X;
        bool sideBySide = contentWidth >= 650f;
        float cardWidth = sideBySide ? (contentWidth - ImGui.GetStyle().ItemSpacing.X) * 0.5f : contentWidth;
        DrawScenarioChoice(simulation, ScenarioType.Explosion, cardWidth);
        if (sideBySide) ImGui.SameLine();
        DrawScenarioChoice(simulation, ScenarioType.ToxicFume, cardWidth);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text("OPEN A SAVED SCENE");
        string preview = _selectedScenarioIndex >= 0 && _selectedScenarioIndex < _savedScenarioNames.Length
            ? _savedScenarioNames[_selectedScenarioIndex] : "Choose a saved scene...";
        ImGui.SetNextItemWidth(Math.Max(1f, ImGui.GetContentRegionAvail().X - 170f));
        if (ImGui.BeginCombo("##StartupSavedScenario", preview))
        {
            for (int index = 0; index < _savedScenarioNames.Length; index++)
            {
                bool selected = index == _selectedScenarioIndex;
                if (ImGui.Selectable(_savedScenarioNames[index], selected)) _selectedScenarioIndex = index;
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        bool canLoad = _selectedScenarioIndex >= 0 && _selectedScenarioIndex < _savedScenarioNames.Length;
        ImGui.SameLine();
        ImGui.BeginDisabled(!canLoad);
        if (DrawNeutralButton("Open scene", new Vector2(150f, 0f)) && canLoad)
            _scenarioLoadRequested = _savedScenarioNames[_selectedScenarioIndex];
        ImGui.EndDisabled();
        ImGui.TextWrapped(_savedScenarioNames.Length == 0
            ? "No saved scenes yet. Create a scenario and use Save scene in the workspace."
            : "Saved scenes open in the builder so you can review or edit the layout before running.");
        if (ImGui.CollapsingHeader("Model notes"))
            ImGui.TextWrapped("Toxic Fume uses normalised concentration and exposure for crowd-response comparison. It is a simplified model, not real-chemical CFD.");
        ImGui.EndChild();
        ImGui.End();
    }

    /// <summary>Scenario choice cards provide the same entry points in wide and narrow windows.</summary>
    private void DrawScenarioChoice(Simulation simulation, ScenarioType type, float width)
    {
        bool explosion = type == ScenarioType.Explosion;
        Vector4 accent = explosion ? new Vector4(0.96f, 0.66f, 0.27f, 1f) : new Vector4(0.36f, 0.82f, 0.63f, 1f);
        ImGui.BeginChild("##scenario-choice-" + type, new Vector2(width, 210f), ImGuiChildFlags.Borders);
        ImGui.TextColored(accent, explosion ? "EXPLOSION" : "TOXIC FUME");
        ImGui.Spacing();
        ImGui.TextWrapped(explosion
            ? "Explore evacuation after a sudden event, including shock, debris, casualties and crowd response."
            : "Explore evacuation around a spreading plume. Compute the field during setup, then release it during the run.");
        ImGui.Spacing();
        DrawMutedText(explosion ? "Build scene > Start > Place explosion" : "Build scene > Compute plume > Start > Release");
        ImGui.Spacing();
        if (DrawActionButton(explosion ? "Create explosion scenario" : "Create toxic fume scenario", accent, new Vector2(-1f, 38f)))
        {
            simulation.SetScenarioType(type);
            _mode = AppMode.Builder;
            _controlPanelTab = ControlPanelTab.Scene;
            _builderTool = BuilderTool.Exit;
            _scenarioName = explosion ? "explosion_scenario" : "toxic_fume_scenario";
            _scenarioDirty = true;
            _inspector.SelectTab(InspectorTab.Overview);
            _statusMessage = explosion
                ? "Place exits and people, then finalise the scene."
                : "Place exits, people and a fume source. Compute dispersion before finalising.";
        }
        ImGui.EndChild();
    }

    private void DrawToxicFumeBuilderControls(Simulation simulation)
    {
        if (!ImGui.CollapsingHeader("TOXIC FUME CONFIGURATION", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ToxicFumeConfig config = simulation.ToxicFumeSystem.Configuration;
        Vector2? source = simulation.ToxicFumeSystem.SourcePosition;
        DrawMutedText("Compute the plume during setup, then preview it before running.");
        KeyValue("Release point", source is Vector2 point
            ? $"({point.X:F1} m, {point.Y:F1} m)"
            : "Not placed");

        bool sourceToolActive = _builderTool == BuilderTool.ToxicFumeSource;
        DrawActiveButton(
            source is null ? "Set Release Point" : "Move Release Point",
            sourceToolActive,
            () =>
            {
                _builderTool = BuilderTool.ToxicFumeSource;
                _statusMessage = "Toxic-fume release-point tool active. Click clear floor inside the room.";
            });

        float initialRadius = config.InitialRadiusMeters;
        float spreadSpeed = config.DispersionSpeedMetersPerSecond;
        float maxExtent = config.MaximumExtentMeters;
        float releaseDuration = config.ReleaseDurationSeconds;
        float driftDirection = config.DriftDirectionDegrees;
        float driftBias = config.DriftBias;
        float awareness = config.AwarenessConcentration;
        float danger = config.DangerConcentration;
        float impairment = config.ImpairmentExposure;
        float incapacitation = config.IncapacitationExposure;

        bool changed = false;
        changed |= DrawFullWidthSlider(
            "Initial cloud radius", "FumeInitialRadius", ref initialRadius, 0.20f, 5.0f, "%.2f m",
            "Size of the initially affected area at release time.");
        changed |= DrawFullWidthSlider(
            "Dispersion speed", "FumeSpreadSpeed", ref spreadSpeed, 0.05f, 2.0f, "%.2f m/s",
            "Abstract speed used to precompute when the plume reaches neighbouring open cells.");
        maxExtent = Math.Max(maxExtent, initialRadius);
        changed |= DrawFullWidthSlider(
            "Maximum plume extent", "FumeMaxExtent", ref maxExtent, Math.Max(2.0f, initialRadius), 60.0f, "%.1f m",
            "Maximum path distance the precomputed plume may travel from its source.");
        changed |= DrawFullWidthSlider(
            "Release duration", "FumeReleaseDuration", ref releaseDuration, 1f, 180f, "%.0f s",
            "How long each reached region remains supplied before the generic post-release decay begins.");

        if (ImGui.CollapsingHeader("FUME ADVANCED"))
        {
            changed |= DrawFullWidthSlider(
                "Drift direction", "FumeDriftDirection", ref driftDirection, 0f, 359f, "%.0f deg",
                "Abstract directional bias for the precomputed plume. 0 degrees points along +X in the simulation world.");
            changed |= DrawFullWidthSlider(
                "Directional drift", "FumeDriftBias", ref driftBias, 0f, 1f, "%.2f",
                "0 spreads symmetrically through open cells; 1 strongly favours the configured drift direction.");
            changed |= DrawFullWidthSlider(
                "Awareness threshold", "FumeAwareness", ref awareness, 0.02f, 0.80f, "%.2f",
                "Normalized concentration at which direct fume awareness can begin. This is a simulator threshold, not a chemical limit.");
            danger = Math.Max(danger, awareness + 0.05f);
            changed |= DrawFullWidthSlider(
                "High-threat threshold", "FumeDanger", ref danger, Math.Min(0.95f, awareness + 0.05f), 0.98f, "%.2f",
                "Normalized concentration that applies a stronger panic stimulus.");
            changed |= DrawFullWidthSlider(
                "Impairment exposure", "FumeImpairment", ref impairment, 1f, 120f, "%.1f",
                "Generic cumulative concentration-seconds before mobility begins to reduce.");
            incapacitation = Math.Max(incapacitation, impairment + 1f);
            changed |= DrawFullWidthSlider(
                "Incapacitation exposure", "FumeIncap", ref incapacitation, impairment + 1f, 300f, "%.1f",
                "Generic cumulative concentration-seconds before the simulator marks an agent incapacitated.");
        }

        if (changed)
        {
            config.SetValues(
                initialRadius, spreadSpeed, maxExtent, releaseDuration,
                driftDirection, driftBias, awareness, danger, impairment, incapacitation);
            simulation.ToxicFumeSystem.InvalidatePrecomputation();
            _scenarioDirty = true;
            _statusMessage = "Fume settings changed. Recompute the plume before finalising.";
        }

        ImGui.Spacing();
        bool canCompute = simulation.ToxicFumeSystem.SourcePosition is not null;
        if (!canCompute)
        {
            ImGui.BeginDisabled();
        }

        if (DrawActionButton(
                simulation.ToxicFumeSystem.IsPrecomputed ? "Recompute Fume Dispersion" : "Compute Fume Dispersion",
                new Vector4(0.13f, 0.58f, 0.42f, 1f),
                new Vector2(-1f, 36f)))
        {
            try
            {
                simulation.PrecomputeToxicFume();
                _statusMessage =
                    $"Fume field ready: {simulation.ToxicFumeSystem.ReachableCellCount:N0} reachable cells, " +
                    $"max arrival {simulation.ToxicFumeSystem.PrecomputedMaximumArrivalSeconds:F1} s.";
            }
            catch (Exception exception)
            {
                _statusMessage = $"Fume computation failed: {exception.Message}";
            }
        }

        if (!canCompute)
        {
            ImGui.EndDisabled();
            ImGui.TextColored(new Vector4(1f, 0.67f, 0.22f, 1f), "Place a release point before computing the plume.");
        }

        if (simulation.ToxicFumeSystem.IsCacheValidFor(simulation))
        {
            ImGui.TextColored(new Vector4(0.28f, 0.86f, 0.48f, 1f), "READY - PRECOMPUTED");
            KeyValue("Reachable cells", simulation.ToxicFumeSystem.ReachableCellCount.ToString("N0", CultureInfo.InvariantCulture));
            KeyValue("Grid cells", simulation.ToxicFumeSystem.GridCellCount.ToString("N0", CultureInfo.InvariantCulture));
            KeyValue("Grid resolution", $"{simulation.ToxicFumeSystem.GridCellSizeMeters:F2} m");
            KeyValue("Cache", FormatBytes(simulation.ToxicFumeSystem.EstimatedCacheBytes));
            KeyValue("Precompute time", $"{simulation.ToxicFumeSystem.LastPrecomputeMilliseconds:F1} ms");

            float previewMax = Math.Max(
                10f,
                simulation.ToxicFumeSystem.PrecomputedMaximumArrivalSeconds +
                config.ReleaseDurationSeconds + config.PostReleaseDecaySeconds);
            float preview = Math.Clamp(simulation.ToxicFumeSystem.PreviewTimeSeconds, 0f, previewMax);
            ImGui.Text("Preview plume timeline");
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##FumePreviewTimeline", ref preview, 0f, previewMax, "%.1f s"))
            {
                simulation.ToxicFumeSystem.PreviewTimeSeconds = preview;
            }
            DrawMutedText("Drag the timeline to preview the plume before running the crowd.");
        }
        else if (source is not null)
        {
            string readiness = simulation.ToxicFumeSystem.IsPrecomputed
                ? "STALE - geometry/configuration changed; recompute dispersion."
                : "NOT READY - dispersion must be computed.";
            ImGui.TextColored(new Vector4(1f, 0.67f, 0.22f, 1f), readiness);
        }

        if (DrawNeutralButton("Reset Fume Defaults", new Vector2(-1f, 0f)))
        {
            config.ResetDefaults();
            simulation.ToxicFumeSystem.InvalidatePrecomputation();
            _scenarioDirty = true;
            _statusMessage = "Toxic-fume defaults restored. Recompute the plume.";
        }

        ImGui.Separator();
    }

    private void DrawToxicFumeRuntimeEventControls(Simulation simulation)
    {
        ImGui.Spacing();
        ImGui.Text("TOXIC FUME EVENT");
        Vector2? source = simulation.ToxicFumeSystem.SourcePosition;
        KeyValue("Source", source is Vector2 point ? $"({point.X:F1}, {point.Y:F1}) m" : "Not set");

        if (simulation.ToxicFumeSystem.IsReleased)
        {
            ImGui.TextColored(new Vector4(0.42f, 0.90f, 0.55f, 1f), "RELEASE ACTIVE");
            KeyValue("Elapsed", $"{simulation.ToxicFumeSystem.GetElapsedSeconds(simulation):F1} s");
            DrawMutedText("The plume follows the dispersion computed during setup.");
            return;
        }

        bool canRelease = simulation.Running && simulation.ToxicFumeSystem.IsCacheValidFor(simulation);
        if (!canRelease)
        {
            ImGui.BeginDisabled();
        }

        if (DrawActionButton("Release Toxic Fume", new Vector4(0.14f, 0.62f, 0.40f, 1f), new Vector2(-1f, 36f)))
        {
            bool triggered = simulation.TriggerToxicFumeRelease();
            _statusMessage = triggered
                ? "Toxic fume released. Cached plume replay is now active; crowd response remains live."
                : "Toxic-fume release rejected.";
        }

        if (!canRelease)
        {
            ImGui.EndDisabled();
            DrawMutedText(simulation.Running
                ? "A valid precomputed fume cache is required."
                : "Start the simulation before releasing the fume.");
        }
    }

    private void DrawToxicFumeInspectorTab(Simulation simulation)
    {
        ToxicFumeConfig config = simulation.ToxicFumeSystem.Configuration;
        ImGui.Text("TOXIC FUME FIELD");
        DrawMutedText("Inspect the plume and its concentration thresholds.");
        ImGui.Separator();

        KeyValue("Cache", simulation.ToxicFumeSystem.IsCacheValidFor(simulation)
            ? "READY"
            : simulation.ToxicFumeSystem.IsPrecomputed ? "STALE" : "Not computed");
        KeyValue("Release", simulation.ToxicFumeSystem.IsReleased ? "ACTIVE" : "Not released");
        if (simulation.ToxicFumeSystem.IsReleased)
        {
            KeyValue("Elapsed", $"{simulation.ToxicFumeSystem.GetElapsedSeconds(simulation):F1} s");
        }
        KeyValue("Initial radius", $"{config.InitialRadiusMeters:F2} m");
        KeyValue("Dispersion speed", $"{config.DispersionSpeedMetersPerSecond:F2} m/s");
        KeyValue("Maximum extent", $"{config.MaximumExtentMeters:F1} m");
        KeyValue("Release duration", $"{config.ReleaseDurationSeconds:F0} s");
        KeyValue("Drift", $"{config.DriftDirectionDegrees:F0} deg / {config.DriftBias:F2}");
        KeyValue("Awareness threshold", config.AwarenessConcentration.ToString("F2", CultureInfo.InvariantCulture));
        KeyValue("High-threat threshold", config.DangerConcentration.ToString("F2", CultureInfo.InvariantCulture));

        ImGui.Separator();
        bool showFume = _debug.ShowToxicFume;
        if (ImGui.Checkbox("Show concentration plume", ref showFume))
        {
            _debug.ShowToxicFume = showFume;
        }
        bool showGrid = _debug.ShowToxicGrid;
        if (ImGui.Checkbox("Show fume grid cells", ref showGrid))
        {
            _debug.ShowToxicGrid = showGrid;
        }
        DrawMutedText("Display options change the view without changing the simulation.");
    }

    private void InvalidateToxicFumeForGeometryChange(Simulation simulation)
    {
        if (simulation.ScenarioType != ScenarioType.ToxicFume)
        {
            return;
        }

        if (simulation.ToxicFumeSystem.IsPrecomputed)
        {
            simulation.ToxicFumeSystem.InvalidatePrecomputation();
            _statusMessage = "Scene geometry changed. Toxic-fume dispersion must be recomputed.";
        }
    }
}
