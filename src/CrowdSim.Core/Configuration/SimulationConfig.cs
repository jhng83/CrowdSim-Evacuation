using CrowdSim.Core.Model;

namespace CrowdSim.Core.Configuration;

/// <summary>
/// Top-level configuration object. This replaces the Python module-level WINDOW,
/// AGENT, COLORS and GRID singletons while preserving their default values.
/// </summary>
public sealed class SimulationConfig
{
    /// <summary>Opt-in adaptive movement and shared routing; false preserves the reference model.</summary>
    public bool HybridEnabled { get; set; }
    public WorldConfig World { get; } = new();
    public AgentConfig Agent { get; init; } = new();
    public ColorPalette Colors { get; } = new();
    public GridConfig Grid { get; }

    public SimulationConfig()
    {
        Grid = new GridConfig { CellSizeMeters = 6f * AgentConfig.RadiusMeters };
    }

    /// <summary>Create an independent configuration using the validated Python defaults.</summary>
    public static SimulationConfig CreateDefault() => new();
}

/// <summary>Metric simulation-world dimensions. All ground-plane coordinates are metres.</summary>
public sealed class WorldConfig
{
    public const float DefaultWidthMeters = 56.0f;
    public const float DefaultLengthMeters = 34.0f;

    public float WidthMeters { get; private set; } = DefaultWidthMeters;
    public float LengthMeters { get; private set; } = DefaultLengthMeters;
    public float MinWidthMeters { get; init; } = 10.0f;
    public float MinLengthMeters { get; init; } = 10.0f;
    public float MaxWidthMeters { get; init; } = 160.0f;
    public float MaxLengthMeters { get; init; } = 120.0f;

    /// <summary>Apply metric room dimensions. Display-window pixels are intentionally unrelated.</summary>
    public void ApplySize(float? widthMeters = null, float? lengthMeters = null)
    {
        if (widthMeters is not null)
        {
            if (!float.IsFinite(widthMeters.Value) || widthMeters.Value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(widthMeters));
            }

            WidthMeters = Math.Clamp(widthMeters.Value, MinWidthMeters, MaxWidthMeters);
        }

        if (lengthMeters is not null)
        {
            if (!float.IsFinite(lengthMeters.Value) || lengthMeters.Value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthMeters));
            }

            LengthMeters = Math.Clamp(lengthMeters.Value, MinLengthMeters, MaxLengthMeters);
        }
    }
}

/// <summary>Validated agent, SFM, panic, blast and flow constants.</summary>
public sealed class AgentConfig
{
    public int Count { get; init; } = 200;
    public const float HeightMeters = 1.70f;
    public const float RadiusMeters = 0.20f;

    /// <summary>Fixed standing height for every agent, in metres.</summary>
    public float Height => HeightMeters;

    /// <summary>Physical ground-plane body radius, in metres.</summary>
    public float Radius { get; init; } = RadiusMeters;

    /// <summary>Scenario-level controls for bounded behavioural heterogeneity.</summary>
    public BehaviorTuningConfig Behavior { get; } = new();

    public float BaseSpeedMin { get; init; } = 0.040f;
    public float BaseSpeedMax { get; init; } = 0.081f;
    public float PanicSpeedGain { get; init; } = 3.0f;
    public float EvacuationSpeedFloorMultiplier { get; init; } = 1.85f;
    public float FollowCrowdSpeedMultiplier { get; init; } = 2.05f;
    public float ThreatEscapeSpeedMultiplier { get; init; } = 2.65f;
    public float AssistanceApproachSpeedMultiplier { get; init; } = 1.30f;
    public float MaximumBehaviorSpeedMultiplier { get; init; } = 3.60f;
    public float ReactionDelayMin { get; init; } = 10.0f;
    public float ReactionDelayMax { get; init; } = 60.0f;
    public int FreezeMinFrames { get; init; } = 3;
    public int FreezeMaxFrames { get; init; } = 20;

