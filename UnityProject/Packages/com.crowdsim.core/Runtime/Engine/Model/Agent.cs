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

namespace CrowdSim.Core.Model
{
    /// <summary>
    /// A single simulated pedestrian, including panic, injury, exploration, wall-follow and
    /// explosion state. Fields deliberately mirror agents.py so behavioural guardrails can be
    /// audited one-to-one.
    /// </summary>
    public sealed class Agent
    {
        private readonly AgentConfig _config;

        public Agent(
            int agentId,
            Vector2 position,
            Vector2 velocity,
            float baseSpeed,
            float panicSpeedMultiplier,
            float reactionDelayFrames,
            DecisionStrategy strategy,
            AgentConfig config)
        {
            AgentId = agentId;
            Position = position;
            Velocity = velocity;
            BaseSpeed = baseSpeed;
            PanicSpeedMultiplier = panicSpeedMultiplier;
            ReactionDelayFrames = reactionDelayFrames;
            Strategy = strategy;
            Mass = config.Mass;
            _config = config;
        }

        public int AgentId { get; }
        public Vector2 Position { get; set; }
        /// <summary>Metres per ReferenceStepSeconds; independent of the chosen integration Dt.</summary>
        public Vector2 Velocity { get; set; }
        /// <summary>Physical velocity in metres per second.</summary>
        public Vector2 VelocityMetersPerSecond => Velocity / AgentConfig.ReferenceStepSeconds;
        public float BaseSpeed { get; }
        /// <summary>Seating never replaces the existing health/response state.</summary>
        public SeatingPosture Posture { get; internal set; }
        public FurnitureChair? Chair { get; internal set; }
        public float StandUpSeconds { get; internal set; }
        public float StandUpElapsed { get; internal set; }
        internal Vector2? SeatDepartureTarget { get; set; }
        public float StandUpProgress => StandUpSeconds > 0f ? Math.Clamp(StandUpElapsed / StandUpSeconds, 0f, 1f) : 0f;
        public float PanicSpeedMultiplier { get; }
        public float ReactionDelayFrames { get; }
        public DecisionStrategy Strategy { get; }
        public AgentState State { get; set; } = AgentState.Wandering;
        public BehaviorIntent Intent { get; set; } = BehaviorIntent.Unaware;
        public Exit? ExitTarget { get; set; }

        // Stable per-agent behavioural heterogeneity. These are abstract simulation traits,
        // not demographic stereotypes. They are sampled deterministically from the scenario seed.
        public float ThreatSensitivity { get; set; } = 1f;
        public float FollowTendency { get; set; } = 0.5f;
        public float HelpingTendency { get; set; } = 0.5f;
        public float RouteFamiliarity { get; set; } = 0.5f;
        public float UrgencyBias { get; set; } = 1f;
        public float StressRecoveryBias { get; set; } = 1f;
        public float PersonalSpaceBias { get; set; } = 1f;
        public float Assertiveness { get; set; } = 0.5f;
        public float PatienceBias { get; set; } = 1f;

        // Short-lived local bypass state for very slow crawling casualties. This is deliberately
        // separate from static A* routing: the crawler remains a dynamic SFM obstacle, while a
        // mobile pedestrian may commit briefly to a clear side point instead of shadowing it.
        public int? SlowObstacleCandidateAgentId { get; set; }
        public int SlowObstacleCandidateSinceFrame { get; set; } = -1;
        public int? SlowObstacleBypassAgentId { get; set; }
        public Vector2? SlowObstacleBypassGoal { get; set; }
        public int SlowObstacleBypassUntilFrame { get; set; } = -1;

        // Local navigation response is runtime-only and orthogonal to health/evacuation state.
        // Casualty blockage may progress through bypass -> wait -> seek gap -> controlled press.
        // Circular obstacles use a short tangent-follow commitment so the desired motor does not
        // repeatedly drive into the same curved surface after collision correction.
        public LocalNavigationMode LocalNavigationMode { get; set; } = CrowdSim.Core.Model.LocalNavigationMode.Free;
        public int LocalNavigationModeSinceFrame { get; set; } = -1;
        public int LocalNavigationModeUntilFrame { get; set; } = -1;
        public int? LocalNavigationBlockerAgentId { get; set; }
        public int LocalNavigationBlockedSinceFrame { get; set; } = -1;
        public int LocalNavigationBypassAttempts { get; set; }
        public Vector2? CircleFollowCenter { get; set; }
        public float CircleFollowObstacleRadius { get; set; }
        public int CircleFollowDirection { get; set; }

