# M10 performance update 2 — CPU evidence

Measured 2026-10-22, Linux x64, .NET 8.0.21/SDK 8.0.415, 4 reported CPUs.
Baseline is **38ab7420478fe7f9b47a478a294eb77a625187c5**, the already optimized M10
build the user tested. Candidate is this iteration's production portable source.
[Trial CSV](M10_PERFORMANCE_2_BENCHMARK.csv),
[environment, assembly/fixture/state hashes and allocations](M10_PERFORMANCE_2_ENVIRONMENT.json).

## Repeated 120-tick comparison

Each cell reports the **median of three trial mean step durations**, in ms. Runs
were serial, paired baseline/candidate with DOTNET_TieredCompilation=0 equally,
12 warmup ticks on a separate model and 120 measured ticks on a fresh model.
All cases use seed 731, dt 0.15 s and the same hazard at tick zero. Shared serialized
v4 dense fixtures supply identical placement to both versions. Dense is the actual
Debug-generated 800-person/32-table fixture (320 seated); open is the actual Debug
1,000-person unfurnished comparison. User detonation position/timing may differ.

| Hazard | Layout | Hybrid | 38ab742 ms | Update 2 ms | Baseline/candidate |
| --- | --- | --- | --- | --- | --- |
| Explosion | dense800 | False | 19.36 | 18.57 | 1.04× |
| Explosion | dense800 | True | 18.39 | 18.54 | 0.99× |
| Explosion | open1000 | False | 10.36 | 7.75 | 1.34× |
| Explosion | open1000 | True | 10.23 | 7.68 | 1.33× |
| ToxicFume | dense800 | False | 17.14 | 15.60 | 1.10× |
| ToxicFume | dense800 | True | 18.28 | 15.54 | 1.18× |
| ToxicFume | open1000 | False | 10.42 | 7.78 | 1.34× |
| ToxicFume | open1000 | True | 10.25 | 8.01 | 1.28× |

Open cases improve in this window. Dense gains are modest; the Explosion/Hybrid-on
result is slightly slower and within observed timing noise. These data do not
establish a substantial dense-scene CPU speedup. All trial means/p95/response/SFM
values are retained in the CSV, including that case.

Median mean managed allocations per tick fall from roughly **225,608 to 160,408
bytes** for dense Explosion/Hybrid off, and **116,874 to 63,408 bytes** for open
Explosion/Hybrid off. Other allocation trials are recorded in the JSON. Removing
the segment query's capturing lambda accounts for work avoided across navigation
calls; this does not make the entire simulation allocation-free.

## Exact behavior and later crowded phases

All **24 paired ordered state hashes match exactly**, covering 5,760 measured
simulation ticks across versions. The existing harness observes every measured
tick's ordered agents (positions, velocities, state, posture, panic, intent, injury,
exposure, rise, routes, exit target, navigation/bottleneck mode), furniture/chair
motion/occupancy, debris and exit metrics outside the timing window. Original
reference traces/oracle and goldens remain unchanged.

A separate **240-tick paired run** checks the later phases visible in the user's
screenshots (open tick 110, dense tick 190). Its additional **8 hashes match**,
covering another 3,840 measured ticks (9,600 measured ticks total across both runs).
The environment JSON retains its hashes and summaries. Its single-trial open CPU
ratios are 1.12–1.18× and dense ratios 1.01–1.09×; it is a correctness extension,
not the source of the three-trial table. Workload composition changes as people exit.

The full regression run passes **404 original / 415 portable / 239 migration**
cases. Seven new cases test the original strict wall predicate, exact circle
force formula, no-allocation warm segment queries and visual reuse invalidation.

## Interpretation and reproduction

These are **portable .NET CPU** measurements, not Unity Mono or application FPS.
They do not execute native Unity world-view reuse, material caching, orthographic
LOD projection, GPU rendering, UI paint, recording or the optional Editor Debug/
Release setting. Those changes may reduce Unity costs but need local measurement.
Shared-worker noise, different CPU/runtime and detonation timing/layout matter.
There is no new FPS promise or implication that 124 ms becomes a specific value.

Production keeps all distant SFM forces, including the nonzero clamp floor, and
all physical contacts/guards. No approximation/Hybrid-default/dt/RNG/order change
is used to obtain these results. The first update's 1.6–1.9× dense figure compared
against original M10; this report compares against the already improved 38ab742.

```bash
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-perf2 120 3 38ab7420478fe7f9b47a478a294eb77a625187c5
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-perf2-long 240 1 38ab7420478fe7f9b47a478a294eb77a625187c5
```

The script extracts the baseline into a temporary archive, builds the same current
harness, swaps only the portable DLL for baseline runs and requires exact matched
state hashes. Baseline 38ab742 already has phase clocks, so it needs no additional
instrumentation. It never edits original implementation/tests or regenerates goldens.
Follow [local testing](M10_PERFORMANCE_2_LOCAL_TESTING.md) before M11.