    public float SfmA { get; init; } = 2000.0f;
    public float SfmB { get; init; } = 0.08f;
    public float SfmK { get; init; } = 120000.0f;
    public float SfmKappa { get; init; } = 240000.0f;
    public float RelaxationTime { get; init; } = 0.5f;
    public float SfmInteractionRadius { get; init; } = 2.43f;
    // Public velocity values retain the existing metres-per-reference-step convention.
    // Force calculations convert to SI; actual displacement scales with Dt below.
    public const float ReferenceStepSeconds = 0.15f;
    public float Dt { get; init; } = ReferenceStepSeconds;
    public float StepScale => Dt / ReferenceStepSeconds;
    /// <summary>Convert legacy duration constants to ticks at the selected Dt.</summary>
    public int ReferenceFramesToTicks(float frames)
        => Math.Max(1, (int)MathF.Ceiling(frames / StepScale));
    public float RetentionAtStep(float retention)
        => StepScale == 1f ? retention : MathF.Pow(retention, StepScale);
    public float MaxAccelerationMetersPerSecondSquared { get; init; } = 3.0f;
    /// <summary>Abstract admission limit per doorway lane, in people per second.</summary>
    public float ExitAdmissionsPerLanePerSecond { get; init; } = 1f / ReferenceStepSeconds;
    public float Mass { get; init; } = 80.0f;
    public float MaxSpeed { get; init; } = 0.607f;
    public float NeighbourRadius { get; init; } = 2.43f;

    public float VisionRange { get; init; } = 4.05f;
    public float VisionHalfAngleDeg { get; init; } = 45.0f;

    public float PanicThreatScale { get; init; } = 8.10f;
    public float PanicKThreat { get; init; } = 0.5f;
    public float PanicKSocial { get; init; } = 0.7f;
    public float PanicKRunning { get; init; } = 0.45f;
    // Legacy per-step decay is retained for configuration compatibility but the behavioural
    // rebuild uses the time-based decay constants below so results are stable if Dt changes.
    public float PanicKDecay { get; init; } = 0.1f;
    public float PanicDecayTimeConstantSeconds { get; init; } = 8.0f;
    public float PanicStimulusRiseSeconds { get; init; } = 0.20f;
    public float PanicActivationThreshold { get; init; } = 0.4f;

    // Only an agent inside a finite active-alarm window may transmit social/running
    // panic. Residual panic can remain after that window, but it is not a contagion source.
    public float PanicSocialSignalThreshold { get; init; } = 0.25f;
    public float DirectAlarmSignalSeconds { get; init; } = 5.0f;
    public float RelayedAlarmSignalSeconds { get; init; } = 4.0f;
    public float DirectAlarmPanicFloor { get; init; } = 0.50f;
    public float RelayedAlarmPanicFloor { get; init; } = 0.45f;
    public float SocialAlarmActivationStimulus { get; init; } = 0.16f;
    public float RunningAlarmActivationStimulus { get; init; } = 0.12f;
    public float SocialAlarmReactionDelayScale { get; init; } = 0.65f;

    // Behavioural emergency awareness is deliberately separate from emotional panic.
    // A coherent nearby flow of evacuees can make an unaware pedestrian start evacuating,
    // but it does not create or refresh a panic alarm episode.
    public float EvacuationAwarenessRadius { get; init; } = 3.85f;
    public int EvacuationAwarenessMinVisibleEvacuees { get; init; } = 2;
    public int EvacuationAwarenessCrowdOverrideCount { get; init; } = 4;
    public float EvacuationAwarenessMinSpeedFactor { get; init; } = 0.55f;
    public float EvacuationAwarenessCoherenceThreshold { get; init; } = 0.35f;
    public float EvacuationAwarenessReactionDelayScale { get; init; } = 0.35f;
    public float RunningContagionRadius { get; init; } = 3.85f;
    public float RunningContagionMinSpeedFactor { get; init; } = 1.30f;
    public int RunningContagionMinVisibleRunners { get; init; } = 2;
    public float RunningContagionCoherenceThreshold { get; init; } = 0.45f;
    public float DirectThreatDurationSeconds { get; init; } = 4.0f;
    public float DirectThreatDecaySeconds { get; init; } = 1.8f;
    public float HerdingLambdaMax { get; init; } = 0.8f;