        // Bounded bottleneck interaction state. Pressure is generated by local density,
        // delayed progress and doorway load rather than panic alone. The selected mode is
        // committed briefly to prevent yield/seek/press oscillation every frame.
        public float BottleneckPressure { get; set; }
        public float BottleneckPressureTarget { get; set; }
        public float BottleneckWaitSeconds { get; set; }
        public BottleneckResponseMode BottleneckMode { get; set; } = BottleneckResponseMode.Free;
        public int BottleneckModeUntilFrame { get; set; } = -1;
        public int BottleneckLastUpdateFrame { get; set; } = -1;
        public Vector2 BottleneckLateralDirection { get; set; }

        // Behaviour commitment windows. Intent is intentionally separate from physical AgentState.
        public int IntentUntilFrame { get; set; } = -1;
        public int? AssistTargetAgentId { get; set; }
        public int AssistPauseUntilFrame { get; set; } = -1;

        // Runtime-only assistance approach state and diagnostics. The progress fields make
        // blocked approaches observable; slot/recovery fields also bound re-routing so a helper
        // cannot remain assigned indefinitely without reaching the casualty.
        public int AssistAssignedFrame { get; set; } = -1;
        public int AssistLastProgressFrame { get; set; } = -1;
        public float AssistBestDistanceMeters { get; set; } = float.PositiveInfinity;
        public Vector2? AssistProgressAnchorPosition { get; set; }
        public int AssistSlotIndex { get; set; } = -1;
        public int AssistRecoveryStage { get; set; }

        public bool AssistanceDecisionMade { get; set; }
        public float PanicLevel { get; set; }
        public float InjurySpeedFactor { get; set; } = 1f;

        // Runtime-only generic toxic-fume exposure. Concentration is normalized rather than tied
        // to a real chemical; cumulative exposure is concentration-seconds for within-simulator comparison.
        public float CurrentToxicConcentration { get; set; }
        public float PeakToxicConcentration { get; set; }
        public float CumulativeToxicExposure { get; set; }
        public float ToxicMobilityFactor { get; set; } = 1f;
        public int FirstToxicExposureFrame { get; set; } = -1;
        public ToxicExposureStatus ToxicExposureStatus { get; set; } = ToxicExposureStatus.Clear;

        public int FreezeUntilFrame { get; set; }
        public int CrawlUntilFrame { get; set; }
        public bool ShockwaveHit { get; set; }
        public int ShockwaveHitFrame { get; set; } = -1;
        public int? GroupId { get; set; }
        public Vector2? WanderDir { get; set; }

        /// <summary>Runtime-only navigation memory; never reset merely by assigning a new route.</summary>
        public NaturalNavigationMemory NavigationMemory { get; } = new();

        public float TimeStuck { get; set; }
        public float LastGoalDistance { get; set; } = float.PositiveInfinity;

        // Net-progress probe used by the wall-end deadlock detector. Unlike per-frame
        // distance jitter, this measures whether the body actually translated over a
        // short window before declaring that useful motion has resumed.
        public Vector2? StuckProgressAnchorPosition { get; set; }
        public float StuckProgressAnchorExitDistance { get; set; } = float.PositiveInfinity;
        public int StuckProgressAnchorFrame { get; set; } = -1;

        public bool Exploring { get; set; }
        public Vector2? TempGoal { get; set; }
        public int ExplorationReplanAfterFrame { get; set; }

        // Body-clear visibility-graph route. Waypoints are static-geometry guidance only; SFM
        // continues to handle person-person contacts, wall forces, casualties and local congestion.
        public List<Vector2> RouteWaypoints { get; } = new();
        public int RouteWaypointIndex { get; set; }
        public string? RouteExitName { get; set; }
        public int RouteReplanAfterFrame { get; set; } = -1;

