#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    /// <summary>Detached ground geometry: quads and independent line pairs, without engine entity references.</summary>
    public sealed class BlastMeshData
    {
        public IReadOnlyList<Vector3> Quads { get; }
        public IReadOnlyList<Vector3> Lines { get; }
        public Vector4 Color { get; }
        internal BlastMeshData(List<Vector3> quads, List<Vector3> lines, Vector4 color)
        { Quads = quads.AsReadOnly(); Lines = lines.AsReadOnly(); Color = color; }
    }

    /// <summary>Reference annulus sampling and conservative wall/room clipping, shared by cloud and Unity.</summary>
    public static class BlastGeometry
    {
        private static readonly Vector4[] Colors = {
            new Vector4(.96f, .08f, .06f, 1), new Vector4(1, .42f, .06f, 1),
            new Vector4(1, .82f, .08f, 1), new Vector4(.12f, .78f, 1, 1), new Vector4(.88f, .90f, .94f, 1)
        };
        public static Vector4 ZoneColor(int zone) => Colors[zone];
        public static bool Visible(Simulation simulation, Vector2 origin, Vector2 point)
        {
            float t = simulation.Environment.BoundaryThickness;
            return point.X >= t && point.X <= simulation.Environment.Width - t &&
                point.Y >= t && point.Y <= simulation.Environment.Height - t && simulation.HasLineOfSight(origin, point);
        }
        public static BlastMeshData Band(Simulation simulation, Vector2 origin, float inner, float outer,
            Vector4 color, float height = .025f, int segments = 128, int outlineSegments = 144)
        {
            List<Vector3> quads = new List<Vector3>(), lines = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Direction(i, segments), b = Direction(i + 1, segments);
                Vector2 ia = origin + a * inner, ib = origin + b * inner;
                Vector2 oa = origin + a * outer, ob = origin + b * outer;
                if (Visible(simulation, origin, ia) && Visible(simulation, origin, ib) &&
                    Visible(simulation, origin, oa) && Visible(simulation, origin, ob) &&
                    Visible(simulation, origin, (ia + ib + oa + ob) * .25f))
                {
                    quads.Add(PresentationFrame.ToPresentation(ia, height)); quads.Add(PresentationFrame.ToPresentation(ib, height));
                    quads.Add(PresentationFrame.ToPresentation(ob, height)); quads.Add(PresentationFrame.ToPresentation(oa, height));
                }
            }
            for (int i = 0; i < outlineSegments; i++)
            {
                Vector2 a = origin + Direction(i, outlineSegments) * outer, b = origin + Direction(i + 1, outlineSegments) * outer;
                if (Visible(simulation, origin, a) && Visible(simulation, origin, b) && Visible(simulation, origin, (a + b) * .5f))
                { lines.Add(PresentationFrame.ToPresentation(a, height + .01f)); lines.Add(PresentationFrame.ToPresentation(b, height + .01f)); }
            }
            return new BlastMeshData(quads, lines, color);
        }
        private static Vector2 Direction(int i, int count)
        { float angle = 2 * MathF.PI * i / count; return new Vector2(MathF.Cos(angle), MathF.Sin(angle)); }

        /// <summary>One-metre samples of the actual wall-shielded visibility field, quantized into eight display opacity bands.</summary>
        public static IReadOnlyList<BlastMeshData> Dust(Simulation simulation, Vector2 origin)
        {
            List<Vector3>[] buckets = new List<Vector3>[8];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<Vector3>();
            float radius = simulation.Config.Agent.DustVisibilityRadius, t = simulation.Environment.BoundaryThickness;
            float minX = Math.Max(t, MathF.Floor(origin.X - radius)), minY = Math.Max(t, MathF.Floor(origin.Y - radius));
            float maxX = Math.Min(simulation.Environment.Width - t, MathF.Ceiling(origin.X + radius));
            float maxY = Math.Min(simulation.Environment.Height - t, MathF.Ceiling(origin.Y + radius));
            for (float x = minX; x < maxX; x += 1)
            for (float y = minY; y < maxY; y += 1)
            {
                Vector2 a = new Vector2(x, y), b = new Vector2(Math.Min(x + 1, maxX), y);
                Vector2 c = new Vector2(b.X, Math.Min(y + 1, maxY)), d = new Vector2(x, c.Y), center = (a + c) * .5f;
                float loss = 1 - simulation.VisibilitySystem.GetVisibilityFactor(simulation, center);
                if (loss < .015f || !Visible(simulation, origin, a) || !Visible(simulation, origin, b) ||
                    !Visible(simulation, origin, c) || !Visible(simulation, origin, d)) continue;
                int bucket = Math.Clamp((int)(loss * 8), 0, 7);
                buckets[bucket].Add(PresentationFrame.ToPresentation(a, .06f)); buckets[bucket].Add(PresentationFrame.ToPresentation(b, .06f));
                buckets[bucket].Add(PresentationFrame.ToPresentation(c, .06f)); buckets[bucket].Add(PresentationFrame.ToPresentation(d, .06f));
            }
            List<BlastMeshData> layers = new List<BlastMeshData>();
            for (int i = 0; i < buckets.Length; i++)
                layers.Add(new BlastMeshData(buckets[i], new List<Vector3>(), new Vector4(.65f, .59f, .48f, (i + 1) / 8f * .7f)));
            return layers.AsReadOnly();
        }
    }

    /// <summary>Per-session persistent annulus cache. Visibility toggles never rebuild or alter its geometry.</summary>
    public sealed class BlastGeometryCache
    {
        private Simulation? _owner;
        private int _layout = -1;
        private Vector2 _origin;
        private string _radii = "";
        private IReadOnlyList<BlastMeshData> _bands = Array.Empty<BlastMeshData>();
        public int BuildCount { get; private set; }
        public IReadOnlyList<BlastMeshData> Get(Simulation simulation)
        {
            Explosion? explosion = simulation.Environment.Explosion;
            if (explosion == null) { _owner = null; _bands = Array.Empty<BlastMeshData>(); return _bands; }
            var config = simulation.Config.Agent;
            float[] radii = { config.HardKillRadius, config.HeatIncapacityRadius, config.FallenRadius, config.InjuryRadius, config.HeatRadius };
            string key = string.Join("/", Array.ConvertAll(radii, r => r.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            if (ReferenceEquals(_owner, simulation) && _layout == simulation.Environment.WallLayoutVersion && _origin == explosion.Position && _radii == key) return _bands;
            List<BlastMeshData> bands = new List<BlastMeshData>();
            for (int i = 0; i < 5; i++) bands.Add(BlastGeometry.Band(simulation, explosion.Position, i == 0 ? 0 : radii[i - 1], radii[i], BlastGeometry.ZoneColor(i)));
            _owner = simulation; _layout = simulation.Environment.WallLayoutVersion; _origin = explosion.Position; _radii = key;
            _bands = bands.AsReadOnly(); BuildCount++; return _bands;
        }
    }
}
