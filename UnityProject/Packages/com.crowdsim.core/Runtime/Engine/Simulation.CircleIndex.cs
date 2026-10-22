#nullable enable
using System.Numerics;
using CrowdSim.Core.Geometry;

namespace CrowdSim.Core
{
    public sealed partial class Simulation
    {
        private readonly CircleCandidateIndex _circleIndex = new();
        private bool _circleIndexActive;
        private int _circleIndexVersion, _circleIndexCount;

        // Only a synchronous Step owns the index. Outside Step, public authoring/test code can
        // edit the exposed obstacle list directly, so always inspect live authoritative entries.
        // Each tick invalidates the snapshot; every chair move/removal also changes the existing
        // navigation version and rebuilds before the next query, even within the same tick.
        internal int NextCircleCandidate(Vector2 point, float margin, int start)
        {
            if (!_circleIndexActive || Environment.CircularBlockages.Count < 32) return start;
            if (_circleIndexVersion != Environment.NavigationLayoutVersion ||
                _circleIndexCount != Environment.CircularBlockages.Count)
            {
                _circleIndex.Rebuild(Environment.CircularBlockages);
                _circleIndexVersion = Environment.NavigationLayoutVersion;
                _circleIndexCount = Environment.CircularBlockages.Count;
            }
            return _circleIndex.Next(point, margin, start);
        }
    }
}
