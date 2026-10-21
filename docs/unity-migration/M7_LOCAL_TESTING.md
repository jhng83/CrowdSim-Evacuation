# M7 local Unity acceptance

Extract the immutable M7 ZIP from the delivery message into a fresh directory.
Open its **UnityProject/** folder with **Unity 6000.3.26f1** (URP 17.3.0).
Assets/Packages/ProjectSettings are inside UnityProject. No original .NET executable
or SDK is required locally. First import needs Unity's package registry. Existing
per-user saves retain the same persistent location.

1. Open **Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play. Expect two scenario
   cards with direct builder/example actions. **Validation and project diagnostics**
   expands the setup checks and M2 parity scene action. Review typography, contrast,
   card wrapping and keyboard focus at **1280x720** and **1600x900**.
2. Choose **Build a Toxic Fume scene**. The common room/exit/asset/people tools remain.
   Add an exit and a 15-person group on clear floor. The left Fume card explains
   readiness; **Fume setup →** opens the right **Hazard** page, including when hidden.
   Expect source → compute → preview → release guidance and an active navigation tab.
3. Click **Set / move release point**, then hover/click clear floor near the people
   (e.g. source around **(26,17)**, people group around **(24,17)** in a default room).
   The candidate is green on clear floor and red over blocked/outside positions.
   Reject clicks on walls/circles/furniture without replacing a valid source. Escape
   over the viewport cancels the tool. The source marker/radius remain visible when
   plume display is off. Source movement is separate from geometry Undo, as in the
   reference; use Set/move to relocate it.
4. Inspect four basic settings: radius, speed, extent and duration. Expand **Advanced
   • drift and exposure** for direction, directional bias, awareness/high-threat
   thresholds and impairment/incapacitation exposure. Sliders allow numeric entry.
   Expect native bounds/dependencies: extent ≥ radius, threat ≥ awareness+0.05 and
   incapacity ≥ impairment+1. Nonfinite input must restore a finite value without a
   Console error. **Reset Fume Defaults** retains the source and invalidates the cache.
5. Before compute, Finalise should be disabled for a newly authored source. Click
   **Compute dispersion**. Expect FIELD READY, reachable/total cells, **0.50 m** grid,
   cache KiB, measured compute milliseconds and maximum arrival time. No runtime
   propagation starts yet. The preview slider becomes available.
6. Scrub **Seconds after release** through 0, 10, 30 and later times, then **Reset
   preview**. Expect a green/yellow field spreading around static obstacles and
   eventually decaying. Concentration legend runs 0→1. People, tick zero, exposure
   and saved authored data must remain unchanged. Preview is view-only and does
   not dirty a saved scene. Turn **Show fume grid cells** on/off; no cells should
   appear in engine-blocked cells. This is the original grid approximation, not
   analytic boundary clipping or chemical CFD.
7. Change a source/setting or place/undo room geometry, furniture or people after
   computing. Expect the preview to disappear, cache invalidation and a recompute
   requirement. Recompute to recover. Changing behavior/Hybrid, camera, selection or
   display flags alone must not invalidate the propagation field. A room resize must
   reject dimensions excluding its existing source. Compare drift 0 versus stronger
   bias/direction in two computations; expect the corresponding directional change.
8. Finalise after computing. Settings/source/preview must lock. Expect paused frame
   zero and spawned people. Start, then **Release Toxic Fume** in the footer. A second
   release is disabled/rejected. Before release, the initial field is shown with
   NOT RELEASED labels and zero exposure; release starts the live field timeline.
   Pause must freeze event time, crowd motion and exposure; playback changes still
   use the configured fixed step.
9. Inspect field time, exposed/impairment/incapacity/high-concentration counts, mean
   cumulative exposure and peak concentration. Select a person for current/peak
   concentration, cumulative exposure, status and mobility. Expect reaction/panic,
   eventual departure from seats, individual collision/navigation behavior and
   exposure-dependent mobility. Social contagion may affect people outside the field.
10. In a separate experiment, set impairment to **1**, incapacitation to **2**, and
    use people close to a source with adequate radius/duration. Some may leave the
    field before reaching the threshold; outcomes depend on layout/time. A person
    who becomes incapacitated must stay incapacitated after the field clears.
    This is normalized simulator exposure, not a chemical/medical prediction.
11. Save after release and use **Replay from start**. Expect paused tick zero, original
    people/positions, intact furniture, zero exposure, an unreleased regenerated cache
    and original authored settings/source. The runtime save must not preserve moved
    positions or incapacity. Start/release should work again. Loading the saved copy
    opens a clean editable builder. Unedited saved-source files retain M5's automatic
    Finalise/cache shortcut; click Compute first when previewing them.
12. Return to startup and choose **Open 205-person Fume example**. Expect an editable
    builder with **205 people, 80 seated**, and the original saved source. Compute,
    preview, finalise, start/release and replay. Expect 205/80 seated and zero exposure
    after replay, with the valid unreleased cache. Repeat with Hybrid enabled before
    finalisation, then off in a fresh run. View toggles must not alter either mode's
    physical results. Re-run the M2 six-case diagnostics if checking actual-host parity.
13. Hide/show the inspector with F4/button during preview and runtime. Plume/grid
    choices, active tab and selection must persist while overlays continue. The
    master plume toggle preserves the grid choice; the source remains visible.
    Resize/scroll so settings and the fixed transport stay accessible. Check text
    focus against F4/Space/R/Ctrl+Z and Alt+F4 behavior from accepted milestones.
14. Open **Build furnished example (26 people)** to review the requested asset polish:
    warm table tops/grain, muted teal chairs/dark supports, slate floor/platform,
    striped barricades and shaped lighting. Tables/chairs/barricades have softened
    edges; the stage retains its rectangular physical footprint. Verify dimensions,
    rotation, previews, seating/undo and the M6 Explosion workflow still match. No
    Unity colliders/rigid bodies may drive people/furniture. Check low/default/high
    zoom, camera rotation, interior obstacles and the transparent physical perimeter.
    Report pink/missing shaders, too-dark surfaces, texture artifacts or illegible UI.
15. Return/discard/reopen, switch examples and stop/re-enter Play Mode without stale
    effects, lights or Console errors. Save before stopping Play Mode. Run **Window >
    General > Test Runner**: **34 EditMode / 12 PlayMode** supplied cases. Report actual
    totals, import/Console results, source/settings/compute/preview/release/replay,
    both Hybrid modes, styles/materials/lighting, native input and resize observations.
    Include reproducible steps/logs for any failure. M7 issues must be fixed before M8.

## Executed evidence and limits

Cloud validation passes **404 original**, **415 portable**, **135 migration** tests,
both unchanged six-case trace captures, five actual native/portable file interop
cases and static asset/reference checks. See [VALIDATION.md](VALIDATION.md).

No Unity Editor is installed in cloud. M7 import/frontend compilation, all supplied
Unity tests, native input/picking/layout, material/light/shader appearance and
player/IL2CPP builds were **not executed**. No screenshot or graphics-performance
claim is supplied. The polished presentation needs your actual Editor review.
Large fields use batched meshes at native cell resolution; no FPS guarantee is made.

The model remains the existing 0.5 m obstacle-aware normalized cache with fixed
24 s post-release decay. Display colors are grouped into eight buckets; physical
concentrations are not quantized by the new view. Full humanoid poses/detail/Follow/
POV remain M8; complete general diagnostics/overlays/scale are M9, recording M10,
and standalone acceptance/distribution M11. M7 is an Editor project delivery.
