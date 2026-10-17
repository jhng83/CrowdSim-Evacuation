using System.Numerics;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems;

/// <summary>
/// Small deterministic A* visibility-graph planner for rectangular and circular builder obstacles.
/// The graph is deliberately limited to static geometry: it tells an agent which side of a
/// blockage is body-clear, while SFM continues to model local people, contacts and congestion.
/// </summary>
public sealed class VisibilityGraphPlanner
{
    private readonly AgentConfig _config;
    private readonly FurnitureGridPlanner _furniturePlanner = new();
    private readonly FurnitureGridPlanner _fineFurniturePlanner = new(0.125f);
    private readonly List<Vector2> _cachedNodes = new(128);
    private readonly List<List<(int To, float Cost)>> _cachedGraph = new(128);
    private readonly PriorityQueue<int, (float Score, int Index)> _openQueue = new();
    private bool[] _startVisible = Array.Empty<bool>();
    private bool[] _goalVisible = Array.Empty<bool>();
    private float[] _startCost = Array.Empty<float>();
    private float[] _goalCost = Array.Empty<float>();
    private float[] _gScoreScratch = Array.Empty<float>();
    private int[] _cameFromScratch = Array.Empty<int>();
    private bool[] _closedScratch = Array.Empty<bool>();
    private int _cachedLayoutVersion = -1;

    public VisibilityGraphPlanner(AgentConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Runtime path planner that caches the static obstacle-corner visibility graph for the
    /// current layout. Each pedestrian then connects only its dynamic start/goal to that graph,
    /// avoiding a full O(corners^2) graph rebuild for every route request.
    /// </summary>
    public IReadOnlyList<Vector2> Plan(
        Simulation simulation,
        Vector2 start,
        Vector2 goal)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        float clearance = NavigationClearance();
        if (simulation.HasTraversablePath(start, goal, clearance))
        {
            return new[] { goal };
        }

        int furnitureObstacleCount = simulation.Environment.CircularBlockages.Count +
            simulation.Environment.Furniture.Count(set => set.StaticObstacle is not null);
        if (simulation.Environment.Furniture.Count > 0 && furnitureObstacleCount >= 24)
        {
            // Embedded endpoints have no body-clear connection in either planner.
            if (simulation.Environment.IsPointBlocked(start, clearance) ||
                simulation.Environment.IsPointBlocked(goal, clearance)) return Array.Empty<Vector2>();
            IReadOnlyList<Vector2> gridRoute = _furniturePlanner.Plan(simulation, start, goal, clearance);
            if (gridRoute.Count > 0) return gridRoute;
            // Never build a quadratic all-pairs furniture graph after a coarse-grid miss.
            // A finer grid handles smaller gaps and still validates every returned segment.
            return _fineFurniturePlanner.Plan(simulation, start, goal, clearance);
        }

        EnsureStaticGraph(simulation, clearance);
        int staticCount = _cachedNodes.Count;
        if (staticCount == 0)
        {
            return Array.Empty<Vector2>();
        }

        EnsureScratchCapacity(staticCount);
        for (int i = 0; i < staticCount; i++)
        {
            Vector2 node = _cachedNodes[i];
            bool startVisible = simulation.HasTraversablePath(start, node, clearance);
            bool goalVisible = simulation.HasTraversablePath(node, goal, clearance);
            _startVisible[i] = startVisible;
            _goalVisible[i] = goalVisible;
            _startCost[i] = startVisible ? Vector2.Distance(start, node) : float.PositiveInfinity;
            _goalCost[i] = goalVisible ? Vector2.Distance(node, goal) : float.PositiveInfinity;
        }

        // Dynamic indices deliberately mirror the original graph ordering: start=0, goal=1,
        // cached obstacle corners begin at index 2. This preserves deterministic tie-breaking.
        int nodeCount = staticCount + 2;
        Array.Fill(_gScoreScratch, float.PositiveInfinity, 0, nodeCount);
        Array.Fill(_cameFromScratch, -1, 0, nodeCount);
        Array.Fill(_closedScratch, false, 0, nodeCount);
        _openQueue.Clear();

        const int startIndex = 0;
        const int goalIndex = 1;
        _gScoreScratch[startIndex] = 0f;
        float startHeuristic = Vector2.Distance(start, goal);
        _openQueue.Enqueue(startIndex, (startHeuristic, startIndex));

        while (_openQueue.Count > 0)
        {
            int current = _openQueue.Dequeue();
            if (_closedScratch[current])
            {
                continue;
            }

            if (current == goalIndex)
            {
                return ReconstructCachedPath(_cameFromScratch, goalIndex, staticCount, goal);
            }

            _closedScratch[current] = true;
            if (current == startIndex)
            {
                for (int i = 0; i < staticCount; i++)
                {
                    if (_startVisible[i])
                    {
                        RelaxCachedEdge(
                            startIndex,
                            i + 2,
                            _startCost[i],
                            goal,
                            staticCount);
                    }
                }

                continue;
            }

            int staticIndex = current - 2;
            if (staticIndex < 0 || staticIndex >= staticCount)
            {
                continue;
            }

            foreach ((int to, float cost) in _cachedGraph[staticIndex])
            {
                RelaxCachedEdge(current, to + 2, cost, goal, staticCount);
            }

            if (_goalVisible[staticIndex])
            {
                RelaxCachedEdge(current, goalIndex, _goalCost[staticIndex], goal, staticCount);
            }
        }

        return Array.Empty<Vector2>();
    }

