namespace CrowdSim.OpenGL.UI;

internal enum AppMode
{
    Builder,
    Simulation,
}

internal enum BuilderTool
{
    Exit,
    Blockage,
    CircularBlockage,
    Agents,
}

/// <summary>
/// User-facing blockage choices. The builder keeps square/circular placement as separate
/// internal tools, while the UI exposes them through one intuitive type/size dropdown.
/// </summary>
internal enum BlockagePreset
{
    SquareOneCell,
    SquareTwoCells,
    CircularTwoPointFiveMetres,
}

internal enum WorldInteractionMode
{
    SelectAgent,
    PlaceExplosion,
}
