#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
namespace CrowdSim.Core.Configuration
{
    /// <summary>
    /// Scenario-level parameters for the abstract toxic-fume model. Concentration is normalized
    /// to [0,1]; these values are intentionally generic and are not calibrated to a named chemical.
    /// </summary>
    public sealed class ToxicFumeConfig
    {
        public const float DefaultInitialRadiusMeters = 0.60f;
        public const float DefaultDispersionSpeedMetersPerSecond = 0.30f;
        public const float DefaultMaximumExtentMeters = 12.0f;
        public const float DefaultReleaseDurationSeconds = 30.0f;
        public const float DefaultDriftDirectionDegrees = 0.0f;
        public const float DefaultDriftBias = 0.0f;
        public const float DefaultGridCellSizeMeters = 0.50f;
        public const float DefaultAwarenessConcentration = 0.12f;
        public const float DefaultDangerConcentration = 0.55f;
        public const float DefaultImpairmentExposure = 8.0f;
        public const float DefaultIncapacitationExposure = 20.0f;
        public const float DefaultPostReleaseDecaySeconds = 24.0f;

        public float InitialRadiusMeters { get; private set; } = DefaultInitialRadiusMeters;
        public float DispersionSpeedMetersPerSecond { get; private set; } = DefaultDispersionSpeedMetersPerSecond;
        public float MaximumExtentMeters { get; private set; } = DefaultMaximumExtentMeters;
        public float ReleaseDurationSeconds { get; private set; } = DefaultReleaseDurationSeconds;
        public float DriftDirectionDegrees { get; private set; } = DefaultDriftDirectionDegrees;
        public float DriftBias { get; private set; } = DefaultDriftBias;
        public float GridCellSizeMeters { get; private set; } = DefaultGridCellSizeMeters;
        public float AwarenessConcentration { get; private set; } = DefaultAwarenessConcentration;
        public float DangerConcentration { get; private set; } = DefaultDangerConcentration;
        public float ImpairmentExposure { get; private set; } = DefaultImpairmentExposure;
        public float IncapacitationExposure { get; private set; } = DefaultIncapacitationExposure;
        public float PostReleaseDecaySeconds { get; private set; } = DefaultPostReleaseDecaySeconds;

        /// <summary>Monotonic setup revision used to invalidate precomputed plume caches.</summary>
        public int Revision { get; private set; }

        /// <summary>Apply validated generic fume settings in one atomic update.</summary>
        public void SetValues(
            float initialRadiusMeters,
            float dispersionSpeedMetersPerSecond,
            float maximumExtentMeters,
            float releaseDurationSeconds,
            float driftDirectionDegrees,
            float driftBias,
            float awarenessConcentration,
            float dangerConcentration,
            float impairmentExposure,
            float incapacitationExposure)
        {
            InitialRadiusMeters = ClampFinite(initialRadiusMeters, 0.20f, 5.00f, DefaultInitialRadiusMeters);
            DispersionSpeedMetersPerSecond = ClampFinite(
                dispersionSpeedMetersPerSecond, 0.05f, 2.00f, DefaultDispersionSpeedMetersPerSecond);
            MaximumExtentMeters = ClampFinite(
                maximumExtentMeters, Math.Max(2.00f, InitialRadiusMeters), 60.0f, DefaultMaximumExtentMeters);
            ReleaseDurationSeconds = ClampFinite(releaseDurationSeconds, 1.0f, 180.0f, DefaultReleaseDurationSeconds);
            DriftDirectionDegrees = NormalizeDegrees(driftDirectionDegrees);
            DriftBias = ClampFinite(driftBias, 0.0f, 1.0f, DefaultDriftBias);
            AwarenessConcentration = ClampFinite(awarenessConcentration, 0.02f, 0.80f, DefaultAwarenessConcentration);
            DangerConcentration = ClampFinite(
                dangerConcentration,
                Math.Min(0.95f, AwarenessConcentration + 0.05f),
                0.98f,
                DefaultDangerConcentration);
            ImpairmentExposure = ClampFinite(impairmentExposure, 1.0f, 120.0f, DefaultImpairmentExposure);
            IncapacitationExposure = ClampFinite(
                incapacitationExposure,
                ImpairmentExposure + 1.0f,
                300.0f,
                DefaultIncapacitationExposure);
            Revision++;
        }

        public void ResetDefaults()
            => SetValues(
                DefaultInitialRadiusMeters,
                DefaultDispersionSpeedMetersPerSecond,
                DefaultMaximumExtentMeters,
                DefaultReleaseDurationSeconds,
                DefaultDriftDirectionDegrees,
                DefaultDriftBias,
                DefaultAwarenessConcentration,
                DefaultDangerConcentration,
                DefaultImpairmentExposure,
                DefaultIncapacitationExposure);

        private static float ClampFinite(float value, float minimum, float maximum, float fallback)
            => Math.Clamp(float.IsFinite(value) ? value : fallback, minimum, maximum);

        private static float NormalizeDegrees(float value)
        {
            if (!float.IsFinite(value))
            {
                return 0f;
            }

            value %= 360f;
            return value < 0f ? value + 360f : value;
        }
    }
}
