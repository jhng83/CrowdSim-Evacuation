# Adaptive Explosion and Toxic Fume navigation

## Delivery status

This revision extends CrowdSimCs_NaturalNavigation.zip (the working Explosion update) to Toxic Fume. It contains complete source,
pinned existing packages, xUnit regression specifications and a headless comparison
benchmark. It has NOT been compiled, run or benchmarked in the delivery environment:
that environment has no .NET SDK, and an attempted SDK download was unavailable.
Current source checks are recorded in TOXIC_NATURAL_NAVIGATION_QA.md; NATURAL_NAVIGATION_QA.md describes the earlier Explosion revision. Earlier performance numbers
elsewhere in the project apply to older revisions, not this one.

The shared recovery behaviour is enabled by default for both Explosion and Toxic Fume. The population
selector is unchanged; choose approximately 600 people for your target workload.
The existing optional HybridEnabled setting remains false by default.

## Behavioural objective

Keep a known exit and a coarse intention, but stop repeating an unsuccessful local
solution. Maintain the existing physical crowd interactions and furniture simulation.
The controller does not receive future furniture landing positions.

1. Follow a still-useful route rather than replacing it simply because two seconds passed.
2. Observe actual movement, not merely whether a new route was assigned. Short returning
   trajectories are identified as loops. Replanning does not erase movement history.
3. When blocked, test a finite set of nearby body-clear positions. Moving sideways or
   briefly backwards is allowed. Nearby people, furniture and walls constrain candidates.
4. Commit to one selected position long enough to move, but abandon it if a new obstacle
   blocks it, movement stops, or its deadline expires. Failed positions and recent visits
   are penalised so the next attempt is less likely to repeat the same mistake.
5. After repeated failures, widen the local search and request a new global route from
   the actual completed detour position. Repeated ineffective episodes can reconsider
   another known exit, with a cooldown against rapid exit switching.
6. Preserve real waiting near congested doors. A physically sealed space does not gain
   a fictitious escape: the agent waits and reassesses while collisions remain enforced.

This is purposeful recovery, not a free-running random walk. A six-second ceiling on a
continuous circular-obstacle-following episode requests recovery; it is NOT a promise
that everyone clears an obstacle within six seconds. Queue latency, crowd pressure,
injury, reaction delays and unavailable physical routes can extend actual clearance.

## Architecture

- `NaturalNavigationMemory`: fixed-capacity position/failure histories and cached decisions.
- `EvacuationSystem.NaturalNavigation`: progress supervisor, local candidate search,
  detour commitment, exit reconsideration and bounded FIFO work scheduling.
- Existing `SharedExitFields`: shared reverse-Dijkstra guidance for many people heading
  to an exit. A full individual A* search is a queued fallback rather than a routine
  periodic operation for every normal walker.
- Existing SFM, casualty bypass, chair pushing and hard geometry contacts retain control
  of physical movement. Steering a person toward a target never directly relocates them.
- Existing optional hybrid open-space movement receives progress metrics and hazard
  avoidance, and disengages while a person is recovering.

A legacy wall-follow stall branch previously could bypass the entire navigation update.
Natural mode bypasses that branch so the progress supervisor keeps receiving observations.

## What perception does and does not model

Local recovery is limited to a nearby search area. Alternate-exit congestion is queried
only when an exit is within the configured observation range and wall line of sight;
otherwise the agent uses uncertain/default or remembered information. Moving-furniture
avoidance uses nearby current position/velocity, a forward field with close peripheral
awareness, and at most 0.55 seconds of closest-approach extrapolation. It does not predict
ballistic landing points or read future collision outcomes.

IMPORTANT: this is NOT a complete per-agent belief/mental-map implementation. The coarse
route planner and shared fields still use the simulation's current geometry, and initial
exit assignment retains the existing map-based logic. Existing line-of-sight checks
ignore low furniture. The update adds local perception and bounded recovery, not fully
individualised unseen-obstacle memories, calibrated attention, or empirical psychology.

