using System.Numerics;

namespace CrowdSim.Core.Model;

/// <summary>Exit opening carved into a boundary wall.</summary>
public sealed class Exit
{
    public Exit(string name, Vector2 position, float size = 0.32f, string side = "N")
    {
        Name = name;
        Position = position;
        Size = size;
        Side = side;
    }

    public string Name { get; }
    public Vector2 Position { get; }
    public float Size { get; }
    public string Side { get; }
}

/// <summary>Explosion trigger position and simulation frame.</summary>
public sealed record Explosion(Vector2 Position, int TriggeredFrame);

/// <summary>
/// Builder-time population placement. Positions are resolved when the user clicks the room so
/// the exact start layout can be reviewed, saved and reproduced on restart.
/// </summary>
public sealed class AgentSpawnGroup
{
    public AgentSpawnGroup(Vector2 center, IEnumerable<Vector2> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        Center = center;
        Positions = positions.ToList();
    }

    public Vector2 Center { get; }
    public List<Vector2> Positions { get; }
    public int Count => Positions.Count;
}

/// <summary>Per-exit raw density and instantaneous flow rate.</summary>
public readonly record struct ExitMetrics(float Density, float Flow);
