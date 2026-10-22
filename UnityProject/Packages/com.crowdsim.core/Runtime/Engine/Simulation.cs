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
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;

namespace CrowdSim.Core
{
    /// <summary>
    /// Core simulation orchestrator. This is the C# counterpart of simulation.py and deliberately
    /// exposes the same major subsystem boundaries while using deterministic per-simulation randomness.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>Opt-in shared navigation and open-space movement.</summary>
        public HybridMovementSystem HybridMovement { get; } = new();

        /// <summary>Wall-clock CPU time for the last completed simulation tick.</summary>
        public double LastStepMilliseconds { get; private set; }
        public double LastFurnitureMilliseconds { get; private set; }
        /// <summary>Exclusive phase timings of the last completed tick; diagnostic only.</summary>
        public double LastGridMilliseconds { get; private set; }
        public double LastHazardMilliseconds { get; private set; }
        public double LastResponseMilliseconds { get; private set; }
        public double LastPreparationMilliseconds { get; private set; }
        public double LastMovementMilliseconds { get; private set; }
        public double LastContactsMilliseconds { get; private set; }
        public double LastExitMilliseconds { get; private set; }
        /// <summary>Nested subsets of movement/response, not additional exclusive phases.</summary>
        public double LastSfmMilliseconds { get; internal set; }
        public double LastCollisionMilliseconds { get; private set; }
        public double LastRoutePlanningMilliseconds { get; internal set; }
        public int LastRoutePlanRequests { get; internal set; }

        private readonly List<Wall> _sightWalls = new();
        private readonly List<CircleBlockage> _sightCircles = new();
        private bool _sightSnapshotActive;
        private readonly Dictionary<(int X, int Y), List<Agent>> _agentGrid = new();
        private (int X, int Y)[] _indexedAgentCells = Array.Empty<(int, int)>();
        private readonly List<(int X, int Y)> _activeAgentGridCells = new(512);
        private readonly Dictionary<(int X, int Y), List<Wall>> _wallGrid = new();
        private readonly HashSet<Wall> _segmentWallSeenScratch = new();
        private readonly Dictionary<string, int> _exitLoadCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _previousExitedByExit = new(StringComparer.Ordinal);
        private readonly List<Agent> _collisionAgentScratch = new(64);
        private readonly List<Wall> _nearbyWallScratch = new(32);
        private readonly HashSet<Wall> _nearbyWallSeenScratch = new();
        private int _exitLoadLastUpdate = -999;
        private readonly int _exitLoadUpdateInterval;
        private readonly float _gridCellSize;

        public Simulation(int agentCount = 0, SimulationConfig? config = null, int? randomSeed = null)
        {
            Config = config ?? SimulationConfig.CreateDefault();
            RandomSeed = randomSeed ?? ReferenceRandom.CreateSeed();
            Random = new ReferenceRandom(RandomSeed);
            Environment = new SimulationEnvironment(Config);
            SfmEngine = new SfmEngine(Config.Agent);
            ExplosionSystem = new ExplosionSystem(Config.Agent);
            ToxicFumeSystem = new ToxicFumeSystem();
            EvacuationSystem = new EvacuationSystem(Config.Agent);
            VisibilitySystem = new VisibilitySystem(Config.Agent);

            // Query slightly beyond the 2.25R mobile-to-casualty hard separation envelope.
            CollisionRadius = 2.30f * Config.Agent.Radius;
            InteractionRadius = 7f * Config.Agent.Radius;
            WallFollowRadius = 14f * Config.Agent.Radius;
            // Match the grid to interaction ranges, rather than body radius. A typical
            // perception query now visits tens of buckets instead of hundreds.
            _gridCellSize = Math.Clamp(Config.Agent.SfmInteractionRadius, 1f, 4f);
            if (!float.IsFinite(Config.Agent.Dt) || Config.Agent.Dt <= 0f ||
                Config.Agent.Dt > AgentConfig.ReferenceStepSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(config),
                    "Dt must be finite and in (0, 0.15] seconds for the bounded explicit integrator.");
            }
            _exitLoadUpdateInterval = Math.Max(1, (int)(0.75f / Config.Agent.Dt));

