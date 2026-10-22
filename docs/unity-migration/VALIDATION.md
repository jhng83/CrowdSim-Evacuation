# M10 performance update 2 — current validation

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
Base: `38ab7420478fe7f9b47a478a294eb77a625187c5`. The user reports the import bug
fixed and lag much better, but requests further M10 optimization. This iteration
keeps the same M10 features and physics; M11 is not implemented.

| Check | Actual result |
| --- | --- |
| Full cloud validation | `bash Migration/Tools/validate.sh` passed; Release builds and C#9/Standard2.1 portable production build passed |
| Original / portable / migration cases | **404 / 415 / 239 passed**, zero failures/skips; seven new checks in FramePerformanceTests |
| Wall sight behavior | 100,000 seeded random/boundary/degenerate rays match the original strict-edge predicate; rotated path retained |
| Circle force behavior | Exact original vector/contact/clamp formula matches scalar loop for three decay settings, coincident centers, contact/friction and far circles |
| Segment allocation | 1,000 warmed physical queries allocate zero managed bytes with or without walls; circles still block motion and furniture stays sight-transparent |
| View equality | Every geometry/state input invalidates exact reuse; small physical movements are not hidden by a tolerance |
| Original/portable captures | Both six-case captures and compatibility oracle match unchanged goldens |
| Persistence / recording | Five v1–v4 native interop cases, custom-stage v5 round trip/native rejection and independent FFmpeg AVI decode pass |
| Repeated CPU/state comparison | 3 serial paired trials × 8 cases × 120 ticks: **24 hashes match**, 5,760 measured ticks across versions |
| Extended late-phase verification | 1 paired trial × 8 cases × 240 ticks: **8 further hashes match**, 3,840 further ticks; covers screenshot ticks 110/190 |
| CPU results against 38ab742 | First 120 ticks: open mean-step ratios **1.28–1.34×**, dense **0.99–1.18×** (one within timing noise); allocations reduced; [report](M10_PERFORMANCE_2_BENCHMARK.md) includes full results/limits |
| Static contract | **190 unique GUIDs**, Editor-only helper assembly, package/shader/font/project wiring and **106 unchanged original files** pass |
| Editor menu API | `CompilationPipeline.codeOptimization` and Debug/Release enums verified against exact 6000.3.26f1 reference source; explicit menu only, no auto-load mutation |
| Unity frontend/shader/Editor-menu compilation | **Not executed — no Editor installed** |
| Unity tests | **52 EditMode / 24 PlayMode supplied, not executed**; two new PlayMode cases for view invalidation and cached material shades |
| Actual Unity CPU/Mono/GPU/FPS, optimization toggle, rendering/input/recording/player builds | **Not executed** |
| GitHub Actions | **Not verified** |

The CPU benchmark excludes Unity transforms/render submission, native GPU/paint,
world-view/material/LOD caching and the optional Editor Release compilation setting.
Actual effects of those frontend changes require [local tests](M10_PERFORMANCE_2_LOCAL_TESTING.md).
The first performance fix's 1.6–1.9× dense ratio below compared against original
M10, not against this iteration's already improved baseline. Do not combine those
ratios into a prediction of local FPS. Main, original source/tests/scenarios/pins,
all original goldens, dt/RNG/order, collision/exposure/navigation and features remain.

Reproduce the second comparison using the existing script's optional fourth argument:

```bash
bash Migration/Tools/validate.sh
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-perf2 120 3 38ab7420478fe7f9b47a478a294eb77a625187c5
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-perf2-long 240 1 38ab7420478fe7f9b47a478a294eb77a625187c5
```

Without the fourth argument, the script retains the original M10+timers comparison.
No benchmark output is written to original source or reference goldens.

# M10 recording compiler correction at 38ab742

Based on performance update `dbe40329d1f47634fb1d086742aa60123720d907`.
The user's local log identifies CS0234 at recording line 150 and separate failed
package authentication/missing package files. The copy call now explicitly names
`global::Unity.Collections`; no engine, GPU renderer, package pin or asmdef changes.

A reduced compiler probe extracted the actual original/corrected copy statements
into the same `CrowdSim.Unity.Workspace` namespace with minimal API shims, using
SDK 8.0.415 Roslyn, C#9 and Standard2.1 reference assemblies. Original statement
reproduced **CS0234: Collections does not exist in CrowdSim.Unity**; corrected
statement compiled with exit 0. This is namespace-binding evidence, **not** Unity
frontend/API compilation. Static validation now rejects the unqualified namespace
in the recording source. See [local recovery](M10_IMPORT_RECOVERY.md).

Full `bash Migration/Tools/validate.sh` rerun for this correction passed: **404
original, 415 portable, 232 migration** cases with zero failures/skips; Release
builds, both six-trace captures/oracle, five legacy file interop cases, custom-stage
v5 round trip/native rejection and independent FFmpeg AVI decode passed. Static
contract passed with 187 unique GUIDs and 106 unchanged original files. The earlier
CPU benchmark was not repeated because simulation sources are unchanged; its evidence
below belongs to `dbe4032`. Actual Unity import/compilation, package authentication,
shader/GPU execution, 52 EditMode/22 PlayMode cases and recording remain unexecuted
in cloud. Main and the original behavioral reference remain unchanged.

