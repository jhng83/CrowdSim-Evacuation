using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CrowdSim.Diagnostics
{
    [Flags]
    public enum DiagnosticOverlay { None = 0, Desired = 1, Avoidance = 2, Herd = 4, Sfm = 8, Blast = 16, Vision = 32, Congestion = 64, Trails = 128 }

    /// <summary>Native cadence/cap: one sample every three ticks, at most forty samples per active person.</summary>
    public sealed class DiagnosticTrails
    {
        private readonly Dictionary<int, Queue<Vector2>> _points = new Dictionary<int, Queue<Vector2>>();
        private int _tick = -1;
        public void Clear() { _points.Clear(); _tick = -1; }
        public void Observe(DiagnosticFrame frame, bool enabled)
        {
            if (frame.Tick < _tick) Clear();
            HashSet<int> active = new HashSet<int>(frame.Agents.Select(a => a.Id));
            foreach (int id in _points.Keys.Where(id => !active.Contains(id)).ToArray()) _points.Remove(id);
            if (!enabled || frame.Tick == _tick || frame.Tick % 3 != 0) return;
            _tick = frame.Tick;
            foreach (AgentVectors agent in frame.Agents)
            {
                if (!_points.TryGetValue(agent.Id, out Queue<Vector2>? points)) { points = new Queue<Vector2>(41); _points.Add(agent.Id, points); }
                points.Enqueue(agent.Position); if (points.Count > 40) points.Dequeue();
            }
        }
        public IReadOnlyList<Vector2> Points(int id) => _points.TryGetValue(id, out Queue<Vector2>? points) ? Array.AsReadOnly(points.ToArray()) : Array.Empty<Vector2>();
    }

    public static class DiagnosticOverlays
    {
        private const float Legacy = 1.70f / 42;
        public static BlastMeshData Build(DiagnosticFrame frame, DiagnosticOverlay kind, DiagnosticTrails trails, int? hiddenAgent = null)
        {
            List<Vector3> lines = new List<Vector3>(), quads = new List<Vector3>();
            Vector4 color = Color(kind);
            void Line(Vector2 a, Vector2 b, float height) { lines.Add(PresentationFrame.ToPresentation(a, height)); lines.Add(PresentationFrame.ToPresentation(b, height)); }
            void Arrow(Vector2 start, Vector2 direction, float length)
            {
                float squared = direction.LengthSquared();
                if (!Finite(direction) || !float.IsFinite(squared) || squared < 1e-8f || !float.IsFinite(length) || length <= .1f) return;
                Vector2 unit = Vector2.Normalize(direction), end = start + unit * length;
                Vector2 side = new Vector2(-unit.Y, unit.X);
                Line(start, end, 1.78f); Line(end, end - unit * .16f + side * .08f, 1.78f); Line(end, end - unit * .16f - side * .08f, 1.78f);
            }
            foreach (AgentVectors agent in frame.Agents)
            {
                if (agent.Id == hiddenAgent) continue;
                Vector2 p = agent.Position;
                switch (kind)
                {
                    case DiagnosticOverlay.Desired: Arrow(p, agent.Desired, 32 * Legacy); break;
                    case DiagnosticOverlay.Avoidance: Arrow(p, agent.Avoidance, 30 * Legacy); break;
                    case DiagnosticOverlay.Herd: Arrow(p, agent.Herd, 28 * Legacy); break;
                    case DiagnosticOverlay.Sfm: Arrow(p, agent.Force, Math.Min(45 * Legacy, agent.Force.Length() / 160 * Legacy)); break;
                    case DiagnosticOverlay.Blast:
                        if (frame.BlastActive && frame.BlastOrigin.HasValue) Arrow(p, p - frame.BlastOrigin.Value, 26 * Legacy);
                        break;
                    case DiagnosticOverlay.Vision:
                        if (!Finite(agent.Heading) || agent.Heading.LengthSquared() < 1e-8f) break;
                        float angle = MathF.Atan2(agent.Heading.Y, agent.Heading.X), half = frame.VisionHalfAngle * MathF.PI / 180;
                        Vector2 previous = p + new Vector2(MathF.Cos(angle - half), MathF.Sin(angle - half)) * frame.VisionRange;
                        Line(p, previous, .3f);
                        for (int i = 1; i <= 18; i++)
                        {
                            float theta = angle - half + 2 * half * i / 18;
                            Vector2 next = p + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * frame.VisionRange;
                            Line(previous, next, .3f); previous = next;
                        }
                        Line(p, previous, .3f); break;
                    case DiagnosticOverlay.Congestion:
                        float intensity = Math.Clamp(agent.Congestion, 0, 1);
                        if (intensity <= .08f) break;
                        float radius = .2f + .5f * intensity;
                        for (int i = 0; i < 16; i++)
                        {
                            float a = i * MathF.PI / 8, b = (i + 1) * MathF.PI / 8;
                            Vector3 center = PresentationFrame.ToPresentation(p, .08f);
                            quads.Add(center); quads.Add(PresentationFrame.ToPresentation(p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius, .08f));
                            quads.Add(PresentationFrame.ToPresentation(p + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius, .08f)); quads.Add(center);
                        }
                        break;
                    case DiagnosticOverlay.Trails:
                        IReadOnlyList<Vector2> points = trails.Points(agent.Id);
                        for (int i = 1; i < points.Count; i++) Line(points[i - 1], points[i], .10f);
                        break;
                }
            }
            return new BlastMeshData(quads, lines, color);
        }
        private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
        public static Vector4 Color(DiagnosticOverlay kind)
        {
            switch (kind)
            {
                case DiagnosticOverlay.Desired: return new Vector4(.06f, .45f, .22f, 1);
                case DiagnosticOverlay.Avoidance: return new Vector4(.66f, .35f, .02f, 1);
                case DiagnosticOverlay.Herd: return new Vector4(0, .40f, .53f, 1);
                case DiagnosticOverlay.Sfm: return new Vector4(.68f, .10f, .60f, 1);
                case DiagnosticOverlay.Blast: return new Vector4(.80f, .25f, .02f, 1);
                case DiagnosticOverlay.Vision: return new Vector4(.1f, .35f, .70f, .5f);
                case DiagnosticOverlay.Congestion: return new Vector4(.55f, .12f, .75f, .24f);
                default: return new Vector4(.15f, .24f, .32f, .7f);
            }
        }
    }
}
