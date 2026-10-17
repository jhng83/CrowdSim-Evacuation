using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using ImGuiNET;

namespace CrowdSim.OpenGL.Rendering;

public sealed partial class WorldRenderer
{
    // Built once per geometry change, not once per agent or frame.
    private readonly List<(FurnitureSet Set, Wall Bounds)> _cachedFixedFurnitureChunks = new(24);

    private void CacheFixedFurnitureChunks(Simulation simulation)
    {
        foreach (FurnitureSet set in simulation.Environment.Furniture)
        {
            if (set.StaticObstacle is not Wall rectangle) continue;
            if (set.Kind == FurnitureKind.Barricade)
            {
                // One static entity/footprint, no per-agent work or physics body.
                _cachedFixedFurnitureChunks.Add((set, rectangle));
                continue;
            }
            if (set.Kind != FurnitureKind.Stage) continue;

            // A 15 m stage must not be painter-sorted as one giant centre point: small
            // cached pieces interleave correctly with people beside its front/side edges.
            int nx = Math.Max(1, (int)MathF.Ceiling((rectangle.X2 - rectangle.X1) / 2f));
            int ny = Math.Max(1, (int)MathF.Ceiling((rectangle.Y2 - rectangle.Y1) / 2f));
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                float x1 = rectangle.X1 + (rectangle.X2 - rectangle.X1) * x / nx;
                float x2 = rectangle.X1 + (rectangle.X2 - rectangle.X1) * (x + 1) / nx;
                float y1 = rectangle.Y1 + (rectangle.Y2 - rectangle.Y1) * y / ny;
                float y2 = rectangle.Y1 + (rectangle.Y2 - rectangle.Y1) * (y + 1) / ny;
                _cachedFixedFurnitureChunks.Add((set, new Wall(x1, y1, x2, y2)));
            }
        }
    }

    private static Vector4 FixedFurnitureColor(FurnitureKind kind) => kind switch
    {
        FurnitureKind.Stage => new Vector4(0.29f, 0.31f, 0.34f, 1f),
        FurnitureKind.Barricade => new Vector4(1.00f, 0.32f, 0.035f, 1f),
        _ => new Vector4(0.60f, 0.39f, 0.23f, 1f),
    };

    private void DrawFixedFurniture(ImDrawListPtr draw, FurnitureSet set, Wall bounds, Vector4 color)
    {
        if (set.Kind == FurnitureKind.Barricade)
        {
            DrawBarricade(draw, set, color);
            return;
        }
        if (set.Kind == FurnitureKind.Stage)
        {
            DrawAxisAlignedPrism(draw, bounds.X1, bounds.X2, bounds.Y1, bounds.Y2,
                0f, FurnitureSet.StageHeight, color);
            return;
        }

        if (set.Table is not RectangularFurnitureBody table || table.State == ChairState.Destroyed) return;
        Vector2 center = table.Position;
        float height = table.Height;
        float heading = table.Heading;
        const float halfLength = FurnitureSet.RectangularTableLength * 0.5f;
        const float halfWidth = FurnitureSet.RectangularTableWidth * 0.5f;
        const float top = FurnitureSet.RectangularTableHeight;
        const float thickness = 0.06f;
        if (table.State is ChairState.Airborne or ChairState.Fallen)
        {
            // Same lightweight toppled-table approximation used by existing round tables.
            DrawOrientedPrism(draw, center, -halfLength, halfLength, -halfWidth, halfWidth,
                height + 0.02f, height + 0.18f, heading, color);
            return;
        }
        // Four legs and one top; no chairs or placeholder people.
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        {
            float forward = x * (halfLength - 0.10f);
            float side = y * (halfWidth - 0.10f);
            DrawOrientedPrism(draw, center, forward - 0.045f, forward + 0.045f,
                side - 0.045f, side + 0.045f, height, height + top - thickness, heading, Shade(color, 0.66f));
        }
        DrawOrientedPrism(draw, center, -halfLength, halfLength, -halfWidth, halfWidth,
            height + top - thickness, height + top, heading, color);
    }

    private void DrawFurnitureFootprint(ImDrawListPtr draw, Wall bounds, Vector4 color)
    {
        Span<Vector2> corners = stackalloc Vector2[4];
        bounds.CopyExpandedCorners(corners, 0f);
        Vector3 a = new(corners[0], 0.025f);
        Vector3 b = new(corners[1], 0.025f);
        Vector3 c = new(corners[2], 0.025f);
        Vector3 d = new(corners[3], 0.025f);
        uint outline = Color(new Vector4(color.X, color.Y, color.Z, 1f));
        DrawWorldLine(draw, a, b, outline, 2f);
        DrawWorldLine(draw, b, c, outline, 2f);
        DrawWorldLine(draw, c, d, outline, 2f);
        DrawWorldLine(draw, d, a, outline, 2f);
    }
}
