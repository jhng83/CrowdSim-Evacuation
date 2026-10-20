# M6 local Unity acceptance

Download the immutable M6 ZIP from the delivery message and extract it to a fresh
folder. Add **UnityProject/** in Unity Hub and open with **Unity 6000.3.26f1**
(URP 17.3.0). Assets, Packages and ProjectSettings are inside UnityProject. No
original .NET executable/SDK is needed locally; first import needs Unity's package
registry. Preserve your M5 saves; the per-user save path is unchanged.

1. Open **Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play and choose **Build
   furnished example (26 people)**. Expect M6, five furniture sets, one exit,
   26 planned people and 11 seated. Accepted builder, furniture, Files and camera
   controls remain available. Open the right **Hazard** inspector.
2. Expect five color-labelled radii in metres: **Kill 1.58**, **Incapacitation 3.16**,
   **Fallen/severe 6.31**, **Injury 9.47**, **Panic/threat 18.21**. The legend colors
   are red/orange/yellow/cyan/pale grey. All zones are initially visible; no event
   zones appear before detonation.
3. Click within Kill's text and check the insertion caret. Ctrl+A selects all text.
   Type `1.25`: typing alone must not change the committed setting. Press **Enter**
   or click another control to commit. Type another value, then **Escape**: expect
   the prior committed value restored. Try `NaN`, `Infinity`, blank, negative,
   a Kill value beyond the Incapacitation radius, and a Panic value over 200.
   Expect a validation message and the previous model value retained, with no
   Console exception. Use a decimal point. F4/Space/Ctrl+Z/R must not trigger
   simulator actions while text input owns focus; Escape cancels only that edit.
4. Test each **−0.10 / +0.10** pair and **Reset Zone Defaults**. Expect 0.10 m steps
   clamped between neighboring radii, minimum radius/gap 0.10 m and maximum outer
   radius 200 m. Reset restores all five defaults. Near the 200 m ceiling, some
   extremely packed float combinations are rejected by the reference model; the
   UI must report rejection and keep the last valid configuration. A no-op field commit or
   rejected edit must not dirty an otherwise saved scene; Reset is an authored action.
5. Set Kill to **1.25**, save a copy in Files, then **Finalise scene**. Radii remain
   editable while Ready: this matches the native application. Start the run and,
   before detonating, change Panic to **22.5**, commit and save another copy.
   Expect a dirty authored change before saving, and the new radii in the saved
   definition. Geometry and behavior remain locked. Later replay must retain these
   settings with the original starting scene, excluding runtime movement.
6. Click **Arm Explosion**. Expect the viewport to request one clear-floor click.
   **Cancel Explosion** or Escape with viewport focus cancels without an event.
   Arm again, then Pause: arming must clear. Resume and re-arm. Click a square
   blockage, circle, stage or physical room boundary: expect rejection, no event,
   and the tool still armed. Click clear floor inside the room once, for example
   near **(18,12)** between the round table and single chair. The first accepted
   click detonates and clears arming. Further clicks cannot create another event.
7. Immediately inspect the Hazard page. All radius fields/increment/reset controls
   must be read-only after detonation; their displayed values must be committed
   values. Zone/shock/dust visibility remains adjustable. Reduce playback to
   **0.25x** before a fresh detonation to inspect the thin propagating shock front,
   outward fragments and sequential reactions. Debris appears as small oriented
   prisms with energy-dependent color/size; it moves from engine snapshots, not
   Unity physics. Furniture may move, fall, fly or be destroyed as in M4.
8. Watch **Physical outcomes / event analysis**: event origin/age, front radius,
   shock hits, currently shielded people, fallen/crawling/incapacitated/injured/
   fatal counts, live debris and hit outcomes, average mobile visibility and
   airborne/destroyed furniture. Select a person to inspect shock-hit flag/tick,
   shielding and visibility alongside posture/panic. Exact casualties depend on
   layout, timing and event location; no exact casualty count is promised.
9. Pause during the front/dust effect. Geometry and event age must freeze. Resume:
   the front advances on configured simulation time and disappears after its short
   lifetime. The tan dust ground field should build behind the front and decay;
   it samples the actual temporary visibility field and is not a smoke solver.
   Perception/navigation still use that field when **Show dust / visibility field**
   is off. Dust/front visibility toggles must never apply extra damage or reset time.
10. Let the run continue beyond **15 simulation seconds** after detonation. The
    transient front/dust should be gone while colored analysis bands/outlines
    remain. Toggle each zone separately and the **Show blast zones** master.
    The master must retain each zone's individual choice. Hide/show the inspector
    with its button/F4: overlays continue and flags/tab/selection persist. In a
    paused scene these display actions must leave ticks, positions and outcomes
    unchanged. The zones classify initial direct-blast distance bands, not a promise
    of current state for every moving/socially reacting person.
11. In a fresh blank Explosion scene, add an exit, a large square around **(25,17)**,
    and single people on clear floor on either side (approximately **(21,17)** and
    **(29,17)**). Finalise/start, arm and detonate around **(22,17)**. Observe early
    shielded status and zone/front shadows behind the square. Repeat with a circular
    blockage and an event near a room edge. Bands/outlines must stay inside the room
    and omit occluded sectors. The clipping uses the native renderer's conservative
    point/sector sampling, so gaps near edges are expected. Static blocks/legacy
    walls shield the direct field; **furniture, including stage/barricade, does not
    provide line-of-sight shielding in the reference model**. Social panic or later
    movement can affect protected people; direct shielding is not immunity to all
    other simulation effects. Perimeter walls stay visually transparent and physical.
12. In the furnished run, save after damage and click **Replay from start**. Expect
    paused tick zero, original people/positions, **11 seated**, intact furniture,
    no event/debris/front/dust/zones and unlocked radius editing. The latest settings
    committed before detonation must remain. Zone visibility choices persist within
    the session. Start/re-arm can produce a new one-shot experiment. Load a saved
    copy: expect an editable paused builder with authored radii/starts, excluding
    damage, moved positions and elapsed time.
13. Repeat a furnished experiment with **Hybrid on**, set before finalising, and
    again with Hybrid off. Both retain individual contacts, navigation, shielding,
    ordered outcomes and furniture/debris behavior. Re-run the M2 six-case diagnostics
    if checking actual-host parity: all should retain PASS/replay. Load the M5
    **toxic_fume_205** example and exercise finalise/release/replay: Explosion controls
    and event meshes must not appear in Fume. Full Fume authoring remains M7.
14. Resize Game view to **1280x720** and **1600x900**. The long Hazard page must scroll,
    field/buttons remain usable and the transport/footer stay accessible. Check
    0.45x/1x/2.5x zoom and camera rotation for overlays behind people, visible internal
    obstacles and volumetric bodies. Return/discard to startup, reopen and stop/
    re-enter Play Mode without stale effects or Console errors. Save before stopping
    Play Mode; Unity itself controls that exit.
15. Open **Window > General > Test Runner**. Run **29 EditMode** and **9 PlayMode**
    supplied cases. M6 adds four EditMode and two PlayMode cases. Report import/
    Console status, actual test totals, text/caret/blur/Escape behavior, arming and
    blocked clicks, timed effects, clipping/visibility, replay/save results, both
    Hybrid modes and any layout/input/shader issues. Include steps/logs for failures.
    M6 problems must be fixed before M7.

## Evidence and remaining scope

Cloud validation passes **404 original**, **415 portable** and **112 migration**
tests, both unchanged six-case trace captures, five actual native/portable file
interop cases and static asset/reference checks. This uses .NET 8; actual Unity
Mono, graphics and native input require your testing. See [VALIDATION.md](VALIDATION.md).

No Unity Editor is installed in cloud. M6 import/frontend compilation, supplied
Unity tests, native input/picking/layout/shader rendering and player/IL2CPP builds
were **not executed**. Synthetic events/world coordinates in supplied PlayMode tests
do not prove native text/pointer behavior. This is an Editor project, not a player.

Complete Fume settings/source/precompute/preview/plume UI remains M7. Complete
humanoids/cameras are M8; general force/vision/trail overlays, full diagnostics,
instrumentation and scale are M9; recording is M10; standalone acceptance is M11.
