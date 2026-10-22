# M11 CPU query fix — local testing

This fixes reported M11 dense-scene lag; it does not advance the roadmap or mark
M11 accepted. Base is `e76d7262bceadc1f314f306274d6df025c0f9468` (v0.11.0).
Keep that extraction for a matched comparison. The new version is **0.11.1**.
Unity stays **6000.3.26f1**, URP **17.3.0**, Windows x64/Mono.

1. **Open a fresh source extraction.** Open its UnityProject folder, directly
   containing Assets, Packages and ProjectSettings, at a short path such as
   `C:\Unity\CrowdSimFix\UnityProject`. Open
   **Assets/CrowdSim/Scenes/Bootstrap.unity**. Play: expect **CPU query fix** and
   **v0.11.1**. All accepted light surfaces/black text/pillars/grid and M11 quit,
   help/notices/build functions remain.
2. **Read the diagnostic context.** Open a workspace and select Debug. The first
   line of the performance card now shows build version and Editor optimization
   mode. Alternatively click **CrowdSim → Performance → Show Editor Optimization
   Status** in the top menu. This only reads the setting; **do not switch modes**.
   Keep Hybrid off, GPU instancing on, playback 1×, recording off, the same Game
   view size/camera and the same optimization mode for before/after runs.
3. **Repeat the reported Toxic Fume scene before release.** From Toxic Fume,
   Debug → Reproducible crowd comparisons → New dense 800-person layout (32
   tables). This is a generated layout, not a filename. Compute the field,
   Finalise and Start; leave Release Toxic Fume unpressed. Compare the same ticks
   (for example 50, 100 and 190) in v0.11.0 and v0.11.1. Capture engine tick,
   movement, SFM, move/collision, world CPU and frame p50/p95 values. Expect
   unchanged motion/contacts and less repeated chair/table query work; there is
   no promised FPS or prediction that the reported 94 ms becomes a fixed value.
4. **Repeat after the hazard and check both layouts.** Replay and release at the
   same recorded tick in both versions, then compare the same ticks. Repeat
   Explosion with identical detonation point/tick. Also test the Debug-generated
   1000-person comparison with Furnished unchecked. Benchmark CPU evidence is
   reported separately for released/unreleased hazards and includes slower cases;
   Editor Mono, native graphics and application FPS were not measured in cloud.
5. **Check exact collision and furniture behavior.** Watch people stand/depart,
   push loose chairs and groups, avoid round tables/pillars, queue at exits and
   negotiate crowded contacts. Repeat with Hybrid on after pausing/replaying.
   Blast-fallen/destroyed/moving chairs must update collision/navigation immediately.
   Replay, change/load layouts and edit then rerun: no stale obstacle locations.
   Fixed dt remains 0.15 s; all physical guards and individual exposure remain.
6. **Check retained presentation and workflows.** GPU on/off, low/high zoom,
   picking/Follow/POV, F4/hidden inspector, overlays, resized stage/save/load/replay,
   and short AVI recording in both hazards must behave as before. Quit with an
   active recording and a dirty scene: keep working/save/discard decisions and
   normal recording finalization remain. Keep recording off for CPU comparisons.
7. **Test an independent player as well as Editor.** Stop Play, select Windows
   x64 with its Build Support installed, and choose
   **CrowdSim → Build → Windows x64 Player and ZIP**. Expect a fresh output under
   `UnityProject/Builds/Windows-x64/<run>/`, including
   **CrowdSim-Windows-x64-0.11.1.zip**. Extract all player files elsewhere, close
   Unity and run CrowdSim.exe. Repeat the dense/open tests with the same setup.
   Player may perform differently; this is a local measurement, not an assumed
   speedup. Complete [M11 standalone testing](M11_LOCAL_TESTING.md).
8. **Run tests and report results.** Unity Test Runner still supplies **55
   EditMode / 26 PlayMode** cases; none ran in cloud. Send any compiler/Console
   errors, before/after screenshots at matched ticks, CPU/GPU, optimization mode
   and whether the run was Editor or standalone. Final M11 acceptance remains
   pending until both the reported lag fix and full standalone workflows pass.

The source ZIP is not a prebuilt Windows executable. No Unity Editor is installed
in cloud; actual compilation/shaders/Mono/GPU/native window/recording/player tests
must be run locally. No Editor optimization switch, approximation, force cutoff,
dt/RNG/order/default-Hybrid change or collision simplification is part of this fix.