        public int? WallFollowDir { get; set; }
        public int WallFollowUntil { get; set; }
        public float WallFollowDistance { get; set; }
        public bool WallFollowReversed { get; set; }
        public int WallFollowStartFrame { get; set; }
        public int WallFollowCooldownUntil { get; set; }
        public Vector2? WallFollowNormal { get; set; }
        public Vector2? WallFollowTangent { get; set; }
        public int WallStuckFrames { get; set; }
        public float LastExitDist { get; set; } = float.PositiveInfinity;

        public float Mass { get; set; }
        public BgrColor Color { get; set; } = new(255, 255, 255);

        // Python used dynamic attributes for these caches. They are explicit in C#.
        public bool BlastShielded { get; set; }
        public int StunnedUntilFrame { get; set; }
        public int LosLastCheckFrame { get; set; } = -999;
        public bool LosCached { get; set; }

        // Emergency-response / Stage 2 state. ReactionStartedFrame makes socially-triggered
        // agents wait from the moment they notice the emergency rather than from the global blast frame.
        public int ReactionStartedFrame { get; set; } = -1;
        public float ReactionDelayScale { get; private set; } = 1f;
        public int ExitCommitUntilFrame { get; set; }
        public int LastExitReselectFrame { get; set; } = -999;

        // Exit-congestion episode state. This is deliberately separate from AgentState so
        // an evacuating/injured pedestrian keeps the same semantic state and body colour.
        public bool ExitCongestionEpisodeActive { get; set; }
        public string? ExitCongestionExitName { get; set; }
        public bool ExitYielding { get; set; }
        public int ExitRetryAfterFrame { get; set; }
        public float ExitCongestionHoldingCoordinate { get; set; } = float.NaN;

        // Stage 2/3 diagnostics and perception. Values are bounded every update and are also
        // useful in the selected-agent UI without feeding renderer state back into physics.
        public float VisibilityFactor { get; set; } = 1f;
        public float LocalCongestion { get; set; }
        public float LastThreatPanicStimulus { get; set; }
        public float LastSocialPanicStimulus { get; set; }
        public float LastRunningPanicStimulus { get; set; }
        public float LastEvacuationAwarenessStimulus { get; set; }

        // Finite panic-source window. This is deliberately distinct from PanicLevel:
        // residual anxiety may remain after the agent stops being an active alarm source.
        // Social/running contagion can relay an alarm once, but cannot keep refreshing it
        // simply because already-alarmed pedestrians continue seeing one another.
        public int ActiveAlarmUntilFrame { get; private set; } = -1;
        public float ActiveAlarmPanicFloor { get; private set; }
        public int ActiveAlarmEpisodeId { get; private set; } = -1;
        public int LastRelayedAlarmEpisodeId { get; private set; } = -1;
        public int LastDirectAlarmEpisodeId { get; private set; } = -1;

        // Debug vectors consumed by the renderer. They do not alter physics.
        public Vector2 LastDesiredDirection { get; set; }
        public Vector2 LastSfmForce { get; set; }
        public Vector2 LastAvoidanceDirection { get; set; }
        public Vector2 LastHerdDirection { get; set; }

        public float DistanceTo(Vector2 point) => Vector2.Distance(Position, point);

        public Exit ChooseExit(IReadOnlyList<Exit> exits, IReadOnlyDictionary<string, int> crowdLoads)
        {
            if (exits.Count == 0)
            {
                throw new InvalidOperationException("Cannot choose an exit when no exits exist.");
            }

            if (Strategy == DecisionStrategy.Nearest)
            {
                return exits.OrderBy(exit => DistanceTo(exit.Position)).First();
            }

            return exits.OrderBy(exit => crowdLoads.TryGetValue(exit.Name, out int load) ? load : 0).First();
        }

        public void MarkExited()
        {
            State = AgentState.Exited;
            Intent = BehaviorIntent.Evacuate;
            Velocity = Vector2.Zero;
            ClearActiveAlarm();
            ClearRoute();
            ResetExitCongestion();
        }

        public void MarkInjured(bool preserveReactionState = false)
        {
            if (State is AgentState.Exited or AgentState.Incapacitated or AgentState.Dead)
            {
                return;
            }

            InjurySpeedFactor = _config.InjuredSpeedFactor;
            if (!(preserveReactionState && State == AgentState.WaitingToReact))
            {
                State = AgentState.Injured;
            }
        }