    // Behaviour-model rebuild: heterogeneous, bounded emergency response. These are abstract
    // human-response timings/weights for the simulator and are not claims about any individual.
    public float ThreatEscapeMinSeconds { get; init; } = 0.8f;
    public float ThreatEscapeMaxSeconds { get; init; } = 2.4f;
    public float FollowCrowdCommitSeconds { get; init; } = 2.0f;
    public float FollowCrowdMinimumTendency { get; init; } = 0.42f;
    public float AssistanceMinimumTendency { get; init; } = 0.72f;
    public float AssistanceMaxPanic { get; init; } = 0.68f;
    public float AssistancePerceptionRadius { get; init; } = 3.04f;
    public float AssistanceReachRadiusFactor { get; init; } = 2.8f;
    public float AssistancePauseSeconds { get; init; } = 2.0f;
    public int AssistanceMaxHelpersPerCasualty { get; init; } = 2;

    // Assistance approach guardrails. Helpers use two body-clear positions on opposite
    // sides of a casualty instead of driving both centres toward the same coordinate.
    // Recovery is based on lack of meaningful progress toward the current assistance
    // position, not total approach duration. Sideways shuffling does not reset the timer.
    public float AssistanceSlotDistanceFactor { get; init; } = 2.55f;
    public float AssistanceSlotArrivalToleranceFactor { get; init; } = 0.80f;
    public float AssistanceMeaningfulProgressMeters { get; init; } = 0.05f;
    public float AssistanceReplanNoProgressSeconds { get; init; } = 2.0f;
    public float AssistanceAlternateSideNoProgressSeconds { get; init; } = 6.0f;
    public float AssistanceAbandonNoProgressSeconds { get; init; } = 10.0f;

    // Static-geometry route planning. A visibility graph over body-inflated blockage corners
    // supplies waypoints; SFM remains responsible for local contact and collision dynamics.
    public float RouteCornerBuffer { get; init; } = 0.20f;
    public float RouteWaypointReachFactor { get; init; } = 0.75f;
    public float RouteReplanSeconds { get; init; } = 2.0f;

    // Shared Explosion/Toxic Fume navigation refinement. These are tunable behavioural heuristics,
    // not empirically calibrated human reaction constants. Both hazard scenarios share recovery.
    public bool NaturalNavigationEnabled { get; init; } = true;
    public float NaturalDecisionSeconds { get; init; } = 0.30f;
    public float NaturalNoProgressSeconds { get; init; } = 1.65f;
    public float NaturalDetourCommitSeconds { get; init; } = 3.2f;
    public float NaturalExitReconsiderSeconds { get; init; } = 6.0f;
    public float NaturalFailureMemorySeconds { get; init; } = 12.0f;
    public float NaturalPerceptionRadiusMeters { get; init; } = 7.0f;
    public float NaturalCircleMaximumSeconds { get; init; } = 6.0f;
    public int NaturalRoutePlansPerStep { get; init; } = 4;
    public int NaturalRecoveryProbesPerStep { get; init; } = 24;
    /// <summary>Preference weight for locally perceived fume on a recovery/exit intention; not a toxicity constant.</summary>
    public float NaturalToxicRiskWeight { get; init; } = 5.0f;

    // Stage 3 perception / rerouting. These are abstract crowd-simulation parameters,
    // not real-world blast or medical calibration constants.
    public float DustVisibilityRadius { get; init; } = 12.14f;
    public float DustPeakVisibilityLoss { get; init; } = 0.65f;
    public float DustBuildSeconds { get; init; } = 0.6f;
    public float DustHoldSeconds { get; init; } = 2.5f;
    public float DustDecaySeconds { get; init; } = 8.0f;
    public float MinimumVisibilityFactor { get; init; } = 0.35f;
    public float RouteCommitmentSeconds { get; init; } = 3.5f;
    public float ExitSwitchImprovementRatio { get; init; } = 0.80f;
    public float ExitCongestionWeight { get; init; } = 0.36f;
    public float ExitCasualtyWeight { get; init; } = 0.97f;
    public float ExitOcclusionPenalty { get; init; } = 3.64f;
    public float ExitVisibilityPenalty { get; init; } = 2.83f;
    public float LocalCongestionRadius { get; init; } = 1.82f;
    public float FallenAvoidanceRadiusFactor { get; init; } = 6.0f;

