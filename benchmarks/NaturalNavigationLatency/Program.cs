using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;

// Usage: [output-directory] [frames=400] [agents=600] [hybrid=false] [scenario=Explosion]
// Paired runs use identical initial layouts, furniture and seeds. Subsequent trajectories
// intentionally differ. This is a CPU-step benchmark, not a GPU/rendering FPS measurement.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string output = args.Length > 0 ? args[0] : "benchmark-results/natural-navigation";
int frames = ReadInteger(args, 1, 400, 20, 20000);
int count = ReadInteger(args, 2, 600, 240, 2000);
bool hybrid = args.Length > 3 && bool.Parse(args[3]);
if (args.Length > 5) throw new ArgumentException("Expected at most five positional arguments.");
ScenarioType scenario = ScenarioType.Explosion;
if (args.Length > 4 && (!Enum.TryParse(args[4], ignoreCase: true, out scenario) || !Enum.IsDefined(scenario)))
    throw new ArgumentException("Scenario must be Explosion or ToxicFume.");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "environment.txt"),
    $"UTC={DateTime.UtcNow:O}\nRuntime={RuntimeInformation.FrameworkDescription}\n" +
    $"OS={RuntimeInformation.OSDescription}\nProcessors={Environment.ProcessorCount}\n" +
    $"Frames={frames}\nAgents={count}\nTables=24\nSeated=240\nHybrid={hybrid}\nScenario={scenario}\n" +
    "Times include Simulation.Step only; rendering, CSV output and fume precomputation are excluded.\n" +
    "Managed allocation counts are for the stepping thread only.\n" +
    "Natural=false is the retained legacy-navigation path in this revision, not a separately built original ZIP.\n");
using StreamWriter summary = new(Path.Combine(output, "summary.csv"));
summary.WriteLine("scenario,seed,agents,natural,hybrid,median_ms,p95_ms,p99_ms,max_ms,step_allocated_bytes,exited,dead,incapacitated,total_recovery_attempts,completed_detours,exit_changes,shared_field_builds,max_natural_plans_per_step,max_recovery_probes_per_step,max_pending_routes,max_pending_recoveries,fume_precompute_ms,fume_cache_bytes");
int[] seeds = { 731, 732, 733 };
for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
foreach (bool natural in seedIndex % 2 == 0 ? new[] { false, true } : new[] { true, false })
{
    int seed = seeds[seedIndex];
    Simulation warm = Create(count, seed, natural, hybrid, scenario);
    for (int i = 0; i < 35; i++) warm.Step();
    Simulation sim = Create(count, seed, natural, hybrid, scenario);
    using StreamWriter raw = new(Path.Combine(output, $"{scenario}-seed-{seed}-natural-{natural}-hybrid-{hybrid}.csv"));
    raw.WriteLine("frame,simulation_seconds,step_ms,furniture_ms,full_astar_ms,all_route_requests,natural_route_plans,recovery_probes,pending_routes,pending_recoveries,active,exited,longest_current_no_progress_seconds");
    double[] times = new double[frames];
    long allocated = 0;
    int maxPlans = 0, maxProbes = 0, maxRoutes = 0, maxRecoveries = 0;
    for (int frame = 0; frame < frames; frame++)
    {
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        sim.Step();
        times[frame] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        allocated += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        if (sim.FrameIndex != frame + 1) throw new InvalidOperationException("Simulation did not advance.");
        var nav = sim.EvacuationSystem;
        maxPlans = Math.Max(maxPlans, nav.LastNaturalRoutePlans);
        maxProbes = Math.Max(maxProbes, nav.LastNaturalRecoveryProbes);
        maxRoutes = Math.Max(maxRoutes, nav.NaturalPendingRoutes);
        maxRecoveries = Math.Max(maxRecoveries, nav.NaturalPendingRecoveries);
        // Counters and CSV formatting are deliberately outside the timed/allocation interval.
        int active = 0, exited = 0;
        float longest = 0f;
        foreach (Agent agent in sim.Agents)
        {
            if (agent.IsActive) active++;
            if (agent.State == AgentState.Exited) exited++;
            if (agent.State is AgentState.Evacuating or AgentState.Injured)
                longest = Math.Max(longest, agent.NavigationMemory.NoProgressSeconds);
        }
        raw.WriteLine($"{sim.FrameIndex},{sim.FrameIndex * sim.Config.Agent.Dt:F3},{times[frame]:F4},{sim.LastFurnitureMilliseconds:F4},{sim.LastRoutePlanningMilliseconds:F4},{sim.LastRoutePlanRequests},{nav.LastNaturalRoutePlans},{nav.LastNaturalRecoveryProbes},{nav.NaturalPendingRoutes},{nav.NaturalPendingRecoveries},{active},{exited},{longest:F3}");
    }
    Array.Sort(times);
    int exitedCount = sim.Agents.Count(a => a.State == AgentState.Exited);
    summary.WriteLine($"{scenario},{seed},{count},{natural},{hybrid},{Percentile(times, .50):F4},{Percentile(times, .95):F4},{Percentile(times, .99):F4},{times[^1]:F4},{allocated},{exitedCount},{sim.Agents.Count(a => a.State == AgentState.Dead)},{sim.Agents.Count(a => a.State is AgentState.Incapacitated or AgentState.TempIncap)},{sim.Agents.Sum(a => a.NavigationMemory.TotalRecoveryAttempts)},{sim.Agents.Sum(a => a.NavigationMemory.SuccessfulDetours)},{sim.Agents.Sum(a => a.NavigationMemory.ExitChanges)},{sim.HybridMovement.Fields.FieldBuilds},{maxPlans},{maxProbes},{maxRoutes},{maxRecoveries},{sim.ToxicFumeSystem.LastPrecomputeMilliseconds:F4},{sim.ToxicFumeSystem.EstimatedCacheBytes}");
    summary.Flush();
    Console.WriteLine($"Scenario {scenario}; seed {seed}; natural={natural}; hybrid={hybrid}; p95={Percentile(times, .95):F2} ms; p99={Percentile(times, .99):F2} ms; exited={exitedCount}/{count}");
}
Console.WriteLine("Compare paired seed rows and tail latency, not only means. No frame-rate or realism guarantee is implied.");

