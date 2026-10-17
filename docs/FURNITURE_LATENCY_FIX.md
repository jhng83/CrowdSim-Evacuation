# Furniture load, replay and latency correction — 9 September 2026

The supplied `toxic_fume_scenario1` scene contains 205 occupants (80 seated), eight
round table sets and four independent circular obstacles. Its original JSON is included
as `tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json`.

## What changed

- Loading restores physical furniture footprints before standing groups. The builder's
  additional empty departure-space reservation is not reapplied during restoration.
  Standing agents may occupy that free space, as the builder permits. Actual overlaps
  with tables/chairs, overlapping furniture and furniture outside the room remain errors.
- The same restoration path fixes **Replay from Scene Start**. Replay restores the authored
  scene, intact chairs, all occupants and a ready fume cache, paused at frame zero.
- Furniture-heavy rooms use a 0.25 m navigation grid and A* with eight local connections
  per cell. Obstacle bounds are rasterised; exact swept collision checks validate edges,
  which are cached until geometry changes. String pulling produces continuous waypoints
  for the existing SFM, rather than grid-stepped agent movement.
- The original visibility planner remains for smaller layouts and as a fallback for
  narrow passages the grid cannot represent. No chair obstacles are removed or ignored.
  Worst-case fallback routing can still be expensive in pathological layouts.
- Segment/circle tests reject disjoint bounding boxes before their detailed calculation.
- The application limits wall-time admitted after a stall and yields to rendering/input
  after an update consumes 12 ms. Physics remains fixed-step; this budget cannot interrupt
  an individual step, but prevents a series of expensive steps running unseen. Under load,
  simulation time slows rather than rapidly catching up with elapsed real time.

No new NuGet packages or GPU requirements were added. Navigation and simulation still run
on the CPU; this change reduces work rather than moving it to the GPU.

## Reproduction and measurements

The original load and replay both failed with the reported furniture-overlap exception.
To measure its simulation independently of that loader error, the baseline diagnostic
restored furniture before standing groups, preserving the original geometry, positions
and random seed. The final project loads the original JSON directly.

One 600-step headless run (90 simulated seconds at the existing 0.15 s step) measured:

| CPU simulation step | Previous code | Corrected code |
|---|---:|---:|
| Median | 7.78 ms | 3.35 ms |
| 95th percentile | 16.31 ms | 16.70 ms |
| Worst step | 402.00 ms | 81.16 ms |
| Total over 600 steps | 5799.2 ms | 2944.6 ms |

These are single-run container measurements, not a hardware-independent guarantee.
The worst measured stall fell about 80%; the 95th percentile was similar/slightly higher.
Navigation changes can produce different valid evacuation trajectories, so later frames
do not have identical crowd arrangements. Raw before/after CSVs are under `validation/`.
Fume precomputation was separately about 8–17 ms; it was not the dominant measured stall.

## Validation

- Core, OpenGL application, tests and the standalone benchmark compiled with Roslyn and
  .NET 8 reference assemblies. Existing dependency-version unification warnings remain.
- 129 xUnit Fact/Theory cases passed using a reflection runner with real xUnit assertions,
  including the supplied scene's load, disk round-trip, replay after fume/chair movement,
  overlap rejection and body-clear routes before/after toppling.
- The bundled benchmark ran against the corrected Core and passed its replay check.
- Standard VSTest crashed inside its test engine before running tests in this container;
  the reflection run is not a claim that `dotnet test` passed here.
- Interactive Windows/OpenGL display, recording and GPU FPS were not tested here.

## Run on Windows

Extract into a fresh folder and run from the folder containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\CrowdSim.sln -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Load your existing saved scenario; no JSON editing or rebuilding the scene is required.
Finalise it, start, release fume, and use **Replay from Scene Start** to return to its
ready state. Do not run an old executable from another extracted folder.

Optional CPU benchmark, from the same solution directory:

```powershell
dotnet run --project .\benchmarks\FurnitureLatency\FurnitureLatency.csproj -c Release
```

It loads the included scene, checks replay, runs 600 steps and writes `furniture-latency.csv`.
Optional arguments after `--`: scene JSON path, output CSV path, number of steps.
