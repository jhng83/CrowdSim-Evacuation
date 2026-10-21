#nullable enable
using CrowdSim.Diagnostics;
using CrowdSim.Core.Persistence;

namespace CrowdSim.Workspace
{
    public sealed partial class WorkspaceSession
    {
        public static WorkspaceSession ScaleExample(int count, bool furnished, bool hybrid, Core.Model.ScenarioType scenario)
            => new WorkspaceSession(CrowdScaleLayout.Create(count, furnished, hybrid, scenario), count, "Scale " + count + (furnished ? " furnished" : " open"));
        private DiagnosticFrame? _diagnostics;
        public DiagnosticFrame Diagnostics => _diagnostics ?? (_diagnostics = new DiagnosticFrame(_simulation, SelectedAgentId));
        public DiagnosticOverlay Overlays { get; set; }
        public bool CanChangeHybrid => !Running;

        /// <summary>Change approximation only while paused. Rebase just this authored flag on the finalized start.</summary>
        public bool SetPausedHybrid(bool enabled)
        {
            if (Running) { Message = "Pause before changing Hybrid crowd mode."; return false; }
            if (IsBuilding) return SetHybrid(enabled);
            if (_checkpoint != null)
            {
                CrowdSim.Core.Simulation authored = _checkpoint.CreateReadySimulation();
                authored.Config.HybridEnabled = enabled;
                _checkpoint = FinalizedSceneCheckpoint.Capture(authored, PlannedCount, Name);
            }
            _simulation.Config.HybridEnabled = enabled;
            Changed("Hybrid " + (enabled ? "enabled" : "disabled") + ". Individual navigation and contacts remain active in both modes.", true);
            return true;
        }
    }
}
