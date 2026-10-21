using System;
using System.Globalization;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private WorkspaceExplosionView _explosionView;
        private readonly TextField[] _radiusFields = new TextField[5];
        private readonly VisualElement[] _radiusInputs = new VisualElement[5];
        private Label _blastValidation, _blastMetrics, _blastLock;
        private Button _resetRadii, _cancelExplosion;
        private bool _wasRadiiLocked;
        private readonly string[] _zoneNames = { "Kill", "Incapacitation", "Fallen / severe", "Injury", "Panic / threat" };

        private void BuildExplosionInspector()
        {
            Label(_inspectorPage, "Blast zones", "heading");
            Label(_inspectorPage, "Persistent, wall-clipped post-event analysis. Abstract simulator outcomes, not explosive-yield or medical calibration.", "muted");
            Toggle master = new Toggle("Show blast zones") { value = Session.ShowBlastZones, name = "blast-zones" };
            master.RegisterValueChangedCallback(evt => { Session.ShowBlastZones = evt.newValue; _explosionView.Draw(Session); }); _inspectorPage.Add(master);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                Toggle visibility = new Toggle(_zoneNames[i] + " (m)") { value = Session.ZoneVisibility[i], name = "blast-zone-" + i };
                System.Numerics.Vector4 color = BlastGeometry.ZoneColor(i);
                visibility.style.color = Color.black;
                visibility.style.borderLeftColor = new Color(color.X, color.Y, color.Z, 1); visibility.style.borderLeftWidth = 3;
                visibility.RegisterValueChangedCallback(evt => { Session.SetZoneVisible(index, evt.newValue); _explosionView.Draw(Session); });
                _inspectorPage.Add(visibility);
                VisualElement row = Box(_inspectorPage, "radius-row"); _radiusInputs[i] = row;
                Button minus = Button(row, "−0.10", () => AdjustRadius(index, -1)); minus.name = "blast-minus-" + i;
                TextField field = new TextField { value = RadiusText(i), name = "blast-radius-" + i, maxLength = 24,
                    tooltip = "Single-click to place the caret. Enter or click elsewhere commits; Escape restores the committed radius." };
                field.textSelection.selectAllOnFocus = false; field.textSelection.selectAllOnMouseUp = false; field.AddToClassList("radius-text"); row.Add(field); _radiusFields[i] = field;
                field.RegisterCallback<FocusOutEvent>(evt => { if (Session.CanEditBlastRadii && field.value != RadiusText(index)) CommitRadius(index); });
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Escape)
                    {
                        field.SetValueWithoutNotify(RadiusText(index)); field.RemoveFromClassList("invalid-radius"); _blastValidation.text = "Edit cancelled.";
                        evt.StopImmediatePropagation(); evt.PreventDefault();
                    }
                    else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    { CommitRadius(index); evt.StopImmediatePropagation(); evt.PreventDefault(); }
                }, TrickleDown.TrickleDown);
                Button plus = Button(row, "+0.10", () => AdjustRadius(index, 1)); plus.name = "blast-plus-" + i;
            }
            _blastValidation = Label(_inspectorPage, "", "status"); _blastValidation.name = "blast-validation";
            _resetRadii = Button(_inspectorPage, "Reset Zone Defaults", () => { Session.ResetBlastRadii(); ResetRadiusFields(); _operationStatus = ""; Refresh(); });
            _blastLock = Label(_inspectorPage, "", "muted");
            Label(_inspectorPage, "Kill < Incapacitation < Fallen < Injury < Panic. Minimum gap 0.10 m; maximum outer radius 200 m. Enter/click elsewhere commits; Escape cancels.", "muted");
            Toggle shock = new Toggle("Show shock front") { value = Session.ShowShockFront, name = "blast-shock" };
            shock.RegisterValueChangedCallback(evt => { Session.ShowShockFront = evt.newValue; _explosionView.Draw(Session); }); _inspectorPage.Add(shock);
            Toggle dust = new Toggle("Show dust / visibility field") { value = Session.ShowDust, name = "blast-dust" };
            dust.RegisterValueChangedCallback(evt => { Session.ShowDust = evt.newValue; _explosionView.Draw(Session); }); _inspectorPage.Add(dust);
            Label(_inspectorPage, "Dust shading samples temporary loss of clarity behind the shock front. These visibility controls change display only. Shielding uses static walls/blocks, not furniture.", "muted");
            Label(_inspectorPage, "Zone legend", "heading");
            for (int i = 0; i < 5; i++)
            {
                VisualElement row = Box(_inspectorPage, "agent-legend-row");
                VisualElement swatch = Box(row, "agent-legend-swatch"); System.Numerics.Vector4 color = BlastGeometry.ZoneColor(i);
                swatch.style.backgroundColor = new Color(color.X, color.Y, color.Z, 1);
                Label(row, _zoneNames[i], "agent-legend-text");
            }
            Label(_inspectorPage, "Physical outcomes / event analysis", "heading");
            _blastMetrics = Label(_inspectorPage, "", "metrics"); _blastMetrics.name = "blast-metrics";
            UpdateExplosionInspector();
        }
        private string RadiusText(int index) => Session.BlastRadii[index].ToString("0.##", CultureInfo.InvariantCulture);
        private void CommitRadius(int index)
        {
            if (_radiusFields[index].value == RadiusText(index)) return;
            bool success = Session.SetBlastRadius(index, _radiusFields[index].value);
            _operationStatus = ""; _blastValidation.text = Session.Message;
            _radiusFields[index].EnableInClassList("invalid-radius", !success);
            if (success) _radiusFields[index].SetValueWithoutNotify(RadiusText(index));
            Refresh();
        }
        private void AdjustRadius(int index, int direction)
        {
            Session.AdjustBlastRadius(index, direction); ResetRadiusFields(); _operationStatus = ""; Refresh();
        }
        private void ResetRadiusFields()
        {
            for (int i = 0; i < 5; i++) { _radiusFields[i].SetValueWithoutNotify(RadiusText(i)); _radiusFields[i].RemoveFromClassList("invalid-radius"); }
            _blastValidation.text = Session.Message;
        }
        private void UpdateExplosionInspector()
        {
            if (Session.InspectorTab != 1 || Session.Scenario != ScenarioType.Explosion || _blastMetrics == null) return;
            for (int i = 0; i < 5; i++) _radiusInputs[i].SetEnabled(Session.CanEditBlastRadii);
            _resetRadii.SetEnabled(Session.CanEditBlastRadii);
            if (!Session.CanEditBlastRadii)
            {
                for (int i = 0; i < 5; i++)
                { _radiusFields[i].SetValueWithoutNotify(RadiusText(i)); _radiusFields[i].RemoveFromClassList("invalid-radius"); }
                _blastValidation.text = "Detonated: radius editing is locked.";
            }
            else if (_wasRadiiLocked)
            { ResetRadiusFields(); _blastValidation.text = ""; }
            _wasRadiiLocked = !Session.CanEditBlastRadii;
            _blastLock.text = Session.CanEditBlastRadii ? "Radii editable until detonation, including during a run. Pending text is not applied until committed."
                : "Radii are read-only after detonation. Visibility remains adjustable. Replay restores frame zero and unlocks editing.";
            ExplosionFrame frame = Session.Blast;
            _blastMetrics.text = (frame.Detonated ? "Triggered at " + frame.Origin.Value.X.ToString("F2") + ", " + frame.Origin.Value.Y.ToString("F2") + " m\nEvent age " + frame.EventSeconds.ToString("F2") + " s · shock radius " + frame.ShockRadius.ToString("F2") + " m"
                : Session.ExplosionArmed ? "ARMED — click clear floor once" : "Waiting for detonation") +
                "\nShock hits " + frame.ShockHits + " · currently shielded " + frame.Shielded +
                "\nFallen " + frame.Fallen + " · crawling " + frame.Crawling +
                "\nTemporary incap. " + frame.TemporaryIncapacitated + " · incap. " + frame.Incapacitated +
                "\nInjured " + Session.Frame.Injured + " · fatal " + Session.Frame.Dead +
                "\nDebris alive " + frame.DebrisAlive + " · hits " + frame.DebrisHits +
                "\nDebris injuries " + frame.DebrisInjuries + " · incap. " + frame.DebrisIncapacitations + " · fatal " + frame.DebrisFatalities +
                "\nAverage mobile visibility " + frame.AverageVisibility.ToString("F2") +
                "\nFurniture airborne " + System.Linq.Enumerable.Count(Session.Frame.FurnitureBodies, b => b.State == ChairState.Airborne) +
                " · destroyed " + System.Linq.Enumerable.Count(Session.Frame.FurnitureBodies, b => b.State == ChairState.Destroyed);
        }
    }
}
