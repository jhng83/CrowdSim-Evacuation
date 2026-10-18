#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Diagnostics;
using System.Numerics;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Systems
{
    public sealed partial class EvacuationSystem
    {
        private readonly Queue<Agent> _naturalRecoveryQueue = new();
        private readonly Queue<Agent> _naturalRouteQueue = new();
        private readonly List<Agent> _naturalPeople = new(96);
        private readonly List<CircleBlockage> _naturalCircles = new(64);
        private readonly List<FurnitureBody> _naturalMovingFurniture = new(32);
        private object? _naturalPopulation;
        private int _naturalFrame = -1;

        public int NaturalPendingRecoveries => _naturalRecoveryQueue.Count;
        public int NaturalPendingRoutes => _naturalRouteQueue.Count;
        public int LastNaturalRecoveryProbes { get; private set; }
        public int LastNaturalRoutePlans { get; private set; }

        internal bool UsesNaturalNavigation(Simulation simulation) =>
            _config.NaturalNavigationEnabled &&
            simulation.ScenarioType is ScenarioType.Explosion or ScenarioType.ToxicFume;

        private static bool IsNaturalMover(Agent agent) =>
            agent.State is (AgentState.Evacuating or AgentState.Injured) &&
            agent.Posture == SeatingPosture.Walking &&
            agent.ExitTarget is not null &&
            agent.Intent is BehaviorIntent.Evacuate or BehaviorIntent.FollowCrowd;

        /// <summary>
        /// Deterministic FIFO work queues, serviced once per simulation step. Expensive retries
        /// cannot run simultaneously for all 600 people, and high agent IDs cannot starve behind
        /// the same low IDs. Ordinary locomotion and physical contact remain per-step work.
        /// </summary>
        internal void BeginNaturalNavigationFrame(Simulation simulation)
        {
            if (!ReferenceEquals(_naturalPopulation, simulation.Agents))
            {
                _naturalPopulation = simulation.Agents;
                _naturalRecoveryQueue.Clear();
                _naturalRouteQueue.Clear();
                _naturalFrame = -1;
            }
            if (_naturalFrame == simulation.FrameIndex) return;
            _naturalFrame = simulation.FrameIndex;
            LastNaturalRecoveryProbes = 0;
            LastNaturalRoutePlans = 0;
            if (!UsesNaturalNavigation(simulation)) return;

            int recoveries = _naturalRecoveryQueue.Count;
            while (recoveries-- > 0 && LastNaturalRecoveryProbes < Math.Max(1, _config.NaturalRecoveryProbesPerStep))
            {
                Agent agent = _naturalRecoveryQueue.Dequeue();
                agent.NavigationMemory.RecoveryRequested = false;
                if (!IsNaturalMover(agent)) continue;
                if (agent.NavigationMemory.NoProgressSeconds < 0.4f &&
                    !agent.NavigationMemory.LoopDetected &&
                    Vector2.DistanceSquared(agent.Position, agent.NavigationMemory.RecoveryRequestPosition) > 0.64f)
                    continue; // useful motion resumed while the request was waiting in the FIFO
                LastNaturalRecoveryProbes++;
                ChooseNaturalRecovery(simulation, agent);
            }

            int routes = _naturalRouteQueue.Count;
            while (routes-- > 0 && LastNaturalRoutePlans < Math.Max(1, _config.NaturalRoutePlansPerStep))
            {
                Agent agent = _naturalRouteQueue.Dequeue();
                NaturalNavigationMemory memory = agent.NavigationMemory;
                memory.RouteQueued = false;
                if (agent.ExitTarget is null || !agent.IsActive ||
                    agent.State is not (AgentState.Evacuating or AgentState.Injured or AgentState.Crawling) ||
                    memory.RequestedRouteKey is not string key) continue;
                if (!string.Equals(key, $"exit:{agent.ExitTarget.Name}", StringComparison.Ordinal)) continue;
                if (memory.DetourGoal is not null) continue; // plan from where the detour finishes, not its old origin

                LastNaturalRoutePlans++;
                memory.RoutePlans++;
                simulation.LastRoutePlanRequests++;
                long started = Stopwatch.GetTimestamp();
                IReadOnlyList<Vector2> route = _routePlanner.Plan(simulation, agent.Position, memory.RequestedRouteGoal);
                simulation.LastRoutePlanningMilliseconds += ReferenceClock.ElapsedMilliseconds(started);
                if (route.Count > 0)
                {
                    agent.SetRoute(route, key, int.MaxValue);
                    memory.GuidancePoint = null;
                    memory.NextRouteCheckFrame = simulation.FrameIndex;
                    agent.FailedRouteKey = null;
                    // Do not reset navigation progress/failure memory here.
                }
                else
                {
                    agent.ClearRoute();
                    memory.NextRouteRequestFrame = simulation.FrameIndex + SecondsToFrames(0.8f + (agent.AgentId % 5) * 0.09f);
                    agent.FailedRouteKey = key;
                    agent.FailedRouteRetryFrame = memory.NextRouteRequestFrame;
                    RequestNaturalRecovery(simulation, agent, force: false);
                }
            }
        }

        private void ObserveNaturalNavigation(Simulation simulation, Agent agent)
        {
            if (!UsesNaturalNavigation(simulation) || !IsNaturalMover(agent)) return;
            BeginNaturalNavigationFrame(simulation);
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (simulation.FrameIndex < memory.NextObservationFrame) return;
            memory.NextObservationFrame = simulation.FrameIndex + SecondsToFrames(
                _config.NaturalDecisionSeconds + (agent.AgentId % 3) * 0.035f);
            Vector2 target = memory.DetourGoal ?? agent.CurrentRouteWaypoint ?? agent.ExitTarget!.Position;
            bool freshFieldCost = memory.DetourGoal is null && agent.CurrentRouteWaypoint is null &&
                (simulation.FrameIndex - memory.FieldCostFrame) * _config.Dt <= 0.9f;
            memory.Observe(agent.Position, target, simulation.FrameIndex, _config.Dt,
                freshFieldCost ? memory.RemainingFieldCost : float.PositiveInfinity, memory.FieldGeometryVersion);
            if (memory.DetourGoal is not null) return;

            float threshold = _config.NaturalNoProgressSeconds * Math.Clamp(agent.PatienceBias, 0.8f, 1.25f);
            // A slow but progressing doorway queue is not a trap. Persistent failure still
            // eventually permits reconsideration, without forced shoving or teleportation.
            bool nearDoor = Vector2.DistanceSquared(agent.Position, agent.ExitTarget!.Position) < 2.25f;
            if (nearDoor) threshold = Math.Max(threshold, _config.NaturalExitReconsiderSeconds);
            bool actionableLoop = memory.LoopDetected && (!nearDoor || memory.NoProgressSeconds >= threshold);
            if (actionableLoop || memory.NoProgressSeconds >= threshold)
            {
                RequestNaturalRecovery(simulation, agent, force: actionableLoop);
            }
            else if (memory.NoProgressSeconds < 0.4f &&
                     simulation.FrameIndex >= memory.NextRecoveryFrame && !memory.RecoveryRequested)
            {
                memory.Stage = NavigationRecoveryStage.FollowingRoute;
                if ((simulation.FrameIndex - memory.LastRecoveryFrame) * _config.Dt >= 5f)
                {
                    memory.RecoveryAttempts = 0; // new blockage episode, not permanent impatience
                    memory.RecoveryEpisodeStartedFrame = -1;
                }
            }
        }

        internal void RequestNaturalRecovery(Simulation simulation, Agent agent, bool force)
        {
            if (!UsesNaturalNavigation(simulation) || !IsNaturalMover(agent)) return;
            BeginNaturalNavigationFrame(simulation);
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (memory.RecoveryRequested || (!force && simulation.FrameIndex < memory.NextRecoveryFrame)) return;
            memory.RecoveryRequested = true;
            memory.RecoveryRequestPosition = agent.Position;
            memory.Stage = NavigationRecoveryStage.AssessingBlockage;
            _naturalRecoveryQueue.Enqueue(agent);
        }

        private void RequestNaturalRoute(Simulation simulation, Agent agent, Vector2 goal, string key)
        {
            NaturalNavigationMemory memory = agent.NavigationMemory;
            memory.RequestedRouteGoal = goal;
            memory.RequestedRouteKey = key;
            if (memory.RouteQueued || simulation.FrameIndex < memory.NextRouteRequestFrame) return;
            memory.RouteQueued = true;
            _naturalRouteQueue.Enqueue(agent);
        }

        /// <summary>
        /// Keep valid routes, check nearby changed geometry at a staggered decision rate, and
        /// use shared exit guidance rather than one new A* search per person every two seconds.
        /// Full A* is a queued fallback/recovery, not the per-frame motor controller.
        /// </summary>
        private Vector2 ResolveNaturalRouteDirection(Simulation simulation, Agent agent, Vector2 goal, string key)
        {
            BeginNaturalNavigationFrame(simulation);
            NaturalNavigationMemory memory = agent.NavigationMemory;
            float clearance = NavigationClearance();
            if (Vector2.DistanceSquared(agent.Position, goal) < 0.000001f) return Vector2.Zero;

            if (string.Equals(agent.RouteExitName, key, StringComparison.Ordinal) &&
                agent.CurrentRouteWaypoint is not null)
            {
                float reach = Math.Max(0.10f, _config.RouteWaypointReachFactor * _config.Radius);
                while (agent.CurrentRouteWaypoint is Vector2 reached &&
                       Vector2.DistanceSquared(agent.Position, reached) <= reach * reach)
                    agent.AdvanceRouteWaypoint();

                if (agent.CurrentRouteWaypoint is Vector2 waypoint)
                {
                    if (simulation.FrameIndex < memory.NextRouteCheckFrame)
                        return VectorMath.SafeNormalize(waypoint - agent.Position);
                    memory.NextRouteCheckFrame = simulation.FrameIndex + SecondsToFrames(_config.NaturalDecisionSeconds);
                    // An unseen distant change does not immediately cancel the whole intention.
                    float range = _config.NaturalPerceptionRadiusMeters * Math.Clamp(agent.VisibilityFactor, 0.35f, 1f);
                    Vector2 toWaypoint = waypoint - agent.Position;
                    Vector2 probe = agent.Position + VectorMath.SafeNormalize(toWaypoint) * Math.Min(range, toWaypoint.Length());
                    if (simulation.HasTraversablePath(agent.Position, probe, clearance))
                    {
                        if (agent.RouteWaypointIndex + 1 < agent.RouteWaypoints.Count)
                        {
                            Vector2 next = agent.RouteWaypoints[agent.RouteWaypointIndex + 1];
                            if (Vector2.DistanceSquared(agent.Position, next) <= range * range &&
                                simulation.HasTraversablePath(agent.Position, next, clearance))
                            {
                                agent.AdvanceRouteWaypoint();
                                waypoint = next;
                            }
                        }
                        return VectorMath.SafeNormalize(waypoint - agent.Position);
                    }
                    agent.ClearRoute();
                    memory.GuidancePoint = null;
                    memory.IgnoreSharedFieldUntilFrame = simulation.FrameIndex + SecondsToFrames(1.0f);
                    RequestNaturalRoute(simulation, agent, goal, key);
                }
            }

            if (string.Equals(memory.GuidanceKey, key, StringComparison.Ordinal) &&
                memory.GuidancePoint is Vector2 cached &&
                simulation.FrameIndex < memory.GuidanceRefreshFrame &&
                Vector2.DistanceSquared(agent.Position, cached) > 0.0225f)
                return VectorMath.SafeNormalize(cached - agent.Position);

            memory.GuidanceKey = key;
            memory.GuidanceRefreshFrame = simulation.FrameIndex + SecondsToFrames(
                _config.NaturalDecisionSeconds + (agent.AgentId % 3) * 0.03f);
            if (simulation.HasTraversablePath(agent.Position, goal, clearance))
            {
                memory.GuidancePoint = goal;
                return VectorMath.SafeNormalize(goal - agent.Position);
            }
            if (simulation.FrameIndex >= memory.IgnoreSharedFieldUntilFrame &&
                simulation.HybridMovement.Fields.TryDirection(simulation, agent.Position,
                    agent.ExitTarget?.Position ?? goal, out Vector2 fieldDirection, out float fieldCost))
            {
                memory.RemainingFieldCost = fieldCost;
                memory.FieldGeometryVersion = simulation.HybridMovement.Fields.GeometryRefreshes;
                memory.FieldCostFrame = simulation.FrameIndex;
                memory.GuidancePoint = agent.Position + fieldDirection * 0.70f;
                return fieldDirection;
            }
            memory.GuidancePoint = null;
            RequestNaturalRoute(simulation, agent, goal, key);
            return TryInjuredChairClearance(simulation, agent, goal - agent.Position);
        }

        private Vector2 ApplyNaturalRecoveryDirection(Simulation simulation, Agent agent, Vector2 direction, bool exitCapture)
        {
            if (!UsesNaturalNavigation(simulation) || !IsNaturalMover(agent)) return direction;
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (exitCapture && Vector2.DistanceSquared(agent.Position, agent.ExitTarget!.Position) < 1.0f)
            {
                memory.DetourGoal = null;
                return direction;
            }
            if (memory.DetourGoal is not Vector2 goal) return direction;
            float distance = Vector2.Distance(agent.Position, goal);
            if (distance <= 0.23f)
            {
                memory.SuccessfulDetours++;
                memory.DetourGoal = null;
                memory.Stage = NavigationRecoveryStage.FollowingRoute;
                memory.GuidancePoint = null;
                memory.NextRecoveryFrame = simulation.FrameIndex + SecondsToFrames(0.6f);
                agent.ClearRoute();
                if (memory.ReplanAfterDetour)
                {
                    // Replan from the actual completed detour position. Planning before arrival
                    // would waste work and could keep pulling the agent back into the old trap.
                    memory.ReplanAfterDetour = false;
                    memory.IgnoreSharedFieldUntilFrame = simulation.FrameIndex + SecondsToFrames(3f);
                    memory.NextRouteRequestFrame = simulation.FrameIndex;
                    RequestNaturalRoute(simulation, agent, ExitApproachTarget(agent, agent.ExitTarget!),
                        $"exit:{agent.ExitTarget!.Name}");
                }
                return ResolveExitNavigationDirection(simulation, agent, agent.ExitTarget!);
            }
            if (memory.DetourBestDistance - distance >= 0.10f)
            {
                memory.DetourBestDistance = distance;
                memory.DetourLastProgressFrame = simulation.FrameIndex;
            }
            bool failed = simulation.FrameIndex >= memory.DetourUntilFrame ||
                (simulation.FrameIndex - memory.DetourLastProgressFrame) * _config.Dt >= 1.65f;
            if (!failed && simulation.FrameIndex >= memory.DetourNextCheckFrame)
            {
                memory.DetourNextCheckFrame = simulation.FrameIndex + SecondsToFrames(_config.NaturalDecisionSeconds);
                CollectNaturalCircles(simulation, agent.Position, distance + 0.5f);
                failed = !NaturalGeometryClear(simulation, agent.Position, goal, _config.Radius + 0.005f) ||
                    NaturalToxicDetourWorsened(simulation, agent, goal);
            }
            if (failed)
            {
                memory.RememberFailure(goal, simulation.FrameIndex + SecondsToFrames(_config.NaturalFailureMemorySeconds));
                memory.DetourGoal = null;
                memory.GuidancePoint = null;
                memory.NextRecoveryFrame = simulation.FrameIndex;
                RequestNaturalRecovery(simulation, agent, force: true);
                return VectorMath.SafeNormalize(agent.ExitTarget!.Position - agent.Position);
            }
            return VectorMath.SafeNormalize(goal - agent.Position);
        }

        private void ChooseNaturalRecovery(Simulation simulation, Agent agent)
        {
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (memory.RecoveryAttempts == 0 || memory.RecoveryEpisodeStartedFrame < 0)
                memory.RecoveryEpisodeStartedFrame = simulation.FrameIndex;
            memory.RecoveryAttempts++;
            memory.TotalRecoveryAttempts++;
            memory.LastRecoveryFrame = simulation.FrameIndex;
            // Remember the unsuccessful forward solution, not the solid obstacle's centre.
            if (agent.LastDesiredDirection.LengthSquared() > 0.01f)
                memory.RememberFailure(agent.Position + VectorMath.SafeNormalize(agent.LastDesiredDirection) * 1.2f,
                    simulation.FrameIndex + SecondsToFrames(_config.NaturalFailureMemorySeconds));
            memory.AcknowledgeLoop(simulation.FrameIndex, SecondsToFrames(2.0f));
            memory.NextRecoveryFrame = simulation.FrameIndex + SecondsToFrames(0.6f + (agent.AgentId % 4) * 0.05f);
            memory.IgnoreSharedFieldUntilFrame = simulation.FrameIndex + SecondsToFrames(3.0f);
            memory.GuidancePoint = null;
            if (memory.DetourGoal is Vector2 failed)
                memory.RememberFailure(failed, simulation.FrameIndex + SecondsToFrames(_config.NaturalFailureMemorySeconds));
            memory.DetourGoal = null;
            agent.ClearRoute();
            ClearCircleFollow(agent);
            ClearSlowObstacleBypass(agent);
            ResetCasualtyBlockageEpisode(agent);
            agent.WallFollowDir = null;
            agent.WallFollowNormal = null;
            agent.WallFollowTangent = null;
            agent.WallFollowUntil = simulation.FrameIndex;
            agent.Exploring = false;
            agent.TempGoal = null;

            float episodeSeconds = (simulation.FrameIndex - memory.RecoveryEpisodeStartedFrame) * _config.Dt;
            bool repeatedlyUnsuccessful = memory.RecoveryAttempts >= 3 &&
                episodeSeconds >= _config.NaturalExitReconsiderSeconds;
            if ((memory.RecoveryAttempts >= 2 && memory.NoProgressSeconds >= _config.NaturalExitReconsiderSeconds) ||
                repeatedlyUnsuccessful)
                TryNaturalAlternativeExit(simulation, agent);
            memory.ReplanAfterDetour = memory.RecoveryAttempts >= 2;

            if (TryChooseNaturalRecoveryGoal(simulation, agent, out Vector2 goal))
            {
                memory.DetourGoal = goal;
                NaturalFumeRisk fumeRisk = SampleNaturalFumeRisk(simulation, agent, goal);
                memory.DetourToxicPeak = fumeRisk.Peak;
                memory.DetourToxicEnd = fumeRisk.End;
                memory.DetourStartedFrame = simulation.FrameIndex;
                memory.DetourLastProgressFrame = simulation.FrameIndex;
                memory.DetourBestDistance = Vector2.Distance(agent.Position, goal);
                float speed = Math.Max(0.20f, agent.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds);
                float commitment = Math.Clamp(Math.Max(_config.NaturalDetourCommitSeconds,
                    1.6f * memory.DetourBestDistance / speed), 1.0f, 10.0f);
                memory.DetourUntilFrame = simulation.FrameIndex + SecondsToFrames(commitment);
                memory.DetourNextCheckFrame = simulation.FrameIndex + SecondsToFrames(_config.NaturalDecisionSeconds);
                memory.Stage = memory.RecoveryAttempts == 1 ? NavigationRecoveryStage.LocalDetour : NavigationRecoveryStage.WiderDetour;
                return;
            }

            // A genuinely sealed or densely occupied space need not have a feasible detour.
            // Retain physical constraints and re-observe later, rather than teleporting anyone.
            memory.Stage = NavigationRecoveryStage.WaitingForOpening;
            RequestNaturalRoute(simulation, agent, ExitApproachTarget(agent, agent.ExitTarget!), $"exit:{agent.ExitTarget!.Name}");
        }

        /// <summary>At most 36 local candidates, one nearby-person query, and reusable geometry buffers.</summary>
        internal bool TryChooseNaturalRecoveryGoal(Simulation simulation, Agent agent, out Vector2 bestGoal)
        {
            bestGoal = default;
            if (agent.ExitTarget is null) return false;
            NaturalNavigationMemory memory = agent.NavigationMemory;
            float maxLength = memory.RecoveryAttempts <= 1 ? 1.7f : 3.2f;
            float speed = agent.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds;
            if (speed < 0.5f) maxLength = Math.Min(maxLength, 1.7f);
            maxLength = Math.Min(maxLength, _config.NaturalPerceptionRadiusMeters * Math.Clamp(agent.VisibilityFactor, 0.35f, 1f));
            CollectNaturalCircles(simulation, agent.Position, maxLength + 0.6f);
            simulation.CollectNearbyAgents(agent, maxLength + 1.25f, _naturalPeople);
            Vector2 forward = VectorMath.SafeNormalize(agent.ExitTarget.Position - agent.Position, Vector2.UnitX);
            ReadOnlySpan<float> lengths = stackalloc float[] { 0.75f, 1.6f, 3.2f };
            float bestScore = float.NegativeInfinity;
            foreach (float length in lengths)
            {
                if (length > maxLength + 0.01f) continue;
                for (int sample = 0; sample < 12; sample++)
                {
                    // Rotate the sampling fan slightly between attempts, deterministically.
                    float angle = sample * ReferenceMath.Tau / 12f + (memory.RecoveryAttempts % 3 - 1) * 0.08f;
                    Vector2 candidateDirection = Rotate(forward, angle);
                    Vector2 candidate = agent.Position + candidateDirection * length;
                    if (!NaturalGeometryClear(simulation, agent.Position, candidate, _config.Radius + 0.01f)) continue;
                    float crowding = 0f;
                    bool blocked = false;
                    foreach (Agent other in _naturalPeople)
                    {
                        if (!other.IsActive) continue;
                        float separation = _config.Radius * (other.IsCasualtyObstacle ? 2.25f : 2f) + 0.015f;
                        if (!SeparatingOrClearSegment(agent.Position, candidate, other.Position, separation))
                        {
                            blocked = true;
                            break;
                        }
                        float squared = Vector2.DistanceSquared(candidate, other.Position);
                        if (squared < 1.44f) crowding += 1f - squared / 1.44f;
                    }
                    if (blocked) continue;
                    float clearance = 1.2f;
                    foreach (CircleBlockage circle in _naturalCircles)
                        clearance = Math.Min(clearance, Vector2.Distance(candidate, circle.Center) - circle.Radius - _config.Radius);
                    foreach (Wall wall in simulation.Environment.Walls)
                        clearance = Math.Min(clearance, Vector2.Distance(candidate, wall.ClosestPoint(candidate)) - _config.Radius);
                    float progress = Vector2.Dot(candidateDirection, forward);
                    float parity = (agent.AgentId & 1) == 0 ? 1f : -1f;
                    float score = 1.1f * Math.Clamp(clearance, 0f, 1.2f) - 1.6f * crowding +
                        0.48f * progress + 0.20f * length + 0.025f * parity * MathF.Sin(angle) -
                        3.0f * memory.FailurePenalty(candidate, simulation.FrameIndex) -
                        1.3f * memory.RecentVisitPenalty(candidate, simulation.FrameIndex, _config.Dt) -
                        NaturalToxicPathPenalty(simulation, agent, candidate);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestGoal = candidate;
                }
            }
            return float.IsFinite(bestScore);
        }

        private void CollectNaturalCircles(Simulation simulation, Vector2 origin, float range)
        {
            _naturalCircles.Clear();
            foreach (CircleBlockage circle in simulation.Environment.CircularBlockages)
            {
                float reach = range + circle.Radius + _config.Radius;
                if (Vector2.DistanceSquared(origin, circle.Center) <= reach * reach) _naturalCircles.Add(circle);
            }
        }

        private bool NaturalGeometryClear(Simulation simulation, Vector2 start, Vector2 end, float radius)
        {
            float border = simulation.Environment.BoundaryThickness + radius;
            if (end.X < border || end.Y < border || end.X > simulation.Environment.Width - border ||
                end.Y > simulation.Environment.Height - border) return false;
            foreach (Wall wall in simulation.Environment.Walls)
            {
                bool intersects = wall.OrientedFootprint is OrientedRectangle rotated
                    ? rotated.IntersectsSegment(start, end, radius)
                    : SegmentIntersectsAabb(start, end, wall.X1 - radius, wall.Y1 - radius, wall.X2 + radius, wall.Y2 + radius);
                if (!intersects) continue;
                Vector2 startOffset = start - wall.ClosestPoint(start);
                Vector2 endOffset = end - wall.ClosestPoint(end);
                // Permit outward release from pre-existing contact, never new penetration.
                if (startOffset.LengthSquared() <= 0.000001f ||
                    startOffset.LengthSquared() > radius * radius + 0.000001f ||
                    Vector2.Dot(startOffset, end - start) < 0f || endOffset.LengthSquared() < radius * radius)
                    return false;
            }
            foreach (CircleBlockage circle in _naturalCircles)
                if (!SeparatingOrClearSegment(start, end, circle.Center, circle.Radius + radius)) return false;
            return true;
        }

        /// <summary>Analytic swept-circle clearance, including monotonic release from existing contact.</summary>
        internal static bool SeparatingOrClearSegment(Vector2 start, Vector2 end, Vector2 center, float radius)
        {
            Vector2 delta = end - start;
            Vector2 relative = start - center;
            float squared = radius * radius;
            if (relative.LengthSquared() < squared)
                return Vector2.Dot(relative, delta) >= 0f && Vector2.DistanceSquared(end, center) >= squared;
            float lengthSquared = delta.LengthSquared();
            if (lengthSquared <= 0.0000001f) return relative.LengthSquared() >= squared;
            float t = Math.Clamp(-Vector2.Dot(relative, delta) / lengthSquared, 0f, 1f);
            return (relative + delta * t).LengthSquared() >= squared;
        }

        private void TryNaturalAlternativeExit(Simulation simulation, Agent agent)
        {
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (simulation.Environment.Exits.Count < 2 || agent.ExitTarget is null ||
                (simulation.FrameIndex - memory.LastExitChangeFrame) * _config.Dt < 5.0f) return;
            Exit current = agent.ExitTarget;
            Exit? best = null;
            float bestCost = float.PositiveInfinity;
            foreach (Exit exit in simulation.Environment.Exits)
            {
                if (ReferenceEquals(exit, current)) continue;
                float cost = NaturalPerceivedExitCost(simulation, agent, exit);
                if (memory.RecentlyFailedExit == exit.Name && simulation.FrameIndex < memory.FailedExitUntilFrame) cost += 20f;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = exit;
            }
            if (best is null) return;
            float currentCost = NaturalPerceivedExitCost(simulation, agent, current) + 12f;
            if (bestCost > currentCost && memory.RecoveryAttempts < 4) return;
            memory.RecentlyFailedExit = current.Name;
            memory.FailedExitUntilFrame = simulation.FrameIndex + SecondsToFrames(_config.NaturalFailureMemorySeconds);
            memory.LastExitChangeFrame = simulation.FrameIndex;
            memory.ExitChanges++;
            memory.Stage = NavigationRecoveryStage.ReconsideringExit;
            agent.ExitTarget = best;
            memory.ResetRouteCostObservation();
            agent.ExitCommitUntilFrame = simulation.FrameIndex + SecondsToFrames(5.0f);
            agent.ResetExitCongestion();
            memory.GuidanceKey = null;
            memory.NextRouteRequestFrame = simulation.FrameIndex;
        }

        private float NaturalPerceivedExitCost(Simulation simulation, Agent agent, Exit exit, float? toxicPenalty = null)
        {
            float distance = Vector2.Distance(agent.Position, exit.Position);
            float range = _config.NaturalPerceptionRadiusMeters * Math.Clamp(agent.VisibilityFactor, 0.35f, 1f);
            NaturalNavigationMemory memory = agent.NavigationMemory;
            float crowdCost = 0.8f; // unknown is uncertain, not known empty
            if (distance <= range && simulation.HasLineOfSight(agent.Position, exit.Position))
            {
                crowdCost = LocalDensity(simulation, exit) * _config.ExitCongestionWeight +
                    DoorwayOccupancy(simulation, exit, agent.AgentId) * _config.ExitCongestionWeight;
                memory.ExitObservations[exit.Name] = (crowdCost, simulation.FrameIndex);
            }
            else if (memory.ExitObservations.TryGetValue(exit.Name, out var observation))
            {
                float age = (simulation.FrameIndex - observation.Frame) * _config.Dt;
                crowdCost = 0.8f + (observation.Cost - 0.8f) * MathF.Exp(-age / 8f);
            }
            return distance + crowdCost + (toxicPenalty ?? NaturalToxicPathPenalty(simulation, agent, exit.Position));
        }

        internal Vector2 ApplyNaturalMovingFurnitureAvoidance(Simulation simulation, Agent agent, Vector2 direction)
        {
            // Blast-driven motion belongs to Explosion only. Ordinary furniture collision and
            // chair-pushing remain shared; Toxic Fume does not run this hazard predictor.
            if (simulation.ScenarioType != ScenarioType.Explosion ||
                !UsesNaturalNavigation(simulation) || !IsNaturalMover(agent)) return direction;
            NaturalNavigationMemory memory = agent.NavigationMemory;
            if (simulation.FrameIndex >= memory.NextHazardCheckFrame)
            {
                memory.NextHazardCheckFrame = simulation.FrameIndex + SecondsToFrames(_config.NaturalDecisionSeconds);
                memory.FurnitureAvoidance = Vector2.Zero;
                simulation.CollectMovingFurnitureNear(agent.Position, 5.0f, _naturalMovingFurniture);
                foreach (FurnitureBody body in _naturalMovingFurniture)
                {
                    Vector2 relative = body.Position - agent.Position;
                    float distance = relative.Length();
                    Vector2 heading = VectorMath.SafeNormalize(agent.LastDesiredDirection, direction);
                    bool perceived = distance <= 1.1f ||
                        (Vector2.Dot(heading, VectorMath.SafeNormalize(relative)) >= -0.34f &&
                         HasLineOfSight(agent.Position, body.Position, simulation.Environment.Walls));
                    if (!perceived || body.Height > AgentConfig.HeightMeters + 0.4f) continue;
                    Vector2 velocity = body.Velocity - agent.VelocityMetersPerSecond;
                    float squaredSpeed = velocity.LengthSquared();
                    if (squaredSpeed < 0.25f) continue;
                    float time = -Vector2.Dot(relative, velocity) / squaredSpeed;
                    if (time < 0f || time > 0.55f) continue;
                    Vector2 nearest = relative + velocity * time;
                    float reach = body.Radius + _config.Radius + 0.20f;
                    if (nearest.LengthSquared() >= reach * reach) continue;
                    Vector2 away = VectorMath.SafeNormalize(-nearest,
                        VectorMath.Perpendicular(VectorMath.SafeNormalize(velocity, Vector2.UnitX)));
                    memory.FurnitureAvoidance += away * (1f - time / 0.70f);
                }
                memory.FurnitureAvoidance = VectorMath.ClampMagnitude(memory.FurnitureAvoidance, 1.0f);
            }
            // This is only a short-horizon inference from current visible velocity. No landing
            // location, future collision result or precomputed furniture trajectory is available.
            return VectorMath.SafeNormalize(direction + 1.25f * memory.FurnitureAvoidance, direction);
        }
    }
}
