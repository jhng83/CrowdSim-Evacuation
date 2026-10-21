using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using UnityEngine;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Eight bounded draw layers rather than per-agent overlay objects. Flags remain session-owned when panels hide.</summary>
    public sealed class WorkspaceDiagnosticView : MonoBehaviour
    {
        private readonly Dictionary<DiagnosticOverlay, GroundOverlayLayer> _layers = new Dictionary<DiagnosticOverlay, GroundOverlayLayer>();
        private readonly DiagnosticTrails _trails = new DiagnosticTrails();
        private WorkspaceSession _session;
        private DiagnosticFrame _frame;
        private DiagnosticOverlay _flags;
        private int? _hidden;
        private int _tick = -1;
        public void Draw(WorkspaceSession session)
        {
            if (_session != session || session.Tick < _tick) { _trails.Clear(); _frame = null; }
            _session = session; _tick = session.Tick;
            DiagnosticFrame frame = session.Diagnostics;
            int? hidden = session.CameraMode == AgentCameraMode.Pov ? session.SelectedAgentId : null;
            if (ReferenceEquals(frame, _frame) && session.Overlays == _flags && hidden == _hidden) return;
            _trails.Observe(frame, (session.Overlays & DiagnosticOverlay.Trails) != 0);
            foreach (DiagnosticOverlay kind in Enum.GetValues(typeof(DiagnosticOverlay)))
            {
                if (kind == DiagnosticOverlay.None) continue;
                bool enabled = (session.Overlays & kind) != 0;
                if (!_layers.TryGetValue(kind, out GroundOverlayLayer layer))
                { if (!enabled) continue; layer = new GroundOverlayLayer(kind.ToString(), transform); _layers.Add(kind, layer); }
                BlastMeshData data = enabled ? DiagnosticOverlays.Build(frame, kind, _trails, hidden) : null;
                layer.Draw(data, enabled, data == null ? 0 : data.Color.W, data == null ? 0 : data.Color.W);
            }
            _frame = frame; _flags = session.Overlays; _hidden = hidden;
        }
        private void OnDestroy() { foreach (GroundOverlayLayer layer in _layers.Values) layer.Dispose(); }
    }
}
