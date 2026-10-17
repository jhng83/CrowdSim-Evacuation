using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using ImGuiNET;
namespace CrowdSim.OpenGL.Rendering;

public sealed partial class WorldRenderer
{
    private readonly Dictionary<CircleBlockage, object> _furnitureVisuals = new();

    private void IndexFurniture(Simulation simulation)
    {
        _furnitureVisuals.Clear();
        foreach (FurnitureSet set in simulation.Environment.Furniture)
        {
            if (set.TableObstacle is not null) _furnitureVisuals[set.TableObstacle] = set;
            foreach (FurnitureChair chair in set.Chairs)
                if (chair.Obstacle is not null) _furnitureVisuals[chair.Obstacle] = chair;
        }
    }

    private void DrawFurnitureProxy(ImDrawListPtr draw, Simulation simulation, CircleBlockage proxy)
    {
        if (!_furnitureVisuals.TryGetValue(proxy, out object? item)) return;
        if (item is FurnitureSet set)
            DrawTable(draw, set.Table!, new Vector4(0.56f, 0.36f, 0.20f, 1f));
        else if (item is FurnitureChair chair)
        {
            DrawChair(draw, chair, new Vector4(0.32f, 0.42f, 0.52f, 1f));
            if (simulation.Agents.Count == 0)
                DrawSeatedPerson(draw, chair.StartPosition, chair.Heading, new Vector4(0.65f, 0.85f, 0.90f, 0.9f), 0f);
        }
    }

    private void DrawTable(ImDrawListPtr draw, FurnitureBody table, Vector4 color)
    {
        float height = table.Height;
        bool tipped = table.State is ChairState.Airborne or ChairState.Fallen;
        float top = height + (tipped ? 0.12f : 0.72f);
        DrawOrientedPrism(draw, table.Position, -0.13f, 0.13f, -0.13f, 0.13f, height, tipped ? height + 0.12f : top, table.Heading, Shade(color, 0.7f));
        for (int i = 0; i < 40; i++)
        {
            float a = MathF.Tau * i / 40, b = MathF.Tau * (i + 1) / 40;
            Vector2 p = table.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * table.Radius;
            Vector2 q = table.Position + new Vector2(MathF.Cos(b), MathF.Sin(b)) * table.Radius;
            DrawFlatQuad(draw, new Vector3(p, top), new Vector3(q, top), new Vector3(q, top + 0.06f), new Vector3(p, top + 0.06f), Color(Shade(color, 0.75f)));
            if (_camera.TryProject(new Vector3(table.Position, top + 0.06f), out Vector2 c) &&
                _camera.TryProject(new Vector3(p, top + 0.06f), out Vector2 ps) && _camera.TryProject(new Vector3(q, top + 0.06f), out Vector2 qs))
                draw.AddTriangleFilled(c, ps, qs, Color(color));
        }
    }

    private void DrawChair(ImDrawListPtr draw, FurnitureChair chair, Vector4 color)
    {
        if (chair.State == ChairState.Destroyed) return;
        float h = chair.Height;
        bool tipped = chair.State is ChairState.Fallen or ChairState.Airborne;
        if (tipped)
        {
            DrawOrientedPrism(draw, chair.Position, -0.35f, 0.35f, -0.24f, 0.24f, h + 0.04f, h + 0.13f, chair.Heading, color);
            DrawOrientedPrism(draw, chair.Position, -0.35f, -0.26f, -0.24f, 0.24f, h + 0.10f, h + 0.50f, chair.Heading, color);
            return;
        }
        foreach (float forward in new[] { -0.20f, 0.20f })
            foreach (float side in new[] { -0.20f, 0.20f })
                DrawOrientedPrism(draw, chair.Position, forward - 0.025f, forward + 0.025f, side - 0.025f, side + 0.025f, 0f, 0.44f, chair.Heading, Shade(color, 0.7f));
        DrawOrientedPrism(draw, chair.Position, -0.24f, 0.24f, -0.24f, 0.24f, 0.43f, 0.49f, chair.Heading, color);
        DrawOrientedPrism(draw, chair.Position, -0.25f, -0.19f, -0.24f, 0.24f, 0.49f, 0.94f, chair.Heading, color);
    }

    private void DrawSeatedPerson(ImDrawListPtr draw, Vector2 position, float heading, Vector4 color, float rise)
    {
        // Builder seat previews do not pass through DrawAgent, but need the same contrast.
        bool previous = _outlineAgentGeometry;
        _outlineAgentGeometry = true;
        try { DrawSeatedPersonGeometry(draw, position, heading, color, rise); }
        finally { _outlineAgentGeometry = previous; }
    }

    private void DrawSeatedPersonGeometry(ImDrawListPtr draw, Vector2 position, float heading, Vector4 color, float rise)
    {
        float hip = 0.49f + 0.40f * rise;
        Vector4 limb = Shade(color, 0.78f);
        DrawOrientedPrism(draw, position, -0.12f, 0.13f, -0.19f, 0.19f, hip, hip + 0.48f, heading, color);
        DrawOrientedPrism(draw, position, -0.10f, 0.14f, -0.12f, 0.12f, hip + 0.50f, hip + 0.75f, heading, Shade(color, 1.1f));
        foreach (float side in new[] { -0.12f, 0.12f })
        {
            float knee = 0.38f * (1f - rise);
            DrawOrientedPrism(draw, position, -0.08f, knee + 0.10f, side - 0.075f, side + 0.075f, hip - 0.12f, hip, heading, limb);
            DrawOrientedPrism(draw, position, knee - 0.06f, knee + 0.08f, side - 0.07f, side + 0.07f, 0.04f, hip - 0.10f, heading, limb);
            DrawOrientedPrism(draw, position, -0.05f, 0.13f, side * 1.9f - 0.06f, side * 1.9f + 0.06f, hip + 0.04f, hip + 0.42f, heading, limb);
        }
    }
}
