# M3 local Unity acceptance

Use **Unity 6000.3.26f1**, URP **17.3.0**, and a fresh extraction of the delivered
commit ZIP. Open its **UnityProject/** directory in Unity Hub. M2 was accepted;
this checklist verifies the new M3 workspace. No .NET SDK is needed locally.

1. Allow import/compilation and open **Assets/CrowdSim/Scenes/Bootstrap.unity**.
   Enter Play Mode. Expect the existing startup plus **Build an Explosion scene**
   and **Open common Toxic Fume builder** entries. Open the Explosion workspace.
   Expect left Build/Run controls, room viewport, right inspector, top navigation
   and a fixed bottom action bar. Finalise/Start are disabled with a reason.
2. In Build/Run, set width/length to **60 / 40 m** and click **Apply room size**.
   The floor should resize and remain framed. Try out-of-range or nonfinite values:
   reject with a message and retain the physical room. Correct the fields afterward.
   Limits are 10–160 m width and 10–120 m length.
3. Choose a **3-person exit**, move over the floor, then click a perimeter edge.
   A green candidate preview should become a cyan opening marker; interior exit
   placement is rejected (red preview). Marker width is 1.2 m for 3 people; 1-person
   and 5-person widths are 0.48 m and 2.0 m. Room dimensions now lock. Undo with the
   button or Ctrl+Z: the exit disappears and the empty room unlocks.
4. Place exits at distinct boundary locations using **1, 3 and 5** capacities.
   Place both square sizes (**1.2 m / 2.4 m**) and a **2.5 m circle** inside.
   Check green valid/red invalid previews, boundary and overlap rejection, and
   touching-but-not-overlapping squares. Failed placement must not add an item or
   consume an undo entry. Move over empty floor to get a fresh preview.
5. Place groups of **1, 5, 15 and 50** people in separate floor regions, including
   near an obstacle. Expect **71 planned** people and raised capsule planning views,
   distributed on available clear floor. Hover previews must not change totals.
   Undo the last 50-person group: expect 21; place 50 again: expect 71. Finalise
   readiness now says ready. Counts must agree with the displayed geometry.
6. Expand **Behaviour tuning · 14 controls**. Adjust all controls using sliders and
   numeric input; verify their bounds and **Reset behaviour defaults**. The running
   speed control is a physical ceiling, distinct from playback speed. Choose Hybrid
   off or on before finalising; both modes retain individual navigation. Test F4,
   Ctrl+Z and Space while editing a numeric input: simulation actions must not fire.
   Finish editing and click the viewport before testing shortcuts. Alt+F4 is never
   intercepted by the workspace; testing it can close the Editor and is optional.
7. Click **Finalise scene**. Expect the same starts, runtime population 71, tick zero,
   ready/paused. Geometry, behavior, Hybrid setup and undo lock. Start becomes enabled.
   Scroll the left panel or switch to Camera/Files: the action footer must remain
   accessible. The Files page explains M5 ownership; no save/load workflow is claimed.
8. Click **Start**. Expect fixed-step movement; change playback to **0.25x**, **1x**
   and **4x**, then Pause/Resume. Pausing must freeze tick/physical state for several
   seconds. Resuming must not catch up paused wall time. Faster playback schedules
   more fixed ticks, with bounded catch-up under load; it does not change dt. There
   is no M2-style 120-tick cap in this authored workspace.
9. While running, click **Arm Explosion**, then click an inside-floor point once.
   Expect a source marker and the reference physical/state response as ticks advance.
   Arm disables after detonation; subsequent clicks select agents rather than causing
   another event. Pause to inspect. Detailed radii, effects and zone overlays remain M6.
10. On **Camera**, change azimuth (-180–180°), elevation (18–70°), zoom (0.45x–2.5x)
    and Reset. Check placement/selection with the camera rotated and zoomed. Runtime
    capsules remain volumetric; humanoid detail/poses, Follow and POV arrive in M8.
11. Select **Debug**, enable **Show room grid**, then hide the inspector using its
    top button or **F4** (viewport focus). The viewport must expand and the grid stay
    visible. Show it: Debug/grid settings remain. Hide again, click a runtime capsule:
    selection must persist without opening the inspector. **Show selected agent**
    explicitly opens Agent details. If the selected agent exits, stale selection clears automatically. Full diagnostic overlays are assigned to M9.
12. Resize to **1280x720** and **1600x900** (and another size you use). Verify scrollable
    controls, accessible footer, viewport resizing and pointer-to-ground accuracy.
    Scroll/typing/clicking panels must not place geometry or fire an Explosion.
13. Click **Return to startup**: expect a discard confirmation. Keep working cancels
    it; the run is paused. Return again and confirm discard: startup reappears and
    the old world disappears. Reopen Explosion to get a clean empty session. Open
    common Toxic Fume builder: room/exits/obstacles/people/tuning work, but finalisation
    explains source/precomputation ownership in M7. Use **Open M2 simulation parity**
    from startup for the already accepted Fume diagnostic runs. Repeat the M3 basic
    run in a fresh Explosion workspace with the other Hybrid setup.
14. Open **Window > General > Test Runner**. Run all EditMode cases: expect **13 pass**.
    Run all PlayMode cases: expect **3 pass**. Check the Console for red errors after
    import, repeated entry/exit, camera changes, placement and test execution.
15. Report actual import/test totals, pointer/keyboard/layout observations, both
    Hybrid results and any errors with reproduction steps. A failure blocks M4;
    do not edit the original source or parity goldens to make it pass.

## Known scope and unexecuted checks

This delivers the common builder and a basic authored Explosion run. The Toxic
Fume source/cache authoring workflow remains M7. Furniture is M4, user file/replay
workflow M5, complete Explosion controls M6, humanoids/full cameras M8, full analysis/
scale M9, recording M10 and standalone acceptance M11. Only grid overlay and basic
metrics/selection are introduced here. Perimeter physics remain active while the
perimeter is visually transparent; authored internal obstacles remain visible.

Codex cloud has no Unity Editor. M3 import, frontend compilation, supplied Unity
tests, native input/picking/layout/rendering and standalone builds were **not run**.
Cloud tests cannot establish those results. The new PlayMode test exercises UI
commands with exact world-click coordinates; manual screen picking above is still
required. No Windows/IL2CPP/player performance guarantee is claimed.
