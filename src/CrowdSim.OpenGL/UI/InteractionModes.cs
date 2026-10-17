namespace CrowdSim.OpenGL.UI;

internal enum AppMode
{
    ScenarioSelection,
    Builder,
    Simulation,
}

internal enum BuilderTool
{
    Exit,
    Blockage,
    CircularBlockage,
    Agents,
    RoundTableSet,
    SingleChair,
    ToxicFumeSource,
    RectangularTable,
    Stage,
    Barricade,
}

internal enum WorldInteractionMode
{
    SelectAgent,
    PlaceExplosion,
}
