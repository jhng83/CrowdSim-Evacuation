# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24` (main unchanged).

| Milestone | Implementation | Cloud evidence | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Foundation | `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1](M1_VALIDATION.md) | Accepted by user |
| M2 — Portable engine | `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2](M2_VALIDATION.md) | Accepted by user |
| M3 — Builder/workspace | `3e55f9c992ac8260cce714970b71631a1b3b601a` | [M3](M3_VALIDATION.md) | Accepted by user |
| M4 — Furniture | `bbc7a3ad64817ccd46d540c695a96831346af817` | [M4](M4_VALIDATION.md) | Accepted by user |
| M5 — Files/replay | `a0fdb72f067bb2116861dcaa78e580da0c904b98` | [M5](M5_VALIDATION.md) | Accepted by user |
| M6 — Explosion | `03137845c858c6d2fd9f85a1bfb73152f724a06b` | [M6](M6_VALIDATION.md) | Accepted by user |
| M7 — Toxic Fume + design | `4fc21756b1d2b97e28a3d5b5236b9276df4da47f` | [M7](M7_VALIDATION.md) | Accepted by user |
| M8 — Humanoids/cameras | `55652fc407d3eb96527f836e57067da42dc41459` | [M8](M8_VALIDATION.md) | User reported successful testing; accepted |
| M9 — Diagnostics/scale + requested light architecture theme | `86ae9d16d8a7f928b6d004d84fd8b639bc7f5ad2` | [M9](M9_VALIDATION.md), [CPU comparisons](M9_BENCHMARK.md) | Accepted by user |
| M10 — Recording + requested adjustable concert stage | `6343f9d52ceb5a7e980e97bd1c7e04310ce37808` | [M10](M10_VALIDATION.md) | Awaiting retest with performance fix |
| M10 fix — CPU performance + GPU-instanced crowd | `dbe40329d1f47634fb1d086742aa60123720d907` | [Current validation](VALIDATION.md), [CPU comparison](M10_PERFORMANCE_BENCHMARK.md) | User reports lag much better after namespace correction; further improvement requested |
| M10 fix — Recording namespace/import recovery | `38ab7420478fe7f9b47a478a294eb77a625187c5` | [Current validation](VALIDATION.md), [import recovery](M10_IMPORT_RECOVERY.md) | User reports bug fixed; M10 testing continues |
| M10 performance update 2 — repeated CPU work and retained views | `b127185bfd1e8e9b620dd6d2d341a0e85c9a708f` | [Validation](VALIDATION.md), [new CPU comparison](M10_PERFORMANCE_2_BENCHMARK.md) | Native crash reported after optimization menu switch |
| M10 menu correction / reload recovery | This iteration, based on `b127185` | [Validation](VALIDATION.md), [recovery](M10_EDITOR_RELOAD_RECOVERY.md) | Awaiting clean short-path restart and M10 testing |
| M11 — Complete replacement | Planned | Unrun | Unrun |

The uploaded Editor.log shows compilation success followed by a native Mono
`unload_thread_main` crash during assembly reload after a menu optimization switch.
The recorded menu call is `UseDebug`, despite the requested Release action; the
correction removes **both** mode-changing commands. The remaining command/status
readout only reads the setting. No automatic restore or scripted reload is added.
The same log has 149 distinct missing package paths, all 260–320 characters long,
strongly suggesting a Windows path-length import problem. A fresh short-path
extraction is required for the local retest. The exact underlying native fault is
not proven; the correction removes its known trigger and preserves all CPU/GPU/
view improvements. See M10_EDITOR_RELOAD_RECOVERY.md. M11 remains pending.

The earlier M10 screenshots show the GPU checkbox enabled, no recording, Hybrid off,
and ongoing engine CPU stalls: dense 800/32 tables at tick 190 reports 123.87 ms
engine, 16.72 ms response, 99.35 ms movement (38.10 ms SFM/30.96 ms move-collision)
and 5.40 ms world commands. Open 1000 at tick 110 reports 65.69 ms engine and
35.10 ms response. These are user observations, not cloud Unity measurements.

