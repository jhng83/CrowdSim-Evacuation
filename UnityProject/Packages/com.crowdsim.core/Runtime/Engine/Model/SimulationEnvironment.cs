#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
// M10 user-authorized stage sizing extension; defaults retain reference behavior.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Numerics;
using CrowdSim.Core.Configuration;

namespace CrowdSim.Core.Model
{
    /// <summary>
    /// Editable room geometry. Boundary walls are rebuilt whenever an exit or blockage is
    /// changed so an exit is a true opening rather than a decorative object.
    /// </summary>
    public sealed partial class SimulationEnvironment
    {
        private readonly SimulationConfig _config;
        private readonly Stack<(string Kind, object Item)> _undoStack = new();

        public SimulationEnvironment(SimulationConfig config)
        {
            _config = config;
            Width = config.World.WidthMeters;
            Height = config.World.LengthMeters;
            RebuildWalls();
        }

        public float Width { get; private set; }
        public float Height { get; private set; }
        public float BoundaryThickness { get; } = 0.60f;
        public List<Exit> Exits { get; } = new();
        public List<Wall> Blockages { get; } = new();
        public List<CircleBlockage> CircularBlockages { get; } = new();
        public List<Wall> BlueWalls { get; } = new();
        public List<AgentSpawnGroup> AgentSpawnGroups { get; } = new();
        public List<Wall> Walls { get; } = new();
        public Explosion? Explosion { get; private set; }

        /// <summary>Monotonic version used by render/navigation caches when static walls change.</summary>
        public int WallLayoutVersion { get; private set; }

        /// <summary>Total number of agents currently planned by mouse placement.</summary>
        public int PlannedAgentCount => AgentSpawnGroups.Sum(group => group.Count) + Furniture.Sum(set => set.Chairs.Count);

        /// <summary>
        /// Room resizing is intentionally limited to an otherwise empty builder. This avoids silently
        /// moving or clipping exits, obstacles and population placements when a smaller room is chosen.
        /// </summary>
        public bool CanResizeRoom =>
            Exits.Count == 0 && Blockages.Count == 0 && CircularBlockages.Count == 0 &&
            BlueWalls.Count == 0 && AgentSpawnGroups.Count == 0 && Furniture.Count == 0;

        public (float Width, float Height) Bounds() => (Width, Height);

        public void SetExplosion(float x, float y, int frame)
            => Explosion = new Explosion(new Vector2(x, y), frame);

        public bool HasExplosion() => Explosion is not null;

