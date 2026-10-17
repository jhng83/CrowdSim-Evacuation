namespace CrowdSim.OpenGL.UI;

/// <summary>
/// Rendering-only diagnostics. These flags never alter the simulation state or crowd physics.
/// </summary>
public sealed class RenderDebugOptions
{
    public bool ShowDesiredDirection { get; set; }
    public bool ShowAvoidanceDirection { get; set; }
    public bool ShowHerdDirection { get; set; }
    public bool ShowSfmForce { get; set; }
    public bool ShowBlastDirection { get; set; }
    public bool ShowVisionCone { get; set; }
    public bool ShowCongestion { get; set; }
    public bool ShowTrails { get; set; }

    // Persistent post-event analysis zones. Rendering-only: these flags never re-apply damage.
    public bool ShowBlastZones { get; set; } = true;
    public bool ShowKillZone { get; set; } = true;
    public bool ShowIncapacitationZone { get; set; } = true;
    public bool ShowFallenZone { get; set; } = true;
    public bool ShowInjuryZone { get; set; } = true;
    public bool ShowPanicZone { get; set; } = true;
}
