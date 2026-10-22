using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
#if PORTABLE
using Random = CrowdSim.Core.Compatibility.ReferenceRandom;
using OracleQueue = CrowdSim.Core.Compatibility.ReferencePriorityQueue<int, int>;
#else
using OracleQueue = System.Collections.Generic.PriorityQueue<int, int>;
#endif

namespace CrowdSim.Migration.ReferenceCapture;

internal static class Program
{
    private const string ReferenceCommit = "01b435568f211f5c8ddf99b32b05e7ceb1ad3a24";
    private const int EventTick = 10;
    private const int LastTick = 120;
    private static readonly JsonSerializerOptions JsonOptions = CaptureJsonOptions();

    private static JsonSerializerOptions CaptureJsonOptions()
    {
        // Honor the production Newtonsoft codec's omission of the two zero/default M10
        // extension fields. Preserve every original field and all ordered physics hashes.
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(FurnitureScenario)) return;
            foreach (JsonPropertyInfo property in info.Properties)
                if (property.Name is "StageLength" or "StageWidth")
                    property.ShouldSerialize = (_, value) => value is float dimension && dimension != 0f;
        });
        return new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, TypeInfoResolver = resolver };
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2 || (args[0] != "--write" && args[0] != "--verify"))
                throw new ArgumentException("Usage: ReferenceCapture --write|--verify <repository-root>");
            string root = Path.GetFullPath(args[1]);
            string output = Path.Combine(root, "Migration", "Parity", "Reference");
            bool write = args[0] == "--write";
            if (write) Directory.CreateDirectory(output);
            SaveOrVerify(output, "compatibility.json", CompatibilityOracle(), write);
            foreach (string preset in new[] { "explosion-open", "explosion-furnished", "toxic-205" })
            foreach (bool hybrid in new[] { false, true })
            {
                string name = preset + (hybrid ? "-hybrid" : "-individual");
                Simulation simulation = CreatePreset(root, preset, hybrid);
                SaveOrVerify(output, name + ".json", Capture(simulation, name), write);
            }
            Console.WriteLine(write ? "Reference baselines written; review before committing." : "All six reference traces and compatibility oracle match.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Simulation CreatePreset(string root, string preset, bool hybrid)
    {
        Simulation simulation;
        if (preset == "toxic-205")
        {
            ScenarioDefinition definition = ScenarioFileStore.Load("toxic_fume_205",
                Path.Combine(root, "tests", "CrowdSim.Tests", "Fixtures"));
            simulation = new Simulation(0, definition.CreateConfig(), definition.RandomSeed);
            definition.ApplyLayout(simulation);
            simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
            Require(simulation.Agents.Count == 205, "The fume regression fixture must retain 205 occupants.");
            simulation.PrecomputeToxicFume();
        }
        else
        {
            simulation = new Simulation(randomSeed: 731);
            Require(simulation.Environment.AddExitFromClick(0.2f, 17f, 5), "West exit placement failed.");
            Require(simulation.Environment.AddExitFromClick(simulation.Environment.Width - 0.2f, 17f, 3), "East exit placement failed.");
            if (preset == "explosion-furnished")
            {
                Require(simulation.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(28f, 17f)), "Round table placement failed.");
                Require(simulation.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(36f, 20f)), "Chair placement failed.");
                Require(simulation.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(24f, 12f)), "Rectangular table placement failed.");
                Require(simulation.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(42f, 27f)), "Stage placement failed.");
                Require(simulation.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 12f), MathF.PI / 4f), "Angled barricade placement failed.");
                Require(simulation.Environment.AddBlockageFromClick(20f, 20f, 2), "Square blockage placement failed.");
                Require(simulation.Environment.AddCircularBlockageFromClick(12f, 23f), "Circular blockage placement failed.");
            }
            Require(simulation.Environment.AddAgentSpawnGroupFromClick(10f, 10f, 50), "Floor population placement failed.");
            simulation.BuildWallGrid();
            simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
        }
        simulation.Config.HybridEnabled = hybrid;
        return simulation;
    }

    private static object Capture(Simulation simulation, string name)
    {
        ScenarioDefinition authored = ScenarioDefinition.Capture(simulation, simulation.Agents.Count, name);
        FinalizedSceneCheckpoint replay = FinalizedSceneCheckpoint.Capture(simulation, simulation.Agents.Count, name);
        Vector2[] startingPositions = simulation.Agents.Select(a => a.Position).ToArray();
        object initialTraits = simulation.Agents.Select(a => new
        {
            a.AgentId, a.BaseSpeed, a.ReactionDelayFrames, a.Strategy, a.ThreatSensitivity,
            a.FollowTendency, a.HelpingTendency, a.RouteFamiliarity, a.UrgencyBias,
            a.StressRecoveryBias, a.PersonalSpaceBias, a.Assertiveness, a.PatienceBias
        }).ToArray();
        List<string> tickHashes = new();
        List<object> checkpoints = new();
        simulation.SetRunning(true);
        Require(simulation.Running, "Preset could not start.");
        for (int tick = 0; tick <= LastTick; tick++)
        {
            Require(simulation.FrameIndex == tick, "Simulation did not advance exactly one fixed tick.");
            foreach (Agent agent in simulation.Agents)
            {
                Require(float.IsFinite(agent.Position.X) && float.IsFinite(agent.Position.Y) &&
                    float.IsFinite(agent.Velocity.X) && float.IsFinite(agent.Velocity.Y), "Non-finite agent state.");
            }
            object state = ObservedState(simulation);
            tickHashes.Add(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions))).ToLowerInvariant());
            if (tick is 0 or 1 or 10 or 11 or 30 or 60 or 120) checkpoints.Add(ObservedState(simulation, sampled: true));
            if (tick == LastTick) break;
            if (tick == EventTick)
            {
                if (simulation.ScenarioType == ScenarioType.ToxicFume)
                    Require(simulation.TriggerToxicFumeRelease(), "Fume release failed.");
                else
                {
                    simulation.ExplosionSystem.TriggerExplosion(simulation, 28f, 17f);
                    Require(simulation.Environment.HasExplosion(), "Explosion trigger failed.");
                }
            }
            simulation.Step();
        }
        Simulation restored = replay.CreateReadySimulation();
        Require(restored.FrameIndex == 0 && !restored.Running &&
            !restored.Environment.HasExplosion() && !restored.ToxicFumeSystem.IsReleased, "Replay did not restore frame zero.");
        Require(startingPositions.SequenceEqual(restored.Agents.Select(a => a.Position)), "Replay changed authored start positions.");
        return new
        {
            FormatVersion = 1, ReferenceCommit, Name = name,
            Runtime = ".NET 8.0.21 / SDK 8.0.415 / Linux x64",
            EventTick, LastTick,
            EventOrdering = "Observe tick, trigger event at tick 10, then Step; tick 11 is first post-event observation.",
            AuthoredScenario = authored, InitialTraits = initialTraits,
            TickHashes = tickHashes, Checkpoints = checkpoints,
            Replay = ObservedState(restored, sampled: true)
        };
    }

    private static object ObservedState(Simulation simulation, bool sampled = false) => new
    {
        SampledAgentsOnly = sampled,
        simulation.FrameIndex, simulation.Running, simulation.ScenarioType,
        simulation.ExplosionTriggerFrame,
        Fume = new { simulation.ToxicFumeSystem.IsReleased, simulation.ToxicFumeSystem.ReleaseFrame,
            simulation.ToxicFumeSystem.GridCellCount, simulation.ToxicFumeSystem.ReachableCellCount },
        Counts = Enum.GetValues<AgentState>().ToDictionary(s => s.ToString(), s => simulation.Agents.Count(a => a.State == s)),
        Agents = simulation.Agents.OrderBy(a => a.AgentId)
            .Where(a => !sampled || a.AgentId < 3 || a.AgentId >= simulation.Agents.Count - 3).Select(a => new
        {
            a.AgentId, a.Position, a.Velocity, a.State, a.Posture, a.Intent, a.PanicLevel,
            Exit = a.ExitTarget?.Name, a.InjurySpeedFactor, a.CumulativeToxicExposure,
            a.CurrentToxicConcentration, a.ToxicMobilityFactor, a.StandUpProgress,
            a.BottleneckMode, a.BottleneckPressure, a.LocalNavigationMode,
            a.AssistTargetAgentId,
            a.RouteWaypointIndex, Route = a.RouteWaypoints.ToArray()
        }).ToArray(),
        Furniture = simulation.Environment.Furniture.Select(f => new
        {
            f.Kind, f.Center, f.Rotation,
            Table = f.Table is null ? null : Body(f.Table),
            Chairs = f.Chairs.Select(c => new { Body = Body(c), c.OccupantId }).ToArray()
        }).ToArray(),
        Debris = simulation.Debris.Select(d => new { d.Position, d.Velocity, d.Ttl, d.Energy }).ToArray(),
        Exits = simulation.ExitMetrics.OrderBy(p => p.Key).Select(p => new { Name = p.Key, p.Value.Density, p.Value.Flow }).ToArray(),
        Hybrid = new { simulation.HybridMovement.OpenSpaceAgents, simulation.HybridMovement.DetailedAgents,
            simulation.HybridMovement.GroupedAgents, simulation.HybridMovement.GroupCount },
        simulation.DebrisHits, simulation.DebrisInjuries, simulation.DebrisIncapacitations, simulation.DebrisFatalities
    };

    private static object Body(FurnitureBody body) => new { body.Position, body.Velocity, body.Height, body.State, body.Heading };

    private static object CompatibilityOracle()
    {
        object[] sequences = new[] { 0, 42, 731, -12345 }.Select(seed =>
        {
            Random integers = new(seed), doubles = new(seed), bytes = new(seed);
            byte[] buffer = new byte[32]; bytes.NextBytes(buffer);
            return (object)new { Seed = seed,
                Integers = Enumerable.Range(0, 20).Select(_ => integers.Next()).ToArray(),
                Doubles = Enumerable.Range(0, 20).Select(_ => doubles.NextDouble()).ToArray(),
                Bytes = Convert.ToHexString(buffer).ToLowerInvariant() };
        }).ToArray();
        OracleQueue queue = new();
        for (int i = 0; i < 16; i++) queue.Enqueue(i, i % 3);
        List<int> order = new();
        while (queue.TryDequeue(out int item, out _)) order.Add(item);
        return new { FormatVersion = 1, ReferenceCommit, SeededRandom = sequences,
            QueueInput = "items 0..15; priority=item%3; ascending enqueue order", QueueDequeueOrder = order };
    }

    private static void SaveOrVerify(string directory, string name, object value, bool write)
    {
        string path = Path.Combine(directory, name);
        string json = JsonSerializer.Serialize(value, JsonOptions) + "\n";
        if (write) File.WriteAllText(path, json);
        else
        {
            if (!File.Exists(path) || File.ReadAllText(path).Replace("\r\n", "\n") != json)
                throw new InvalidDataException("Reference baseline differs: " + name + ". Diagnose runtime/source drift; do not regenerate blindly.");
            Console.WriteLine("Verified " + name);
        }
    }
}
