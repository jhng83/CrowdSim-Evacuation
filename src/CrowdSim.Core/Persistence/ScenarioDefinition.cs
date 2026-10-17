using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;

namespace CrowdSim.Core.Persistence;

/// <summary>
/// Versioned, configuration-only scenario description. Builder-defined initial spawn positions
/// are scenario configuration and are persisted. Runtime-moved positions, injuries, panic, debris,
/// elapsed time and exited agents are intentionally excluded; those belong to a future snapshot feature.
/// </summary>
public sealed class ScenarioDefinition
{
    public const int CurrentVersion = 4;
    public const float LegacyMetersPerUnit = AgentConfig.HeightMeters / 42f;

    public int Version { get; init; } = CurrentVersion;
    public string Name { get; init; } = "Scenario";
    public int AgentCount { get; init; } = 200;
    public bool HybridEnabled { get; init; }
    public int RandomSeed { get; init; }
    public float RoomWidth { get; init; }
    public float RoomHeight { get; init; }
    public ScenarioType ScenarioType { get; init; } = CrowdSim.Core.Model.ScenarioType.Explosion;
    public ToxicFumeScenario ToxicFume { get; init; } = new();
    public BlastZoneScenario BlastZones { get; init; } = new();
    public BehaviorScenario Behavior { get; init; } = new();
    public List<ExitScenario> Exits { get; init; } = new();
    public List<BlockageScenario> Blockages { get; init; } = new();
    public List<FurnitureScenario> Furniture { get; init; } = new();
    public List<CircleBlockageScenario> CircularBlockages { get; init; } = new();
    public List<BlockageScenario> BlueWalls { get; init; } = new();
    public List<AgentSpawnGroupScenario> AgentSpawnGroups { get; init; } = new();

    /// <summary>Capture the current scenario configuration without runtime agent state.</summary>
    public static ScenarioDefinition Capture(
        Simulation simulation,
        int requestedAgentCount,
        string name)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        AgentConfig agent = simulation.Config.Agent;
        int plannedCount = simulation.Environment.PlannedAgentCount;
        int count = plannedCount > 0
            ? plannedCount
            : simulation.Agents.Count > 0
                ? simulation.Agents.Count
                : Math.Clamp(requestedAgentCount, 0, 5000);

