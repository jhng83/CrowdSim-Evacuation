# M9 CPU comparison evidence

Executed 2026-10-22 (Asia/Singapore), with UTC timestamps in
[M9_BENCHMARK_ENVIRONMENT.json](M9_BENCHMARK_ENVIRONMENT.json). Cloud worker:
Debian 13 x64, .NET 8.0.21/SDK 8.0.415, four reported logical processors,
AMD EPYC 9V74 host. No Unity Editor or GPU rendering was run.

The shared CrowdScaleLayout adapter supplies four five-person exits, seed 731,
220/500/1000 people, open/eight-table furnished rooms (80 seated included in total),
and both hazards/Hybrid modes. Independent layout RNG makes each mode's authored
starts identical. Twelve ticks warm engine, diagnostic and geometry paths on a
separate model; a fresh model then measures 60 ticks at 0.15 s (nine model seconds).
Setup, spawning, field precompute, file I/O and warm-up are excluded from samples.
Each case is one short initial-event run, not a steady-state or statistical study.

| Hazard | Layout | People | Detailed p95 ms | Hybrid p95 ms |
| --- | --- | ---: | ---: | ---: |
| Explosion | open | 220 | 8.01 | 5.95 |
| Explosion | open | 500 | 26.04 | 8.34 |
| Explosion | open | 1000 | 14.14 | 12.97 |
| Explosion | furnished | 220 | 7.72 | 6.26 |
| Explosion | furnished | 500 | 15.59 | 13.13 |
| Explosion | furnished | 1000 | 32.46 | 30.89 |
| ToxicFume | open | 220 | 0.91 | 0.64 |
| ToxicFume | open | 500 | 4.72 | 4.35 |
| ToxicFume | open | 1000 | 12.60 | 17.55 |
| ToxicFume | furnished | 220 | 1.65 | 2.23 |
| ToxicFume | furnished | 500 | 13.05 | 11.98 |
| ToxicFume | furnished | 1000 | 30.23 | 30.67 |

Full [CSV](M9_BENCHMARK.csv) also reports median/p99/max step CPU, mean main-thread
allocated bytes per step, pure snapshot/geometry CPU/allocations separately, exits,
casualties, mean open/grouped counts and shared-field builds. Those pure-view timings
include PresentationFrame, DiagnosticFrame and low-detail HumanGeometry; they do
**not** measure Unity transforms, meshes, UI Toolkit, native input, GPU work or FPS.
Small samples and shared cloud scheduling can affect timings. No FPS, GPU capacity,
realism/calibration, or universal Hybrid speedup is established. Hybrid remains opt-in;
benefits vary with eligible open-space movement and detailed fallback. Model outcomes
may differ between approximation modes; same-mode reference parity remains tested.

Reproduce from repository root (output outside source):

```bash
dotnet restore Migration/Benchmarks/CrowdSim.Scale.csproj --locked-mode
dotnet run --project Migration/Benchmarks/CrowdSim.Scale.csproj -c Release -- /tmp/crowdsim-scale 60
```

Use 400 measured ticks for a longer comparison if desired. Keep machine/runtime,
command, seeds, scene, warm-up, populations and mode with any report. In Unity,
Debug → Reproducible crowd comparisons creates the same authored fixtures; measure
observed frame rate and CPU-command/managed-allocation counters locally at consistent
zoom/playback. A true GPU profiler/player check remains local acceptance work.
