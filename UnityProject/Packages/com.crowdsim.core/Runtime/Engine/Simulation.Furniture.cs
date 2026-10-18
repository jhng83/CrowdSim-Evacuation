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
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;
using CrowdSim.Core.Systems;
namespace CrowdSim.Core
{
    public sealed partial class Simulation
    {
        /// <summary>Pause locomotion while seated/rising, while hazard and panic systems keep running.</summary>
        private bool UpdateSeating(Agent agent)
        {
            if (agent.Posture == SeatingPosture.Walking || agent.Chair is not FurnitureChair chair) return false;
            // A casualty must never be revived or forced to stand by the furniture controller.
            if (agent.State is AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap or AgentState.Fallen or AgentState.Crawling or AgentState.Exited)
            {
                agent.Posture = SeatingPosture.Walking;
                chair.OccupantId = null;
                agent.Chair = null;
                return false;
            }
            agent.Velocity = Vector2.Zero;
            if (agent.Posture == SeatingPosture.Seated)
            {
                if (agent.State == AgentState.Wandering && chair.State == ChairState.Upright) return true;
                agent.Posture = SeatingPosture.StandingUp;
                float normal = 0.8f + 0.8f * (float)Random.NextDouble();
                float mobility = Math.Clamp(agent.InjurySpeedFactor * agent.ToxicMobilityFactor, 0.35f, 1f);
                agent.StandUpSeconds = Math.Clamp(normal + (1f - mobility) * 0.5f + Math.Max(0f, 1f - agent.UrgencyBias) * 0.4f + Math.Max(0f, 0.3f - agent.PanicLevel) * 0.3f - Math.Max(0f, agent.PanicLevel - 0.8f) * 1.0f, 0.6f, 2.1f);
                agent.StandUpElapsed = 0f;
            }
            agent.StandUpElapsed += Config.Agent.Dt;
            if (agent.StandUpElapsed < agent.StandUpSeconds || agent.StunnedUntilFrame > FrameIndex) return true;

            // Reserve the fallen footprint too, so toppling cannot overlap the departing person.
            float seatMargin = Config.Agent.Radius + 0.01f + Math.Max(0f, 0.40f - (chair.Obstacle?.Radius ?? 0.40f));
            // Once the seat has moved away or the person clears it, release normal navigation.
            if (chair.Obstacle is null || !chair.Obstacle.Contains(agent.Position, seatMargin))
            {
                FinishSeatDeparture(agent, chair);
                return false;
            }
            if (FrameIndex < agent.NextSeatDepartureProbeFrame) return true;
            float preferred = chair.StartHeading + MathF.PI;
            float travel = 0.7f * Config.Agent.Dt;
            for (int i = 0; i < 16; i++)
            {
                int offset = i == 0 ? 0 : (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
                float angle = preferred + offset * MathF.PI / 8f;
                Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 next = agent.Position + direction * travel;
                if (!SeatDepartureStepIsClear(agent, chair, next)) continue;
                agent.Position = next;
                if (!chair.Obstacle.Contains(next, seatMargin))
                {
                    FinishSeatDeparture(agent, chair);
                    return false;
                }
                return true;
            }
            // A nearby loose chair can be shifted while standing; an occupied chair cannot.
            foreach (FurnitureSet set in Environment.Furniture)
                foreach (FurnitureChair other in set.Chairs)
                {
                    if (ReferenceEquals(other, chair) || other.Obstacle is null) continue;
                    if (TryPushLooseChair(agent, other.Obstacle, other.Position - agent.Position)) return true;
                }
            agent.NextSeatDepartureProbeFrame = FrameIndex + Math.Max(1,
                (int)MathF.Ceiling((0.20f + (agent.AgentId % 3) * 0.05f) / Config.Agent.Dt));
            return true;
        }

        private void FinishSeatDeparture(Agent agent, FurnitureChair chair)
        {
            agent.SeatDepartureTarget = null;
            agent.Posture = SeatingPosture.Walking;
            agent.Chair = null;
            chair.OccupantId = null;
            // Never enlarge a toppling footprint into the person who just left it.
            if (chair.State == ChairState.Upright &&
                Vector2.Distance(agent.Position, chair.Position) > 0.40f + Config.Agent.Radius &&
                Random.NextDouble() < 0.08 + 0.32 * agent.PanicLevel)
                SetFurnitureObstacle(chair, ChairState.Fallen);
        }

        private bool SeatDepartureStepIsClear(Agent agent, FurnitureChair chair, Vector2 next)
        {
            float radius = Config.Agent.Radius;
            CollectAgentsNearPoint(agent.Position, radius * 2.25f + 0.2f, _collisionAgentScratch, agent.AgentId);
            Vector2 previous = agent.Position;
            for (int i = 1; i <= 4; i++)
            {
                Vector2 point = Vector2.Lerp(agent.Position, next, i / 4f);
                if (point.X < Environment.BoundaryThickness + radius || point.Y < Environment.BoundaryThickness + radius ||
                    point.X > Environment.Width - Environment.BoundaryThickness - radius ||
                    point.Y > Environment.Height - Environment.BoundaryThickness - radius ||
                    Environment.Walls.Any(w => w.Contains(point, radius))) return false;
                foreach (CircleBlockage circle in Environment.CircularBlockages)
                    if (!ReferenceEquals(circle, chair.Obstacle) &&
                        !SeparatingOrClear(previous, point, circle.Center, radius + circle.Radius + 0.005f)) return false;
                foreach (Agent other in _collisionAgentScratch)
                    if (other.IsActive && !SeparatingOrClear(previous, point, other.Position,
                        radius * (other.IsCasualtyObstacle ? 2.25f : 2f) + 0.005f)) return false;
                previous = point;
            }
            // Make outward progress rather than orbiting forever inside the attached seat proxy.
            return Vector2.DistanceSquared(next, chair.Position) >
                Vector2.DistanceSquared(agent.Position, chair.Position) + 1e-6f;
        }

        private void SetFurnitureObstacle(FurnitureBody body, ChairState state)
        {
            if (body is RectangularFurnitureBody rectangle)
            {
                SetRectangularFurnitureObstacle(rectangle, state);
                return;
            }
            float radius = body is FurnitureChair && state != ChairState.Upright ? 0.40f : body.Radius;
            CircleBlockage? next = state == ChairState.Destroyed ? null :
                new CircleBlockage(body.Position, radius) { IsFurniture = true, ChairOwner = body as FurnitureChair };
            body.State = state;
            // Stationary bodies do not invalidate paths or churn the obstacle collection.
            if (Equals(body.Obstacle, next)) return;
            if (body.Obstacle is not null) Environment.CircularBlockages.Remove(body.Obstacle);
            body.Obstacle = next;
            if (next is not null) Environment.CircularBlockages.Add(next);
            Environment.InvalidateFurnitureNavigation();
        }

        /// <summary>Apply the shock front to every body before integrating any motion.</summary>
        private void UpdateFurniture()
        {
            _blastFurnitureBodies.Clear();
            foreach (FurnitureSet set in Environment.Furniture)
            {
                if (set.Table is not null) _blastFurnitureBodies.Add(set.Table);
                _blastFurnitureBodies.AddRange(set.Chairs);
            }
            foreach (FurnitureBody body in _blastFurnitureBodies) ApplyFurnitureBlast(body);
            _blastStaticCircles.Clear();
            foreach (CircleBlockage circle in Environment.CircularBlockages)
                if (!circle.IsFurniture) _blastStaticCircles.Add(circle);
            _activeBlastFurniture.Clear();
            float maximumSpeed = 0f;
            foreach (FurnitureBody body in _blastFurnitureBodies)
            {
                if (body.State == ChairState.Destroyed) continue;
                maximumSpeed = Math.Max(maximumSpeed, body.Velocity.Length() + MathF.Abs(body.VerticalSpeed));
                if (body.State == ChairState.Airborne || body.SupportBody is not null ||
                    body.Velocity.LengthSquared() >= 0.0004f) ActivateFurniture(body);
                else body.Velocity = Vector2.Zero;
            }
            if (_activeBlastFurniture.Count == 0) return;
            RebuildFurnitureImpactGrid();
            // Allow a factor of two for speed gained in a light/heavy-body collision.
            int steps = Math.Max(1, (int)MathF.Ceiling((2f * maximumSpeed + 9.81f * Config.Agent.Dt) * Config.Agent.Dt / 0.04f));
            float dt = Config.Agent.Dt / steps;
            for (int step = 0; step < steps; step++)
                for (int index = 0; index < _activeBlastFurniture.Count; index++)
                    IntegrateFurniture(_activeBlastFurniture[index], dt);
            foreach (FurnitureBody body in _activeBlastFurniture)
                if (body.State != ChairState.Destroyed) SetFurnitureObstacle(body, body.State);
        }

        private void ApplyFurnitureBlast(FurnitureBody body)
        {
            if (Environment.Explosion is not Explosion blast || body.BlastApplied) return;
            float distance = Vector2.Distance(body.Position, blast.Position);
            float waveRadius = Config.Agent.ShockwaveSpeedMetersPerFrame *
                (FrameIndex - blast.TriggeredFrame) * Config.Agent.StepScale;
            if (distance > waveRadius) return;
            body.BlastApplied = true;
            if (ExplosionSystem.BlastPathIsBlocked(blast.Position, body.Position, Environment.Walls)) return;
            if (distance <= Config.Agent.HeatIncapacityRadius)
            {
                SetFurnitureObstacle(body, ChairState.Destroyed);
            }
            else if (distance < Config.Agent.HeatRadius)
            {
                float strength = Math.Clamp((Config.Agent.HeatRadius - distance) /
                    Math.Max(0.1f, Config.Agent.HeatRadius - Config.Agent.HeatIncapacityRadius), 0f, 1f);
                // Heavier tables receive less speed than chairs from the same stylised impulse.
                float massFactor = body is FurnitureChair ? 1f : 0.65f;
                body.Velocity = VectorMath.SafeNormalize(body.Position - blast.Position, Vector2.UnitX) *
                    (6f * strength * massFactor);
                body.VerticalSpeed = (4f * strength) * MathF.Sqrt(massFactor);
                body.SupportBody = null;
                body.FlightSeconds = 0f;
                SetFurnitureObstacle(body, ChairState.Airborne);
            }
        }

        /// <summary>Ballistic flight with drag, swept wall collision and a persistent ground footprint.</summary>
        private void IntegrateFurniture(FurnitureBody body, float dt)
        {
            if (body.State == ChairState.Destroyed) return;
            if (body.SupportBody is FurnitureBody support)
            {
                if (support.State == ChairState.Destroyed ||
                    !FurnitureFootprintsOverlap(body, body.Position, support, support.Position))
                {
                    body.SupportBody = null;
                    body.State = ChairState.Airborne;
                    body.FlightSeconds = 0f;
                }
                else body.Height = support.Height + FurnitureBodyHeight(support) + 0.002f;
            }
            bool airborne = body.State == ChairState.Airborne;
            if (!airborne && body.Velocity.LengthSquared() < 0.0004f)
            {
                body.Velocity = Vector2.Zero;
                return;
            }
            Vector2 oldPosition = body.Position;
            float oldHeight = body.Height;
            float nextHeight = oldHeight;
            if (airborne)
            {
                body.FlightSeconds += dt;
                nextHeight = Math.Max(0f, oldHeight + body.VerticalSpeed * dt - 0.5f * 9.81f * dt * dt);
                body.VerticalSpeed -= 9.81f * dt;
            }
            Vector2 next = oldPosition + body.Velocity * dt;
            if (FurnitureHitsStaticGeometry(body, next))
            {
                next = oldPosition;
                body.Velocity = Vector2.Zero;
            }
            ResolveFurnitureImpact(body, oldPosition, oldHeight, ref next, ref nextHeight);
            body.Height = nextHeight;
            float radius = FurnitureCollisionRadius(body);
            if (!body.ImpactApplied && body.Height < 1.6f && body.Velocity.LengthSquared() > 0.01f)
            {
                CollectAgentsNearPoint(next, radius + Config.Agent.Radius, _collisionAgentScratch);
                Agent? hit = null;
                float closestSquared = float.PositiveInfinity;
                foreach (Agent candidate in _collisionAgentScratch)
                {
                    if (!candidate.IsActive || candidate.State == AgentState.Dead ||
                        (body is FurnitureChair chair && candidate.AgentId == chair.OriginalOccupantId)) continue;
                    if (body is RectangularFurnitureBody &&
                        !FurnitureOverlapsDisc(body, next, candidate.Position, Config.Agent.Radius)) continue;
                    float squared = Vector2.DistanceSquared(candidate.Position, next);
                    if (squared < closestSquared || (squared == closestSquared &&
                        (hit is null || candidate.AgentId < hit.AgentId)))
                    {
                        closestSquared = squared;
                        hit = candidate;
                    }
                }
                if (hit is not null)
                {
                    float speed = body.Velocity.Length();
                    ExplosionSystem.ApplyDebrisImpact(this, hit, new Debris(next,
                        body.Velocity * Configuration.AgentConfig.ReferenceStepSeconds, 1, radius,
                        Math.Clamp(speed * speed / 50f, 0.05f, 0.85f)));
                    body.ImpactApplied = true;
                    body.Velocity *= 0.65f;
                }
            }
            body.Position = next;
            UpdateFurnitureImpactCell(body, oldPosition);
            // A translating rectangle retains its authored axis alignment: visible geometry
            // and collision shape must never disagree during flight or after landing.
            if (airborne && body is not RectangularFurnitureBody)
                body.Heading += dt * (body is FurnitureChair ? 4f : 2f);
            body.Velocity *= MathF.Exp(-(body.State == ChairState.Airborne ? 0.9f : 4f) * dt);
            if (body.State == ChairState.Airborne && body.Height <= 0f)
            {
                body.Height = 0f;
                body.VerticalSpeed = 0f;
                body.State = ChairState.Fallen;
                body.Velocity *= 0.55f; // landing dissipates energy; remaining momentum slides
            }
        }
    }
}
