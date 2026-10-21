using System;
using System.Collections.Generic;
using CrowdSim.Diagnostics;
using CrowdSim.Workspace;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Transparent ground meshes from detached values. No particle, collider or rendering callback edits simulation state.</summary>
    public sealed class WorkspaceExplosionView : MonoBehaviour
    {
        private readonly Dictionary<string, GroundOverlayLayer> _layers = new Dictionary<string, GroundOverlayLayer>();
        public void Draw(WorkspaceSession session)
        {
            ExplosionFrame frame = session.Blast;
            for (int i = 0; i < 5; i++)
                Display("Blast zone " + i, i < frame.Zones.Count ? frame.Zones[i] : null,
                    session.ShowBlastZones && session.ZoneVisibility[i], .075f, .68f);
            Display("Blast shock", frame.Shock, session.ShowShockFront, .16f, .74f);
            for (int i = 0; i < 8; i++)
                Display("Blast dust " + i, i < frame.Dust.Count ? frame.Dust[i] : null,
                    session.ShowDust, i < frame.Dust.Count ? frame.Dust[i].Color.W : 0, 0);
        }
        private void Display(string name, BlastMeshData data, bool visible, float fillAlpha, float lineAlpha)
        {
            if (!_layers.TryGetValue(name, out GroundOverlayLayer layer))
            {
                if (data == null) return;
                layer = new GroundOverlayLayer(name, transform); _layers.Add(name, layer);
            }
            layer.Draw(data, visible, fillAlpha, lineAlpha);
        }
        private void OnDestroy()
        { foreach (GroundOverlayLayer layer in _layers.Values) layer.Dispose(); }

    }
}
