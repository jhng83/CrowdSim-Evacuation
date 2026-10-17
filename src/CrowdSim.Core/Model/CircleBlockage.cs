using System.Numerics;

namespace CrowdSim.Core.Model;

/// <summary>A true circular static blockage on the metric ground plane.</summary>
public sealed record CircleBlockage(Vector2 Center, float Radius)
{
    /// <summary>Furniture proxies share navigation but use dedicated rendering/persistence.</summary>
    public bool IsFurniture { get; init; }

    /// <summary>Runtime seat owner for constant-time pushability lookup; tables have no owner.</summary>
    internal FurnitureChair? ChairOwner { get; init; }

    public const float DefaultDiameterMeters = 2.50f;
    public const float DefaultRadiusMeters = DefaultDiameterMeters / 2f;

    public float Diameter => 2f * Radius;

    public bool Contains(Vector2 point, float margin = 0f)
    {
        float effectiveRadius = Radius + Math.Max(0f, margin);
        return Vector2.DistanceSquared(point, Center) <= effectiveRadius * effectiveRadius;
    }

    public Vector2 ClosestPoint(Vector2 point)
    {
        Vector2 delta = point - Center;
        float distance = delta.Length();
        if (!float.IsFinite(distance) || distance < 1e-6f)
        {
            return Center + new Vector2(Radius, 0f);
        }

        return Center + delta / distance * Radius;
    }
}
