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
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    /// <summary>
    /// Abstract event-effects subsystem: wall-aware propagating shock front, ordered casualty outcomes,
    /// bounded knockback/stun, blast wind and lightweight outward-moving debris. The model intentionally
    /// avoids real explosive-yield or fragmentation calculations; it uses normalized simulation severity.
    /// </summary>
    public sealed class ExplosionSystem
    {
        private readonly AgentConfig _config;
        private readonly List<Agent> _debrisAgentScratch = new(64);
        private readonly List<Debris> _debrisSurvivorScratch = new(64);

        public ExplosionSystem(AgentConfig config)
        {
            _config = config;
        }

        /// <summary>Return whether any solid wall rectangle blocks the segment from start to end.</summary>
        public static bool BlastPathIsBlocked(Vector2 start, Vector2 end, IReadOnlyList<Wall> walls)
        {
            float startX = start.X;
            float startY = start.Y;
            float deltaX = end.X - startX;
            float deltaY = end.Y - startY;

            foreach (Wall wall in walls)
            {
                if (wall.IsFurniture) continue;
                float minimumX = Math.Min(wall.X1, wall.X2);
                float maximumX = Math.Max(wall.X1, wall.X2);
                float minimumY = Math.Min(wall.Y1, wall.Y2);
                float maximumY = Math.Max(wall.Y1, wall.Y2);

                float nearTime = 0f;
                float farTime = 1f;
                bool intersects =
                    ClipAxis(startX, deltaX, minimumX, maximumX, ref nearTime, ref farTime) &&
                    ClipAxis(startY, deltaY, minimumY, maximumY, ref nearTime, ref farTime);

                if (intersects)
                {
                    return true;
                }
            }

            return false;
        }

        public void TriggerExplosion(Simulation simulation, float x, float y)
        {
            if (!simulation.Running || simulation.Environment.HasExplosion())
            {
                return;
            }

            simulation.Environment.SetExplosion(x, y, simulation.FrameIndex);
            simulation.ExplosionTriggerFrame = simulation.FrameIndex;
            Vector2 explosionPosition = simulation.Environment.Explosion!.Position;

            simulation.DebrisHits = 0;
            simulation.DebrisInjuries = 0;
            simulation.DebrisIncapacitations = 0;
            simulation.DebrisFatalities = 0;
            simulation.Debris.Clear();
            simulation.Debris.AddRange(Debris.SpawnCluster(
                explosionPosition,
                count: _config.DebrisCount,
                speedMin: _config.DebrisSpeedMin,
                speedMax: _config.DebrisSpeedMax,
                ttlMin: 25,
                ttlMax: 70,
                radius: 0.24f,
                simulation.Random));

            // Initialise per-agent shock state. Startle/panic is not applied globally here: direct
            // event response begins when the propagating front reaches the agent. Social/running-crowd
            // contagion can still cause an earlier reaction if the agent observes others first.
            foreach (Agent agent in simulation.Agents)
            {
                agent.ShockwaveHit = false;
                agent.ShockwaveHitFrame = -1;
                agent.BlastShielded = !simulation.HasLineOfSight(
                    explosionPosition,
                    agent.Position);
            }
        }

        public void ApplyExplosionEffects(Simulation simulation)
        {
            Explosion? explosion = simulation.Environment.Explosion;
            if (explosion is null)
            {
                return;
            }

            float t = (simulation.FrameIndex - explosion.TriggeredFrame) * _config.StepScale;
            if (t < 0 || t > _config.BlastWindDurationFrames + 5)
            {
                return;
            }

            float shockRadius = Math.Min(
                _config.HeatRadius,
                _config.ShockwaveSpeedMetersPerFrame * t);

            const float knockMaxMeters = 0.61f;
            const float knockMinMeters = 0.16f;
            float knockDecay = Math.Max(0.04f, 0.5f * _config.InjuryRadius);
            const int knockSubsteps = 3;
            const int stunMinFrames = 10;
            const int stunMaxFrames = 15;

            foreach (Agent agent in simulation.Agents)
            {
                if (!agent.IsActive || agent.State == AgentState.Dead)
                {
                    continue;
                }

                Vector2 offset = agent.Position - explosion.Position;
                if (!VectorMath.IsFinite(offset))
                {
                    continue;
                }

                float distance = offset.Length();
                if (!float.IsFinite(distance) || distance > _config.HeatRadius)
                {
                    continue;
                }

                // Re-evaluate shielding against the agent's current position. An agent that reaches
                // cover before the front arrives should be protected; one that leaves cover can become exposed.
                agent.BlastShielded = !simulation.HasLineOfSight(
                    explosion.Position,
                    agent.Position);
                if (agent.BlastShielded)
                {
                    continue;
                }

                Vector2 direction = distance < VectorMath.Epsilon
                    ? VectorMath.RandomUnit(simulation.Random)
                    : offset / distance;

                // Casualty and knockback effects are triggered once, when the visible/physical shock
                // front reaches the agent. This keeps simulation timing consistent with the renderer.
                if (!agent.ShockwaveHit && distance <= shockRadius)
                {
                    ApplyOrderedShockOutcome(simulation, agent, distance);

                    if (agent.State is not (AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap or AgentState.Fallen))
                    {
                        float impulse = _config.ShockwavePeakImpulse *
                            MathF.Exp(-distance / Math.Max(_config.ShockwaveDecayLength, 1e-5f));
                        agent.Velocity += direction * impulse;

                        if ((agent.State is AgentState.WaitingToReact or AgentState.Evacuating or AgentState.Injured) &&
                            distance > _config.FallenRadius)
                        {
                            float rawKnock = knockMaxMeters * MathF.Exp(-distance / Math.Max(knockDecay, 1e-5f));
                            float knockDistance = Math.Clamp(rawKnock, knockMinMeters, knockMaxMeters);
                            float step = knockDistance / knockSubsteps;

                            for (int substep = 0; substep < knockSubsteps; substep++)
                            {
                                agent.Position += direction * step;
                                agent.Position = VectorMath.ClampToBounds(
                                    agent.Position,
                                    simulation.Config.World.WidthMeters,
                                    simulation.Config.World.LengthMeters);
                                simulation.ResolveSolidCollisions(agent);
                            }

                            float severity = Math.Clamp(1f - distance / _config.HeatRadius, 0f, 1f);
                            int stunFrames = _config.ReferenceFramesToTicks((int)(stunMinFrames + severity * (stunMaxFrames - stunMinFrames)));
                            agent.FreezeUntilFrame = Math.Max(
                                agent.FreezeUntilFrame,
                                simulation.FrameIndex + stunFrames);
                            agent.StunnedUntilFrame = simulation.FrameIndex + stunFrames;
                        }
                    }

                    agent.ShockwaveHit = true;
                    agent.ShockwaveHitFrame = simulation.FrameIndex;

                    // A pedestrian may already be evacuating because they observed the emergency
                    // before the physical front reached them. Direct exposure should still be able
                    // to produce the short threat-escape intent instead of leaving that earlier
                    // social route completely unchanged.
                    if (agent.State is AgentState.Evacuating or AgentState.Injured)
                    {
                        simulation.EvacuationSystem.BeginPurposefulResponse(simulation, agent);
                    }
                }

                // Short bounded post-front blast wind. It cannot push terminal casualty states.
                if (t <= _config.BlastWindDurationFrames &&
                    distance <= shockRadius &&
                    agent.State is not (AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap or AgentState.Fallen))
                {
                    float wind = _config.BlastWindStrength *
                        MathF.Exp(-distance / Math.Max(_config.ShockwaveDecayLength, 1e-5f));
                    agent.Velocity += direction * (wind * _config.StepScale);
                }

                if (!VectorMath.IsFinite(agent.Velocity))
                {
                    agent.Velocity = Vector2.Zero;
                }

                agent.Velocity = VectorMath.ClampMagnitude(agent.Velocity, _config.MaxSpeed);
            }
        }

        public void UpdateDebris(Simulation simulation)
        {
            if (simulation.Debris.Count == 0)
            {
                return;
            }

            _debrisSurvivorScratch.Clear();
            foreach (Debris fragment in simulation.Debris)
            {
                if (!fragment.Advance(simulation.Config.World, _config))
                {
                    continue;
                }

                // Swept segment collision prevents fast fragments tunnelling through a thin wall.
                if (!simulation.HasLineOfSight(fragment.PreviousPosition, fragment.Position))
                {
                    fragment.Ttl = 0;
                    continue;
                }

                Agent? hitAgent = null;
                float hitDistance = float.PositiveInfinity;
                float hitRadius = fragment.Radius + simulation.Config.Agent.Radius;
                Vector2 segmentMidpoint = 0.5f * (fragment.PreviousPosition + fragment.Position);
                float segmentHalfLength = 0.5f * Vector2.Distance(fragment.PreviousPosition, fragment.Position);
                simulation.CollectAgentsNearPoint(
                    segmentMidpoint,
                    segmentHalfLength + hitRadius,
                    _debrisAgentScratch);

                foreach (Agent agent in _debrisAgentScratch)
                {
                    if (!agent.IsActive || agent.State == AgentState.Exited || agent.State == AgentState.Dead)
                    {
                        continue;
                    }

                    float distance = DistancePointToSegment(agent.Position, fragment.PreviousPosition, fragment.Position);
                    if (distance <= hitRadius && distance < hitDistance)
                    {
                        hitAgent = agent;
                        hitDistance = distance;
                    }
                }

                if (hitAgent is not null)
                {
                    ApplyDebrisImpact(simulation, hitAgent, fragment);
                    fragment.Ttl = 0; // one fragment cannot damage multiple people/frames
                    continue;
                }

                _debrisSurvivorScratch.Add(fragment);
            }

            simulation.Debris.Clear();
            simulation.Debris.AddRange(_debrisSurvivorScratch);
        }

        private void ApplyOrderedShockOutcome(Simulation simulation, Agent agent, float distance)
        {
            if (distance <= _config.HardKillRadius)
            {
                agent.MarkDead();
                return;
            }

            int directAlarmFrames = Math.Max(
                1,
                (int)MathF.Round(_config.DirectAlarmSignalSeconds / Math.Max(_config.Dt, 1e-5f)));
            agent.ActivateDirectAlarm(
                simulation.FrameIndex,
                directAlarmFrames,
                _config.DirectAlarmPanicFloor,
                simulation.Environment.Explosion?.TriggeredFrame ?? simulation.FrameIndex);

            if (distance <= _config.HeatIncapacityRadius)
            {
                agent.MarkTempIncap(simulation.FrameIndex, simulation.Random);
                agent.PanicLevel = Math.Max(agent.PanicLevel, 0.8f);
                return;
            }

            if (distance <= _config.FallenRadius)
            {
                agent.MarkFallen(simulation.FrameIndex, simulation.Random);
                agent.PanicLevel = Math.Max(agent.PanicLevel, 0.8f);
                return;
            }

            // Mobile survivors enter the same bounded startle/reaction sequence whether their
            // eventual response is direct evacuation or an injury-limited evacuation.
            if (agent.State == AgentState.Wandering)
            {
                int freezeUntil = simulation.FrameIndex + RandomFrameRange(
                    simulation.Random,
                    _config.FreezeMinFrames,
                    _config.FreezeMaxFrames);
                agent.BeginReaction(simulation.FrameIndex, freezeUntil);
            }

            if (distance <= _config.InjuryRadius &&
                agent.State is (AgentState.Evacuating or AgentState.WaitingToReact or AgentState.Injured))
            {
                agent.MarkInjured(preserveReactionState: agent.State == AgentState.WaitingToReact);
            }

            agent.PanicLevel = Math.Max(agent.PanicLevel, 0.8f);
        }

        internal void ApplyDebrisImpact(Simulation simulation, Agent agent, Debris fragment)
        {
            float speedFactor = Math.Clamp(
                fragment.Velocity.Length() / Math.Max(_config.DebrisReferenceSpeed, 1e-5f),
                0f,
                1f);
            float severity = Math.Clamp(fragment.Energy * (0.45f + 0.55f * speedFactor), 0f, 1f);

            simulation.DebrisHits++;
            agent.PanicLevel = Math.Max(agent.PanicLevel, Math.Clamp(0.35f + 0.45f * severity, 0f, 0.9f));
            int directAlarmFrames = Math.Max(
                1,
                (int)MathF.Round(_config.DirectAlarmSignalSeconds / Math.Max(_config.Dt, 1e-5f)));
            agent.ActivateDirectAlarm(
                simulation.FrameIndex,
                directAlarmFrames,
                _config.DirectAlarmPanicFloor,
                simulation.Environment.Explosion?.TriggeredFrame ?? simulation.FrameIndex);

            if (agent.State == AgentState.Wandering && severity < _config.DebrisFallenThreshold)
            {
                int freezeUntil = simulation.FrameIndex + RandomFrameRange(
                    simulation.Random,
                    _config.FreezeMinFrames,
                    _config.FreezeMaxFrames);
                agent.BeginReaction(simulation.FrameIndex, freezeUntil);
            }

            if (severity >= _config.DebrisFatalThreshold)
            {
                // Normalized high-severity branch; deterministic random source preserves reproducibility.
                if (simulation.Random.NextDouble() < 0.45)
                {
                    agent.MarkDead();
                    simulation.DebrisFatalities++;
                }
                else
                {
                    agent.MarkIncapacitated();
                    simulation.DebrisIncapacitations++;
                }

                return;
            }

            if (severity >= _config.DebrisIncapacityThreshold)
            {
                agent.MarkIncapacitated();
                simulation.DebrisIncapacitations++;
                return;
            }

            if (severity >= _config.DebrisFallenThreshold)
            {
                if (agent.State is AgentState.Fallen or AgentState.Crawling or AgentState.TempIncap or AgentState.Incapacitated)
                {
                    agent.MarkIncapacitated();
                    simulation.DebrisIncapacitations++;
                }
                else
                {
                    agent.MarkFallen(simulation.FrameIndex, simulation.Random);
                }

                return;
            }

            if (severity >= _config.DebrisInjuryThreshold)
            {
                agent.MarkInjured(preserveReactionState: agent.State == AgentState.WaitingToReact);
                simulation.DebrisInjuries++;
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            if (lengthSquared <= 1e-8f || !float.IsFinite(lengthSquared))
            {
                return Vector2.Distance(point, a);
            }

            float t = Vector2.Dot(point - a, ab) / lengthSquared;
            t = Math.Clamp(t, 0f, 1f);
            Vector2 closest = a + t * ab;
            return Vector2.Distance(point, closest);
        }

        private static bool ClipAxis(
            float origin,
            float delta,
            float lower,
            float upper,
            ref float nearTime,
            ref float farTime)
        {
            if (MathF.Abs(delta) < 1e-8f)
            {
                return origin >= lower && origin <= upper;
            }

            float timeOne = (lower - origin) / delta;
            float timeTwo = (upper - origin) / delta;
            if (timeOne > timeTwo)
            {
                (timeOne, timeTwo) = (timeTwo, timeOne);
            }

            nearTime = Math.Max(nearTime, timeOne);
            farTime = Math.Min(farTime, timeTwo);
            return nearTime <= farTime;
        }

        private int RandomFrameRange(Random random, int minInclusive, int maxExclusive)
            => _config.ReferenceFramesToTicks(maxExclusive <= minInclusive
                ? minInclusive
                : minInclusive + (int)(random.NextDouble() * (maxExclusive - minInclusive)));
    }
}
