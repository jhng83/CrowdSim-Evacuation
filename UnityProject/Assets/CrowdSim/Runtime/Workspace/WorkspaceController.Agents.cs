using System.Collections.Generic;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Workspace
{
    public sealed partial class WorkspaceController
    {
        private sealed class CameraActions
        {
            public VisualElement Root;
            public Label Status;
            public Button Follow, Pov, Clear;
        }
        private readonly List<CameraActions> _cameraActions = new List<CameraActions>();
        private Slider _zoomSlider;
        private Label _cameraBadge;
        private Button _leaveCamera;

        private void BuildCameraActions(VisualElement parent)
        {
            VisualElement card = Box(parent, "camera-card");
            CameraActions actions = new CameraActions { Root = card };
            actions.Status = Label(card, "", "muted");
            actions.Follow = Button(card, "Follow selected agent", () =>
            {
                ChangeCamera(Session.CameraMode == AgentCameraMode.Follow ? AgentCameraMode.Overview : AgentCameraMode.Follow);
            });
            actions.Pov = Button(card, "Enter agent POV", () =>
            {
                ChangeCamera(Session.CameraMode == AgentCameraMode.Pov ? AgentCameraMode.Follow : AgentCameraMode.Pov);
            });
            actions.Clear = Button(card, "Clear selection", () => { Session.SelectAgentById(null); _operationStatus = ""; Refresh(); });
            _cameraActions.Add(actions); UpdateCameraActions();
        }
        private void ChangeCamera(AgentCameraMode mode)
        {
            Session.SetCameraMode(mode);
            _tool = CrowdSim.Workspace.PlacementTool.Select;
            _operationStatus = ""; ClearPreview(); Refresh();
        }
        private void UpdateCameraActions()
        {
            if (Session == null) return;
            string state = Session.CameraMode == AgentCameraMode.Pov ? "Agent POV" : Session.CameraMode == AgentCameraMode.Follow ? "Follow view" : "Room overview";
            string summary = state + (Session.SelectedAgentId.HasValue ? " · Agent " + Session.SelectedAgentId : " · No selection");
            if (_cameraBadge != null) _cameraBadge.text = summary;
            if (_leaveCamera != null)
            {
                _leaveCamera.style.display = Session.CameraMode == AgentCameraMode.Overview ? DisplayStyle.None : DisplayStyle.Flex;
                _leaveCamera.text = Session.CameraMode == AgentCameraMode.Pov ? "Exit POV → Follow" : "Stop following → Overview";
            }
            for (int i = _cameraActions.Count - 1; i >= 0; i--)
            {
                CameraActions actions = _cameraActions[i];
                if (actions.Root.parent == null) { _cameraActions.RemoveAt(i); continue; }
                actions.Status.text = summary;
                actions.Follow.text = Session.CameraMode == AgentCameraMode.Follow ? "Stop following" : "Follow selected agent";
                actions.Pov.text = Session.CameraMode == AgentCameraMode.Pov ? "Exit POV to Follow" : "Enter agent POV";
                actions.Follow.SetEnabled(Session.SelectedAgentId.HasValue);
                actions.Pov.SetEnabled(Session.SelectedAgentId.HasValue);
                actions.Clear.SetEnabled(Session.SelectedAgentId.HasValue);
            }
        }
        private static void BuildAgentLegend(VisualElement parent)
        {
            Foldout legend = new Foldout { text = "Agent states and indicators", value = true }; parent.Add(legend);
            AgentState[] states = { AgentState.Wandering, AgentState.Evacuating, AgentState.WaitingToReact, AgentState.Injured,
                AgentState.Crawling, AgentState.Fallen, AgentState.Incapacitated, AgentState.TempIncap, AgentState.Dead };
            foreach (AgentState state in states)
            {
                VisualElement row = Box(legend, "agent-legend-row");
                VisualElement swatch = Box(row, "agent-legend-swatch");
                System.Numerics.Vector3 rgb = AgentPalette.Color(state);
                swatch.style.backgroundColor = new Color(rgb.X, rgb.Y, rgb.Z);
                string label = state == AgentState.WaitingToReact ? "Amber · Waiting / reaction freeze" : state == AgentState.TempIncap ? "Dark red · Temporary incapacity"
                    : state == AgentState.Incapacitated ? "Dark red · Incapacitated" : state == AgentState.Wandering ? "White · Wandering / normal"
                    : state == AgentState.Evacuating ? "Green · Evacuating" : state == AgentState.Injured ? "Blue · Injured"
                    : state == AgentState.Crawling ? "Yellow · Crawling" : state == AgentState.Fallen ? "Red · Fallen" : "Grey · Fatal casualty";
                Label(row, label, "agent-legend-text");
            }
            Label(legend, "Body colour means physical state. The selected label also names that state; low poses identify casualties. Temporary and permanent incapacity share the native prone pose and colour; inspect the state label to distinguish them.", "muted");
            Label(legend, "White ring + #ID = selected person\nOrange ground ring = panic ≥ 20% (selected label gives %)\n! ALARM = waiting / reaction freeze\nGOING TO HELP + target ID = approaching casualty\n+ HELPING + target ID = timed kneeling assistance", "muted");
        }
    }
}
