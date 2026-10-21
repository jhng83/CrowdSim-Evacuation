using CrowdSim.Workspace;
using UnityEngine;

namespace CrowdSim.Unity.Workspace
{
    /// <summary>Batched cached concentration field. Grid/visibility changes never affect exposure or propagation.</summary>
    public sealed class WorkspaceFumeView : MonoBehaviour
    {
        private readonly GroundOverlayLayer[] _layers = new GroundOverlayLayer[8];
        private GroundOverlayLayer _source;
        public void Draw(WorkspaceSession session)
        {
            var frame = session.Fume;
            if (frame.SourceRing != null && _source == null) _source = new GroundOverlayLayer("Fume source radius", transform);
            _source?.Draw(frame.SourceRing, frame.SourceRing != null, 0, .8f);
            for (int i = 0; i < 8; i++)
            {
                var data = i < frame.Layers.Count ? frame.Layers[i] : null;
                if (data != null && _layers[i] == null) _layers[i] = new GroundOverlayLayer("Fume concentration " + i, transform);
                _layers[i]?.Draw(data, session.ShowFume && frame.CacheReady, data?.Color.W ?? 0, .18f, session.ShowFumeGrid);
            }
        }
        private void OnDestroy()
        {
            _source?.Dispose(); foreach (var layer in _layers) layer?.Dispose();
        }
    }
}
