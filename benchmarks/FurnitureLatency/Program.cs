using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;

// Run from the solution directory; optional arguments: scenario JSON, output CSV, frame count.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string input = args.Length > 0 ? args[0] : "tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json";
string output = args.Length > 1 ? args[1] : "furniture-latency.csv";
int frames = args.Length > 2 ? int.Parse(args[2]) : 600;
if (frames < 1) throw new ArgumentOutOfRangeException(nameof(frames));
ScenarioDefinition saved = JsonSerializer.Deserialize<ScenarioDefinition>(File.ReadAllText(input),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Simulation sim = new(0, saved.CreateConfig(), saved.RandomSeed);
saved.ApplyLayout(sim);
sim.SpawnAgentsAtPositions(sim.Environment.GetPlannedAgentPositions());
Console.WriteLine($"Loaded {sim.Agents.Count} occupants, {sim.Environment.Furniture.Count} furniture sets.");
var checkpoint = FinalizedSceneCheckpoint.Capture(sim, sim.Agents.Count, "latency_check");
Simulation replay = checkpoint.CreateReadySimulation();
if (replay.Running || replay.FrameIndex != 0 ||
    !sim.Agents.Select(a => a.Position).SequenceEqual(replay.Agents.Select(a => a.Position)))
    throw new InvalidOperationException("Replay did not restore the ready scene.");
Console.WriteLine("Replay checkpoint PASS.");
sim.PrecomputeToxicFume();
sim.SetRunning(true);
if (!sim.TriggerToxicFumeRelease()) throw new InvalidOperationException("Fume release failed.");
double[] times = new double[frames];
using StreamWriter csv = new(output);
csv.WriteLine("frame,step_ms,seated,standing_up,fallen_chairs");
for (int frame = 0; frame < frames; frame++)
{
    long start = Stopwatch.GetTimestamp();
    sim.Step();
    times[frame] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    csv.WriteLine($"{frame},{times[frame]:F3},{sim.Agents.Count(a => a.Posture == SeatingPosture.Seated)},{sim.Agents.Count(a => a.Posture == SeatingPosture.StandingUp)},{sim.Environment.Furniture.SelectMany(f => f.Chairs).Count(c => c.State == ChairState.Fallen)}");
}
Array.Sort(times);
Console.WriteLine($"CPU step ms: median={times[frames / 2]:F2}, p95={times[(int)(frames * .95)]:F2}, max={times[^1]:F2}; total={times.Sum():F2}");
Console.WriteLine($"Raw timings: {Path.GetFullPath(output)}. These exclude rendering and do not measure display FPS.");
