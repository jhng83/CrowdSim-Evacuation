using System.Numerics;

namespace CrowdSim.OpenGL.UI;

/// <summary>Shared colors for the abstract blast-zone analysis overlay and its right-side legend.</summary>
public static class BlastZoneStyle
{
    public static readonly Vector4 Kill = new(0.96f, 0.08f, 0.06f, 1f);
    public static readonly Vector4 Incapacitation = new(1.00f, 0.42f, 0.06f, 1f);
    public static readonly Vector4 Fallen = new(1.00f, 0.82f, 0.08f, 1f);
    public static readonly Vector4 Injury = new(0.12f, 0.78f, 1.00f, 1f);
    public static readonly Vector4 Panic = new(0.88f, 0.90f, 0.94f, 1f);
}