This follow-up retains M10 and GPU instancing, rejects only distant wall bounds
before unchanged strict edge tests, removes a navigation-query closure allocation,
and spells out identical non-contact circle-force arithmetic to reduce Mono vector
helper overhead. Every far clamp-floor force and ordered accumulation remains.
The Unity view retains exact unchanged furniture/floor/stage/pillar descriptors,
reuses draw scratch collections and per-part materials, and shares orthographic
LOD projections except near the existing threshold. Perspective/selected detail,
all poses, guards, contacts, dt/RNG/order, recording and stage behavior remain.
Startup identifies **PERFORMANCE UPDATE 2 · MENU CORRECTION**, portable package **0.10.2**. Follow
[the new local checklist](M10_PERFORMANCE_2_LOCAL_TESTING.md); M11 remains pending.
The Performance menu now has only **Show Editor Optimization Status**. Mode-switching
instructions are withdrawn; Debug reports the current mode without changing it.

An earlier local report identified a real recording compiler error, CS0234 at
WorkspaceController.Recording.cs:150: `Unity.Collections` bound to `CrowdSim.Unity`.
The copy call now uses `global::Unity.Collections`. A reduced C#9/Standard2.1 compiler
probe reproduces the original error and compiles the corrected production statement;
the static contract guards this namespace qualification. The same report includes
an unauthenticated Input System package download, missing Collections/Unsafe files
and package importer errors. The user now reports the bug fixed. Keep
[M10_IMPORT_RECOVERY.md](M10_IMPORT_RECOVERY.md) for future import troubleshooting;
cloud still has no Editor or access to the user's package cache.

The user reported ~3.5 FPS/~300 ms simulation CPU with about 800 people in a furnished
Unity Editor scene and explicitly requested performance work using M10 as the base,
including GPU rendering. Their screenshot's `recording: M10 pending` was from M9;
this iteration retains all of M10 and addresses the reported class of slowdown.

The fix adds default GPU-instanced batches for volumetric body parts, outlines and
rings, reuses bounded matrix blocks (≤256 instances/submission), preserves geometric
picking/poses/cameras/labels, retains shaders for player builds and supplies a Debug
compatibility toggle/actual backend/submission counters. Lightweight roots remain;
GPU mode has no per-part MeshRenderers or ring LineRenderers. Actual GPU execution
is unverified in cloud. There is no Jobs/Burst or compute-shader simulation change.

Exact CPU optimizations filter already-transparent furniture from per-tick sight
queries, hoist repeated clamped circle-force exponentials, bypass unnecessary small-
force square roots and reject distant circular contact/steering candidates before
the original exact tests. No force/guard/physical rule/agent ordering is removed.
New phase timers and a valid 800-person/32-table fixture support local diagnosis.

Current cloud passes 404 original, 415 portable and 239 migration cases, unchanged six-case
captures/oracle, five legacy schema interop cases, v5 round trip/native rejection
and independent AVI decode. The first update's paired CPU comparison against
original M10 produced 24 exact matched case hashes and 1.6–1.9× dense .NET gains.
The second update compares against the already improved `38ab742`: another 24 exact
matched pairs (5,760 measured ticks), open CPU ratios 1.28–1.34× and dense ratios
0.99–1.18×, including a case within timing noise. These do not measure Unity Mono/FPS/GPU.
Static validation covers 190 GUIDs, shader/package/source wiring and 106 unchanged
original files. Package is 0.10.2; Editor/URP/other dependencies remain pinned.

No Unity Editor is installed: frontend/shader compilation, 52 EditMode/24 PlayMode
cases, actual instancing/Frame Debugger/FPS/GPU/Mono/visual/input/recording checks and
standalone builds were not executed. Main, original source/tests/scenarios/pins and
M1 goldens remain unchanged. The portable performance changes are explicitly scoped
in COMPATIBILITY.md. Remote GitHub Actions execution is unverified.

Next: [M10_PERFORMANCE_2_LOCAL_TESTING.md](M10_PERFORMANCE_2_LOCAL_TESTING.md). Stop for
local results; fix remaining M10/performance issues before M11. M10 stage schema v5,
recording limits and original/native compatibility remain as documented. Supported
player-target confirmation and standalone acceptance remain M11 work.
