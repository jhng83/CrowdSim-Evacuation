# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Original reference/main: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24` (unchanged).

| Milestone | Implementation | Local acceptance |
| --- | --- | --- |
| M1 — Foundation | `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | Accepted by user |
| M2 — Portable engine | `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | Accepted by user |
| M3 — Builder/workspace | `3e55f9c992ac8260cce714970b71631a1b3b601a` | Accepted by user |
| M4 — Furniture | `bbc7a3ad64817ccd46d540c695a96831346af817` | Accepted by user |
| M5 — Files/replay | `a0fdb72f067bb2116861dcaa78e580da0c904b98` | Accepted by user |
| M6 — Explosion | `03137845c858c6d2fd9f85a1bfb73152f724a06b` | Accepted by user |
| M7 — Toxic Fume + design | `4fc21756b1d2b97e28a3d5b5236b9276df4da47f` | Accepted by user |
| M8 — Humanoids/cameras | `55652fc407d3eb96527f836e57067da42dc41459` | Accepted by user |
| M9 — Diagnostics/scale/theme | `86ae9d16d8a7f928b6d004d84fd8b639bc7f5ad2` | Accepted by user |
| M10 — Recording/stage, CPU/GPU improvements and menu correction | Final accepted revision `c97d49c84ecf9c79d08239fa83e4703eeb7353bc` | User reported “M10 tested successfully”; accepted |
| M11 — Standalone application and distribution | Initial delivery `e76d726`; CPU fix `eeb484b`; this correction adds package preflight/recovery | User reports latency solved; build blocked by package authentication/Burst dependency; player acceptance pending |

M11 targets the approved roadmap's **Windows x64 / Mono** player, consistent with
the user's Windows testing. Unity stays **6000.3.26f1**, URP **17.3.0**; portable
production stays **C#9 / .NET Standard 2.1**, package/application version **0.11.1**.
Company/product identity stays CrowdSim / CrowdSim Unity to retain M10 user data.
No Editor optimization changes, automatic target switches or new dependencies.

Latest local evidence: the user reports **“the latency issue is solved”** for
`eeb484b`. Player build failed with unauthenticated Input System/package search,
missing Burst Unsafe dependency and URP host-type errors. See
[build recovery](M11_BUILD_RECOVERY.md); source diagnostics do not repair credentials
or establish the unproven causes of all import/compiler errors.

Delivered M11 capabilities:

- Read-only Check Package Readiness menu and automatic preflight before output/
  BuildPipeline: registered lock versions/errors and matching readable package.json
  files. Actionable failures retain real-build/no-ZIP gates. This does not verify
  every DLL, import, account session or compiler. No cache/package/settings mutation.

- CPU query correction: ordered conservative circle candidates for collision,
  contact, chair departure/pushing and local navigation. Positional corrections
  re-query later candidates; moved/removed chairs invalidate the index immediately.
  Outside synchronous Step, geometry stays live. No SFM force cutoff, approximation,
  dt/RNG/order/default-Hybrid or guard change. Debug exposes version/mode first.
- Startup/workspace Quit and normal OS close use save/discard/cancel transactions;
  failed saving/overwrite cancellation keeps the app open, and approved exit drains
  recording before the owner exits. Editor Quit stops Play; forced termination or
  toolbar Stop cannot promise an authored-data prompt.
- Local user guide/credits/licenses at startup, Open save folder, and all authored,
  legacy, furnished/Fume and generated comparison examples retained.
- Explicit Editor/batch Windows player build validates pins/Mono/Standard/stripping,
  Bootstrap-first scene list and fresh output. It creates a real BuildPipeline
  player before copying actual package legal files, writing a receipt and validating
  x64 PE/runtime/docs/example contents. The ZIP has SHA256 file checksums and omits
  symbol/debug backups; existing builds/archives are not overwritten.
- A Git-based source ZIP exporter and GitHub artifact step provide a short-root
  UnityProject source download with commit/file hashes, excluding caches/builds.
  A source artifact is not a prebuilt Windows executable.

Paired CPU evidence: [48 matching ordered state hashes](M11_PERFORMANCE_FIX_BENCHMARK.md),
19,200 measured ticks across versions. Dense Toxic Fume uses ~14–38% less CPU in
these cloud windows; open results are mixed. Unity FPS remains unmeasured.

Cloud evidence: [VALIDATION.md](VALIDATION.md), **404 original / 415 portable / 280
migration** cases, unchanged six-case captures/oracle, file interop and AVI decode;
203 GUIDs and 106 unchanged original files. New package tests use synthetic binary
headers/layouts and do not establish player executability. Source ZIP content/hash
validation ran against the working candidate; committed export is checked at delivery.

**No Unity Editor is installed in cloud.** Actual frontend/Editor build-helper/shader
compilation, **55 EditMode / 26 PlayMode** cases, Windows build, real ZIP packaging,
standalone launch, native OS close and full player workflows are unexecuted here.
GitHub Actions execution/artifact publication cannot be verified through the current
GitHub API access (403). The build command and source download are supplied; no
prebuilt .exe or final complete-replacement acceptance is claimed.

Next: [build recovery](M11_BUILD_RECOVERY.md), then
[M11_LOCAL_TESTING.md](M11_LOCAL_TESTING.md). Test Editor, build the Windows
player and complete F01–F29 acceptance. Fix reported issues before marking M11 or
the replacement accepted. No further milestone or main merge is authorized by this
handoff. Earlier M10 performance/recording/import evidence remains in the M10 reports;
its native reload trigger was removed and its final revision accepted by the user.
