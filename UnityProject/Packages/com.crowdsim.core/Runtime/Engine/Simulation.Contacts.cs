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
        private readonly List<Agent> _contactScratch = new(32);
        private readonly List<Wall> _contactWallScratch = new(8);
        private readonly HashSet<Wall> _contactWallSeen = new();

        /// <summary>
        /// One local, unordered-pair projection pass bounds body penetration at the coarse
        /// fixed step. This is a numerical contact guard, not another steering controller.
        /// Dense multi-body contacts can retain small residual overlap after one pass;
        /// a global iterative solver is deliberately avoided to bound per-step cost.
        /// </summary>
        private void ResolveCrowdContacts()
        {
            float diameter = 2f * Config.Agent.Radius;
            float diameterSquared = diameter * diameter;
            foreach (Agent agent in Agents)
            {
                if (!agent.IsActive)
                {
                    continue;
                }
                CollectNearbyAgents(agent, diameter, _contactScratch);
                foreach (Agent other in _contactScratch)
                {
                    if (other.AgentId <= agent.AgentId || !other.IsActive)
                    {
                        continue;
                    }
                    float ownWeight = CanDisplace(agent) ? 1f : 0f;
                    float otherWeight = CanDisplace(other) ? 1f : 0f;
                    float totalWeight = ownWeight + otherWeight;
                    if (totalWeight == 0f)
                    {
                        continue;
                    }
                    Vector2 offset = agent.Position - other.Position;
                    float squared = offset.LengthSquared();
                    if (!float.IsFinite(squared) || squared >= diameterSquared)
                    {
                        continue;
                    }
                    float distance = MathF.Sqrt(squared);
                    Vector2 normal = distance > VectorMath.Epsilon ? offset / distance : -Vector2.UnitX;
                    Vector2 correction = normal * ((diameter - distance) / totalWeight);
                    if (ownWeight > 0f)
                    {
                        ApplyContactCorrection(agent, correction);
                    }
                    if (otherWeight > 0f)
                    {
                        ApplyContactCorrection(other, -correction);
                    }
                    // Remove closing normal velocity only. Tangential locomotion is retained.
                    float closing = Vector2.Dot(agent.Velocity - other.Velocity, normal);
                    if (closing < 0f)
                    {
                        Vector2 impulse = normal * (closing / totalWeight);
                        agent.Velocity -= ownWeight * impulse;
                        other.Velocity += otherWeight * impulse;
                    }
                }
            }
        }

        private static bool CanDisplace(Agent agent)
            => agent.Posture == SeatingPosture.Walking && (agent.State is AgentState.Wandering or AgentState.WaitingToReact or
                AgentState.Evacuating or AgentState.Injured or AgentState.Crawling);

        private void ApplyContactCorrection(Agent agent, Vector2 correction)
        {
            Vector2 candidate = VectorMath.ClampToBounds(agent.Position + correction,
                Config.World.WidthMeters, Config.World.LengthMeters);
            float radius = Config.Agent.Radius;
            CollectNearbyWalls(candidate, radius, _contactWallScratch, _contactWallSeen);
            foreach (Wall wall in _contactWallScratch)
            {
                if (wall.OrientedFootprint is OrientedRectangle rotated
                    ? rotated.CircleSeparation(candidate, radius) != Vector2.Zero
                    : candidate.X > wall.X1 - radius && candidate.X < wall.X2 + radius &&
                        candidate.Y > wall.Y1 - radius && candidate.Y < wall.Y2 + radius)
                {
                    // Do not resolve a crowd overlap by putting the pedestrian into a wall.
                    return;
                }
            }
            foreach (CircleBlockage circle in Environment.CircularBlockages)
            {
                float clearance = circle.Radius + radius;
                if (Vector2.DistanceSquared(candidate, circle.Center) < clearance * clearance)
                {
                    return;
                }
            }
            agent.Position = candidate;
            RefreshAgentCell(agent);
        }
    }
}
