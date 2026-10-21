#nullable enable
using System;
using System.Numerics;

namespace CrowdSim.Diagnostics
{
    public enum AgentCameraMode { Overview, Follow, Pov }

    /// <summary>Pure Unity-coordinate camera values. No model or graphics objects are retained.</summary>
    public readonly struct AgentCameraFrame
    {
        public Vector3 Eye { get; }
        public Vector3 Target { get; }
        public bool Orthographic { get; }
        public float Size { get; }
        public const float FieldOfView = 72;
        public const float NearPlane = .04f;
        public AgentCameraFrame(Vector3 eye, Vector3 target, bool orthographic, float size)
        { Eye = eye; Target = target; Orthographic = orthographic; Size = size; }
    }

    public static class AgentCameraRig
    {
        public static AgentCameraFrame Calculate(float width, float length, float aspect, float azimuth, float elevation,
            float zoom, AgentCameraMode mode, AgentVisualFrame? selected, Vector2? renderedPosition = null)
        {
            if (selected == null || selected.State == Core.Model.AgentState.Exited) mode = AgentCameraMode.Overview;
            Vector2 ground = renderedPosition ?? selected?.Position ?? new Vector2(width / 2, length / 2);
            if (mode == AgentCameraMode.Pov && selected != null)
            {
                Vector3 eye = PresentationFrame.ToPresentation(ground, 1.70f * .88f);
                Vector3 forward = Vector3.Normalize(new Vector3(selected.Heading.X, -.055f, selected.Heading.Y));
                return new AgentCameraFrame(eye, eye + forward, false, 1);
            }
            float el = Math.Clamp(elevation, 18, 70) * MathF.PI / 180, az = azimuth * MathF.PI / 180;
            Vector3 direction = new Vector3(MathF.Cos(el) * MathF.Cos(az), MathF.Sin(el), MathF.Cos(el) * MathF.Sin(az));
            Vector3 target = mode == AgentCameraMode.Follow ? PresentationFrame.ToPresentation(ground, 0) : new Vector3(width / 2, 0, length / 2);
            Vector3 right = Vector3.Normalize(Vector3.Cross(-direction, Vector3.UnitY));
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, -direction));
            float horizontal = 0, vertical = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = new Vector3(x * width / 2, 0, z * length / 2);
                horizontal = Math.Max(horizontal, Math.Abs(Vector3.Dot(corner, right)));
                vertical = Math.Max(vertical, Math.Abs(Vector3.Dot(corner, up)));
            }
            float size = Math.Max(vertical + 3, (horizontal + 3) / Math.Max(.1f, aspect)) * 1.08f / Math.Clamp(zoom, .45f, 2.5f);
            if (mode == AgentCameraMode.Follow) size /= 1.35f;
            return new AgentCameraFrame(target + direction * 200, target, true, size);
        }
    }
}