        public void MarkFallen(int currentFrame, Random random)
        {
            if (State is AgentState.Exited or AgentState.Incapacitated or AgentState.Dead)
            {
                return;
            }

            State = AgentState.Fallen;
            Intent = BehaviorIntent.Unaware;
            Velocity = Vector2.Zero;
            InjurySpeedFactor = 0f;
            ClearRoute();
            ResetExitCongestion();
            int delay = RandomRange(random, _config.CrawlDelayMin, _config.CrawlDelayMax);
            CrawlUntilFrame = currentFrame + _config.ReferenceFramesToTicks(delay);
        }

        public void MarkCrawling()
        {
            if (State is AgentState.Exited or AgentState.Incapacitated or AgentState.Dead)
            {
                return;
            }

            State = AgentState.Crawling;
            Intent = BehaviorIntent.Evacuate;
            InjurySpeedFactor = _config.CrawlSpeedFactor;
        }

        public void MarkIncapacitated()
        {
            if (State is AgentState.Exited or AgentState.Dead)
            {
                return;
            }

            State = AgentState.Incapacitated;
            Intent = BehaviorIntent.Unaware;
            Velocity = Vector2.Zero;
            InjurySpeedFactor = 0f;
            ClearRoute();
            ResetExitCongestion();
        }

        /// <summary>Mark a terminal fatality while leaving the body in the world as an obstacle.</summary>
        public void MarkDead()
        {
            if (State == AgentState.Exited)
            {
                return;
            }

            State = AgentState.Dead;
            Intent = BehaviorIntent.Unaware;
            Velocity = Vector2.Zero;
            InjurySpeedFactor = 0f;
            PanicLevel = 0f;
            ClearActiveAlarm();
            ClearRoute();
            ResetExitCongestion();
        }

        /// <summary>
        /// Start or refresh a direct alarm from the physical event. Direct exposure may
        /// upgrade a previously relayed alarm and keeps the original event episode id.
        /// </summary>
        public void ActivateDirectAlarm(
            int currentFrame,
            int durationFrames,
            float panicFloor,
            int episodeId)
        {
            int boundedDuration = Math.Max(1, durationFrames);
            ActiveAlarmUntilFrame = Math.Max(
                ActiveAlarmUntilFrame,
                currentFrame + boundedDuration);
            ActiveAlarmPanicFloor = Math.Max(
                ActiveAlarmPanicFloor,
                Math.Clamp(panicFloor, 0f, 1f));
            ActiveAlarmEpisodeId = episodeId;
            LastDirectAlarmEpisodeId = episodeId;
        }

        /// <summary>
        /// Relay an alarm episode once. An agent cannot be socially re-activated by the
        /// same event after its relay window expires, preventing ping-pong panic loops.
        /// </summary>
        public bool TryActivateRelayedAlarm(
            int currentFrame,
            int durationFrames,
            float panicFloor,
            int episodeId)
        {
            if (episodeId < 0 ||
                LastRelayedAlarmEpisodeId == episodeId ||
                LastDirectAlarmEpisodeId == episodeId ||
                IsActiveAlarmSource(currentFrame))
            {
                return false;
            }

            int boundedDuration = Math.Max(1, durationFrames);
            ActiveAlarmUntilFrame = currentFrame + boundedDuration;
            ActiveAlarmPanicFloor = Math.Clamp(panicFloor, 0f, 1f);
            ActiveAlarmEpisodeId = episodeId;
            LastRelayedAlarmEpisodeId = episodeId;
            return true;
        }

        /// <summary>Whether this agent is currently allowed to transmit alarm contagion.</summary>
        public bool IsActiveAlarmSource(int currentFrame) => currentFrame <= ActiveAlarmUntilFrame;

        /// <summary>Expire the finite alarm source while leaving residual panic to decay naturally.</summary>
        public void ExpireAlarmIfNeeded(int currentFrame)
        {
            if (currentFrame > ActiveAlarmUntilFrame)
            {
                ActiveAlarmPanicFloor = 0f;
                ActiveAlarmEpisodeId = -1;
            }
        }