    // Dynamic slow-casualty bypass. Crawlers remain physical SFM obstacles and panic/alarm
    // sources, but they are not evacuation-flow leaders. A pedestrian that stays directly
    // behind a materially slower crawler briefly commits to a body-clear side point when
    // static geometry and local occupancy leave usable lateral space.
    public float SlowObstacleBypassDetectionRadiusFactor { get; init; } = 6.5f;
    public float SlowObstacleBypassForwardCorridorFactor { get; init; } = 2.8f;
    public float SlowObstacleBypassObservationSeconds { get; init; } = 0.55f;
    public float SlowObstacleBypassLateralOffsetFactor { get; init; } = 3.5f;
    public float SlowObstacleBypassForwardOffsetFactor { get; init; } = 0.50f;
    public float SlowObstacleBypassCommitSeconds { get; init; } = 1.8f;
    public float SlowObstacleBypassMaxSpeedRatio { get; init; } = 0.70f;
    public float SlowObstacleBypassDynamicClearanceFactor { get; init; } = 1.60f;

    // During the short post-blast ThreatEscape phase, casualty piles may block the preferred
    // radial direction. A mobile survivor may make a brief lateral/back-side detour into clear
    // floor, but the maximum radial backtrack is bounded so this never becomes sustained travel
    // back toward the explosion. SFM still owns the actual person-person dynamics.
    public float ThreatEscapeCasualtyBypassProbeDistanceFactor { get; init; } = 5.5f;
    public float ThreatEscapeCasualtyBypassCommitSeconds { get; init; } = 1.2f;
    public float ThreatEscapeCasualtyBypassMaxBacktrackMeters { get; init; } = 0.45f;
    public float ThreatEscapeCasualtyBypassOccupancyRadiusFactor { get; init; } = 3.0f;

    // If no immediate body-clear bypass exists, stop left/right thrashing and use a bounded
    // human-like response sequence: wait briefly, seek a wider gap, then accept tighter social
    // spacing if a physically valid narrow gap appears. Hard body collision is never relaxed.
    public float CasualtyGapWaitMinSeconds { get; init; } = 0.45f;
    public float CasualtyGapWaitMaxSeconds { get; init; } = 1.30f;
    public float CasualtyGapSeekCommitSeconds { get; init; } = 1.35f;
    public float CasualtyPressAfterSeconds { get; init; } = 2.60f;
    public float CasualtyGapProbeDistanceFactor { get; init; } = 7.0f;
    public float CasualtyPressPersonalSpaceCompression { get; init; } = 0.24f;
    public float CasualtyPressSpeedFactor { get; init; } = 0.82f;

    // Curved static-obstacle recovery. An evacuating pedestrian close to a circular blockage
    // follows one tangent direction for a short commitment window instead of alternating
    // between desired-force penetration and collision push-out every frame.
    public float CircleFollowActivationDistanceFactor { get; init; } = 2.5f;
    public float CircleFollowProbeDistanceFactor { get; init; } = 6.0f;
    public float CircleFollowCommitSeconds { get; init; } = 1.80f;
    public float CircleFollowMinimumCommitSeconds { get; init; } = 0.40f;
    public float CircleFollowReleaseClearanceFactor { get; init; } = 2.0f;
    public float CircleFollowSpeedFactor { get; init; } = 0.92f;

    // Local exploration / wall-end recovery. A stuck pedestrian probes wall-clear
    // temporary waypoints instead of committing to a random wall tangent. The short
    // replan interval provides heading commitment and prevents left/right thrashing.
    public float ExplorationProbeDistance { get; init; } = 1.94f;
    public int ExplorationDirectionSamples { get; init; } = 16;
    public float ExplorationReplanSeconds { get; init; } = 1.35f;
    public float ExplorationLowSpeedFactor { get; init; } = 0.15f;
    public float ExplorationEndpointClearanceFactor { get; init; } = 0.75f;

