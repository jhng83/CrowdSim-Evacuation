using System.Numerics;
using ImGuiNET;

namespace CrowdSim.OpenGL.Rendering;

public sealed partial class WorldRenderer
{
    private const float AgentOutlineWidth = 1.1f;
    private const uint AgentOutlineColor = 0xFF000000u; // opaque black; no native call during type initialisation
    private bool _outlineAgentGeometry;

    /// <summary>
    /// Outline the convex screen silhouette of one body-part prism using its already projected
    /// eight corners. Stack storage only; no second camera projection, mesh pass or frame buffer.
    /// Subsequent body parts retain normal occlusion/painter order.
    /// </summary>
    private static void DrawProjectedBodyOutline(ImDrawListPtr draw,
        ReadOnlySpan<Vector2> projected, ReadOnlySpan<bool> visible)
    {
        Span<Vector2> points = stackalloc Vector2[8];
        int n = 0;
        for (int i = 0; i < projected.Length; i++)
        {
            // Match the renderer's existing near-plane handling rather than connect a
            // silhouette across unprojectable corners in first-person view.
            if (!visible[i] || !float.IsFinite(projected[i].X) || !float.IsFinite(projected[i].Y)) return;
            Vector2 p = projected[i];
            int j = n - 1;
            while (j >= 0 && (points[j].X > p.X || (points[j].X == p.X && points[j].Y > p.Y)))
            {
                points[j + 1] = points[j];
                j--;
            }
            points[j + 1] = p;
            n++;
        }
        if (n < 3) return;
        Span<Vector2> hull = stackalloc Vector2[16];
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            while (count >= 2 && OutlineCross(hull[count - 2], hull[count - 1], points[i]) <= 0f) count--;
            hull[count++] = points[i];
        }
        int lower = count + 1;
        for (int i = n - 2; i >= 0; i--)
        {
            while (count >= lower && OutlineCross(hull[count - 2], hull[count - 1], points[i]) <= 0f) count--;
            hull[count++] = points[i];
        }
        count--; // the closing corner duplicates hull[0]
        if (count < 3) return;
        // One native submission per body-part outline, rather than one per edge.
        draw.AddPolyline(ref hull[0], count, AgentOutlineColor, ImDrawFlags.Closed, AgentOutlineWidth);
    }

    private static float OutlineCross(Vector2 origin, Vector2 a, Vector2 b) =>
        (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);
}
