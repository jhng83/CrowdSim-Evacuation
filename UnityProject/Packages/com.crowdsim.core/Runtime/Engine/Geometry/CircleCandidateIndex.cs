#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Geometry
{
    /// <summary>Conservative circular-obstacle broad phase. Candidates retain source-list order;
    /// callers still perform the original exact geometry/contact tests.</summary>
    public sealed class CircleCandidateIndex
    {
        private const float CellSize = 2f;
        private const float Padding = .001f;
        private readonly Dictionary<(int X, int Y), List<int>> _cells = new();
        private readonly List<List<int>> _used = new();
        private int _count;
        private bool _linear;

        /// <summary>Rebuild after geometry or ordering changes. Existing buckets are reused.</summary>
        public void Rebuild(IReadOnlyList<CircleBlockage> circles)
        {
            foreach (List<int> bucket in _used) bucket.Clear();
            _used.Clear(); _count = circles.Count; _linear = _count < 32;
            if (_linear) return;
            for (int i = 0; i < circles.Count; i++)
            {
                CircleBlockage circle = circles[i];
                float extent = circle.Radius + Padding;
                if (circle.Radius < 0f || !Valid(circle.Center, extent)) { _linear = true; return; }
                int minX = Cell(circle.Center.X - extent), maxX = Cell(circle.Center.X + extent);
                int minY = Cell(circle.Center.Y - extent), maxY = Cell(circle.Center.Y + extent);
                // Pathological/imported geometry uses the authoritative scan rather than an
                // unbounded grid allocation. Normal room/table/chair geometry is much smaller.
                if ((long)(maxX - minX + 1) * (maxY - minY + 1) > 4096) { _linear = true; return; }
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                    {
                        if (!_cells.TryGetValue((x, y), out List<int>? bucket))
                        { bucket = new List<int>(8); _cells.Add((x, y), bucket); }
                        if (bucket.Count == 0) _used.Add(bucket);
                        bucket.Add(i);
                    }
            }
        }

        /// <summary>Return the first possible contact at or after start, or the source count.
        /// Re-querying after each positional correction preserves ordered chain collisions:
        /// newly reachable later obstacles are included, earlier ones are not revisited.</summary>
        public int Next(Vector2 point, float margin, int start)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (start >= _count) return _count;
            if (_linear || !Valid(point, margin)) return start;
            float extent = margin + Padding;
            int minX = Cell(point.X - extent), maxX = Cell(point.X + extent);
            int minY = Cell(point.Y - extent), maxY = Cell(point.Y + extent);
            if ((long)(maxX - minX + 1) * (maxY - minY + 1) > 4096) return start;
            int best = _count;
            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    if (!_cells.TryGetValue((x, y), out List<int>? bucket) || bucket.Count == 0) continue;
                    int low = 0, high = bucket.Count;
                    while (low < high)
                    {
                        int middle = low + (high - low) / 2;
                        if (bucket[middle] < start) low = middle + 1; else high = middle;
                    }
                    if (low < bucket.Count && bucket[low] < best) best = bucket[low];
                }
            return best;
        }

        private static int Cell(float value) => (int)MathF.Floor(value / CellSize);
        private static bool Valid(Vector2 point, float extent) => float.IsFinite(point.X) && float.IsFinite(point.Y) &&
            float.IsFinite(extent) && extent >= 0f && MathF.Abs(point.X) + extent < 100000f &&
            MathF.Abs(point.Y) + extent < 100000f;
    }
}
