using System;
using System.Collections.Generic;
using CrowdSim.Core.Configuration;

namespace CrowdSim.Workspace
{
    /// <summary>Shared authoring metadata. Values and dependency clamps are owned by the unchanged fume configuration.</summary>
    public sealed class FumeControl
    {
        public string Label { get; }
        public string Help { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public FumeControl(string label, float min, float max, string help)
        { Label = label; Minimum = min; Maximum = max; Help = help; }
    }
    public static class FumeControls
    {
        public static IReadOnlyList<FumeControl> All { get; } = Array.AsReadOnly(new[] {
            new FumeControl("Initial radius (m)", .2f, 5, "Initial connected contaminated area at release."),
            new FumeControl("Dispersion speed (m/s)", .05f, 2, "Setup-time obstacle-aware propagation speed."),
            new FumeControl("Maximum extent (m)", 2, 60, "Maximum path distance; never less than the initial radius."),
            new FumeControl("Release duration (s)", 1, 180, "Supply duration at each reached cell, followed by decay."),
            new FumeControl("Drift direction (deg)", 0, 359, "0° is +X; 90° is +Y on the room plan."),
            new FumeControl("Directional drift", 0, 1, "0 is symmetric; 1 strongly favors the drift direction."),
            new FumeControl("Awareness concentration", .02f, .8f, "Normalized concentration that begins direct awareness."),
            new FumeControl("High-threat concentration", .07f, .98f, "Must be at least 0.05 above awareness."),
            new FumeControl("Impairment exposure", 1, 120, "Cumulative normalized concentration-seconds before mobility loss."),
            new FumeControl("Incapacitation exposure", 2, 300, "At least 1 above impairment; incapacity persists for the run.")
        });
        public static float[] Read(ToxicFumeConfig c) => new[] { c.InitialRadiusMeters, c.DispersionSpeedMetersPerSecond,
            c.MaximumExtentMeters, c.ReleaseDurationSeconds, c.DriftDirectionDegrees, c.DriftBias, c.AwarenessConcentration,
            c.DangerConcentration, c.ImpairmentExposure, c.IncapacitationExposure };
        public static void Apply(ToxicFumeConfig c, float[] v) => c.SetValues(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]);
    }
}
