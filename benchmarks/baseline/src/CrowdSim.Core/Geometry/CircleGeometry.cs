using System.Numerics;

namespace CrowdSim.Core.Geometry;

/// <summary>Allocation-free helpers for metric circular obstacle geometry.</summary>
public static class CircleGeometry
{
    public static bool SegmentIntersectsCircle(
        Vector2 start,
        Vector2 end,
        Vector2 center,
        float radius)
    {
        float safeRadius = Math.Max(0f, radius);
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
