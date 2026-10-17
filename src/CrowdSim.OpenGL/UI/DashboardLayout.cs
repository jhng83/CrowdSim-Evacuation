namespace CrowdSim.OpenGL.UI;

/// <summary>
/// Allocates the workspace in logical pixels. Panel visibility changes the viewport width,
/// never the physical simulation or camera state. Narrow windows shrink both side panels.
/// </summary>
internal readonly record struct DashboardLayout(
    float ControlsWidth,
    float ViewportWidth,
    float InspectorWidth,
    float Gap)
{
    public const float PreferredControlsWidth = 336f;
    public const float PreferredInspectorWidth = 360f;

    /// <summary>Return positive widths whose sum, including gaps, fits the available space.</summary>
    public static DashboardLayout Calculate(float width, bool inspectorVisible, float gap = 10f)
    {
        if (!float.IsFinite(width) || width <= 0f)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!float.IsFinite(gap) || gap < 0f)
            throw new ArgumentOutOfRangeException(nameof(gap));

        float actualGap = Math.Min(gap, width * 0.04f);
        // Control width is independent of inspector visibility, so hiding the inspector gives
        // all of its width and its inter-panel gap back to the scene rather than to the controls.
        float controls = Math.Min(PreferredControlsWidth, width * 0.27f);
        float inspector = inspectorVisible ? Math.Min(PreferredInspectorWidth, width * 0.29f) : 0f;
        int gaps = inspectorVisible ? 2 : 1;
        float viewport = width - controls - inspector - actualGap * gaps;
        return new DashboardLayout(controls, viewport, inspector, actualGap);
    }
}