        return new ScenarioDefinition
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Scenario" : name.Trim(),
            AgentCount = count,
            RandomSeed = simulation.RandomSeed,
            HybridEnabled = simulation.Config.HybridEnabled,
            RoomWidth = simulation.Config.World.WidthMeters,
            RoomHeight = simulation.Config.World.LengthMeters,
            ScenarioType = simulation.ScenarioType,
            ToxicFume = ToxicFumeScenario.Capture(simulation),
            BlastZones = new BlastZoneScenario
            {
                Kill = agent.HardKillRadius,
                Incapacitation = agent.HeatIncapacityRadius,
                Fallen = agent.FallenRadius,
                Injury = agent.InjuryRadius,
                Panic = agent.HeatRadius,
            },
            Behavior = BehaviorScenario.Capture(agent.Behavior),
            Exits = simulation.Environment.Exits
                .Select(exit => new ExitScenario
                {
                    Name = exit.Name,
                    X = exit.Position.X,
                    Y = exit.Position.Y,
                    Size = exit.Size,
                    Side = exit.Side,
                })
                .ToList(),
            Blockages = simulation.Environment.Blockages
                .Select(wall => new BlockageScenario
                {
                    X1 = wall.X1,
                    Y1 = wall.Y1,
                    X2 = wall.X2,
                    Y2 = wall.Y2,
                })
                .ToList(),
            Furniture = simulation.Environment.Furniture.Select(set => new FurnitureScenario
            { Kind = set.Kind, X = set.Center.X, Y = set.Center.Y, Rotation = set.Rotation }).ToList(),
            CircularBlockages = simulation.Environment.CircularBlockages.Where(circle => !circle.IsFurniture)
                .Select(circle => new CircleBlockageScenario
                {
                    CenterX = circle.Center.X,
                    CenterY = circle.Center.Y,
                    Radius = circle.Radius,
                })
                .ToList(),
            BlueWalls = simulation.Environment.BlueWalls
                .Select(wall => new BlockageScenario
                {
                    X1 = wall.X1,
                    Y1 = wall.Y1,
                    X2 = wall.X2,
                    Y2 = wall.Y2,
                })
                .ToList(),
            AgentSpawnGroups = simulation.Environment.AgentSpawnGroups
                .Select(group => new AgentSpawnGroupScenario
                {
                    CenterX = group.Center.X,
                    CenterY = group.Center.Y,
                    Positions = group.Positions
                        .Select(position => new AgentStartScenario
                        {
                            X = position.X,
                            Y = position.Y,
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    /// <summary>Create a fresh default config and apply this scenario's configurable radii.</summary>
    public SimulationConfig CreateConfig()
    {
        ValidateBasic();
        SimulationConfig config = SimulationConfig.CreateDefault();
        config.HybridEnabled = HybridEnabled;
        if (RoomWidth > 0 && RoomHeight > 0 &&
            (RoomWidth != config.World.WidthMeters || RoomHeight != config.World.LengthMeters))
        {
            config.World.ApplySize(RoomWidth, RoomHeight);
        }

        config.Agent.SetBlastZoneRadii(
            BlastZones.Kill,
            BlastZones.Incapacitation,
            BlastZones.Fallen,
            BlastZones.Injury,
            BlastZones.Panic);
        Behavior.Apply(config.Agent.Behavior);
        return config;
    }

    /// <summary>
    /// Apply saved exits, regular blockages, blue walls and planned spawn groups to a fresh
    /// simulation. Agents are intentionally not spawned; the builder finalises the loaded scenario
    /// so optional edits can occur before deterministic population creation.
    /// </summary>
    public void ApplyLayout(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ValidateAgainst(simulation.Config);

        List<Exit> exits = Exits
            .Select(item => new Exit(
                item.Name,
                new System.Numerics.Vector2(item.X, item.Y),
                item.Size,
                item.Side))
            .ToList();
        List<Wall> blockages = Blockages
            .Select(item => new Wall(item.X1, item.Y1, item.X2, item.Y2))
            .ToList();
        List<CircleBlockage> circularBlockages = CircularBlockages
            .Select(item => new CircleBlockage(
                new System.Numerics.Vector2(item.CenterX, item.CenterY),
                item.Radius))
            .ToList();
        List<Wall> blueWalls = BlueWalls
            .Select(item => new Wall(item.X1, item.Y1, item.X2, item.Y2))
            .ToList();
        List<AgentSpawnGroup> spawnGroups = AgentSpawnGroups
            .Select(group => new AgentSpawnGroup(
                new System.Numerics.Vector2(group.CenterX, group.CenterY),
                group.Positions.Select(position => new System.Numerics.Vector2(position.X, position.Y))))
            .ToList();

        // Restore physical furniture first. Builder placement reserves additional departure
        // space, but standing groups placed later are allowed to occupy that free space.
        simulation.Environment.ReplaceLayout(exits, blockages, circularBlockages, blueWalls);
        foreach (FurnitureScenario set in Furniture)
        {
            if (!simulation.Environment.RestoreFurniture(set.Kind, new System.Numerics.Vector2(set.X, set.Y), set.Rotation))
                throw new InvalidOperationException("Saved furniture physically overlaps geometry or room bounds.");
        }
        foreach (AgentSpawnGroup group in spawnGroups)
        foreach (System.Numerics.Vector2 position in group.Positions)
        {
            float margin = simulation.Config.Agent.Radius * 1.08f;
            if (simulation.Environment.CircularBlockages.Any(circle => circle.IsFurniture &&
                circle.Contains(position, margin)) ||
                simulation.Environment.Furniture.Any(set => set.StaticObstacle is Wall wall &&
                    wall.Contains(position, margin)))
                throw new InvalidOperationException("A saved standing occupant physically overlaps furniture.");
        }
        simulation.Environment.AgentSpawnGroups.AddRange(spawnGroups);
        simulation.BuildWallGrid();
        simulation.Config.HybridEnabled = HybridEnabled;
        simulation.SetScenarioType(ScenarioType);
        if (ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume)
        {
            ToxicFume.Apply(simulation.ToxicFumeSystem.Configuration);
            simulation.ToxicFumeSystem.RestoreSource(ToxicFume.HasSource
                ? new System.Numerics.Vector2(ToxicFume.SourceX, ToxicFume.SourceY)
                : null);
        }
    }

    /// <summary>Convert a version-1 pixel/world-unit scenario into the version-2 metric schema.</summary>
    public static ScenarioDefinition UpgradeLegacyV1(ScenarioDefinition legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        if (legacy.Version != 1)
        {
            return legacy;
        }

        const float scale = LegacyMetersPerUnit;
        return new ScenarioDefinition
        {
            Version = 2,
            Name = legacy.Name,
            AgentCount = legacy.AgentCount,
            RandomSeed = legacy.RandomSeed,
            RoomWidth = legacy.RoomWidth * scale,
            RoomHeight = legacy.RoomHeight * scale,
            ScenarioType = CrowdSim.Core.Model.ScenarioType.Explosion,
            ToxicFume = new ToxicFumeScenario(),
            BlastZones = new BlastZoneScenario
            {
                Kill = legacy.BlastZones.Kill * scale,
                Incapacitation = legacy.BlastZones.Incapacitation * scale,
                Fallen = legacy.BlastZones.Fallen * scale,
                Injury = legacy.BlastZones.Injury * scale,
                Panic = legacy.BlastZones.Panic * scale,
            },
            Behavior = legacy.Behavior,
            Exits = legacy.Exits.Select(exit => new ExitScenario
            {
                Name = exit.Name,
                X = exit.X * scale,
                Y = exit.Y * scale,
                Size = exit.Size * scale,
                Side = exit.Side,
            }).ToList(),
            Blockages = legacy.Blockages.Select(wall => new BlockageScenario
            {
                X1 = wall.X1 * scale,
                Y1 = wall.Y1 * scale,
                X2 = wall.X2 * scale,
                Y2 = wall.Y2 * scale,
            }).ToList(),
            CircularBlockages = new List<CircleBlockageScenario>(),
            BlueWalls = legacy.BlueWalls.Select(wall => new BlockageScenario
            {
                X1 = wall.X1 * scale,
                Y1 = wall.Y1 * scale,
                X2 = wall.X2 * scale,
                Y2 = wall.Y2 * scale,
            }).ToList(),
            AgentSpawnGroups = legacy.AgentSpawnGroups.Select(group => new AgentSpawnGroupScenario
            {
                CenterX = group.CenterX * scale,
                CenterY = group.CenterY * scale,
                Positions = group.Positions.Select(position => new AgentStartScenario
                {
                    X = position.X * scale,
                    Y = position.Y * scale,
                }).ToList(),
            }).ToList(),
        };
    }

    /// <summary>Upgrade version-2 metric scenarios by assigning the historical Explosion mode.</summary>
    public static ScenarioDefinition UpgradeLegacyV2(ScenarioDefinition legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        if (legacy.Version != 2)
        {
            return legacy;
        }

        return new ScenarioDefinition
        {
            Version = CurrentVersion,
            Name = legacy.Name,
            AgentCount = legacy.AgentCount,
            RandomSeed = legacy.RandomSeed,
            RoomWidth = legacy.RoomWidth,
            RoomHeight = legacy.RoomHeight,
            ScenarioType = CrowdSim.Core.Model.ScenarioType.Explosion,
            ToxicFume = new ToxicFumeScenario(),
            BlastZones = legacy.BlastZones,
            Behavior = legacy.Behavior,
            Exits = legacy.Exits,
            Blockages = legacy.Blockages,
            CircularBlockages = legacy.CircularBlockages,
            BlueWalls = legacy.BlueWalls,
            AgentSpawnGroups = legacy.AgentSpawnGroups,
        };
    }

    /// <summary>Upgrade version-3 scenarios by adding an empty furniture layout.</summary>
    public static ScenarioDefinition UpgradeLegacyV3(ScenarioDefinition legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        if (legacy.Version != 3)
        {
            return legacy;
        }

        return new ScenarioDefinition
        {
            Version = CurrentVersion,
            Name = legacy.Name,
            AgentCount = legacy.AgentCount,
            RandomSeed = legacy.RandomSeed,
            RoomWidth = legacy.RoomWidth,
            RoomHeight = legacy.RoomHeight,
            ScenarioType = legacy.ScenarioType,
            ToxicFume = legacy.ToxicFume,
            BlastZones = legacy.BlastZones,
            Behavior = legacy.Behavior,
            Exits = legacy.Exits,
            Blockages = legacy.Blockages,
            CircularBlockages = legacy.CircularBlockages,
            BlueWalls = legacy.BlueWalls,
            AgentSpawnGroups = legacy.AgentSpawnGroups,
        };
    }

    public void ValidateBasic()
    {
        if (Version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported scenario version {Version}. Expected {CurrentVersion}.");
        }

        if (AgentCount is < 0 or > 5000)
        {
            throw new InvalidDataException("Scenario AgentCount must be between 0 and 5000.");
        }

        if (ToxicFume is null || BlastZones is null || Behavior is null || Exits is null || Blockages is null ||
            CircularBlockages is null || BlueWalls is null || AgentSpawnGroups is null || Furniture is null)
        {
            throw new InvalidDataException("Scenario is missing required configuration sections.");
        }

        if (!Enum.IsDefined(typeof(CrowdSim.Core.Model.ScenarioType), ScenarioType))
        {
            throw new InvalidDataException("Scenario contains an unsupported scenario type.");
        }

        ToxicFume.Validate();

        int plannedCount = 0;
        int stageCount = 0;
        foreach (FurnitureScenario? set in Furniture)
        {
            if (set is null || !Enum.IsDefined(set.Kind) || !float.IsFinite(set.X) || !float.IsFinite(set.Y) || !float.IsFinite(set.Rotation))
                throw new InvalidDataException("Scenario contains invalid furniture.");
            if (set.Kind == FurnitureKind.Stage && ++stageCount > 1)
                throw new InvalidDataException("A scenario may contain at most one stage.");
            plannedCount += FurnitureSet.GetSeatCount(set.Kind);
            if (plannedCount > 5000) throw new InvalidDataException("Scenario contains more than 5000 occupants.");
        }
        foreach (AgentSpawnGroupScenario? group in AgentSpawnGroups)
        {
            if (group?.Positions is null)
            {
                throw new InvalidDataException("Scenario contains an invalid agent-spawn group.");
            }

            plannedCount += group.Positions.Count;
            if (plannedCount > 5000)
            {
                throw new InvalidDataException("Scenario contains more than 5000 planned agents.");
            }
        }

        Behavior.Validate();

        float[] radii =
        {
            BlastZones.Kill,
            BlastZones.Incapacitation,
            BlastZones.Fallen,
            BlastZones.Injury,
            BlastZones.Panic,
        };
        if (radii.Any(value => !float.IsFinite(value) || value <= 0f))
        {
            throw new InvalidDataException("Blast-zone radii must be finite positive values.");
        }

        if (!(BlastZones.Kill < BlastZones.Incapacitation &&
              BlastZones.Incapacitation < BlastZones.Fallen &&
              BlastZones.Fallen < BlastZones.Injury &&
              BlastZones.Injury < BlastZones.Panic))
        {
            throw new InvalidDataException(
                "Blast-zone radii must be strictly ordered: Kill < Incapacitation < Fallen < Injury < Panic.");
        }
    }

    private void ValidateAgainst(SimulationConfig config)
    {
        ValidateBasic();
        if ((RoomWidth > 0 && RoomWidth != config.World.WidthMeters) ||
            (RoomHeight > 0 && RoomHeight != config.World.LengthMeters))
        {
            throw new InvalidDataException("Scenario room dimensions do not match the loaded configuration.");
        }

        if (ScenarioType == CrowdSim.Core.Model.ScenarioType.ToxicFume && ToxicFume.HasSource &&
            (!float.IsFinite(ToxicFume.SourceX) || !float.IsFinite(ToxicFume.SourceY) ||
             ToxicFume.SourceX < 0f || ToxicFume.SourceX > config.World.WidthMeters ||
             ToxicFume.SourceY < 0f || ToxicFume.SourceY > config.World.LengthMeters))
        {
            throw new InvalidDataException("Scenario contains an invalid toxic-fume source position.");
        }

        foreach (ExitScenario exit in Exits)
        {
            if (string.IsNullOrWhiteSpace(exit.Name) || exit.Size <= 0 ||
                exit.Side is not ("N" or "S" or "E" or "W") ||
                !float.IsFinite(exit.X) || !float.IsFinite(exit.Y))
            {
                throw new InvalidDataException("Scenario contains an invalid exit definition.");
            }
        }

        foreach (BlockageScenario wall in Blockages)
        {
            if (!float.IsFinite(wall.X1) || !float.IsFinite(wall.Y1) ||
                !float.IsFinite(wall.X2) || !float.IsFinite(wall.Y2) ||
                wall.X2 <= wall.X1 || wall.Y2 <= wall.Y1)
            {
                throw new InvalidDataException("Scenario contains an invalid blockage definition.");
            }
        }

        foreach (CircleBlockageScenario circle in CircularBlockages)
        {
            if (!float.IsFinite(circle.CenterX) || !float.IsFinite(circle.CenterY) ||
                !float.IsFinite(circle.Radius) || circle.Radius <= 0f ||
                circle.CenterX - circle.Radius < 0f ||
                circle.CenterY - circle.Radius < 0f ||
                circle.CenterX + circle.Radius > config.World.WidthMeters ||
                circle.CenterY + circle.Radius > config.World.LengthMeters)
            {
                throw new InvalidDataException("Scenario contains an invalid circular blockage definition.");
            }
        }

        foreach (BlockageScenario wall in BlueWalls)
        {
            if (!float.IsFinite(wall.X1) || !float.IsFinite(wall.Y1) ||
                !float.IsFinite(wall.X2) || !float.IsFinite(wall.Y2) ||
                wall.X2 <= wall.X1 || wall.Y2 <= wall.Y1)
            {
                throw new InvalidDataException("Scenario contains an invalid blue-wall definition.");
            }
        }

        foreach (AgentSpawnGroupScenario group in AgentSpawnGroups)
        {
            if (!float.IsFinite(group.CenterX) || !float.IsFinite(group.CenterY) || group.Positions is null)
            {
                throw new InvalidDataException("Scenario contains an invalid agent-spawn group.");
            }

            foreach (AgentStartScenario position in group.Positions)
            {
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
                    position.X < 0f || position.X > config.World.WidthMeters ||
                    position.Y < 0f || position.Y > config.World.LengthMeters)
                {
                    throw new InvalidDataException("Scenario contains an invalid planned agent position.");
                }
            }
        }
    }
}

public sealed class ToxicFumeScenario
{
    public bool HasSource { get; init; }
    public float SourceX { get; init; }
    public float SourceY { get; init; }
    public float InitialRadiusMeters { get; init; } = ToxicFumeConfig.DefaultInitialRadiusMeters;
    public float DispersionSpeedMetersPerSecond { get; init; } = ToxicFumeConfig.DefaultDispersionSpeedMetersPerSecond;
    public float MaximumExtentMeters { get; init; } = ToxicFumeConfig.DefaultMaximumExtentMeters;
    public float ReleaseDurationSeconds { get; init; } = ToxicFumeConfig.DefaultReleaseDurationSeconds;
    public float DriftDirectionDegrees { get; init; } = ToxicFumeConfig.DefaultDriftDirectionDegrees;
    public float DriftBias { get; init; } = ToxicFumeConfig.DefaultDriftBias;
    public float AwarenessConcentration { get; init; } = ToxicFumeConfig.DefaultAwarenessConcentration;
    public float DangerConcentration { get; init; } = ToxicFumeConfig.DefaultDangerConcentration;
    public float ImpairmentExposure { get; init; } = ToxicFumeConfig.DefaultImpairmentExposure;
    public float IncapacitationExposure { get; init; } = ToxicFumeConfig.DefaultIncapacitationExposure;

    public static ToxicFumeScenario Capture(Simulation simulation)
    {
        ToxicFumeConfig config = simulation.ToxicFumeSystem.Configuration;
        System.Numerics.Vector2? source = simulation.ToxicFumeSystem.SourcePosition;
        return new ToxicFumeScenario
        {
            HasSource = source.HasValue,
            SourceX = source?.X ?? 0f,
            SourceY = source?.Y ?? 0f,
            InitialRadiusMeters = config.InitialRadiusMeters,
            DispersionSpeedMetersPerSecond = config.DispersionSpeedMetersPerSecond,
            MaximumExtentMeters = config.MaximumExtentMeters,
            ReleaseDurationSeconds = config.ReleaseDurationSeconds,
            DriftDirectionDegrees = config.DriftDirectionDegrees,
            DriftBias = config.DriftBias,
            AwarenessConcentration = config.AwarenessConcentration,
            DangerConcentration = config.DangerConcentration,
            ImpairmentExposure = config.ImpairmentExposure,
            IncapacitationExposure = config.IncapacitationExposure,
        };
    }

    public void Apply(ToxicFumeConfig config)
    {
        Validate();
        config.SetValues(
            InitialRadiusMeters,
            DispersionSpeedMetersPerSecond,
            MaximumExtentMeters,
            ReleaseDurationSeconds,
            DriftDirectionDegrees,
            DriftBias,
            AwarenessConcentration,
            DangerConcentration,
            ImpairmentExposure,
            IncapacitationExposure);
    }

    public void Validate()
    {
        float[] values =
        {
            InitialRadiusMeters, DispersionSpeedMetersPerSecond, MaximumExtentMeters,
            ReleaseDurationSeconds, DriftDirectionDegrees, DriftBias, AwarenessConcentration,
            DangerConcentration, ImpairmentExposure, IncapacitationExposure,
        };
        if (values.Any(value => !float.IsFinite(value)) ||
            InitialRadiusMeters is < 0.20f or > 5.00f ||
            DispersionSpeedMetersPerSecond is < 0.05f or > 2.00f ||
            MaximumExtentMeters is < 2.00f or > 60.0f ||
            MaximumExtentMeters < InitialRadiusMeters ||
            ReleaseDurationSeconds is < 1.0f or > 180.0f ||
            DriftBias is < 0f or > 1f ||
            AwarenessConcentration is < 0.02f or > 0.80f ||
            DangerConcentration <= AwarenessConcentration || DangerConcentration > 0.98f ||
            ImpairmentExposure is < 1f or > 120f ||
            IncapacitationExposure <= ImpairmentExposure || IncapacitationExposure > 300f)
        {
            throw new InvalidDataException("Scenario contains invalid toxic-fume configuration values.");
        }
    }
}

public sealed class BlastZoneScenario
{
    public float Kill { get; init; } = AgentConfig.DefaultHardKillRadius;
    public float Incapacitation { get; init; } = AgentConfig.DefaultHeatIncapacityRadius;
    public float Fallen { get; init; } = AgentConfig.DefaultFallenRadius;
    public float Injury { get; init; } = AgentConfig.DefaultInjuryRadius;
    public float Panic { get; init; } = AgentConfig.DefaultHeatRadius;
}

public sealed class BehaviorScenario
{
    public float TraitVariation { get; init; } = BehaviorTuningConfig.DefaultTraitVariation;
    public float ReactionVariation { get; init; } = BehaviorTuningConfig.DefaultReactionVariation;
    public float StressSusceptibility { get; init; } = BehaviorTuningConfig.DefaultStressSusceptibility;
    public float StressPersistence { get; init; } = BehaviorTuningConfig.DefaultStressPersistence;
    public float CrowdFollowing { get; init; } = BehaviorTuningConfig.DefaultCrowdFollowing;
    public float PersonalSpace { get; init; } = BehaviorTuningConfig.DefaultPersonalSpace;
    public float PanicSpaceCompression { get; init; } = BehaviorTuningConfig.DefaultPanicSpaceCompression;
    public float EmergencyUrgency { get; init; } = BehaviorTuningConfig.DefaultEmergencyUrgency;
    public float MaxRunningSpeedMetersPerSecond { get; init; } = BehaviorTuningConfig.DefaultMaxRunningSpeedMetersPerSecond;
    public float HelpingTendency { get; init; } = BehaviorTuningConfig.DefaultHelpingTendency;
    public float RouteFamiliarity { get; init; } = BehaviorTuningConfig.DefaultRouteFamiliarity;
    public float BottleneckAssertiveness { get; init; } = BehaviorTuningConfig.DefaultBottleneckAssertiveness;
    public float BottleneckPressureSensitivity { get; init; } = BehaviorTuningConfig.DefaultBottleneckPressureSensitivity;
    public float BottleneckSpaceCompression { get; init; } = BehaviorTuningConfig.DefaultBottleneckSpaceCompression;

    public static BehaviorScenario Capture(BehaviorTuningConfig tuning)
        => new()
        {
            TraitVariation = tuning.TraitVariation,
            ReactionVariation = tuning.ReactionVariation,
            StressSusceptibility = tuning.StressSusceptibility,
            StressPersistence = tuning.StressPersistence,
            CrowdFollowing = tuning.CrowdFollowing,
            PersonalSpace = tuning.PersonalSpace,
            PanicSpaceCompression = tuning.PanicSpaceCompression,
            EmergencyUrgency = tuning.EmergencyUrgency,
            MaxRunningSpeedMetersPerSecond = tuning.MaxRunningSpeedMetersPerSecond,
            HelpingTendency = tuning.HelpingTendency,
            RouteFamiliarity = tuning.RouteFamiliarity,
            BottleneckAssertiveness = tuning.BottleneckAssertiveness,
            BottleneckPressureSensitivity = tuning.BottleneckPressureSensitivity,
            BottleneckSpaceCompression = tuning.BottleneckSpaceCompression,
        };

    public void Apply(BehaviorTuningConfig tuning)
    {
        Validate();
        tuning.SetMaxRunningSpeed(MaxRunningSpeedMetersPerSecond);
        tuning.SetValues(
            TraitVariation, ReactionVariation, StressSusceptibility, StressPersistence,
            CrowdFollowing, PersonalSpace, PanicSpaceCompression, EmergencyUrgency,
            HelpingTendency, RouteFamiliarity, BottleneckAssertiveness,
            BottleneckPressureSensitivity, BottleneckSpaceCompression);
    }

    public void Validate()
    {
        float[] values =
        {
            TraitVariation, ReactionVariation, StressSusceptibility, StressPersistence,
            CrowdFollowing, PersonalSpace, PanicSpaceCompression, EmergencyUrgency,
            MaxRunningSpeedMetersPerSecond,
            HelpingTendency, RouteFamiliarity, BottleneckAssertiveness,
            BottleneckPressureSensitivity, BottleneckSpaceCompression,
        };
        if (values.Any(value => !float.IsFinite(value)) ||
            TraitVariation is < 0f or > 1f ||
            ReactionVariation is < 0f or > 1f ||
            StressSusceptibility is < 0.60f or > 1.40f ||
            StressPersistence is < 0.50f or > 2.00f ||
            CrowdFollowing is < 0f or > 1f ||
            PersonalSpace is < 0.85f or > 1.30f ||
            PanicSpaceCompression is < 0f or > 0.35f ||
            EmergencyUrgency is < 0.80f or > 1.30f ||
            MaxRunningSpeedMetersPerSecond < BehaviorTuningConfig.MinimumRunningSpeedLimitMetersPerSecond ||
            MaxRunningSpeedMetersPerSecond > BehaviorTuningConfig.MaximumRunningSpeedLimitMetersPerSecond ||
            HelpingTendency is < 0f or > 1f ||
            RouteFamiliarity is < 0f or > 1f ||
            BottleneckAssertiveness is < 0f or > 1f ||
            BottleneckPressureSensitivity is < 0.50f or > 1.50f ||
            BottleneckSpaceCompression is < 0f or > 0.35f)
        {
            throw new InvalidDataException("Scenario contains invalid behaviour tuning values.");
        }
    }
}

public sealed class ExitScenario
{
    public string Name { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public float Size { get; init; }
    public string Side { get; init; } = "N";
}

public sealed class BlockageScenario
{
    public float X1 { get; init; }
    public float Y1 { get; init; }
    public float X2 { get; init; }
    public float Y2 { get; init; }
}

public sealed class CircleBlockageScenario
{
    public float CenterX { get; init; }
    public float CenterY { get; init; }
    public float Radius { get; init; } = 1.25f;
}

public sealed class AgentSpawnGroupScenario
{
    public float CenterX { get; init; }
    public float CenterY { get; init; }
    public List<AgentStartScenario> Positions { get; init; } = new();
}

public sealed class AgentStartScenario
{
    public float X { get; init; }
    public float Y { get; init; }
}

/// <summary>Authored furniture only; replay starts with intact chairs and seated occupants.</summary>
public sealed class FurnitureScenario
{
    public FurnitureKind Kind { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Rotation { get; init; }
}
