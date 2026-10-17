using System.Numerics;

namespace CrowdSim.Core.Geometry;

/// <summary>
/// Immutable oriented footprint. Axes and broad-phase bounds are computed once at
/// placement, not per pedestrian. Narrow-phase tests use its actual rotated shape.
/// </summary>
public readonly record struct OrientedRectangle
{
    public OrientedRectangle(Vector2 center, Vector2 halfExtents, float rotation)
    {
        if (!VectorMath.IsFinite(center) || !VectorMath.IsFinite(halfExtents) ||
            halfExtents.X <= 0f || halfExtents.Y <= 0f || !float.IsFinite(rotation))
            throw new ArgumentOutOfRangeException(nameof(halfExtents));
        Center = center;
        HalfExtents = halfExtents;
        // Cardinal axes stay exact, including old saved quarter-turn placements.
        float cosine = MathF.Cos(rotation), sine = MathF.Sin(rotation);
        if (MathF.Abs(cosine) < 1e-6f) cosine = 0f;
        if (MathF.Abs(sine) < 1e-6f) sine = 0f;
        AxisX = Vector2.Normalize(new Vector2(cosine, sine));
        AxisY = new Vector2(-AxisX.Y, AxisX.X);
        BoundsHalfExtents = new Vector2(
            MathF.Abs(AxisX.X) * halfExtents.X + MathF.Abs(AxisY.X) * halfExtents.Y,
            MathF.Abs(AxisX.Y) * halfExtents.X + MathF.Abs(AxisY.Y) * halfExtents.Y);
    }

    public Vector2 Center { get; }
    public Vector2 HalfExtents { get; }
    public Vector2 AxisX { get; }
    public Vector2 AxisY { get; }
    public Vector2 BoundsHalfExtents { get; }
    public Vector2 Minimum => Center - BoundsHalfExtents;
    public Vector2 Maximum => Center + BoundsHalfExtents;

    public Vector2 ToLocal(Vector2 point)
    {
        Vector2 delta = point - Center;
        return new Vector2(Vector2.Dot(delta, AxisX), Vector2.Dot(delta, AxisY));
    }

    public Vector2 ToWorld(Vector2 point) => Center + AxisX * point.X + AxisY * point.Y;

    public Vector2 ClosestPoint(Vector2 point)
    {
        Vector2 local = ToLocal(point);
        return ToWorld(Vector2.Clamp(local, -HalfExtents, HalfExtents));
    }

    /// <summary>Point membership, optionally expanded by a circular body's radius.</summary>
    public bool Contains(Vector2 point, float clearance = 0f)
    {
        Vector2 local = ToLocal(point);
        Vector2 outside = Vector2.Max(Vector2.Abs(local) - HalfExtents, Vector2.Zero);
        float margin = Math.Max(0f, clearance);
        return outside.LengthSquared() <= margin * margin;
    }

    /// <summary>Separating-axis test; exact edge contact is not a positive-area overlap.</summary>
    public bool Overlaps(OrientedRectangle other, float tolerance = 0f)
    {
        Vector2 delta = other.Center - Center;
        return OverlapsOnAxis(other, delta, AxisX, tolerance) &&
            OverlapsOnAxis(other, delta, AxisY, tolerance) &&
            OverlapsOnAxis(other, delta, other.AxisX, tolerance) &&
            OverlapsOnAxis(other, delta, other.AxisY, tolerance);
    }

    private bool OverlapsOnAxis(OrientedRectangle other, Vector2 delta, Vector2 axis, float tolerance)
    {
        float radius = HalfExtents.X * MathF.Abs(Vector2.Dot(AxisX, axis)) +
            HalfExtents.Y * MathF.Abs(Vector2.Dot(AxisY, axis));
        float otherRadius = other.HalfExtents.X * MathF.Abs(Vector2.Dot(other.AxisX, axis)) +
            other.HalfExtents.Y * MathF.Abs(Vector2.Dot(other.AxisY, axis));
        return radius + otherRadius - MathF.Abs(Vector2.Dot(delta, axis)) > tolerance;
    }

    /// <summary>
    /// Sweep a disc along a line. Two strips plus four corner discs are the exact
    /// rounded expansion of a rectangle, not its much larger world-aligned bounds.
    /// </summary>
    public bool IntersectsSegment(Vector2 start, Vector2 end, float clearance = 0f)
    {
        Vector2 a = ToLocal(start), b = ToLocal(end);
        float margin = Math.Max(0f, clearance);
        if (!IntersectsBox(a, b, HalfExtents + new Vector2(margin))) return false;
        if (margin == 0f || IntersectsBox(a, b, HalfExtents + new Vector2(margin, 0f)) ||
            IntersectsBox(a, b, HalfExtents + new Vector2(0f, margin))) return true;
        return SegmentTouchesDisc(a, b, HalfExtents, margin) ||
            SegmentTouchesDisc(a, b, -HalfExtents, margin) ||
            SegmentTouchesDisc(a, b, new Vector2(HalfExtents.X, -HalfExtents.Y), margin) ||
            SegmentTouchesDisc(a, b, new Vector2(-HalfExtents.X, HalfExtents.Y), margin);
    }

    private static bool SegmentTouchesDisc(Vector2 a, Vector2 b, Vector2 center, float radius)
    {
        Vector2 delta = b - a;
        float squared = delta.LengthSquared();
        float t = squared > 1e-12f ? Math.Clamp(Vector2.Dot(center - a, delta) / squared, 0f, 1f) : 0f;
        return Vector2.DistanceSquared(a + t * delta, center) <= radius * radius;
    }

    private static bool IntersectsBox(Vector2 a, Vector2 b, Vector2 half)
    {
        Vector2 delta = b - a;
        float near = 0f, far = 1f;
        return ClipAxis(a.X, delta.X, half.X, ref near, ref far) &&
            ClipAxis(a.Y, delta.Y, half.Y, ref near, ref far);
    }

    private static bool ClipAxis(float origin, float direction, float half, ref float near, ref float far)
    {
        if (MathF.Abs(direction) < 1e-8f) return origin >= -half && origin <= half;
        float first = (-half - origin) / direction, last = (half - origin) / direction;
        if (first > last) (first, last) = (last, first);
        near = Math.Max(near, first);
        far = Math.Min(far, last);
        return near <= far;
    }

    /// <summary>Minimum translation to separate a pedestrian disc; zero means no penetration.</summary>
    public Vector2 CircleSeparation(Vector2 center, float radius)
    {
        Vector2 local = ToLocal(center);
        Vector2 closest = Vector2.Clamp(local, -HalfExtents, HalfExtents);
        Vector2 difference = local - closest;
        float squared = difference.LengthSquared();
        if (squared > 1e-12f)
        {
            if (squared >= radius * radius) return Vector2.Zero;
            float distance = MathF.Sqrt(squared);
            Vector2 correction = difference * ((radius - distance + 1e-5f) / distance);
            return AxisX * correction.X + AxisY * correction.Y;
        }
        float xDepth = HalfExtents.X - MathF.Abs(local.X) + radius;
        float yDepth = HalfExtents.Y - MathF.Abs(local.Y) + radius;
        return xDepth < yDepth
            ? AxisX * ((local.X < 0f ? -1f : 1f) * (xDepth + 1e-5f))
            : AxisY * ((local.Y < 0f ? -1f : 1f) * (yDepth + 1e-5f));
    }

    /// <summary>Four safe routing corners, expanded in the rectangle's own coordinate system.</summary>
    public void CopyExpandedCorners(Span<Vector2> corners, float margin)
    {
        if (corners.Length < 4) throw new ArgumentException("Four corners are required.", nameof(corners));
        Vector2 half = HalfExtents + new Vector2(Math.Max(0f, margin));
        corners[0] = ToWorld(-half);
        corners[1] = ToWorld(new Vector2(half.X, -half.Y));
        corners[2] = ToWorld(half);
        corners[3] = ToWorld(new Vector2(-half.X, half.Y));
    }
}
