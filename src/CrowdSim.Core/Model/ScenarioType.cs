namespace CrowdSim.Core.Model;

/// <summary>
/// Top-level emergency scenario selected before scene construction. The crowd engine, SFM,
/// routing, recording and metrics remain shared; only the hazard subsystem differs.
/// </summary>
public enum ScenarioType
{
    Explosion = 0,
    ToxicFume = 1,
}

/// <summary>Runtime exposure band for the abstract normalized toxic-fume model.</summary>
public enum ToxicExposureStatus
{
    Clear = 0,
    Exposed = 1,
    Impaired = 2,
    Incapacitated = 3,
}