    // Navigation uses body clearance rather than centre-point visibility. The stuck
    // detector samples net displacement over a short window so rotation/jitter at a
    // wall end does not masquerade as useful progress.
    public float NavigationClearanceFactor { get; init; } = 1.05f;
    public float StuckProgressWindowSeconds { get; init; } = 0.75f;
    public float StuckMinimumTravelFactor { get; init; } = 0.40f;
    public float StuckMinimumExitProgressFactor { get; init; } = 0.20f;

    // Exit-congestion behaviour. A rejected pedestrian is displaced once, then either
    // yields for a short panic-dependent interval or maintains forward pressure.
    // High panic never bypasses exit capacity, wall or collision guardrails.
    public float ExitPushPanicThreshold { get; init; } = 0.80f;
    public float ExitYieldMinimumSeconds { get; init; } = 0.20f;
    public float ExitYieldMaximumSeconds { get; init; } = 0.90f;
    public float ExitRetryJitterSeconds { get; init; } = 0.25f;
    public float ExitInitialSetbackMin { get; init; } = 0.08f;
    public float ExitInitialSetbackMax { get; init; } = 0.20f;

    // Final approach corridor. Once a body-clear route to the selected doorway is
    // recovered, the doorway target dominates herding so agents do not orbit a nearby
    // group instead of completing egress. Wide openings use stable per-agent lane targets.
    public float ExitCaptureRadius { get; init; } = 4.86f;
    public float ExitLaneSpreadFactor { get; init; } = 0.65f;
    public float ExitCaptureMotorBlend { get; init; } = 0.36f;

    // Bounded bottleneck-pressure model. These values govern local doorway interaction,
    // not global panic: dense delayed pedestrians may yield, seek a lateral gap, or press
    // while the physical body radius, force caps and exit-capacity guardrails remain active.
    public float BottleneckInfluenceRadius { get; init; } = 3.64f;
    public float BottleneckDensityReference { get; init; } = 7.0f;
    public float BottleneckMinimumCongestion { get; init; } = 0.18f;
    public float BottleneckWaitReferenceSeconds { get; init; } = 2.4f;
    public float BottleneckPressureRiseSeconds { get; init; } = 0.65f;
    public float BottleneckPressureDecaySeconds { get; init; } = 1.40f;
    public float BottleneckSeekGapThreshold { get; init; } = 0.36f;
    public float BottleneckPressThreshold { get; init; } = 0.68f;
    public float BottleneckPressReleaseThreshold { get; init; } = 0.50f;
    public float BottleneckModeCommitSeconds { get; init; } = 0.85f;
    public float BottleneckGapProbeDistanceFactor { get; init; } = 3.5f;
    public float BottleneckGapBlendMin { get; init; } = 0.18f;
    public float BottleneckGapBlendMax { get; init; } = 0.42f;
    public float BottleneckPressSpeedGain { get; init; } = 0.18f;
    public float BottleneckPressRelaxationReduction { get; init; } = 0.35f;

    public const float DefaultHardKillRadius = 1.58f;
    public const float DefaultHeatIncapacityRadius = 3.16f;
    public const float DefaultFallenRadius = 6.31f;
    public const float DefaultInjuryRadius = 9.47f;
    public const float DefaultHeatRadius = 18.21f;
    private const float MinimumBlastZoneRadius = 0.10f;
    private const float MaximumBlastZoneRadius = 200.0f;
    private const float MinimumBlastZoneGap = 0.10f;

    /// <summary>Inner abstract simulation zone that produces the terminal direct-blast outcome.</summary>
    public float HardKillRadius { get; private set; } = DefaultHardKillRadius;

    /// <summary>Abstract direct-blast temporary-incapacitation zone.</summary>
    public float HeatIncapacityRadius { get; private set; } = DefaultHeatIncapacityRadius;

    /// <summary>Abstract direct-blast severe/fallen zone.</summary>
    public float FallenRadius { get; private set; } = DefaultFallenRadius;

    /// <summary>Abstract direct-blast injury zone.</summary>
    public float InjuryRadius { get; private set; } = DefaultInjuryRadius;

