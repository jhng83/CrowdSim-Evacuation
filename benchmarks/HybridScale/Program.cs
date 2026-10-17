using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
string output=args.Length>0?args[0]:"benchmark-results";
int frames=args.Length>1?int.Parse(args[1]):400;
if(frames<20) throw new ArgumentOutOfRangeException(nameof(frames));
Directory.CreateDirectory(output);
using StreamWriter summary=new(Path.Combine(output,"summary.csv"));
summary.WriteLine("scene,agents,seed,hybrid,median_ms,p95_ms,p99_ms,max_ms,allocated_bytes,exited,fatal,incapacitated,mean_open_agents,mean_grouped_agents,shared_field_builds");
foreach(bool furniture in new[]{false,true})
foreach(int count in new[]{220,500,1000})
foreach(bool hybrid in new[]{false,true})
{
    Simulation warm=Create(count,furniture,hybrid);
    for(int i=0;i<25;i++) warm.Step();
    Simulation sim=Create(count,furniture,hybrid);
    string scene=furniture?"furniture":"open";
    using StreamWriter raw=new(Path.Combine(output,$"{scene}-{count}-{hybrid}.csv"));
    raw.WriteLine("frame,sim_seconds,step_ms,furniture_ms,route_ms,route_requests,active,exited,open_agents,grouped_agents");
    double[] times=new double[frames];
    double open=0,grouped=0;
    long allocated=0;
    for(int i=0;i<frames;i++)
    {
        long before=GC.GetAllocatedBytesForCurrentThread();
        long started=Stopwatch.GetTimestamp();
        sim.Step();
        times[i]=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
        if(sim.FrameIndex!=i+1) throw new InvalidOperationException("Simulation did not advance.");
        open+=sim.HybridMovement.OpenSpaceAgents;
        grouped+=sim.HybridMovement.GroupedAgents;
        raw.WriteLine($"{sim.FrameIndex},{sim.FrameIndex*sim.Config.Agent.Dt:F3},{times[i]:F3},{sim.LastFurnitureMilliseconds:F3},{sim.LastRoutePlanningMilliseconds:F3},{sim.LastRoutePlanRequests},{sim.Agents.Count(a=>a.IsActive)},{sim.Agents.Count(a=>a.State==AgentState.Exited)},{sim.HybridMovement.OpenSpaceAgents},{sim.HybridMovement.GroupedAgents}");
    }
    Array.Sort(times);
    summary.WriteLine($"{scene},{count},731,{hybrid},{times[frames/2]:F3},{times[(int)(frames*.95)]:F3},{times[(int)(frames*.99)]:F3},{times[^1]:F3},{allocated},{sim.Agents.Count(a=>a.State==AgentState.Exited)},{sim.Agents.Count(a=>a.State==AgentState.Dead)},{sim.Agents.Count(a=>a.State is AgentState.Incapacitated or AgentState.TempIncap)},{open/frames:F2},{grouped/frames:F2},{sim.HybridMovement.Fields.FieldBuilds}");
    summary.Flush();
    Console.WriteLine($"{scene} {count} hybrid={hybrid}: p95 {times[(int)(frames*.95)]:F2} ms; exited {sim.Agents.Count(a=>a.State==AgentState.Exited)}");
}
Console.WriteLine("CPU-only results; rendering/recording are not measured. These are comparisons, not realism validation.");

static Simulation Create(int count,bool furniture,bool hybrid)
{
    Simulation sim=new(randomSeed:731);
    sim.Config.HybridEnabled=hybrid;
    foreach(float y in new[]{10f,24f})
    {
        if(!sim.Environment.AddExitFromClick(0.3f,y,5)||
            !sim.Environment.AddExitFromClick(sim.Environment.Width-0.3f,y,5))
            throw new InvalidOperationException("Exit placement failed.");
    }
    if(furniture)
        foreach(float x in new[]{12f,24f,36f,48f})
        foreach(float y in new[]{12f,24f})
            if(!sim.Environment.AddFurniture(FurnitureKind.RoundTableSet,new Vector2(x,y)))
                throw new InvalidOperationException("Furniture placement failed.");
    List<Vector2> positions=sim.Environment.GetPlannedAgentPositions().ToList();
    // Fixed independent layout RNG keeps reference/hybrid starting positions identical.
    Random layout = new(731);
    int attempts = 0;
    float separation = sim.Config.Agent.Radius * 2.1f;
    while (positions.Count < count && attempts++ < 200000)
    {
        Vector2 candidate = new(1f + (float)layout.NextDouble() * (sim.Environment.Width - 2f),
            1f + (float)layout.NextDouble() * (sim.Environment.Height - 2f));
        if (sim.Environment.IsPointBlocked(candidate, sim.Config.Agent.Radius) ||
            positions.Any(p => Vector2.DistanceSquared(p, candidate) < separation * separation)) continue;
        positions.Add(candidate);
    }
    sim.SpawnAgentsAtPositions(positions);
    if(sim.Agents.Count!=count) throw new InvalidOperationException("Population mismatch.");
    sim.SetRunning(true);
    if(!sim.Running) throw new InvalidOperationException("Simulation refused to start.");
    sim.TriggerExplosion(28f,17f);
    return sim;
}