    private void EnsureStaticGraph(Simulation simulation, float clearance)
    {
        int layoutVersion = simulation.Environment.NavigationLayoutVersion;
        if (layoutVersion == _cachedLayoutVersion)
        {
            return;
        }

        _cachedLayoutVersion = layoutVersion;
        _cachedNodes.Clear();
        _cachedGraph.Clear();
        SimulationEnvironment environment = simulation.Environment;
        float cornerOffset = clearance + Math.Max(0.02f, _config.RouteCornerBuffer);
        float innerMargin = environment.BoundaryThickness + clearance + 0.02f;

        Span<Vector2> corners = stackalloc Vector2[4];
        foreach (Wall blockage in environment.Blockages.Concat(environment.BlueWalls)
            .Concat(environment.Furniture.Where(set => set.StaticObstacle is not null)
                .Select(set => set.StaticObstacle!)))
        {
            blockage.CopyExpandedCorners(corners, cornerOffset);

            foreach (Vector2 candidate in corners)
            {
                if (candidate.X <= innerMargin || candidate.Y <= innerMargin ||
                    candidate.X >= environment.Width - innerMargin ||
                    candidate.Y >= environment.Height - innerMargin ||
                    environment.IsPointBlocked(candidate, 0.25f * clearance))
                {
                    continue;
                }

                bool duplicate = false;
                foreach (Vector2 existing in _cachedNodes)
                {
                    if (Vector2.DistanceSquared(existing, candidate) < 0.0004f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    _cachedNodes.Add(candidate);
                }
            }
        }

        AddCircleWaypoints(environment, clearance, innerMargin, _cachedNodes);

        for (int i = 0; i < _cachedNodes.Count; i++)
        {
            _cachedGraph.Add(new List<(int To, float Cost)>(12));
        }

        for (int i = 0; i < _cachedNodes.Count; i++)
        {
            for (int j = i + 1; j < _cachedNodes.Count; j++)
            {
                if (!simulation.HasTraversablePath(_cachedNodes[i], _cachedNodes[j], clearance))
                {
                    continue;
                }

                float cost = Vector2.Distance(_cachedNodes[i], _cachedNodes[j]);
                _cachedGraph[i].Add((j, cost));
                _cachedGraph[j].Add((i, cost));
            }
        }
    }

    private void EnsureScratchCapacity(int staticCount)
    {
        int dynamicCount = staticCount + 2;
        if (_startVisible.Length < staticCount)
        {
            int capacity = Math.Max(staticCount, Math.Max(32, _startVisible.Length * 2));
            Array.Resize(ref _startVisible, capacity);
            Array.Resize(ref _goalVisible, capacity);
            Array.Resize(ref _startCost, capacity);
            Array.Resize(ref _goalCost, capacity);
        }

        if (_gScoreScratch.Length < dynamicCount)
        {
            int capacity = Math.Max(dynamicCount, Math.Max(34, _gScoreScratch.Length * 2));
            Array.Resize(ref _gScoreScratch, capacity);
            Array.Resize(ref _cameFromScratch, capacity);
            Array.Resize(ref _closedScratch, capacity);
        }
    }

    private void RelaxCachedEdge(
        int from,
        int to,
        float edgeCost,
        Vector2 goal,
        int staticCount)
    {
        if (_closedScratch[to])
        {
            return;
        }

        float tentative = _gScoreScratch[from] + edgeCost;
        if (tentative >= _gScoreScratch[to])
        {
            return;
        }

        _cameFromScratch[to] = from;
        _gScoreScratch[to] = tentative;
        float heuristic = to switch
        {
            1 => 0f,
            >= 2 when to - 2 < staticCount => Vector2.Distance(_cachedNodes[to - 2], goal),
            _ => 0f,
        };
        _openQueue.Enqueue(to, (tentative + heuristic, to));
    }

    private IReadOnlyList<Vector2> ReconstructCachedPath(
        IReadOnlyList<int> cameFrom,
        int goalIndex,
        int staticCount,
        Vector2 goal)
    {
        List<Vector2> reverse = new();
        int current = goalIndex;
        while (current >= 0)
        {
            if (current == 1)
            {
                reverse.Add(goal);
            }
            else if (current >= 2 && current - 2 < staticCount)
            {
                reverse.Add(_cachedNodes[current - 2]);
            }

            current = cameFrom[current];
        }

        reverse.Reverse();
        return reverse;
    }

    /// <summary>
    /// Plan a body-clear waypoint chain from start to goal. The returned list excludes start
    /// and includes goal. An empty list means no static-geometry route was found.
    /// </summary>
    public IReadOnlyList<Vector2> Plan(
        SimulationEnvironment environment,
        Vector2 start,
        Vector2 goal)
    {
        float clearance = NavigationClearance();
        if (HasTraversablePath(environment, start, goal, clearance))
        {
            return new[] { goal };
        }

        List<Vector2> nodes = new() { start, goal };
        float cornerOffset = clearance + Math.Max(0.02f, _config.RouteCornerBuffer);
        float innerMargin = environment.BoundaryThickness + clearance + 0.02f;

        foreach (Wall blockage in environment.Blockages.Concat(environment.BlueWalls)
            .Concat(environment.Furniture.Where(set => set.StaticObstacle is not null)
                .Select(set => set.StaticObstacle!)))
        {
            Vector2[] corners = new Vector2[4];
            blockage.CopyExpandedCorners(corners, cornerOffset);

            foreach (Vector2 candidate in corners)
            {
                if (candidate.X <= innerMargin || candidate.Y <= innerMargin ||
                    candidate.X >= environment.Width - innerMargin ||
                    candidate.Y >= environment.Height - innerMargin)
                {
                    continue;
                }

                // Candidate already contains cornerOffset, so use a small point margin here.
                if (environment.IsPointBlocked(candidate, 0.25f * clearance))
                {
                    continue;
                }

                if (!nodes.Any(existing => Vector2.DistanceSquared(existing, candidate) < 0.0004f))
                {
                    nodes.Add(candidate);
                }
            }
        }

        AddCircleWaypoints(environment, clearance, innerMargin, nodes);

        int count = nodes.Count;
        if (count <= 2)
        {
            return Array.Empty<Vector2>();
        }

        List<(int To, float Cost)>[] graph = new List<(int To, float Cost)>[count];
        for (int i = 0; i < count; i++)
        {
            graph[i] = new List<(int To, float Cost)>();
        }

        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (!HasTraversablePath(environment, nodes[i], nodes[j], clearance))
                {
                    continue;
                }

                float cost = Vector2.Distance(nodes[i], nodes[j]);
                graph[i].Add((j, cost));
                graph[j].Add((i, cost));
            }
        }

