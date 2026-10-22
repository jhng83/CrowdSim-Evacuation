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
| M10 fix — CPU performance + GPU-instanced crowd | `dbe40329d1f47634fb1d086742aa60123720d907` | [Current validation](VALIDATION.md), [CPU comparison](M10_PERFORMANCE_BENCHMARK.md) | Local import blocked; performance testing pending |
| M10 fix — Recording namespace/import recovery | This fix commit, based on `dbe4032` | [Current validation](VALIDATION.md), [import recovery](M10_IMPORT_RECOVERY.md) | Awaiting clean local import and retest |
| M11 — Complete replacement | Planned | Unrun | Unrun |

Latest local report identified a real recording compiler error, CS0234 at
WorkspaceController.Recording.cs:150: `Unity.Collections` bound to `CrowdSim.Unity`.
The copy call now uses `global::Unity.Collections`. A reduced C#9/Standard2.1 compiler
probe reproduces the original error and compiles the corrected production statement;
the static contract guards this namespace qualification. The same report includes
an unauthenticated Input System package download, missing Collections/Unsafe files
and package importer errors. Those require local package recovery; this source fix
does not claim to resolve authentication or establish a successful Editor import.
Follow [M10_IMPORT_RECOVERY.md](M10_IMPORT_RECOVERY.md) before performance testing.

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

Cloud passes 404 original, 415 portable and 232 migration cases, unchanged six-case
captures/oracle, five legacy schema interop cases, v5 round trip/native rejection
and independent AVI decode. Three paired baseline/candidate CPU trials produce
24 exact matched case hashes (5,760 measured ticks across both versions). Dense
.NET mean-step comparisons improve 1.6–1.9×; this does not measure Unity Mono/FPS/GPU.
Static validation covers 187 GUIDs, shader/package/source wiring and 106 unchanged
original files. Package is 0.10.1; Editor/URP/other dependencies remain pinned.

No Unity Editor is installed: frontend/shader compilation, 52 EditMode/22 PlayMode
cases, actual instancing/Frame Debugger/FPS/GPU/Mono/visual/input/recording checks and
standalone builds were not executed. Main, original source/tests/scenarios/pins and
M1 goldens remain unchanged. The portable performance changes are explicitly scoped
in COMPATIBILITY.md. Remote GitHub Actions execution is unverified.

Next: [M10_PERFORMANCE_LOCAL_TESTING.md](M10_PERFORMANCE_LOCAL_TESTING.md). Stop for
local results; fix remaining M10/performance issues before M11. M10 stage schema v5,
recording limits and original/native compatibility remain as documented. Supported
player-target confirmation and standalone acceptance remain M11 work.
