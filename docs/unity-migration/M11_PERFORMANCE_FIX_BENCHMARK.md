# M11 CPU lag correction — benchmark evidence

Measured 2026-10-22 (Asia/Singapore), Linux x64, .NET 8.0.21/SDK 8.0.415,
4 reported CPUs. Baseline is initial M11 **e76d7262bceadc1f314f306274d6df025c0f9468**
(v0.11.0). Candidate is this correction's production portable source (v0.11.1).
[All 48 trial timing rows](M11_PERFORMANCE_FIX_BENCHMARK.csv) and
[environment, binary/fixture/state hashes and allocations](M11_PERFORMANCE_FIX_ENVIRONMENT.json).

These are CPU-only portable .NET Release measurements, **not Unity Mono, GPU or
application FPS**. All trials are serial paired baseline/candidate, equal
DOTNET_TieredCompilation=0, 12 warmup ticks on a separate model, then 200 measured
ticks on a fresh model. Seed 731, dt .15 s, same generated open-1000 and serialized
dense-800/32-table/320-seat inputs. The current harness swaps only the baseline
portable DLL; the baseline already contains all clocks. No original reference or
golden is edited. Data below uses the median of three trial mean tick durations.

The screenshot's Release Toxic Fume action appears available, so both **running
without triggering the hazard** and **triggering at tick zero** are measured.
Released Explosion is at (28,17); Toxic Fume precompute runs for both modes.
User release timing/state may differ; local comparisons must match these details.

## Hazard triggered at tick zero

| Hazard | Layout | Hybrid | M11 baseline ms | Fix ms | Baseline/fix |
| --- | --- | --- | --- | --- | --- |
| Explosion | dense800 | False | 18.28 | 16.40 | 1.11× |
| Explosion | dense800 | True | 17.49 | 15.71 | 1.11× |
| Explosion | open1000 | False | 8.52 | 7.73 | 1.10× |
| Explosion | open1000 | True | 8.27 | 7.78 | 1.06× |
| ToxicFume | dense800 | False | 16.17 | 13.87 | 1.17× |
| ToxicFume | dense800 | True | 16.78 | 14.46 | 1.16× |
| ToxicFume | open1000 | False | 8.64 | 8.16 | 1.06× |
| ToxicFume | open1000 | True | 8.09 | 8.71 | 0.93× |

## Running before hazard release

| Hazard | Layout | Hybrid | M11 baseline ms | Fix ms | Baseline/fix |
| --- | --- | --- | --- | --- | --- |
| Explosion | dense800 | False | 3.87 | 2.52 | 1.53× |
| Explosion | dense800 | True | 3.90 | 2.42 | 1.61× |
| Explosion | open1000 | False | 2.49 | 2.36 | 1.06× |
| Explosion | open1000 | True | 2.52 | 2.48 | 1.02× |
| ToxicFume | dense800 | False | 3.87 | 2.40 | 1.61× |
| ToxicFume | dense800 | True | 3.84 | 2.41 | 1.59× |
| ToxicFume | open1000 | False | 2.56 | 2.74 | 0.93× |
| ToxicFume | open1000 | True | 2.67 | 2.52 | 1.06× |

Dense Toxic Fume/Hybrid-off uses roughly **38% less CPU before release** and
**14% less after release** in these windows. Other dense cases improve too.
Open-1000 results are mixed, including slower Toxic Fume cases (released Hybrid-on
and unreleased Hybrid-off); no across-the-board speedup is claimed. Shared-worker
noise, scene evolution, platform/runtime and Editor overhead can change results.
No hardware-specific FPS promise or conversion of the user's ~94 ms is justified.
The CSV retains every trial mean/p95, response/SFM and movement/collision timings,
including slower cases. Allocation data includes index initialization/rebuild costs;
this does not make the whole simulation allocation-free.

## Preservation checks and limits

All **48 paired ordered state hashes match exactly**, observing **19,200 measured
ticks across versions**: 8 hazard/layout/Hybrid cases × 2 release modes × 3 trials
× 200 ticks × 2 versions. Hashes observe each tick's ordered agents (positions,
velocities, state, posture, panic, intent, injury, exposure, rise, route indices/
waypoints, exit target and local navigation/bottleneck mode), furniture/chair
position/heading/velocity/height/state/occupancy, debris and exit metrics outside
the timed step. This supports behavior preservation for these tested inputs; it
is not an exhaustive serialization of every internal field or proof for all inputs.

All 404 original, 415 portable and 272 migration tests pass in this correction's
full run, plus unchanged six-case captures/oracle, legacy/custom-stage interop and
independent production AVI decoding. Nine new cases cover conservative candidates,
boundaries/order, dense correction chains, geometry/order replacement and fallback,
including direct live same-count edits between steps. See VALIDATION.md.

The index only removes provably distant candidates from contact/navigation queries;
original exact predicates and list order still decide outcomes. Each positional
correction queries the next eligible original index using the new position.
Chair proxy changes invalidate it immediately; each tick starts a fresh snapshot,
and non-Step authoring/test calls inspect live lists. Every distant SFM circle
force including its clamp floor remains; no approximation, dt/RNG/guard/order or
Hybrid-default change is used. GPU rendering, frame scheduling and recording stay.

No Unity Editor is installed in cloud. Actual frontend/Mono/shader compilation,
55 EditMode/26 PlayMode cases, native UI/GPU/rendering/AVI capture/quit and Windows
build/player/FPS are unexecuted. Keep Editor optimization unchanged; Debug reports
its mode/version first. Local performance and full M11 acceptance remain pending.

## Reproduce

```bash
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/m11-released 200 3 e76d726
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/m11-unreleased 200 3 e76d726 --profile-unreleased
```

Use fresh output paths. Follow [local retesting](M11_PERFORMANCE_FIX_LOCAL_TESTING.md).
