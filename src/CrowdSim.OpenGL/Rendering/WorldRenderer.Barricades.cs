using System.Numerics;
using CrowdSim.Core.Model;
using ImGuiNET;

namespace CrowdSim.OpenGL.Rendering;

public sealed partial class WorldRenderer
{
    /// <summary>
    /// Lightweight orange road-barrier silhouette with a broad base, tapered body and
    /// white reflector panels. Uses the shared painter renderer in overview and POV.
    /// Its conservative collision footprint is exactly the 1.5 x 0.4 metre base.
    /// </summary>
    private void DrawBarricade(ImDrawListPtr draw, FurnitureSet set, Vector4 color)
    {
        const float halfLength = FurnitureSet.BarricadeLength * 0.5f;
        const float halfWidth = FurnitureSet.BarricadeWidth * 0.5f;
        const float baseHeight = 0.16f;
        const float topHalfWidth = 0.10f;
        const float topHalfLength = halfLength - 0.04f;
        const float height = FurnitureSet.BarricadeHeight;
        DrawOrientedPrism(draw, set.Center, -halfLength, halfLength, -halfWidth, halfWidth,
            0f, baseHeight, set.Rotation, Shade(color, 0.88f));

        Span<Vector3> vertices = stackalloc Vector3[8]
        {
            LocalPoint(set.Center, set.Rotation, -halfLength, -halfWidth, baseHeight),
            LocalPoint(set.Center, set.Rotation, halfLength, -halfWidth, baseHeight),
            LocalPoint(set.Center, set.Rotation, halfLength, halfWidth, baseHeight),
            LocalPoint(set.Center, set.Rotation, -halfLength, halfWidth, baseHeight),
            LocalPoint(set.Center, set.Rotation, -topHalfLength, -topHalfWidth, height),
            LocalPoint(set.Center, set.Rotation, topHalfLength, -topHalfWidth, height),
            LocalPoint(set.Center, set.Rotation, topHalfLength, topHalfWidth, height),
            LocalPoint(set.Center, set.Rotation, -topHalfLength, topHalfWidth, height),
        };
        Span<int> order = stackalloc int[5];
        Span<float> depth = stackalloc float[6];
        for (int i = 0; i < 5; i++)
        {
            int face = i == 4 ? 5 : i; // Four sides plus the top; no hidden bottom.
            int k = face * 4;
            Vector3 center = (vertices[PrismCorners[k]] + vertices[PrismCorners[k + 1]] +
                vertices[PrismCorners[k + 2]] + vertices[PrismCorners[k + 3]]) * 0.25f;
            depth[face] = _camera.Depth(center);
            int j = i - 1;
            while (j >= 0 && depth[order[j]] < depth[face])
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = face;
        }
        foreach (int face in order)
        {
            int k = face * 4;
            Vector3 a = vertices[PrismCorners[k]], b = vertices[PrismCorners[k + 1]];
            Vector3 c = vertices[PrismCorners[k + 2]], d = vertices[PrismCorners[k + 3]];
            DrawFlatQuad(draw, a, b, c, d, Color(Shade(color, OrientedShades[face])));
            if (face is not (0 or 2)) continue;
            // Paint reflectors WITH their face, not over the completed object. Nearer
            // faces then hide far-side reflectors rather than showing them through it.
            uint white = Color(new Vector4(0.97f, 0.97f, 0.92f, color.W));
            for (int panel = 0; panel < 2; panel++)
            {
                float u0 = 0.14f + panel * 0.43f, u1 = u0 + 0.29f;
                Vector3 lowA = Vector3.Lerp(a, d, 0.58f), lowB = Vector3.Lerp(b, c, 0.58f);
                Vector3 highA = Vector3.Lerp(a, d, 0.81f), highB = Vector3.Lerp(b, c, 0.81f);
                DrawFlatQuad(draw, Vector3.Lerp(lowA, lowB, u0), Vector3.Lerp(lowA, lowB, u1),
                    Vector3.Lerp(highA, highB, u1), Vector3.Lerp(highA, highB, u0), white);
            }
        }
    }
}
