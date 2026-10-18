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
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    /// <summary>Shared reverse-Dijkstra exit fields with locally updated obstacle occupancy.</summary>
    public sealed class SharedExitFields
    {
        private const float Cell = 0.5f;
        private readonly Dictionary<CircleBlockage, bool> _circles = new();
        private readonly HashSet<Wall> _movingRectangles = new();
        private readonly HashSet<Wall> _currentRectangles = new();
        private readonly List<Wall> _removedRectangles = new();
        private readonly Dictionary<Vector2, float[]> _fields = new();
        private readonly ReferencePriorityQueue<int, float> _queue = new();
        private int[] _occupancy = Array.Empty<int>();
        private int _width, _height, _walls = -1, _navigation = -1, _refreshFrame = -1;
        private SimulationEnvironment? _environment;
        public int FieldBuilds { get; private set; }
        public int GeometryRefreshes { get; private set; }
        private static readonly (int X, int Y)[] Directions =
            { (-1,-1), (0,-1), (1,-1), (-1,0), (1,0), (-1,1), (0,1), (1,1) };

        public bool TryDirection(Simulation sim, Vector2 position, Vector2 goal, out Vector2 direction)
            => TryDirection(sim, position, goal, out direction, out _);

        /// <summary>Return direction plus remaining field cost for progress monitoring.</summary>
        public bool TryDirection(Simulation sim, Vector2 position, Vector2 goal,
            out Vector2 direction, out float remainingCost)
        {
            direction = Vector2.Zero;
            remainingCost = float.PositiveInfinity;
            EnsureGeometry(sim);
            if (!_fields.TryGetValue(goal, out float[]? costs))
            {
                costs = Build(sim, goal);
                _fields[goal] = costs;
            }
            int cx = (int)MathF.Round(position.X / Cell), cy = (int)MathF.Round(position.Y / Cell);
            float best = float.PositiveInfinity;
            // Sample several nearby cells so an arbitrary continuous position can connect to the field.
            for (int y = Math.Max(0, cy - 2); y <= Math.Min(_height - 1, cy + 2); y++)
                for (int x = Math.Max(0, cx - 2); x <= Math.Min(_width - 1, cx + 2); x++)
                {
                    int index = y * _width + x;
                    if (!float.IsFinite(costs[index])) continue;
                    Vector2 point = Point(index);
                    float distance = Vector2.Distance(position, point);
                    if (distance < 0.05f) continue;
                    // Prefer forward progress when several cells lie on the same shortest path.
                    float score = costs[index] + 0.9f * distance;
                    if (score >= best || !sim.HasTraversablePath(position, point, sim.Config.Agent.Radius * 1.08f)) continue;
                    best = score;
                    direction = VectorMath.SafeNormalize(point - position);
                }
            remainingCost = best;
            return float.IsFinite(best);
        }

        private void EnsureGeometry(Simulation sim)
        {
            SimulationEnvironment env = sim.Environment;
            bool reset = !ReferenceEquals(env, _environment) || _walls != env.WallLayoutVersion;
            if (!reset && (_navigation == env.NavigationLayoutVersion || sim.FrameIndex < _refreshFrame)) return;
            if (reset)
            {
                _environment = env;
                _walls = env.WallLayoutVersion;
                _width = (int)MathF.Ceiling(env.Width / Cell) + 1;
                _height = (int)MathF.Ceiling(env.Height / Cell) + 1;
                _occupancy = new int[_width * _height];
                _circles.Clear();
                _movingRectangles.Clear();
                float margin = sim.Config.Agent.Radius * 1.08f;
                foreach (Wall wall in env.Walls)
                    if (!wall.IsMovableFurniture)
                        MarkRectangle(wall, 1, margin);
                for (int i = 0; i < _occupancy.Length; i++)
                {
                    Vector2 point = Point(i);
                    if (point.X < margin || point.Y < margin || point.X > env.Width - margin || point.Y > env.Height - margin)
                        _occupancy[i]++;
                }
            }
            float clearance = sim.Config.Agent.Radius * 1.08f;
            // Moving rectangles invalidate only their old/new occupied cells. They must not
            // remain baked into static occupancy after being displaced or destroyed.
            _currentRectangles.Clear();
            foreach (FurnitureSet set in env.Furniture)
                if (set.Table is RectangularFurnitureBody { RectangleObstacle: Wall bounds })
                    _currentRectangles.Add(bounds);
            _removedRectangles.Clear();
            foreach (Wall previous in _movingRectangles)
                if (!_currentRectangles.Contains(previous)) _removedRectangles.Add(previous);
            foreach (Wall previous in _removedRectangles)
            {
                MarkRectangle(previous, -1, clearance);
                _movingRectangles.Remove(previous);
            }
            foreach (Wall added in _currentRectangles)
                if (_movingRectangles.Add(added)) MarkRectangle(added, 1, clearance);
            HashSet<CircleBlockage> current = new(env.CircularBlockages);
            foreach (CircleBlockage removed in _circles.Keys.Where(c => !current.Contains(c)).ToArray())
            {
                MarkCircle(removed, -1, clearance);
                _circles.Remove(removed);
            }
            foreach (CircleBlockage added in current)
                if (!_circles.ContainsKey(added)) { MarkCircle(added, 1, clearance); _circles.Add(added, true); }
            _navigation = env.NavigationLayoutVersion;
            _refreshFrame = sim.FrameIndex + Math.Max(1, (int)MathF.Ceiling(0.6f / sim.Config.Agent.Dt));
            _fields.Clear();
            GeometryRefreshes++;
        }

        private void MarkRectangle(Wall rectangle, int delta, float margin) =>
            Raster(rectangle.X1 - margin, rectangle.Y1 - margin,
                rectangle.X2 + margin, rectangle.Y2 + margin, delta, null, margin, rectangle);

        private void MarkCircle(CircleBlockage circle, int delta, float margin)
        {
            float r = circle.Radius + margin;
            Raster(circle.Center.X - r, circle.Center.Y - r, circle.Center.X + r, circle.Center.Y + r, delta, circle, margin);
        }

        private void Raster(float x1, float y1, float x2, float y2, int delta, CircleBlockage? circle, float margin, Wall? rectangle = null)
        {
            for (int y = Math.Max(0, (int)MathF.Ceiling(y1 / Cell)); y <= Math.Min(_height - 1, (int)MathF.Floor(y2 / Cell)); y++)
                for (int x = Math.Max(0, (int)MathF.Ceiling(x1 / Cell)); x <= Math.Min(_width - 1, (int)MathF.Floor(x2 / Cell)); x++)
                {
                    int index = y * _width + x;
                    if ((circle is null || circle.Contains(Point(index), margin)) &&
                        (rectangle?.OrientedFootprint is null || rectangle.Contains(Point(index), margin)))
                        _occupancy[index] += delta;
                }
        }

        private float[] Build(Simulation sim, Vector2 goal)
        {
            FieldBuilds++;
            float[] costs = new float[_occupancy.Length];
            Array.Fill(costs, float.PositiveInfinity);
            _queue.Clear();
            int gx = (int)MathF.Round(goal.X / Cell), gy = (int)MathF.Round(goal.Y / Cell);
            for (int y = Math.Max(0, gy - 2); y <= Math.Min(_height - 1, gy + 2); y++)
                for (int x = Math.Max(0, gx - 2); x <= Math.Min(_width - 1, gx + 2); x++)
                {
                    int index = y * _width + x;
                    if (_occupancy[index] != 0 || !sim.HasTraversablePath(Point(index), goal, sim.Config.Agent.Radius * 1.08f)) continue;
                    costs[index] = Vector2.Distance(Point(index), goal);
                    _queue.Enqueue(index, costs[index]);
                }
            while (_queue.TryDequeue(out int index, out float cost))
            {
                if (cost > costs[index]) continue;
                int x = index % _width, y = index / _width;
                foreach (var (dx, dy) in Directions)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height) continue;
                    int next = ny * _width + nx;
                    if (_occupancy[next] != 0 || (dx != 0 && dy != 0 && (_occupancy[y * _width + nx] != 0 || _occupancy[ny * _width + x] != 0))) continue;
                    float nextCost = cost + Cell * (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                    if (nextCost >= costs[next]) continue;
                    costs[next] = nextCost; _queue.Enqueue(next, nextCost);
                }
            }
            return costs;
        }

        private Vector2 Point(int index) => new(index % _width * Cell, index / _width * Cell);
    }
}
