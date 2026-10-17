using System.Numerics;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core;

public sealed partial class Simulation
{
    private const float FurnitureCellSize = 2f;
    private readonly List<FurnitureBody> _blastFurnitureBodies = new();
    private readonly List<CircleBlockage> _blastStaticCircles = new();
    private readonly List<FurnitureBody> _activeBlastFurniture = new();

    private void ActivateFurniture(FurnitureBody body)
    {
        if (body.PhysicsActiveFrame == FrameIndex || body.State == ChairState.Destroyed) return;
        body.PhysicsActiveFrame = FrameIndex;
        _activeBlastFurniture.Add(body);
    }
    private readonly Dictionary<(int X, int Y), List<FurnitureBody>> _furnitureImpactGrid = new();
    private float _maximumFurnitureRadius;

    internal static float FurnitureCollisionRadius(FurnitureBody body) =>
        body is FurnitureChair && body.State != ChairState.Upright ? 0.40f : body.Radius;

    internal static float FurnitureBodyHeight(FurnitureBody body) => body is FurnitureChair
        ? (body.State == ChairState.Upright ? 0.94f : 0.50f)
        : (body.State == ChairState.Upright ? 0.78f : 0.18f);

    private static (int X, int Y) FurnitureCell(Vector2 point) =>
        ((int)MathF.Floor(point.X / FurnitureCellSize), (int)MathF.Floor(point.Y / FurnitureCellSize));

    private void RebuildFurnitureImpactGrid()
    {
        foreach (List<FurnitureBody> bucket in _furnitureImpactGrid.Values) bucket.Clear();
        _maximumFurnitureRadius = 0f;
        foreach (FurnitureBody body in _blastFurnitureBodies)
        {
            if (body.State == ChairState.Destroyed) continue;
            _maximumFurnitureRadius = Math.Max(_maximumFurnitureRadius,
                body is FurnitureChair ? 0.40f : body.Radius);
            var cell = FurnitureCell(body.Position);
            if (!_furnitureImpactGrid.TryGetValue(cell, out var bucket))
                _furnitureImpactGrid[cell] = bucket = new List<FurnitureBody>();
            bucket.Add(body);
        }
    }

    /// <summary>Reuse the furniture-physics spatial grid for visible short-horizon hazards.</summary>
    internal void CollectMovingFurnitureNear(Vector2 position, float radius, List<FurnitureBody> result)
    {
        result.Clear();
        if (_activeBlastFurniture.Count == 0) return;
        var min = FurnitureCell(position - new Vector2(radius));
        var max = FurnitureCell(position + new Vector2(radius));
        float radiusSquared = radius * radius;
        for (int x = min.X; x <= max.X; x++)
        for (int y = min.Y; y <= max.Y; y++)
        {
            if (!_furnitureImpactGrid.TryGetValue((x, y), out var bucket)) continue;
            foreach (FurnitureBody body in bucket)
            {
                if (body.State == ChairState.Destroyed || body.Velocity.LengthSquared() < 0.25f ||
                    Vector2.DistanceSquared(position, body.Position) > radiusSquared) continue;
                result.Add(body);
            }
        }
    }

    private void UpdateFurnitureImpactCell(FurnitureBody body, Vector2 oldPosition)
    {
        var oldCell = FurnitureCell(oldPosition);
        var newCell = FurnitureCell(body.Position);
        if (oldCell == newCell) return;
        _furnitureImpactGrid[oldCell].Remove(body);
        if (!_furnitureImpactGrid.TryGetValue(newCell, out var bucket))
            _furnitureImpactGrid[newCell] = bucket = new List<FurnitureBody>();
        bucket.Add(body);
    }

    private bool FurnitureHitsStaticGeometry(FurnitureBody body, Vector2 point)
    {
        float radius = FurnitureCollisionRadius(body);
        Vector2 half = body is RectangularFurnitureBody rectangle
            ? rectangle.HalfExtents : new Vector2(radius);
        if (point.X < Environment.BoundaryThickness + half.X ||
            point.Y < Environment.BoundaryThickness + half.Y ||
            point.X > Environment.Width - Environment.BoundaryThickness - half.X ||
            point.Y > Environment.Height - Environment.BoundaryThickness - half.Y) return true;
        CollectNearbyWalls(point, radius, _nearbyWallScratch, _nearbyWallSeenScratch);
        foreach (Wall wall in _nearbyWallScratch)
        {
            // Live rectangular bodies are resolved in the furniture contact grid below.
            // Testing their last-frame proxies here would make a table collide with itself.
            if (wall.IsMovableFurniture) continue;
            if (wall.OrientedFootprint is OrientedRectangle rotated)
            {
                if (body is RectangularFurnitureBody
                    ? rotated.Overlaps(new OrientedRectangle(point, half, 0f))
                    : rotated.Contains(point, radius)) return true;
            }
            else if (point.X >= wall.X1 - half.X && point.X <= wall.X2 + half.X &&
                point.Y >= wall.Y1 - half.Y && point.Y <= wall.Y2 + half.Y) return true;
        }
        foreach (CircleBlockage circle in _blastStaticCircles)
            if (body is RectangularFurnitureBody
                ? FurnitureOverlapsDisc(body, point, circle.Center, circle.Radius)
                : circle.Contains(point, radius)) return true;
        return false;
    }

