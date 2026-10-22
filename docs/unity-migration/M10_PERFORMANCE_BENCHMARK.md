# M10 performance-fix CPU comparison

Executed 2026-10-22 (Asia/Singapore), .NET 8.0.21/SDK 8.0.415 on the Linux cloud
worker. **No Unity Editor, Mono, GPU rendering or application FPS was measured.**
Baseline is M10 `6343f9d52ceb5a7e980e97bd1c7e04310ce37808` plus diagnostic phase clocks;
its simulation algorithms are unchanged. Candidate is this performance fix.

The actual reproducibility script extracted M10 into a throwaway directory, applied
only read-only timers, built it and used the same current harness and valid authored
dense fixtures for both assemblies. Fixtures are serialized and loaded through the
real scenario codec before measurement. The baseline also includes the fixture
factory as a diagnostic helper for type resolution, but its engine algorithms remain
M10. No original checkout, reference source or golden baseline was edited.

Three serial baseline/candidate trial pairs each cover both hazards, Hybrid off/on,
an open 1,000-person room and a dense 800-person room with 32 tables/320 seated people.
Twelve ticks on a separate model warm each case. A fresh instance then measures 120
ticks (18 model seconds), triggering the selected hazard at tick zero. Setup, spawn,
fume precompute, JSON hashing, graphics/UI and I/O are excluded from step timings.
Tiered compilation is disabled equally for both versions to reduce startup/JIT skew.
These remain wall-clock measurements on a shared worker, with scheduling noise.

| Hazard | Layout | Hybrid | M10 ms | Optimized ms | Ratio |
| --- | --- | --- | ---: | ---: | ---: |
| Explosion | Dense 800 | Off | 32.06 | 19.28 | 1.66× |
| Explosion | Dense 800 | On | 30.75 | 19.00 | 1.62× |
| Toxic Fume | Dense 800 | Off | 32.21 | 16.90 | 1.91× |
| Toxic Fume | Dense 800 | On | 33.22 | 17.36 | 1.91× |
| Explosion | Open 1,000 | Off | 12.00 | 10.77 | 1.11× |
| Explosion | Open 1,000 | On | 11.71 | 10.35 | 1.13× |
| Toxic Fume | Open 1,000 | Off | 11.24 | 10.10 | 1.11× |
| Toxic Fume | Open 1,000 | On | 11.59 | 10.80 | 1.07× |

Times are **median of three trial mean step times**, not frame times or a real-time
capacity claim. [CSV](M10_PERFORMANCE_BENCHMARK.csv) retains each trial's mean/p95 and
response/SFM subset times. [Environment/provenance](M10_PERFORMANCE_ENVIRONMENT.json)
records runtime, fixture/assembly hashes and exact ordered observed-state hashes.

All **24 paired case hashes match exactly**: every measured tick observes the full
ordered population's position/velocity/state/posture/panic/intent/injury/exposure/
stand-up/route/exit/navigation/bottleneck values, furniture/chair motion/occupancy,
debris and exit metrics. Hashes are outside timed sections. This supplements the
unchanged six original traces; it is not a serialization of all hidden RNG/cache
state. No tolerances or goldens were changed. The representative fixture is not an
exact reconstruction of the user's screenshot or hardware/runtime.

Most dense savings come from excluding already-transparent furniture from repeated
sight tests and avoiding repeated clamp-floor exponentials/small-force square roots.
Conservative circular-contact/steering rejection retains the original exact tests
and order. No distant SFM force, collision, navigation or exposure is omitted.
Physics remains CPU C#; Hybrid remains opt-in and unchanged.

GPU instancing is a separate frontend change. Its bounded matrix batches, material
keys, shader inclusion and source/API wiring are inspected in cloud, but no GPU
speedup is measured or promised. In Unity, Debug shows the actual chosen backend,
instance and submission counts; use Frame Debugger/Profiler and the local checklist
to verify real draws/FPS. CPU instance preparation and GPU draw time are distinct.

Reproduce on a cloud/Linux checkout with the .NET 8 SDK:

```bash
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-comparison 120 3
```

The script builds the current adapter, reconstructs the exact M10 instrumented
baseline without a worktree, runs paired trials and requires matching hashes before
writing summary CSV/JSON. Raw per-tick phase CSVs and fixture JSONs stay in the chosen
output directory. Do not run concurrent benchmarks/tests for a comparison.

Runtime shader preservation also sets GraphicsSettings **Instancing Variants =
Keep All** (`m_InstancingStripping: 2`), verified against the exact Editor source
enum. This prevents variants used only by scripted submissions from being stripped;
actual player shader compilation remains unexecuted and part of local/M11 checks.