Crawling and assistance retain their existing dedicated movement/recovery rules. The new
natural recovery supervisor applies to walking evacuating/injured agents whose intent
is evacuation or crowd following. Initial threat-escape and seated/standing transitions retain their dedicated rules.
Toxic Fume now shares normal-walker recovery and adds locally sampled fume risk.

## Latency controls for approximately 600 people

The new logic runs on the CPU; the existing OpenGL rendering path is unchanged. No GPU
migration or measured FPS improvement is claimed.

| Control | Default | Meaning |
|---|---:|---|
| `NaturalDecisionSeconds` | 0.30 s | Nominal simulated-time observation/cache interval, with ID-based phase variation and timestep rounding. |
| `NaturalNoProgressSeconds` | 1.65 s | Base stalled-motion threshold, modified by patience; door queues wait longer. |
| `NaturalDetourCommitSeconds` | 3.2 s | Base temporary-goal deadline, extended for distance/speed up to 10 s. |
| `NaturalExitReconsiderSeconds` | 6.0 s | Persistent failure/episode threshold before considering another known exit. |
| `NaturalFailureMemorySeconds` | 12.0 s | Lifetime of failed-location penalties and recent failed-exit discouragement. |
| `NaturalPerceptionRadiusMeters` | 7.0 m | Maximum observation horizon before visibility reduction; recovery steps are at most 3.2 m. |
| `NaturalCircleMaximumSeconds` | 6.0 s | Requests recovery instead of indefinitely renewing one circle-follow episode. |
| `NaturalRoutePlansPerStep` | 4 | Maximum full searches serviced by the NEW normal-navigation route FIFO per simulation step. |
| `NaturalRecoveryProbesPerStep` | 24 | Maximum agents receiving an expensive local-recovery search per step. |
| `NaturalToxicRiskWeight` | 5.0 | Soft preference against locally observed plume on a candidate; not a physical toxicity constant. |

These are engineering defaults, not empirically calibrated human-response constants.
Configuration is in `src/CrowdSim.Core/Configuration/SimulationConfig.cs`, in `AgentConfig`.

Each local search has at most 36 candidates (12 headings and three distances), collects
nearby agents once, and reuses geometry buffers. Six failed-location entries and 24
position samples are stored per agent. Requests are deduplicated, and FIFO scheduling
avoids repeatedly favouring low agent IDs. Shared fields reuse work across people.

The budgets limit operation counts, NOT elapsed milliseconds. A full A* search and a
shared-field rebuild can still be expensive. Legacy assistance/crawling route work,
physics, rendering, recording and contact resolution are outside these new budgets.
When all 600 request local recovery simultaneously, 24-per-step servicing alone needs
25 service steps to visit them all; normal local movement continues in the meantime.
Do not interpret a simulated-time reaction threshold as a wall-clock performance promise.

## Toxic Fume integration

The same progress/loop memory, detour deadlines, failed-location penalties, wider retries,
exit reconsideration and FIFO budgets now apply to both scenarios. The fume-specific layer
is `Systems/EvacuationSystem.ToxicNavigation.cs`.

- Recovery candidates are penalized for locally observed current concentration, high patches,
  an increasingly contaminated endpoint and the length of the observed segment. It is a soft
  cost: a fully contaminated region is not treated as an impassable wall.
- Each risk sample reads at most five points from the existing cached plume at the current
  release time. It stops at a wall or the configured perception horizon. Preview time and
  future cell-arrival times are not navigation inputs.
- A committed detour is abandoned when a newly worsened patch appears ahead of the person.
  Increasing concentration at the person alone does not cancel an improving retreat.
- A progressing person can reconsider the chosen exit when a locally visible plume worsens
  along the intention; this does not require waiting for the no-progress clock. The default
  20-percent exit-cost improvement rule and an additional switch cooldown limit oscillation.
- Short-horizon fume steering runs before detailed circular-obstacle/casualty handling, and
  each member of the optional hybrid fast path also evaluates its own local plume.