    /// <summary>Outer direct-threat/panic zone and maximum propagating shock-front radius.</summary>
    public float HeatRadius { get; private set; } = DefaultHeatRadius;

    /// <summary>
    /// Set the five metric blast-zone radii while preserving strict inner-to-outer ordering.
    /// These are simulator tuning values, not real-world explosive-yield or medical calibration data.
    /// </summary>
    public void SetBlastZoneRadii(
        float hardKillRadius,
        float heatIncapacityRadius,
        float fallenRadius,
        float injuryRadius,
        float heatRadius)
    {
        if (!float.IsFinite(hardKillRadius) ||
            !float.IsFinite(heatIncapacityRadius) ||
            !float.IsFinite(fallenRadius) ||
            !float.IsFinite(injuryRadius) ||
            !float.IsFinite(heatRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(hardKillRadius), "Blast-zone radii must be finite.");
        }

        HardKillRadius = Math.Clamp(
            hardKillRadius,
            MinimumBlastZoneRadius,
            MaximumBlastZoneRadius - 4f * MinimumBlastZoneGap);
        HeatIncapacityRadius = Math.Clamp(
            heatIncapacityRadius,
            HardKillRadius + MinimumBlastZoneGap,
            MaximumBlastZoneRadius - 3f * MinimumBlastZoneGap);
        FallenRadius = Math.Clamp(
            fallenRadius,
            HeatIncapacityRadius + MinimumBlastZoneGap,
            MaximumBlastZoneRadius - 2f * MinimumBlastZoneGap);
        InjuryRadius = Math.Clamp(
            injuryRadius,
            FallenRadius + MinimumBlastZoneGap,
            MaximumBlastZoneRadius - MinimumBlastZoneGap);
        HeatRadius = Math.Clamp(
            heatRadius,
            InjuryRadius + MinimumBlastZoneGap,
            MaximumBlastZoneRadius);
    }

    /// <summary>Restore the validated default metric blast-zone radii.</summary>
    public void ResetBlastZoneRadii()
        => SetBlastZoneRadii(
            DefaultHardKillRadius,
            DefaultHeatIncapacityRadius,
            DefaultFallenRadius,
            DefaultInjuryRadius,
            DefaultHeatRadius);

    public float ShockwaveSpeedMetersPerFrame { get; init; } = 1.21f;
    public float ShockwavePeakImpulse { get; init; } = 0.121f;
    public float ShockwaveDecayLength { get; init; } = 6.07f;
    public int BlastWindDurationFrames { get; init; } = 40;
    public float BlastWindStrength { get; init; } = 0.032f;
    public float ExplosionRepulsionCoeff { get; init; } = 5000.0f;

    // Lightweight debris-impact model. Severity is normalized for this simulator and
    // deliberately avoids real ordnance/yield calculations.
    public int DebrisCount { get; init; } = 36;
    public float DebrisSpeedMin { get; init; } = 0.40f;
    public float DebrisSpeedMax { get; init; } = 1.42f;
    public float DebrisReferenceSpeed { get; init; } = 1.42f;
    public float DebrisEnergyDecay { get; init; } = 0.975f;
    public float DebrisInjuryThreshold { get; init; } = 0.28f;
    public float DebrisFallenThreshold { get; init; } = 0.48f;
    public float DebrisIncapacityThreshold { get; init; } = 0.68f;
    public float DebrisFatalThreshold { get; init; } = 0.86f;

    public float InjuredSpeedFactor { get; init; } = 0.4f;
    public float CrawlSpeedFactor { get; init; } = 0.25f;
    public int CrawlDelayMin { get; init; } = 30;
    public int CrawlDelayMax { get; init; } = 120;

    public float ExitCapacityBase { get; init; } = 3.0f;
    public float ExitDensityFree { get; init; } = 0.2f;
    public float ExitDensityJam { get; init; } = 2.5f;
    public float ExitInfluenceRadius { get; init; } = 2.43f;
    public float ExitMinSpeedFactor { get; init; } = 0.25f;
}

