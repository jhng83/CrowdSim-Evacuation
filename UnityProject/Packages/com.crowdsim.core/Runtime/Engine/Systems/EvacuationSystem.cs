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
    /// Panic contagion, herding, local navigation, deadlock escape, exit reselection and
    /// width-aware exit flow. The patch/guard logic from evacuation_logic.py is kept explicit.
    /// </summary>
    public sealed partial class EvacuationSystem
    {
        public const float SnapRadius = 2.43f;
        public const int ReselectInterval = 15;

        private readonly AgentConfig _config;
        private readonly VisibilityGraphPlanner _routePlanner;
        private readonly Dictionary<Exit, (int Frame, double Credit)> _exitAdmissionCredits = new();
        private readonly List<Agent> _nearbyScratch = new(128);
        private readonly List<Agent> _exitQueryScratch = new(192);
        private readonly List<Agent> _exitCandidatesScratch = new(128);
        private readonly List<Agent> _exitEligibleScratch = new(128);
        private readonly List<Agent> _explorationCrowdScratch = new(64);
        private readonly List<Agent> _bypassScratch = new(32);
        private float[] _panicSnapshot = Array.Empty<float>();
        private bool[] _activeAlarmSnapshot = Array.Empty<bool>();
        private int[] _alarmEpisodeSnapshot = Array.Empty<int>();

        public EvacuationSystem(AgentConfig config)
        {
            _config = config;
            _routePlanner = new VisibilityGraphPlanner(config);
        }

        public static bool LineIntersectsRect(Vector2 p1, Vector2 p2, Wall wall)
        {
            if (wall.OrientedFootprint is OrientedRectangle rotated)
                return rotated.IntersectsSegment(p1, p2);
            // Avoid allocating a four-corner array for every line-of-sight test. This routine sits
            // on several hot perception/routing paths and is called thousands of times per step.
            Vector2 topLeft = new(wall.X1, wall.Y1);
            Vector2 topRight = new(wall.X2, wall.Y1);
            Vector2 bottomRight = new(wall.X2, wall.Y2);
            Vector2 bottomLeft = new(wall.X1, wall.Y2);
            return
                SegmentsIntersect(p1, p2, topLeft, topRight) ||
                SegmentsIntersect(p1, p2, topRight, bottomRight) ||
                SegmentsIntersect(p1, p2, bottomRight, bottomLeft) ||
                SegmentsIntersect(p1, p2, bottomLeft, topLeft);
        }

        public static bool HasLineOfSight(Vector2 agentPosition, Vector2 exitPosition, IReadOnlyList<Wall> walls)
        {
            foreach (Wall wall in walls)
            {
                if (wall.IsFurniture) continue;
                if (LineIntersectsRect(agentPosition, exitPosition, wall))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return whether a circular pedestrian can translate from start to end without its
        /// body intersecting a wall. Perception intentionally keeps using centre-point LOS;
        /// navigation instead inflates each wall by the required body clearance.
        /// </summary>
        public static bool HasTraversablePath(
            Vector2 start,
            Vector2 end,
            IReadOnlyList<Wall> walls,
            float clearance)
        {
            float margin = Math.Max(0f, clearance);
            foreach (Wall wall in walls)
            {
                if (wall.OrientedFootprint is OrientedRectangle rotated
                    ? rotated.IntersectsSegment(start, end, margin)
                    : SegmentIntersectsAabb(start, end, wall.X1 - margin, wall.Y1 - margin,
                        wall.X2 + margin, wall.Y2 + margin))
                {
                    return false;
                }
            }

            return true;
        }

        public void UpdatePanic(Simulation simulation)
        {
            // Snapshot panic before updating anyone so contagion does not depend on agent list order.
            // Arrays replace three per-step dictionaries; AgentId is the stable dense identifier
            // assigned when the scenario population is spawned.
            int snapshotLength = simulation.Agents.Count;
            EnsureSnapshotCapacity(snapshotLength);
            foreach (Agent candidate in simulation.Agents)
            {
                int id = candidate.AgentId;
                _panicSnapshot[id] = candidate.PanicLevel;
                _activeAlarmSnapshot[id] = candidate.IsActiveAlarmSource(simulation.FrameIndex);
                _alarmEpisodeSnapshot[id] = candidate.ActiveAlarmEpisodeId;
            }

            Explosion? explosion = simulation.Environment.Explosion;
            bool toxicFumeActive = simulation.ScenarioType == ScenarioType.ToxicFume &&
                simulation.ToxicFumeSystem.IsReleased;
            bool emergencyActive = explosion is not null || toxicFumeActive;

            foreach (Agent agent in simulation.Agents)
            {
                if (!agent.IsActive || agent.State is AgentState.Exited or AgentState.Dead or
                    AgentState.Incapacitated or AgentState.TempIncap)
                {
                    continue;
                }

                float panicDt = _config.Dt;
                if (simulation.Config.HybridEnabled && agent.State == AgentState.Evacuating &&
                    agent.Posture == SeatingPosture.Walking && agent.ShockwaveHitFrame != simulation.FrameIndex &&
                    !toxicFumeActive && agent.LocalCongestion < 0.3f)
                {
                    if (agent.HybridLastPanicFrame >= 0 && (simulation.FrameIndex + agent.AgentId) % 3 != 0) continue;
                }
                if (simulation.Config.HybridEnabled && agent.HybridLastPanicFrame >= 0)
                    panicDt = Math.Max(_config.Dt, (simulation.FrameIndex - agent.HybridLastPanicFrame) * _config.Dt);
                agent.HybridLastPanicFrame = simulation.FrameIndex;

                agent.ExpireAlarmIfNeeded(simulation.FrameIndex);
                agent.VisibilityFactor = simulation.VisibilitySystem.GetVisibilityFactor(simulation, agent.Position);


                float threat = 0f;
                if (explosion is not null &&
                    agent.ShockwaveHit &&
                    agent.ShockwaveHitFrame >= 0 &&
                    !agent.BlastShielded)
                {
                    float exposureAgeSeconds = Math.Max(
                        0f,
                        (simulation.FrameIndex - agent.ShockwaveHitFrame) * _config.Dt);
                    if (exposureAgeSeconds <= _config.DirectThreatDurationSeconds)
                    {
                        float distance = Vector2.Distance(agent.Position, explosion.Position);
                        if (distance <= _config.HeatRadius)
                        {
                            float spatial = MathF.Exp(-distance / Math.Max(_config.PanicThreatScale, 1e-5f));
                            float temporal = MathF.Exp(
                                -exposureAgeSeconds / Math.Max(_config.DirectThreatDecaySeconds, 1e-5f));
                            threat = spatial * temporal;
                        }
                    }
                }

                // Toxic-fume threat is a normalized cached concentration lookup. Propagation itself
                // was precomputed during setup, so this adds no live dispersion solver to the hot loop.
                if (toxicFumeActive)
                {
                    float concentration = agent.CurrentToxicConcentration;
                    float awareness = simulation.ToxicFumeSystem.Configuration.AwarenessConcentration;
                    float toxicThreat = concentration <= awareness
                        ? 0f
                        : Math.Clamp((concentration - awareness) / Math.Max(0.05f, 1f - awareness), 0f, 1f);
                    threat = Math.Max(threat, toxicThreat);
                }

                float perceptionRadius = Math.Max(
                    _config.RunningContagionRadius,
                    _config.EvacuationAwarenessRadius) *
                    (0.45f + 0.55f * Math.Clamp(agent.VisibilityFactor, 0f, 1f));
                float socialWeighted = 0f;
                int socialSourceCount = 0;
                Vector2 runnerDirectionWeighted = Vector2.Zero;
                float runnerWeightTotal = 0f;
                float runnerIntensityWeighted = 0f;
                int visibleRunners = 0;
                Vector2 evacueeDirectionWeighted = Vector2.Zero;
                float evacueeWeightTotal = 0f;
                int visibleEvacuees = 0;
                int movingEvacuees = 0;
                int relayEpisodeId = -1;
                float relayEpisodeScore = 0f;

                simulation.CollectNearbyAgents(agent,
                    Math.Max(perceptionRadius, _config.LocalCongestionRadius), _nearbyScratch);
                int congestionNeighbours = 0;
                float congestionRadiusSquared = _config.LocalCongestionRadius * _config.LocalCongestionRadius;
                foreach (Agent other in _nearbyScratch)
                {
                    float distanceSquared = Vector2.DistanceSquared(agent.Position, other.Position);
                    if (other.IsActive && distanceSquared <= congestionRadiusSquared)
                    {
                        congestionNeighbours++;
                    }
                    if (!other.IsActive || other.State is AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap)
                    {
                        continue;
                    }
                    bool possibleAlarm = _activeAlarmSnapshot[other.AgentId];
                    bool possibleAwareness = emergencyActive &&
                        other.State is (AgentState.Evacuating or AgentState.Injured);
                    if (!possibleAlarm && !possibleAwareness)
                    {
                        continue;
                    }

                    float neighbourDistance = MathF.Sqrt(distanceSquared);
                    if (neighbourDistance > perceptionRadius ||
                        !simulation.HasLineOfSight(agent.Position, other.Position))
                    {
                        continue;
                    }

                    float weight = MathF.Exp(-neighbourDistance / Math.Max(_config.PanicThreatScale, 1e-5f));
                    float sourcePanic = Math.Clamp(_panicSnapshot[other.AgentId], 0f, 1f);
                    bool activeAlarmSource = _activeAlarmSnapshot[other.AgentId];
                    if (activeAlarmSource && sourcePanic >= _config.PanicSocialSignalThreshold)
                    {
                        socialWeighted += sourcePanic * weight;
                        socialSourceCount++;
                        float socialEpisodeScore = sourcePanic * weight;
                        int sourceEpisodeId = _alarmEpisodeSnapshot[other.AgentId];
                        if (sourceEpisodeId >= 0 && socialEpisodeScore > relayEpisodeScore)
                        {
                            relayEpisodeScore = socialEpisodeScore;
                            relayEpisodeId = sourceEpisodeId;
                        }
                    }

                    float speed = other.Velocity.Length();

                    // FIX15: coherent evacuation behaviour is a separate awareness cue. A calm
                    // green evacuee can help an unaware pedestrian recognise that an emergency
                    // response is under way, but this path never creates a panic alarm source.
                    if (other.State is AgentState.Evacuating or AgentState.Injured)
                    {
                        // The semantic state itself is evidence that an evacuation is under way.
                        // Count visible evacuees even if congestion has temporarily slowed them
                        // almost to a stop; use speed only when estimating directional coherence.
                        visibleEvacuees++;
                        // 0.25 was a legacy world-units-per-step floor. Scale it to metres so
                        // coherent calm evacuees are still recognised at the same relative speed.
                        float awarenessSpeedThreshold = Math.Max(
                            0.010f,
                            other.BaseSpeed * _config.EvacuationAwarenessMinSpeedFactor);
                        if (speed >= awarenessSpeedThreshold)
                        {
                            evacueeDirectionWeighted += VectorMath.SafeNormalize(other.Velocity) * weight;
                            evacueeWeightTotal += weight;
                            movingEvacuees++;
                        }
                    }

                    float runningThreshold = Math.Max(0.014f, other.BaseSpeed * _config.RunningContagionMinSpeedFactor);
                    if (!other.CanSignalPanic || !activeAlarmSource || speed < runningThreshold)
                    {
                        continue;
                    }

                    float speedExcess = Math.Clamp((speed - runningThreshold) / Math.Max(other.BaseSpeed, 1e-5f), 0f, 1f);
                    float panicWeight = 0.35f + 0.65f * Math.Clamp(_panicSnapshot[other.AgentId], 0f, 1f);
                    float runnerWeight = weight * panicWeight;
                    runnerDirectionWeighted += VectorMath.SafeNormalize(other.Velocity) * runnerWeight;
                    runnerIntensityWeighted += speedExcess * runnerWeight;
                    runnerWeightTotal += runnerWeight;
                    visibleRunners++;

                    int runnerEpisodeId = _alarmEpisodeSnapshot[other.AgentId];
                    if (runnerEpisodeId >= 0 && runnerWeight > relayEpisodeScore)
                    {
                        relayEpisodeScore = runnerWeight;
                        relayEpisodeId = runnerEpisodeId;
                    }
                }

                agent.LocalCongestion = Math.Clamp(congestionNeighbours / 10f, 0f, 1f);
                float socialSourceLevel = socialSourceCount > 0
                    ? socialWeighted / socialSourceCount
                    : 0f;
                // Only finite active-alarm sources enter socialSourceLevel, so using the
                // source level directly restores a visible event-driven contagion wave without
                // allowing low residual panic to self-amplify after every alarm window expires.
                float social = socialSourceLevel;
                float running = 0f;
                if (visibleRunners >= _config.RunningContagionMinVisibleRunners && runnerWeightTotal > 1e-5f)
                {
                    float coherence = Math.Clamp(runnerDirectionWeighted.Length() / runnerWeightTotal, 0f, 1f);
                    if (coherence >= _config.RunningContagionCoherenceThreshold)
                    {
                        float averageIntensity = runnerIntensityWeighted / runnerWeightTotal;
                        float crowdScale = Math.Clamp(visibleRunners / 5f, 0f, 1f);
                        running = Math.Clamp(averageIntensity * coherence * crowdScale, 0f, 1f);
                    }
                }

                float evacuationAwareness = 0f;
                if (emergencyActive &&
                    visibleEvacuees >= _config.EvacuationAwarenessMinVisibleEvacuees)
                {
                    float coherence = movingEvacuees >= _config.EvacuationAwarenessMinVisibleEvacuees &&
                        evacueeWeightTotal > 1e-5f
                            ? Math.Clamp(
                                evacueeDirectionWeighted.Length() / evacueeWeightTotal,
                                0f,
                                1f)
                            : 0f;
                    bool coherentFlow = coherence >= _config.EvacuationAwarenessCoherenceThreshold;
                    bool obviousCrowdResponse = visibleEvacuees >= _config.EvacuationAwarenessCrowdOverrideCount;
                    if (coherentFlow || obviousCrowdResponse)
                    {
                        float crowdScale = Math.Clamp(
                            visibleEvacuees / (float)Math.Max(
                                _config.EvacuationAwarenessCrowdOverrideCount,
                                1),
                            0f,
                            1f);
                        float directionalEvidence = coherentFlow ? coherence : 0.35f;
                        evacuationAwareness = Math.Clamp(
                            0.45f * directionalEvidence + 0.55f * crowdScale,
                            0f,
                            1f);
                    }
                }

                bool activeAtFrameStart = _activeAlarmSnapshot[agent.AgentId];
                bool relayedAlarmActivated = false;
                if (!activeAtFrameStart &&
                    (social >= _config.SocialAlarmActivationStimulus ||
                     running >= _config.RunningAlarmActivationStimulus))
                {
                    int relayFrames = Math.Max(
                        1,
                        (int)MathF.Round(_config.RelayedAlarmSignalSeconds / Math.Max(_config.Dt, 1e-5f)));
                    relayedAlarmActivated = agent.TryActivateRelayedAlarm(
                        simulation.FrameIndex,
                        relayFrames,
                        _config.RelayedAlarmPanicFloor,
                        relayEpisodeId);

                    // FIX14: a genuine finite alarm relay is itself a perception event.
                    // Do not require the independently tuned numeric panic threshold to be
                    // crossed before a wandering observer begins the startle/reaction
                    // sequence. Panic still controls the intensity and SFM speed response,
                    // while TryActivateRelayedAlarm preserves one relay per event episode.
                    if (relayedAlarmActivated && agent.State == AgentState.Wandering)
                    {
                        int freezeUntil = simulation.FrameIndex + RandomFrameRange(
                            simulation.Random,
                            _config.FreezeMinFrames,
                            _config.FreezeMaxFrames);
                        agent.BeginReaction(
                            simulation.FrameIndex,
                            freezeUntil,
                            _config.SocialAlarmReactionDelayScale);
                    }
                }

                // Behavioural awareness is intentionally non-emotional. A wandering agent
                // surrounded by a coherent evacuation flow starts the normal reaction sequence,
                // but receives no panic floor and cannot relay a panic episode from this cue alone.
                if (!relayedAlarmActivated &&
                    agent.State == AgentState.Wandering &&
                    evacuationAwareness > 0f)
                {
                    int freezeUntil = simulation.FrameIndex + RandomFrameRange(
                        simulation.Random,
                        _config.FreezeMinFrames,
                        _config.FreezeMaxFrames);
                    agent.BeginReaction(
                        simulation.FrameIndex,
                        freezeUntil,
                        _config.EvacuationAwarenessReactionDelayScale);
                }

                // Behaviour-model rebuild: panic is an acute-stress variable, not the master
                // evacuation switch. Decay is time-based so the same scenario remains stable if
                // the fixed timestep changes, while current threat/social cues can still raise it
                // quickly. Low residual panic is not a source because contagion above only samples
                // finite active-alarm windows.
                float susceptibility = Math.Clamp(agent.ThreatSensitivity, 0.60f, 1.40f);
                float persistence =
                    _config.Behavior.StressPersistence *
                    Math.Clamp(agent.StressRecoveryBias, 0.65f, 1.35f);
                float decayTime = Math.Max(
                    _config.Dt,
                    _config.PanicDecayTimeConstantSeconds * persistence);
                float decayFactor = MathF.Exp(-panicDt / decayTime);
                float riseTime = Math.Max(
                    _config.Dt,
                    _config.PanicStimulusRiseSeconds / Math.Clamp(susceptibility, 0.75f, 1.25f));
                float riseAlpha = 1f - MathF.Exp(-panicDt / riseTime);

                // Treat current cues as a bounded acute-stress target rather than adding them to
                // panic every frame. This prevents a group of mutually visible alarmed people from
                // numerically integrating toward 1.0 simply because the same cue persists. Direct
                // exposure can sustain a high target; social/running cues produce moderate targets.
                // Per-agent susceptibility affects all genuine cues, while individual recovery bias
                // affects only how quickly stress settles after those cues disappear.
                float threatTarget = threat > 0f
                    ? Math.Clamp(
                        0.35f + 0.65f * threat *
                            susceptibility *
                            Math.Clamp(2f * _config.PanicKThreat, 0f, 2f),
                        0f,
                        1f)
                    : 0f;
                float socialTarget = social > 0f
                    ? Math.Clamp(
                        (0.20f + _config.PanicKSocial * social) *
                        (0.80f + 0.20f * susceptibility),
                        0f,
                        0.85f)
                    : 0f;
                float runningTarget = running > 0f
                    ? Math.Clamp(
                        (0.18f + _config.PanicKRunning * running) *
                        (0.80f + 0.20f * susceptibility),
                        0f,
                        0.75f)
                    : 0f;
                float stimulusTarget = Math.Max(threatTarget, Math.Max(socialTarget, runningTarget));
                float previousPanic = Math.Clamp(_panicSnapshot[agent.AgentId], 0f, 1f);
                float updatedPanic = stimulusTarget > previousPanic
                    ? previousPanic + riseAlpha * (stimulusTarget - previousPanic)
                    : stimulusTarget + (previousPanic - stimulusTarget) * decayFactor;
                updatedPanic = Math.Clamp(updatedPanic, 0f, 1f);

                if (agent.IsActiveAlarmSource(simulation.FrameIndex))
                {
                    updatedPanic = Math.Max(updatedPanic, agent.ActiveAlarmPanicFloor);
                }

                agent.PanicLevel = updatedPanic;

                agent.LastThreatPanicStimulus = threat;
                agent.LastSocialPanicStimulus = social;
                agent.LastRunningPanicStimulus = running;
                agent.LastEvacuationAwarenessStimulus = evacuationAwareness;

                if (agent.State == AgentState.Wandering && agent.PanicLevel > _config.PanicActivationThreshold)
                {
                    int freezeUntil = simulation.FrameIndex + RandomFrameRange(
                        simulation.Random,
                        _config.FreezeMinFrames,
                        _config.FreezeMaxFrames);
                    agent.BeginReaction(simulation.FrameIndex, freezeUntil);
                }
            }
        }

        private void EnsureSnapshotCapacity(int required)
        {
            if (_panicSnapshot.Length >= required)
            {
                return;
            }

            int capacity = Math.Max(required, Math.Max(64, _panicSnapshot.Length * 2));
            Array.Resize(ref _panicSnapshot, capacity);
            Array.Resize(ref _activeAlarmSnapshot, capacity);
            Array.Resize(ref _alarmEpisodeSnapshot, capacity);
        }

        /// <summary>
        /// Update the bounded doorway-pressure state. Panic is only one small input: local
        /// density, blocked progress, waiting time, proximity and stable assertiveness determine
        /// whether an agent yields, seeks a lateral gap, or maintains forward pressure.
        /// </summary>
        public void UpdateBottleneckInteraction(
            Simulation simulation,
            Agent agent,
            Exit exit,
            float distanceToExit)
        {
            if (agent.BottleneckLastUpdateFrame == simulation.FrameIndex)
            {
                return;
            }

            agent.BottleneckLastUpdateFrame = simulation.FrameIndex;
            bool eligibleState = agent.State is
                AgentState.Evacuating or AgentState.Injured or AgentState.Crawling;
            float influenceRadius = Math.Max(
                4f * _config.Radius,
                _config.BottleneckInfluenceRadius);
            if (!eligibleState || !agent.IsActive || distanceToExit > influenceRadius)
            {
                RelaxBottleneckInteraction(agent);
                return;
            }

            int capacity = ExitCapacity(exit);
            int occupancy = DoorwayOccupancy(simulation, exit, agent.AgentId);
            float doorwayLoad = Math.Clamp(occupancy / (float)Math.Max(1, capacity), 0f, 1f);

            float densityRadius = Math.Max(4f * _config.Radius, 0.55f * influenceRadius);
            simulation.CollectNearbyAgents(agent, densityRadius, _nearbyScratch);
            int nearbyCount = 0;
            foreach (Agent other in _nearbyScratch)
            {
                if (other.IsActive && other.State != AgentState.Exited)
                {
                    nearbyCount++;
                }
            }
            float density = Math.Clamp(
                nearbyCount / Math.Max(_config.BottleneckDensityReference, 1f),
                0f,
                1f);
            float proximity = Math.Clamp(1f - distanceToExit / influenceRadius, 0f, 1f);

            float desiredSpeed = Math.Max(agent.CurrentDesiredSpeed, 0.004f);
            float speedRatio = Math.Clamp(agent.Velocity.Length() / desiredSpeed, 0f, 1f);
            float blockedProgress = 1f - speedRatio;
            bool congested =
                doorwayLoad >= 0.50f ||
                density >= _config.BottleneckMinimumCongestion ||
                agent.ExitCongestionEpisodeActive;

            if (!congested)
            {
                RelaxBottleneckInteraction(agent);
                return;
            }

            if (blockedProgress >= 0.55f && proximity > 0.05f)
            {
                agent.BottleneckWaitSeconds += _config.Dt;
            }
            else
            {
                agent.BottleneckWaitSeconds = Math.Max(
                    0f,
                    agent.BottleneckWaitSeconds - 0.75f * _config.Dt);
            }

            float patience = Math.Clamp(agent.PatienceBias, 0.65f, 1.35f);
            float waitReference = Math.Max(
                _config.Dt,
                _config.BottleneckWaitReferenceSeconds * patience);
            float waitSignal = Math.Clamp(agent.BottleneckWaitSeconds / waitReference, 0f, 1f);
            float assertiveness = Math.Clamp(agent.Assertiveness, 0f, 1f);
            float urgencySignal = Math.Clamp(
                0.50f * Math.Clamp(agent.PanicLevel, 0f, 1f) +
                0.50f * Math.Clamp((agent.UrgencyBias - 0.75f) / 0.50f, 0f, 1f),
                0f,
                1f);

            float target =
                0.27f * density +
                0.22f * waitSignal +
                0.18f * doorwayLoad +
                0.12f * proximity +
                0.11f * assertiveness +
                0.06f * urgencySignal +
                0.04f * blockedProgress;
            if (agent.ExitCongestionEpisodeActive)
            {
                target += 0.08f;
            }

            target *= _config.Behavior.BottleneckPressureSensitivity;
            target = Math.Clamp(target, 0f, 1f);
            agent.BottleneckPressureTarget = target;

            float timeConstant = target > agent.BottleneckPressure
                ? _config.BottleneckPressureRiseSeconds
                : _config.BottleneckPressureDecaySeconds;
            float alpha = 1f - MathF.Exp(
                -_config.Dt / Math.Max(_config.Dt, timeConstant));
            agent.BottleneckPressure = Math.Clamp(
                agent.BottleneckPressure + alpha * (target - agent.BottleneckPressure),
                0f,
                1f);

            SelectBottleneckMode(
                simulation,
                agent,
                exit,
                waitSignal,
                assertiveness,
                patience);
        }

        private void RelaxBottleneckInteraction(Agent agent)
        {
            agent.BottleneckWaitSeconds = Math.Max(
                0f,
                agent.BottleneckWaitSeconds - 1.25f * _config.Dt);
            agent.BottleneckPressureTarget = 0f;
            float decay = MathF.Exp(
                -_config.Dt / Math.Max(_config.Dt, _config.BottleneckPressureDecaySeconds));
            agent.BottleneckPressure = Math.Clamp(agent.BottleneckPressure * decay, 0f, 1f);

            // Doorway behaviour is only meaningful while a real bottleneck is present. Drop
            // the committed response immediately when congestion clears while retaining a short
            // pressure decay for diagnostics.
            agent.BottleneckMode = BottleneckResponseMode.Free;
            agent.BottleneckModeUntilFrame = -1;
            agent.BottleneckLateralDirection = Vector2.Zero;
            if (agent.BottleneckPressure <= 0.05f)
            {
                agent.BottleneckPressure = 0f;
            }
        }

        private void SelectBottleneckMode(
            Simulation simulation,
            Agent agent,
            Exit exit,
            float waitSignal,
            float assertiveness,
            float patience)
        {
            bool forceYield = agent.State == AgentState.Crawling;
            if (!forceYield &&
                simulation.FrameIndex < agent.BottleneckModeUntilFrame &&
                agent.BottleneckMode != BottleneckResponseMode.Free)
            {
                return;
            }

            BottleneckResponseMode desiredMode;
            if (forceYield)
            {
                desiredMode = BottleneckResponseMode.Yield;
            }
            else
            {
                float impatience = Math.Clamp((1.35f - patience) / 0.70f, 0f, 1f);
                float pressEligibility =
                    0.60f * assertiveness +
                    0.25f * impatience +
                    0.15f * Math.Clamp(agent.PanicLevel, 0f, 1f) +
                    0.15f * waitSignal;
                bool keepPressing =
                    agent.BottleneckMode == BottleneckResponseMode.Press &&
                    agent.BottleneckPressure >= _config.BottleneckPressReleaseThreshold;
                bool canPress =
                    (agent.BottleneckPressure >= _config.BottleneckPressThreshold || keepPressing) &&
                    pressEligibility >= 0.55f;

                if (canPress)
                {
                    desiredMode = BottleneckResponseMode.Press;
                }
                else if (agent.BottleneckPressure >= _config.BottleneckSeekGapThreshold)
                {
                    desiredMode = BottleneckResponseMode.SeekGap;
                }
                else
                {
                    desiredMode = BottleneckResponseMode.Yield;
                }
            }

            int commitFrames = Math.Max(
                1,
                (int)MathF.Ceiling(
                    _config.BottleneckModeCommitSeconds / Math.Max(_config.Dt, 1e-5f)));
            bool modeChanged = desiredMode != agent.BottleneckMode;
            agent.BottleneckMode = desiredMode;
            agent.BottleneckModeUntilFrame = simulation.FrameIndex + commitFrames;

            if (desiredMode == BottleneckResponseMode.SeekGap)
            {
                agent.BottleneckLateralDirection = ChooseBottleneckGapDirection(
                    simulation,
                    agent,
                    exit);
            }
            else
            {
                agent.BottleneckLateralDirection = Vector2.Zero;
            }

            if (!agent.ExitCongestionEpisodeActive)
            {
                return;
            }

            if (desiredMode == BottleneckResponseMode.Yield || agent.State == AgentState.Crawling)
            {
                agent.ExitYielding = true;
                if (modeChanged || agent.ExitRetryAfterFrame <= simulation.FrameIndex)
                {
                    agent.ExitRetryAfterFrame = NextExitRetryFrame(simulation, agent);
                }
            }
            else
            {
                agent.ExitYielding = false;
                agent.ExitRetryAfterFrame = simulation.FrameIndex;
            }
        }

        private Vector2 ChooseBottleneckGapDirection(
            Simulation simulation,
            Agent agent,
            Exit exit)
        {
            Vector2 tangent = exit.Side is "N" or "S"
                ? Vector2.UnitX
                : Vector2.UnitY;
            Vector2 forward = VectorMath.SafeNormalize(exit.Position - agent.Position);
            float probeDistance = Math.Max(
                2f * _config.Radius,
                _config.BottleneckGapProbeDistanceFactor * _config.Radius);

            float negativeScore = ScoreBottleneckGapCandidate(
                simulation,
                agent,
                tangent,
                forward,
                -1f,
                probeDistance);
            float positiveScore = ScoreBottleneckGapCandidate(
                simulation,
                agent,
                tangent,
                forward,
                1f,
                probeDistance);

            if (!float.IsFinite(negativeScore) && !float.IsFinite(positiveScore))
            {
                return Vector2.Zero;
            }

            if (MathF.Abs(positiveScore - negativeScore) < 0.01f)
            {
                return (agent.AgentId & 1) == 0 ? tangent : -tangent;
            }

            return positiveScore > negativeScore ? tangent : -tangent;
        }

        private float ScoreBottleneckGapCandidate(
            Simulation simulation,
            Agent agent,
            Vector2 tangent,
            Vector2 forward,
            float sign,
            float probeDistance)
        {
            Vector2 direction = tangent * sign;
            Vector2 candidate =
                agent.Position +
                direction * probeDistance +
                forward * (0.20f * probeDistance);
            float margin = NavigationClearance();
            if (candidate.X <= margin ||
                candidate.Y <= margin ||
                candidate.X >= simulation.Environment.Width - margin ||
                candidate.Y >= simulation.Environment.Height - margin ||
                simulation.Environment.IsPointBlocked(candidate, margin) ||
                !simulation.HasTraversablePath(
                    agent.Position,
                    candidate,
                    margin))
            {
                return float.NegativeInfinity;
            }

            simulation.CollectNearbyAgents(agent, 2.5f * probeDistance, _nearbyScratch);
            int crowdAtCandidate = 0;
            float nearestDistance = 4f * _config.Radius;
            bool foundNearby = false;
            foreach (Agent other in _nearbyScratch)
            {
                if (!other.IsActive)
                {
                    continue;
                }

                float candidateDistance = Vector2.Distance(other.Position, candidate);
                if (candidateDistance <= 2.5f * _config.Radius)
                {
                    crowdAtCandidate++;
                }

                if (!foundNearby || candidateDistance < nearestDistance)
                {
                    nearestDistance = candidateDistance;
                    foundNearby = true;
                }
            }
            float clearanceScore = Math.Clamp(
                nearestDistance / Math.Max(3f * _config.Radius, 0.04f),
                0f,
                1f);
            float currentExitDistance = agent.ExitTarget is null
                ? 0f
                : Vector2.Distance(agent.Position, agent.ExitTarget.Position);
            float candidateExitDistance = agent.ExitTarget is null
                ? currentExitDistance
                : Vector2.Distance(candidate, agent.ExitTarget.Position);
            float progress = Math.Clamp(
                (currentExitDistance - candidateExitDistance) / Math.Max(probeDistance, 0.04f),
                -1f,
                1f);

            return
                1.10f * clearanceScore -
                0.55f * crowdAtCandidate +
                0.25f * progress;
        }

        private Vector2 ApplyBottleneckDirection(
            Simulation simulation,
            Agent agent,
            Vector2 goalDirection)
        {
            if (agent.BottleneckMode != BottleneckResponseMode.SeekGap ||
                agent.BottleneckLateralDirection.LengthSquared() <=
                    VectorMath.Epsilon * VectorMath.Epsilon)
            {
                return goalDirection;
            }

            float pressure = Math.Clamp(agent.BottleneckPressure, 0f, 1f);
            float blend = _config.BottleneckGapBlendMin +
                (_config.BottleneckGapBlendMax - _config.BottleneckGapBlendMin) * pressure;
            blend = Math.Clamp(blend, 0f, 0.50f);
            Vector2 candidateDirection = VectorMath.SafeNormalize(
                (1f - blend) * goalDirection +
                blend * agent.BottleneckLateralDirection,
                goalDirection);
            float probeDistance = Math.Max(2.5f * _config.Radius, 0.73f);
            Vector2 probePoint = agent.Position + candidateDirection * probeDistance;
            return simulation.HasTraversablePath(
                agent.Position,
                probePoint,
                NavigationClearance())
                    ? candidateDirection
                    : goalDirection;
        }

        public Vector2 ComputeHerdDirection(Simulation simulation, Agent agent)
        {
            Vector2 accumulator = Vector2.Zero;
            float weightTotal = 0f;
            float perceptionRadius = _config.NeighbourRadius *
                (0.55f + 0.45f * Math.Clamp(agent.VisibilityFactor, 0f, 1f));

            simulation.CollectNearbyAgents(agent, perceptionRadius, _nearbyScratch);
            foreach (Agent other in _nearbyScratch)
            {
                if (!other.IsActive || other.State is AgentState.Dead or AgentState.Incapacitated or
                    AgentState.TempIncap or AgentState.Crawling)
                {
                    continue;
                }

                if (other.WallFollowDir is not null && other.WallFollowUntil > simulation.FrameIndex)
                {
                    continue;
                }

                if (other.TimeStuck > 2f ||
                    !simulation.HasLineOfSight(agent.Position, other.Position))
                {
                    continue;
                }

                float distance = Vector2.Distance(other.Position, agent.Position);
                float speed = other.Velocity.Length();
                if (distance > perceptionRadius || speed <= VectorMath.Epsilon)
                {
                    continue;
                }

                float distanceWeight = MathF.Exp(-distance / Math.Max(_config.NeighbourRadius, 1e-5f));
                float stateWeight = other.CanSignalPanic ? 1f : 0.35f;
                float weight = distanceWeight * stateWeight;
                accumulator += other.Velocity / speed * weight;
                weightTotal += weight;
            }

            if (weightTotal <= 1e-5f)
            {
                return Vector2.Zero;
            }

            return VectorMath.SafeNormalize(accumulator / weightTotal);
        }

        /// <summary>
        /// Direction of visible purposeful evacuation movement. Unlike general SFM herding, this
        /// excludes ordinary wandering pedestrians so a temporary follow-crowd intent cannot lock
        /// onto unrelated pre-event motion.
        /// </summary>
        private Vector2 ComputeEvacuationFlowDirection(Simulation simulation, Agent agent)
        {
            Vector2 accumulator = Vector2.Zero;
            float weightTotal = 0f;
            float radius = Math.Max(_config.NeighbourRadius, _config.EvacuationAwarenessRadius) *
                (0.55f + 0.45f * Math.Clamp(agent.VisibilityFactor, 0f, 1f));

            simulation.CollectNearbyAgents(agent, radius, _nearbyScratch);
            foreach (Agent other in _nearbyScratch)
            {
                if (!other.IsActive ||
                    other.State is not (AgentState.Evacuating or AgentState.Injured) ||
                    !simulation.HasLineOfSight(agent.Position, other.Position))
                {
                    continue;
                }

                float speed = other.Velocity.Length();
                if (speed <= Math.Max(0.008f, 0.20f * other.BaseSpeed))
                {
                    continue;
                }

                float distance = Vector2.Distance(agent.Position, other.Position);
                if (distance > radius)
                {
                    continue;
                }

                float weight = MathF.Exp(-distance / Math.Max(_config.NeighbourRadius, 1e-5f));
                accumulator += VectorMath.SafeNormalize(other.Velocity) * weight;
                weightTotal += weight;
            }

            return weightTotal > 1e-5f
                ? VectorMath.SafeNormalize(accumulator / weightTotal)
                : Vector2.Zero;
        }

        public static bool IsInsideExitZone(Vector2 position, Exit exit)
            => MathF.Abs(position.X - exit.Position.X) <= exit.Size &&
               MathF.Abs(position.Y - exit.Position.Y) <= exit.Size;

        public int ExitPeopleWidth(Exit exit)
        {
            float doorWidthMeters = 2f * exit.Size;
            float personWidthMeters = 2f * _config.Radius;
            return Math.Max(1, (int)MathF.Floor(doorWidthMeters / personWidthMeters + 1e-6f));
        }

        public int DoorwayOccupancy(Simulation simulation, Exit exit, int excludeId)
        {
            float queryRadius = exit.Size * 1.414214f + 0.01f;
            simulation.CollectAgentsNearPoint(exit.Position, queryRadius, _exitQueryScratch, excludeId);
            int count = 0;
            foreach (Agent agent in _exitQueryScratch)
            {
                if (agent.IsActive && agent.State != AgentState.Exited && IsInsideExitZone(agent.Position, exit))
                {
                    count++;
                }
            }

            return count;
        }

        private int ExitCapacity(Exit exit)
            => ExitPeopleWidth(exit);

        private int DoorwayAdmissionOccupancy(Simulation simulation, Exit exit, int excludeId)
        {
            float queryRadius = exit.Size * 1.414214f + 0.01f;
            simulation.CollectAgentsNearPoint(exit.Position, queryRadius, _exitQueryScratch, excludeId);
            int count = 0;
            foreach (Agent agent in _exitQueryScratch)
            {
                if (agent.IsActive &&
                    agent.State != AgentState.Exited &&
                    IsInsideExitZone(agent.Position, exit) &&
                    (!agent.ExitYielding || simulation.FrameIndex >= agent.ExitRetryAfterFrame))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsExitPusher(Agent agent)
            => agent.State is (AgentState.Evacuating or AgentState.Injured) &&
               (agent.BottleneckMode == BottleneckResponseMode.Press ||
                Math.Clamp(agent.PanicLevel, 0f, 1f) >= _config.ExitPushPanicThreshold);

        private static bool IsExitGapSeeker(Agent agent)
            => agent.State is (AgentState.Evacuating or AgentState.Injured) &&
               agent.BottleneckMode == BottleneckResponseMode.SeekGap;

        private int NextExitRetryFrame(Simulation simulation, Agent agent)
        {
            float threshold = Math.Max(_config.ExitPushPanicThreshold, 0.01f);
            float panicFraction = Math.Clamp(agent.PanicLevel / threshold, 0f, 1f);
            float urgencyFraction = Math.Max(
                panicFraction,
                Math.Clamp(agent.BottleneckPressure, 0f, 1f));
            float baseSeconds = _config.ExitYieldMaximumSeconds -
                (_config.ExitYieldMaximumSeconds - _config.ExitYieldMinimumSeconds) * urgencyFraction;
            float patienceScale = Math.Clamp(agent.PatienceBias, 0.65f, 1.35f);
            float assertivenessScale = 1.10f - 0.35f * Math.Clamp(agent.Assertiveness, 0f, 1f);
            float jitter = (float)simulation.Random.NextDouble() * Math.Max(0f, _config.ExitRetryJitterSeconds);
            float seconds = Math.Max(
                _config.Dt,
                baseSeconds * patienceScale * assertivenessScale + jitter);
            return simulation.FrameIndex + Math.Max(1, (int)MathF.Ceiling(seconds / _config.Dt));
        }

        private void SyncExitCongestionEpisode(Agent agent, Exit exit, float distanceToExit)
        {
            if (!agent.ExitCongestionEpisodeActive)
            {
                return;
            }

            bool changedExit = !string.Equals(agent.ExitCongestionExitName, exit.Name, StringComparison.Ordinal);
            bool leftDoorwayArea = distanceToExit > 1.35f * SnapRadius;
            if (changedExit || leftDoorwayArea)
            {
                agent.ResetExitCongestion();
            }
        }

        private bool ShouldYieldAtCongestedExit(Simulation simulation, Agent agent, Exit exit)
        {
            if (!agent.ExitCongestionEpisodeActive ||
                !string.Equals(agent.ExitCongestionExitName, exit.Name, StringComparison.Ordinal))
            {
                return false;
            }

            // Panic can rise while an agent is waiting. Crossing the threshold cancels the
            // polite yield immediately, but does not bypass exit capacity; the agent simply
            // resumes normal panic-SFM pressure toward the doorway. Crawlers always yield.
            if (agent.State != AgentState.Crawling &&
                (IsExitPusher(agent) || IsExitGapSeeker(agent)))
            {
                agent.ExitYielding = false;
                agent.ExitRetryAfterFrame = simulation.FrameIndex;
                return false;
            }

            if (!agent.ExitYielding)
            {
                return false;
            }

            if (simulation.FrameIndex < agent.ExitRetryAfterFrame)
            {
                return true;
            }

            int occupancy = DoorwayAdmissionOccupancy(simulation, exit, agent.AgentId);
            if (occupancy >= ExitCapacity(exit))
            {
                agent.ExitRetryAfterFrame = NextExitRetryFrame(simulation, agent);
                return true;
            }

            agent.ExitYielding = false;
            return false;
        }

        private static Vector2 ExitInwardNormal(Exit exit)
            => exit.Side switch
            {
                "N" => Vector2.UnitY,
                "S" => -Vector2.UnitY,
                "W" => Vector2.UnitX,
                _ => -Vector2.UnitX,
            };

        private void ConstrainRejectedAgentToDoorwayThreshold(Simulation simulation, Agent agent, Exit exit)
        {
            float inset = Math.Max(0.04f, 0.25f * _config.Radius);
            float thickness = simulation.Environment.BoundaryThickness;
            Vector2 position = agent.Position;
            float defaultHoldingCoordinate = exit.Side switch
            {
                "N" => thickness + inset,
                "S" => simulation.Environment.Height - thickness - inset,
                "W" => thickness + inset,
                _ => simulation.Environment.Width - thickness - inset,
            };
            float holdingCoordinate = float.IsFinite(agent.ExitCongestionHoldingCoordinate)
                ? agent.ExitCongestionHoldingCoordinate
                : defaultHoldingCoordinate;

            switch (exit.Side)
            {
                case "N":
                    position.Y = Math.Max(position.Y, holdingCoordinate);
                    break;
                case "S":
                    position.Y = Math.Min(position.Y, holdingCoordinate);
                    break;
                case "W":
                    position.X = Math.Max(position.X, holdingCoordinate);
                    break;
                default:
                    position.X = Math.Min(position.X, holdingCoordinate);
                    break;
            }

            agent.Position = VectorMath.ClampToBounds(
                position,
                simulation.Config.World.WidthMeters,
                simulation.Config.World.LengthMeters);

            // Remove only outward motion. Tangential velocity is preserved so a high-panic
            // pedestrian can still make small lateral adjustments without visibly bouncing.
            Vector2 inward = ExitInwardNormal(exit);
            float inwardVelocity = Vector2.Dot(agent.Velocity, inward);
            if (inwardVelocity < 0f)
            {
                agent.Velocity -= inwardVelocity * inward;
            }
        }

        private void RegisterExitRejection(Simulation simulation, Agent agent, Exit exit)
        {
            bool newEpisode = !agent.ExitCongestionEpisodeActive ||
                !string.Equals(agent.ExitCongestionExitName, exit.Name, StringComparison.Ordinal);

            if (newEpisode)
            {
                agent.ExitCongestionEpisodeActive = true;
                agent.ExitCongestionExitName = exit.Name;

                // One physical setback per congestion episode. Subsequent failed attempts are
                // held at the doorway threshold rather than repeatedly kicked backward.
                Vector2 inward = ExitInwardNormal(exit);
                float setbackRange = Math.Max(0f, _config.ExitInitialSetbackMax - _config.ExitInitialSetbackMin);
                float setback = _config.ExitInitialSetbackMin +
                    (float)simulation.Random.NextDouble() * setbackRange;
                agent.Position += inward * setback;

                float inset = Math.Max(0.04f, 0.25f * _config.Radius);
                float thickness = simulation.Environment.BoundaryThickness;
                agent.ExitCongestionHoldingCoordinate = exit.Side switch
                {
                    "N" => Math.Max(agent.Position.Y, thickness + inset),
                    "S" => Math.Min(agent.Position.Y, simulation.Environment.Height - thickness - inset),
                    "W" => Math.Max(agent.Position.X, thickness + inset),
                    _ => Math.Min(agent.Position.X, simulation.Environment.Width - thickness - inset),
                };
            }

            ConstrainRejectedAgentToDoorwayThreshold(simulation, agent, exit);

            if (agent.State != AgentState.Crawling &&
                (IsExitPusher(agent) || IsExitGapSeeker(agent)))
            {
                agent.ExitYielding = false;
                agent.ExitRetryAfterFrame = simulation.FrameIndex;
            }
            else
            {
                agent.ExitYielding = true;
                agent.ExitRetryAfterFrame = NextExitRetryFrame(simulation, agent);
                agent.Velocity *= 0.20f;
            }
        }

        public int LocalDensity(Simulation simulation, Exit exit, float radius = 2.02f)
        {
            simulation.CollectAgentsNearPoint(exit.Position, radius, _exitQueryScratch);
            int count = 0;
            foreach (Agent agent in _exitQueryScratch)
            {
                if (agent.IsActive && agent.State != AgentState.Exited)
                {
                    count++;
                }
            }

            return count;
        }

        public bool ShouldCommitToExit(Simulation simulation, Agent agent, Exit exit, float distanceToExit)
        {
            if (distanceToExit > SnapRadius || agent.State is not (AgentState.Evacuating or AgentState.Injured))
            {
                return false;
            }

            if (!simulation.HasTraversablePath(
                agent.Position,
                exit.Position,
                NavigationClearance()))
            {
                return false;
            }

            int peopleWide = ExitPeopleWidth(exit);
            float panic = Math.Clamp(agent.PanicLevel, 0f, 1f);
            int density = LocalDensity(simulation, exit);
            int occupancy = DoorwayOccupancy(simulation, exit, agent.AgentId);

            float probability;
            if (peopleWide <= 1)
            {
                float panicProbability = 0.05f + 0.60f * panic;
                float densityProbability = Math.Min(0.35f, 0.07f * density);
                probability = panicProbability + densityProbability;
                if (occupancy > 0)
                {
                    probability *= 0.25f;
                }

                if (distanceToExit < 0.5f * SnapRadius)
                {
                    probability *= 1.15f;
                }

                probability = Math.Clamp(probability, 0f, 1f);
            }
            else
            {
                probability = 0.02f + 0.12f * panic + Math.Min(0.10f, 0.02f * density);
                if (occupancy > 0)
                {
                    probability *= 0.60f;
                }

                if (distanceToExit < 0.5f * SnapRadius)
                {
                    probability *= 1.10f;
                }

                probability = Math.Clamp(probability, 0f, 0.25f);
            }

            return simulation.Random.NextDouble() < probability;
        }

        public void MaybeReselectExit(Simulation simulation, Agent agent)
        {
            if (agent.ExitTarget is null || simulation.Environment.Exits.Count < 2)
            {
                return;
            }

            if (UsesNaturalNavigation(simulation) && IsNaturalMover(agent))
            {
                // Physical blockage is handled by the recovery supervisor. An evolving plume
                // can invalidate an exit intention even while the pedestrian is making progress.
                MaybeReselectNaturalToxicExit(simulation, agent);
                return;
            }

            int intervalFrames = Math.Max(1, (int)(0.6f / _config.Dt));
            if (simulation.FrameIndex - agent.LastExitReselectFrame < intervalFrames)
            {
                return;
            }

            agent.LastExitReselectFrame = simulation.FrameIndex;
            float currentDistance = Vector2.Distance(agent.Position, agent.ExitTarget.Position);
            bool currentVisible = simulation.HasLineOfSight(agent.Position, agent.ExitTarget.Position);

            // Near a usable doorway, preserve commitment unless geometry truly makes it inaccessible.
            if (currentDistance < 0.45f * SnapRadius && currentVisible)
            {
                return;
            }

            Exit? bestExit = null;
            float bestScore = float.PositiveInfinity;
            bool bestVisible = false;
            foreach (Exit exit in simulation.Environment.Exits)
            {
                float score = ScoreExit(simulation, agent, exit, out bool visible);
                if (bestExit is null || score < bestScore)
                {
                    bestExit = exit;
                    bestScore = score;
                    bestVisible = visible;
                }
            }

            if (bestExit is null || ReferenceEquals(bestExit, agent.ExitTarget))
            {
                return;
            }

            float currentScore = ScoreExit(simulation, agent, agent.ExitTarget, out _);
            bool persistentBlockage = agent.TimeStuck >= 2f ||
                (agent.LocalNavigationBlockedSinceFrame >= 0 &&
                 (simulation.FrameIndex - agent.LocalNavigationBlockedSinceFrame) * _config.Dt >= 2f);
            bool commitmentExpired = simulation.FrameIndex >= agent.ExitCommitUntilFrame || persistentBlockage;
            bool materiallyBetter = bestScore <= currentScore * _config.ExitSwitchImprovementRatio;
            bool visibilityRecovery = !currentVisible && bestVisible && bestScore < currentScore;

            if (commitmentExpired && (materiallyBetter || visibilityRecovery))
            {
                agent.ResetExitCongestion();
                agent.ExitTarget = bestExit;
                agent.ExitCommitUntilFrame = simulation.FrameIndex +
                    Math.Max(1, (int)(_config.RouteCommitmentSeconds / _config.Dt));
                agent.Exploring = false;
                agent.TempGoal = null;
                agent.ExplorationReplanAfterFrame = 0;
                agent.ClearRoute();
                agent.LastGoalDistance = float.PositiveInfinity;
                agent.StuckProgressAnchorPosition = null;
                agent.StuckProgressAnchorExitDistance = float.PositiveInfinity;
                agent.StuckProgressAnchorFrame = -1;
            }
        }

        private float ScoreExit(Simulation simulation, Agent agent, Exit exit, out bool visible)
        {
            float distance = Vector2.Distance(agent.Position, exit.Position);
            int density = LocalDensity(simulation, exit, _config.ExitInfluenceRadius);
            int occupancy = DoorwayOccupancy(simulation, exit, agent.AgentId);
            simulation.CollectAgentsNearPoint(exit.Position, _config.ExitInfluenceRadius, _exitQueryScratch);
            int casualties = 0;
            foreach (Agent other in _exitQueryScratch)
            {
                if (other.IsActive && other.IsCasualtyObstacle)
                {
                    casualties++;
                }
            }

            visible = simulation.HasLineOfSight(agent.Position, exit.Position);
            float visibilityLoss = 1f - Math.Clamp(agent.VisibilityFactor, 0f, 1f);

            float familiarity = Math.Clamp(agent.RouteFamiliarity, 0f, 1f);
            float occlusionPenalty = visible
                ? 0f
                : _config.ExitOcclusionPenalty * (1.25f - 0.50f * familiarity);

            float toxicRoutePenalty = 0f;
            if (simulation.ScenarioType == ScenarioType.ToxicFume && simulation.ToxicFumeSystem.IsReleased)
            {
                // Cached concentration samples bias exit reselection away from contaminated paths.
                // The multiplier expresses route preference only; it is not a physical toxicity constant.
                toxicRoutePenalty = simulation.ToxicFumeSystem.EstimateSegmentExposure(
                    simulation, agent.Position, exit.Position) * Math.Max(6f, distance * 1.4f);
            }

            return distance +
                density * _config.ExitCongestionWeight +
                occupancy * (_config.ExitCongestionWeight * 1.5f) +
                casualties * _config.ExitCasualtyWeight +
                occlusionPenalty +
                toxicRoutePenalty +
                visibilityLoss * _config.ExitVisibilityPenalty *
                    Math.Clamp(distance / Math.Max(_config.ExitInfluenceRadius, 0.04f), 0.5f, 4f);
        }

        /// <summary>
        /// Select the first purposeful behaviour after the bounded orient/startle period.
        /// Directly exposed mobile survivors first create distance from the immediate event;
        /// indirectly alerted pedestrians may follow a coherent crowd or briefly attend a nearby
        /// casualty. All branches remain bounded and eventually transition to evacuation.
        /// </summary>
        public void BeginPurposefulResponse(Simulation simulation, Agent agent)
        {
            if (agent.State is AgentState.Exited or AgentState.Dead or AgentState.Incapacitated or
                AgentState.TempIncap or AgentState.Fallen)
            {
                return;
            }

            agent.ClearRoute();
            agent.AssistPauseUntilFrame = -1;
            // The old collision wall-follow patch was useful before static route planning, but it can
            // fight a valid A* waypoint and recreate corner oscillation. Clear any pre-event residue
            // when purposeful emergency movement begins; collision resolution remains active.
            agent.WallFollowDir = null;
            agent.WallFollowUntil = simulation.FrameIndex;
            agent.WallFollowNormal = null;
            agent.WallFollowTangent = null;

            bool directExplosionThreat = simulation.Environment.Explosion is not null &&
                agent.ShockwaveHit && !agent.BlastShielded;
            bool directToxicThreat = simulation.ScenarioType == ScenarioType.ToxicFume &&
                simulation.ToxicFumeSystem.IsReleased &&
                agent.CurrentToxicConcentration >= simulation.ToxicFumeSystem.Configuration.AwarenessConcentration;

            if (directExplosionThreat || directToxicThreat)
            {
                float sensitivity = Math.Clamp(agent.ThreatSensitivity, 0.8f, 1.2f);
                float t = (float)simulation.Random.NextDouble();
                float seconds = _config.ThreatEscapeMinSeconds +
                    t * Math.Max(0f, _config.ThreatEscapeMaxSeconds - _config.ThreatEscapeMinSeconds);
                seconds *= 0.9f + 0.2f * sensitivity;
                agent.Intent = BehaviorIntent.ThreatEscape;
                agent.IntentUntilFrame = simulation.FrameIndex + SecondsToFrames(seconds);
                return;
            }

            if (TryAssignAssistanceIntent(simulation, agent))
            {
                return;
            }

            bool socialCue = agent.LastEvacuationAwarenessStimulus > 0.05f ||
                agent.LastSocialPanicStimulus > 0.05f ||
                agent.LastRunningPanicStimulus > 0.05f;
            Vector2 evacuationFlow = ComputeEvacuationFlowDirection(simulation, agent);
            if (socialCue &&
                evacuationFlow.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon &&
                agent.FollowTendency >= _config.FollowCrowdMinimumTendency)
            {
                agent.Intent = BehaviorIntent.FollowCrowd;
                agent.IntentUntilFrame = simulation.FrameIndex +
                    SecondsToFrames(_config.FollowCrowdCommitSeconds);
                return;
            }

            agent.Intent = BehaviorIntent.Evacuate;
            agent.IntentUntilFrame = -1;
        }

        /// <summary>Expose the body-clear route planner for regression tests and diagnostics.</summary>
        public IReadOnlyList<Vector2> PlanBodyClearRoute(
            Simulation simulation,
            Vector2 start,
            Vector2 goal)
            => _routePlanner.Plan(simulation, start, goal);

        private int SecondsToFrames(float seconds)
            => Math.Max(1, (int)MathF.Ceiling(Math.Max(_config.Dt, seconds) / _config.Dt));

        private void UpdateBehaviorIntent(Simulation simulation, Agent agent)
        {
            if (agent.Intent == BehaviorIntent.ThreatEscape &&
                simulation.FrameIndex >= agent.IntentUntilFrame)
            {
                if (TryAssignAssistanceIntent(simulation, agent))
                {
                    return;
                }

                Vector2 herd = ComputeEvacuationFlowDirection(simulation, agent);
                if (agent.FollowTendency >= _config.FollowCrowdMinimumTendency &&
                    herd.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon)
                {
                    agent.Intent = BehaviorIntent.FollowCrowd;
                    agent.IntentUntilFrame = simulation.FrameIndex +
                        SecondsToFrames(_config.FollowCrowdCommitSeconds);
                }
                else
                {
                    agent.Intent = BehaviorIntent.Evacuate;
                    agent.IntentUntilFrame = -1;
                }

                agent.ClearRoute();
                return;
            }

            if (agent.Intent == BehaviorIntent.FollowCrowd &&
                simulation.FrameIndex >= agent.IntentUntilFrame)
            {
                if (!TryAssignAssistanceIntent(simulation, agent))
                {
                    agent.Intent = BehaviorIntent.Evacuate;
                    agent.IntentUntilFrame = -1;
                }

                agent.ClearRoute();
                return;
            }

            if (agent.Intent is BehaviorIntent.Unaware or BehaviorIntent.Orienting)
            {
                agent.Intent = BehaviorIntent.Evacuate;
            }
        }

        private bool TryAssignAssistanceIntent(Simulation simulation, Agent agent)
        {
            if (agent.AssistanceDecisionMade ||
                agent.HelpingTendency < _config.AssistanceMinimumTendency ||
                agent.PanicLevel > _config.AssistanceMaxPanic ||
                agent.State is AgentState.Injured or AgentState.Crawling)
            {
                return false;
            }

            agent.AssistanceDecisionMade = true;
            Agent? target = null;
            int selectedSlot = -1;
            float bestDistance = float.PositiveInfinity;

            foreach (Agent candidate in simulation.NearbyAgents(agent, _config.AssistancePerceptionRadius))
            {
                if (!candidate.IsActive ||
                    candidate.State is not (AgentState.Fallen or AgentState.TempIncap or AgentState.Incapacitated or AgentState.Crawling) ||
                    !simulation.HasLineOfSight(agent.Position, candidate.Position))
                {
                    continue;
                }

                if (!TryChooseAvailableAssistanceSlot(simulation, candidate, out int slotIndex))
                {
                    continue;
                }

                float distance = Vector2.Distance(agent.Position, candidate.Position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                target = candidate;
                selectedSlot = slotIndex;
            }

            if (target is null || selectedSlot < 0)
            {
                return false;
            }

            agent.Intent = BehaviorIntent.AssistCasualty;
            agent.AssistTargetAgentId = target.AgentId;
            agent.AssistSlotIndex = selectedSlot;
            agent.AssistRecoveryStage = 0;
            agent.AssistPauseUntilFrame = -1;
            agent.AssistAssignedFrame = simulation.FrameIndex;
            agent.AssistLastProgressFrame = simulation.FrameIndex;
            agent.AssistProgressAnchorPosition = agent.Position;
            Vector2 approachPoint = AssistanceApproachPoint(simulation, target, selectedSlot);
            agent.AssistBestDistanceMeters = Vector2.Distance(agent.Position, approachPoint);
            agent.IntentUntilFrame = -1;
            agent.ClearRoute();
            return true;
        }

        private bool TryChooseAvailableAssistanceSlot(
            Simulation simulation,
            Agent casualty,
            out int slotIndex)
        {
            slotIndex = -1;
            bool slot0Used = false;
            bool slot1Used = false;
            int assignedHelpers = 0;
            foreach (Agent helper in simulation.Agents)
            {
                if (!helper.IsActive || helper.Intent != BehaviorIntent.AssistCasualty ||
                    helper.AssistTargetAgentId != casualty.AgentId)
                {
                    continue;
                }

                assignedHelpers++;
                if (helper.AssistSlotIndex == 0)
                {
                    slot0Used = true;
                }
                else if (helper.AssistSlotIndex == 1)
                {
                    slot1Used = true;
                }
            }

            if (assignedHelpers >= Math.Min(2, _config.AssistanceMaxHelpersPerCasualty))
            {
                return false;
            }

            if (!slot0Used && IsAssistanceSlotUsable(simulation, casualty, 0))
            {
                slotIndex = 0;
                return true;
            }

            if (!slot1Used && IsAssistanceSlotUsable(simulation, casualty, 1))
            {
                slotIndex = 1;
                return true;
            }

            return false;
        }

        private bool IsAssistanceSlotUsable(Simulation simulation, Agent casualty, int slotIndex)
        {
            Vector2 point = AssistanceApproachPoint(simulation, casualty, slotIndex);
            float clearance = NavigationClearance();
            return VectorMath.IsFinite(point) &&
                   !simulation.Environment.IsPointBlocked(point, clearance);
        }

        private Vector2 AssistanceApproachPoint(Simulation simulation, Agent casualty, int slotIndex)
        {
            Vector2 forward = Vector2.Zero;
            if (casualty.ExitTarget is not null)
            {
                forward = VectorMath.SafeNormalize(casualty.ExitTarget.Position - casualty.Position);
            }

            if (forward.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon &&
                simulation.Environment.Exits.Count > 0)
            {
                Exit nearest = simulation.Environment.Exits
                    .OrderBy(exit => Vector2.DistanceSquared(casualty.Position, exit.Position))
                    .First();
                forward = VectorMath.SafeNormalize(nearest.Position - casualty.Position);
            }

            if (forward.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon)
            {
                float angle = 0.73f * (casualty.AgentId + 1);
                forward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            Vector2 side = VectorMath.SafeNormalize(VectorMath.Perpendicular(forward), Vector2.UnitY);
            float sign = slotIndex == 1 ? -1f : 1f;
            float distance = Math.Max(
                2.30f * _config.Radius,
                _config.AssistanceSlotDistanceFactor * _config.Radius);
            return casualty.Position + side * (sign * distance);
        }

        private bool TrySwitchAssistanceSlot(Simulation simulation, Agent agent, Agent casualty)
        {
            int alternate = agent.AssistSlotIndex == 0 ? 1 : 0;
            if (alternate < 0 || !IsAssistanceSlotUsable(simulation, casualty, alternate))
            {
                return false;
            }

            foreach (Agent helper in simulation.Agents)
            {
                if (helper.AgentId == agent.AgentId || !helper.IsActive ||
                    helper.Intent != BehaviorIntent.AssistCasualty ||
                    helper.AssistTargetAgentId != casualty.AgentId)
                {
                    continue;
                }

                if (helper.AssistSlotIndex == alternate)
                {
                    return false;
                }
            }

            agent.AssistSlotIndex = alternate;
            agent.ClearRoute();
            return true;
        }

        private Vector2 ComputeThreatEscapeDirection(Simulation simulation, Agent agent)
        {
            if (simulation.ScenarioType == ScenarioType.ToxicFume &&
                simulation.ToxicFumeSystem.IsReleased)
            {
                Vector2 safer = simulation.ToxicFumeSystem.GetSaferDirection(simulation, agent.Position);
                if (safer.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon)
                {
                    return safer;
                }
            }

            Explosion? explosion = simulation.Environment.Explosion;
            if (explosion is null)
            {
                return Vector2.Zero;
            }

            Vector2 away = VectorMath.SafeNormalize(agent.Position - explosion.Position);
            if (away.LengthSquared() < VectorMath.Epsilon * VectorMath.Epsilon)
            {
                float angle = 0.73f * (agent.AgentId + 1);
                away = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            float probe = Math.Max(6f * _config.Radius, 1.46f);
            float clearance = NavigationClearance();
            Vector2 best = Vector2.Zero;
            float bestScore = float.NegativeInfinity;
            const int samples = 16;

            for (int i = 0; i < samples; i++)
            {
                // Start around the away vector and progressively test wider deviations. The
                // stable id phase prevents a perfectly synchronized split around obstacles.
                float phase = ((i + (agent.AgentId % 3)) % samples) * (2f * MathF.PI / samples);
                Vector2 direction = Rotate(away, phase);
                Vector2 candidate = agent.Position + direction * probe;
                float inner = simulation.Environment.BoundaryThickness + clearance + 0.04f;
                if (candidate.X <= inner || candidate.Y <= inner ||
                    candidate.X >= simulation.Environment.Width - inner ||
                    candidate.Y >= simulation.Environment.Height - inner)
                {
                    continue;
                }

                if (!simulation.HasTraversablePath(
                    agent.Position,
                    candidate,
                    clearance))
                {
                    continue;
                }

                float awayAlignment = Vector2.Dot(direction, away);
                float openSpace = Math.Clamp(
                    EndpointClearance(simulation, candidate) / Math.Max(4f * _config.Radius, 0.04f),
                    0f,
                    1f);
                float score = 2.2f * awayAlignment + 0.8f * openSpace;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }

            return best;
        }

        private Vector2 ComputeAssistanceDirection(
            Simulation simulation,
            Agent agent,
            out bool holdPosition)
        {
            holdPosition = false;
            if (agent.AssistTargetAgentId is not int targetId)
            {
                agent.Intent = BehaviorIntent.Evacuate;
                ClearAssistanceDiagnostics(agent);
                return Vector2.Zero;
            }

            Agent? target = simulation.Agents.FirstOrDefault(candidate => candidate.AgentId == targetId);
            if (target is null || !target.IsActive ||
                target.State is not (AgentState.Fallen or AgentState.TempIncap or AgentState.Incapacitated or AgentState.Crawling))
            {
                agent.AssistTargetAgentId = null;
                agent.Intent = BehaviorIntent.Evacuate;
                ClearAssistanceDiagnostics(agent);
                agent.ClearRoute();
                return Vector2.Zero;
            }

            if (agent.AssistSlotIndex is < 0 or > 1)
            {
                if (!TryChooseAvailableAssistanceSlot(simulation, target, out int recoveredSlot))
                {
                    AbandonAssistance(agent);
                    return Vector2.Zero;
                }

                agent.AssistSlotIndex = recoveredSlot;
            }

            float reach = Math.Max(
                2f * _config.Radius,
                _config.AssistanceReachRadiusFactor * _config.Radius);
            float arrivalTolerance = Math.Max(
                0.10f,
                _config.AssistanceSlotArrivalToleranceFactor * _config.Radius);
            Vector2 approachPoint = AssistanceApproachPoint(simulation, target, agent.AssistSlotIndex);
            float casualtyDistance = Vector2.Distance(agent.Position, target.Position);
            float approachDistance = Vector2.Distance(agent.Position, approachPoint);

            UpdateAssistanceProgress(simulation, agent, approachDistance);

            if (approachDistance <= arrivalTolerance && casualtyDistance <= reach)
            {
                if (agent.AssistPauseUntilFrame < 0)
                {
                    agent.AssistPauseUntilFrame = simulation.FrameIndex +
                        SecondsToFrames(_config.AssistancePauseSeconds);
                    agent.AssistLastProgressFrame = simulation.FrameIndex;
                }

                if (simulation.FrameIndex < agent.AssistPauseUntilFrame)
                {
                    holdPosition = true;
                    return Vector2.Zero;
                }

                agent.AssistTargetAgentId = null;
                agent.Intent = BehaviorIntent.Evacuate;
                agent.IntentUntilFrame = -1;
                ClearAssistanceDiagnostics(agent);
                agent.ClearRoute();
                return Vector2.Zero;
            }

            float noProgressSeconds = agent.AssistLastProgressFrame >= 0
                ? Math.Max(0, simulation.FrameIndex - agent.AssistLastProgressFrame) * _config.Dt
                : 0f;

            if (noProgressSeconds >= _config.AssistanceAbandonNoProgressSeconds)
            {
                AbandonAssistance(agent);
                return Vector2.Zero;
            }

            if (noProgressSeconds >= _config.AssistanceAlternateSideNoProgressSeconds &&
                agent.AssistRecoveryStage < 2)
            {
                bool switched = TrySwitchAssistanceSlot(simulation, agent, target);
                agent.AssistRecoveryStage = 2;
                approachPoint = AssistanceApproachPoint(simulation, target, agent.AssistSlotIndex);

                if (switched)
                {
                    // The goal point changed, so establish a fresh distance baseline for the
                    // new side. Deliberately do NOT reset AssistLastProgressFrame here: changing
                    // strategy is not physical progress, and the 10-second no-progress guard
                    // must remain bounded unless the helper actually gets closer afterwards.
                    agent.AssistBestDistanceMeters = Vector2.Distance(agent.Position, approachPoint);
                    agent.AssistProgressAnchorPosition = agent.Position;
                }
            }
            else if (noProgressSeconds >= _config.AssistanceReplanNoProgressSeconds &&
                     agent.AssistRecoveryStage < 1)
            {
                agent.AssistRecoveryStage = 1;
                agent.ClearRoute();
            }

            return ResolveStaticRouteDirection(
                simulation,
                agent,
                approachPoint,
                $"assist:{target.AgentId}:slot:{agent.AssistSlotIndex}");
        }

        private void UpdateAssistanceProgress(Simulation simulation, Agent agent, float approachDistance)
        {
            // FIX9: only progress toward the assistance goal may refresh the no-progress clock.
            // The FIX8 implementation also treated 0.15 m of arbitrary displacement as progress.
            // A helper trapped in a dense crowd could therefore shuffle sideways indefinitely and
            // never reach the 10-second abandonment guard even though its goal distance did not
            // improve.
            bool distanceProgress = !float.IsFinite(agent.AssistBestDistanceMeters) ||
                approachDistance <= agent.AssistBestDistanceMeters - _config.AssistanceMeaningfulProgressMeters;

            if (!distanceProgress)
            {
                return;
            }

            agent.AssistBestDistanceMeters = approachDistance;
            agent.AssistProgressAnchorPosition = agent.Position;
            agent.AssistLastProgressFrame = simulation.FrameIndex;
            agent.AssistRecoveryStage = 0;
        }

        private static void AbandonAssistance(Agent agent)
        {
            agent.AssistTargetAgentId = null;
            agent.Intent = BehaviorIntent.Evacuate;
            agent.IntentUntilFrame = -1;
            ClearAssistanceDiagnostics(agent);
            agent.ClearRoute();
        }

        private static void ClearAssistanceDiagnostics(Agent agent)
        {
            agent.AssistPauseUntilFrame = -1;
            agent.AssistAssignedFrame = -1;
            agent.AssistLastProgressFrame = -1;
            agent.AssistBestDistanceMeters = float.PositiveInfinity;
            agent.AssistProgressAnchorPosition = null;
            agent.AssistSlotIndex = -1;
            agent.AssistRecoveryStage = 0;
        }

        private Vector2 ResolveExitNavigationDirection(Simulation simulation, Agent agent, Exit exit)
            => ResolveStaticRouteDirection(
                simulation,
                agent,
                ExitApproachTarget(agent, exit),
                $"exit:{exit.Name}");

        private Vector2 TryInjuredChairClearance(Simulation simulation, Agent agent, Vector2 direction)
        {
            if (agent.State == AgentState.Injured &&
                simulation.TryPushChairTowardGoal(agent, direction))
            {
                ClearCircleFollow(agent);
                return VectorMath.SafeNormalize(direction);
            }
            return Vector2.Zero;
        }

        private Vector2 ExitApproachTarget(Agent agent, Exit exit)
        {
            int laneCount = ExitPeopleWidth(exit);
            if (laneCount <= 1)
            {
                return exit.Position;
            }

            int lane = Math.Abs(agent.AgentId) % laneCount;
            float normalized = ((lane + 0.5f) / laneCount) * 2f - 1f;
            float offset = normalized * exit.Size *
                Math.Clamp(_config.ExitLaneSpreadFactor, 0f, 0.90f);
            Vector2 tangent = exit.Side is "N" or "S" ? Vector2.UnitX : Vector2.UnitY;
            return exit.Position + tangent * offset;
        }

        private bool IsInExitCaptureCorridor(
            Simulation simulation,
            Agent agent,
            Exit exit,
            float distanceToExit,
            Vector2 approachTarget)
            => distanceToExit <= Math.Max(SnapRadius, _config.ExitCaptureRadius) &&
               simulation.HasTraversablePath(
                   agent.Position,
                   approachTarget,
                   NavigationClearance());

        internal Vector2 ResolveStaticRouteDirection(
            Simulation simulation,
            Agent agent,
            Vector2 goal,
            string routeKey)
        {
            if (UsesNaturalNavigation(simulation) && agent.State != AgentState.Crawling &&
                routeKey.StartsWith("exit:", StringComparison.Ordinal))
            {
                return ResolveNaturalRouteDirection(simulation, agent, goal, routeKey);
            }

            float clearance = NavigationClearance();
            Vector2 direct = goal - agent.Position;
            if (direct.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon)
            {
                return Vector2.Zero;
            }

            if (simulation.HasTraversablePath(agent.Position, goal, clearance))
            {
                if (string.Equals(agent.RouteExitName, routeKey, StringComparison.Ordinal))
                {
                    agent.ClearRoute();
                }

                return VectorMath.SafeNormalize(direct);
            }

            if (simulation.Config.HybridEnabled && routeKey.StartsWith("exit:", StringComparison.Ordinal) &&
                simulation.HybridMovement.Fields.TryDirection(simulation, agent.Position,
                    agent.ExitTarget?.Position ?? goal, out Vector2 fieldDirection))
                return fieldDirection;

            // A blocked chair cluster must not trigger the same failed A* search every tick.
            // Direct-path recovery above and changed destinations bypass this short cooldown.
            if (string.Equals(agent.FailedRouteKey, routeKey, StringComparison.Ordinal) &&
                simulation.FrameIndex < agent.FailedRouteRetryFrame)
            {
                return TryInjuredChairClearance(simulation, agent, direct);
            }

            bool routeStale = !string.Equals(agent.RouteExitName, routeKey, StringComparison.Ordinal) ||
                agent.CurrentRouteWaypoint is null ||
                simulation.FrameIndex >= agent.RouteReplanAfterFrame;
            if (routeStale)
            {
                long planningStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                simulation.LastRoutePlanRequests++;
                IReadOnlyList<Vector2> planned = _routePlanner.Plan(
                    simulation,
                    agent.Position,
                    goal);
                simulation.LastRoutePlanningMilliseconds +=
                    ReferenceClock.ElapsedMilliseconds(planningStarted);
                if (planned.Count > 0)
                {
                    agent.FailedRouteKey = null;
                    agent.FailedRouteRetryFrame = -1;
                    agent.SetRoute(
                        planned,
                        routeKey,
                        simulation.FrameIndex + SecondsToFrames(_config.RouteReplanSeconds));
                    agent.StuckProgressAnchorPosition = agent.Position;
                    agent.StuckProgressAnchorExitDistance = Vector2.Distance(agent.Position, planned[0]);
                    agent.StuckProgressAnchorFrame = simulation.FrameIndex;
                    agent.TimeStuck = 0f;
                }
                else
                {
                    agent.ClearRoute();
                    agent.FailedRouteKey = routeKey;
                    agent.FailedRouteRetryFrame = simulation.FrameIndex +
                        SecondsToFrames(0.30f + (agent.AgentId % 5) * 0.06f);
                    return TryInjuredChairClearance(simulation, agent, direct);
                }
            }

            // Visibility-graph corner nodes are not compulsory point targets. As soon as the
            // body has a clear segment to the following node, skip the current corner. This rounds
            // obstacle turns and prevents a whole crowd from being funnelled through one exact
            // coordinate while still preserving body-clear geometry.
            while (agent.CurrentRouteWaypoint is not null &&
                   agent.RouteWaypointIndex + 1 < agent.RouteWaypoints.Count)
            {
                Vector2 lookAhead = agent.RouteWaypoints[agent.RouteWaypointIndex + 1];
                if (!simulation.HasTraversablePath(
                    agent.Position,
                    lookAhead,
                    clearance))
                {
                    break;
                }

                agent.AdvanceRouteWaypoint();
                agent.StuckProgressAnchorPosition = agent.Position;
                agent.StuckProgressAnchorExitDistance = Vector2.Distance(agent.Position, lookAhead);
                agent.StuckProgressAnchorFrame = simulation.FrameIndex;
                agent.TimeStuck = 0f;
            }

            float reach = Math.Max(0.04f, _config.RouteWaypointReachFactor * _config.Radius);
            while (agent.CurrentRouteWaypoint is Vector2 waypoint &&
                   Vector2.Distance(agent.Position, waypoint) <= reach)
            {
                agent.AdvanceRouteWaypoint();
                agent.StuckProgressAnchorPosition = agent.Position;
                agent.StuckProgressAnchorExitDistance = agent.CurrentRouteWaypoint is Vector2 next
                    ? Vector2.Distance(agent.Position, next)
                    : Vector2.Distance(agent.Position, goal);
                agent.StuckProgressAnchorFrame = simulation.FrameIndex;
                agent.TimeStuck = 0f;
            }

            if (agent.CurrentRouteWaypoint is not Vector2 currentWaypoint)
            {
                agent.ClearRoute();
                return VectorMath.SafeNormalize(goal - agent.Position);
            }

            if (!simulation.HasTraversablePath(
                agent.Position,
                currentWaypoint,
                clearance))
            {
                agent.ClearRoute();
                return Vector2.Zero;
            }

            return VectorMath.SafeNormalize(currentWaypoint - agent.Position);
        }

        private static Vector2 Rotate(Vector2 vector, float angle)
        {
            float c = MathF.Cos(angle);
            float s = MathF.Sin(angle);
            return new Vector2(
                vector.X * c - vector.Y * s,
                vector.X * s + vector.Y * c);
        }

        public void UpdateEvacAgent(Simulation simulation, Agent agent)
        {
            if (agent.ExitTarget is null)
            {
                return;
            }

            if (!simulation.Config.HybridEnabled || agent.State != AgentState.Evacuating ||
                (simulation.FrameIndex + agent.AgentId) % 3 == 0 || simulation.FrameIndex >= agent.IntentUntilFrame)
                UpdateBehaviorIntent(simulation, agent);
            ObserveNaturalNavigation(simulation, agent);
            if (simulation.HybridMovement.TryAdvance(simulation, agent)) return;

            Exit exit = agent.ExitTarget;
            Vector2 approachTarget = ExitApproachTarget(agent, exit);
            Vector2 directExitVector = approachTarget - agent.Position;
            float distanceToExit = Vector2.Distance(agent.Position, exit.Position);
            Vector2 directExitDirection = VectorMath.SafeNormalize(directExitVector);
            // A waiting person can deliberately shift a reachable loose chair without momentum.
            // Moving the chair invalidates routes; casualty bypass still decides whether to advance.
            if (agent.TimeStuck >= 0.5f || agent.LocalNavigationMode == LocalNavigationMode.WaitingForGap)
            {
                if (simulation.TryPushChairTowardGoal(agent, directExitDirection)) ClearCircleFollow(agent);
            }
            bool exitCapture = IsInExitCaptureCorridor(
                simulation,
                agent,
                exit,
                distanceToExit,
                approachTarget);

            if (exitCapture && agent.Intent != BehaviorIntent.AssistCasualty)
            {
                // A visible, body-clear doorway is now the authoritative objective. This
                // prevents FollowCrowd/herding from holding a group beside the opening.
                agent.Intent = BehaviorIntent.Evacuate;
                agent.IntentUntilFrame = -1;
                agent.Exploring = false;
                agent.TempGoal = null;
                agent.ClearRoute();
                ClearSlowObstacleBypass(agent);
            }

            UpdateBottleneckInteraction(simulation, agent, exit, distanceToExit);
            SyncExitCongestionEpisode(agent, exit, distanceToExit);
            if (ShouldYieldAtCongestedExit(simulation, agent, exit))
            {
                agent.Velocity = Vector2.Zero;
                agent.LastDesiredDirection = Vector2.Zero;
                agent.LastSfmForce = Vector2.Zero;
                return;
            }

            if (agent.State == AgentState.Crawling)
            {
                ClearSlowObstacleBypass(agent);
                Vector2 crawlDirection = ResolveExitNavigationDirection(simulation, agent, exit);
                if (crawlDirection.LengthSquared() < VectorMath.Epsilon * VectorMath.Epsilon)
                {
                    crawlDirection = directExitDirection;
                }

                IntegrateLocomotion(simulation, agent, crawlDirection, agent.CurrentDesiredSpeed);
                simulation.MoveAgent(agent);
                return;
            }

            Vector2 primaryDirection;
            bool holdForAssistance = false;
            if (exitCapture && agent.Intent != BehaviorIntent.AssistCasualty)
            {
                primaryDirection = directExitDirection;
            }
            else if (UsesNaturalNavigation(simulation) &&
                     agent.NavigationMemory.DetourGoal is Vector2 recoveryGoal &&
                     agent.Intent is BehaviorIntent.Evacuate or BehaviorIntent.FollowCrowd)
            {
                primaryDirection = VectorMath.SafeNormalize(recoveryGoal - agent.Position);
            }
            else switch (agent.Intent)
                {
                    case BehaviorIntent.ThreatEscape:
                        primaryDirection = ComputeThreatEscapeDirection(simulation, agent);
                        break;

                    case BehaviorIntent.FollowCrowd:
                        primaryDirection = ComputeEvacuationFlowDirection(simulation, agent);
                        if (primaryDirection.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon)
                        {
                            float probe = Math.Max(4f * _config.Radius, 0.97f);
                            Vector2 probePoint = agent.Position + primaryDirection * probe;
                            if (!simulation.HasTraversablePath(
                                agent.Position,
                                probePoint,
                                NavigationClearance()))
                            {
                                primaryDirection = Vector2.Zero;
                            }
                        }
                        break;

                    case BehaviorIntent.AssistCasualty:
                        primaryDirection = ComputeAssistanceDirection(
                            simulation,
                            agent,
                            out holdForAssistance);
                        break;

                    default:
                        primaryDirection = ResolveExitNavigationDirection(simulation, agent, exit);
                        break;
                }

            if (holdForAssistance)
            {
                agent.Velocity = Vector2.Zero;
                agent.LastDesiredDirection = Vector2.Zero;
                agent.LastSfmForce = Vector2.Zero;
                return;
            }

            // A helper with no body-clear route to its current assistance position must not
            // silently fall back to exit exploration while still labelled GOING TO HELP. Keep
            // it stationary so the bounded 2/6/10-second assistance recovery logic can act.
            if (primaryDirection.LengthSquared() < VectorMath.Epsilon * VectorMath.Epsilon &&
                agent.Intent == BehaviorIntent.AssistCasualty)
            {
                agent.Velocity = Vector2.Zero;
                agent.LastDesiredDirection = Vector2.Zero;
                agent.LastSfmForce = Vector2.Zero;
                return;
            }

            // An intent may have completed while its direction was being resolved.
            if (primaryDirection.LengthSquared() < VectorMath.Epsilon * VectorMath.Epsilon &&
                agent.Intent is BehaviorIntent.Evacuate or BehaviorIntent.FollowCrowd)
            {
                primaryDirection = ResolveExitNavigationDirection(simulation, agent, exit);
            }

            primaryDirection = ApplyNaturalRecoveryDirection(simulation, agent, primaryDirection, exitCapture);

            if (primaryDirection.LengthSquared() < VectorMath.Epsilon * VectorMath.Epsilon &&
                UsesNaturalNavigation(simulation) && agent.Intent != BehaviorIntent.AssistCasualty)
            {
                // Continue a coarse intention while a bounded search is queued. Local obstacle
                // following and physical collision checks still run; do not start legacy random
                // exploration, which can erase the new failure history or cancel a useful detour.
                primaryDirection = directExitDirection;
            }

            if (primaryDirection.LengthSquared() < VectorMath.Epsilon * VectorMath.Epsilon)
            {
                // Visibility-graph failure is rare (e.g. pathological custom geometry). Retain the
                // previous local exploration only as a bounded fallback, not the primary planner.
                if (!agent.Exploring)
                {
                    EnterExploration(agent, simulation);
                }

                if (agent.TempGoal is Vector2 temporaryGoal)
                {
                    primaryDirection = VectorMath.SafeNormalize(temporaryGoal - agent.Position);
                }
                else
                {
                    primaryDirection = directExitDirection;
                }
            }
            else if (agent.Exploring)
            {
                StopExploration(agent);
            }

            // Fume steering precedes circle/casualty handling: hazard avoidance must not undo
            // a body-clear tangent or a local wait decision. Committed detours are scored and
            // rechecked separately, so this layer does not pull against a useful recovery goal.
            primaryDirection = ApplyNaturalToxicAvoidance(simulation, agent, primaryDirection, exitCapture);

            Vector2 navigationPoint = agent.CurrentRouteWaypoint ?? approachTarget;
            if (agent.Intent == BehaviorIntent.AssistCasualty && agent.AssistTargetAgentId is int targetId)
            {
                Agent? target = simulation.Agents.FirstOrDefault(candidate => candidate.AgentId == targetId);
                if (target is not null)
                {
                    Vector2 assistPoint = agent.AssistSlotIndex is 0 or 1
                        ? AssistanceApproachPoint(simulation, target, agent.AssistSlotIndex)
                        : target.Position;
                    navigationPoint = agent.CurrentRouteWaypoint ?? assistPoint;
                }
            }
            else if (agent.Intent == BehaviorIntent.ThreatEscape)
            {
                navigationPoint = agent.Position + primaryDirection * Math.Max(6f * _config.Radius, 1.46f);
            }

            UpdateStuckProgress(simulation, agent, Vector2.Distance(agent.Position, navigationPoint));
            if (agent.TimeStuck > 1.5f && !(UsesNaturalNavigation(simulation) && IsNaturalMover(agent)))
            {
                agent.ClearRoute();
                agent.TimeStuck = 0f;
                if (agent.Intent is BehaviorIntent.Evacuate or BehaviorIntent.FollowCrowd)
                {
                    Vector2 replanned = ResolveExitNavigationDirection(simulation, agent, exit);
                    if (replanned.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon)
                    {
                        primaryDirection = replanned;
                    }
                }
            }

            // Casualties are dynamic local obstacles, intentionally excluded from the static route graph.
            // The same small spatial query also identifies a slow crawler directly ahead so FIX10
            // does not add a second per-agent neighbour scan to the hot path.
            Vector2 avoidTangent = Vector2.Zero;
            int avoidCount = 0;
            Agent? localCasualtyBlocker = null;
            float localCasualtyBlockerScore = float.PositiveInfinity;
            Vector2 bypassForward = VectorMath.SafeNormalize(primaryDirection);
            float casualtyAvoidanceRadius = _config.FallenAvoidanceRadiusFactor * _config.Radius;
            float slowBypassDetectionRadius = Math.Max(
                4f * _config.Radius,
                _config.SlowObstacleBypassDetectionRadiusFactor * _config.Radius);
            float obstacleQueryRadius = Math.Max(casualtyAvoidanceRadius, slowBypassDetectionRadius);
            float slowBypassCorridorHalfWidth = Math.Max(
                1.8f * _config.Radius,
                _config.SlowObstacleBypassForwardCorridorFactor * _config.Radius);
            simulation.CollectNearbyAgents(agent, obstacleQueryRadius, _nearbyScratch);
            foreach (Agent other in _nearbyScratch)
            {
                if (!other.IsActive || !other.IsCasualtyObstacle ||
                    other.AgentId == agent.AssistTargetAgentId)
                {
                    continue;
                }

                Vector2 difference = agent.Position - other.Position;
                float distance = difference.Length();
                if (distance < VectorMath.Epsilon || distance > obstacleQueryRadius)
                {
                    continue;
                }

                if (distance <= slowBypassDetectionRadius &&
                    bypassForward.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon)
                {
                    Vector2 relative = other.Position - agent.Position;
                    float forwardDistance = Vector2.Dot(relative, bypassForward);
                    float lateralDistance = MathF.Abs(Vector2.Dot(
                        relative,
                        VectorMath.Perpendicular(bypassForward)));
                    float ownSpeed = Math.Max(agent.CurrentDesiredSpeed, agent.BaseSpeed);
                    float blockerSpeed = other.Velocity.Length();
                    bool validSlowCrawler = other.State == AgentState.Crawling &&
                        blockerSpeed <= Math.Max(
                            0.004f,
                            ownSpeed * _config.SlowObstacleBypassMaxSpeedRatio);
                    bool validThreatCasualty = agent.Intent == BehaviorIntent.ThreatEscape;

                    if ((validSlowCrawler || validThreatCasualty) &&
                        forwardDistance > Math.Max(0.35f * _config.Radius, 0.07f) &&
                        forwardDistance <= slowBypassDetectionRadius &&
                        lateralDistance <= slowBypassCorridorHalfWidth &&
                        simulation.HasLineOfSight(agent.Position, other.Position))
                    {
                        float score = forwardDistance + 0.55f * lateralDistance;
                        if (score < localCasualtyBlockerScore)
                        {
                            localCasualtyBlockerScore = score;
                            localCasualtyBlocker = other;
                        }
                    }
                }

                if (distance > casualtyAvoidanceRadius)
                {
                    continue;
                }

                Vector2 normal = difference / distance;
                Vector2 tangent = VectorMath.Perpendicular(normal);
                if (Vector2.Dot(-tangent, primaryDirection) > Vector2.Dot(tangent, primaryDirection))
                {
                    tangent = -tangent;
                }

                float weight = MathF.Exp(-distance / Math.Max(2f * _config.Radius, 0.04f));
                avoidTangent += tangent * weight;
                avoidCount++;
            }

            if (avoidCount > 0)
            {
                avoidTangent = VectorMath.SafeNormalize(avoidTangent / avoidCount);
            }

            Vector2 herdDirection = ComputeHerdDirection(simulation, agent);
            float visibilityLoss = 1f - Math.Clamp(agent.VisibilityFactor, 0f, 1f);
            float lambda = agent.Intent switch
            {
                BehaviorIntent.FollowCrowd => Math.Clamp(0.45f + 0.35f * agent.FollowTendency, 0f, 0.80f),
                BehaviorIntent.ThreatEscape => 0.08f,
                BehaviorIntent.AssistCasualty => 0.05f,
                _ => Math.Clamp(
                    _config.HerdingLambdaMax *
                    agent.PanicLevel *
                    (0.65f + 0.70f * agent.FollowTendency) *
                    (1f + 0.65f * visibilityLoss),
                    0f,
                    _config.HerdingLambdaMax),
            };

            // Static route waypoints must dominate near obstacle corners. In the final
            // body-clear doorway corridor, the exit dominates completely; otherwise a
            // coherent herd can orbit beside the opening and release only one agent at a time.
            if (exitCapture)
            {
                lambda = 0f;
            }
            else if (agent.CurrentRouteWaypoint is not null)
            {
                lambda = Math.Min(lambda, 0.20f);
            }
            else if (simulation.HasLineOfSight(agent.Position, exit.Position) &&
                     agent.VisibilityFactor > 0.78f &&
                     agent.Intent == BehaviorIntent.Evacuate)
            {
                lambda *= 0.20f;
            }

            if (UsesNaturalNavigation(simulation) && agent.NavigationMemory.DetourGoal is not null)
            {
                lambda = Math.Min(lambda, 0.04f); // a crowd must not pull a committed escape back into its trap
            }

            Vector2 goalDirection = primaryDirection;
            if (herdDirection.LengthSquared() > VectorMath.Epsilon * VectorMath.Epsilon && lambda > 0f)
            {
                goalDirection = VectorMath.SafeNormalize(
                    (1f - lambda) * primaryDirection + lambda * herdDirection,
                    primaryDirection);
            }

            // Static circular geometry gets first local-navigation priority. Once a pedestrian is
            // close enough that the current motor direction would cut through an inflated circle,
            // commit briefly to one tangent side instead of repeating push-in / collision-push-out.
            goalDirection = ApplyCircleObstacleFollow(
                simulation, agent, goalDirection, exitCapture);

            // Casualties are dynamic obstacles, not evacuation leaders. If no immediate body-clear
            // bypass exists, use a bounded bypass -> wait -> seek gap -> controlled press sequence.
            // Waiting is a behaviour mode, not a physical AgentState, so colours/health remain stable.
            bool holdForLocalGap;
            goalDirection = ApplyCasualtyBypass(
                simulation, agent, localCasualtyBlocker, goalDirection, exitCapture, out holdForLocalGap);
            if (holdForLocalGap)
            {
                agent.Velocity = Vector2.Zero;
                // Waiting is a motor decision, not permission to overlap a casualty. Resolve the
                // larger casualty body envelope even though MoveAgent() is intentionally skipped.
                simulation.ResolveSolidCollisions(agent);
                agent.LastAvoidanceDirection = avoidTangent;
                agent.LastHerdDirection = herdDirection;
                agent.LastDesiredDirection = Vector2.Zero;
                agent.LastSfmForce = Vector2.Zero;
                return;
            }

            goalDirection = ApplyBottleneckDirection(simulation, agent, goalDirection);
            if (exitCapture)
            {
                // Preserve bounded gap seeking/pressing, but require every final-approach
                // choice to retain a strong component toward an actual doorway lane.
                float directWeight = agent.BottleneckMode == BottleneckResponseMode.SeekGap
                    ? 0.72f
                    : 0.88f;
                goalDirection = VectorMath.SafeNormalize(
                    directWeight * directExitDirection + (1f - directWeight) * goalDirection,
                    directExitDirection);
            }

            if (avoidCount > 0)
            {
                float avoidStrength = 0.14f + 0.12f * Math.Clamp(agent.PanicLevel, 0f, 1f);
                goalDirection = VectorMath.SafeNormalize(
                    (1f - avoidStrength) * goalDirection + avoidStrength * avoidTangent,
                    goalDirection);
            }

            goalDirection = ApplyNaturalMovingFurnitureAvoidance(simulation, agent, goalDirection);
            agent.LastAvoidanceDirection = avoidTangent;
            agent.LastHerdDirection = herdDirection;
            agent.LastDesiredDirection = goalDirection;

            IntegrateLocomotion(simulation, agent, goalDirection, agent.CurrentDesiredSpeed);

            simulation.MoveAgent(agent);
        }

        /// <summary>
        /// Integrate a single SI acceleration field, then return to the public reference-step
        /// velocity convention. No second velocity blend competes with contact forces.
        /// </summary>
        public void IntegrateLocomotion(Simulation simulation, Agent agent, Vector2 direction, float speedLimit)
        {
            speedLimit = Math.Min(Math.Max(0f, speedLimit), _config.Behavior.MaxRunningSpeedPerReferenceStep);
            Vector2 force = simulation.SfmEngine.ComputeForces(simulation, agent, direction);
            Vector2 acceleration = VectorMath.ClampMagnitude(
                force / Math.Max(agent.Mass, 1e-5f),
                _config.MaxAccelerationMetersPerSecondSquared);
            agent.Velocity += acceleration * (_config.Dt * AgentConfig.ReferenceStepSeconds);
            agent.Velocity = VectorMath.IsFinite(agent.Velocity)
                ? VectorMath.ClampMagnitude(agent.Velocity, Math.Max(0f, speedLimit))
                : Vector2.Zero;
        }

        public void ApplyExitFlows(Simulation simulation)
        {
            foreach (Exit exit in simulation.Environment.Exits)
            {
                int admissionBudget = GetExitAdmissionBudget(simulation, exit);
                _exitCandidatesScratch.Clear();
                _exitEligibleScratch.Clear();

                float queryRadius = exit.Size * 1.414214f + 0.01f;
                simulation.CollectAgentsNearPoint(exit.Position, queryRadius, _exitQueryScratch);
                foreach (Agent agent in _exitQueryScratch)
                {
                    if (!agent.IsActive ||
                        agent.Posture != SeatingPosture.Walking ||
                        agent.State is not (AgentState.Evacuating or AgentState.Injured or AgentState.Crawling) ||
                        !IsInsideExitZone(agent.Position, exit))
                    {
                        continue;
                    }

                    _exitCandidatesScratch.Add(agent);
                }

                // Update doorway-pressure state for everyone physically in the zone before deciding
                // who is eligible for this frame's bounded admission slots.
                foreach (Agent candidate in _exitCandidatesScratch)
                {
                    UpdateBottleneckInteraction(
                        simulation,
                        candidate,
                        exit,
                        Vector2.Distance(candidate.Position, exit.Position));

                    if (!candidate.ExitYielding || simulation.FrameIndex >= candidate.ExitRetryAfterFrame)
                    {
                        _exitEligibleScratch.Add(candidate);
                    }
                }

                if (_exitEligibleScratch.Count == 0)
                {
                    continue;
                }

                Vector2 exitPosition = exit.Position;
                _exitEligibleScratch.Sort((left, right) =>
                {
                    int distanceOrder = Vector2.Distance(left.Position, exitPosition)
                        .CompareTo(Vector2.Distance(right.Position, exitPosition));
                    return distanceOrder != 0
                        ? distanceOrder
                        : left.AgentId.CompareTo(right.AgentId);
                });

                int admitted = Math.Min(admissionBudget, _exitEligibleScratch.Count);
                (int creditFrame, double credit) = _exitAdmissionCredits[exit];
                _exitAdmissionCredits[exit] = (creditFrame, credit - admitted);
                for (int i = 0; i < admitted; i++)
                {
                    _exitEligibleScratch[i].MarkExited();
                }

                for (int i = admitted; i < _exitEligibleScratch.Count; i++)
                {
                    RegisterExitRejection(simulation, _exitEligibleScratch[i], exit);
                }
            }
        }

        private int GetExitAdmissionBudget(Simulation simulation, Exit exit)
        {
            double perStep = ExitPeopleWidth(exit) *
                _config.ExitAdmissionsPerLanePerSecond * _config.Dt;
            double credit;
            if (!_exitAdmissionCredits.TryGetValue(exit, out var previous))
            {
                credit = perStep;
            }
            else if (previous.Frame == simulation.FrameIndex)
            {
                return (int)Math.Floor(previous.Credit + 1e-6);
            }
            else
            {
                // Discard unused whole admissions; retain fractions for small timesteps.
                credit = previous.Credit - Math.Floor(previous.Credit + 1e-6) + perStep;
            }
            credit = Math.Max(0d, credit);
            _exitAdmissionCredits[exit] = (simulation.FrameIndex, credit);
            return (int)Math.Floor(credit + 1e-6);
        }

        public void EnterExploration(Agent agent, Simulation simulation)
        {
            if (agent.ExitTarget is null)
            {
                return;
            }

            // A visually clear exit may still be physically blocked by the pedestrian's body
            // radius at a wall endpoint. Only a body-clear route cancels exploration.
            if (simulation.HasTraversablePath(
                agent.Position,
                agent.ExitTarget.Position,
                NavigationClearance()))
            {
                StopExploration(agent);
                // Preserve TimeStuck here so the existing concave-corner / zero-velocity
                // breakers can still activate if motion nevertheless fails.
                return;
            }

            if (TryChooseExplorationGoal(simulation, agent, out Vector2 goal))
            {
                BeginExplorationToward(simulation, agent, goal);
            }
        }

        private void BeginExplorationToward(Simulation simulation, Agent agent, Vector2 goal)
        {
            agent.TempGoal = goal;
            agent.Exploring = true;
            agent.TimeStuck = 0f;
            agent.StuckProgressAnchorPosition = agent.Position;
            agent.StuckProgressAnchorExitDistance = agent.ExitTarget is null
                ? float.PositiveInfinity
                : Vector2.Distance(agent.Position, agent.ExitTarget.Position);
            agent.StuckProgressAnchorFrame = simulation.FrameIndex;
            int replanFrames = Math.Max(
                1,
                (int)MathF.Ceiling(Math.Max(_config.Dt, _config.ExplorationReplanSeconds) / _config.Dt));
            agent.ExplorationReplanAfterFrame = simulation.FrameIndex + replanFrames;
        }

        private static void StopExploration(Agent agent)
        {
            agent.Exploring = false;
            agent.TempGoal = null;
            agent.ExplorationReplanAfterFrame = 0;
        }

        private bool TryChooseExplorationGoal(Simulation simulation, Agent agent, out Vector2 bestGoal)
        {
            bestGoal = agent.Position;
            if (agent.ExitTarget is null)
            {
                return false;
            }

            Vector2 exitVector = agent.ExitTarget.Position - agent.Position;
            float currentExitDistance = exitVector.Length();
            Vector2 exitDirection = VectorMath.SafeNormalize(exitVector);
            Vector2 velocityDirection = VectorMath.SafeNormalize(agent.Velocity);
            float probeDistance = Math.Max(
                4f * _config.Radius,
                Math.Max(0.04f, _config.ExplorationProbeDistance));
            int directionSamples = Math.Max(8, _config.ExplorationDirectionSamples);
            float endpointMargin = Math.Max(
                NavigationClearance(),
                _config.ExplorationEndpointClearanceFactor * _config.Radius);
            float innerMargin = simulation.Environment.BoundaryThickness + endpointMargin;

            float bestScore = float.NegativeInfinity;
            bool found = false;

            for (int index = 0; index < directionSamples; index++)
            {
                float angle = 2f * MathF.PI * index / directionSamples;
                Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 candidate = agent.Position + direction * probeDistance;

                if (candidate.X < innerMargin ||
                    candidate.Y < innerMargin ||
                    candidate.X > simulation.Environment.Width - innerMargin ||
                    candidate.Y > simulation.Environment.Height - innerMargin)
                {
                    continue;
                }

                if (simulation.Environment.IsPointBlocked(candidate, endpointMargin) ||
                    !simulation.HasTraversablePath(
                        agent.Position,
                        candidate,
                        NavigationClearance()))
                {
                    continue;
                }

                float candidateExitDistance = Vector2.Distance(candidate, agent.ExitTarget.Position);
                float normalizedProgress = (currentExitDistance - candidateExitDistance) / probeDistance;
                float exitAlignment = Vector2.Dot(direction, exitDirection);
                float velocityAlignment = velocityDirection.LengthSquared() >
                    VectorMath.Epsilon * VectorMath.Epsilon
                    ? Vector2.Dot(direction, velocityDirection)
                    : 0f;
                float clearance = EndpointClearance(simulation, candidate);
                float clearanceScore = Math.Clamp(
                    clearance / Math.Max(3f * _config.Radius, 0.04f),
                    0f,
                    1f);
                bool recoversExitPath = simulation.HasTraversablePath(
                    candidate,
                    agent.ExitTarget.Position,
                    NavigationClearance());
                simulation.CollectAgentsNearPoint(
                    candidate,
                    3f * _config.Radius,
                    _explorationCrowdScratch,
                    agent.AgentId);
                int nearbyPeople = _explorationCrowdScratch.Count;
                float crowdPenalty = Math.Clamp(nearbyPeople / 4f, 0f, 1f);

                // Reacquiring a body-clear route to the exit dominates the score. When no sampled
                // waypoint can do that yet, prefer open space that still makes reasonable
                // progress and broadly faces the exit. A small velocity term preserves motion
                // continuity without locking the agent into a bad heading.
                float score =
                    (recoversExitPath ? 3.0f : 0f) +
                    1.20f * normalizedProgress +
                    0.40f * exitAlignment +
                    0.85f * clearanceScore +
                    0.10f * velocityAlignment -
                    0.45f * crowdPenalty;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestGoal = candidate;
                }
            }

            return found;
        }

        /// <summary>
        /// Apply a short tangent-follow commitment around a circular static blockage. This is a
        /// local motor response layered on top of the visibility-graph route: the route still owns
        /// the destination, while the tangent commitment prevents repeated inward drive/correction
        /// when a pedestrian reaches the curved surface under crowd pressure.
        /// </summary>
        private Vector2 ApplyCircleObstacleFollow(
            Simulation simulation,
            Agent agent,
            Vector2 goalDirection,
            bool exitCapture)
        {
            if (agent.State is not (AgentState.Evacuating or AgentState.Injured) ||
                agent.Intent == BehaviorIntent.AssistCasualty ||
                goalDirection.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon ||
                simulation.Environment.CircularBlockages.Count == 0)
            {
                if (agent.LocalNavigationMode == LocalNavigationMode.CircleFollow)
                {
                    ClearCircleFollow(agent);
                }

                return goalDirection;
            }

            if (UsesNaturalNavigation(simulation) &&
                agent.NavigationMemory.DetourGoal is not null)
            {
                ClearCircleFollow(agent);
                return goalDirection;
            }

            if (UsesNaturalNavigation(simulation) &&
                agent.LocalNavigationMode == LocalNavigationMode.CircleFollow &&
                agent.LocalNavigationModeSinceFrame >= 0 &&
                (simulation.FrameIndex - agent.LocalNavigationModeSinceFrame) * _config.Dt >=
                    _config.NaturalCircleMaximumSeconds)
            {
                RequestNaturalRecovery(simulation, agent, force: true);
                ClearCircleFollow(agent);
                return goalDirection;
            }

            if (agent.State == AgentState.Injured && agent.LastChairPushFrame == simulation.FrameIndex)
            {
                ClearCircleFollow(agent);
                return agent.LastChairPushDirection;
            }

            if (IsCommittedCasualtyManeuver(agent.LocalNavigationMode) &&
                simulation.FrameIndex <= agent.SlowObstacleBypassUntilFrame)
                return goalDirection; // the committed open-space path owns local steering

            Vector2 desired = VectorMath.SafeNormalize(goalDirection);
            float clearance = NavigationClearance();
            float probeDistance = Math.Max(
                _config.CircleFollowProbeDistanceFactor * _config.Radius,
                0.95f);
            if (exitCapture)
            {
                probeDistance = Math.Min(probeDistance, 1.10f);
            }

            if (agent.LocalNavigationMode == LocalNavigationMode.CircleFollow &&
                agent.CircleFollowCenter is Vector2 committedCenter)
            {
                CircleBlockage? activeCircle = null;
                foreach (CircleBlockage circle in simulation.Environment.CircularBlockages)
                {
                    if (Vector2.DistanceSquared(circle.Center, committedCenter) <= 0.0004f &&
                        MathF.Abs(circle.Radius - agent.CircleFollowObstacleRadius) <= 0.01f)
                    {
                        activeCircle = circle;
                        break;
                    }
                }

                if (activeCircle is null)
                {
                    ClearCircleFollow(agent);
                    return desired;
                }

                if (simulation.TryPushLooseChair(agent, activeCircle, desired))
                {
                    ClearCircleFollow(agent);
                    return desired;
                }

                Vector2 offset = agent.Position - activeCircle.Center;
                float centerDistance = offset.Length();
                Vector2 normal = centerDistance > VectorMath.Epsilon
                    ? offset / centerDistance
                    : Vector2.UnitX;
                Vector2 tangent = VectorMath.Perpendicular(normal) *
                    (agent.CircleFollowDirection == 0 ? 1 : agent.CircleFollowDirection);

                int committedFrames = simulation.FrameIndex - agent.LocalNavigationModeSinceFrame;
                int minimumCommitFrames = SecondsToFrames(_config.CircleFollowMinimumCommitSeconds);
                Vector2 desiredProbe = agent.Position + desired * probeDistance;
                bool desiredPathClear = simulation.HasTraversablePath(
                    agent.Position,
                    desiredProbe,
                    clearance);
                float surfaceGap = centerDistance - (activeCircle.Radius + _config.Radius);
                float releaseClearance = Math.Max(
                    0.10f,
                    _config.CircleFollowReleaseClearanceFactor * _config.Radius);
                bool desiredMovesAwayFromCircle = Vector2.Dot(desired, normal) > 0.08f;

                if (committedFrames >= minimumCommitFrames &&
                    desiredPathClear &&
                    (surfaceGap >= releaseClearance || desiredMovesAwayFromCircle))
                {
                    ClearCircleFollow(agent);
                    return desired;
                }

                // If the initial window expires while the route is still blocked, keep the same
                // side rather than flip-flopping clockwise/counter-clockwise around the cylinder.
                if (simulation.FrameIndex > agent.LocalNavigationModeUntilFrame)
                {
                    agent.LocalNavigationModeUntilFrame = simulation.FrameIndex +
                        SecondsToFrames(_config.CircleFollowCommitSeconds);
                }

                // A small outward component keeps the body clear of the curved surface while SFM
                // wall force remains active. The tangent still dominates the intended motion.
                return VectorMath.SafeNormalize(
                    0.96f * tangent + 0.28f * normal,
                    tangent);
            }

            float activationDistance = Math.Max(
                1.5f * _config.Radius,
                _config.CircleFollowActivationDistanceFactor * _config.Radius);
            if (exitCapture)
            {
                activationDistance *= 0.80f;
            }

            CircleBlockage? nearestBlockingCircle = null;
            Vector2 nearestNormal = Vector2.Zero;
            float nearestSurfaceGap = float.PositiveInfinity;
            Vector2 lookAhead = agent.Position + desired * probeDistance;
            foreach (CircleBlockage circle in simulation.Environment.CircularBlockages)
            {
                Vector2 offset = agent.Position - circle.Center;
                float centerDistance = offset.Length();
                if (!float.IsFinite(centerDistance) || centerDistance < VectorMath.Epsilon)
                {
                    continue;
                }

                Vector2 normal = offset / centerDistance;
                float surfaceGap = centerDistance - (circle.Radius + _config.Radius);
                if (surfaceGap > activationDistance ||
                    Vector2.Dot(desired, -normal) <= 0.04f ||
                    !CircleGeometry.SegmentIntersectsCircle(
                        agent.Position,
                        lookAhead,
                        circle.Center,
                        circle.Radius + clearance))
                {
                    continue;
                }

                if (surfaceGap < nearestSurfaceGap)
                {
                    nearestSurfaceGap = surfaceGap;
                    nearestBlockingCircle = circle;
                    nearestNormal = normal;
                }
            }

            if (nearestBlockingCircle is null)
            {
                return desired;
            }

            if (simulation.TryPushLooseChair(agent, nearestBlockingCircle, desired))
            {
                ClearCircleFollow(agent);
                return desired;
            }

            BeginCircleFollow(simulation, agent, nearestBlockingCircle, nearestNormal, desired);
            Vector2 chosenTangent = VectorMath.Perpendicular(nearestNormal) * agent.CircleFollowDirection;
            return VectorMath.SafeNormalize(
                0.96f * chosenTangent + 0.28f * nearestNormal,
                chosenTangent);
        }

        private void BeginCircleFollow(
            Simulation simulation,
            Agent agent,
            CircleBlockage circle,
            Vector2 normal,
            Vector2 desiredDirection)
        {
            Vector2 tangent = VectorMath.Perpendicular(normal);
            float positiveAlignment = Vector2.Dot(tangent, desiredDirection);
            float negativeAlignment = Vector2.Dot(-tangent, desiredDirection);
            int direction;
            if (MathF.Abs(positiveAlignment - negativeAlignment) < 0.02f)
            {
                direction = (agent.AgentId & 1) == 0 ? 1 : -1;
            }
            else
            {
                direction = positiveAlignment >= negativeAlignment ? 1 : -1;
            }

            // Static geometry takes precedence over a casualty detour for this short local window.
            ClearSlowObstacleBypass(agent);
            agent.LocalNavigationBlockerAgentId = null;
            agent.LocalNavigationBlockedSinceFrame = -1;
            agent.LocalNavigationBypassAttempts = 0;
            agent.LocalNavigationMode = LocalNavigationMode.CircleFollow;
            agent.LocalNavigationModeSinceFrame = simulation.FrameIndex;
            agent.LocalNavigationModeUntilFrame = simulation.FrameIndex +
                SecondsToFrames(_config.CircleFollowCommitSeconds);
            agent.CircleFollowCenter = circle.Center;
            agent.CircleFollowObstacleRadius = circle.Radius;
            agent.CircleFollowDirection = direction;
        }

        private static void ClearCircleFollow(Agent agent)
        {
            if (agent.LocalNavigationMode == LocalNavigationMode.CircleFollow)
            {
                agent.LocalNavigationMode = LocalNavigationMode.Free;
                agent.LocalNavigationModeSinceFrame = -1;
                agent.LocalNavigationModeUntilFrame = -1;
            }

            agent.CircleFollowCenter = null;
            agent.CircleFollowObstacleRadius = 0f;
            agent.CircleFollowDirection = 0;
        }

        /// <summary>
        /// Resolve a casualty that blocks the immediate movement corridor. A clear local bypass is
        /// still preferred. When both sides are occupied, a pedestrian now waits instead of changing
        /// side every frame, then seeks a wider gap, and only after a longer delay accepts reduced
        /// social spacing through a physically body-clear gap. Hard contact radii never change.
        /// </summary>
        private Vector2 ApplyCasualtyBypass(
            Simulation simulation,
            Agent agent,
            Agent? blocker,
            Vector2 goalDirection,
            bool exitCapture,
            out bool holdPosition)
        {
            holdPosition = false;
            if (agent.LocalNavigationMode == LocalNavigationMode.CircleFollow)
            {
                if (blocker is not null && agent.State == AgentState.Evacuating &&
                    agent.Intent != BehaviorIntent.AssistCasualty &&
                    TryChooseEarlyOpenSpaceGoal(simulation, agent, goalDirection,
                        agent.Intent == BehaviorIntent.ThreatEscape, out Vector2 mixedObstacleGoal))
                {
                    ClearCircleFollow(agent);
                    StartCasualtyManeuver(simulation, agent, blocker.AgentId, mixedObstacleGoal,
                        LocalNavigationMode.SeekingGap, Math.Max(1.8f, _config.CasualtyGapSeekCommitSeconds));
                    return VectorMath.SafeNormalize(mixedObstacleGoal - agent.Position, goalDirection);
                }
                return goalDirection;
            }

            if (exitCapture ||
                agent.State is not (AgentState.Evacuating or AgentState.Injured) ||
                agent.Intent == BehaviorIntent.AssistCasualty ||
                goalDirection.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon)
            {
                ResetCasualtyBlockageEpisode(agent);
                return goalDirection;
            }

            bool threatEscape = agent.Intent == BehaviorIntent.ThreatEscape;

            // Honour a committed bypass/gap manoeuvre. A fixed side/goal for a short interval is the
            // main anti-oscillation mechanism; expiry simply returns the agent to the state machine.
            if (IsCommittedCasualtyManeuver(agent.LocalNavigationMode) &&
                agent.SlowObstacleBypassGoal is Vector2 committedGoal &&
                simulation.FrameIndex <= agent.SlowObstacleBypassUntilFrame)
            {
                float reach = Math.Max(1.35f * _config.Radius, 0.27f);
                if (Vector2.DistanceSquared(agent.Position, committedGoal) > reach * reach &&
                    simulation.HasTraversablePath(agent.Position, committedGoal, NavigationClearance()) &&
                    OpenSpacePathAvoidsPeople(simulation, agent, committedGoal))
                {
                    return VectorMath.SafeNormalize(committedGoal - agent.Position, goalDirection);
                }

                ClearSlowObstacleBypass(agent, clearObservation: false);
                SetLocalNavigationMode(simulation, agent, LocalNavigationMode.Free, 0f);
            }
            else if (agent.SlowObstacleBypassGoal is not null &&
                     IsCommittedCasualtyManeuver(agent.LocalNavigationMode))
            {
                ClearSlowObstacleBypass(agent, clearObservation: false);
                SetLocalNavigationMode(simulation, agent, LocalNavigationMode.Free, 0f);
            }

            Vector2 forward = VectorMath.SafeNormalize(goalDirection);
            if (blocker is null)
            {
                ResetCasualtyBlockageEpisode(agent);
                return goalDirection;
            }

            if (agent.LocalNavigationBlockedSinceFrame < 0)
            {
                agent.LocalNavigationBlockedSinceFrame = simulation.FrameIndex;
            }
            agent.LocalNavigationBlockerAgentId = blocker.AgentId;

            // ThreatEscape reacts immediately. Ordinary evacuation retains the observation window
            // that prevents treating a momentary crawler speed fluctuation as a true blockage.
            if (agent.SlowObstacleCandidateAgentId != blocker.AgentId)
            {
                agent.SlowObstacleCandidateAgentId = blocker.AgentId;
                agent.SlowObstacleCandidateSinceFrame = simulation.FrameIndex;
                if (!threatEscape)
                {
                    return goalDirection;
                }
            }

            if (!threatEscape)
            {
                int observationFrames = SecondsToFrames(agent.State == AgentState.Evacuating
                    ? Math.Min(0.15f, _config.SlowObstacleBypassObservationSeconds)
                    : _config.SlowObstacleBypassObservationSeconds);
                if (agent.SlowObstacleCandidateSinceFrame < 0 ||
                    simulation.FrameIndex - agent.SlowObstacleCandidateSinceFrame < observationFrames)
                {
                    return goalDirection;
                }
            }

            Vector2 bypassGoal = default;
            bool foundImmediateBypass = agent.LocalNavigationBypassAttempts == 0 &&
                (threatEscape
                    ? TryChooseThreatEscapeCasualtyBypassGoal(simulation, agent, forward, out bypassGoal)
                    : TryChooseSlowCrawlerBypassGoal(simulation, agent, blocker, forward, out bypassGoal));

            if (foundImmediateBypass)
            {
                float commitSeconds = threatEscape
                    ? _config.ThreatEscapeCasualtyBypassCommitSeconds
                    : _config.SlowObstacleBypassCommitSeconds;
                StartCasualtyManeuver(
                    simulation,
                    agent,
                    blocker.AgentId,
                    bypassGoal,
                    LocalNavigationMode.CasualtyBypass,
                    commitSeconds);
                return VectorMath.SafeNormalize(bypassGoal - agent.Position, goalDirection);
            }

            // Healthy pedestrians seek open space before entering the long wait/press sequence.
            if (agent.State == AgentState.Evacuating &&
                TryChooseEarlyOpenSpaceGoal(simulation, agent, forward, threatEscape, out Vector2 openGoal))
            {
                StartCasualtyManeuver(simulation, agent, blocker.AgentId, openGoal,
                    LocalNavigationMode.SeekingGap, Math.Max(1.8f, _config.CasualtyGapSeekCommitSeconds));
                return VectorMath.SafeNormalize(openGoal - agent.Position, goalDirection);
            }

            float blockedSeconds = Math.Max(
                0f,
                (simulation.FrameIndex - agent.LocalNavigationBlockedSinceFrame) * _config.Dt);
            float waitSeconds = ComputeCasualtyWaitSeconds(agent);
            if (blockedSeconds < waitSeconds)
            {
                SetLocalNavigationMode(simulation, agent, LocalNavigationMode.WaitingForGap, _config.Dt);
                holdPosition = true;
                return Vector2.Zero;
            }

            if (TryChooseCasualtyGapGoal(
                simulation,
                agent,
                forward,
                threatEscape,
                pressMode: false,
                out Vector2 gapGoal))
            {
                StartCasualtyManeuver(
                    simulation,
                    agent,
                    blocker.AgentId,
                    gapGoal,
                    LocalNavigationMode.SeekingGap,
                    _config.CasualtyGapSeekCommitSeconds);
                return VectorMath.SafeNormalize(gapGoal - agent.Position, goalDirection);
            }

            float patience = Math.Clamp(agent.PatienceBias, 0.65f, 1.35f);
            float assertiveness = Math.Clamp(agent.Assertiveness, 0f, 1f);
            float panic = Math.Clamp(agent.PanicLevel, 0f, 1f);
            float pressAfterSeconds = Math.Max(
                waitSeconds,
                _config.CasualtyPressAfterSeconds *
                patience *
                (1.15f - 0.45f * assertiveness) *
                (1f - 0.20f * panic));

            if (blockedSeconds >= pressAfterSeconds &&
                TryChooseCasualtyGapGoal(
                    simulation,
                    agent,
                    forward,
                    threatEscape,
                    pressMode: true,
                    out Vector2 pressGoal))
            {
                StartCasualtyManeuver(
                    simulation,
                    agent,
                    blocker.AgentId,
                    pressGoal,
                    LocalNavigationMode.PressingThrough,
                    _config.CasualtyGapSeekCommitSeconds);
                return VectorMath.SafeNormalize(pressGoal - agent.Position, goalDirection);
            }

            // No physically valid gap exists yet. Remaining still is preferable to deterministic
            // left/right thrashing or penetrating a casualty body; the next update checks again.
            SetLocalNavigationMode(simulation, agent, LocalNavigationMode.WaitingForGap, _config.Dt);
            holdPosition = true;
            return Vector2.Zero;
        }

        private float ComputeCasualtyWaitSeconds(Agent agent)
        {
            float patience = Math.Clamp(agent.PatienceBias, 0.65f, 1.35f);
            float normalizedPatience = Math.Clamp((patience - 0.65f) / 0.70f, 0f, 1f);
            float baseWait = _config.CasualtyGapWaitMinSeconds +
                (_config.CasualtyGapWaitMaxSeconds - _config.CasualtyGapWaitMinSeconds) *
                normalizedPatience;
            float urgency = Math.Clamp(
                0.70f * Math.Clamp(agent.PanicLevel, 0f, 1f) +
                0.30f * Math.Clamp((agent.UrgencyBias - 0.75f) / 0.50f, 0f, 1f),
                0f,
                1f);
            return Math.Max(
                0.25f,
                baseWait * (1f - 0.45f * urgency));
        }

        private bool TryChooseCasualtyGapGoal(
            Simulation simulation,
            Agent agent,
            Vector2 forward,
            bool threatEscape,
            bool pressMode,
            out Vector2 bestGoal)
        {
            bestGoal = default;
            if (forward.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon)
            {
                return false;
            }

            float[] anglesDegrees = pressMode
                ? new[] { 0f, 18f, -18f, 32f, -32f, 48f, -48f }
                : new[] { 45f, -45f, 70f, -70f, 95f, -95f, 120f, -120f, 135f, -135f };
            float probeDistance = Math.Max(
                _config.CasualtyGapProbeDistanceFactor * _config.Radius,
                pressMode ? 1.00f : 1.25f);
            float hardBodyClearance = 2.26f * _config.Radius;
            float casualtyClearance = pressMode
                ? hardBodyClearance + 0.01f
                : Math.Max(hardBodyClearance + 0.05f, 2.55f * _config.Radius);
            float occupancyRadius = Math.Max(3.0f * _config.Radius, 0.60f);
            float queryRadius = probeDistance + occupancyRadius + casualtyClearance;
            float currentExplosionDistance = simulation.Environment.Explosion is Explosion explosion
                ? Vector2.Distance(agent.Position, explosion.Position)
                : 0f;
            float maxBacktrack = Math.Max(0f, _config.ThreatEscapeCasualtyBypassMaxBacktrackMeters);
            int preferredSign = (agent.AgentId & 1) == 0 ? 1 : -1;
            float bestScore = float.NegativeInfinity;
            bool found = false;

            simulation.CollectAgentsNearPoint(
                agent.Position,
                queryRadius,
                _bypassScratch,
                agent.AgentId);

            foreach (float angleDegrees in anglesDegrees)
            {
                Vector2 direction = Rotate(forward, angleDegrees * (MathF.PI / 180f));
                Vector2 candidate = agent.Position + direction * probeDistance;
                if (!simulation.HasTraversablePath(agent.Position, candidate, NavigationClearance()))
                {
                    continue;
                }

                if (threatEscape && simulation.Environment.Explosion is Explosion currentExplosion)
                {
                    float candidateExplosionDistance = Vector2.Distance(candidate, currentExplosion.Position);
                    if (candidateExplosionDistance < currentExplosionDistance - maxBacktrack)
                    {
                        continue;
                    }
                }

                bool blockedByCasualty = false;
                float occupancyPenalty = 0f;
                foreach (Agent nearby in _bypassScratch)
                {
                    float distanceToGoal = Vector2.Distance(candidate, nearby.Position);
                    if (nearby.IsCasualtyObstacle)
                    {
                        if (distanceToGoal < casualtyClearance ||
                            DistancePointToSegment(
                                nearby.Position,
                                agent.Position,
                                candidate) < casualtyClearance)
                        {
                            blockedByCasualty = true;
                            break;
                        }
                    }

                    if (distanceToGoal < occupancyRadius)
                    {
                        float normalizedCrowding = Math.Clamp(
                            (occupancyRadius - distanceToGoal) / Math.Max(occupancyRadius, 1e-5f),
                            0f,
                            1f);
                        occupancyPenalty += nearby.IsCasualtyObstacle
                            ? 1.50f + 1.50f * normalizedCrowding
                            : 0.25f + 0.70f * normalizedCrowding;
                    }
                }

                if (blockedByCasualty)
                {
                    continue;
                }

                float alignment = Vector2.Dot(direction, forward);
                float backtrackPenalty = Math.Max(0f, -alignment);
                float clearanceScore = Math.Clamp(
                    EndpointClearance(simulation, candidate) /
                        Math.Max(4f * _config.Radius, 0.40f),
                    0f,
                    1f);
                int sign = angleDegrees >= 0f ? 1 : -1;
                float preferenceBonus = sign == preferredSign ? 0.02f : 0f;
                float score = pressMode
                    ? 1.10f * alignment + 0.50f * clearanceScore - 0.45f * occupancyPenalty + preferenceBonus
                    : 0.90f * clearanceScore + 0.50f * alignment - 0.30f * backtrackPenalty -
                        0.70f * occupancyPenalty + preferenceBonus;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestGoal = candidate;
                }
            }

            return found;
        }

        private void StartCasualtyManeuver(
            Simulation simulation,
            Agent agent,
            int blockerAgentId,
            Vector2 goal,
            LocalNavigationMode mode,
            float commitSeconds)
        {
            agent.SlowObstacleBypassAgentId = blockerAgentId;
            agent.SlowObstacleBypassGoal = goal;
            agent.SlowObstacleBypassUntilFrame = simulation.FrameIndex + SecondsToFrames(commitSeconds);
            agent.LocalNavigationBlockerAgentId = blockerAgentId;
            if (mode == LocalNavigationMode.CasualtyBypass)
            {
                agent.LocalNavigationBypassAttempts++;
            }
            SetLocalNavigationMode(simulation, agent, mode, commitSeconds);
            ClearSlowObstacleObservation(agent);
        }

        private void SetLocalNavigationMode(
            Simulation simulation,
            Agent agent,
            LocalNavigationMode mode,
            float commitSeconds)
        {
            if (agent.LocalNavigationMode != mode)
            {
                agent.LocalNavigationModeSinceFrame = simulation.FrameIndex;
            }

            agent.LocalNavigationMode = mode;
            agent.LocalNavigationModeUntilFrame = commitSeconds > 0f
                ? simulation.FrameIndex + SecondsToFrames(commitSeconds)
                : simulation.FrameIndex;
        }

        private static bool IsCommittedCasualtyManeuver(LocalNavigationMode mode)
            => mode is LocalNavigationMode.CasualtyBypass or
                LocalNavigationMode.SeekingGap or
                LocalNavigationMode.PressingThrough;

        private static bool IsCasualtyNavigationMode(LocalNavigationMode mode)
            => mode is LocalNavigationMode.CasualtyBypass or
                LocalNavigationMode.WaitingForGap or
                LocalNavigationMode.SeekingGap or
                LocalNavigationMode.PressingThrough;

        private static void ResetCasualtyBlockageEpisode(Agent agent)
        {
            ClearSlowObstacleBypass(agent);
            agent.LocalNavigationBlockerAgentId = null;
            agent.LocalNavigationBlockedSinceFrame = -1;
            agent.LocalNavigationBypassAttempts = 0;
            if (IsCasualtyNavigationMode(agent.LocalNavigationMode))
            {
                agent.LocalNavigationMode = LocalNavigationMode.Free;
                agent.LocalNavigationModeSinceFrame = -1;
                agent.LocalNavigationModeUntilFrame = -1;
            }
        }

        private bool TryChooseSlowCrawlerBypassGoal(
            Simulation simulation,
            Agent agent,
            Agent blocker,
            Vector2 forward,
            out Vector2 bestGoal)
        {
            bestGoal = default;
            bool found = false;
            float bestScore = float.PositiveInfinity;
            Vector2 lateral = VectorMath.Perpendicular(forward);
            float sideOffset = Math.Max(
                3.0f * _config.Radius,
                _config.SlowObstacleBypassLateralOffsetFactor * _config.Radius);
            float forwardOffset = Math.Max(
                0.25f * _config.Radius,
                _config.SlowObstacleBypassForwardOffsetFactor * _config.Radius);
            float dynamicClearance = Math.Max(
                2.26f * _config.Radius,
                _config.SlowObstacleBypassDynamicClearanceFactor * _config.Radius);
            float continuationDistance = Math.Max(2.5f * _config.Radius, 0.50f);
            float occupancyRadius = Math.Max(2.25f * _config.Radius, 0.45f);
            int preferredSign = (agent.AgentId & 1) == 0 ? 1 : -1;

            for (int pass = 0; pass < 2; pass++)
            {
                int sign = pass == 0 ? preferredSign : -preferredSign;
                Vector2 candidate = blocker.Position +
                    lateral * (sign * sideOffset) +
                    forward * forwardOffset;

                if (!simulation.HasTraversablePath(agent.Position, candidate, NavigationClearance()) ||
                    !simulation.HasTraversablePath(
                        candidate,
                        candidate + forward * continuationDistance,
                        NavigationClearance()))
                {
                    continue;
                }

                // Reject a chord that visually looks like a side-step but still cuts through the
                // crawler's body/contact envelope.
                if (DistancePointToSegment(blocker.Position, agent.Position, candidate) < dynamicClearance ||
                    Vector2.Distance(candidate, blocker.Position) < dynamicClearance)
                {
                    continue;
                }

                simulation.CollectAgentsNearPoint(candidate, occupancyRadius, _bypassScratch, agent.AgentId);
                float occupancyPenalty = 0f;
                bool blockedByCasualty = false;
                foreach (Agent nearby in _bypassScratch)
                {
                    if (nearby.AgentId == blocker.AgentId)
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(candidate, nearby.Position);
                    if (nearby.IsCasualtyObstacle && distance < dynamicClearance)
                    {
                        blockedByCasualty = true;
                        break;
                    }

                    float normalizedCrowding = Math.Clamp(
                        (occupancyRadius - distance) / Math.Max(occupancyRadius, 1e-5f),
                        0f,
                        1f);
                    occupancyPenalty += 0.35f + normalizedCrowding;
                }

                if (blockedByCasualty)
                {
                    continue;
                }

                // Tiny deterministic preference splits otherwise equivalent pedestrians between
                // both sides rather than sending every agent to the same bypass lane.
                float preferencePenalty = sign == preferredSign ? 0f : 0.03f;
                float score = occupancyPenalty + preferencePenalty;
                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestGoal = candidate;
                }
            }

            return found;
        }

        private bool TryChooseThreatEscapeCasualtyBypassGoal(
            Simulation simulation,
            Agent agent,
            Vector2 forward,
            out Vector2 bestGoal)
        {
            bestGoal = default;
            if (forward.LengthSquared() <= VectorMath.Epsilon * VectorMath.Epsilon)
            {
                return false;
            }

            // Candidate headings widen progressively from the current escape vector. The final
            // +/-120 degree options permit a short back-side step when a casualty pile fills the
            // forward hemisphere, but pure reversal is deliberately not sampled.
            ReadOnlySpan<float> anglesDegrees =
                stackalloc float[] { 35f, -35f, 60f, -60f, 90f, -90f, 120f, -120f };

            float probeDistance = Math.Max(
                _config.ThreatEscapeCasualtyBypassProbeDistanceFactor * _config.Radius,
                0.85f);
            float dynamicClearance = Math.Max(
                2.26f * _config.Radius,
                _config.SlowObstacleBypassDynamicClearanceFactor * _config.Radius);
            float occupancyRadius = Math.Max(
                _config.ThreatEscapeCasualtyBypassOccupancyRadiusFactor * _config.Radius,
                0.50f);
            float maxBacktrack = Math.Max(0f, _config.ThreatEscapeCasualtyBypassMaxBacktrackMeters);
            float currentExplosionDistance = simulation.Environment.Explosion is Explosion explosion
                ? Vector2.Distance(agent.Position, explosion.Position)
                : 0f;
            int preferredSign = (agent.AgentId & 1) == 0 ? 1 : -1;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            float localQueryRadius = probeDistance + occupancyRadius + dynamicClearance;
            simulation.CollectAgentsNearPoint(
                agent.Position,
                localQueryRadius,
                _bypassScratch,
                agent.AgentId);

            foreach (float angleDegrees in anglesDegrees)
            {
                float radians = angleDegrees * (MathF.PI / 180f);
                Vector2 direction = Rotate(forward, radians);
                Vector2 candidate = agent.Position + direction * probeDistance;

                if (!simulation.HasTraversablePath(agent.Position, candidate, NavigationClearance()))
                {
                    continue;
                }

                // A local detour may briefly give up radial progress, but never more than the
                // configured bounded backtrack. This prevents a clear space behind the pedestrian
                // from turning into sustained travel back toward the blast origin.
                if (simulation.Environment.Explosion is Explosion currentExplosion)
                {
                    float candidateExplosionDistance = Vector2.Distance(candidate, currentExplosion.Position);
                    if (candidateExplosionDistance < currentExplosionDistance - maxBacktrack)
                    {
                        continue;
                    }
                }

                bool blocked = false;
                float occupancyPenalty = 0f;
                foreach (Agent nearby in _bypassScratch)
                {
                    float distanceToGoal = Vector2.Distance(candidate, nearby.Position);
                    if (nearby.IsCasualtyObstacle)
                    {
                        // Reject both a destination inside a casualty envelope and a chord that
                        // would cut through a fallen/crawling body on the way to the clear space.
                        if (distanceToGoal < dynamicClearance ||
                            DistancePointToSegment(
                                nearby.Position,
                                agent.Position,
                                candidate) < dynamicClearance)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (distanceToGoal < occupancyRadius)
                    {
                        float normalizedCrowding = Math.Clamp(
                            (occupancyRadius - distanceToGoal) / Math.Max(occupancyRadius, 1e-5f),
                            0f,
                            1f);
                        occupancyPenalty += nearby.IsCasualtyObstacle
                            ? 2.0f + 2.0f * normalizedCrowding
                            : 0.30f + 0.80f * normalizedCrowding;
                    }
                }

                if (blocked)
                {
                    continue;
                }

                float alignment = Vector2.Dot(direction, forward);
                float backtrackPenalty = Math.Max(0f, -alignment);
                float clearanceScore = Math.Clamp(
                    EndpointClearance(simulation, candidate) /
                        Math.Max(4f * _config.Radius, 0.40f),
                    0f,
                    1f);
                int sign = angleDegrees >= 0f ? 1 : -1;
                float preferenceBonus = sign == preferredSign ? 0.025f : 0f;

                // Prefer useful forward/lateral progress, but let a much clearer 120-degree option
                // win when the forward hemisphere is occupied by casualties or dense crowding.
                float score =
                    1.05f * clearanceScore +
                    0.70f * alignment -
                    0.80f * backtrackPenalty -
                    0.70f * occupancyPenalty +
                    preferenceBonus;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestGoal = candidate;
                }
            }

            return found;
        }

        private static void ClearSlowObstacleObservation(Agent agent)
        {
            agent.SlowObstacleCandidateAgentId = null;
            agent.SlowObstacleCandidateSinceFrame = -1;
        }

        private static void ClearSlowObstacleBypass(Agent agent, bool clearObservation = true)
        {
            agent.SlowObstacleBypassAgentId = null;
            agent.SlowObstacleBypassGoal = null;
            agent.SlowObstacleBypassUntilFrame = -1;
            if (clearObservation)
            {
                ClearSlowObstacleObservation(agent);
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.LengthSquared();
            if (lengthSquared <= VectorMath.Epsilon * VectorMath.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
            Vector2 closest = start + segment * t;
            return Vector2.Distance(point, closest);
        }

        private float NavigationClearance()
            => Math.Max(0f, _config.Radius * _config.NavigationClearanceFactor);

        private void UpdateStuckProgress(Simulation simulation, Agent agent, float distanceToExit)
        {
            int windowFrames = Math.Max(
                1,
                (int)MathF.Ceiling(
                    Math.Max(_config.Dt, _config.StuckProgressWindowSeconds) / _config.Dt));

            if (agent.StuckProgressAnchorPosition is null ||
                agent.StuckProgressAnchorFrame < 0 ||
                !float.IsFinite(agent.StuckProgressAnchorExitDistance))
            {
                agent.StuckProgressAnchorPosition = agent.Position;
                agent.StuckProgressAnchorExitDistance = distanceToExit;
                agent.StuckProgressAnchorFrame = simulation.FrameIndex;
                agent.TimeStuck = 0f;
                return;
            }

            int elapsedFrames = simulation.FrameIndex - agent.StuckProgressAnchorFrame;
            if (elapsedFrames < windowFrames)
            {
                return;
            }

            float elapsedSeconds = Math.Max(_config.Dt, elapsedFrames * _config.Dt);
            float expectedTravel = Math.Max(agent.BaseSpeed, 0.004f) * elapsedSeconds;
            float goalImprovement = agent.StuckProgressAnchorExitDistance - distanceToExit;

            // Behaviour rebuild: sideways shuffling or rotation at an obstacle corner no longer
            // counts as progress. The anchor distance is measured to the current route waypoint,
            // so legitimate motion around a blockage still resets the stuck timer.
            bool meaningfulGoalProgress = goalImprovement >=
                expectedTravel * Math.Max(0f, _config.StuckMinimumExitProgressFactor);

            if (meaningfulGoalProgress)
            {
                agent.TimeStuck = 0f;
            }
            else
            {
                agent.TimeStuck += elapsedSeconds;
            }

            agent.StuckProgressAnchorPosition = agent.Position;
            agent.StuckProgressAnchorExitDistance = distanceToExit;
            agent.StuckProgressAnchorFrame = simulation.FrameIndex;
        }

        private static float EndpointClearance(Simulation simulation, Vector2 point)
        {
            float minimum = float.PositiveInfinity;
            foreach (Wall wall in simulation.Environment.Walls)
            {
                float distance = Vector2.Distance(point, wall.ClosestPoint(point));
                if (distance < minimum)
                {
                    minimum = distance;
                }
            }

            foreach (CircleBlockage circle in simulation.Environment.CircularBlockages)
            {
                float distance = Math.Max(0f, Vector2.Distance(point, circle.Center) - circle.Radius);
                if (distance < minimum)
                {
                    minimum = distance;
                }
            }

            return float.IsFinite(minimum) ? minimum : 0f;
        }

        private int RandomFrameRange(Random random, int minInclusive, int maxExclusive)
            => _config.ReferenceFramesToTicks(maxExclusive <= minInclusive
                ? minInclusive
                : minInclusive + (int)(random.NextDouble() * (maxExclusive - minInclusive)));

        private static bool SegmentIntersectsAabb(
            Vector2 start,
            Vector2 end,
            float minX,
            float minY,
            float maxX,
            float maxY)
        {
            Vector2 delta = end - start;
            float tMin = 0f;
            float tMax = 1f;

            static bool ClipAxis(
                float origin,
                float direction,
                float minimum,
                float maximum,
                ref float tMin,
                ref float tMax)
            {
                if (MathF.Abs(direction) < VectorMath.Epsilon)
                {
                    return origin >= minimum && origin <= maximum;
                }

                float inverse = 1f / direction;
                float t1 = (minimum - origin) * inverse;
                float t2 = (maximum - origin) * inverse;
                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                }

                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                return tMin <= tMax;
            }

            return ClipAxis(start.X, delta.X, minX, maxX, ref tMin, ref tMax) &&
                ClipAxis(start.Y, delta.Y, minY, maxY, ref tMin, ref tMax);
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            static float Cross(Vector2 p, Vector2 q, Vector2 r)
                => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

            float abC = Cross(a, b, c);
            float abD = Cross(a, b, d);
            float cdA = Cross(c, d, a);
            float cdB = Cross(c, d, b);
            return abC * abD < 0f && cdA * cdB < 0f;
        }
    }
}
