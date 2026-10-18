#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Diagnostics;
using System.Numerics;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    /// <summary>
    /// Precomputed, obstacle-aware, normalized toxic-fume field for crowd-response experiments.
    /// The expensive propagation pass runs only during scenario setup. Runtime queries reconstruct
    /// concentration from cached arrival/peak fields and therefore avoid solving dispersion per frame.
    /// This is intentionally an abstract research stimulus rather than a calibrated chemical model.
    /// </summary>
    public sealed class ToxicFumeSystem
    {
        private static readonly (int Dx, int Dy, float Distance)[] Neighbours =
        {
            (-1, 0, 1f), (1, 0, 1f), (0, -1, 1f), (0, 1, 1f),
            (-1, -1, 1.41421356f), (1, -1, 1.41421356f),
            (-1, 1, 1.41421356f), (1, 1, 1.41421356f),
        };

        private float[] _arrivalSeconds = Array.Empty<float>();
        private float[] _pathDistanceMeters = Array.Empty<float>();
        private byte[] _peakConcentration = Array.Empty<byte>();
        private bool[] _blocked = Array.Empty<bool>();
        private int _columns;
        private int _rows;
        private int _precomputedWallLayoutVersion = -1;
        private int _precomputedConfigurationRevision = -1;

        public ToxicFumeSystem()
        {
            Configuration = new ToxicFumeConfig();
        }

        public ToxicFumeConfig Configuration { get; }
        public Vector2? SourcePosition { get; private set; }
        public bool IsPrecomputed { get; private set; }
        public bool IsReleased { get; private set; }
        public int ReleaseFrame { get; private set; } = -1;
        public float PreviewTimeSeconds { get; set; }
        public float PrecomputedMaximumArrivalSeconds { get; private set; }
        public double LastPrecomputeMilliseconds { get; private set; }
        public int ReachableCellCount { get; private set; }
        public int GridCellCount => _columns * _rows;
        public float GridCellSizeMeters => Configuration.GridCellSizeMeters;
        public long EstimatedCacheBytes =>
            (long)_arrivalSeconds.Length * sizeof(float) +
            (long)_pathDistanceMeters.Length * sizeof(float) +
            _peakConcentration.Length + _blocked.Length;

        /// <summary>Place or move the release source on clear floor and invalidate the cache.</summary>
        public bool TrySetSource(Simulation simulation, Vector2 position)
        {
            if (simulation is null) throw new ArgumentNullException(nameof(simulation));
            float margin = Math.Max(0.02f, 0.25f * simulation.Config.Agent.Radius);
            if (position.X <= simulation.Environment.BoundaryThickness + margin ||
                position.Y <= simulation.Environment.BoundaryThickness + margin ||
                position.X >= simulation.Environment.Width - simulation.Environment.BoundaryThickness - margin ||
                position.Y >= simulation.Environment.Height - simulation.Environment.BoundaryThickness - margin ||
                simulation.Environment.IsPointBlocked(position, margin))
            {
                return false;
            }

            SourcePosition = position;
            InvalidatePrecomputation();
            return true;
        }

        /// <summary>Restore source/configuration from a saved scenario without precomputing.</summary>
        public void RestoreSource(Vector2? position)
        {
            SourcePosition = position;
            InvalidatePrecomputation();
        }

        /// <summary>Invalidate only the setup-time propagation cache; runtime release state is reset.</summary>
        public void InvalidatePrecomputation()
        {
            IsPrecomputed = false;
            IsReleased = false;
            ReleaseFrame = -1;
            PreviewTimeSeconds = 0f;
            PrecomputedMaximumArrivalSeconds = 0f;
            LastPrecomputeMilliseconds = 0d;
            ReachableCellCount = 0;
            _precomputedWallLayoutVersion = -1;
            _precomputedConfigurationRevision = -1;
            _columns = 0;
            _rows = 0;
            _arrivalSeconds = Array.Empty<float>();
            _pathDistanceMeters = Array.Empty<float>();
            _peakConcentration = Array.Empty<byte>();
            _blocked = Array.Empty<bool>();
        }

        /// <summary>
        /// Compute obstacle-aware arrival time and peak normalized concentration for every reachable
        /// grid cell. PriorityQueue keeps the pass O(N log N) and the resulting cache is read-only at runtime.
        /// </summary>
        public void Precompute(Simulation simulation)
        {
            if (simulation is null) throw new ArgumentNullException(nameof(simulation));
            long started = Stopwatch.GetTimestamp();
            if (SourcePosition is not Vector2 source)
            {
                throw new InvalidOperationException("Place a toxic-fume release point before computing dispersion.");
            }

            float cell = Configuration.GridCellSizeMeters;
            _columns = Math.Max(1, (int)MathF.Ceiling(simulation.Environment.Width / cell));
            _rows = Math.Max(1, (int)MathF.Ceiling(simulation.Environment.Height / cell));
            int count = checked(_columns * _rows);

            _arrivalSeconds = new float[count];
            _pathDistanceMeters = new float[count];
            _peakConcentration = new byte[count];
            _blocked = new bool[count];
            Array.Fill(_arrivalSeconds, float.PositiveInfinity);
            Array.Fill(_pathDistanceMeters, float.PositiveInfinity);

            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _columns; x++)
                {
                    int index = Index(x, y);
                    Vector2 center = CellCenter(x, y);
                    _blocked[index] =
                        center.X < 0f || center.Y < 0f ||
                        center.X > simulation.Environment.Width || center.Y > simulation.Environment.Height ||
                        simulation.Environment.IsFumeBlocked(center, 0.02f);
                }
            }

            ReferencePriorityQueue<int, float> queue = new();
            float initialRadius = Configuration.InitialRadiusMeters;
            int sourceIndex = FindNearestOpenCell(source);
            if (sourceIndex < 0)
            {
                throw new InvalidOperationException("The selected release point has no open fume-grid cell nearby.");
            }

            // Start from one connected source cell. Cells reached through open space within the
            // configured initial radius receive zero arrival time below; this prevents the initial
            // cloud from appearing through a wall merely because a cell is close in Euclidean distance.
            _arrivalSeconds[sourceIndex] = 0f;
            _pathDistanceMeters[sourceIndex] = 0f;
            queue.Enqueue(sourceIndex, 0f);

            float maxExtent = Configuration.MaximumExtentMeters;
            Vector2 driftDirection = DirectionFromDegrees(Configuration.DriftDirectionDegrees);
            float baseSpeed = Math.Max(0.01f, Configuration.DispersionSpeedMetersPerSecond);

            while (queue.TryDequeue(out int current, out float queuedArrival))
            {
                if (queuedArrival > _arrivalSeconds[current] + 1e-5f)
                {
                    continue;
                }

                int cx = current % _columns;
                int cy = current / _columns;
                float currentPath = _pathDistanceMeters[current];

                foreach ((int dx, int dy, float distanceFactor) in Neighbours)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows)
                    {
                        continue;
                    }

                    int next = Index(nx, ny);
                    if (_blocked[next])
                    {
                        continue;
                    }

                    // Prevent diagonal corner cutting through solid cells.
                    if (dx != 0 && dy != 0 &&
                        (_blocked[Index(cx + dx, cy)] || _blocked[Index(cx, cy + dy)]))
                    {
                        continue;
                    }

                    float edgeMeters = cell * distanceFactor;
                    float nextPath = currentPath + edgeMeters;
                    if (nextPath > maxExtent + 0.5f * cell)
                    {
                        continue;
                    }

                    Vector2 direction = VectorMath.SafeNormalize(new Vector2(dx, dy));
                    float alignment = Vector2.Dot(direction, driftDirection);
                    float bias = Configuration.DriftBias;
                    float speedFactor = alignment >= 0f
                        ? 1f + 1.5f * bias * alignment
                        : 1f / (1f + bias * -alignment);
                    float effectiveSpeed = Math.Max(0.02f, baseSpeed * speedFactor);
                    float currentOutsideInitial = Math.Max(0f, currentPath - initialRadius);
                    float nextOutsideInitial = Math.Max(0f, nextPath - initialRadius);
                    float spreadingEdgeMeters = Math.Max(0f, nextOutsideInitial - currentOutsideInitial);
                    float nextArrival = queuedArrival + spreadingEdgeMeters / effectiveSpeed;

                    bool arrivesEarlier = nextArrival + 1e-5f < _arrivalSeconds[next];
                    bool equalArrivalButShorterPath =
                        MathF.Abs(nextArrival - _arrivalSeconds[next]) <= 1e-5f &&
                        nextPath + 1e-5f < _pathDistanceMeters[next];
                    if (!arrivesEarlier && !equalArrivalButShorterPath)
                    {
                        continue;
                    }

                    _arrivalSeconds[next] = nextArrival;
                    _pathDistanceMeters[next] = nextPath;
                    queue.Enqueue(next, nextArrival);
                }
            }

            ReachableCellCount = 0;
            PrecomputedMaximumArrivalSeconds = 0f;
            for (int index = 0; index < count; index++)
            {
                float arrival = _arrivalSeconds[index];
                if (!float.IsFinite(arrival))
                {
                    continue;
                }

                ReachableCellCount++;
                PrecomputedMaximumArrivalSeconds = Math.Max(PrecomputedMaximumArrivalSeconds, arrival);

                int x = index % _columns;
                int y = index / _columns;
                Vector2 directionFromSource = VectorMath.SafeNormalize(CellCenter(x, y) - source);
                float downwind = Math.Max(0f, Vector2.Dot(directionFromSource, driftDirection));
                float distance = _pathDistanceMeters[index];
                float spatial = MathF.Exp(-1.30f * distance / Math.Max(maxExtent, cell));
                float directional = 1f - 0.28f * Configuration.DriftBias * (1f - downwind);
                float peak = Math.Clamp(spatial * directional, 0.04f, 1f);
                if (distance <= initialRadius + 0.5f * cell)
                {
                    peak = 1f;
                }

                _peakConcentration[index] = (byte)Math.Clamp((int)MathF.Round(255f * peak), 0, 255);
            }

            // Path distance is needed only while building peak concentration. Drop it before runtime
            // so the replay cache stays compact: arrival(float) + peak(byte) + blocked(bool) per cell.
            _pathDistanceMeters = Array.Empty<float>();

            _precomputedWallLayoutVersion = simulation.Environment.WallLayoutVersion;
            _precomputedConfigurationRevision = Configuration.Revision;
            IsPrecomputed = true;
            LastPrecomputeMilliseconds =
                (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            IsReleased = false;
            ReleaseFrame = -1;
            PreviewTimeSeconds = 0f;
        }

        /// <summary>Whether the cached field still matches the current environment geometry.</summary>
        public bool IsCacheValidFor(Simulation simulation)
        {
            if (simulation is null) throw new ArgumentNullException(nameof(simulation));
            return CacheMatches(simulation);
        }

        /// <summary>Begin replaying the precomputed plume timeline from the current simulation frame.</summary>
        public bool TriggerRelease(Simulation simulation)
        {
            if (simulation is null) throw new ArgumentNullException(nameof(simulation));
            if (!simulation.Running || !CacheMatches(simulation) || IsReleased)
            {
                return false;
            }

            ReleaseFrame = simulation.FrameIndex;
            IsReleased = true;
            return true;
        }

        /// <summary>Seconds elapsed since release, or zero before release.</summary>
        public float GetElapsedSeconds(Simulation simulation)
            => IsReleased && ReleaseFrame >= 0
                ? Math.Max(0f, (simulation.FrameIndex - ReleaseFrame) * simulation.Config.Agent.Dt)
                : 0f;

        /// <summary>Normalized concentration [0,1] at a point for an explicit time after release.</summary>
        public float GetConcentration(Vector2 position, float elapsedSeconds)
        {
            if (!IsPrecomputed || elapsedSeconds < 0f || _columns <= 0 || _rows <= 0)
            {
                return 0f;
            }

            int x = Math.Clamp((int)(position.X / GridCellSizeMeters), 0, _columns - 1);
            int y = Math.Clamp((int)(position.Y / GridCellSizeMeters), 0, _rows - 1);
            return ConcentrationAtCell(Index(x, y), elapsedSeconds);
        }

        /// <summary>Current runtime concentration at a point. Returns zero before the release event.</summary>
        public float GetCurrentConcentration(Simulation simulation, Vector2 position)
            => IsReleased ? GetConcentration(position, GetElapsedSeconds(simulation)) : 0f;

        /// <summary>
        /// Integrate per-agent normalized exposure and trigger the normal panic/reaction pathway.
        /// Mobility impairment is a separate multiplier so the existing injury semantics remain intact.
        /// </summary>
        public void UpdateAgents(Simulation simulation)
        {
            if (!IsReleased || !CacheMatches(simulation))
            {
                return;
            }

            float elapsedSeconds = GetElapsedSeconds(simulation);
            int alarmFrames = Math.Max(
                1,
                (int)MathF.Round(simulation.Config.Agent.DirectAlarmSignalSeconds /
                    Math.Max(simulation.Config.Agent.Dt, 1e-5f)));

            foreach (Agent agent in simulation.Agents)
            {
                if (!agent.IsActive || agent.State is AgentState.Exited or AgentState.Dead)
                {
                    agent.CurrentToxicConcentration = 0f;
                    continue;
                }

                float concentration = GetConcentration(agent.Position, elapsedSeconds);
                agent.CurrentToxicConcentration = concentration;
                if (agent.State == AgentState.Incapacitated)
                {
                    // Keep the displayed local concentration current for an immobile body without
                    // repeatedly integrating exposure after the terminal fume-response state.
                    continue;
                }

                agent.PeakToxicConcentration = Math.Max(agent.PeakToxicConcentration, concentration);
                agent.CumulativeToxicExposure += concentration * simulation.Config.Agent.Dt;

                if (concentration >= Configuration.AwarenessConcentration && agent.FirstToxicExposureFrame < 0)
                {
                    agent.FirstToxicExposureFrame = simulation.FrameIndex;
                    agent.ToxicExposureStatus = ToxicExposureStatus.Exposed;
                    agent.ActivateDirectAlarm(
                        simulation.FrameIndex,
                        alarmFrames,
                        simulation.Config.Agent.DirectAlarmPanicFloor,
                        ReleaseFrame);
                    agent.PanicLevel = Math.Max(
                        agent.PanicLevel,
                        Math.Clamp(0.35f + 0.55f * concentration, 0f, 0.90f));

                    if (agent.State == AgentState.Wandering)
                    {
                        int freezeFrames = Math.Max(1, simulation.Config.Agent.ReferenceFramesToTicks(4));
                        agent.BeginReaction(simulation.FrameIndex, simulation.FrameIndex + freezeFrames);
                    }
                    else if (agent.State is AgentState.Evacuating or AgentState.Injured)
                    {
                        simulation.EvacuationSystem.BeginPurposefulResponse(simulation, agent);
                    }
                }

                if (concentration >= Configuration.DangerConcentration)
                {
                    agent.PanicLevel = Math.Max(agent.PanicLevel, 0.65f);
                }

                if (agent.CumulativeToxicExposure >= Configuration.IncapacitationExposure)
                {
                    agent.ToxicExposureStatus = ToxicExposureStatus.Incapacitated;
                    agent.ToxicMobilityFactor = 0f;
                    agent.MarkIncapacitated();
                    continue;
                }

                if (agent.CumulativeToxicExposure >= Configuration.ImpairmentExposure)
                {
                    agent.ToxicExposureStatus = ToxicExposureStatus.Impaired;
                    float span = Math.Max(
                        1e-5f,
                        Configuration.IncapacitationExposure - Configuration.ImpairmentExposure);
                    float progress = Math.Clamp(
                        (agent.CumulativeToxicExposure - Configuration.ImpairmentExposure) / span,
                        0f,
                        1f);
                    agent.ToxicMobilityFactor = 1f - 0.45f * progress;
                }
                else if (concentration >= Configuration.AwarenessConcentration)
                {
                    agent.ToxicExposureStatus = ToxicExposureStatus.Exposed;
                }
            }
        }

        /// <summary>Return a local direction toward lower current concentration, respecting blocked cells.</summary>
        public Vector2 GetSaferDirection(Simulation simulation, Vector2 position)
        {
            if (!IsReleased || !IsPrecomputed || _columns <= 0 || _rows <= 0)
            {
                return Vector2.Zero;
            }

            float elapsed = GetElapsedSeconds(simulation);
            int cx = Math.Clamp((int)(position.X / GridCellSizeMeters), 0, _columns - 1);
            int cy = Math.Clamp((int)(position.Y / GridCellSizeMeters), 0, _rows - 1);
            float current = ConcentrationAtCell(Index(cx, cy), elapsed);
            Vector2 bestDirection = Vector2.Zero;
            float bestScore = current;

            foreach (var (dx, dy, _) in Neighbours)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows)
                {
                    continue;
                }

                int index = Index(nx, ny);
                if (_blocked[index])
                {
                    continue;
                }

                float concentration = ConcentrationAtCell(index, elapsed);
                if (concentration + 0.01f < bestScore)
                {
                    bestScore = concentration;
                    bestDirection = VectorMath.SafeNormalize(new Vector2(dx, dy));
                }
            }

            if (bestDirection.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon)
            {
                return bestDirection;
            }

            if (SourcePosition is Vector2 source)
            {
                return VectorMath.SafeNormalize(position - source);
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// Cheap route-risk estimate used only during exit scoring. It samples the cached plume field;
        /// no propagation work occurs here.
        /// </summary>
        public float EstimateSegmentExposure(Simulation simulation, Vector2 start, Vector2 end, int samples = 8)
        {
            if (!IsReleased || !IsPrecomputed)
            {
                return 0f;
            }

            samples = Math.Clamp(samples, 2, 24);
            float elapsed = GetElapsedSeconds(simulation);
            float sum = 0f;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                sum += GetConcentration(Vector2.Lerp(start, end, t), elapsed);
            }

            return sum / (samples + 1);
        }

        /// <summary>Enumerate nonzero cells at a requested replay time for renderer/debug overlays.</summary>
        public IEnumerable<ToxicFumeCellSample> EnumerateCells(
            float elapsedSeconds,
            float minimumConcentration = 0.03f,
            int stride = 1)
        {
            if (!IsPrecomputed)
            {
                yield break;
            }

            minimumConcentration = Math.Clamp(minimumConcentration, 0f, 1f);
            stride = Math.Clamp(stride, 1, 8);
            float drawCellSize = GridCellSizeMeters * stride;
            for (int y = 0; y < _rows; y += stride)
            {
                for (int x = 0; x < _columns; x += stride)
                {
                    int index = Index(x, y);
                    float concentration = ConcentrationAtCell(index, elapsedSeconds);
                    if (concentration < minimumConcentration)
                    {
                        continue;
                    }

                    yield return new ToxicFumeCellSample(CellCenter(x, y), concentration, drawCellSize);
                }
            }
        }


        private int FindNearestOpenCell(Vector2 position)
        {
            int centerX = Math.Clamp((int)(position.X / GridCellSizeMeters), 0, _columns - 1);
            int centerY = Math.Clamp((int)(position.Y / GridCellSizeMeters), 0, _rows - 1);
            int bestIndex = -1;
            float bestDistanceSquared = float.PositiveInfinity;

            // A clear click can share a coarse grid cell with a thin wall. Search a small local
            // neighbourhood so discretisation does not reject an otherwise valid source placement.
            for (int radius = 0; radius <= 2; radius++)
            {
                for (int y = Math.Max(0, centerY - radius); y <= Math.Min(_rows - 1, centerY + radius); y++)
                {
                    for (int x = Math.Max(0, centerX - radius); x <= Math.Min(_columns - 1, centerX + radius); x++)
                    {
                        if (radius > 0 && Math.Abs(x - centerX) < radius && Math.Abs(y - centerY) < radius)
                        {
                            continue;
                        }

                        int index = Index(x, y);
                        if (_blocked[index])
                        {
                            continue;
                        }

                        float distanceSquared = Vector2.DistanceSquared(CellCenter(x, y), position);
                        if (distanceSquared < bestDistanceSquared)
                        {
                            bestDistanceSquared = distanceSquared;
                            bestIndex = index;
                        }
                    }
                }

                if (bestIndex >= 0)
                {
                    return bestIndex;
                }
            }

            return -1;
        }

        private bool CacheMatches(Simulation simulation)
            => IsPrecomputed &&
               _precomputedWallLayoutVersion == simulation.Environment.WallLayoutVersion &&
               _precomputedConfigurationRevision == Configuration.Revision;

        private float ConcentrationAtCell(int index, float elapsedSeconds)
        {
            if ((uint)index >= (uint)_arrivalSeconds.Length || _blocked[index])
            {
                return 0f;
            }

            float arrival = _arrivalSeconds[index];
            if (!float.IsFinite(arrival) || elapsedSeconds < arrival)
            {
                return 0f;
            }

            float localAge = elapsedSeconds - arrival;
            float build = arrival <= 1e-5f ? 1f : Math.Clamp(localAge / 1.5f, 0f, 1f);
            float envelope;
            if (localAge <= Configuration.ReleaseDurationSeconds)
            {
                envelope = build;
            }
            else
            {
                float afterRelease = localAge - Configuration.ReleaseDurationSeconds;
                envelope = build * Math.Clamp(
                    1f - afterRelease / Math.Max(1f, Configuration.PostReleaseDecaySeconds),
                    0f,
                    1f);
            }

            return (_peakConcentration[index] / 255f) * envelope;
        }

        private int Index(int x, int y) => y * _columns + x;

        private Vector2 CellCenter(int x, int y)
            => new((x + 0.5f) * GridCellSizeMeters, (y + 0.5f) * GridCellSizeMeters);

        private static Vector2 DirectionFromDegrees(float degrees)
        {
            float radians = degrees * MathF.PI / 180f;
            return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        }
    }

    /// <summary>Lightweight renderer/debug sample from the precomputed fume cache.</summary>
    public readonly struct ToxicFumeCellSample : IEquatable<ToxicFumeCellSample>
    {
        public Vector2 Center { get; }
        public float Concentration { get; }
        public float CellSizeMeters { get; }
        public ToxicFumeCellSample(Vector2 Center, float Concentration, float CellSizeMeters)
        {
            this.Center = Center;
            this.Concentration = Concentration;
            this.CellSizeMeters = CellSizeMeters;
        }
        public void Deconstruct(out Vector2 Center, out float Concentration, out float CellSizeMeters) { Center = this.Center; Concentration = this.Concentration; CellSizeMeters = this.CellSizeMeters; }
        public bool Equals(ToxicFumeCellSample other) => EqualityComparer<Vector2>.Default.Equals(Center, other.Center) && EqualityComparer<float>.Default.Equals(Concentration, other.Concentration) && EqualityComparer<float>.Default.Equals(CellSizeMeters, other.CellSizeMeters);
        public override bool Equals(object? other) => other is ToxicFumeCellSample value && Equals(value);
        public override int GetHashCode()
        {
            int hash = 0;
            hash = unchecked(hash * -1521134295 + EqualityComparer<Vector2>.Default.GetHashCode(Center));
            hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(Concentration));
            hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(CellSizeMeters));
            return hash;
        }
        public static bool operator ==(ToxicFumeCellSample left, ToxicFumeCellSample right) => left.Equals(right);
        public static bool operator !=(ToxicFumeCellSample left, ToxicFumeCellSample right) => !left.Equals(right);
    }
}
