using System.Numerics;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core;

public sealed partial class Simulation
{
    /// <summary>
    /// Publish a table's exact current footprint once per outer physics step. Updating only
    /// the touched spatial-hash cells preserves wall, stage-render and fume caches.
    /// </summary>
    private void SetRectangularFurnitureObstacle(RectangularFurnitureBody body, ChairState state)
    {
        body.State = state;
        Wall? previous = body.RectangleObstacle;
        Wall? next = state == ChairState.Destroyed ? null : body.BoundsAt(body.Position);
        if (Equals(previous, next)) return;

        int index = previous is null ? -1 : Environment.Walls.IndexOf(previous);
        if (index >= 0)
        {
            if (next is null) Environment.Walls.RemoveAt(index);
            else Environment.Walls[index] = next;
        }
        else if (next is not null) Environment.Walls.Add(next);

        body.RectangleObstacle = next;
        if (previous is not null) UpdateFurnitureWallIndex(previous, add: false);
        if (next is not null) UpdateFurnitureWallIndex(next, add: true);
        Environment.InvalidateFurnitureNavigation();
    }

    private void UpdateFurnitureWallIndex(Wall wall, bool add)
    {
        float inverse = 1f / _gridCellSize;
        int x1 = (int)MathF.Floor(wall.X1 * inverse), x2 = (int)MathF.Floor(wall.X2 * inverse);
        int y1 = (int)MathF.Floor(wall.Y1 * inverse), y2 = (int)MathF.Floor(wall.Y2 * inverse);
        for (int x = x1; x <= x2; x++)
        for (int y = y1; y <= y2; y++)
        {
            if (!_wallGrid.TryGetValue((x, y), out List<Wall>? bucket))
            {
                if (!add) continue;
                _wallGrid[(x, y)] = bucket = new List<Wall>();
            }
            if (add) bucket.Add(wall);
            else bucket.Remove(wall);
        }
    }

    /// <summary>True footprint contact, not a table's larger broad-phase bounding circle.</summary>
    internal static bool FurnitureOverlapsDisc(FurnitureBody body, Vector2 position,
        Vector2 discCenter, float discRadius)
    {
        if (body is RectangularFurnitureBody rectangle)
        {
            Vector2 closest = Vector2.Clamp(discCenter,
                position - rectangle.HalfExtents, position + rectangle.HalfExtents);
            return Vector2.DistanceSquared(closest, discCenter) < discRadius * discRadius;
        }
        float reach = FurnitureCollisionRadius(body) + discRadius;
        return Vector2.DistanceSquared(position, discCenter) < reach * reach;
    }

    internal static bool FurnitureFootprintsOverlap(FurnitureBody first, Vector2 firstPosition,
        FurnitureBody second, Vector2 secondPosition)
    {
        if (first is RectangularFurnitureBody a && second is RectangularFurnitureBody b)
        {
            Vector2 delta = Vector2.Abs(secondPosition - firstPosition);
            Vector2 reach = a.HalfExtents + b.HalfExtents;
            return delta.X < reach.X && delta.Y < reach.Y;
        }
        if (first is RectangularFurnitureBody)
            return FurnitureOverlapsDisc(first, firstPosition, secondPosition, FurnitureCollisionRadius(second));
        if (second is RectangularFurnitureBody)
            return FurnitureOverlapsDisc(second, secondPosition, firstPosition, FurnitureCollisionRadius(first));
        float radius = FurnitureCollisionRadius(first) + FurnitureCollisionRadius(second);
        return Vector2.DistanceSquared(firstPosition, secondPosition) < radius * radius;
    }

    /// <summary>Contact normal points from the moving body into the contacted body.</summary>
    internal static Vector2 FurnitureContactNormal(FurnitureBody first, Vector2 firstPosition,
        FurnitureBody second, Vector2 secondPosition)
    {
        Vector2 delta = secondPosition - firstPosition;
        if (first is RectangularFurnitureBody a && second is RectangularFurnitureBody b)
        {
            Vector2 penetration = a.HalfExtents + b.HalfExtents - Vector2.Abs(delta);
            return penetration.X <= penetration.Y
                ? new Vector2(delta.X >= 0f ? 1f : -1f, 0f)
                : new Vector2(0f, delta.Y >= 0f ? 1f : -1f);
        }
        if (second is RectangularFurnitureBody rectangle)
            return NormalTowardRectangle(firstPosition, secondPosition, rectangle.HalfExtents);
        if (first is RectangularFurnitureBody movingRectangle)
            return -NormalTowardRectangle(secondPosition, firstPosition, movingRectangle.HalfExtents);
        return VectorMath.SafeNormalize(delta, Vector2.UnitX);
    }

    private static Vector2 NormalTowardRectangle(Vector2 point, Vector2 center, Vector2 half)
    {
        Vector2 closest = Vector2.Clamp(point, center - half, center + half);
        Vector2 normal = closest - point;
        if (normal.LengthSquared() > 1e-10f) return Vector2.Normalize(normal);
        // A body already inside the rectangle resolves toward its nearest face, not
        // an arbitrary diagonal that could keep it sliding along the same obstacle.
        Vector2 delta = center - point;
        Vector2 edgeDistance = half - Vector2.Abs(delta);
        return edgeDistance.X <= edgeDistance.Y
            ? new Vector2(delta.X >= 0f ? 1f : -1f, 0f)
            : new Vector2(0f, delta.Y >= 0f ? 1f : -1f);
    }
}
