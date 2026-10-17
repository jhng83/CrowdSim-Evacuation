using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;

// Usage: [output-directory] [Explosion|ToxicFume] [frames=400] [agents=600] [barricades=0] [barricade-angle-deg=0]
// Timings are CPU simulation steps only, never rendering FPS or a performance guarantee.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length > 6) throw new ArgumentException("Expected at most six positional arguments.");
string output = args.Length > 0 ? args[0] : "benchmark-results/rectangular-assets";
ScenarioType scenario = args.Length > 1 ? Enum.Parse<ScenarioType>(args[1], true) : ScenarioType.Explosion;
if (!Enum.IsDefined(scenario)) throw new ArgumentException("Unknown scenario.");
int frames = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 400;
int count = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 600;
int barricades = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 0;
float angleDegrees = args.Length > 5 ? float.Parse(args[5], CultureInfo.InvariantCulture) : 0f;
if (!float.IsFinite(angleDegrees)) throw new ArgumentOutOfRangeException(nameof(args), "Angle must be finite.");
angleDegrees = (angleDegrees % 360f + 360f) % 360f;
float rotation = angleDegrees * MathF.PI / 180f;
if (barricades is < 0 or > 64) throw new ArgumentOutOfRangeException(nameof(args), "Expected 0..64 barricades.");
if (frames is < 20 or > 20000 || count is < 1 or > 5000)
    throw new ArgumentOutOfRangeException(nameof(args), "Expected 20..20000 frames and 1..5000 agents.");
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "environment.txt"),
    $"UTC={DateTime.UtcNow:O}\nRuntime={RuntimeInformation.FrameworkDescription}\nOS={RuntimeInformation.OSDescription}\n" +
    $"Scenario={scenario}\nAgents={count}\nRectangularTables=24\nStage=15x6 metres\nBarricades={barricades}\nBarricadeAngleDegrees={angleDegrees}\nFrames={frames}\n" +
    "Rendering, output formatting, setup and fume precomputation are excluded from timed steps.\n");
using StreamWriter summary = new(Path.Combine(output, "summary.csv"));
summary.WriteLine("scenario,seed,agents,barricades,barricade_angle_degrees,median_ms,p95_ms,p99_ms,max_ms,step_allocated_bytes,exited,tables_destroyed");
foreach (int seed in new[] { 731, 732, 733 })
{
    Simulation warm = CreateScene(count, seed, scenario, barricades, rotation);
    for (int i = 0; i < 30; i++) warm.Step();
    Simulation sim = CreateScene(count, seed, scenario, barricades, rotation);
    using StreamWriter raw = new(Path.Combine(output, $"{scenario}-seed-{seed}.csv"));
    raw.WriteLine("frame,step_ms,furniture_ms,route_ms,airborne_tables,destroyed_tables,exited");
    double[] times = new double[frames];
    long allocated = 0;
    for (int i = 0; i < frames; i++)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        sim.Step();
        times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        if (sim.FrameIndex != i + 1) throw new InvalidOperationException("Simulation did not advance.");
        raw.WriteLine($"{sim.FrameIndex},{times[i]:F4},{sim.LastFurnitureMilliseconds:F4},{sim.LastRoutePlanningMilliseconds:F4}," +
            $"{TableCount(sim, ChairState.Airborne)},{TableCount(sim, ChairState.Destroyed)},{sim.Agents.Count(a => a.State == AgentState.Exited)}");
    }
    Array.Sort(times);
    summary.WriteLine($"{scenario},{seed},{count},{barricades},{angleDegrees},{Percentile(times, .50):F4},{Percentile(times, .95):F4}," +
        $"{Percentile(times, .99):F4},{times[^1]:F4},{allocated},{sim.Agents.Count(a => a.State == AgentState.Exited)},{TableCount(sim, ChairState.Destroyed)}");
    summary.Flush();
    Console.WriteLine($"{scenario}, seed {seed}: p95={Percentile(times, .95):F2} ms, p99={Percentile(times, .99):F2} ms (CPU step only).");
}

static int TableCount(Simulation sim, ChairState state) => sim.Environment.Furniture.Count(
    set => set.Kind == FurnitureKind.RectangularTable && set.Table?.State == state);

static double Percentile(double[] values, double p) => values[
    Math.Clamp((int)Math.Ceiling(values.Length * p) - 1, 0, values.Length - 1)];

static Simulation CreateScene(int count, int seed, ScenarioType scenario, int barricades, float rotation)
{
    Simulation sim = new(config: new SimulationConfig
    {
        Agent = new AgentConfig { Count = count, NaturalNavigationEnabled = true },
    }, randomSeed: seed);
    sim.SetScenarioType(scenario);
    foreach (float y in new[] { 10f, 24f })
        if (!sim.Environment.AddExitFromClick(0.3f, y, 5) ||
            !sim.Environment.AddExitFromClick(sim.Environment.Width - 0.3f, y, 5))
            throw new InvalidOperationException("Exit placement failed.");
    foreach (float x in new[] { 6f, 14f, 22f, 30f, 38f, 46f })
    foreach (float y in new[] { 6f, 12f, 18f, 24f })
        if (!sim.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(x, y)))
            throw new InvalidOperationException("Table placement failed.");
    if (!sim.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(28f, 30f)))
        throw new InvalidOperationException("Stage placement failed.");
    // Up to four rows in clear lanes, independent of table/stage footprints.
    for (int i = 0; i < barricades; i++)
    {
        Vector2 center = new(4f + 3f * (i % 16), 3f + 6f * (i / 16));
        if (!sim.Environment.AddFurniture(FurnitureKind.Barricade, center, rotation))
            throw new InvalidOperationException($"Barricade {i} placement failed.");
    }
    if (sim.Environment.PlannedAgentCount != 0)
        throw new InvalidOperationException("These assets must not add occupants.");
    Random layout = new(seed);
    List<Vector2> positions = new(count);
    float gapSquared = MathF.Pow(sim.Config.Agent.Radius * 2.1f, 2f);
    for (int attempts = 0; positions.Count < count && attempts < 500000; attempts++)
    {
        Vector2 point = new(1f + (float)layout.NextDouble() * (sim.Environment.Width - 2f),
            1f + (float)layout.NextDouble() * (sim.Environment.Height - 2f));
        if (sim.Environment.IsPointBlocked(point, sim.Config.Agent.Radius) ||
            positions.Any(other => Vector2.DistanceSquared(point, other) < gapSquared)) continue;
        positions.Add(point);
    }
    if (positions.Count != count) throw new InvalidOperationException("Population placement failed.");
    sim.SpawnAgentsAtPositions(positions);
    if (scenario == ScenarioType.ToxicFume)
    {
        if (!sim.ToxicFumeSystem.TrySetSource(sim, new Vector2(28f, 17f)))
            throw new InvalidOperationException("Fume source placement failed.");
        sim.PrecomputeToxicFume();
    }
    sim.SetRunning(true);
    if (!sim.Running) throw new InvalidOperationException("Scene refused to start.");
    if (scenario == ScenarioType.ToxicFume)
    {
        if (!sim.TriggerToxicFumeRelease()) throw new InvalidOperationException("Fume release failed.");
    }
    else
    {
        sim.TriggerExplosion(22f, 12f);
        if (sim.Environment.Explosion is null) throw new InvalidOperationException("Explosion failed.");
    }
    return sim;
}