static int ReadInteger(string[] values, int index, int fallback, int min, int max)
{
    int value = values.Length > index ? int.Parse(values[index], CultureInfo.InvariantCulture) : fallback;
    if (value < min || value > max) throw new ArgumentOutOfRangeException($"argument-{index}", $"Expected {min}..{max}.");
    return value;
}

static double Percentile(double[] sorted, double fraction) =>
    sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * fraction) - 1, 0, sorted.Length - 1)];

static Simulation Create(int count, int seed, bool natural, bool hybrid, ScenarioType scenario)
{
    SimulationConfig config = new()
    {
        Agent = new AgentConfig { Count = count, NaturalNavigationEnabled = natural },
        HybridEnabled = hybrid,
    };
    Simulation sim = new(config: config, randomSeed: seed);
    sim.SetScenarioType(scenario);
    foreach (float y in new[] { 10f, 24f })
    {
        if (!sim.Environment.AddExitFromClick(0.3f, y, 5) ||
            !sim.Environment.AddExitFromClick(sim.Environment.Width - 0.3f, y, 5))
            throw new InvalidOperationException("Exit placement failed.");
    }
    foreach (float x in new[] { 6f, 14f, 22f, 30f, 38f, 46f })
    foreach (float y in new[] { 6f, 13f, 20f, 27f })
        if (!sim.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(x, y)))
            throw new InvalidOperationException("Furniture placement failed.");
    List<Vector2> positions = sim.Environment.GetPlannedAgentPositions().ToList();
    if (positions.Count != 240) throw new InvalidOperationException("Expected 240 occupied seats.");
    Random layout = new(seed);
    float separation = config.Agent.Radius * 2.1f;
    int attempts = 0;
    while (positions.Count < count && attempts++ < 250000)
    {
        Vector2 candidate = new(1f + (float)layout.NextDouble() * (sim.Environment.Width - 2f),
            1f + (float)layout.NextDouble() * (sim.Environment.Height - 2f));
        if (sim.Environment.IsPointBlocked(candidate, config.Agent.Radius) ||
            positions.Any(p => Vector2.DistanceSquared(p, candidate) < separation * separation)) continue;
        positions.Add(candidate);
    }
    if (positions.Count != count) throw new InvalidOperationException("Could not place requested population.");
    sim.SpawnAgentsAtPositions(positions);
    if (scenario == ScenarioType.ToxicFume)
    {
        if (!sim.ToxicFumeSystem.TrySetSource(sim, new Vector2(28f, 17f)))
            throw new InvalidOperationException("Fume source placement failed.");
        sim.PrecomputeToxicFume(); // setup work, deliberately outside the stepping timer
    }
    sim.SetRunning(true);
    if (!sim.Running) throw new InvalidOperationException("Simulation refused to start.");
    if (scenario == ScenarioType.ToxicFume)
    {
        if (!sim.TriggerToxicFumeRelease()) throw new InvalidOperationException("Fume release failed.");
    }
    else sim.TriggerExplosion(28f, 17f);
    return sim;
}