/// <summary>
/// Scenario-level behaviour controls. Defaults preserve the validated Behaviour Rebuild 1
/// response while allowing bounded tuning before agents are spawned. These values are
/// abstract simulation parameters and do not represent demographic assumptions.
/// </summary>
public sealed class BehaviorTuningConfig
{
    public const float DefaultTraitVariation = 0.65f;
    public const float DefaultReactionVariation = 0.75f;
    public const float DefaultStressSusceptibility = 1.00f;
    public const float DefaultStressPersistence = 1.00f;
    public const float DefaultCrowdFollowing = 0.50f;
    public const float DefaultPersonalSpace = 1.00f;
    public const float DefaultPanicSpaceCompression = 0.20f;
    public const float DefaultEmergencyUrgency = 1.00f;
    // A locomotion ceiling, not a new desired speed or a playback multiplier.
    public const float DefaultMaxRunningSpeedMetersPerSecond = 0.607f / AgentConfig.ReferenceStepSeconds;
    public const float MinimumRunningSpeedLimitMetersPerSecond = 0.50f;
    public const float MaximumRunningSpeedLimitMetersPerSecond = 6.00f;
    public const float DefaultHelpingTendency = 0.50f;
    public const float DefaultRouteFamiliarity = 0.50f;
    public const float DefaultBottleneckAssertiveness = 0.50f;
    public const float DefaultBottleneckPressureSensitivity = 1.00f;
    public const float DefaultBottleneckSpaceCompression = 0.18f;

    public float TraitVariation { get; private set; } = DefaultTraitVariation;
    public float ReactionVariation { get; private set; } = DefaultReactionVariation;
    public float StressSusceptibility { get; private set; } = DefaultStressSusceptibility;
    public float StressPersistence { get; private set; } = DefaultStressPersistence;
    public float CrowdFollowing { get; private set; } = DefaultCrowdFollowing;
    public float PersonalSpace { get; private set; } = DefaultPersonalSpace;
    public float PanicSpaceCompression { get; private set; } = DefaultPanicSpaceCompression;
    public float EmergencyUrgency { get; private set; } = DefaultEmergencyUrgency;
    /// <summary>Ceiling for voluntary movement, in physical metres per second.</summary>
    public float MaxRunningSpeedMetersPerSecond { get; private set; } = DefaultMaxRunningSpeedMetersPerSecond;
    public float MaxRunningSpeedPerReferenceStep => MaxRunningSpeedMetersPerSecond * AgentConfig.ReferenceStepSeconds;

    /// <summary>Reject invalid values and clamp the user control; independent of integration Dt.</summary>
    public void SetMaxRunningSpeed(float metresPerSecond)
    {
        if (!float.IsFinite(metresPerSecond)) throw new ArgumentOutOfRangeException(nameof(metresPerSecond));
        MaxRunningSpeedMetersPerSecond = Math.Clamp(metresPerSecond,
            MinimumRunningSpeedLimitMetersPerSecond, MaximumRunningSpeedLimitMetersPerSecond);
    }

    public float HelpingTendency { get; private set; } = DefaultHelpingTendency;
    public float RouteFamiliarity { get; private set; } = DefaultRouteFamiliarity;
    public float BottleneckAssertiveness { get; private set; } = DefaultBottleneckAssertiveness;
    public float BottleneckPressureSensitivity { get; private set; } = DefaultBottleneckPressureSensitivity;
    public float BottleneckSpaceCompression { get; private set; } = DefaultBottleneckSpaceCompression;