        private void ClearActiveAlarm()
        {
            ActiveAlarmUntilFrame = -1;
            ActiveAlarmPanicFloor = 0f;
            ActiveAlarmEpisodeId = -1;
            LastRelayedAlarmEpisodeId = -1;
            LastDirectAlarmEpisodeId = -1;
        }

        /// <summary>Begin the bounded startle/reaction sequence at the frame the emergency is perceived.</summary>
        public void BeginReaction(int currentFrame, int freezeUntilFrame, float reactionDelayScale = 1f)
        {
            if (State is AgentState.Exited or AgentState.Dead or AgentState.Incapacitated or AgentState.TempIncap)
            {
                return;
            }

            if (State == AgentState.Wandering)
            {
                State = AgentState.WaitingToReact;
                Intent = BehaviorIntent.Orienting;
            }

            if (ReactionStartedFrame < 0)
            {
                ReactionStartedFrame = currentFrame;
            }

            ReactionDelayScale = Math.Min(
                ReactionDelayScale,
                Math.Clamp(reactionDelayScale, 0.05f, 1f));
            FreezeUntilFrame = Math.Max(FreezeUntilFrame, freezeUntilFrame);
        }

        public void MarkTempIncap(int currentFrame, Random random)
        {
            State = AgentState.TempIncap;
            Intent = BehaviorIntent.Unaware;
            Velocity = Vector2.Zero;
            InjurySpeedFactor = 0f;
            ClearRoute();
            ResetExitCongestion();
            int delay = RandomRange(random, _config.CrawlDelayMin, _config.CrawlDelayMax);
            CrawlUntilFrame = currentFrame + _config.ReferenceFramesToTicks(delay);
        }

        /// <summary>Clear cached static-geometry route guidance.</summary>
        internal int HybridGroupLeaderId { get; set; } = -1;
        internal int HybridRegroupAfterFrame { get; set; } = -1;
        internal int HybridLastPanicFrame { get; set; } = -1;

        internal int LastChairPushFrame { get; set; } = -1;
        internal Vector2 LastChairPushDirection { get; set; }

        internal string? FailedRouteKey { get; set; }
        internal int FailedRouteRetryFrame { get; set; } = -1;

        internal int NextSeatDepartureProbeFrame { get; set; } = -1;

        internal int NextOpenSpaceProbeFrame { get; set; } = -1;

        public void ClearRoute()
        {
            RouteWaypoints.Clear();
            RouteWaypointIndex = 0;
            RouteExitName = null;
            RouteReplanAfterFrame = -1;
        }

        /// <summary>Replace the body-clear route for the current exit.</summary>
        public void SetRoute(IEnumerable<Vector2> waypoints, string exitName, int replanAfterFrame)
        {
            RouteWaypoints.Clear();
            RouteWaypoints.AddRange(waypoints);
            RouteWaypointIndex = 0;
            RouteExitName = exitName;
            RouteReplanAfterFrame = replanAfterFrame;
        }

        public Vector2? CurrentRouteWaypoint
            => RouteWaypointIndex >= 0 && RouteWaypointIndex < RouteWaypoints.Count
                ? RouteWaypoints[RouteWaypointIndex]
                : null;

        public void AdvanceRouteWaypoint()
        {
            if (RouteWaypointIndex < RouteWaypoints.Count)
            {
                RouteWaypointIndex++;
            }
        }

        public void ResetExitCongestion()
        {
            ExitCongestionEpisodeActive = false;
            ExitCongestionExitName = null;
            ExitYielding = false;
            ExitRetryAfterFrame = 0;
            ExitCongestionHoldingCoordinate = float.NaN;
            ResetBottleneckInteraction();
        }

        /// <summary>Clear transient doorway pressure and response commitment.</summary>
        public void ResetBottleneckInteraction()
        {
            BottleneckPressure = 0f;
            BottleneckPressureTarget = 0f;
            BottleneckWaitSeconds = 0f;
            BottleneckMode = BottleneckResponseMode.Free;
            BottleneckModeUntilFrame = -1;
            BottleneckLastUpdateFrame = -1;
            BottleneckLateralDirection = Vector2.Zero;
        }

