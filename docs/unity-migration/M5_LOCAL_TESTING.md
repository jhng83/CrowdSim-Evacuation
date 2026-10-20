# M5 local Unity acceptance

Use the immutable M5 commit/ZIP from the delivery message. Extract the repository
to a fresh directory, add **UnityProject/** through Unity Hub, and open with
**Unity 6000.3.26f1** (URP 17.3.0). The **Assets**, **Packages** and
**ProjectSettings** folders are inside UnityProject. No original .NET executable
or SDK is required locally; first import needs Unity's package registry.

1. Open **Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play, and choose
   **Build furnished example (26 people)**. Expect an editable room, five furniture
   sets, 26 planned people including 11 seated, one exit and all three obstacle
   choices. The top bar shows M5 and `*` for unsaved authored changes.
2. Open **Files**. Record the displayed **Save directory**; it resolves to
   `Application.persistentDataPath/scenarios`. Enter `M5 room<> test` in **Save name**.
   Expect the target hint to end in `M5_room_test.json`. Click **Save**. Expect a
   success message, a `[saved]` entry in the list, no dirty star and the actual JSON
   at that path. File contents describe initial placement/configuration, not runtime
   agents. Ordinary Save updates the current writable target directly.
3. Enter `M5_copy`, click **Save As**, and expect a second file. Click Save As again:
   expect a replacement dialog with the exact path. **Back / keep existing save**
   cancels without changing that file; repeat and **Replace save** to confirm. Try
   `../M5:name` and reserved `CON` as new names: the hint must stay inside the save
   directory and normalize them to `M5_name.json` and `scenario_CON.json`. Use
   **Refresh file list** to see external additions/removals. No write targets may
   escape the displayed directory.
4. In **Build / Run**, choose **Place people**, place one person on clear floor,
   and expect 27 planned people and dirty `*`. Return to Files and select the older
   26-person save. **Load selected (paused)** should ask about unsaved changes.
   **Keep working** retains 27 people and the current scene. Repeat and **Discard
   session and continue**: expect the saved 26-person scene in a paused builder,
   a clean name and Replay disabled until finalisation.
5. Make another authored change, then Files > **New Scenario**. **Keep working**
   cancels. Repeat, give **Save name before continuing** a new name, and choose
   **Save and continue**: expect a saved copy and a new empty scenario. Load that
   copy to confirm the change was saved. Test the same dirty choices using **Return
   to startup** and **Open M2 diagnostics**; neither may lose changes on cancel.
   New/leave requests pause a running scene. A clean scene leaves without a dirty
   prompt. Stopping Play Mode itself is controlled by Unity; save before stopping.
6. Load the furnished save again, **Finalise scene**, and note tick zero, population
   26 and 11 seated. **Start**, **Arm Explosion**, then click near the round table
   to cause runtime movement/damage. Run for several seconds; pause and inspect
   furniture/people. Save As `M5_after_damage`. Runtime motion alone must not mark
   authored data dirty. **Replay from start** must pause at tick zero, restore all
   initial positions, 11 seated people, intact furniture and an untriggered hazard.
   Load `M5_after_damage`, finalise again: expect the same initial authored scene,
   rather than current injuries or damaged/moved furniture. Replay is a finalized
   checkpoint reset; loading deliberately returns to an editable builder.
7. Start again and open Files. **Load selected (paused)** and **Import path
   (paused)** must be disabled. Pause through the footer: they become available.
   Save is allowed during runtime and still writes authored starts. Camera,
   selection, grid, playback and preview changes must not make a saved scene dirty.
8. In Files, load bundled **legacy_v1 [read-only]** (discard/save any dirty scene
   when prompted). Expect four legacy random starts reported before finalisation,
   metric-sized room/exits and an internal legacy wall. Finalise: expect four people.
   Run, then Replay: expect the same seeded initial positions and tick zero. Save
   As a new writable copy; the exported file must have `"version": 4`. The bundled
   source must be unchanged. Repeat with **legacy_v2**: this original example has
   **zero people**, two exits and seven blocks. Add floor people before finalising;
   zero people is the fixture's authored content.
9. Load **legacy_v3** then **toxic_fume_205** separately. Both should open as clean,
   paused Fume builders with 205 people, including 80 seated. Finalise to precompute
   the saved source's obstacle-aware cache; the Hazard inspector reports cache
   readiness. **Start**, then **Release saved Fume**; expect a triggered hazard,
   advancing ticks, reactions and eventual seat departures. Pause and save a copy.
   **Replay from start** must pause at tick zero with 205 people, 80 seated, upright
   intact furniture, rebuilt valid cache, unreleased hazard and cleared exposure.
   Start/release must work again. Repeat once with Hybrid changed before finalising;
   individual contacts remain active. Cache computation/replay can take time.
10. Test **Import JSON path (read-only)** by pasting an absolute path to a copy of
    a valid original or exported scenario outside the save directory. Expect a
    read-only source identity and paused builder. Save As copies into the displayed
    writable directory; the imported file must remain unchanged. Native legacy save
    directories are also listed if present on your machine. Duplicate basenames
    must remain distinguishable by `[saved]`/`[read-only]` and directory labels.
    Loading old files does not copy them automatically.
11. Import a missing `.json` path and a temporary malformed JSON file, then a file
    with unsupported `version: 99`. Expect a readable failure and the exact current
    session retained. For an optional safe write-error check, create a directory
    named `M5_blocked.json` inside the displayed save directory and try Save As
    `M5_blocked`: expect failure, retained dirty state and existing saves unchanged.
    Remove only that test directory afterward. Do not alter real save permissions.
    A failed Save and continue must leave its pending decision available.
12. Save your scene, return/reopen the workspace, then stop/re-enter Play Mode and
    reload the saved entry. Restart the Editor if convenient: the same persistent
    path and saved files must remain available. Resize Game view to **1280x720** and
    **1600x900**. Files controls must scroll, the transport/footer remain accessible,
    and confirmation cards scroll without clipping their choices/path. A modal
    must block background placement, shortcuts and simulation stepping. Check text
    focus, inspector hide/show/F4, camera, selection and accepted M4 furniture tools.
13. Open **Window > General > Test Runner**. Run **25 EditMode** and **7 PlayMode**
    supplied cases. M5 adds eight EditMode schema/file/replay cases and two real
    Files-page interactions, with temporary injected save directories. Run the M2
    six-case diagnostics too if checking engine results on your actual Unity host;
    they must retain PASS/replay. Do not replace goldens to resolve failures.
14. Report import/Console status, actual test totals, saved-file paths/persistence,
    schema results, dirty/overwrite decisions, damage/replay and Fume205 results,
    plus layout/input issues. Include reproducible steps and logs for failures.
    M5 issues must be fixed before M6.

## Scope and unexecuted checks

Cloud checks pass 404 original, 415 portable and 91 migration tests, both unchanged
six-case trace captures, and five actual native/portable codec/store round trips.
Those interop runs use .NET 8, not Unity Mono. Your actual filesystem/Unity tests
are necessary before acceptance. Original files, dependency pins and M1 goldens
remain unchanged; [VALIDATION.md](VALIDATION.md) records the evidence.

No Unity Editor is installed in cloud: M5 import/frontend compilation, supplied
Unity tests, native input/picking/layout/rendering and player/IL2CPP builds were
**not executed**. This delivery is an Editor project, not a standalone executable.

Saved Fume scenes have minimal finalise/precompute/release controls for M5 replay;
new Fume source placement, settings, preview and plume visualization remain M7.
Full Explosion radii/effects/overlays remain M6, humanoids/cameras M8, complete
analysis/scale M9, recording M10 and standalone acceptance/distribution M11.
