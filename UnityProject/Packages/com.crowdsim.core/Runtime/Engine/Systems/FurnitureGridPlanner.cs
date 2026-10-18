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
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    /// <summary>
    /// Bounded-degree A* for furniture-heavy rooms. Collision-checked grid edges replace the
    /// quadratic all-pairs visibility graph; smoothed routes still use continuous SFM motion.
    /// </summary>
    internal sealed class FurnitureGridPlanner
    {
        private readonly float CellSize;

        public FurnitureGridPlanner(float cellSize = 0.25f)
        {
            if (!float.IsFinite(cellSize) || cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            CellSize = cellSize;
        }
        private SimulationEnvironment? _environment;
        private int _version = -1;
        private int _gridFrame = -1;
        private int _wallVersion = -1;
        private int _width;
        private int _height;
        private float _clearance;
        private bool[] _blocked = Array.Empty<bool>();
        private byte[] _checkedEdges = Array.Empty<byte>();
        private byte[] _openEdges = Array.Empty<byte>();
        private float[] _costs = Array.Empty<float>();
        private int[] _parents = Array.Empty<int>();
        private bool[] _closed = Array.Empty<bool>();
        private readonly ReferencePriorityQueue<int, (float Cost, int Index)> _queue = new();
        private static readonly (int X, int Y)[] Directions =
            { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };

        /// <summary>Return a body-clear continuous polyline, or no route when connections are blocked.</summary>
        public IReadOnlyList<Vector2> Plan(Simulation simulation, Vector2 start, Vector2 goal, float clearance)
        {
            EnsureGrid(simulation, clearance);
            Array.Fill(_costs, float.PositiveInfinity);
            Array.Fill(_parents, -1);
            Array.Clear(_closed, 0, _closed.Length);
            _queue.Clear();
            List<int> goals = Connections(simulation, goal);
            if (goals.Count == 0) return Array.Empty<Vector2>();
            foreach (int index in Connections(simulation, start))
            {
                _costs[index] = Vector2.Distance(start, Position(index));
                _queue.Enqueue(index, (_costs[index] + Vector2.Distance(Position(index), goal), index));
            }
            int best = -1;
            float bestCost = float.PositiveInfinity;
            while (_queue.TryDequeue(out int current, out var priority))
            {
                if (priority.Cost >= bestCost) break;
                if (_closed[current]) continue;
                _closed[current] = true;
                Vector2 position = Position(current);
                if (goals.Contains(current))
                {
                    float cost = _costs[current] + Vector2.Distance(position, goal);
                    if (cost < bestCost) { bestCost = cost; best = current; }
                }
                int x = current % _width;
                int y = current / _width;
                for (int d = 0; d < Directions.Length; d++)
                {
                    (int dx, int dy) = Directions[d];
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height) continue;
                    int next = ny * _width + nx;
                    if (_blocked[next] || _closed[next]) continue;
                    float cost = _costs[current] + CellSize * (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                    if (cost >= _costs[next]) continue;
                    int mask = 1 << d;
                    if ((_checkedEdges[current] & mask) == 0)
                    {
                        // Inflated raster cells provide cheap broad-phase edge clearance.
                        // The returned continuous route is checked against live geometry below.
                        bool visible = dx == 0 || dy == 0 ||
                            (!_blocked[y * _width + nx] && !_blocked[ny * _width + x]);
                        _checkedEdges[current] |= (byte)mask;
                        _checkedEdges[next] |= (byte)(1 << (7 - d));
                        if (visible)
                        {
                            _openEdges[current] |= (byte)mask;
                            _openEdges[next] |= (byte)(1 << (7 - d));
                        }
                    }
                    if ((_openEdges[current] & mask) == 0) continue;
                    _costs[next] = cost;
                    _parents[next] = current;
                    _queue.Enqueue(next, (cost + Vector2.Distance(Position(next), goal), next));
                }
            }
            if (best < 0) return Array.Empty<Vector2>();
            List<Vector2> path = new() { goal };
            for (int index = best; index >= 0; index = _parents[index]) path.Add(Position(index));
            path.Reverse();
            // Greedy string pulling removes the grid stair-step while retaining swept clearance.
            List<Vector2> smooth = new();
            Vector2 anchor = start;
            for (int i = 0; i < path.Count;)
            {
                int next = Math.Min(path.Count - 1, i + 12);
                while (next > i && !simulation.HasTraversablePath(anchor, path[next], clearance)) next--;
                if (!simulation.HasTraversablePath(anchor, path[next], clearance)) return Array.Empty<Vector2>();
                smooth.Add(path[next]);
                anchor = path[next];
                i = next + 1;
            }
            return smooth;
        }

        private List<int> Connections(Simulation simulation, Vector2 point)
        {
            List<int> result = new(9);
            int cx = (int)MathF.Round(point.X / CellSize), cy = (int)MathF.Round(point.Y / CellSize);
            for (int y = Math.Max(0, cy - 1); y <= Math.Min(_height - 1, cy + 1); y++)
                for (int x = Math.Max(0, cx - 1); x <= Math.Min(_width - 1, cx + 1); x++)
                {
                    int index = y * _width + x;
                    if (!_blocked[index] && simulation.HasTraversablePath(point, Position(index), _clearance)) result.Add(index);
                }
            return result;
        }

        private Vector2 Position(int index) => new((index % _width) * CellSize, (index / _width) * CellSize);

        private void EnsureGrid(Simulation simulation, float clearance)
        {
            SimulationEnvironment environment = simulation.Environment;
            if (ReferenceEquals(environment, _environment) && _version == environment.NavigationLayoutVersion && clearance == _clearance) return;
            // Chair pushes can change layout many times during one agent-update pass.
            // Reuse its raster until the next tick; final segments still use current geometry.
            if (ReferenceEquals(environment, _environment) && _gridFrame == simulation.FrameIndex &&
                _wallVersion == environment.WallLayoutVersion && clearance == _clearance) return;
            _environment = environment;
            _gridFrame = simulation.FrameIndex;
            _wallVersion = environment.WallLayoutVersion;
            _version = environment.NavigationLayoutVersion;
            _clearance = clearance;
            _width = (int)MathF.Ceiling(environment.Width / CellSize) + 1;
            _height = (int)MathF.Ceiling(environment.Height / CellSize) + 1;
            int count = checked(_width * _height);
            if (_blocked.Length != count)
            {
                _blocked = new bool[count]; _checkedEdges = new byte[count]; _openEdges = new byte[count];
                _costs = new float[count]; _parents = new int[count]; _closed = new bool[count];
            }
            Array.Clear(_checkedEdges, 0, _checkedEdges.Length);
            Array.Clear(_openEdges, 0, _openEdges.Length);
            Array.Clear(_blocked, 0, _blocked.Length);
            // Rasterise obstacle bounding boxes instead of testing every room cell against every
            // chair. A toppling chair changes a small footprint, even in a large room.
            foreach (Wall wall in environment.Walls)
            {
                MarkRegion(wall.X1 - clearance, wall.Y1 - clearance, wall.X2 + clearance,
                    wall.Y2 + clearance, null, wall);
            }
            foreach (CircleBlockage circle in environment.CircularBlockages)
            {
                float r = circle.Radius + clearance;
                MarkRegion(circle.Center.X - r, circle.Center.Y - r,
                    circle.Center.X + r, circle.Center.Y + r, circle);
            }
            // The final row/column may lie beyond a non-integral room dimension.
            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    if (x * CellSize > environment.Width || y * CellSize > environment.Height)
                        _blocked[y * _width + x] = true;
        }

        private void MarkRegion(float x1, float y1, float x2, float y2, CircleBlockage? circle, Wall? rectangle = null)
        {
            int minX = Math.Max(0, (int)MathF.Ceiling(x1 / CellSize));
            int minY = Math.Max(0, (int)MathF.Ceiling(y1 / CellSize));
            int maxX = Math.Min(_width - 1, (int)MathF.Floor(x2 / CellSize));
            int maxY = Math.Min(_height - 1, (int)MathF.Floor(y2 / CellSize));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * _width + x;
                    if ((circle is null || circle.Contains(Position(index), _clearance)) &&
                        (rectangle?.OrientedFootprint is null || rectangle.Contains(Position(index), _clearance)))
                        _blocked[index] = true;
                }
        }
    }
}