    /// <summary>Apply finite values while keeping every behavioural control inside its safe range.</summary>
    public void SetValues(
        float traitVariation,
        float reactionVariation,
        float stressSusceptibility,
        float stressPersistence,
        float crowdFollowing,
        float personalSpace,
        float panicSpaceCompression,
        float emergencyUrgency,
        float helpingTendency,
        float routeFamiliarity,
        float bottleneckAssertiveness = DefaultBottleneckAssertiveness,
        float bottleneckPressureSensitivity = DefaultBottleneckPressureSensitivity,
        float bottleneckSpaceCompression = DefaultBottleneckSpaceCompression)
    {
        float[] values =
        {
            traitVariation,
            reactionVariation,
            stressSusceptibility,
            stressPersistence,
            crowdFollowing,
            personalSpace,
            panicSpaceCompression,
            emergencyUrgency,
            helpingTendency,
            routeFamiliarity,
            bottleneckAssertiveness,
            bottleneckPressureSensitivity,
            bottleneckSpaceCompression,
        };
        if (values.Any(value => !float.IsFinite(value)))
        {
            throw new ArgumentOutOfRangeException(nameof(traitVariation), "Behaviour tuning values must be finite.");
        }

        TraitVariation = Math.Clamp(traitVariation, 0f, 1f);
        ReactionVariation = Math.Clamp(reactionVariation, 0f, 1f);
        StressSusceptibility = Math.Clamp(stressSusceptibility, 0.60f, 1.40f);
        StressPersistence = Math.Clamp(stressPersistence, 0.50f, 2.00f);
        CrowdFollowing = Math.Clamp(crowdFollowing, 0f, 1f);
        PersonalSpace = Math.Clamp(personalSpace, 0.85f, 1.30f);
        PanicSpaceCompression = Math.Clamp(panicSpaceCompression, 0f, 0.35f);
        EmergencyUrgency = Math.Clamp(emergencyUrgency, 0.80f, 1.30f);
        HelpingTendency = Math.Clamp(helpingTendency, 0f, 1f);
        RouteFamiliarity = Math.Clamp(routeFamiliarity, 0f, 1f);
        BottleneckAssertiveness = Math.Clamp(bottleneckAssertiveness, 0f, 1f);
        BottleneckPressureSensitivity = Math.Clamp(bottleneckPressureSensitivity, 0.50f, 1.50f);
        BottleneckSpaceCompression = Math.Clamp(bottleneckSpaceCompression, 0f, 0.35f);
    }

    public void Reset()
    {
        SetMaxRunningSpeed(DefaultMaxRunningSpeedMetersPerSecond);
        SetValues(
            DefaultTraitVariation,
            DefaultReactionVariation,
            DefaultStressSusceptibility,
            DefaultStressPersistence,
            DefaultCrowdFollowing,
            DefaultPersonalSpace,
            DefaultPanicSpaceCompression,
            DefaultEmergencyUrgency,
            DefaultHelpingTendency,
            DefaultRouteFamiliarity,
            DefaultBottleneckAssertiveness,
            DefaultBottleneckPressureSensitivity,
            DefaultBottleneckSpaceCompression);
    }
}

/// <summary>Legacy BGR color triplet preserved from the validated Python configuration.</summary>
public readonly record struct BgrColor(byte B, byte G, byte R);

/// <summary>Rendering colors carried over from config.py.</summary>
public sealed class ColorPalette
{
    public BgrColor Background { get; init; } = new(0, 0, 0);
    public BgrColor AgentWandering { get; init; } = new(255, 255, 255);
    public BgrColor AgentEvacuating { get; init; } = new(0, 255, 0);
    public BgrColor AgentExited { get; init; } = new(100, 100, 100);
    public BgrColor AgentIncapacitated { get; init; } = new(0, 0, 120);
    public BgrColor AgentInjured { get; init; } = new(255, 0, 0);
    public BgrColor AgentCrawling { get; init; } = new(0, 255, 255);
    public BgrColor AgentFallen { get; init; } = new(0, 0, 255);
    public BgrColor ExitColor { get; init; } = new(255, 0, 0);
    public BgrColor ExplosionColor { get; init; } = new(0, 0, 255);
    public BgrColor WallColor { get; init; } = new(128, 128, 128);
    public BgrColor TextColor { get; init; } = new(255, 255, 255);
}

/// <summary>Scene-builder grid settings.</summary>
public sealed class GridConfig
{
    public bool Enabled { get; init; } = true;
    /// <summary>Builder grid spacing, in metres.</summary>
    public float CellSizeMeters { get; init; } = 1.20f;
    public BgrColor Color { get; init; } = new(50, 50, 50);
    public int Thickness { get; init; } = 1;
}
