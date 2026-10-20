# M4 local Unity acceptance

Use **Unity 6000.3.26f1**, URP **17.3.0** and a fresh extraction of the delivered
commit ZIP. Add its **UnityProject/** folder in Unity Hub. Exact scene:
**Assets/CrowdSim/Scenes/Bootstrap.unity**. No original .NET process/SDK is required.
M3 was accepted; M4 adds furniture to the same workspace. The unified **Asset**
picker replaces M3's separate square/circle controls while retaining all choices.

1. Allow import/compilation, open Bootstrap and enter Play Mode. Expect no Console
   errors. Choose **Build furnished example (26 people)**. It opens an editable
   56x34 m room, one 5-person exit, three obstacle choices, all five furniture kinds,
   **26 planned people / 11 seated / five furniture sets**, tick zero, not running.
   Inspect using Camera and the Debug room grid. Table/chair backs and supports
   should be volumetric; stage/barricade stay fixed. Dimensions are below.
2. Finalise the example: population must remain 26, with 11 seated occupants and
   unchanged starts. Start briefly without an event: seated people stay attached
   to their chairs, while the 15 floor agents can wander. Pause: all physical motion
   and ticks freeze. Geometry, picker, facing, snapping and undo lock after finalisation.
3. Resume, **Arm Explosion**, then click near **(18,12) m** (the viewport shows cursor
   coordinates). Inspect at 0.25x/1x and pause as needed. Expect furniture movement,
   height changes, tipped/fallen bodies and damage/destruction according to the
   engine. Destroyed bodies must lose every visual part. Overview reports airborne,
   fallen and destroyed counts; counts are per physical body, not per rendered part.
   Outcomes depend on the chosen event point/time; no exact casualty count is promised.
4. Use **Return to startup**, confirm discard, then **Build an Explosion scene**
   for manual authoring. Place a boundary exit. Choose **Round table + 10 seated
   people** in Asset; inspect the green candidate and place it on clear floor.
   Expect ten seated planning figures and +10 people. Place a **single chair** well
   away: +1, total 11. One undo removes that chair/occupant; a second undo removes
   the entire round table, ten chairs and ten occupants. No orphaned view/count remains.
5. Try all eight picker choices, including both square sizes and the circle. The
   round set, single chair, rectangular table, stage and barricade must match the
   dimensions/occupancy table below. Place them with adequate room and seat-departure
   clearance. Green preview means valid; red candidate geometry means rejection.
   Bounds, overlap and occupied-floor guardrails must reject invalid clicks without
   changing counts or consuming an undo entry.
6. Give the single chair an exact **37.5°** facing; switch to another asset and back:
   its facing is retained. Give the round set another angle and verify independent
   memory. For the rectangular table and stage, enter **68°**: expect **90°** because
   they use quarter turns. Repeated Rotate 90° wraps 270° to 0°. Blocks/circles show
   no irrelevant rotation controls. Existing placed assets must never rotate when
   next-placement facing changes.
7. Choose barricade, enter **37.5°**, and verify a free-angle preview. Test **+15°**,
   **-15°**, **R** and **Shift+R** over the viewport: 37.5→52.5→37.5. Hovering the room
   with this tool active is required for keyboard rotation. Check negative input
   (e.g. -22.5→337.5°) and wraparound (382.5→22.5°). Nonfinite angle input must retain
   the last valid facing without a Console exception. While a numeric text field is
   focused, R/F4/Ctrl+Z/Space must not trigger simulator actions. Click/hover the
   viewport again before testing shortcuts; Alt-modified keys remain untouched.
8. With **Snap barricades end-to-end** on, place a barricade around (20,20) at 37.5°.
   Move the next candidate near **(21.3,21.0)**. Within 0.30 m of the parallel endpoint
   it should snap into an end-to-end chain without changing its angle. Preview and
   placement must agree. Turn snap off and verify the requested center is retained
   (a small clear gap is allowed). Distant/perpendicular/blocked endpoints must not
   pull the cursor; true overlap is still rejected. Repeated placements can form a
   chain. R/Shift+R changes the next member only.
9. Place a stage as the latest command. Expect one stage; selecting Stage again
   explains **1/1** and disables its placement/rotation controls. A second stage must
   not appear. Undo the stage: the guard clears and placement works again. The stage
   adds no people. The unoccupied rectangular table and barricade also add none.
10. To inspect timed seat departure separately, use a fresh scene with an exit and a
    single occupied chair around (15,15). Finalise/start, then trigger around (25,15),
    outside the default injury radius of that seat. Wait for the seeded reaction
    delay and inspect at 0.25x: seated→rising→walking, progressive rise, then release
    from the chair. Agent details report posture/rise. A casualty must not be forced
    to stand. Pushing, chair chains, impacts and landing follow the existing core;
    contact opportunities depend on layout and event placement.
11. Test another authored furnished run with Hybrid on (set before finalising), then
    off in a fresh session. Both retain individual contacts and physical furniture.
    Exercise hide/show/F4, selection with the inspector hidden, grid persistence,
    camera azimuth/elevation and 0.45x–2.5x zoom. Resize to **1280x720** and **1600x900**:
    picker/angle controls should scroll and the action footer stay accessible. Return,
    discard/reopen and stop/re-enter Play Mode without stale objects or errors.
12. Open **Window > General > Test Runner**. Expect **17 EditMode** and **5 PlayMode**
    cases passing. Re-run the accepted M2 six preset/mode checks if checking diagnostic
    visuals: all must still show PASS/replay despite richer furniture/seat views.
13. Report import/Console status, actual test totals, picker/angle/snap/undo results,
    seat/furniture motion observations, both Hybrid setups and input/layout issues.
    Include steps and logs for any failure. M4 problems must be fixed before M5.

| Furniture | Dimensions / placement rule | Added occupants | Motion |
| --- | --- | --- | --- |
| Round table set | Table diameter 1.8 m; ten chairs on a 1.5 m seat ring; 2.25 m placement reservation | 10 | Table/chairs movable and destructible |
| Single chair | Chair body with seat/back/legs; 1.05 m placement reservation | 1 | Movable/destructible; independent facing |
| Rectangular table | 3x1 m, 0.78 m high; quarter turns, exact rectangular footprint | 0 | Blast-movable/destructible |
| Stage | 15x6 m, 0.80 m high; quarter turns; maximum one | 0 | Fixed, non-walkable |
| Barricade | 1.5x0.4 m, 1 m high; free angle, exact oriented footprint | 0 | Fixed, non-walkable |

## Scope and unexecuted checks

Seated/rising people use basic volumetric parts; walking remains capsules. Complete
humanoid detail/animation, casualty/help poses and full cameras remain M8. Furniture
views use the reference's lightweight tipped profiles and live model anchors; they
are not a second Unity rigid-body simulation. Their current runtime positions never
become authored starts. Player files/replay are M5, full Explosion UI/effects M6,
Fume source/precompute workflow M7, full analysis/scale M9, recording M10 and player
acceptance M11. The common Fume builder retains furniture but still enforces M7
source/cache readiness; the accepted M2 Fume diagnostics remain available.

No Unity Editor is installed in cloud: M4 import/frontend compilation, supplied
Unity tests, native input/picking/layout/rendering and player/IL2CPP builds were
**not executed**. New PlayMode tests pass exact world coordinates through UI command
handlers; manual screen picking and R/Shift+R testing above remain necessary.
