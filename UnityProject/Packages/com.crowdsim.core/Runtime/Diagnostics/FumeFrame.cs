#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    public sealed class FumeCellFrame
    {
        public Vector2 Center { get; }
        public float Concentration { get; }
        public float Size { get; }
        public FumeCellFrame(Vector2 center, float concentration, float size)
        { Center = center; Concentration = concentration; Size = size; }
    }
    /// <summary>Detached cache/readiness, exposure metrics and batched 0.5 m field samples. No mutable engine values escape.</summary>
    public sealed class FumeFrame
    {
        public Vector2? Source { get; }
        public bool CacheReady { get; }
        public bool Released { get; }
        public bool Preview { get; }
        public float DisplaySeconds { get; }
        public float ElapsedSeconds { get; }
        public float PreviewMaximum { get; }
        public int ReachableCells { get; }
        public int GridCells { get; }
        public float GridResolution { get; }
        public float MaximumArrival { get; }
        public long CacheBytes { get; }
        public double ComputeMilliseconds { get; }
        public int Exposed { get; }
        public int Impaired { get; }
        public int Incapacitated { get; }
        public int HighConcentration { get; }
        public float AverageExposure { get; }
        public float PeakConcentration { get; }
        public IReadOnlyList<FumeCellFrame> Cells { get; } = Array.Empty<FumeCellFrame>();
        public IReadOnlyList<BlastMeshData> Layers { get; } = Array.Empty<BlastMeshData>();
        public BlastMeshData? SourceRing { get; }
        public static FumeFrame Capture(Simulation simulation, bool building) => new FumeFrame(simulation, building);
        private FumeFrame(Simulation simulation, bool building)
        {
            if (simulation.ScenarioType != ScenarioType.ToxicFume) return;
            var field = simulation.ToxicFumeSystem;
            Source = field.SourcePosition; CacheReady = field.IsCacheValidFor(simulation); Released = field.IsReleased;
            Preview = building && CacheReady && !Released;
            ElapsedSeconds = field.GetElapsedSeconds(simulation);
            DisplaySeconds = Released ? ElapsedSeconds : building ? field.PreviewTimeSeconds : 0;
            MaximumArrival = field.PrecomputedMaximumArrivalSeconds;
            PreviewMaximum = Math.Max(10, MaximumArrival + field.Configuration.ReleaseDurationSeconds + field.Configuration.PostReleaseDecaySeconds);
            ReachableCells = CacheReady ? field.ReachableCellCount : 0; GridCells = CacheReady ? field.GridCellCount : 0;
            GridResolution = field.GridCellSizeMeters; CacheBytes = CacheReady ? field.EstimatedCacheBytes : 0;
            ComputeMilliseconds = field.LastPrecomputeMilliseconds;
            Exposed = simulation.Agents.Count(a => a.ToxicExposureStatus == ToxicExposureStatus.Exposed || a.ToxicExposureStatus == ToxicExposureStatus.Impaired);
            Impaired = simulation.Agents.Count(a => a.ToxicExposureStatus == ToxicExposureStatus.Impaired);
            Incapacitated = simulation.Agents.Count(a => a.ToxicExposureStatus == ToxicExposureStatus.Incapacitated);
            HighConcentration = simulation.Agents.Count(a => a.IsActive && a.CurrentToxicConcentration >= field.Configuration.DangerConcentration);
            AverageExposure = simulation.Agents.Count == 0 ? 0 : simulation.Agents.Average(a => a.CumulativeToxicExposure);
            PeakConcentration = simulation.Agents.Count == 0 ? 0 : simulation.Agents.Max(a => a.PeakToxicConcentration);
            if (Source is Vector2 source)
            {
                List<Vector3> ring = new List<Vector3>();
                float radius = Math.Max(.18f, field.Configuration.InitialRadiusMeters);
                for (int i = 0; i < 48; i++)
                {
                    float a = i * 2 * MathF.PI / 48, b = (i + 1) * 2 * MathF.PI / 48;
                    ring.Add(PresentationFrame.ToPresentation(source + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius, .07f));
                    ring.Add(PresentationFrame.ToPresentation(source + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius, .07f));
                }
                SourceRing = new BlastMeshData(new List<Vector3>(), ring, new Vector4(.18f, .92f, .48f, .8f));
            }
            if (!CacheReady) return;
            List<FumeCellFrame> cells = new List<FumeCellFrame>();
            List<Vector3>[] quads = new List<Vector3>[8], lines = new List<Vector3>[8];
            float[] sums = new float[8]; int[] counts = new int[8];
            for (int i = 0; i < 8; i++) { quads[i] = new List<Vector3>(); lines[i] = new List<Vector3>(); }
            foreach (var sample in field.EnumerateCells(DisplaySeconds, .025f, 1))
            {
                cells.Add(new FumeCellFrame(sample.Center, sample.Concentration, sample.CellSizeMeters));
                int bucket = Math.Clamp((int)(sample.Concentration * 8), 0, 7);
                sums[bucket] += sample.Concentration; counts[bucket]++;
                float half = sample.CellSizeMeters * .5f;
                float x0 = Math.Max(0, sample.Center.X - half), x1 = Math.Min(simulation.Environment.Width, sample.Center.X + half);
                float z0 = Math.Max(0, sample.Center.Y - half), z1 = Math.Min(simulation.Environment.Height, sample.Center.Y + half);
                Vector3 a = new Vector3(x0, .065f, z0), b = new Vector3(x1, .065f, z0), c = new Vector3(x1, .065f, z1), d = new Vector3(x0, .065f, z1);
                quads[bucket].AddRange(new[] { a, b, c, d });
                foreach (Vector3 p in new[] { a, b, b, c, c, d, d, a }) lines[bucket].Add(new Vector3(p.X, .075f, p.Z));
            }
            List<BlastMeshData> layers = new List<BlastMeshData>();
            for (int i = 0; i < 8; i++)
            {
                float c = counts[i] == 0 ? 0 : sums[i] / counts[i];
                layers.Add(new BlastMeshData(quads[i], lines[i], new Vector4(.18f + .58f * c, .82f - .18f * c, .30f - .12f * c, .07f + .30f * c)));
            }
            Cells = cells.AsReadOnly(); Layers = layers.AsReadOnly();
        }
    }
}
