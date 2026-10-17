using System.Numerics;
using CrowdSim.Core;
using ImGuiNET;

namespace CrowdSim.OpenGL.UI;

public sealed partial class SimulationUi
{
    private BuilderTool SelectedAssetTool => _assetSelection.Selected switch
    {
        BuilderAsset.SquareBlock or BuilderAsset.LargeSquareBlock => BuilderTool.Blockage,
        BuilderAsset.CircularBlock => BuilderTool.CircularBlockage,
        BuilderAsset.RoundTableSet => BuilderTool.RoundTableSet,
        BuilderAsset.SingleChair => BuilderTool.SingleChair,
        BuilderAsset.RectangularTable => BuilderTool.RectangularTable,
        BuilderAsset.Stage => BuilderTool.Stage,
        BuilderAsset.Barricade => BuilderTool.Barricade,
        _ => throw new InvalidOperationException("Unknown asset selection."),
    };

    private void DrawAssetPlacementControls(Simulation simulation)
    {
        ImGui.Spacing();
        ImGui.Text("ASSETS");
        ImGui.TextWrapped("Select an asset, rotate if needed, then click the floor.");
        float cell = simulation.Config.Grid.CellSizeMeters;
        string preview = BuilderAssetSelection.Label(_assetSelection.Selected, cell);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##AssetPicker", preview))
        {
            foreach (BuilderAsset asset in BuilderAssetSelection.Options)
            {
                if (asset == BuilderAsset.RoundTableSet || asset == BuilderAsset.Stage) ImGui.Separator();
                bool selected = _assetSelection.Selected == asset;
                bool unavailable = asset == BuilderAsset.Stage && simulation.Environment.HasStage;
                string label = unavailable ? "Stage - 15 x 6 m (1/1 placed)" : BuilderAssetSelection.Label(asset, cell);
                ImGui.BeginDisabled(unavailable);
                if (ImGui.Selectable(label, selected))
                {
                    _assetSelection.Select(asset);
                    ActivateSelectedAsset(simulation);
                }
                ImGui.EndDisabled();
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.TextWrapped(_assetSelection.Hint);

        bool stageLimit = _assetSelection.Selected == BuilderAsset.Stage && simulation.Environment.HasStage;
        ImGui.BeginDisabled(stageLimit);
        if (_assetSelection.SupportsRotation)
        {
            if (_assetSelection.UsesQuarterTurns)
            {
                bool alongY = ((int)MathF.Round(_assetSelection.RotationDegrees / 90f) & 1) != 0;
                ImGui.Text($"Long side along {(alongY ? "Y" : "X")} | {_assetSelection.RotationDegrees:0} deg");
            }
            else if (_assetSelection.Selected == BuilderAsset.Barricade)
            {
                float degrees = _assetSelection.RotationDegrees;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("##BarricadeAngle", ref degrees, 0f, 359.9f, "Angle: %.1f deg"))
                {
                    _assetSelection.SetRotation(degrees);
                    ActivateSelectedAsset(simulation);
                }
                ImGui.Text("Exact angle (degrees)");
                degrees = _assetSelection.RotationDegrees;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputFloat("##BarricadeExactAngle", ref degrees, 0f, 0f, "%.1f") && float.IsFinite(degrees))
                {
                    _assetSelection.SetRotation(degrees);
                    ActivateSelectedAsset(simulation);
                }
                float buttonWidth = Math.Max(1f, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f);
                if (DrawNeutralButton("-15 deg", new Vector2(buttonWidth, 0f)))
                {
                    _assetSelection.RotateStep(reverse: true);
                    ActivateSelectedAsset(simulation);
                }
                ImGui.SameLine();
                if (DrawNeutralButton("+15 deg", new Vector2(buttonWidth, 0f)))
                {
                    _assetSelection.RotateStep();
                    ActivateSelectedAsset(simulation);
                }
                ImGui.TextWrapped("R: +15 deg. Shift+R: -15 deg. 0 deg runs along X; 90 deg runs along Y.");
            }
            else
            {
                float degrees = _assetSelection.RotationDegrees;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("##AssetFacing", ref degrees, 0f, 359f, "Facing: %.0f deg"))
                {
                    _assetSelection.SetRotation(degrees);
                    ActivateSelectedAsset(simulation);
                }
            }
            if (_assetSelection.Selected != BuilderAsset.Barricade &&
                DrawNeutralButton("Rotate 90 deg (R)", new Vector2(-1f, 0f)))
            {
                _assetSelection.RotateQuarterTurn();
                ActivateSelectedAsset(simulation);
            }
            ImGui.TextWrapped("Rotation changes the preview and next placement, not existing assets.");
        }
        if (_assetSelection.Selected == BuilderAsset.Barricade)
        {
            bool snap = _assetSelection.SnapBarricadeEnds;
            if (ImGui.Checkbox("Snap barricades end-to-end", ref snap))
                _assetSelection.SnapBarricadeEnds = snap;
        }

        DrawActiveButton("Place selected asset", _builderTool == SelectedAssetTool,
            () => ActivateSelectedAsset(simulation));
        ImGui.EndDisabled();
        if (stageLimit)
            ImGui.TextWrapped("Stage already placed (1/1). Select another asset, or undo the stage before replacing it.");
        else
            ImGui.TextWrapped("Green preview: fits. Red: blocked. Click repeatedly to place more; Ctrl+Z undoes the last placement.");
    }

    private void ActivateSelectedAsset(Simulation simulation)
    {
        if (_assetSelection.Selected == BuilderAsset.Stage && simulation.Environment.HasStage) return;
        _builderTool = SelectedAssetTool;
        _statusMessage = $"{BuilderAssetSelection.Label(_assetSelection.Selected, simulation.Config.Grid.CellSizeMeters)} selected. " +
            (_assetSelection.SupportsRotation ? "Set the angle with the controls or R, then click to place." : "Click to place.");
    }

    /// <summary>Native R shortcut: never edits text or rotates a placed object.</summary>
    public void TryRotateSelectedAssetShortcut(Simulation simulation, bool reverse = false)
    {
        if (_mode != AppMode.Builder || !_assetSelection.SupportsRotation ||
            _builderTool != SelectedAssetTool || !_worldCanvasHovered || ImGui.GetIO().WantTextInput ||
            (_assetSelection.Selected == BuilderAsset.Stage && simulation.Environment.HasStage)) return;
        _assetSelection.RotateStep(reverse);
        _statusMessage = $"Placement rotated to {_assetSelection.RotationDegrees:0.0} deg. Existing assets are unchanged.";
    }
}
