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
        /// <summary>Simple pre-explosion wandering with noisy direction drift.</summary>
        public void UpdateWandering(Agent agent)
        {
            if (agent.WanderDir is null)
            {
                agent.WanderDir = VectorMath.SafeNormalize(VectorMath.RandomGaussian2(Random));
            }

            Vector2 wander = agent.WanderDir.Value + 0.2f * MathF.Sqrt(Config.Agent.StepScale) * VectorMath.RandomGaussian2(Random);
            wander = VectorMath.SafeNormalize(wander, agent.WanderDir.Value);
            agent.WanderDir = wander;
            agent.Velocity = wander * Math.Min(agent.BaseSpeed * 0.7f, Config.Agent.Behavior.MaxRunningSpeedPerReferenceStep);
            MoveAgent(agent);
        }

        /// <summary>Advance the simulation by one fixed frame.</summary>
        public void Step()
        {
            if (!Running || Agents.Count == 0)
            {
                return;
            }

            try
            {
                StepCore();
            }
            finally { _sightSnapshotActive = false; _circleIndexActive = false; }
        }

        private void StepCore()
        {
            long stepStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            _circleIndexActive = true; _circleIndexCount = -1;
            _sightWalls.Clear(); _sightCircles.Clear();
            foreach (Wall wall in Environment.Walls) if (!wall.IsFurniture) _sightWalls.Add(wall);
            foreach (CircleBlockage circle in Environment.CircularBlockages) if (!circle.IsFurniture) _sightCircles.Add(circle);
            _sightSnapshotActive = true;
            LastRoutePlanningMilliseconds = 0d;
            LastRoutePlanRequests = 0;
            LastSfmMilliseconds = LastCollisionMilliseconds = 0d;
            long phaseStarted = stepStarted;
            RebuildAgentGrid();
            LastGridMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();

            // Resolve the selected hazard before panic for this frame. Both scenarios feed the same
            // panic/SFM/route engine; only the hazard-specific exposure calculation differs.
            if (ScenarioType == CrowdSim.Core.Model.ScenarioType.Explosion)
            {
                ExplosionSystem.ApplyExplosionEffects(this);
                if (Environment.Explosion is not null && !BlastOver())
                {
                    foreach (Agent agent in Agents)
                    {
                        RefreshAgentCell(agent); // knockback can cross a cell before perception
                    }
                }
            }
            else if (ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume)
            {
                ToxicFumeSystem.UpdateAgents(this);
            }

            LastHazardMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            EvacuationSystem.UpdatePanic(this);
            if (ScenarioType == CrowdSim.Core.Model.ScenarioType.Explosion)
            {
                ExplosionSystem.UpdateDebris(this);
            }

            if (FrameIndex - _exitLoadLastUpdate >= _exitLoadUpdateInterval)
            {
                _exitLoadCache.Clear();
                foreach (Agent agent in Agents)
                {
                    if (agent.ExitTarget is null || !agent.IsActive)
                    {
                        continue;
                    }

                    _exitLoadCache.TryGetValue(agent.ExitTarget.Name, out int load);
                    _exitLoadCache[agent.ExitTarget.Name] = load + 1;
                }

                _exitLoadLastUpdate = FrameIndex;
            }

            foreach (Agent agent in Agents)
            {
                if (agent.State != AgentState.WaitingToReact)
                {
                    continue;
                }

                int reactionStart = agent.ReactionStartedFrame >= 0
                    ? agent.ReactionStartedFrame
                    : FrameIndex;
                if (FrameIndex - reactionStart < agent.EffectiveReactionDelayFrames ||
                    FrameIndex < agent.FreezeUntilFrame)
                {
                    continue;
                }

                agent.ExitTarget = ChooseInitialExit(agent);
                if (agent.ExitTarget is not null)
                {
                    agent.ExitCommitUntilFrame = FrameIndex +
                        Math.Max(1, (int)(Config.Agent.RouteCommitmentSeconds / Config.Agent.Dt));

                    // Update the local assignment estimate immediately so agents that finish their
                    // reaction on the same frame do not all see an identical stale "least crowded"
                    // cache and select the same doorway. The periodic cache rebuild still remains
                    // the authoritative correction once movement is under way.
                    _exitLoadCache.TryGetValue(agent.ExitTarget.Name, out int assignedLoad);
                    _exitLoadCache[agent.ExitTarget.Name] = assignedLoad + 1;
                }

                if (agent.State is not (AgentState.Incapacitated or AgentState.Fallen or AgentState.Dead))
                {
                    agent.State = agent.InjurySpeedFactor < 0.999f
                        ? AgentState.Injured
                        : AgentState.Evacuating;
                    EvacuationSystem.BeginPurposefulResponse(this, agent);
                }
            }

            foreach (Agent agent in Agents)
            {
                if (agent.State == AgentState.Fallen && agent.CrawlUntilFrame > 0 && FrameIndex >= agent.CrawlUntilFrame)
                {
                    agent.MarkCrawling();
                }
                else if (agent.State == AgentState.TempIncap && agent.CrawlUntilFrame > 0 && FrameIndex >= agent.CrawlUntilFrame)
                {
                    agent.MarkCrawling();
                }
            }

            LastResponseMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            long furnitureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            UpdateFurniture();
            LastFurnitureMilliseconds = ReferenceClock.ElapsedMilliseconds(furnitureStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            EvacuationSystem.BeginNaturalNavigationFrame(this);
            HybridMovement.BeginFrame(this);
            LastPreparationMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (Agent agent in Agents)
            {
                if (UpdateSeating(agent))
                {
                    RefreshAgentCell(agent);
                    continue;
                }
                switch (agent.State)
                {
                    case AgentState.Wandering:
                        UpdateWandering(agent);
                        break;

                    case AgentState.Evacuating:
                    case AgentState.Injured:
                    case AgentState.Crawling:
                        // The Python source stores a stunned-until guard but only renders its halo.
                        // This C# port enforces the documented short stun so the feature is functional.
                        if (agent.StunnedUntilFrame > FrameIndex && agent.State != AgentState.Crawling)
                        {
                            agent.Velocity = Vector2.Zero;
                            break;
                        }

                        if (agent.ExitTarget is null && Environment.Exits.Count > 0)
                        {
                            agent.ExitTarget = ChooseInitialExit(agent);
                        }

                        bool applyLaneLogic = true;
                        if (agent.ExitTarget is not null && agent.WallFollowDir is not null &&
                            !EvacuationSystem.UsesNaturalNavigation(this))
                        {
                            float exitDistance = Vector2.Distance(agent.Position, agent.ExitTarget.Position);
                            if (!float.IsFinite(agent.LastExitDist))
                            {
                                agent.LastExitDist = exitDistance;
                            }

                            int stuckFrameCount = FrameIndex - agent.WallFollowStartFrame;
                            if (stuckFrameCount > (int)(1.5f / Config.Agent.Dt) &&
                                exitDistance > agent.LastExitDist - 2f)
                            {
                                applyLaneLogic = false;
                            }
                            else if (exitDistance < agent.LastExitDist)
                            {
                                agent.LastExitDist = exitDistance;
                            }
                        }

                        if (!Config.HybridEnabled || agent.State != AgentState.Evacuating ||
                            agent.ExitTarget is null || (FrameIndex + agent.AgentId) % 3 == 0)
                            EvacuationSystem.MaybeReselectExit(this, agent);

                        if (applyLaneLogic)
                        {
                            EvacuationSystem.UpdateEvacAgent(this, agent);
                        }
                        else
                        {
                            agent.Velocity *= 0.7f;
                            if (agent.Velocity.Length() > 0.3f * agent.BaseSpeed)
                            {
                                agent.Velocity *= 0.5f;
                            }
                        }

                        break;

                    case AgentState.WaitingToReact:
                    case AgentState.Fallen:
                    case AgentState.Incapacitated:
                    case AgentState.TempIncap:
                    case AgentState.Dead:
                        agent.Velocity = Vector2.Zero;
                        break;

                    case AgentState.Exited:
                        break;
                }
                RefreshAgentCell(agent);
            }

            LastMovementMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            ResolveCrowdContacts();
            LastContactsMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            phaseStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            EvacuationSystem.ApplyExitFlows(this);
            UpdateExitMetrics();
            LastExitMilliseconds = ReferenceClock.ElapsedMilliseconds(phaseStarted);
            FrameIndex++;
            LastStepMilliseconds = ReferenceClock.ElapsedMilliseconds(stepStarted);
        }

        /// <summary>
        /// Replacement for the stale Python Simulation.render metrics path. Front ends render
        /// directly from this immutable lightweight snapshot without coupling the physics core to a renderer.
        /// </summary>
        public SimulationRenderSnapshot Render()
            => new(
                FrameIndex,
                Environment.Walls.ToArray(),
                Environment.Exits.ToArray(),
                Agents.ToArray(),
                Debris.ToArray(),
                ExitMetrics.ToDictionary(pair => pair.Key, pair => pair.Value),
                Environment.Explosion);

        private Exit? ChooseInitialExit(Agent agent)
        {
            if (Environment.Exits.Count == 0)
            {
                return null;
            }

            // Low-familiarity pedestrians prefer currently visible exits. Select directly without
            // allocating temporary arrays or LINQ sort buffers; if none are visible, fall back to
            // the full exit set exactly as before.
            bool requireVisible = agent.RouteFamiliarity < 0.45f;
            Exit? selected = SelectInitialExit(agent, requireVisible);
            return selected ?? SelectInitialExit(agent, requireVisible: false);
        }

        private Exit? SelectInitialExit(Agent agent, bool requireVisible)
        {
            Exit? best = null;
            int bestLoad = int.MaxValue;
            float bestDistance = float.PositiveInfinity;

            foreach (Exit exit in Environment.Exits)
            {
                if (requireVisible && !HasLineOfSight(agent.Position, exit.Position))
                {
                    continue;
                }

                float distance = Vector2.Distance(agent.Position, exit.Position);
                if (agent.Strategy == DecisionStrategy.Nearest)
                {
                    if (best is null ||
                        distance < bestDistance ||
                        (distance == bestDistance &&
                         string.Compare(exit.Name, best.Name, StringComparison.Ordinal) < 0))
                    {
                        best = exit;
                        bestDistance = distance;
                    }

                    continue;
                }

                int load = _exitLoadCache.TryGetValue(exit.Name, out int currentLoad) ? currentLoad : 0;
                if (best is null ||
                    load < bestLoad ||
                    (load == bestLoad && distance < bestDistance) ||
                    (load == bestLoad && distance == bestDistance &&
                     string.Compare(exit.Name, best.Name, StringComparison.Ordinal) < 0))
                {
                    best = exit;
                    bestLoad = load;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void UpdateExitMetrics()
        {
            float roiRadius = 10f * Config.Agent.Radius;
            float roiRadiusSquared = roiRadius * roiRadius;
            float dt = Config.Agent.Dt;

            foreach (Exit exit in Environment.Exits)
            {
                int densityRaw = 0;
                int exitedTotal = 0;
                foreach (Agent agent in Agents)
                {
                    if (agent.IsActive &&
                        Vector2.DistanceSquared(agent.Position, exit.Position) <= roiRadiusSquared)
                    {
                        densityRaw++;
                    }

                    if (agent.State == AgentState.Exited && ReferenceEquals(agent.ExitTarget, exit))
                    {
                        exitedTotal++;
                    }
                }

                _previousExitedByExit.TryGetValue(exit.Name, out int previousTotal);
                int exitedDelta = Math.Max(0, exitedTotal - previousTotal);
                _previousExitedByExit[exit.Name] = exitedTotal;
                float flowRate = exitedDelta / dt;
                ExitMetrics[exit.Name] = new ExitMetrics(densityRaw, flowRate);
            }
        }
    }

    /// <summary>Renderer-neutral frame snapshot returned by Simulation.Render().</summary>
    public sealed record SimulationRenderSnapshot(
        int FrameIndex,
        IReadOnlyList<Wall> Walls,
        IReadOnlyList<Exit> Exits,
        IReadOnlyList<Agent> Agents,
        IReadOnlyList<Debris> Debris,
        IReadOnlyDictionary<string, ExitMetrics> ExitMetrics,
        Explosion? Explosion);
}