    /// <summary>Local cylinder contacts: overhead clearance, supported landing, and inelastic impulses.</summary>
    private void ResolveFurnitureImpact(FurnitureBody body, Vector2 oldPosition, float oldHeight,
        ref Vector2 next, ref float nextHeight)
    {
        // A long table can overlap a body more than one grid cell away. Query by
        // combined broad-phase radii, then reject with exact rectangle/circle contacts.
        float searchRadius = FurnitureCollisionRadius(body) + _maximumFurnitureRadius;
        var minimum = FurnitureCell(next - new Vector2(searchRadius));
        var maximum = FurnitureCell(next + new Vector2(searchRadius));
        for (int x = minimum.X; x <= maximum.X; x++)
        for (int y = minimum.Y; y <= maximum.Y; y++)
        {
            if (!_furnitureImpactGrid.TryGetValue((x, y), out var bucket)) continue;
            foreach (FurnitureBody other in bucket)
            {
                if (ReferenceEquals(body, other) || other.State == ChairState.Destroyed) continue;
                float reach = FurnitureCollisionRadius(body) + FurnitureCollisionRadius(other);
                if (Vector2.DistanceSquared(next, other.Position) >= reach * reach ||
                    !FurnitureFootprintsOverlap(body, next, other, other.Position)) continue;
                float otherTop = other.Height + FurnitureBodyHeight(other);
                if (body.VerticalSpeed <= 0f && oldHeight >= otherTop && nextHeight <= otherTop)
                {
                    // Stop downward penetration. The support is checked again on each microstep.
                    nextHeight = otherTop + 0.002f;
                    body.VerticalSpeed = 0f;
                    body.SupportBody = other;
                    body.State = ChairState.Fallen;
                    body.Velocity *= 0.55f;
                    continue;
                }
                if (nextHeight >= otherTop || nextHeight + FurnitureBodyHeight(body) <= other.Height) continue;
                Vector2 normal = body is RectangularFurnitureBody || other is RectangularFurnitureBody
                    ? FurnitureContactNormal(body, next, other, other.Position)
                    : VectorMath.SafeNormalize(other.Position - oldPosition, Vector2.UnitX);
                ApplyFurnitureContactImpulse(body, other, normal,
                    other is FurnitureChair { OccupantId: not null } ||
                    FurnitureHitsStaticGeometry(other, other.Position + normal * 0.02f));
                if (other.Velocity.LengthSquared() >= 0.0004f) ActivateFurniture(other);
                // Never advance through a contact. Already overlapping blast footprints may separate.
                if (Vector2.DistanceSquared(next, other.Position) <=
                    Vector2.DistanceSquared(oldPosition, other.Position)) next = oldPosition;
                if (oldHeight + FurnitureBodyHeight(body) <= other.Height && nextHeight > oldHeight)
                {
                    nextHeight = oldHeight;
                    body.VerticalSpeed = Math.Min(0f, body.VerticalSpeed);
                }
            }
        }
    }

    /// <summary>Normal impulse with restitution 0.15 and a 6:1 table/chair mass ratio.</summary>
    internal static void ApplyFurnitureContactImpulse(FurnitureBody moving, FurnitureBody target,
        Vector2 normal, bool targetPinned)
    {
        float closingSpeed = Vector2.Dot(moving.Velocity - target.Velocity, normal);
        if (closingSpeed <= 0f) return;
        float inverseMovingMass = moving is FurnitureChair ? 1f : 1f / 6f;
        float inverseTargetMass = targetPinned ? 0f : target is FurnitureChair ? 1f : 1f / 6f;
        float impulse = 1.15f * closingSpeed / (inverseMovingMass + inverseTargetMass);
        moving.Velocity -= normal * (impulse * inverseMovingMass);
        target.Velocity += normal * (impulse * inverseTargetMass);
    }
}
