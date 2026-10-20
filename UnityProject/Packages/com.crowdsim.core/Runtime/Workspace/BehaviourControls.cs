#nullable enable
using System;
using System.Collections.Generic;
using CrowdSim.Core.Configuration;

namespace CrowdSim.Workspace
{
    /// <summary>The fourteen reference behaviour controls in stable UI order, with the original bounds.</summary>
    public sealed class BehaviourControl
    {
        public string Label { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        internal BehaviourControl(string label, float min, float max) { Label = label; Minimum = min; Maximum = max; }
    }
    public static class BehaviourControls
    {
        public static readonly IReadOnlyList<BehaviourControl> All = Array.AsReadOnly(new[] {
            new BehaviourControl("Trait variation", 0, 1), new BehaviourControl("Reaction variation", 0, 1),
            new BehaviourControl("Stress susceptibility", .6f, 1.4f), new BehaviourControl("Stress persistence", .5f, 2),
            new BehaviourControl("Crowd following", 0, 1), new BehaviourControl("Personal space", .85f, 1.3f),
            new BehaviourControl("Panic space compression", 0, .35f), new BehaviourControl("Emergency urgency", .8f, 1.3f),
            new BehaviourControl("Max running speed (m/s)", BehaviorTuningConfig.MinimumRunningSpeedLimitMetersPerSecond, BehaviorTuningConfig.MaximumRunningSpeedLimitMetersPerSecond),
            new BehaviourControl("Helping tendency", 0, 1), new BehaviourControl("Route familiarity", 0, 1),
            new BehaviourControl("Bottleneck assertiveness", 0, 1), new BehaviourControl("Pressure sensitivity", .5f, 1.5f),
            new BehaviourControl("Pressure space compression", 0, .35f) });
        internal static float[] Read(BehaviorTuningConfig b) => new[] { b.TraitVariation, b.ReactionVariation, b.StressSusceptibility,
            b.StressPersistence, b.CrowdFollowing, b.PersonalSpace, b.PanicSpaceCompression, b.EmergencyUrgency,
            b.MaxRunningSpeedMetersPerSecond, b.HelpingTendency, b.RouteFamiliarity, b.BottleneckAssertiveness,
            b.BottleneckPressureSensitivity, b.BottleneckSpaceCompression };
        internal static void Set(BehaviorTuningConfig b, int index, float value)
        {
            if (index < 0 || index >= All.Count || !float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(index));
            float[] v = Read(b);
            v[index] = Math.Clamp(value, All[index].Minimum, All[index].Maximum);
            b.SetValues(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[9], v[10], v[11], v[12], v[13]);
            b.SetMaxRunningSpeed(v[8]);
        }
    }
}
