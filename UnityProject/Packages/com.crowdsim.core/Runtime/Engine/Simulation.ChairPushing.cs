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

namespace CrowdSim.Core
{
    public sealed partial class Simulation
    {
        private readonly List<Agent> _chairPushAgentScratch = new(32);
        private readonly List<FurnitureChair> _chairPushGroup = new(3);

        /// <summary>
        /// Push a reachable loose chair, or a bounded group when individual shifts are blocked.
        /// Jammed chairs remain solid and let the existing bypass/wait/replan behaviour proceed.
        /// </summary>
        internal bool TryPushLooseChair(Agent agent, CircleBlockage obstacle, Vector2 desired)
        {
            FurnitureChair? chair = obstacle.ChairOwner;
            if (chair is null || !ReferenceEquals(chair.Obstacle, obstacle) ||
                chair.State is not (ChairState.Upright or ChairState.Fallen) ||
                chair.OccupantId is not null || chair.LastPushFrame == FrameIndex ||
                (agent.Posture != SeatingPosture.Walking &&
                 !(agent.Posture == SeatingPosture.StandingUp && agent.StandUpElapsed >= agent.StandUpSeconds)) ||
                agent.State is not (AgentState.Evacuating or AgentState.Injured) ||
                agent.Intent == BehaviorIntent.AssistCasualty ||
                !VectorMath.IsFinite(desired) || desired.LengthSquared() < 1e-8f)
                return false;

            Vector2 offset = chair.Position - agent.Position;
            float distance = offset.Length();
            float reachMargin = agent.State == AgentState.Injured ? 0.45f : 0.30f;
            if (distance > obstacle.Radius + Config.Agent.Radius + reachMargin || distance < 1e-5f)
                return false;
            Vector2 away = offset / distance;
            if (Vector2.Dot(VectorMath.SafeNormalize(desired), away) < 0.10f) return false;

            // Agent velocities use metres per reference tick; the push itself uses SI seconds.
            float walkingSpeed = agent.Velocity.Length() / Configuration.AgentConfig.ReferenceStepSeconds;
            float pushSpeed = Math.Clamp(0.5f + walkingSpeed * 0.55f, 0.5f, 1.5f);
            if (agent.State == AgentState.Injured)
            {
                float mobility = Math.Clamp(agent.InjurySpeedFactor * agent.ToxicMobilityFactor, 0.15f, 1f);
                pushSpeed = Math.Min(pushSpeed, 0.20f + 0.40f * mobility);
            }
            float travel = Math.Min(0.12f, pushSpeed * Config.Agent.Dt);
            Vector2 side = VectorMath.Perpendicular(away);
            if ((agent.AgentId & 1) != 0) side = -side;
            // Forward first, then bounded diagonal/sideways shifts to open a local gap.
            Span<Vector2> directions = stackalloc Vector2[5]
            {
                away, VectorMath.SafeNormalize(away + side), VectorMath.SafeNormalize(away - side), side, -side
            };
            foreach (Vector2 direction in directions)
            {
                Vector2 destination = chair.Position + direction * travel;
                if (!ChairPushPathIsClear(chair, destination)) continue;
                chair.Position = destination;
                chair.LastPushFrame = FrameIndex; // dense crowds cannot multiply speed in one tick
                chair.Velocity = Vector2.Zero; // friction stops the chair when contact ends
                SetFurnitureObstacle(chair, chair.State);
                agent.ClearRoute();
                agent.LastChairPushFrame = FrameIndex;
                agent.LastChairPushDirection = VectorMath.SafeNormalize(desired);
                agent.Velocity *= 0.90f;
                return true;
            }
            // Prefer shifting a single chair; only then try moving a small contacting group.
            foreach (Vector2 direction in directions)
            {
                if (!TryTranslateChairGroup(chair, direction * travel)) continue;
                agent.ClearRoute();
                agent.LastChairPushFrame = FrameIndex;
                agent.LastChairPushDirection = VectorMath.SafeNormalize(desired);
                agent.Velocity *= 0.90f;
                return true;
            }
            return false;
        }

