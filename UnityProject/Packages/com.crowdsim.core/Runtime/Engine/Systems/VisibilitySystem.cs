#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Numerics;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    /// <summary>
    /// Temporary post-event visibility degradation used only for perception and route choice.
    /// The field is deliberately abstract: it models a short-lived dust/visual-obscuration effect
    /// without attempting to reproduce real smoke or explosive-yield physics.
    /// </summary>
    public sealed class VisibilitySystem
    {
        private readonly AgentConfig _config;

        public VisibilitySystem(AgentConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Returns visual clarity in [MinimumVisibilityFactor, 1]. Walls shield the field so
        /// obscuration does not propagate straight through solid geometry.
        /// </summary>
        public float GetVisibilityFactor(Simulation simulation, Vector2 position)
        {
            Explosion? explosion = simulation.Environment.Explosion;
            if (explosion is null)
            {
                return 1f;
            }

            float eventSeconds = Math.Max(0f, (simulation.FrameIndex - explosion.TriggeredFrame) * _config.Dt);
            float distance = Vector2.Distance(position, explosion.Position);
            if (!float.IsFinite(distance) || distance >= _config.DustVisibilityRadius)
            {
                return 1f;
            }

            // The temporary obscuration begins locally after the same abstract propagating front reaches
            // the point; this avoids degrading perception ahead of the visible/event wave.
            float arrivalSeconds = distance / Math.Max(_config.ShockwaveSpeedMetersPerFrame, 1e-5f) * AgentConfig.ReferenceStepSeconds;
            float localSeconds = eventSeconds - arrivalSeconds;
            if (localSeconds < 0f)
            {
                return 1f;
            }

            float temporal = TemporalEnvelope(localSeconds);
            if (temporal <= 0f)
            {
                return 1f;
            }

            // A wall between the event and the observer blocks the direct dust field. Social
            // contagion can still reach the observer later through visible running pedestrians.
            if (!simulation.HasLineOfSight(explosion.Position, position))
            {
                return 1f;
            }

            float spatial = 1f - Math.Clamp(distance / Math.Max(_config.DustVisibilityRadius, 1e-5f), 0f, 1f);
            spatial *= spatial;
            float loss = _config.DustPeakVisibilityLoss * temporal * spatial;
            return Math.Clamp(1f - loss, _config.MinimumVisibilityFactor, 1f);
        }

        private float TemporalEnvelope(float seconds)
        {
            float build = Math.Max(0.05f, _config.DustBuildSeconds);
            float holdEnd = build + Math.Max(0f, _config.DustHoldSeconds);
            float decay = Math.Max(0.05f, _config.DustDecaySeconds);

            if (seconds < build)
            {
                return Math.Clamp(seconds / build, 0f, 1f);
            }

            if (seconds <= holdEnd)
            {
                return 1f;
            }

            float afterHold = seconds - holdEnd;
            if (afterHold >= decay)
            {
                return 0f;
            }

            return 1f - afterHold / decay;
        }
    }
}