# M10 performance-fix validation at dbe4032

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
Base: M10 `6343f9d52ceb5a7e980e97bd1c7e04310ce37808`.
[M10 feature evidence](M10_VALIDATION.md). User authorized CPU performance work and
GPU rendering after reporting ~300 ms physics/~3.5 FPS in a furnished Editor scene.
This is an M10 fix iteration; M11 is not implemented.

| Check | Actual result |
| --- | --- |
| Original Release solution | Passed; original files/dependencies unchanged |
| Original regressions | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: 404 unchanged original + 11 presentation |
| Migration suite | **232 passed, 0 failed, 0 skipped**: 217 previous + 15 performance-contract cases |
| Portable production build | C#9/netstandard2.1 passed; no UnityEngine dependency |
| Reference/portable seeded captures | Both match all six original goldens and compatibility oracle exactly |
| File interop | Five v1–v4 cases pass; custom-stage v5 Unity round trip and original native rejection pass |
| Recording regression | Production AVI worker/muxer known frames + independent ffprobe/ffmpeg decode pass |
| Reproducible CPU comparison | Actual script extracted M10 + read-only clocks and ran 3 paired trials × 8 cases × 120 measured ticks per version |
| CPU state preservation | **24 paired hashes match exactly**, observing every measured tick's full ordered population/furniture/debris/exit data; original goldens untouched |
| CPU timing | Dense 800-person mean-step comparisons **1.62–1.91× faster**; [method/results](M10_PERFORMANCE_BENCHMARK.md), [CSV](M10_PERFORMANCE_BENCHMARK.csv), [environment](M10_PERFORMANCE_ENVIRONMENT.json) |
| GPU contract | Instancing API/RenderParams signature checked against exact Editor source; bounded matrix-block split/conservation, shader/variant inclusion and source wiring verified |
| Dense authoring/lifecycle | 800 people/320 seated/32 tables, valid save/load/finalise/replay for both hazards/modes; filtered sight cache lifetime and finite/detached timing contracts pass |
| Static project contract | Passed: **187 unique GUIDs**, black text/contrast/transparent viewport, package graph/shaders/resources and **106 unchanged original files** |
| Unity frontend/shader import/compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode / PlayMode | **52 / 22 supplied, not executed**; includes new instancing shader and GPU picking/fallback/POV/cleanup cases |
| Actual GPU instancing, Frame Debugger, shader colour/lighting, render/Mono/FPS | **Not executed** |
| Cameras/selection/hidden inspector/native input; actual stage/video capture | **Not executed** for this fix |
| Standalone Mono/IL2CPP build/record/quit | **Not executed** |
| GitHub Actions | **Not verified** |

## Preservation and measurement scope

CPU benchmarks execute the exact production portable sources under .NET 8, not the
Unity Mono runtime from the user's screenshot. They do not measure Unity transforms,
UI Toolkit paint, render thread/GPU work or application FPS. The 1.6–1.9× dense CPU
ratio is a cloud observation, not a prediction that 300 ms becomes a particular
value locally. Shared-worker timing noise and different layout/event/runtime affect
results. The published script and summaries preserve each trial's timings/hashes.

Per-step sight snapshots contain architecture only, retaining the pre-existing rule
that low furniture is transparent to perception. Physical collision/path queries
still include furniture. Snapshots are cleared/rebuilt every synchronous tick and
disabled in finally; calls outside a tick read live geometry. Contact/steering AABB
rejection includes rounding padding and retains original exact tests and iteration
order. Far SFM forces are still included with the same clamp floor/accumulation;
only repeated exponentials and provably unnecessary small-force square roots vanish.
Integration dt, RNG consumption/order, contact guards, navigation and exposure remain.

GPU mode submits real box volumes and black silhouette hulls, grouped by material
and mesh using 256-entry matrix arrays. It also instances selection/panic annuli.
Per-person lightweight roots and CPU pose/interpolation/matrix preparation remain;
there is no per-part renderer/transform tree or ring LineRenderer in GPU mode.
CPU frustum/POV hiding and matrix-based volume picking retain the visual behavior.
Compatibility mode reuses the prior renderer when instancing is unavailable or
explicitly disabled. Variant retention and shader source checks do not establish
actual compiled output or GPU execution. Draw counters count submitted batches,
not profiler-derived GPU milliseconds. Simulation is still CPU C#, without Jobs/
Burst/compute acceleration or a claim of guaranteed 60 FPS.

New Unity cases are source-only until executed. They require actual instancing on
supported graphics hardware and verify nonempty submissions, absence of per-part
renderers, pose picking, POV hiding, fallback toggling and empty-frame cleanup.
The original Unity cases remain intact. M10 recording/stage limitations and all
prior functionality are retained; visual/capture acceptance remains local.

## Reproduce

```bash
bash Migration/Tools/validate.sh
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-comparison 120 3
```

Use appropriate DOTNET_ROOT/DOTNET_CLI_HOME/NUGET_PACKAGES for a private SDK. First
command builds/tests/static-checks/traces/interop/AVI decode. Timed CPU comparisons
are separate; run without concurrent test/benchmark processes. Follow the numbered
[M10 performance local checklist](M10_PERFORMANCE_LOCAL_TESTING.md) before advancing.

Runtime shader preservation also sets GraphicsSettings **Instancing Variants =
Keep All** (`m_InstancingStripping: 2`), verified against the exact Editor source
enum. This prevents variants used only by scripted submissions from being stripped;
actual player shader compilation remains unexecuted and part of local/M11 checks.