        /// <summary>Discover at most three loose chairs and validate their motion before atomic commit.</summary>
        internal bool TryTranslateChairGroup(FurnitureChair first, Vector2 displacement)
        {
            if (!VectorMath.IsFinite(displacement) || displacement.LengthSquared() < 1e-8f ||
                displacement.LengthSquared() > 0.1201f * 0.1201f || !CanJoinChairGroup(first)) return false;
            _chairPushGroup.Clear();
            _chairPushGroup.Add(first);
            int steps = Math.Max(1, (int)MathF.Ceiling(displacement.Length() / 0.025f));
            for (int index = 0; index < _chairPushGroup.Count; index++)
            {
                FurnitureChair member = _chairPushGroup[index];
                Vector2 previous = member.Position;
                for (int step = 1; step <= steps; step++)
                {
                    Vector2 point = member.Position + displacement * (step / (float)steps);
                    foreach (CircleBlockage obstacle in Environment.CircularBlockages)
                    {
                        if (ReferenceEquals(obstacle, member.Obstacle) ||
                            (obstacle.ChairOwner is FurnitureChair grouped && _chairPushGroup.Contains(grouped))) continue;
                        if (SeparatingOrClear(previous, point, obstacle.Center, member.Obstacle!.Radius + obstacle.Radius)) continue;
                        FurnitureChair? next = obstacle.ChairOwner;
                        if (next is null || !CanJoinChairGroup(next) || _chairPushGroup.Count == 3) return false;
                        _chairPushGroup.Add(next);
                    }
                    previous = point;
                }
            }
            if (_chairPushGroup.Count < 2) return false;
            // Added resistance: two chairs move at 2/3 speed, three at 1/2 speed.
            Vector2 reduced = displacement / (1f + 0.5f * (_chairPushGroup.Count - 1));
            foreach (FurnitureChair member in _chairPushGroup)
                if (!ChairPushPathIsClear(member, member.Position + reduced, _chairPushGroup)) return false;
            foreach (FurnitureChair member in _chairPushGroup)
            {
                member.Position += reduced;
                member.LastPushFrame = FrameIndex;
                member.Velocity = Vector2.Zero;
            }
            foreach (FurnitureChair member in _chairPushGroup) SetFurnitureObstacle(member, member.State);
            return true;
        }

        private bool CanJoinChairGroup(FurnitureChair chair) => chair.Obstacle is not null &&
            (chair.State is ChairState.Upright or ChairState.Fallen) && chair.OccupantId is null &&
            chair.LastPushFrame != FrameIndex;

        private bool ChairPushPathIsClear(FurnitureChair chair, Vector2 destination,
            List<FurnitureChair>? movingGroup = null)
        {
            float radius = chair.Obstacle!.Radius;
            Vector2 start = chair.Position;
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(start, destination) / 0.025f));
            // Use a dedicated scratch list: navigation/collision callers may be enumerating theirs.
            CollectAgentsNearPoint(start, radius + Config.Agent.Radius * 1.25f + 0.15f, _chairPushAgentScratch);
            Vector2 previous = start;
            for (int step = 1; step <= steps; step++)
            {
                Vector2 point = Vector2.Lerp(start, destination, step / (float)steps);
                if (point.X < Environment.BoundaryThickness + radius ||
                    point.Y < Environment.BoundaryThickness + radius ||
                    point.X > Environment.Width - Environment.BoundaryThickness - radius ||
                    point.Y > Environment.Height - Environment.BoundaryThickness - radius ||
                    Environment.Walls.Any(w => w.Contains(point, radius))) return false;
                foreach (CircleBlockage other in Environment.CircularBlockages)
                {
                    if (ReferenceEquals(other, chair.Obstacle) ||
                        (other.ChairOwner is FurnitureChair member && movingGroup?.Contains(member) == true)) continue;
                    if (!SeparatingOrClear(previous, point, other.Center, radius + other.Radius)) return false;
                }
                foreach (Agent other in _chairPushAgentScratch)
                {
                    if (!other.IsActive) continue;
                    float bodyRadius = Config.Agent.Radius * (other.IsCasualtyObstacle ? 1.25f : 1f);
                    if (!SeparatingOrClear(previous, point, other.Position, radius + bodyRadius)) return false;
                }
                previous = point;
            }
            return true;
        }

        /// <summary>Existing blast pile overlap may decrease, but a push cannot create/deepen overlap.</summary>
        private static bool SeparatingOrClear(Vector2 previous, Vector2 next, Vector2 center, float radius)
        {
            float nextDistance = Vector2.DistanceSquared(next, center);
            return nextDistance >= radius * radius ||
                nextDistance > Vector2.DistanceSquared(previous, center) + 1e-7f;
        }

        private void PushChairInMovementDirection(Agent agent)
        {
            TryPushChairTowardGoal(agent, agent.Velocity);
        }

        /// <summary>Reach for a nearby chair even while waiting with zero actual velocity.</summary>
        internal bool TryPushChairTowardGoal(Agent agent, Vector2 desired)
        {
            if (Environment.Furniture.Count == 0 || !VectorMath.IsFinite(desired) ||
                desired.LengthSquared() < 1e-8f || agent.State is not (AgentState.Evacuating or AgentState.Injured))
                return false;
            CircleBlockage? nearest = null;
            float best = float.PositiveInfinity;
            Vector2 direction = VectorMath.SafeNormalize(desired);
            foreach (CircleBlockage circle in Environment.CircularBlockages)
            {
                if (circle.ChairOwner is not FurnitureChair chair || chair.OccupantId is not null ||
                    chair.State is not (ChairState.Upright or ChairState.Fallen)) continue;
                Vector2 delta = circle.Center - agent.Position;
                float distance = delta.Length();
                float gap = distance - circle.Radius - Config.Agent.Radius;
                float reachMargin = agent.State == AgentState.Injured ? 0.45f : 0.30f;
                if (gap > reachMargin || Vector2.Dot(direction, VectorMath.SafeNormalize(delta)) < 0.10f) continue;
                if (gap < best) { best = gap; nearest = circle; }
            }
            // Mutation happens after enumeration, so replacing the proxy cannot invalidate it.
            return nearest is not null && TryPushLooseChair(agent, nearest, direction);
        }
    }
}
