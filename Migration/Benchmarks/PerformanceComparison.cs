using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.Core.Persistence;

internal static class PerformanceComparison
{
    public static void Run(string output, int frames, string? fixtureDirectory)
    {
        if (frames < 20 || frames > 2000) throw new ArgumentOutOfRangeException(nameof(frames));
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(new {
            Utc = DateTime.UtcNow, Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            CPUs = Environment.ProcessorCount, Warmup = 12, Frames = frames,
            Note = "M10 + diagnostic phase timers baseline versus optimized portable CPU. No Unity, Mono, graphics or GPU executed."
        }, new JsonSerializerOptions { WriteIndented = true }));
        using StreamWriter csv = new(Path.Combine(output, "phases.csv"));
        csv.WriteLine("hazard,layout,hybrid,tick,total_ms,grid_ms,hazard_ms,response_ms,furniture_ms,preparation_ms,movement_ms,contacts_ms,exit_ms,sfm_nested_ms,collision_nested_ms,bytes");
        foreach (ScenarioType scenario in new[] { ScenarioType.Explosion, ScenarioType.ToxicFume })
        foreach (bool dense in new[] { false, true })
        foreach (bool hybrid in new[] { false, true })
        {
            Simulation Ready()
            {
                Simulation m = dense ? (fixtureDirectory == null ? DenseCrowdLayout.Create(hybrid, scenario) : new Simulation()) : CrowdScaleLayout.Create(1000, false, hybrid, scenario);
                if (dense && fixtureDirectory != null)
                {
                    ScenarioDefinition definition = ScenarioJson.Deserialize(File.ReadAllText(Path.Combine(fixtureDirectory, "dense800-" + scenario + ".json")));
                    m = new Simulation(config: definition.CreateConfig(), randomSeed: definition.RandomSeed); definition.ApplyLayout(m);
                    m.Config.HybridEnabled = hybrid;
                }
                m.SpawnAgentsAtPositions(m.Environment.GetPlannedAgentPositions());
                if (scenario == ScenarioType.ToxicFume) m.PrecomputeToxicFume();
                m.SetRunning(true); if (scenario == ScenarioType.Explosion) m.TriggerExplosion(28, 17); else m.TriggerToxicFumeRelease(); return m;
            }
            Simulation warm = Ready(); for (int i = 0; i < 12; i++) warm.Step();
            Simulation model = Ready(); using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            string name = scenario + (dense ? "-dense800-" : "-open1000-") + hybrid;
            for (int i = 0; i < frames; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread(); model.Step(); long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                csv.WriteLine($"{scenario},{(dense ? "dense800" : "open1000")},{hybrid},{model.FrameIndex},{model.LastStepMilliseconds:F6},{model.LastGridMilliseconds:F6},{model.LastHazardMilliseconds:F6},{model.LastResponseMilliseconds:F6},{model.LastFurnitureMilliseconds:F6},{model.LastPreparationMilliseconds:F6},{model.LastMovementMilliseconds:F6},{model.LastContactsMilliseconds:F6},{model.LastExitMilliseconds:F6},{model.LastSfmMilliseconds:F6},{model.LastCollisionMilliseconds:F6},{bytes}");
                // Observe the full ordered population, furniture and debris each tick, outside timing.
                hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(new {
                    model.FrameIndex,
                    Agents = model.Agents.Select(a => new { a.AgentId, a.Position, a.Velocity, a.State, a.Posture, a.PanicLevel,
                        a.Intent, a.InjurySpeedFactor, a.CumulativeToxicExposure, a.StandUpProgress, a.RouteWaypointIndex,
                        a.RouteWaypoints, a.ExitTarget, a.LocalNavigationMode, a.BottleneckMode }),
                    Furniture = model.Environment.Furniture.Select(f => new { f.Center, f.Rotation, Table = f.Table == null ? null : Body(f.Table),
                        Chairs = f.Chairs.Select(c => new { Physical = Body(c), c.OccupantId }) }),
                    model.Debris, model.ExitMetrics
                }, new JsonSerializerOptions { IncludeFields = true }));
            }
            File.WriteAllText(Path.Combine(output, name + ".sha256"), Convert.ToHexString(hash.GetHashAndReset()) + "\n");
            csv.Flush(); Console.WriteLine("Profiled " + name);
        }
    }
    private static object Body(FurnitureBody b) => new { b.Position, b.Heading, b.Velocity, b.Height, b.State };
}