            if (agentCount > 0)
            {
                SpawnAgents(agentCount);
            }
        }

        public SimulationConfig Config { get; }
        public SimulationEnvironment Environment { get; }
        public List<Agent> Agents { get; private set; } = new();
        public List<Debris> Debris { get; } = new();
        public SfmEngine SfmEngine { get; }
        public ExplosionSystem ExplosionSystem { get; }
        public ToxicFumeSystem ToxicFumeSystem { get; }
        public EvacuationSystem EvacuationSystem { get; }
        public VisibilitySystem VisibilitySystem { get; }
        public Random Random { get; }
        public int RandomSeed { get; }

        /// <summary>Emergency hazard selected before scene construction.</summary>
        public ScenarioType ScenarioType { get; private set; } = CrowdSim.Core.Model.ScenarioType.Explosion;

        public bool Running { get; private set; }
        public int FrameIndex { get; private set; }
        public int? ExplosionTriggerFrame { get; internal set; }
        public Dictionary<int, Vector2> PreviousPositions { get; private set; } = new();
        public Dictionary<int, int> StuckFrames { get; private set; } = new();
        public Dictionary<string, ExitMetrics> ExitMetrics { get; } = new(StringComparer.Ordinal);

        public int DebrisHits { get; internal set; }
        public int DebrisInjuries { get; internal set; }
        public int DebrisIncapacitations { get; internal set; }
        public int DebrisFatalities { get; internal set; }

        public float CollisionRadius { get; }
        public float InteractionRadius { get; }
        public float WallFollowRadius { get; }

        /// <summary>Return nearby agents using the source spatial hash strategy.</summary>
        public IReadOnlyList<Agent> NearbyAgents(Agent agent, float radius)
        {
            List<Agent> result = new();
            CollectNearbyAgents(agent, radius, result);
            return result;
        }

        /// <summary>
        /// Fill a caller-owned buffer with nearby active agents. This avoids a new list allocation in
        /// hot loops such as SFM and panic propagation while preserving the same spatial-hash query.
        /// </summary>
        public void CollectNearbyAgents(Agent agent, float radius, List<Agent> result)
            => CollectAgentsNearPoint(agent.Position, radius, result, agent.AgentId);

        /// <summary>
        /// Fill a caller-owned buffer with active agents around an arbitrary point. Running queries
        /// use the same spatial hash as pedestrian-neighbour queries, which avoids full-population
        /// scans for doorway occupancy, exit density and other fixed-location measurements.
        /// </summary>
        public void CollectAgentsNearPoint(
            Vector2 position,
            float radius,
            List<Agent> result,
            int excludeAgentId = -1)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));
            result.Clear();

            float safeRadius = Math.Max(0f, radius);
            float radiusSquared = safeRadius * safeRadius;

            // Tests, builder tools and diagnostics may reposition agents directly while the
            // simulation is paused. In that state the spatial hash may intentionally be stale, so
            // use current authoritative positions. The running hot path below remains hashed.
            if (!Running)
            {
                foreach (Agent other in Agents)
                {
                    if (!other.IsActive || other.AgentId == excludeAgentId)
                    {
                        continue;
                    }

                    if (VectorMath.DistanceSquared(other.Position, position) <= radiusSquared)
                    {
                        result.Add(other);
                    }
                }

                return;
            }

            float inverse = 1f / _gridCellSize;
            int minX = (int)MathF.Floor((position.X - safeRadius) * inverse);
            int maxX = (int)MathF.Floor((position.X + safeRadius) * inverse);
            int minY = (int)MathF.Floor((position.Y - safeRadius) * inverse);
            int maxY = (int)MathF.Floor((position.Y + safeRadius) * inverse);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!_agentGrid.TryGetValue((x, y), out List<Agent>? bucket))
                    {
                        continue;
                    }

                    foreach (Agent other in bucket)
                    {
                        if (!other.IsActive || other.AgentId == excludeAgentId)
                        {
                            continue;
                        }

                        if (VectorMath.DistanceSquared(other.Position, position) <= radiusSquared)
                        {
                            result.Add(other);
                        }
                    }
                }
            }
        }

        public void SpawnAgents(int count)
        {
            Agents = InitAgents(Math.Max(0, count));
            FinishAgentSpawn();
        }

        /// <summary>
        /// Spawn the population at exact builder-selected start locations. The locations are scenario
        /// configuration, while all behavioural traits remain deterministically sampled from the run seed.
        /// </summary>
        public void SpawnAgentsAtPositions(IReadOnlyList<Vector2> positions)
        {
            if (positions is null) throw new ArgumentNullException(nameof(positions));
            Agents = new List<Agent>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 position = positions[i];
                if (Environment.IsPointBlocked(position, Config.Agent.Radius) && !Environment.IsSeatStart(position))
                {
                    throw new InvalidOperationException(
                        $"Planned start position {i} intersects a wall or blockage.");
                }

                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
                    Agents.Any(other => Vector2.DistanceSquared(other.Position, position) < 4f * Config.Agent.Radius * Config.Agent.Radius))
                    throw new InvalidOperationException($"Planned start position {i} is invalid or overlaps another occupant.");
                Agents.Add(CreateAgent(i, position));
            }

            FinishAgentSpawn();
        }

        private void FinishAgentSpawn()
        {
            foreach (FurnitureChair chair in Environment.Furniture.SelectMany(set => set.Chairs))
            {
                Agent? occupant = Agents.FirstOrDefault(agent => Vector2.DistanceSquared(agent.Position, chair.StartPosition) < 0.000001f);
                if (occupant is null) continue;
                occupant.Chair = chair;
                occupant.Posture = SeatingPosture.Seated;
                occupant.Velocity = Vector2.Zero;
                chair.OccupantId = chair.OriginalOccupantId = occupant.AgentId;
            }
            BuildWallGrid();
            RebuildAgentGrid();
            PreviousPositions = Agents.ToDictionary(agent => agent.AgentId, agent => agent.Position);
            StuckFrames = Agents.ToDictionary(agent => agent.AgentId, _ => 0);
        }

        public List<Agent> InitAgents(int count)
        {
            (float width, float height) = Environment.Bounds();
            List<Agent> agents = new(count);
            float thickness = Environment.BoundaryThickness;
            float radius = Config.Agent.Radius;
            float xMin = thickness + radius + 0.08f;
            float xMax = width - thickness - radius - 0.08f;
            float yMin = thickness + radius + 0.08f;
            float yMax = height - thickness - radius - 0.08f;
            const int maxTries = 5000;

            for (int i = 0; i < count; i++)
            {
                Vector2? position = null;
                for (int attempt = 0; attempt < maxTries; attempt++)
                {
                    Vector2 candidate = new(
                        Lerp(xMin, xMax, (float)Random.NextDouble()),
                        Lerp(yMin, yMax, (float)Random.NextDouble()));
                    if (!Environment.IsPointBlocked(candidate, Config.Agent.Radius))
                    {
                        position = candidate;
                        break;
                    }
                }

                Vector2 safePosition = position ?? new Vector2(width / 2f, height / 2f);
                agents.Add(CreateAgent(i, safePosition));
            }

            return agents;
        }

        private Agent CreateAgent(int id, Vector2 position)
        {
            BehaviorTuningConfig behaviour = Config.Agent.Behavior;
            float speedT = Math.Clamp(
                0.5f + (AverageTwoRandoms(Random) - 0.5f) * behaviour.TraitVariation,
                0f,
                1f);
            float baseSpeed = Lerp(
                Config.Agent.BaseSpeedMin,
                Config.Agent.BaseSpeedMax,
                speedT);
            float reactionMidpoint = 0.5f * (
                Config.Agent.ReactionDelayMin + Config.Agent.ReactionDelayMax);
            float reactionHalfRange = 0.5f * (
                Config.Agent.ReactionDelayMax - Config.Agent.ReactionDelayMin);
            float reactionDelay = Math.Clamp(
                reactionMidpoint +
                (2f * AverageTwoRandoms(Random) - 1f) *
                reactionHalfRange * behaviour.ReactionVariation,
                Config.Agent.ReactionDelayMin,
                Config.Agent.ReactionDelayMax);
            DecisionStrategy strategy = Random.NextDouble() < 0.6
                ? DecisionStrategy.Nearest
                : DecisionStrategy.LeastCrowded;

            return new Agent(
                id,
                position,
                Vector2.Zero,
                baseSpeed,
                panicSpeedMultiplier: 1f,
                reactionDelay / Config.Agent.StepScale,
                strategy,
                Config.Agent)
            {
                // Trait values are sampled from bounded, non-demographic distributions.
                // The scenario seed makes the exact population reproducible.
                ThreatSensitivity = SampleCenteredTrait(
                    Random, behaviour.StressSusceptibility, 0.25f,
                    behaviour.TraitVariation, 0.60f, 1.40f),
                FollowTendency = SampleCenteredTrait(
                    Random, behaviour.CrowdFollowing, 0.45f,
                    behaviour.TraitVariation, 0f, 1f),
                HelpingTendency = SampleCenteredTrait(
                    Random, behaviour.HelpingTendency, 0.45f,
                    behaviour.TraitVariation, 0f, 1f),
                RouteFamiliarity = SampleCenteredTrait(
                    Random, behaviour.RouteFamiliarity, 0.45f,
                    behaviour.TraitVariation, 0f, 1f),
                UrgencyBias = SampleCenteredTrait(
                    Random, 1f, 0.15f, behaviour.TraitVariation, 0.75f, 1.25f),
                StressRecoveryBias = SampleCenteredTrait(
                    Random, 1f, 0.25f, behaviour.TraitVariation, 0.65f, 1.35f),
                PersonalSpaceBias = SampleCenteredTrait(
                    Random, 1f, 0.12f, behaviour.TraitVariation, 0.80f, 1.20f),
                Assertiveness = SampleCenteredTrait(
                    Random, behaviour.BottleneckAssertiveness, 0.45f,
                    behaviour.TraitVariation, 0f, 1f),
                PatienceBias = SampleCenteredTrait(
                    Random, 1f, 0.35f, behaviour.TraitVariation, 0.65f, 1.35f),
            };
        }

        public void TriggerExplosion(float x, float y)
        {
            if (ScenarioType == CrowdSim.Core.Model.ScenarioType.Explosion)
            {
                ExplosionSystem.TriggerExplosion(this, x, y);
            }
        }

        /// <summary>Change the hazard mode while the scene is not running.</summary>
        public void SetScenarioType(ScenarioType scenarioType)
        {
            if (Running || FrameIndex != 0)
            {
                throw new InvalidOperationException("Scenario type can only be changed before a run begins.");
            }

            ScenarioType = scenarioType;
            if (scenarioType != CrowdSim.Core.Model.ScenarioType.ToxicFume)
            {
                ToxicFumeSystem.InvalidatePrecomputation();
            }
        }

        /// <summary>Precompute the generic toxic-fume field during scenario setup.</summary>
        public void PrecomputeToxicFume()
        {
            if (ScenarioType != CrowdSim.Core.Model.ScenarioType.ToxicFume)
            {
                throw new InvalidOperationException("Toxic-fume precomputation is only available in the Toxic Fume scenario.");
            }

            ToxicFumeSystem.Precompute(this);
        }

        /// <summary>Start replaying the precomputed toxic-fume field.</summary>
        public bool TriggerToxicFumeRelease()
            => ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume && ToxicFumeSystem.TriggerRelease(this);

        public IReadOnlyList<Wall> NearbyWalls(Vector2 position, float radius)
        {
            List<Wall> result = new();
            HashSet<Wall> seen = new();
            CollectNearbyWalls(position, radius, result, seen);
            return result;
        }

        /// <summary>Fill caller-owned wall buffers without allocating in collision hot paths.</summary>
        public void CollectNearbyWalls(
            Vector2 position,
            float radius,
            List<Wall> result,
            HashSet<Wall> seen)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));
            if (seen is null) throw new ArgumentNullException(nameof(seen));
            result.Clear();
            seen.Clear();

            float inverse = 1f / _gridCellSize;
            int minX = (int)MathF.Floor((position.X - radius) * inverse);
            int maxX = (int)MathF.Floor((position.X + radius) * inverse);
            int minY = (int)MathF.Floor((position.Y - radius) * inverse);
            int maxY = (int)MathF.Floor((position.Y + radius) * inverse);

            // Honour the requested radius; a fixed 3x3 scan missed distant wall candidates.
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!_wallGrid.TryGetValue((x, y), out List<Wall>? bucket))
                    {
                        continue;
                    }

                    foreach (Wall wall in bucket)
                    {
                        if (!seen.Add(wall))
                        {
                            continue;
                        }

                        if (wall.X1 - radius <= position.X && position.X <= wall.X2 + radius &&
                            wall.Y1 - radius <= position.Y && position.Y <= wall.Y2 + radius)
                        {
                            result.Add(wall);
                        }
                    }
                }
            }
        }

        public void BuildWallGrid()
        {
            _wallGrid.Clear();
            float inverse = 1f / _gridCellSize;
            foreach (Wall wall in Environment.Walls)
            {
                int x1 = (int)MathF.Floor(wall.X1 * inverse);
                int x2 = (int)MathF.Floor(wall.X2 * inverse);
                int y1 = (int)MathF.Floor(wall.Y1 * inverse);
                int y2 = (int)MathF.Floor(wall.Y2 * inverse);
                for (int gx = x1; gx <= x2; gx++)
                {
                    for (int gy = y1; gy <= y2; gy++)
                    {
                        if (!_wallGrid.TryGetValue((gx, gy), out List<Wall>? bucket))
                        {
                            bucket = new List<Wall>();
                            _wallGrid[(gx, gy)] = bucket;
                        }

                        bucket.Add(wall);
                    }
                }
            }
        }

        /// <summary>
        /// Exact centre-point line of sight using the static-wall spatial hash. With only a few
        /// walls the simple scan is cheaper; obstacle-rich builder scenes switch to grid traversal.
        /// </summary>
        public bool HasLineOfSight(Vector2 start, Vector2 end)
        {
            // Architecture cannot change during a synchronous Step. Furniture may move, but
            // has always been transparent to sight. Avoid rescanning hundreds of ignored chairs
            // for each social-awareness pair. Outside Step, query live lists as before.
            if (!_sightSnapshotActive) return !SegmentHitsWall(start, end, 0f, ignoreFurniture: true);
            foreach (Wall wall in _sightWalls)
                if (SegmentIntersectsExpandedWall(start, end, wall, 0f)) return false;
            foreach (CircleBlockage circle in _sightCircles)
                if (CircleGeometry.SegmentIntersectsCircle(start, end, circle.Center, circle.Radius)) return false;
            return true;
        }

        /// <summary>
        /// Exact body-clear segment test using spatially culled wall candidates. Walls are still
        /// tested against the same inflated AABB as the original navigation guardrail.
        /// </summary>
        public bool HasTraversablePath(Vector2 start, Vector2 end, float clearance)
            => !SegmentHitsWall(start, end, Math.Max(0f, clearance));

        private bool SegmentHitsWall(Vector2 start, Vector2 end, float clearance, bool ignoreFurniture = false)
        {
            if (Environment.Walls.Count == 0)
            {
                foreach (CircleBlockage circle in Environment.CircularBlockages)
                {
                    if (ignoreFurniture && circle.IsFurniture) continue;
                    if (CircleGeometry.SegmentIntersectsCircle(start, end, circle.Center, circle.Radius + clearance)) return true;
                }
                return false;
            }

            // For small/moderate layouts a direct scan is cheaper than walking many grid cells along a
            // long sightline. Builder-heavy layouts switch to spatial culling once wall count is high.
            if (Environment.Walls.Count <= 48 || _wallGrid.Count == 0)
            {
                foreach (Wall wall in Environment.Walls)
                {
                    if (ignoreFurniture && wall.IsFurniture) continue;
                    if (SegmentIntersectsExpandedWall(start, end, wall, clearance))
                    {
                        return true;
                    }
                }

                foreach (CircleBlockage circle in Environment.CircularBlockages)
                {
                    if (ignoreFurniture && circle.IsFurniture) continue;
                    if (CircleGeometry.SegmentIntersectsCircle(
                        start, end, circle.Center, circle.Radius + clearance))
                    {
                        return true;
                    }
                }

                return false;
            }

            _segmentWallSeenScratch.Clear();
            float inverse = 1f / _gridCellSize;
            int x = (int)MathF.Floor(start.X * inverse);
            int y = (int)MathF.Floor(start.Y * inverse);
            int endX = (int)MathF.Floor(end.X * inverse);
            int endY = (int)MathF.Floor(end.Y * inverse);
            Vector2 delta = end - start;

            int stepX = delta.X > 0f ? 1 : delta.X < 0f ? -1 : 0;
            int stepY = delta.Y > 0f ? 1 : delta.Y < 0f ? -1 : 0;
            float tDeltaX = stepX == 0 ? float.PositiveInfinity : _gridCellSize / MathF.Abs(delta.X);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : _gridCellSize / MathF.Abs(delta.Y);
            float nextBoundaryX = stepX > 0 ? (x + 1) * _gridCellSize : x * _gridCellSize;
            float nextBoundaryY = stepY > 0 ? (y + 1) * _gridCellSize : y * _gridCellSize;
            float tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - start.X) / delta.X;
            float tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - start.Y) / delta.Y;
            int neighbourCells = Math.Max(1, (int)MathF.Ceiling(clearance / _gridCellSize));
            int maxSteps = Math.Abs(endX - x) + Math.Abs(endY - y) + 4;

            for (int step = 0; step < maxSteps; step++)
            {
                for (int ox = -neighbourCells; ox <= neighbourCells; ox++)
                {
                    for (int oy = -neighbourCells; oy <= neighbourCells; oy++)
                    {
                        if (!_wallGrid.TryGetValue((x + ox, y + oy), out List<Wall>? bucket))
                        {
                            continue;
                        }

                        foreach (Wall wall in bucket)
                        {
                            if (ignoreFurniture && wall.IsFurniture) continue;
                            if (_segmentWallSeenScratch.Add(wall) &&
                                SegmentIntersectsExpandedWall(start, end, wall, clearance))
                            {
                                return true;
                            }
                        }
                    }
                }

                if (x == endX && y == endY)
                {
                    break;
                }

                if (tMaxX < tMaxY)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }
            }

            foreach (CircleBlockage circle in Environment.CircularBlockages)
            {
                if (ignoreFurniture && circle.IsFurniture) continue;
                if (CircleGeometry.SegmentIntersectsCircle(
                    start, end, circle.Center, circle.Radius + clearance))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SegmentIntersectsExpandedWall(
            Vector2 start,
            Vector2 end,
            Wall wall,
            float clearance)
        {
            float margin = Math.Max(0f, clearance);
            if (wall.OrientedFootprint is OrientedRectangle rotated)
                return rotated.IntersectsSegment(start, end, margin);
            if (margin <= VectorMath.Epsilon)
            {
                return EvacuationSystem.LineIntersectsRect(start, end, wall);
            }

            float minX = wall.X1 - margin;
            float minY = wall.Y1 - margin;
            float maxX = wall.X2 + margin;
            float maxY = wall.Y2 + margin;
            Vector2 delta = end - start;
            float tMin = 0f;
            float tMax = 1f;

            static bool ClipAxis(
                float origin,
                float direction,
                float minimum,
                float maximum,
                ref float tMin,
                ref float tMax)
            {
                if (MathF.Abs(direction) < VectorMath.Epsilon)
                {
                    return origin >= minimum && origin <= maximum;
                }

                float inverse = 1f / direction;
                float t1 = (minimum - origin) * inverse;
                float t2 = (maximum - origin) * inverse;
                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                }

                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                return tMin <= tMax;
            }

            return ClipAxis(start.X, delta.X, minX, maxX, ref tMin, ref tMax) &&
                   ClipAxis(start.Y, delta.Y, minY, maxY, ref tMin, ref tMax);
        }

        public bool BlastOver()
        {
            if (Environment.Explosion is null)
            {
                return true;
            }

            float elapsed = (FrameIndex - Environment.Explosion.TriggeredFrame) * Config.Agent.StepScale;
            return elapsed > Config.Agent.BlastWindDurationFrames + 10;
        }

        /// <summary>Toggle run/pause. As in Python, simulation cannot start before agents and an exit exist.</summary>
        public bool ToggleRunning()
        {
            if (Agents.Count == 0 || Environment.Exits.Count == 0)
            {
                return false;
            }

            if (!Running && ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume && !ToxicFumeSystem.IsCacheValidFor(this))
            {
                return false;
            }

            Running = !Running;
            return true;
        }

        public void SetRunning(bool running)
        {
            if (running && (Agents.Count == 0 || Environment.Exits.Count == 0))
            {
                Running = false;
                return;
            }

            if (running && ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume && !ToxicFumeSystem.IsCacheValidFor(this))
            {
                Running = false;
                return;
            }

            Running = running;
        }

        private void RebuildAgentGrid()
        {
            // Keep cell buckets alive and clear only the cells that were occupied last frame.
            // The previous implementation cleared the dictionary and allocated fresh List<Agent>
            // buckets every physics tick, which produced GC spikes once large mouse-placed groups
            // started moving through the room.
            foreach ((int X, int Y) cell in _activeAgentGridCells)
            {
                if (_agentGrid.TryGetValue(cell, out List<Agent>? bucket))
                {
                    bucket.Clear();
                }
            }

            _activeAgentGridCells.Clear();
            if (_indexedAgentCells.Length != Agents.Count)
            {
                _indexedAgentCells = new (int, int)[Agents.Count];
            }
            float inverse = 1f / _gridCellSize;
            foreach (Agent agent in Agents)
            {
                if (!agent.IsActive)
                {
                    continue;
                }

                (int X, int Y) cell = (
                    (int)MathF.Floor(agent.Position.X * inverse),
                    (int)MathF.Floor(agent.Position.Y * inverse));
                _indexedAgentCells[agent.AgentId] = cell;
                if (!_agentGrid.TryGetValue(cell, out List<Agent>? bucket))
                {
                    bucket = new List<Agent>(8);
                    _agentGrid[cell] = bucket;
                }

                if (bucket.Count == 0)
                {
                    _activeAgentGridCells.Add(cell);
                }

                bucket.Add(agent);
            }
        }

        /// <summary>Refresh after external diagnostic edits to positions while running.</summary>
        public void RefreshSpatialIndex() => RebuildAgentGrid();

        private void RefreshAgentCell(Agent agent)
        {
            if ((uint)agent.AgentId >= (uint)_indexedAgentCells.Length ||
                !VectorMath.IsFinite(agent.Position))
            {
                return;
            }
            var previous = _indexedAgentCells[agent.AgentId];
            var current = ((int)MathF.Floor(agent.Position.X / _gridCellSize),
                (int)MathF.Floor(agent.Position.Y / _gridCellSize));
            if (current == previous)
            {
                return;
            }
            if (_agentGrid.TryGetValue(previous, out List<Agent>? oldBucket))
            {
                int index = oldBucket.IndexOf(agent);
                if (index >= 0)
                {
                    oldBucket[index] = oldBucket[^1];
                    oldBucket.RemoveAt(oldBucket.Count - 1);
                }
            }
            if (!_agentGrid.TryGetValue(current, out List<Agent>? bucket))
            {
                bucket = new List<Agent>(8);
                _agentGrid[current] = bucket;
            }
            if (bucket.Count == 0)
            {
                _activeAgentGridCells.Add(current);
            }
            bucket.Add(agent);
            _indexedAgentCells[agent.AgentId] = current;
        }

        private static float AverageTwoRandoms(Random random)
            => 0.5f * ((float)random.NextDouble() + (float)random.NextDouble());

        private static float SampleCenteredTrait(
            Random random,
            float centre,
            float halfRange,
            float variation,
            float minimum,
            float maximum)
        {
            float signedTriangular = 2f * AverageTwoRandoms(random) - 1f;
            return Math.Clamp(
                centre + signedTriangular * halfRange * Math.Clamp(variation, 0f, 1f),
                minimum,
                maximum);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
