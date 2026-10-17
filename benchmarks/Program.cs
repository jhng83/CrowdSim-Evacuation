using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.OpenGL.Rendering;
using CrowdSim.OpenGL.UI;
using ImGuiNET;

// The exact same harness compiles against original and updated sources.
// No benchmark framework, extra simulation thread, GPU, or timer is added to the application.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string output = args.Length > 0 ? args[0] : "benchmark.csv";
int seed = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 42;
const int warmup = 40;
const int measurements = 160;
List<string> rows = new() { "Mode,Agents,MedianMs,P95Ms,MeanMs,BytesPerCall,MeanActiveAgents" };
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; CPU threads={Environment.ProcessorCount}; seed={seed}");
foreach (int count in new[] { 100, 200, 500, 1000 })
{
    foreach (string mode in new[] { "wandering", "emergency", "bottleneck", "forces", "perception", "render-overview", "render-close" })
    {
        Simulation sim = CreateScene(count, mode, seed);
        bool rendering = mode.StartsWith("render-", StringComparison.Ordinal);
        IntPtr context = IntPtr.Zero;
        WorldRenderer? renderer = null;
        if (rendering)
        {
            context = ImGui.CreateContext();
            ImGui.GetIO().DisplaySize = new Vector2(1600f, 900f);
            ImGui.GetIO().DeltaTime = 1f / 60f;
            ImGui.GetIO().Fonts.AddFontDefault();
            ImGui.GetIO().Fonts.Build();
            renderer = new WorldRenderer();
        }
        try
        {
            void RunOnce()
            {
                if (rendering)
                {
                    DrawFrame(sim, renderer!, mode == "render-close");
                }
                else if (mode == "forces")
                {
                    foreach (Agent agent in sim.Agents)
                    {
                        _ = sim.SfmEngine.ComputeForces(sim, agent, Vector2.UnitY);
                    }
                }
                else if (mode == "perception")
                {
                    // Keep the workload identical: all agents remain present, same positions,
                    // same current alarm level. Spatial hashes remain valid on both revisions.
                    foreach (Agent agent in sim.Agents)
                    {
                        agent.PanicLevel = 0.6f;
                        agent.State = AgentState.Evacuating;
                        agent.ActivateDirectAlarm(0, 1000, 0.5f, seed);
                    }
                    sim.EvacuationSystem.UpdatePanic(sim);
                }
                else
                {
                    sim.Step();
                }
            }
            for (int i = 0; i < warmup; i++) RunOnce();
            // GC occurs outside the measured window; per-call samples include allocations
            // and any natural collections caused by the workload itself.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            double[] times = new double[measurements];
            long totalBytes = 0;
            long totalActive = 0;
            for (int i = 0; i < measurements; i++)
            {
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                RunOnce();
                times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                totalBytes += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                totalActive += sim.Agents.Count(a => a.IsActive);
            }
            Array.Sort(times);
            string row = $"{mode},{count},{times[measurements / 2]:F6},{times[(int)(measurements * 0.95)]:F6},{times.Average():F6},{totalBytes / (double)measurements:F2},{totalActive / (double)measurements:F2}";
            rows.Add(row);
            Console.WriteLine(row);
        }
        finally
        {
            if (context != IntPtr.Zero) ImGui.DestroyContext(context);
        }
    }
}
File.WriteAllLines(output, rows);

static Simulation CreateScene(int count, string mode, int seed)
{
    Simulation sim = new(0, randomSeed: seed);
    sim.Environment.AddExitFromClick(28f, 0.3f, 3);
    sim.Environment.AddExitFromClick(42f, 33.7f, 5);
    // Common obstacle-rich workload; avoid the dense starting region near the north exit.
    for (int y = 15; y <= 27; y += 4)
        for (int x = 8; x <= 48; x += 8)
            sim.Environment.AddBlockageFromClick(x, y);
    if (mode == "bottleneck")
    {
        List<Vector2> starts = new(count);
        for (int i = 0; i < count; i++)
            starts.Add(new Vector2(17f + (i % 50) * 0.43f, 1f + (i / 50) * 0.43f));
        sim.SpawnAgentsAtPositions(starts);
    }
    else
    {
        sim.SpawnAgents(count);
    }
    if (mode != "wandering" && mode != "emergency")
    {
        foreach (Agent agent in sim.Agents)
        {
            agent.State = AgentState.Evacuating;
            agent.Intent = BehaviorIntent.Evacuate;
            agent.ExitTarget = sim.Environment.Exits[0];
            agent.PanicLevel = 0.6f;
            agent.HelpingTendency = 0f;
            agent.ActivateDirectAlarm(0, 1000, 0.5f, seed);
        }
    }
    sim.SetRunning(true);
    if (mode == "emergency") sim.TriggerExplosion(28f, 17f);
    return sim;
}

static void DrawFrame(Simulation sim, WorldRenderer renderer, bool close)
{
    ImGui.NewFrame();
    ImGui.SetNextWindowPos(Vector2.Zero);
    ImGui.SetNextWindowSize(new Vector2(880f, 760f));
    ImGui.Begin("benchmark", ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
    renderer.Draw(sim, builderMode: false, builderTool: BuilderTool.Exit,
        blockageCells: 1, exitPersonsWide: 3, agentPlacementCount: 1,
        selectedAgentId: close ? 0 : null, cameraAgent: close ? sim.Agents[0] : null,
        followAgent: false, povEnabled: close, simulationSpeed: 1f,
        debug: DebugSettings.Options);
    ImGui.End();
    ImGui.Render();
}

static class DebugSettings
{
    internal static readonly RenderDebugOptions Options = new();
}