        const int startIndex = 0;
        const int goalIndex = 1;
        float[] gScore = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
        float[] fScore = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
        int[] cameFrom = Enumerable.Repeat(-1, count).ToArray();
        bool[] closed = new bool[count];
        HashSet<int> open = new() { startIndex };
        gScore[startIndex] = 0f;
        fScore[startIndex] = Vector2.Distance(start, goal);

        while (open.Count > 0)
        {
            int current = open.OrderBy(index => fScore[index]).ThenBy(index => index).First();
            if (current == goalIndex)
            {
                return Reconstruct(nodes, cameFrom, goalIndex);
            }

            open.Remove(current);
            closed[current] = true;

            foreach ((int neighbour, float edgeCost) in graph[current])
            {
                if (closed[neighbour])
                {
                    continue;
                }

                float tentative = gScore[current] + edgeCost;
                if (tentative >= gScore[neighbour])
                {
                    continue;
                }

                cameFrom[neighbour] = current;
                gScore[neighbour] = tentative;
                fScore[neighbour] = tentative + Vector2.Distance(nodes[neighbour], goal);
                open.Add(neighbour);
            }
        }

        return Array.Empty<Vector2>();
    }

    private static void AddCircleWaypoints(
        SimulationEnvironment environment,
        float clearance,
        float innerMargin,
        ICollection<Vector2> nodes)
    {
        const int segments = 12;
        const float radialSafetyMeters = 0.02f;
        float halfSegmentAngle = MathF.PI / segments;
        float chordClearanceScale = 1f / MathF.Cos(halfSegmentAngle);

        foreach (CircleBlockage circle in environment.CircularBlockages)
        {
            // Adjacent waypoint nodes are connected by straight chords. Placing nodes only
            // just outside the inflated circle lets those chords cut back through the obstacle.
            // Increase the waypoint radius by sec(pi / segments) so each chord itself remains
            // body-clear, plus a small metric safety margin for floating-point tolerance.
            float inflatedRadius = circle.Radius + Math.Max(0f, clearance);
            float waypointRadius =
                (inflatedRadius + radialSafetyMeters) * chordClearanceScale;
            for (int i = 0; i < segments; i++)
            {
                float angle = MathF.Tau * i / segments;
                Vector2 candidate = circle.Center +
                    new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * waypointRadius;
                if (candidate.X <= innerMargin || candidate.Y <= innerMargin ||
                    candidate.X >= environment.Width - innerMargin ||
                    candidate.Y >= environment.Height - innerMargin ||
                    environment.IsPointBlocked(candidate, 0.25f * clearance))
                {
                    continue;
                }

                bool duplicate = nodes.Any(existing =>
                    Vector2.DistanceSquared(existing, candidate) < 0.0004f);
                if (!duplicate)
                {
                    nodes.Add(candidate);
                }
            }
        }
    }

    private static bool HasTraversablePath(
        SimulationEnvironment environment,
        Vector2 start,
        Vector2 end,
        float clearance)
    {
        if (!EvacuationSystem.HasTraversablePath(start, end, environment.Walls, clearance))
        {
            return false;
        }

        foreach (CircleBlockage circle in environment.CircularBlockages)
        {
            if (CircleGeometry.SegmentIntersectsCircle(
                start,
                end,
                circle.Center,
                circle.Radius + Math.Max(0f, clearance)))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<Vector2> Reconstruct(
        IReadOnlyList<Vector2> nodes,
        IReadOnlyList<int> cameFrom,
        int goalIndex)
    {
        List<Vector2> reverse = new();
        int current = goalIndex;
        while (current >= 0)
        {
            reverse.Add(nodes[current]);
            current = cameFrom[current];
        }

        reverse.Reverse();
        if (reverse.Count > 0)
        {
            reverse.RemoveAt(0); // exclude start
        }

        return reverse;
    }

    private float NavigationClearance()
        => Math.Max(0f, _config.Radius * _config.NavigationClearanceFactor);
}
