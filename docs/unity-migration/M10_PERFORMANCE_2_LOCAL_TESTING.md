# M10 performance update 2 — local testing

This continues M10 on `unity-migration`, based on `38ab742`. The user reports that
build's import bug fixed and lag much better. This update targets remaining CPU
work and repeated Unity view updates. Keep the prior extraction for comparison.

1. **Open a fresh extraction's UnityProject/** in **Unity 6000.3.26f1** through Hub.
   Open **Assets/CrowdSim/Scenes/Bootstrap.unity**, then Play. Expect **PERFORMANCE
   UPDATE 2**, the same black text/off-white architecture/grid and all M10 features.
   Committed package version is 0.10.2; URP remains 17.3.0. Import should compile
   without the fixed recording CS0234. For registry failures use M10_IMPORT_RECOVERY.md.
   In Debug, note **Editor C# optimization**. If it reads Debug, stop Play, choose
   **CrowdSim → Performance → Use Release Code Optimization**, wait for recompilation
   and reenter Play. Expect the readout to say **Release**. This explicitly changes
   the Editor's compilation/debugging setting; nothing changes automatically on
   import. To restore script debugging, stop and choose **Use Debug Code Optimization**.
   Compare builds using the same setting, and record whether Release alone helps.
2. **Repeat both supplied comparison scenes.** Debug → Reproducible crowd comparisons:
   create **New dense 800-person layout (32 tables)**, then separately population
   **1000**, furnished unchecked → **New comparison layout**. These are generated
   layouts, not bundled filenames. Keep Hybrid off, GPU instancing enabled, speed
   1×, recording off and the same camera/window. Finalise, Start and detonate at the
   same clear position/tick as the prior build; replay between comparisons.
   Compare engine/response/SFM/move-collision/world CPU and frame p50/p95 around the
   same ticks (the supplied screenshots show dense tick 190 and open tick 110).
   Expect matching behavior and less repeated CPU work; actual FPS improvement
   depends on your hardware and was not measured in cloud. Retain the observed
   numbers even if movement/collision still dominates.
3. **Check world-view reuse.** Debug now shows **Last scene draw · updated … · reused
   unchanged …**. These count non-crowd scene descriptors on the last scene redraw,
   not every rendered frame. On a stationary furnished scene most furniture should
   be reused after the initial draw; moved/fallen/destroyed furniture and debris
   must still update/disappear immediately at the next simulation frame. Start,
   pause, replay and switch layouts; no stale or duplicate assets should remain.
4. **Check builder invalidation.** Place/move the pointer between valid and invalid
   previews, undo, switch tools, and place square/round pillars. Expect green/red
   previews to change correctly and disappear on leaving the viewport; the grid
   persists. Resize/rotate/place the concert stage and save/load it. Reused views
   must not freeze moving previews, dimensions, colours or authored changes.
5. **Check crowd colours, poses and cameras.** Toggle GPU instancing off/on while
   paused; test both 0.45× and 2.5× zoom, selection, Follow, POV and F4. Expect the
   same state colours, shaded volumetric parts, outlines/rings and labels, with
   correct seated/rising/walking/running/crawling/prone/help poses and picking.
   No flat crowd fallback or loss of hidden-inspector state. Perspective keeps
   per-agent detail selection; selected people remain detailed at every zoom.
6. **Repeat Toxic Fume and recording.** Generate the dense fixture from the Toxic
   Fume workspace, Compute/Finalise/Start/Release; repeat view/camera/replay checks.
   Record a short AVI in each hazard with the inspector hidden/shown; stop and play
   it. Expect complete application UI/crowd capture and preserved stage guards,
   exposure, contact and exit behavior. Synchronous recording readback can still
   add stalls, so compare performance separately with recording off.
7. **Run supplied Unity tests and report.** Stop Play; run **52 EditMode / 24
   PlayMode** cases in Test Runner, keeping Game view active for recording tests.
   The two new PlayMode cases cover retained world movement/colour/shape/prefix
   invalidation and crowd material shades across poses/zoom. Existing GPU
   instancing/picking/POV/fallback cases remain. Report errors/test XML and before/
   after Debug screenshots, CPU/GPU model, OS, tick/hazard/layout/Hybrid/speed/backend.
   Include the Editor C# optimization mode. The supplied menu is disabled during
   Play/compilation and must not change the setting merely by opening the project.

Cloud executed .NET regression/parity/CPU checks documented in VALIDATION.md and
M10_PERFORMANCE_2_BENCHMARK.md. **No Unity Editor is installed: frontend/shader
compilation, these Unity tests, actual native rendering/Mono/GPU/FPS, UI/input,
recording and player builds were not executed.** The previous build's user results
are not acceptance of this new change. M10 testing continues; M11 remains pending.
