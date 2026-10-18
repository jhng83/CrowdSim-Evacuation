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
    /// <summary>Adaptive open-space steering groups; individual physical bodies are never merged.</summary>
    public sealed class HybridMovementSystem
    {
        private readonly List<Agent> _nearby = new(32);
        private readonly Dictionary<int, List<Agent>> _groups = new();
        private readonly Dictionary<(int X, int Y), List<Agent>> _buckets = new();
        private readonly Dictionary<int, Vector2> _directions = new();
        private readonly Dictionary<int, float> _directionCosts = new();
        private int _regroupFrame = -1;
        private object? _population;
        public SharedExitFields Fields { get; } = new();
        public int OpenSpaceAgents { get; private set; }
        public int DetailedAgents { get; private set; }
        public int GroupedAgents { get; private set; }
        public int GroupCount => _groups.Count;

        public void BeginFrame(Simulation sim)
        {
            OpenSpaceAgents = DetailedAgents = GroupedAgents = 0;
            _directions.Clear();
            _directionCosts.Clear();
            if (!ReferenceEquals(_population, sim.Agents) || !sim.Config.HybridEnabled)
            {
                _population = sim.Agents;
                _groups.Clear();
                _regroupFrame = -1;
                foreach (Agent agent in sim.Agents) agent.HybridGroupLeaderId = -1;
            }
            if (!sim.Config.HybridEnabled) return;
            if (sim.FrameIndex < _regroupFrame) return;
            _regroupFrame = sim.FrameIndex + Math.Max(1, (int)MathF.Ceiling(0.6f / sim.Config.Agent.Dt));
            _groups.Clear();
            foreach (var list in _buckets.Values) list.Clear();
            foreach (Agent agent in sim.Agents)
            {
                agent.HybridGroupLeaderId = -1;
                if (!Compatible(agent) || sim.FrameIndex < agent.HybridRegroupAfterFrame) continue;
                var cell = ((int)MathF.Floor(agent.Position.X / 3f), (int)MathF.Floor(agent.Position.Y / 3f));
                if (!_buckets.TryGetValue(cell, out var list)) _buckets[cell] = list = new List<Agent>();
                list.Add(agent);
            }
            foreach (var list in _buckets.Values)
                foreach (Agent leader in list)
                {
                    if (leader.HybridGroupLeaderId >= 0) continue;
                    List<Agent> members = new() { leader };
                    foreach (Agent other in list)
                    {
                        if (members.Count == 5) break;
                        if (ReferenceEquals(leader, other) || other.HybridGroupLeaderId >= 0 ||
                            !ReferenceEquals(leader.ExitTarget, other.ExitTarget) ||
                            MathF.Abs(leader.CurrentDesiredSpeed - other.CurrentDesiredSpeed) > 0.25f * leader.BaseSpeed ||
                            Vector2.DistanceSquared(leader.Position, other.Position) > 4f) continue;
                        members.Add(other);
                    }
                    if (members.Count < 2) continue;
                    foreach (Agent member in members) member.HybridGroupLeaderId = leader.AgentId;
                    _groups[leader.AgentId] = members;
                }
        }

        private static bool Compatible(Agent a) => a.State == AgentState.Evacuating &&
            a.Posture == SeatingPosture.Walking && a.ExitTarget is not null &&
            a.LocalNavigationMode == LocalNavigationMode.Free &&
            a.NavigationMemory.DetourGoal is null && !a.NavigationMemory.RecoveryRequested &&
            a.Intent is BehaviorIntent.Evacuate or BehaviorIntent.FollowCrowd;

        private bool IsOpen(Simulation sim, Agent agent)
        {
            if (!Compatible(agent) || agent.TimeStuck > 1f || agent.CurrentDesiredSpeed <= 0f ||
                Vector2.Distance(agent.Position, agent.ExitTarget!.Position) < 4f) return false;
            float lookAhead = Math.Max(1.1f, agent.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds * 0.8f);
            foreach (Wall wall in sim.Environment.Walls)
                if (Vector2.DistanceSquared(wall.ClosestPoint(agent.Position), agent.Position) < lookAhead * lookAhead) return false;
            foreach (CircleBlockage circle in sim.Environment.CircularBlockages)
                if (circle.Contains(agent.Position, lookAhead)) return false;
            sim.CollectNearbyAgents(agent, 2f, _nearby);
            int count = 0;
            foreach (Agent other in _nearby)
            {
                if (!other.IsActive) continue;
                count++;
                if (other.IsCasualtyObstacle || Vector2.DistanceSquared(agent.Position, other.Position) < 0.81f) return false;
            }
            return count < 6;
        }

        private void Split(Simulation sim, Agent agent)
        {
            int id = agent.HybridGroupLeaderId;
            if (id >= 0 && _groups.TryGetValue(id, out var members))
            {
                foreach (Agent member in members)
                {
                    member.HybridGroupLeaderId = -1;
                    member.HybridRegroupAfterFrame = sim.FrameIndex + Math.Max(1, (int)MathF.Ceiling(1f / sim.Config.Agent.Dt));
                }
                _groups.Remove(id);
                _directions.Remove(id);
                _directionCosts.Remove(id);
            }
            agent.HybridGroupLeaderId = -1;
        }

        public bool TryAdvance(Simulation sim, Agent agent)
        {
            if (!sim.Config.HybridEnabled) return false;
            if (!IsOpen(sim, agent)) { Split(sim, agent); DetailedAgents++; return false; }
            if (agent.HybridGroupLeaderId >= 0 && _groups.TryGetValue(agent.HybridGroupLeaderId, out var group))
                foreach (Agent member in group)
                    if (!Compatible(member) || !ReferenceEquals(member.ExitTarget, agent.ExitTarget) || !IsOpen(sim, member))
                    { Split(sim, agent); break; }
            Vector2 direction;
            float guidanceCost = float.PositiveInfinity;
            int groupId = agent.HybridGroupLeaderId;
            if (groupId >= 0 && _directions.TryGetValue(groupId, out direction))
            {
                guidanceCost = _directionCosts[groupId];
            }
            else
            {
                Vector2 origin = agent.Position;
                if (groupId >= 0 && _groups.TryGetValue(groupId, out var members))
                {
                    origin = Vector2.Zero;
                    foreach (Agent member in members) origin += member.Position;
                    origin /= members.Count;
                }
                Vector2 goal = agent.ExitTarget!.Position;
                if (sim.HasTraversablePath(origin, goal, sim.Config.Agent.Radius * 1.08f))
                {
                    direction = VectorMath.SafeNormalize(goal - origin);
                    guidanceCost = Vector2.Distance(origin, goal);
                }
                else if (!Fields.TryDirection(sim, origin, goal, out direction, out guidanceCost))
                { Split(sim, agent); DetailedAgents++; return false; }
                if (groupId >= 0)
                {
                    _directions[groupId] = direction;
                    _directionCosts[groupId] = guidanceCost;
                }
            }
            // The progress supervisor must recognise a valid route that initially moves away
            // from the exit even when the optional open-space fast path owns locomotion.
            agent.NavigationMemory.RemainingFieldCost = guidanceCost;
            agent.NavigationMemory.FieldGeometryVersion = Fields.GeometryRefreshes;
            agent.NavigationMemory.FieldCostFrame = sim.FrameIndex;
            // Shared geometric guidance is not permission to ignore each member's local plume.
            direction = sim.EvacuationSystem.ApplyNaturalToxicAvoidance(sim, agent, direction, exitCapture: false);
            direction = sim.EvacuationSystem.ApplyNaturalMovingFurnitureAvoidance(sim, agent, direction);
            // Small smooth variations preserve individual movement; no rigid group attraction.
            float variation = 0.04f * MathF.Sin(sim.FrameIndex * sim.Config.Agent.Dt * 0.9f + agent.AgentId * 2.399963f);
            direction = VectorMath.SafeNormalize(direction + VectorMath.Perpendicular(direction) * variation);
            if (!sim.HasTraversablePath(agent.Position, agent.Position + direction * 0.9f, sim.Config.Agent.Radius * 1.08f))
            { Split(sim, agent); DetailedAgents++; return false; }
            float blend = 1f - MathF.Exp(-sim.Config.Agent.Dt / 0.45f);
            Vector2 desired = direction * agent.CurrentDesiredSpeed;
            Vector2 change = (desired - agent.Velocity) * blend;
            agent.Velocity += VectorMath.ClampMagnitude(change,
                sim.Config.Agent.MaxAccelerationMetersPerSecondSquared * sim.Config.Agent.Dt * AgentConfig.ReferenceStepSeconds);
            agent.Velocity = VectorMath.ClampMagnitude(agent.Velocity, agent.CurrentDesiredSpeed);
            agent.LastDesiredDirection = direction;
            agent.LastSfmForce = Vector2.Zero;
            sim.MoveAgent(agent); // keeps individual hard collision resolution
            OpenSpaceAgents++;
            if (groupId >= 0) GroupedAgents++;
            return true;
        }
    }
}