- Furniture flight avoidance is explicitly Explosion-only. Tables, ordinary chair pushing,
  body contacts and physical collision constraints remain shared.

The risk sampler creates no per-candidate arrays or collections and reuses existing spatial
queries; those queries may grow their retained scratch buffers. The fume propagation cache
is never rebuilt by navigation. Exit hazard checks run about every 0.6-0.9 simulated
seconds after timestep rounding/ID variation, not once per frame. Hazard steering runs at
roughly the normal 0.3-second decision cadence. Each feasible recovery candidate needs at
most five concentration samples, so a 36-candidate probe needs at most 180 such samples,
plus at most five samples to remember the selected goal's baseline. Geometry and crowd
rejection often reduce that work. Existing FIFO limits remain 24 probes and 4 individual
route searches per simulation step; shared-field rebuilding is outside those limits.

This is not an optimal exposure-minimizing planner. The coarse planner/shared fields still
route around current geometry; the new hazard layer applies local risk cues, detours and
exit preferences. Sampling may miss a narrow pocket between sample points, hidden fume is
not known, and a lower current concentration does not guarantee future safety. Human fume
perception remains an abstract model rather than a claim about a particular chemical.
Exposure accumulation, impairment, permanent incapacitation and the precomputed spread
model are unchanged. None of the new recovery rules revive an incapacitated person.

## Build, tests and launch on Windows

Use a fresh extracted directory rather than mixing source and old bin/obj outputs.
From the directory containing CrowdSim.sln:

