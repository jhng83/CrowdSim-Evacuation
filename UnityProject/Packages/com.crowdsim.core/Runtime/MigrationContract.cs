using System;

namespace CrowdSim.Migration
{
    /// <summary>Shared build and startup metadata; contains no simulation implementation.</summary>
    public static class MigrationContract
    {
        public const string EditorVersion = "6000.3.26f1";
        public const string UrpVersion = "17.3.0";
        public const string ReferenceCommit = "01b435568f211f5c8ddf99b32b05e7ceb1ad3a24";
        public const string BootstrapScene = "Assets/CrowdSim/Scenes/Bootstrap.unity";
        public const string Milestone = "M9 - Diagnostics, inspection and crowd scale";
        public const string ParityScene = "Assets/CrowdSim/Scenes/Validation/SimulationParity.unity";
        public const float ReferenceStepSeconds = 0.15f;

        /// <summary>Require the approved patch; another patch is an explicit migration decision.</summary>
        public static bool IsExpectedEditor(string version)
        {
            return string.Equals(version, EditorVersion, StringComparison.Ordinal);
        }
    }
}
