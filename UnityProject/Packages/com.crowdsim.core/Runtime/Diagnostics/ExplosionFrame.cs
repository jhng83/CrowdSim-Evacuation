#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    /// <summary>Immutable Explosion analysis and display values, sampled on model ticks rather than render time.</summary>
    public sealed class ExplosionFrame
    {
        public bool Detonated { get; }
        public Vector2? Origin { get; }
        public int TriggerTick { get; }
        public float EventSeconds { get; }
        public float ShockRadius { get; }
        public int ShockHits { get; }
        public int Shielded { get; }
        public int DebrisAlive { get; }
        public int DebrisHits { get; }
        public int DebrisInjuries { get; }
        public int DebrisIncapacitations { get; }
        public int DebrisFatalities { get; }
        public int Fallen { get; }
        public int TemporaryIncapacitated { get; }
        public int Incapacitated { get; }
        public int Crawling { get; }
        public float AverageVisibility { get; }
        public IReadOnlyList<BlastMeshData> Zones { get; }
        public BlastMeshData? Shock { get; }
        public IReadOnlyList<BlastMeshData> Dust { get; }
        public static ExplosionFrame Capture(Simulation simulation, BlastGeometryCache cache) => new ExplosionFrame(simulation, cache);

        internal ExplosionFrame(Simulation simulation, BlastGeometryCache cache)
        {
            Explosion? explosion = simulation.Environment.Explosion;
            Detonated = explosion != null; Origin = explosion?.Position; TriggerTick = explosion?.TriggeredFrame ?? -1;
            EventSeconds = explosion == null ? 0 : Math.Max(0, simulation.FrameIndex - explosion.TriggeredFrame) * simulation.Config.Agent.Dt;
            ShockHits = simulation.Agents.Count(a => a.ShockwaveHit); Shielded = simulation.Agents.Count(a => a.IsActive && a.BlastShielded);
            DebrisAlive = simulation.Debris.Count; DebrisHits = simulation.DebrisHits; DebrisInjuries = simulation.DebrisInjuries;
            DebrisIncapacitations = simulation.DebrisIncapacitations; DebrisFatalities = simulation.DebrisFatalities;
            Fallen = simulation.Agents.Count(a => a.State == AgentState.Fallen);
            TemporaryIncapacitated = simulation.Agents.Count(a => a.State == AgentState.TempIncap);
            Incapacitated = simulation.Agents.Count(a => a.State == AgentState.Incapacitated);
            Crawling = simulation.Agents.Count(a => a.State == AgentState.Crawling);
            Agent[] mobile = simulation.Agents.Where(a => a.IsActive && a.State != AgentState.Dead && a.State != AgentState.Incapacitated && a.State != AgentState.TempIncap && a.State != AgentState.Fallen).ToArray();
            AverageVisibility = mobile.Length == 0 ? 1 : mobile.Average(a => a.VisibilityFactor);
            Zones = cache.Get(simulation); Dust = Array.Empty<BlastMeshData>();
            if (explosion == null) return;
            var config = simulation.Config.Agent;
            float elapsed = Math.Max(0, simulation.FrameIndex - explosion.TriggeredFrame) * config.StepScale;
            ShockRadius = Math.Min(config.HeatRadius, elapsed * config.ShockwaveSpeedMetersPerFrame);
            int end = (int)MathF.Ceiling(config.HeatRadius / Math.Max(config.ShockwaveSpeedMetersPerFrame, 1e-5f)) + 3;
            if (elapsed <= end && ShockRadius > 1)
                Shock = BlastGeometry.Band(simulation, explosion.Position, Math.Max(.04f, ShockRadius - .28f), ShockRadius + .20f,
                    new Vector4(.82f, .92f, 1, 1), .08f, 96, 112);
            float dustEnd = config.DustVisibilityRadius / Math.Max(config.ShockwaveSpeedMetersPerFrame, 1e-5f) * CrowdSim.Core.Configuration.AgentConfig.ReferenceStepSeconds +
                config.DustBuildSeconds + config.DustHoldSeconds + config.DustDecaySeconds;
            if (EventSeconds > 0 && EventSeconds < dustEnd) Dust = BlastGeometry.Dust(simulation, explosion.Position);
        }
    }
}