        public bool UndoLast()
        {
            if (!_undoStack.TryPop(out (string Kind, object Item) entry))
            {
                return false;
            }

            if (entry.Kind == "furniture" && entry.Item is FurnitureSet furniture)
            {
                Furniture.Remove(furniture);
                if (furniture.TableObstacle is not null) CircularBlockages.Remove(furniture.TableObstacle);
                foreach (FurnitureChair chair in furniture.Chairs)
                    if (chair.Obstacle is not null) CircularBlockages.Remove(chair.Obstacle);
                RebuildWalls();
                return true;
            }

            if (entry.Kind == "exit" && entry.Item is Exit exit)
            {
                Exits.Remove(exit);
                RebuildWalls();
                return true;
            }

            if (entry.Kind == "blockage" && entry.Item is Wall wall)
            {
                Blockages.Remove(wall);
                RebuildWalls();
                return true;
            }

            if (entry.Kind == "circle_blockage" && entry.Item is CircleBlockage circle)
            {
                CircularBlockages.Remove(circle);
                RebuildWalls();
                return true;
            }

            if (entry.Kind == "bluewall" && entry.Item is Wall blueWall)
            {
                BlueWalls.Remove(blueWall);
                RebuildWalls();
                return true;
            }

            if (entry.Kind == "agent_spawn" && entry.Item is AgentSpawnGroup spawnGroup)
            {
                AgentSpawnGroups.Remove(spawnGroup);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Place an exit only on a boundary wall. Exit width is snapped to body diameter;
        /// one-person exits receive the validated 1.2x clearance.
        /// </summary>
        public bool AddExitFromClick(float x, float y, int personsWide)
        {
            string? side = DetectBoundarySide(x, y);
            if (side is null)
            {
                return false;
            }

            float agentDiameter = 2f * _config.Agent.Radius;
            personsWide = Math.Max(1, personsWide);
            float exitWidth = personsWide == 1
                ? agentDiameter * 1.2f
                : personsWide * agentDiameter;
            float halfSize = exitWidth / 2f;
            float thickness = BoundaryThickness;

            float cx;
            float cy;
            string name;

            switch (side)
            {
                case "N":
                    cx = Math.Clamp(x, thickness + halfSize, Width - thickness - halfSize);
                    cy = thickness / 2f;
                    name = $"North_{Exits.Count(e => e.Side == "N") + 1}";
                    break;
                case "S":
                    cx = Math.Clamp(x, thickness + halfSize, Width - thickness - halfSize);
                    cy = Height - thickness / 2f;
                    name = $"South_{Exits.Count(e => e.Side == "S") + 1}";
                    break;
                case "W":
                    cx = thickness / 2f;
                    cy = Math.Clamp(y, thickness + halfSize, Height - thickness - halfSize);
                    name = $"West_{Exits.Count(e => e.Side == "W") + 1}";
                    break;
                default:
                    cx = Width - thickness / 2f;
                    cy = Math.Clamp(y, thickness + halfSize, Height - thickness - halfSize);
                    name = $"East_{Exits.Count(e => e.Side == "E") + 1}";
                    break;
            }

            Exit candidate = new(name, new Vector2(cx, cy), halfSize, side);
            if (ExitOverlapsExisting(candidate) || ExitOverlapsBlockage(candidate))
            {
                return false;
            }

            Exits.Add(candidate);
            _undoStack.Push(("exit", candidate));
            RebuildWalls();
            return true;
        }

        /// <summary>Mirror of Python _snap_point_to_grid.</summary>
        public Vector2 SnapPointToGrid(float x, float y)
        {
            if (!_config.Grid.Enabled)
            {
                return new Vector2(x, y);
            }

            float step = Math.Max(0.10f, _config.Grid.CellSizeMeters);
            float t = BoundaryThickness;
            float sx = t + (MathF.Floor((x - t) / step) + 0.5f) * step;
            float sy = t + (MathF.Floor((y - t) / step) + 0.5f) * step;
            return new Vector2(sx, sy);
        }

        /// <summary>
        /// Placement-only tolerance (0.1 mm). This absorbs single-precision rounding at
        /// touching edges; it never shrinks collision geometry or creates a walkable gap.
        /// </summary>
        internal const float PlacementToleranceMeters = 0.0001f;

        /// <summary>
        /// Compute the exact same footprint used by the preview and placement. Both one-
        /// and two-cell blocks use GRID EDGES, not a common cell-centre grid. Consequently
        /// different-sized blocks can meet flush rather than being half a cell out of phase.
        /// </summary>
        public Wall GetBlockagePlacementBounds(float x, float y, int cells = 1)
        {
            if (!float.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (!float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
            cells = Math.Clamp(cells, 1, 2);
            float step = Math.Max(0.10f, _config.Grid.CellSizeMeters);
            if (!_config.Grid.Enabled)
            {
                float half = cells * step * 0.5f;
                return new Wall(x - half, y - half, x + half, y + half);
            }

            double origin = BoundaryThickness;
            // Odd footprints snap their centre to cell centres, even ones to intersections.
            // Calculate both edges directly from the SAME integer grid coordinates. Avoid
            // centre +/- half-size: adjacent cells could otherwise differ by a float ULP.
            double ix = Math.Floor(((double)x - origin) / step - cells * 0.5 + 0.5);
            double iy = Math.Floor(((double)y - origin) / step - cells * 0.5 + 0.5);
            Wall gridBounds = new((float)(origin + ix * step), (float)(origin + iy * step),
                (float)(origin + (ix + cells) * step), (float)(origin + (iy + cells) * step));
            return SnapToLegacyBlockNeighbours(gridBounds, new Vector2(x, y), cells, step);
        }

        /// <summary>
        /// Old two-cell blocks were centred half a cell out of phase. Leave those saved
        /// blocks untouched, but allow a new neighbour to inherit their edge grid locally.
        /// Only nearby, physically valid face-adjacent positions are eligible.
        /// </summary>
        private Wall SnapToLegacyBlockNeighbours(Wall gridBounds, Vector2 mouse, int cells, float step)
        {
            Wall best = gridBounds;
            float bestSquared = step * step * 0.85f * 0.85f;
            float size = cells * step;
            for (int collection = 0; collection < 2; collection++)
            {
                IReadOnlyList<Wall> blocks = collection == 0 ? Blockages : BlueWalls;
                foreach (Wall other in blocks)
                {
                    bool xAligned = IsPlacementGridEdge(other.X1, step);
                    bool yAligned = IsPlacementGridEdge(other.Y1, step);
                    if (xAligned && yAligned) continue;
                    double ix = Math.Floor(((double)mouse.X - other.X1) / step - cells * 0.5 + 0.5);
                    double iy = Math.Floor(((double)mouse.Y - other.Y1) / step - cells * 0.5 + 0.5);
                    float alignedX = (float)(other.X1 + ix * step);
                    float alignedY = (float)(other.Y1 + iy * step);
                    for (int side = 0; side < 4; side++)
                    {
                        float x1 = side == 0 ? other.X2 : side == 1 ? other.X1 - size : alignedX;
                        float y1 = side == 2 ? other.Y2 : side == 3 ? other.Y1 - size : alignedY;
                        float x2 = side == 1 ? other.X1 : x1 + size;
                        float y2 = side == 3 ? other.Y1 : y1 + size;
                        float contactLength = side < 2
                            ? MathF.Min(y2, other.Y2) - MathF.Max(y1, other.Y1)
                            : MathF.Min(x2, other.X2) - MathF.Max(x1, other.X1);
                        if (contactLength <= PlacementToleranceMeters) continue;
                        float distance = Vector2.DistanceSquared(mouse, new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f));
                        if (distance >= bestSquared) continue;
                        Wall candidate = new(x1, y1, x2, y2);
                        if (!RectangleIsInsideRoom(candidate) || WallOverlapsAny(candidate)) continue;
                        best = candidate;
                        bestSquared = distance;
                    }
                }
            }
            return best;
        }

        private bool IsPlacementGridEdge(float coordinate, float step)
        {
            double index = Math.Round(((double)coordinate - BoundaryThickness) / step);
            return Math.Abs(coordinate - (BoundaryThickness + index * step)) <= PlacementToleranceMeters;
        }

        /// <summary>Validate a square preview without changing the environment or undo stack.</summary>
        public bool CanPlaceBlockageFromClick(float x, float y, int cells = 1)
        {
            if (!float.IsFinite(x) || !float.IsFinite(y)) return false;
            Wall candidate = GetBlockagePlacementBounds(x, y, cells);
            return RectangleIsInsideRoom(candidate) && !WallOverlapsAny(candidate);
        }

        private bool RectangleIsInsideRoom(Wall rectangle) =>
            rectangle.X1 >= BoundaryThickness - PlacementToleranceMeters &&
            rectangle.Y1 >= BoundaryThickness - PlacementToleranceMeters &&
            rectangle.X2 <= Width - BoundaryThickness + PlacementToleranceMeters &&
            rectangle.Y2 <= Height - BoundaryThickness + PlacementToleranceMeters;

        /// <summary>Place a grid-aligned square. Edge/corner contact is allowed; overlap is not.</summary>
        public bool AddBlockageFromClick(float x, float y, int cells = 1)
        {
            if (!CanPlaceBlockageFromClick(x, y, cells)) return false;
            Wall candidate = GetBlockagePlacementBounds(x, y, cells);
            Blockages.Add(candidate);
            _undoStack.Push(("blockage", candidate));
            RebuildWalls();
            return true;
        }

        /// <summary>Place a true circular blockage with a fixed 2.5 m diameter.</summary>
        public bool AddCircularBlockageFromClick(float x, float y)
        {
            const float radius = CircleBlockage.DefaultRadiusMeters;
            Vector2 center = SnapPointToGrid(x, y);
            float innerMargin = BoundaryThickness + radius + 0.05f;
            if (center.X < innerMargin || center.X > Width - innerMargin ||
                center.Y < innerMargin || center.Y > Height - innerMargin)
            {
                return false;
            }

            CircleBlockage candidate = new(center, radius);
            if (CircleOverlapsAny(candidate))
            {
                return false;
            }

            CircularBlockages.Add(candidate);
            _undoStack.Push(("circle_blockage", candidate));
            RebuildWalls();
            return true;
        }

        /// <summary>
        /// Place a fixed blue 2x2 wall tile. "2x2" means two builder grid cells by two builder
        /// grid cells, keeping the element proportional to the existing snapped blockage tool.
        /// </summary>
        public bool AddBlueWallFromClick(float x, float y)
        {
            if (!CanPlaceBlockageFromClick(x, y, cells: 2)) return false;
            Wall candidate = GetBlockagePlacementBounds(x, y, cells: 2);
            BlueWalls.Add(candidate);
            _undoStack.Push(("bluewall", candidate));
            RebuildWalls();
            return true;
        }

        /// <summary>
        /// Add a mouse-positioned population group. Candidates expand outwards from the click in a
        /// deterministic square spiral, skipping solid geometry and already planned agents. This lets
        /// a 50-person placement naturally flow around blocked floor instead of stacking inside it.
        /// </summary>
        public bool AddAgentSpawnGroupFromClick(float x, float y, int count)
        {
            count = Math.Clamp(count, 1, 50);
            if (PlannedAgentCount + count > 5000)
            {
                return false;
            }

            Vector2 center = new(x, y);
            if (center.X <= BoundaryThickness || center.X >= Width - BoundaryThickness ||
                center.Y <= BoundaryThickness || center.Y >= Height - BoundaryThickness)
            {
                return false;
            }

            if (!TryBuildSpawnPositions(center, count, out List<Vector2> positions))
            {
                return false;
            }

            AgentSpawnGroup group = new(center, positions);
            AgentSpawnGroups.Add(group);
            _undoStack.Push(("agent_spawn", group));
            return true;
        }

        /// <summary>Flatten the builder-time spawn groups into deterministic start positions.</summary>
        public List<Vector2> GetPlannedAgentPositions()
        {
            List<Vector2> result = new(PlannedAgentCount);
            foreach (AgentSpawnGroup group in AgentSpawnGroups)
            {
                result.AddRange(group.Positions);
            }

            result.AddRange(Furniture.SelectMany(set => set.Chairs).Select(chair => chair.StartPosition));
            return result;
        }

        /// <summary>Resize an empty builder room while preserving the existing application window.</summary>
        public bool TryResizeRoom(float widthMeters, float lengthMeters)
        {
            if (!CanResizeRoom)
            {
                return false;
            }

            _config.World.ApplySize(widthMeters, lengthMeters);
            Width = _config.World.WidthMeters;
            Height = _config.World.LengthMeters;
            RebuildWalls();
            return true;
        }

        /// <summary>
        /// Replace builder geometry from a validated saved scenario. The undo history is
        /// cleared because imported geometry is treated as the new builder baseline.
        /// </summary>
        public void ReplaceLayout(
            IEnumerable<Exit> exits,
            IEnumerable<Wall> blockages,
            IEnumerable<CircleBlockage>? circularBlockages = null,
            IEnumerable<Wall>? blueWalls = null,
            IEnumerable<AgentSpawnGroup>? agentSpawnGroups = null)
        {
            if (exits is null) throw new ArgumentNullException(nameof(exits));
            if (blockages is null) throw new ArgumentNullException(nameof(blockages));

            Furniture.Clear();
            Exits.Clear();
            Exits.AddRange(exits);
            Blockages.Clear();
            Blockages.AddRange(blockages);
            CircularBlockages.Clear();
            if (circularBlockages is not null)
            {
                CircularBlockages.AddRange(circularBlockages);
            }

            BlueWalls.Clear();
            if (blueWalls is not null)
            {
                BlueWalls.AddRange(blueWalls);
            }

            AgentSpawnGroups.Clear();
            if (agentSpawnGroups is not null)
            {
                AgentSpawnGroups.AddRange(agentSpawnGroups);
            }

            _undoStack.Clear();
            Explosion = null;
            RebuildWalls();
        }

        public bool IsPointBlocked(Vector2 position, float margin = 0f)
        {
            foreach (Wall wall in Walls)
            {
                if (wall.Contains(position, margin))
                {
                    return true;
                }
            }

            foreach (CircleBlockage circle in CircularBlockages)
            {
                if (circle.Contains(position, margin))
                {
                    return true;
                }
            }

            return false;
        }

        public void RebuildWalls()
        {
            Walls.Clear();
            float t = BoundaryThickness;
            Walls.AddRange(BuildHorizontalWallWithOpenings("N", 0f, t, Width));
            Walls.AddRange(BuildHorizontalWallWithOpenings("S", Height - t, Height, Width));
            Walls.AddRange(BuildVerticalWallWithOpenings("W", 0f, t, Height));
            Walls.AddRange(BuildVerticalWallWithOpenings("E", Width - t, Width, Height));
            Walls.AddRange(Blockages);
            Walls.AddRange(BlueWalls);
            foreach (FurnitureSet set in Furniture)
                if (set.StaticObstacle is Wall rectangle) Walls.Add(rectangle);
            WallLayoutVersion++;
        }

        public List<Wall> BuildHorizontalWallWithOpenings(string side, float y1, float y2, float length)
        {
            List<(float Start, float End)> openings = Exits
                .Where(exit => exit.Side == side)
                .Select(exit => (exit.Position.X - exit.Size, exit.Position.X + exit.Size))
                .ToList();

            return SegmentsWithOpenings1D(0f, length, y1, y2, openings, horizontal: true);
        }

        public List<Wall> BuildVerticalWallWithOpenings(string side, float x1, float x2, float length)
        {
            List<(float Start, float End)> openings = Exits
                .Where(exit => exit.Side == side)
                .Select(exit => (exit.Position.Y - exit.Size, exit.Position.Y + exit.Size))
                .ToList();

            return SegmentsWithOpenings1D(0f, length, x1, x2, openings, horizontal: false);
        }

        public static List<Wall> SegmentsWithOpenings1D(
            float axisStart,
            float axisEnd,
            float fixedMin,
            float fixedMax,
            IReadOnlyList<(float Start, float End)> openings,
            bool horizontal)
        {
            if (openings.Count == 0)
            {
                return new List<Wall>
                {
                    horizontal
                        ? new Wall(axisStart, fixedMin, axisEnd, fixedMax)
                        : new Wall(fixedMin, axisStart, fixedMax, axisEnd),
                };
            }

            List<(float Start, float End)> clamped = openings
                .Select(opening => (
                    Start: Math.Clamp(opening.Start, axisStart, axisEnd),
                    End: Math.Clamp(opening.End, axisStart, axisEnd)))
                .Where(opening => opening.End > opening.Start)
                .OrderBy(opening => opening.Start)
                .ToList();

            List<(float Start, float End)> merged = new();
            foreach ((float start, float end) in clamped)
            {
                if (merged.Count == 0)
                {
                    merged.Add((start, end));
                    continue;
                }

                (float lastStart, float lastEnd) = merged[^1];
                if (start <= lastEnd)
                {
                    merged[^1] = (lastStart, Math.Max(lastEnd, end));
                }
                else
                {
                    merged.Add((start, end));
                }
            }

            List<Wall> segments = new();
            float cursor = axisStart;
            foreach ((float start, float end) in merged)
            {
                if (start > cursor)
                {
                    segments.Add(horizontal
                        ? new Wall(cursor, fixedMin, start, fixedMax)
                        : new Wall(fixedMin, cursor, fixedMax, start));
                }

                cursor = Math.Max(cursor, end);
            }

            if (cursor < axisEnd)
            {
                segments.Add(horizontal
                    ? new Wall(cursor, fixedMin, axisEnd, fixedMax)
                    : new Wall(fixedMin, cursor, fixedMax, axisEnd));
            }

            return segments;
        }

        public Wall? FindNearestWall(Vector2 position)
        {
            Wall? nearest = null;
            float minDistanceSquared = float.PositiveInfinity;
            foreach (Wall wall in Walls)
            {
                Vector2 closest = wall.ClosestPoint(position);
                float distanceSquared = Vector2.DistanceSquared(position, closest);
                if (distanceSquared < minDistanceSquared)
                {
                    minDistanceSquared = distanceSquared;
                    nearest = wall;
                }
            }

            return nearest;
        }

        public string? DetectBoundarySide(float x, float y)
        {
            float t = BoundaryThickness;
            const float snap = 0.40f;
            (string Side, float Distance)[] distances =
            {
                ("N", MathF.Abs(y - t / 2f)),
                ("S", MathF.Abs(y - (Height - t / 2f))),
                ("W", MathF.Abs(x - t / 2f)),
                ("E", MathF.Abs(x - (Width - t / 2f))),
            };

            (string side, float best) = distances.OrderBy(pair => pair.Distance).First();
            return best > t / 2f + snap ? null : side;
        }

        public bool ExitOverlapsExisting(Exit candidate)
        {
            foreach (Exit existing in Exits.Where(exit => exit.Side == candidate.Side))
            {
                float a0;
                float a1;
                float b0;
                float b1;
                if (candidate.Side is "N" or "S")
                {
                    a0 = candidate.Position.X - candidate.Size;
                    a1 = candidate.Position.X + candidate.Size;
                    b0 = existing.Position.X - existing.Size;
                    b1 = existing.Position.X + existing.Size;
                }
                else
                {
                    a0 = candidate.Position.Y - candidate.Size;
                    a1 = candidate.Position.Y + candidate.Size;
                    b0 = existing.Position.Y - existing.Size;
                    b1 = existing.Position.Y + existing.Size;
                }

                if (!(a1 < b0 || b1 < a0))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ExitOverlapsBlockage(Exit candidate)
        {
            float size = candidate.Size;
            float x1 = candidate.Position.X - size;
            float y1 = candidate.Position.Y - size;
            float x2 = candidate.Position.X + size;
            float y2 = candidate.Position.Y + size;

            if (Blockages.Concat(BlueWalls).Concat(Furniture
                .Where(set => set.StaticObstacle is not null).Select(set => set.StaticObstacle!)).Any(blockage =>
                blockage.Overlaps(new Wall(x1, y1, x2, y2))))
            {
                return true;
            }

            return CircularBlockages.Any(circle => CircleIntersectsAabb(circle, x1, y1, x2, y2));
        }

        /// <summary>Check candidate against all current walls and exits; edge-touching is allowed.</summary>
        public bool WallOverlapsAny(Wall candidate, Wall? ignored = null)
        {
            foreach (Wall other in Walls)
            {
                if (!ReferenceEquals(other, ignored) && PlacementRectanglesOverlap(candidate, other)) return true;
            }

            foreach (CircleBlockage circle in CircularBlockages)
            {
                if (candidate.OverlapsCircle(circle.Center, circle.Radius))
                {
                    return true;
                }
            }

            foreach (Exit exit in Exits)
            {
                float size = exit.Size;
                float x1 = exit.Position.X - size;
                float y1 = exit.Position.Y - size;
                float x2 = exit.Position.X + size;
                float y2 = exit.Position.Y + size;
                if (PlacementRectanglesOverlap(candidate, new Wall(x1, y1, x2, y2)))
                {
                    return true;
                }
            }

            float agentMargin = _config.Agent.Radius * 1.08f;
            foreach (AgentSpawnGroup group in AgentSpawnGroups)
            {
                foreach (Vector2 position in group.Positions)
                {
                    if (candidate.Contains(position, agentMargin))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>True only for positive-area overlap exceeding placement rounding noise.</summary>
        private static bool PlacementRectanglesOverlap(Wall a, Wall b) =>
            a.Overlaps(b, PlacementToleranceMeters);

        public bool CircleOverlapsAny(CircleBlockage candidate)
        {
            foreach (Wall wall in Walls)
            {
                if (wall.OverlapsCircle(candidate.Center, candidate.Radius))
                {
                    return true;
                }
            }

            foreach (CircleBlockage other in CircularBlockages)
            {
                float combined = candidate.Radius + other.Radius;
                if (Vector2.DistanceSquared(candidate.Center, other.Center) < combined * combined)
                {
                    return true;
                }
            }

            foreach (Exit exit in Exits)
            {
                float size = exit.Size;
                if (CircleIntersectsAabb(candidate,
                    exit.Position.X - size, exit.Position.Y - size,
                    exit.Position.X + size, exit.Position.Y + size))
                {
                    return true;
                }
            }

            float agentMargin = _config.Agent.Radius * 1.08f;
            float exclusion = candidate.Radius + agentMargin;
            float exclusionSquared = exclusion * exclusion;
            foreach (AgentSpawnGroup group in AgentSpawnGroups)
            {
                foreach (Vector2 position in group.Positions)
                {
                    if (Vector2.DistanceSquared(position, candidate.Center) < exclusionSquared)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CircleIntersectsAabb(
            CircleBlockage circle, float x1, float y1, float x2, float y2)
        {
            float closestX = Math.Clamp(circle.Center.X, x1, x2);
            float closestY = Math.Clamp(circle.Center.Y, y1, y2);
            Vector2 delta = circle.Center - new Vector2(closestX, closestY);
            return delta.LengthSquared() < circle.Radius * circle.Radius;
        }

        private bool TryBuildSpawnPositions(Vector2 center, int count, out List<Vector2> positions)
        {
            List<Vector2> result = new(count);
            positions = result;
            float radius = _config.Agent.Radius;
            float spacing = Math.Max(2.25f * radius, 0.05f);
            float minimumDistanceSquared = spacing * spacing * 0.94f;
            float minX = BoundaryThickness + radius + 0.05f;
            float maxX = Width - BoundaryThickness - radius - 0.05f;
            float minY = BoundaryThickness + radius + 0.05f;
            float maxY = Height - BoundaryThickness - radius - 0.05f;

            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            List<Vector2> existing = GetPlannedAgentPositions();
            int maxRing = Math.Max(8, (int)MathF.Ceiling(Math.Max(Width, Height) / spacing) + 2);

            bool TryCandidate(int dx, int dy)
            {
                Vector2 candidate = center + new Vector2(dx * spacing, dy * spacing);
                if (candidate.X < minX || candidate.X > maxX || candidate.Y < minY || candidate.Y > maxY)
                {
                    return false;
                }

                if (IsPointBlocked(candidate, radius * 1.08f))
                {
                    return false;
                }

                foreach (Vector2 other in existing)
                {
                    if (Vector2.DistanceSquared(candidate, other) < minimumDistanceSquared)
                    {
                        return false;
                    }
                }

                foreach (Vector2 other in result)
                {
                    if (Vector2.DistanceSquared(candidate, other) < minimumDistanceSquared)
                    {
                        return false;
                    }
                }

                result.Add(candidate);
                return true;
            }

            TryCandidate(0, 0);
            for (int ring = 1; ring <= maxRing && result.Count < count; ring++)
            {
                for (int dx = -ring; dx <= ring && result.Count < count; dx++)
                {
                    TryCandidate(dx, -ring);
                    if (result.Count >= count) break;
                    TryCandidate(dx, ring);
                }

                for (int dy = -ring + 1; dy <= ring - 1 && result.Count < count; dy++)
                {
                    TryCandidate(-ring, dy);
                    if (result.Count >= count) break;
                    TryCandidate(ring, dy);
                }
            }

            if (result.Count == count)
            {
                return true;
            }

            result.Clear();
            return false;
        }
    }
}
