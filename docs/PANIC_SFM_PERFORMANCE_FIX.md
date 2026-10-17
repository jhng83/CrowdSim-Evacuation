# Panic/SFM correction and CPU performance pass

Base: the uploaded CrowdSimCs(4).zip. Status: source candidate, **not runtime-validated**.

## Validation status

This editing environment has no .NET SDK. The attempt to download it was blocked by network policy. Consequently, the application has **not been compiled or launched**, the xUnit suite has **not been executed**, and there are **no measured before/after timings** from this environment. No claim of zero latency regression or a percentage speedup is made.

Project XML, lexical delimiter checks, original benchmark source integrity, and independent numerical checks were completed. See `STATIC_VALIDATION.json`. These checks do not substitute for C# compilation, xUnit, or Windows/OpenGL validation.

Run `scripts/compare-performance.ps1` on the target Windows machine. It runs the regression suite first, builds both benchmark revisions in Release, alternates baseline/candidate execution order, and compares median and p95 CPU time at 100, 200, 500 and 1,000 agents. Its default permitted increase is **0%**. It exits with code 2 and marks `REVIEW` if any row regresses. Measurement noise is possible; inspect repeated runs rather than treating one sample as a universal guarantee.

The user requirement of no increased latency remains an **open acceptance gate**, not a verified result.

## Corrections

1. **Contact in every direction.** The forward vision cone now gates anticipatory social repulsion only. Body compression and sliding friction apply to touching neighbours on every side. Coincident centres receive deterministic opposing normals. A casualty's physical contact radius no longer shrinks with another pedestrian's frustration.
2. **One force convention.** Desired force is mass times desired SI acceleration. Contact and friction terms use metres and metres/second. The returned force is in newtons before bounded integration. The public `Velocity` and speed parameters remain metres per fixed 0.15-second reference interval for compatibility with existing animation and behaviour constants. `VelocityMetersPerSecond` exposes SI velocity; the inspector displays m/s.
3. **One evacuation movement controller.** Removed the additional 22%/36% motor velocity blend and the stochastic near-door movement bypass. Normal and crawling evacuation use the SFM acceleration update and speed cap. Pathfinding, intent selection, yielding and assistance attendance still choose goals or intentional stops. Helpers reduce only psychological spacing force towards their own assigned casualty so that close assistance remains reachable; body contact is retained.
4. **Bounded overlap repair.** One local unordered-pair projection pass limits overlap between standing, reacting, moving and crawling people, while immobile casualties remain immobile. Corrections into static geometry are rejected. Closing normal velocity is removed without cancelling tangential movement. This is deliberately one pass; packed multi-body contacts can retain residual overlap. It is not an exact rigid-body or crowd-pressure solver.
5. **Panic attenuation and recovery.** A single social source now weakens with distance. The signal averages distance-weighted panic over source count, rather than dividing by the weights and cancelling distance. Recovery approaches a remaining lower stimulus instead of always decaying towards zero. Existing finite alarm windows, one-relay-per-episode protection and non-emotional evacuation awareness remain.
6. **Elapsed-time movement and width-dependent admission.** Displacement scales with `Dt / 0.15`. Exit admissions use a fractional per-second budget per lane, without banking unused whole slots or admitting twice on repeated calls in one frame. Five-lane openings no longer inherit the three-lane ceiling. The default admission rate retains the original one-/three-lane upper limits; it is an abstract simulator guard, not a calibrated real-world door flow.
7. **Related timing.** Reaction, freeze, crawl delay, shock propagation, wind, debris retention/lifetime and visible front timing account for the reference interval. The default 0.15-second step is unchanged. Smaller timesteps do not promise identical trajectories: legacy navigation/contact decisions and stochastic wandering remain discretized. Arbitrary timestep convergence has not been established.
8. **Visibility shielding.** Temporary obscuration uses the same line-of-sight path as the rest of the simulator, including circular blockages.

## Performance changes

- The spatial grid uses cells matched to the normal interaction range (default 2.43 m), with exact distance filtering. Interaction and vision ranges are not reduced.
- Query bounds use the actual radius and cell coordinates. A normal 3.85 m perception lookup uses at most 25 buckets, versus 289 in the old 0.5 m grid. Candidate occupancy per bucket is higher, so bucket counts alone are not timing results.
- Panic and congestion share one neighbour query. People without an alarm or evacuation-awareness cue do not cause unnecessary perception line-of-sight tests.
- Moving agents update their grid cell immediately. Later queries and exit admission use current positions. Wall queries honour their supplied radius instead of always scanning 3x3 cells.
- Each humanoid prism projects its eight distinct vertices once, reusing them for 20/24 face corners. Face ordering, colours, geometry, animation and existing LOD thresholds are preserved.
- Trail drawing iterates the existing queue, avoiding an array allocation per visible trail per render frame.
- No extra worker thread, GPU dependency, physics substep loop, or reduced simulation update frequency is introduced. The new single contact pass does add work; the optimizations are intended to offset it, and the benchmark gate checks this.

## Tests and reproducible comparisons

`PanicMovementFixTests.cs` covers rear/side contact, coincident bodies, mass-independent driving acceleration, single-controller integration, standing overlap, social distance attenuation, time-based travel, five-lane admission, repeated admission calls, spatial query correctness, moving-cell updates, large-radius wall queries and deterministic finite emergency runs.

The existing `Fix15Tests` social-signal assertion now checks the distance-attenuated value instead of the old unattenuated 0.5. Other pre-existing tests were retained. They may expose behaviours needing further tuning when actually run; they are not reported as passing.

`benchmarks/baseline/` contains exact original Core and renderer source files; `baseline-manifest.json` contains their SHA-256 hashes. The same benchmark program compiles separately against baseline and updated sources, with the original dependency versions.

Workloads include live wandering, blast response, a dense doorway approach, fixed-population SFM forces, fixed-population perception, overview drawing and close POV drawing. CSV files report active population as well as time and allocation. Live runs can have different survivor/exit counts after the corrections; use the fixed-population rows when comparing equal workloads.

Drawing benchmarks measure **CPU draw-list generation**, including ImGui frame work. They do not create an OpenGL window, submit to the GPU, encode video or measure mouse-to-screen latency. After they pass, check the actual app's Debug/Performance panel at the same window size, camera, scenario and simulation speed. Test recording separately.

## Running

1. Extract the project to a fresh folder. The archive intentionally contains source, not the stale pre-fix `publish`, `bin` or `obj` output.
2. Double-click `run_release.cmd`, or use `dotnet run -c Release --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj` from the solution folder.
3. From PowerShell, run `./scripts/compare-performance.ps1` for the regression and CPU latency gates.
4. Inspect `validation/comparison.csv`, `validation/regression.trx` and the per-round logs. Send back any failure or REVIEW rows before relying on the no-regression claim.
5. For a new Windows executable after validation: `dotnet publish src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release -r win-x64 --self-contained true -o publish/CrowdSim`.

The old documentation remains as development history. This note takes precedence where force integration, time units, contact rules, exit admission or verification status differ.
