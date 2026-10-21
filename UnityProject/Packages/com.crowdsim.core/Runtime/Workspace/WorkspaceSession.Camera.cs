#nullable enable
using System;
using System.Linq;
using CrowdSim.Diagnostics;
using CrowdSim.Core.Model;

namespace CrowdSim.Workspace
{
    public sealed partial class WorkspaceSession
    {
        public AgentCameraMode CameraMode { get; private set; }
        public AgentVisualFrame? SelectedVisual => Frame.Agents.FirstOrDefault(a => a.Id == SelectedAgentId);

        /// <summary>Presentation-only camera command. POV exit returns to Follow, as in the reference UI.</summary>
        public bool SetCameraMode(AgentCameraMode mode)
        {
            if (!Enum.IsDefined(typeof(AgentCameraMode), mode)) return false;
            if (mode != AgentCameraMode.Overview && SelectedVisual == null)
            { Message = "Select a runtime person before using Follow or POV."; return false; }
            CameraMode = mode;
            if (mode != AgentCameraMode.Overview) ExplosionArmed = false;
            Message = mode == AgentCameraMode.Pov ? "Agent POV active. Exit POV returns to Follow. Exiting the room ends both automatically."
                : mode == AgentCameraMode.Follow ? "Following agent " + SelectedAgentId + "." : "Room overview active.";
            Revision++;
            return true;
        }

        /// <summary>Accept an ID from detached rendered-volume picking. Selection never opens a hidden inspector.</summary>
        public void SelectAgentById(int? id)
        {
            Agent? selected = id.HasValue ? _simulation.Agents.FirstOrDefault(a => a.AgentId == id && a.IsActive) : null;
            SelectedAgentId = selected?.AgentId;
            if (selected == null) CameraMode = AgentCameraMode.Overview;
            else _inspector.SelectTab(CrowdSim.OpenGL.UI.InspectorTab.Agent);
            Message = selected == null ? "Selection cleared; room overview active." : "Selected agent " + id + ". Open its details for state, Follow and POV.";
            Revision++;
        }

        private bool ValidateCameraSelection()
        {
            if (!SelectedAgentId.HasValue) { CameraMode = AgentCameraMode.Overview; return false; }
            if (_simulation.Agents.Any(a => a.AgentId == SelectedAgentId && a.IsActive)) return false;
            SelectedAgentId = null; CameraMode = AgentCameraMode.Overview;
            return true;
        }
    }
}
