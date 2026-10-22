# M10 performance-fix local testing

This is a fix iteration based on M10 `6343f9d`, **not M11**. Use **Unity 6000.3.26f1**,
URP **17.3.0**, a fresh extraction and the committed packages. Your screenshot's
`recording: M10 pending` identifies M9; this download includes M10 recording and
the adjustable concert stage as well as this performance fix.

1. **Open the correct folder.** Add the extracted **UnityProject/** folder to Hub;
   it contains **Assets/**, **Packages/** and **ProjectSettings/**. Open
   **Assets/CrowdSim/Scenes/Bootstrap.unity**, then Play. **Expect:** startup banner
   includes **PERFORMANCE UPDATE**, all previous entries and M10 Recording tab.
   Black fonts, off-white gridded floor/pillars and concert stage remain available.

2. **Run supplied tests.** Stop Play; Window → General → Test Runner. Run EditMode
   and interactive PlayMode with Game view visible for recording. **Expect:**
   **52 EditMode / 22 PlayMode** supplied cases. InstancedCrowdContractTests checks
   bundled shaders/variant retention; InstancedCrowdInteractionTests exercises GPU
   batching, standing/seated/crawling/prone picking, POV hiding, fallback and cleanup.
   GPU case is ignored only on hardware without instancing; an ignored case is not
   GPU acceptance. Save actual XML/Console logs. All Unity tests were unrun in cloud.

3. **Check GPU submission.** Open your saved furnished scenario, finalise and pause
   at frame zero. Open Debug. **Expect:** **GPU instanced crowd rendering** enabled,
   backend **GPU instancing**, positive GPU instance/submission counts and visible
   volumetric people, outlines and black selection/orange panic rings. Expand a
   runtime Human in Hierarchy: GPU mode has no individual body-part MeshRenderers
   or ring LineRenderers. A lightweight root remains per person. Unsupported devices
   automatically use compatibility renderers; shaders must not appear pink/missing.

4. **Verify actual GPU work.** Window → Analysis → Frame Debugger, enable on a paused
   scene; inspect crowd draw events/instance counts. **Expect:** instanced body and
   outline batches, each at most **256** instances, plus indicator batches. Shader
   names are **CrowdSim/CrowdInstanced** and **CrowdSim/HumanOutline**. Stats count
   submissions from our code, not measured driver draw calls or GPU milliseconds.
   Disable Frame Debugger before comparing FPS; use GPU Profiler where supported.
   Report any Console errors, including the URP VirtualOffsetRTShader import error
   shown in the earlier screenshot; this fix does not claim that error was resolved.

5. **Compare render backends.** Pause, switch the GPU checkbox off, allow one frame,
   inspect poses/colours/selection, then switch it on. **Expect:** same crowd and
   physical positions; compatibility uses per-part renderers while GPU uses volume
   batches. Backend switching may reset visual interpolation briefly and does not
   change the simulation tick/state. Record World CPU, FPS, frame p50/p95 and native
   Profiler draw/render CPU for each mode with identical camera/zoom/window settings.

6. **Retest your reported scene.** Use the same saved layout, seed, crowd and 1×
   playback as before. Recording off, overlays off, Deep Profile off; avoid comparing
   a debugger-heavy Editor run against a native Release run. Replay before each run
   and trigger Explosion at the same position/time. **Expect:** physics behavior
   retained; Debug now shows exclusive grid/hazard/response/furniture/preparation/
   movement/contact/exit times. SFM/move-collision/route are nested subsets and must
   not be added to exclusive phase totals. Compare measured tick CPU to your prior
   ~300 ms. Actual Unity/Mono improvement and FPS were not measured in cloud; report
   the observed values even if this scene remains CPU-bound. No 60 FPS promise.

7. **Use the reproducible dense fixture.** Debug → Reproducible crowd comparisons →
   **New dense 800-person layout (32 tables)**; accept save/discard if needed.
   **Expect:** 800 people including 320 seated, 32 tables and four exits. Save/load
   and finalise successfully; choose Hybrid off initially. Start, detonate on clear
   floor and compare with M10/your original scene separately. Also test existing
   open/furnished 220/500/1000 layouts. Enabling Hybrid remains an optional model
   approximation, not a prerequisite for these optimizations.

8. **Exercise both hazards and all views.** Repeat with Toxic Fume, Compute,
   Finalise/Start/Release. Test 0.45×/1×/2.5× zoom, seated/rising/walking/running/
   crawling/prone/helping poses, state colours, panic/alarm/help labels, selection,
   Follow, POV and exit cleanup. F4 hide/show retains selection/tab/flags; overlays
   continue while hidden. **Expect:** visible 3D bodies at every zoom, accurate
   picking and no ghosts after replay/new/scene change. No flat crowd fallback.

9. **Retest M10 stage and recording.** Resize/place/save/load/replay the concert
   stage and exercise dimension/overlap guards. Record both hazards while changing
   inspector/camera/window size, then stop/play the AVI. **Expect:** all instanced
   people and application UI included, correct orientation, 1280×720/20 fps MJPEG,
   bounded queue/drops and finalized replay/quit outputs. See the full
   [M10 checklist](M10_LOCAL_TESTING.md) for limits/schema v5 compatibility.

10. **Report results before advancing.** Include delivered commit, CPU/GPU model,
    OS, Editor version, crowd/layout/hazard/Hybrid/zoom, backend, recording state,
    FPS/frame p95, total physics, response/SFM/collision/world timings, instanced
    batch counts, Console errors and test XML. Compare equal model ticks/events,
    not just equal wall time, if one run cannot maintain real time.

Cloud passes original/portable/migration regressions, unchanged traces and 24 exact
paired CPU comparison hashes. Dense .NET CPU mean-step comparisons improved roughly
1.6–1.9× on the cloud worker. **Unity frontend/shader compilation, rendering, actual
GPU/FPS/Mono performance, these tests and standalone builds were not executed.**
Simulation stays on the CPU; this update adds GPU-instanced drawing and exact CPU
optimizations, without Jobs/Burst or compute-shader simulation. M11 remains pending.

Instancing targets the runtime crowd. Builder planning markers and authored
furniture/stage use the existing world renderer; those objects are not converted
to the new crowd batches in this fix. All still render through Unity's graphics
pipeline. Local profiling should include their cost in heavily furnished scenes.
