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
namespace CrowdSim.Core.Model
{
    public sealed partial class SimulationEnvironment
    {
        private int _furnitureMotionVersion;
        /// <summary>Navigation changes independently of static fume-precompute geometry.</summary>
        public int NavigationLayoutVersion => WallLayoutVersion + _furnitureMotionVersion;
        internal void InvalidateFurnitureNavigation() => _furnitureMotionVersion++;
        /// <summary>Low, open furniture does not seal the simplified floor-plane fume field.</summary>
        public bool IsFumeBlocked(Vector2 p, float margin) =>
            Walls.Any(w => !w.IsFurniture && p.X >= w.X1 - margin && p.X <= w.X2 + margin && p.Y >= w.Y1 - margin && p.Y <= w.Y2 + margin) ||
            CircularBlockages.Any(c => !c.IsFurniture && c.Contains(p, margin));

        public List<FurnitureSet> Furniture { get; } = new();

        /// <summary>At most one authored stage is allowed in a scenario, including restored layouts.</summary>
        public bool HasStage => Furniture.Any(set => set.Kind == FurnitureKind.Stage);

        /// <summary>Validate exact rectangular footprints, or the existing seated-set clearance.</summary>
        public bool CanPlaceFurniture(FurnitureKind kind, Vector2 center, float rotation = 0f)
        {
            if (!Enum.IsDefined(typeof(FurnitureKind), kind) || !float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(rotation)) return false;
            if (kind == FurnitureKind.Stage && HasStage) return false;
            if (PlannedAgentCount + FurnitureSet.GetSeatCount(kind) > 5000) return false;
            FurnitureSet set = new(kind, center, rotation);
            if (set.StaticObstacle is Wall rectangle)
            {
                if (!RectangleIsInsideRoom(rectangle) || WallOverlapsAny(rectangle)) return false;
                // Preserve the departure reservation of occupied chairs/table sets.
                foreach (FurnitureSet other in Furniture)
                {
                    if (other.StaticObstacle is null && rectangle.OverlapsCircle(other.Center, other.PlacementRadius)) return false;
                }
                return true;
            }

            float r = set.PlacementRadius;
            if (center.X - r < BoundaryThickness || center.Y - r < BoundaryThickness ||
                center.X + r > Width - BoundaryThickness || center.Y + r > Height - BoundaryThickness) return false;
            if (CircleOverlapsAny(new CircleBlockage(center, r))) return false;
            if (Furniture.Any(other => other.StaticObstacle is null &&
                Vector2.Distance(other.Center, center) < other.PlacementRadius + r)) return false;
            return !GetPlannedAgentPositions().Any(p => Vector2.Distance(p, center) < r + _config.Agent.Radius);
        }

        /// <summary>
        /// Optional builder-only end-to-end snap for parallel road barricades. Nearby valid
        /// endpoints win; distant, perpendicular or obstructed endpoints never pull the cursor.
        /// Does not mutate the layout. The same result is used for preview and commit.
        /// </summary>
        public Vector2 SnapBarricadePlacement(Vector2 requested, float rotation, float snapDistance = 0.30f)
        {
            if (!float.IsFinite(requested.X) || !float.IsFinite(requested.Y) ||
                !float.IsFinite(rotation) || !float.IsFinite(snapDistance) || snapDistance <= 0f)
                return requested;
            float angle = FurnitureSet.NormalizeRotation(rotation);
            Vector2 requestedAxis = new(MathF.Cos(angle), MathF.Sin(angle));
            float bestSquared = snapDistance * snapDistance;
            Vector2 best = requested;
            foreach (FurnitureSet other in Furniture)
            {
                if (other.Kind != FurnitureKind.Barricade) continue;
                Vector2 axis = other.StaticObstacle!.OrientedFootprint!.Value.AxisX;
                // Opposite facing is still parallel. Do not change the user's chosen angle.
                float cross = axis.X * requestedAxis.Y - axis.Y * requestedAxis.X;
                if (MathF.Abs(cross) > 1e-5f) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 candidate = other.Center + axis * (side * FurnitureSet.BarricadeLength);
                    float squared = Vector2.DistanceSquared(requested, candidate);
                    if (squared > bestSquared ||
                        !CanPlaceFurniture(FurnitureKind.Barricade, candidate, angle)) continue;
                    bestSquared = squared;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>One undo item owns its table, chairs and all seated occupants.</summary>
        public bool AddFurniture(FurnitureKind kind, Vector2 center, float rotation = 0f)
        {
            if (!CanPlaceFurniture(kind, center, rotation)) return false;
            FurnitureSet set = new(kind, center, rotation);
            InsertFurniture(set);
            return true;
        }

        /// <summary>Restore actual saved footprints without reapplying the builder's empty-space reservation.</summary>
        internal bool RestoreFurniture(FurnitureKind kind, Vector2 center, float rotation)
        {
            if (!Enum.IsDefined(typeof(FurnitureKind), kind) || !float.IsFinite(center.X) || !float.IsFinite(center.Y) ||
                !float.IsFinite(rotation) || (kind == FurnitureKind.Stage && HasStage)) return false;
            FurnitureSet set = new(kind, center, rotation);
            if (set.StaticObstacle is Wall rectangle)
            {
                if (!RectangleIsInsideRoom(rectangle) || WallOverlapsAny(rectangle)) return false;
                InsertFurniture(set);
                return true;
            }
            IEnumerable<CircleBlockage> footprints = set.Chairs.Select(chair => chair.Obstacle!);
            if (set.TableObstacle is not null) footprints = footprints.Prepend(set.TableObstacle);
            foreach (CircleBlockage circle in footprints)
            {
                if (circle.Center.X - circle.Radius < BoundaryThickness ||
                    circle.Center.Y - circle.Radius < BoundaryThickness ||
                    circle.Center.X + circle.Radius > Width - BoundaryThickness ||
                    circle.Center.Y + circle.Radius > Height - BoundaryThickness || CircleOverlapsAny(circle))
                    return false;
            }
            InsertFurniture(set);
            return true;
        }

        private void InsertFurniture(FurnitureSet set)
        {
            Furniture.Add(set);
            if (set.TableObstacle is not null) CircularBlockages.Add(set.TableObstacle);
            foreach (FurnitureChair chair in set.Chairs) CircularBlockages.Add(chair.Obstacle!);
            _undoStack.Push(("furniture", set));
            RebuildWalls();
        }

        /// <summary>Ignore only the attached chair for its seated occupant.</summary>
        public bool IsSeatStart(Vector2 position) => Furniture.SelectMany(set => set.Chairs)
            .Any(chair => Vector2.DistanceSquared(chair.StartPosition, position) < 0.000001f);
    }
}