```powershell
dotnet --info
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

For both natural-navigation regression classes:

```powershell
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~NaturalNavigationTests"
```

Or run `powershell -ExecutionPolicy Bypass -File .\scripts\verify-natural-navigation.ps1`.
That process-local invocation does not change the machine's execution policy. The script
stops at the first failed restore/build/test; it never reports failed tests as passes.

To compare with the retained reference navigation, construct `SimulationConfig` with
`Agent = new AgentConfig { NaturalNavigationEnabled = false }`. This setting is not a
new UI switch. Leave `HybridEnabled` identical between paired comparisons.

## Reproducible 600-agent CPU comparison

```powershell
dotnet run --project .\benchmarks\NaturalNavigationLatency\NaturalNavigationLatency.csproj -c Release -- .\benchmark-results\natural-navigation 400 600 false
```

The benchmark creates 24 round tables, 240 seated occupants and 360 standing occupants,
with four exits. The optional fifth argument selects `Explosion` (the unchanged default)
or `ToxicFume`. In Toxic Fume mode, precomputation occurs before timing, then the central
release and the existing perception/reaction cascade run normally. Seeds 731, 732 and 733 run with natural mode
both off and on, alternating run order. Each case has a separate 35-step warm-up before
a fresh measured scene. At the retained 0.15-second timestep, 400 measured steps cover
60 simulated seconds. Increase the step count for longer evacuation outcomes.

Outputs include per-step CSV traces, median/p95/p99/max CPU step time, allocation counts,
exit/casualty totals, recovery totals, route queues and work-budget maxima. CSV formatting
and output are outside the timed block. Allocation counts cover only the stepping thread.
The benchmark does not create a graphics window and excludes rendering/recording latency.
A runtime/OS/core-count manifest accompanies results.

`false` in the command above is the hybrid setting, not the natural-mode setting: the
program always runs both natural modes. To examine hybrid as a separate factor, repeat
with `true` and a different output directory. The reference is this revision's retained
legacy mode, not a separately built copy of the original ZIP.

No benchmark results are supplied for this revision because it has not been executable
in the delivery environment. Do not reuse older benchmark CSVs as evidence for this change.

For the Toxic Fume paired comparison:

```powershell
dotnet run --project .\benchmarks\NaturalNavigationLatency\NaturalNavigationLatency.csproj -c Release -- .\benchmark-results\toxic-natural-navigation 400 600 false ToxicFume
```

`summary.csv` includes the scenario and setup-only fume precomputation/cache measurements.
The `false` argument remains the hybrid setting; both natural-navigation toggle values
are always compared. Use separate output directories for separate scenario experiments.

## Regression coverage and manual checks

`NaturalNavigationTests.cs` covers stationary replanning, true progress, legitimate
away-from-exit progress, loop detection, expired failure memory, swept-circle clearance,
backward escape from a dead end, sealed geometry, newly blocked detours, remote exit
information, repeated-failure exit reconsideration, 600-request deduplication, FIFO fairness,
work budgets, and seeded repeatability. These are test specifications until run locally.

Two existing assertions were updated transparently: `BehaviorRebuildTests` accepts valid
shared-field guidance as well as explicit waypoints; the legacy global exit-cost test in
`GuardrailTests` explicitly disables natural mode so it still tests the retained scorer.
The new natural class separately tests event-driven exit behaviour. The previous explicit
Toxic-Fume-disabled expectation has been replaced with a disabled-toggle test for both
scenarios, because that old expectation is exactly what this revision changes. The separate
`ToxicNaturalNavigationTests.cs` covers enabling, no premature/future plume awareness, wall
and range limits, fume-aware detours and invalidation, retained improving retreats, hazard
exit reconsideration, hybrid movement, no flying-furniture steering in Toxic Fume,
incapacitated-agent exclusion, 600-request deduplication, FIFO fairness and reproducibility.
The unchanged `ToxicFumeScenarioTests.cs` still specifies permanent incapacitation/cache rules.

Manual visual acceptance checks:

- Start beside a table, then with a table and adjacent wall. Confirm temporary retreats
  occur instead of repeated clockwise/counter-clockwise reversals at the same spot.
- Block a previously selected detour with a displaced chair. Confirm the goal is abandoned
  and remembered rather than followed through the obstacle.
- Congest an exit. Confirm ordinary waiting is not instantly interpreted as a trap, while
  persistent repeated failure can cause an exit change where another exit is known.
- Seal a pocket. Confirm nobody teleports through walls or overlaps a body to guarantee escape.
- Run the same 600-agent scene/seed with recording off, then on. Check p95/p99/max frame
  behaviour on the actual Windows GPU machine, not only headless CPU averages.

Selected-agent diagnostics are visible in both scenarios and show the adaptive stage, no-useful-progress timer, attempted
recoveries, completed detours, exit reconsiderations and current temporary escape point.

There is no mathematical guarantee that every mobile person escapes every arbitrary
layout. Recovery strategies have bounded commitments and repeated attempts; impossible
geometry and crowd blockage remain physically meaningful outcomes. This revision is a
behavioural engineering experiment, not a validated life-safety evacuation model.

## Main changed source files

New: `Model/NaturalNavigationMemory.cs`, `Systems/EvacuationSystem.NaturalNavigation.cs`,
`tests/CrowdSim.Tests/NaturalNavigationTests.cs`, and `benchmarks/NaturalNavigationLatency`.

Integrated through `Simulation.Step.cs`, `Simulation.Collisions.cs`,
`Simulation.FurnitureImpacts.cs`, `Model/Agent.cs`, `Configuration/SimulationConfig.cs`,
`Systems/EvacuationSystem.cs`, `Systems/HybridMovementSystem.cs`,
`Systems/SharedExitFields.cs`, `Systems/EvacuationSystem.OpenSpace.cs`, and the selected-agent
panel in `src/CrowdSim.OpenGL/UI/SimulationUi.cs`. No NuGet packages were added or changed.

### Toxic Fume acceptance checks

Run the same table-heavy layout and seed before/after the change. Observe a stalled walker,
a newly contaminated detour, a person already escaping to cleaner space, a congested exit,
and an incapacitated body. Confirm temporary retreat/retry without wall penetration, no
endless goal renewal, no exit ping-pong, and no incapacitated recovery. Compare p95/p99/max
CPU-step and actual rendered-frame latency at 600 agents, with hybrid and recording held
constant between pairs. No measured latency improvement is claimed for this revision.
