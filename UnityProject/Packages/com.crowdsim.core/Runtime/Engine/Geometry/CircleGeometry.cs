#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Numerics;

namespace CrowdSim.Core.Geometry
{
    /// <summary>Allocation-free helpers for metric circular obstacle geometry.</summary>
    public static class CircleGeometry
    {
        /// <summary>Conservative axis rejection before expensive distance/normal calculations.
        /// Padding retains boundary rounding; unordered comparisons defer to the exact guard.</summary>
        public static bool CouldTouch(Vector2 point, Vector2 center, float reach)
        {
            float x = point.X - center.X, y = point.Y - center.Y;
            float extent = reach + .0001f;
            return !(x > extent || x < -extent || y > extent || y < -extent);
        }

        public static bool SegmentIntersectsCircle(
            Vector2 start,
            Vector2 end,
            Vector2 center,
            float radius)
        {
            float safeRadius = Math.Max(0f, radius);
            // Cheap conservative rejection is especially useful for short navigation-grid edges.
            if (center.X + safeRadius < Math.Min(start.X, end.X) ||
                center.X - safeRadius > Math.Max(start.X, end.X) ||
                center.Y + safeRadius < Math.Min(start.Y, end.Y) ||
                center.Y - safeRadius > Math.Max(start.Y, end.Y)) return false;
            Vector2 segment = end - start;
            float lengthSquared = segment.LengthSquared();
            if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-8f)
            {
                return Vector2.DistanceSquared(start, center) <= safeRadius * safeRadius;
            }

            float t = Vector2.Dot(center - start, segment) / lengthSquared;
            t = Math.Clamp(t, 0f, 1f);
            Vector2 closest = start + t * segment;
            return Vector2.DistanceSquared(closest, center) <= safeRadius * safeRadius;
        }
    }
}
