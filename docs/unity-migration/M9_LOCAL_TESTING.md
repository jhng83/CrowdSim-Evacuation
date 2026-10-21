# M9 local Unity acceptance

M1–M8 were reported successful by the user. This is **M9 only**, including the
requested architectural light theme. Use **Unity 6000.3.26f1**, URP **17.3.0**, the
committed packages/lock and a fresh extraction folder. No original .NET executable
or SDK is required locally; first import needs Unity package-registry access.

1. **Open the download.** Extract the delivered commit ZIP. Add its **UnityProject/**
   directory to Unity Hub (it contains **Assets/**, **Packages/**, **ProjectSettings/**).
   Open **Assets/CrowdSim/Scenes/Bootstrap.unity**, then Play.
   **Expect:** M9 banner, both scenario entries, black text on pale panels and no
   font/shader/script errors. The 3D simulation must remain visible through the
   viewport; the full-screen UI root is transparent.

2. **Execute supplied tests.** Stop Play; Window → General → Test Runner. Run
   EditMode then PlayMode. **Expect:** **49 EditMode / 19 PlayMode** cases pass,
   including DiagnosticContractTests/DiagnosticInteractionTests. These cases and
   actual Unity frontend compilation were **not executed in cloud**. Save XML/logs.

3. **Review black typography and light surfaces.** Check startup, Build/Run, Files,
   Camera and all inspector tabs at 1280×720 and 1600×900, including hover/focus/input/
   disabled states and dirty prompts. **Expect:** black Atkinson Hyperlegible labels,
   readable numbers, visible focus, scroll access and pale action/readiness accents.
   Hazard/agent legends use black text with coloured swatches. Visit the legacy
   simulation-validation panel and the M8 pose-reference scene too; their text is black.

4. **Author architectural pillars.** Create an Explosion scene. Place an exit and
   people, then use the asset picker for both square sizes and circular blockage.
   Rotate the view and check 0.45×/1×/2.5× zoom. **Expect:** off-white square/round
   pillars with black silhouettes and feature edges, correctly dimensioned relative
   to chairs/people. Floor is warm off-white with a subtle finish and default-on
   **1.2 m grid**. Debug → Show room grid can hide/show that grid. Preview validity,
   placement rejection and undo still work. Legacy blue internal walls stay distinct;
   the physical perimeter stays invisible, and people/hazards still respect it.

5. **Exercise both hazards.** Use the furnished Explosion example, Finalise/Start,
   arm and detonate on clear floor near (18,12). Separately use the 205-person Fume
   example, Compute dispersion, Finalise/Start and Release Toxic Fume.
   **Expect:** previous M6–M8 controls, humanoid poses, hazard overlays, furniture,
   exposure and camera behaviours remain available. Check pillar shading and black
   outlines without losing agent/state visibility on the new floor.

6. **Inspect Overview and exits.** During a run, open Overview and scroll.
   **Expect:** population/in-room/mobile, all casualties, assistance, panic, congestion,
   visibility, contagion/intent, seated/rising/furniture, geometry/seed/time/run state,
   scenario-specific exposure/debris and per-exit metrics. Exit density is the native
   **active-person count within 2 m**, not people per square metre; flow is the
   most recent tick's admitted count divided by dt, in persons/s.

7. **Inspect selected navigation/assistance.** Select normal, reacting, injured/low
   and helping people when available. Open Agent details and scroll.
   **Expect:** state/posture/pose/rise, intent/panic/position, desired/actual **m/s**,
   strategy/exit/visibility/congestion/stimuli/alarm, route/next waypoint/stuck time,
   adaptive recovery/no-progress/attempts/detours/exit changes, local navigation/
   blockage/bypass, helper target/side/phase/progress/timeout and casualty helper
   counts, traits/bottleneck pressure/wait, and hazard-specific data. Conditional
   rows appear only when applicable; approaching and timed attendance are distinct.

8. **Try all eight overlays.** Debug → Diagnostic overlays. Test Desired, Avoidance,
   Herd/lane, Total SFM, Blast (Explosion only), Vision, Congestion and Trails, first
   separately then together. Start/resume to produce useful vectors.
   **Expect:** green/amber/teal/magenta/orange arrows, blue cones, purple congestion
   disks and charcoal trails. Zero vectors show no arrow. Arrow lengths are display
   scaling/caps, not literal displacement. Cones use configured range/angle without
   occlusion clipping. Congestion disks grow with the model's local congestion;
   they are distinct from orange panic. Trails sample every 3 ticks, retaining 40.

9. **Hide, pause, follow and replay.** With overlays active hide the inspector via F4
   or its button. Select another person, Follow, enter/exit POV, pause and replay.
   **Expect:** overlay flags/tab/selection are retained on hiding, viewport expands,
   and selection alone never forces reveal. POV omits its own agent's diagnostic
   geometry. Pause freezes model/trail progression. Replay clears old trail history,
   restores frame-zero people/hazards/furniture, keeps overlay visibility choices and
   clears selected-camera state. New/load starts a new diagnostic history.

10. **Switch Hybrid safely.** On Debug while paused, toggle **Hybrid crowd mode
    (pause to change)**. Resume, then pause again.
    **Expect:** checkbox is locked while running; changing it never moves people or
    advances the tick. Individual SFM/navigation/contact rules remain authoritative.
    The latest flag is saved with the authored definition and retained on replay;
    replay restores original starts, not current damage/positions. A Hybrid change
    marks authored settings dirty. Approximation-mode outcomes may differ; compare
    identical replayed starts rather than expecting cross-mode bitwise identity.

11. **Run the scale matrix.** Debug → Reproducible crowd comparisons. Choose
    **220, 500, 1000** with Furnished off/on. New comparison layout uses the existing
    save/discard/keep-working prompt. Finalise (Compute first for Fume), Start and
    trigger Explosion at **(28,17)** or Release Toxic Fume. Repeat each layout with
    Hybrid off/on using replay and the paused checkbox, in **both hazards**.
    **Expect:** 24 combinations, four exits, seed 731, exact selected population;
    furnished counts include **80 seated**, not an additional 80 people. Frames and
    input remain functional; no flat humanoid fallback or physics shortcuts.

12. **Record actual performance.** Keep the same resolution, camera/zoom, inspector,
    overlays and **1×** playback for comparisons. Allow warm-up and record at least
    20–30 wall seconds per run. Debug reports observed FPS/frame interval, last ≤120
    frame p50/p95, physics-command CPU, native last-tick/furniture/route CPU, world/UI
    command CPU, main-thread managed bytes/frame, visible/active people and native
    Hybrid/group/field counters. **Expect:** units and scopes stay distinct. Paused
    engine timing is the last completed tick. UI Toolkit paint and GPU are excluded
    from CPU-command scopes; GPU says **not measured**, recorder says **M10 pending**.
    Shared-field/open/detailed counters retain native meanings; Hybrid off still
    navigates. Save your actual FPS/CPU/allocations and use Unity Profiler for GPU
    work if supported. No hardware FPS threshold or speedup guarantee is asserted.

13. **Save/replay/lifecycle regression.** After damage and a paused Hybrid change,
    save/load/replay; then create another comparison or return to startup.
    **Expect:** authored initial state and selected Hybrid setting persist without
    runtime injuries/moved starts. New/load/replay resets performance history; old
    overlay meshes/trails/people do not remain in the new world. Repeated runs should
    reach bounded view caches; destruction cleans resources when leaving a workspace.
    Retest numeric text focus, F4 while editing and Alt+F4 as in earlier milestones.

14. **Send acceptance or issues.** Include Editor/OS/hardware, scene/layout/count,
    Hybrid mode, zoom/overlay flags, exact steps, Console logs/test XML and screenshots
    for visual faults. Include actual FPS/CPU/managed bytes separately for scale
    issues. Fix M9 problems before advancing to recording.

Known limits: cloud measurements are short CPU-only comparisons, not Unity/GPU/FPS
results. Native input, fonts/glyphs, off-white lighting, line thickness/aliasing,
picking and actual layout need local review. Primitives and low-poly people are
procedural; no external character pack is added. Overlays are batched schematic
views; congestion uses world-space disks with fixed layer opacity and trails use
uniform layer opacity. Large overlay sets cost CPU/GPU and memory. People retain
pooled geometry up to the workspace's peak population; offscreen posing is culled.
The legacy SimulationParity scene stays a bounded trace/primitive diagnostic, not
an alternative production renderer. Recording is M10; player/IL2CPP acceptance M11.
No Unity Editor import, frontend compilation, tests, rendering, native input or
standalone build was executed in cloud.