        /// <summary>True while the agent/body remains present in the simulated room.</summary>
        public bool IsActive => State != AgentState.Exited;

        /// <summary>Whether the agent can currently contribute a visible social panic signal.</summary>
        public bool CanSignalPanic => State is AgentState.Evacuating or AgentState.Injured or AgentState.Crawling;

        /// <summary>Static/slow casualty states that other pedestrians should locally route around.</summary>
        public bool IsCasualtyObstacle => State is AgentState.Fallen or AgentState.TempIncap or
            AgentState.Incapacitated or AgentState.Dead or AgentState.Crawling;

        public float EffectiveReactionDelayFrames => ReactionDelayFrames * ReactionDelayScale;

        public float CurrentDesiredSpeed
        {
            get
            {
                if (Posture != SeatingPosture.Walking) return 0f;
                float panic = Math.Clamp(PanicLevel, 0f, 1f);
                if (State == AgentState.Crawling)
                {
                    // Retain the former smoothed crawler's steady-state speed (0.28 / 0.58).
                    return Math.Min(BaseSpeed * _config.CrawlSpeedFactor * (1f + 0.5f * panic) *
                        (0.28f / 0.58f), _config.Behavior.MaxRunningSpeedPerReferenceStep) *
                        Math.Clamp(ToxicMobilityFactor, 0f, 1f);
                }
                float urgencyScale = _config.Behavior.EmergencyUrgency;
                float multiplier = 1f + _config.PanicSpeedGain * urgencyScale * panic;

                if (State is AgentState.Evacuating or AgentState.Injured)
                {
                    float intentFloor = Intent switch
                    {
                        BehaviorIntent.ThreatEscape => _config.ThreatEscapeSpeedMultiplier,
                        BehaviorIntent.FollowCrowd => _config.FollowCrowdSpeedMultiplier,
                        BehaviorIntent.AssistCasualty => _config.AssistanceApproachSpeedMultiplier,
                        _ => _config.EvacuationSpeedFloorMultiplier,
                    };
                    intentFloor *= urgencyScale;

                    // Panic raises urgency, but purposeful evacuation remains brisk even after acute
                    // stress subsides. The bounded per-agent urgency bias avoids synchronized motion.
                    multiplier = Math.Max(intentFloor, multiplier);
                    multiplier *= Math.Clamp(UrgencyBias, 0.75f, 1.25f);

                    float bottleneckModeFactor = BottleneckMode switch
                    {
                        BottleneckResponseMode.SeekGap => 0.45f,
                        BottleneckResponseMode.Press => 1.00f,
                        _ => 0f,
                    };
                    if (bottleneckModeFactor > 0f)
                    {
                        float pressure = Math.Clamp(BottleneckPressure, 0f, 1f);
                        multiplier *= 1f +
                            _config.BottleneckPressSpeedGain *
                            _config.Behavior.BottleneckPressureSensitivity *
                            pressure *
                            bottleneckModeFactor;
                    }

                    multiplier = Math.Min(multiplier, _config.MaximumBehaviorSpeedMultiplier);
                }

                float localNavigationSpeedFactor = LocalNavigationMode switch
                {
                    CrowdSim.Core.Model.LocalNavigationMode.WaitingForGap => 0f,
                    CrowdSim.Core.Model.LocalNavigationMode.SeekingGap => 0.90f,
                    CrowdSim.Core.Model.LocalNavigationMode.PressingThrough => _config.CasualtyPressSpeedFactor,
                    CrowdSim.Core.Model.LocalNavigationMode.CircleFollow => _config.CircleFollowSpeedFactor,
                    _ => 1f,
                };

                // Cap healthy purposeful locomotion first, so injury, fumes and local waiting
                // can still reduce it. Raising the ceiling does not make slower agents run faster.
                float cappedSpeed = Math.Min(BaseSpeed * multiplier, _config.Behavior.MaxRunningSpeedPerReferenceStep);
                return cappedSpeed * InjurySpeedFactor *
                    Math.Clamp(ToxicMobilityFactor, 0f, 1f) *
                    localNavigationSpeedFactor;
            }
        }

        private static int RandomRange(Random random, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + (int)(random.NextDouble() * (maxExclusive - minInclusive));
        }
    }
}
