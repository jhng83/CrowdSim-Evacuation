# Experimental hybrid crowd mode

This complete source project extends the chair-chain-pushing revision. It targets lower CPU cost at larger populations, but has not been compiled or benchmarked in the delivery environment because the .NET SDK is unavailable. No 1,000-agent frame-rate guarantee or empirical realism validation is claimed.

## Enable and compare

1. Install the .NET 8 SDK and extract the entire archive.
2. Open PowerShell in the folder containing CrowdSim.sln.
3. Run the commands below. Do not use --no-build for the first test run.
4. In the application, pause and open the DEBUG inspector. Enable **Hybrid crowd mode (experimental)**, then resume. Save the scenario to retain the setting. Old scenarios default to reference mode.
5. For a controlled comparison, reload the same scene and use the same seed and blast placement. Set the mode before running each trial.

    dotnet restore .\CrowdSim.sln
    dotnet test .\CrowdSim.sln -c Release
    dotnet run --project .\src\CrowdSim.OpenGL -c Release
    dotnet run --project .\benchmarks\HybridScale\HybridScale.csproj -c Release -- .\benchmark-results 400

The benchmark project restores independently through dotnet run. Package versions remain centrally pinned in Directory.Packages.props; no new dependency is required.

## How it works

- **Shared routes:** reverse Dijkstra on a 0.5 m grid creates one cost field per exit. Nearby agents sample it instead of each running a route search. Circle occupancy updates touch changed footprints; the cost field itself is rebuilt in full, lazily. Moving-furniture refreshes are throttled to 0.6 simulation seconds. Each requested local segment is checked against current geometry. Existing individual navigation remains the fallback where the coarse field cannot connect.
- **Open-space movement:** healthy walking evacuees use individual desired speeds, smooth acceleration relaxation and small deterministic lateral variation. Existing movement collision resolution and the crowd-contact pass remain active for each person.
- **Groups of one to five:** compatible nearby evacuees with similar speeds and the same exit can reuse one steering calculation. Every member keeps their own position, speed, collision body, panic and blast exposure. Groups do not pull members into a formation. This implementation shares steering; it does not merge mass or implement group-level force physics.
- **Split early:** nearby walls, furniture, casualties, close neighbours, congestion, incompatible behavior or exit proximity return agents to detailed SFM. Injured and crawling agents always retain detailed locomotion, including existing chair pushing and detours. Groups are reconsidered every 0.6 seconds, with a one-second cooldown after splitting.
- **Staggered decisions:** eligible healthy walkers evaluate panic every third tick, using elapsed time for rise/decay. Direct shockwave arrival and active toxic-fume exposure bypass that schedule. Exit reconsideration is staggered; behavior intent retains commitment-expiry checks. These are approximations, so social responses can differ from reference mode.
- **Furniture:** existing blast destruction, furniture impacts and settling, and limited chair chain pushing remain in the pipeline.

At the default 0.15-second time step, a three-tick behavioral interval is 0.45 seconds. Detailed SFM remains active in crowded furniture areas, so a dense 1,000-person scene can still be expensive. Grouping may be rare in crowded scenes, by design.

## Benchmark interpretation

The fixed-seed runner compares reference and hybrid at 220, 500 and 1,000 people in open and furnished rooms. Each run includes a blast; random floor positions are rejected if they overlap another initial person or geometry. Warmup uses a separate simulation. CSV results include each step's CPU duration, furniture/route cost, route requests, active/exited counts and hybrid/grouped counts. The summary reports median, p95, p99, worst step, current-thread allocations and outcome counts.

Compare early post-blast spikes as well as late averages. At the default time step, 400 steps cover 60 simulated seconds and do not imply complete evacuation. Increase the argument for longer trials. Use raw exited-count changes to compare doorway throughput, and inspect queues, density and overtaking visually. Record hardware, seed, scene and build configuration when sharing results.

The benchmark excludes drawing and CSV I/O from its measured Step interval. It does not measure GPU cost, overall application FPS, or establish realistic evacuation times. Rendering has not been changed in this revision; instancing or distance-based simplification should follow measured renderer bottlenecks. Existing local spatial queries and individual hard contacts are retained; this update does not cap away collision candidates.

## Verification status

Added HybridMovementTests covers field reuse, live obstacle rejection, individual movement and speed limits, injury fallback, maximum group size, splitting cooldown and mode persistence. These tests and the existing suite still need execution on a .NET 8 machine. New source was inspected here, but static inspection cannot substitute for compilation or runtime validation.
