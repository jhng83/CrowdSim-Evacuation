using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;

if (args.Length > 0 && args[0] == "--write-dense-fixtures")
{
    Directory.CreateDirectory(args[1]);
    foreach (ScenarioType scenario in new[] { ScenarioType.Explosion, ScenarioType.ToxicFume })
    {
        var definition = CrowdSim.Core.Persistence.ScenarioDefinition.Capture(DenseCrowdLayout.Create(false, scenario), 800, "Dense 800 / 32 tables");
        File.WriteAllText(Path.Combine(args[1], "dense800-" + scenario + ".json"), CrowdSim.Core.Persistence.ScenarioJson.Serialize(definition));
    }
    return;
}
if (args.Length > 0 && args[0] == "--profile")
{
    PerformanceComparison.Run(args[1], args.Length > 2 ? int.Parse(args[2]) : 120, args.Length > 3 ? args[3] : null); return;
}
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string output = args.Length > 0 ? args[0] : throw new ArgumentException("Provide an output directory outside source.");
int frames = args.Length > 1 ? int.Parse(args[1]) : 60;
if (frames < 20 || frames > 2000) throw new ArgumentOutOfRangeException(nameof(frames));
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "environment.json"), JsonSerializer.Serialize(new {
    Utc = DateTime.UtcNow, Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(), LogicalProcessors = Environment.ProcessorCount,
    WarmupTicks = 12, MeasuredTicks = frames, Seed = 731, StepSeconds = .15,
    Note = "CPU-only engine and pure diagnostic/geometry commands. No Unity, GPU, application FPS or recording measured."
}, new JsonSerializerOptions { WriteIndented = true }));
using StreamWriter csv = new(Path.Combine(output, "summary.csv"));
csv.WriteLine("hazard,layout,population,hybrid,seed,steps,step_median_ms,step_p95_ms,step_p99_ms,step_max_ms,step_bytes_mean,pure_view_mean_ms,pure_view_bytes_mean,exited,fatal,incapacitated,mean_open,mean_grouped,field_builds");
foreach (ScenarioType scenario in new[] { ScenarioType.Explosion, ScenarioType.ToxicFume })
foreach (bool furnished in new[] { false, true })
foreach (int count in new[] { 220, 500, 1000 })
foreach (bool hybrid in new[] { false, true })
{
    Simulation warm = Ready(count, furnished, hybrid, scenario);
    List<HumanPart> warmParts = new(16);
    for (int i = 0; i < 12; i++)
    {
        warm.Step(); PresentationFrame warmFrame = PresentationFrame.Capture(warm);
        _ = new DiagnosticFrame(warm, 0);
        foreach (AgentVisualFrame agent in warmFrame.Agents) HumanGeometry.Build(agent, false, i * .1f, warmParts);
    }
    Simulation model = Ready(count, furnished, hybrid, scenario);
    List<HumanPart> parts = new(16); double[] times = new double[frames];
    long stepBytes = 0, viewBytes = 0; double views = 0, open = 0, grouped = 0;
    for (int i = 0; i < frames; i++)
    {
        long before = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
        model.Step(); times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        stepBytes += GC.GetAllocatedBytesForCurrentThread() - before;
        if (model.FrameIndex != i + 1 || model.Agents.Any(a => !float.IsFinite(a.Position.X + a.Position.Y))) throw new InvalidOperationException("Tick/finite guard failed.");
        before = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
        PresentationFrame frame = PresentationFrame.Capture(model);
        _ = new DiagnosticFrame(model, frame.Agents.FirstOrDefault()?.Id);
        foreach (AgentVisualFrame agent in frame.Agents) HumanGeometry.Build(agent, false, i * .1f, parts);
        views += Stopwatch.GetElapsedTime(started).TotalMilliseconds; viewBytes += GC.GetAllocatedBytesForCurrentThread() - before;
        open += model.HybridMovement.OpenSpaceAgents; grouped += model.HybridMovement.GroupedAgents;
    }
    Array.Sort(times);
    double Percentile(double p) => times[(int)Math.Ceiling(p * frames) - 1];
    csv.WriteLine($"{scenario},{(furnished ? "furnished" : "open")},{count},{hybrid},731,{frames},{Percentile(.5):F4},{Percentile(.95):F4},{Percentile(.99):F4},{times[^1]:F4},{stepBytes / (double)frames:F0},{views / frames:F4},{viewBytes / (double)frames:F0},{model.Agents.Count(a => a.State == AgentState.Exited)},{model.Agents.Count(a => a.State == AgentState.Dead)},{model.Agents.Count(a => a.State is AgentState.Incapacitated or AgentState.TempIncap)},{open / frames:F1},{grouped / frames:F1},{model.HybridMovement.Fields.FieldBuilds}");
    csv.Flush(); Console.WriteLine($"{scenario} {(furnished ? "furnished" : "open")} {count} Hybrid={hybrid}: p95 {Percentile(.95):F2} ms (CPU only)");
}
static Simulation Ready(int count, bool furnished, bool hybrid, ScenarioType scenario)
{
    Simulation model = CrowdScaleLayout.Create(count, furnished, hybrid, scenario);
    model.SpawnAgentsAtPositions(model.Environment.GetPlannedAgentPositions());
    if (scenario == ScenarioType.ToxicFume) model.PrecomputeToxicFume();
    model.SetRunning(true);
    if (scenario == ScenarioType.Explosion) model.TriggerExplosion(28, 17); else model.TriggerToxicFumeRelease();
    return model;
}
