# Approved Unity replacement roadmap

The user approved this sequence before M1. Reference commit:
`01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`. Preserve the original application,
tests, dependency pins and authored examples. Development occurs in cloud/GitHub
on `unity-migration`, with Unity under `UnityProject/`. Main is unchanged.

## Architecture and compatibility

Pin Unity 6000.3.26f1 and URP 17.3.0. Use UI Toolkit and a Unity-independent,
C# 9-compatible netstandard2.1 engine package. Cloud tests and Unity consume the
same portable production source. Separate migration build settings avoid inheriting
the reference's net8.0/C# 12 configuration. See COMPATIBILITY.md.

The model remains authoritative for motion, routing, contacts, hazards, furniture
and exit admission. Preserve units, 0.15-second default/smaller configured steps,
seeded randomness, update order, numerical guards, Hybrid detailed fallback, fume
cache invalidation, and authored-definition versus finalized-checkpoint semantics.
Unity handles input, presentation, UI, cameras and capture. View interpolation and
animation never mutate physical state. Keep perimeter geometry physically active
and visually transparent; retain volumetric runtime humanoids at every zoom.

The user requested a second performance fix during M10 testing after accepting the
recording namespace correction and observing improved but remaining lag. This is
an M10 fix iteration (CPU repeated work/allocations and retained Unity views), with
the same preservation requirements; it does not advance to or replace M11. See
M10_PERFORMANCE_2_LOCAL_TESTING.md and VALIDATION.md.

## Milestones

Each milestone depends on the previously accepted milestone. Each delivers a
downloadable project, exact scene, tests and acceptance checklist. Bootstrap means
`Assets/CrowdSim/Scenes/Bootstrap.unity` throughout.

### M1 — Openable Unity foundation and migration baseline

- Capability: open the pinned project and enter Play Mode into a usable startup screen.
- Features: project/URP/input settings, UI Toolkit, assembly boundaries, sample data,
  reference outputs, migration documentation and GitHub validation checks.
- Architecture: add UnityProject, portable package shell, isolated cloud harness and
  trace format; identify engine, JSON and recording compatibility risks.
- Automated: unchanged reference suite, Standard API/dependency probes, static
  GUID/project/package/binding checks and repeatable reference captures.
- Local: open Bootstrap, exercise overview controls, resize, enter/exit Play Mode,
  and run initial EditMode/PlayMode tests.
- Complete when: clean local import/compilation and functional startup are accepted,
  with cloud checks reproducible. No simulation parity is claimed.

### M2 — Portable engine and executable parity demonstrations

- Capability: run preset Explosion/Fume scenes in a diagnostic Unity scene with
  simplified 3D representations: `Assets/CrowdSim/Scenes/Validation/SimulationParity.unity`.
- Features: all core states, SFM/panic, navigation, contacts, exits, both hazards,
  furniture, Hybrid, scenario definitions/codec and checkpoint reconstruction.
- Architecture: compatibility utilities, fixed-step driver, coordinate conversions,
  portable core and presentation snapshots; retain algorithms and tie ordering.
- Automated: unchanged 404 reference cases and corresponding 376 non-frontend cases
  against the portable engine; seeded traces, furniture/hazard outcomes, reconstruction.
- Local: run both presets, pause/resume and compare tick results with reference.
- Complete when: portable regressions and defined trace comparisons agree, including
  actual Unity runtime checks. Diagnose numeric divergence; never weaken tests to pass.

### M3 — Common builder and application workspace

- Capability: author a basic room, finalize/start, trigger the basic Explosion event,
  and pause/resume in Bootstrap.
- Features: entry screen; room sizing/locks; exits 1/3/5 wide; square sizes/circles;
  floor groups 1/5/15/50; previews, undo, readiness reasons; all 14 tuning controls;
  Build/Run/Camera/Files and inspector organization; 0.25x–4x transport.
- Architecture: session state, builder commands, picking, focus-aware input and presenters.
- Automated: placement/overlap/invalid input, finalization/timing/transport, layout and
  inspector tests; relevant original frontend helper coverage.
- Local: build/undo/run, test invalid finalization and resizing, toggle inspector/F4.
- Complete when: common author-to-run workflow works; action footer stays accessible;
  tab/selection/overlay state survives hiding and shortcuts respect text entry/Alt+F4.

### M4 — Furniture authoring and physical presentation

- Capability: author and run furnished scenes with occupied seats and existing interactions.
- Features: every furniture kind/dimension/occupancy, one-stage limit, per-asset rotation,
  quarter turns, free barricade angles, exact entry, +/-15 degrees, snapping and shortcuts.
- Architecture: furniture views/placement adapters, standing/departure and moving,
  airborne/fallen/destroyed states driven by the portable core.
- Automated: furniture placement, seating, push/chains, impacts/flight/destruction,
  precise rectangular/rotated footprints and obstacle updates; remaining frontend helpers.
- Local: place all kinds, verify totals/angles/snaps, trigger Explosion, inspect motion.
- Complete when: previews and accepted geometry agree; all assets and interactions
  retain reference behavior, including fixed stage/barricade rules.

### M5 — Scenario files, legacy compatibility and replay

- Capability: save/reload authored scenes and replay from the finalized starting scene.
- Features: naming/Save As/list/refresh, paused loading, dirty prompts/New Scenario,
  errors, v1–v4 upgrade paths, legacy units/blue walls and writable/read-only locations.
- Architecture: platform filesystem adapter, JSON compatibility and replay lifecycle.
- Automated: .NET/Unity round trips, defaults/enums, malformed inputs, atomic writes,
  legacy fixtures and replay after damage using the 205-person scene.
- Local: load old files, save a copy, damage/run/replay and verify frame-zero starts,
  intact furniture and regenerated unreleased fume cache.
- Complete when: supported files interoperate and runtime damage never becomes authored state.

### M6 — Complete Explosion workflow and analysis

- Capability: full experiment with editable radii, physical outcomes and analysis overlays.
- Features: single armed detonation; five ordered radii; text commit/cancel/increment/reset;
  post-event locking; shock/debris/dust/visibility, shielding, zone views and metrics.
- Architecture: Explosion presenters, view effects, wall-clipped zone geometry/caches.
- Automated: shock timing, shielding, ordered states, debris, furniture damage,
  visibility, parameter locking and overlay noninterference.
- Local: blast near walls/furniture; edit before/toggle after; inspect effects and states.
- Complete when: full hazard workflow matches reference and view changes cannot reapply damage.

### M7 — Complete Toxic Fume workflow

- Capability: source authoring, precompute/preview, release, exposure inspection and replay.
- Features: all radius/speed/extent/duration/drift/threshold/exposure settings, resets,
  readiness/invalidation, timeline, plume/grid and metrics.
- Architecture: fume presenters/cached visualization retaining the 0.5 m normalized field
  and setup/runtime distinction; no added chemical/CFD solver.
- Automated: arrival/concentration parity, guards/invalidation, local risk navigation,
  impairment/permanent incapacity and preview/runtime isolation.
- Local: compute the 205-person fixture, scrub preview, invalidate/recompute, release/replay.
- Complete when: the complete workflow and exposure outcomes agree with the reference.

The user additionally authorized intuitive professional UX/UI and asset refinement
with M7 after accepting M6. This includes scenario entry/cards, navigation/action
hierarchy, readiness/preview guidance and owned surface/mesh/lighting refinements
for both scenarios. Physics/dimensions are preserved. Complete humanoid poses,
zoom detail and camera features remain in M8 below.

### M8 — Humanoids, selection and cameras

- Capability: complete current agent presentation and camera operation.
- Features: always-volumetric near/far humans, all poses/animation, colors/outlines,
  panic/help/alarm indicators, selection/legends, isometric controls, Follow and POV.
- Architecture: pooled presentation views, visual interpolation/detail, picking and cameras.
- Automated: pose/color mapping, coordinate conversion, selection/camera cleanup and
  proof that presentation leaves physical state untouched.
- Local: both hazards at 0.45x/1x/2.5x, hidden-panel selection, Follow/POV and agent exit.
- Complete when: all visual states/camera actions work and the transparent perimeter
  and volumetric bodies persist at every zoom.

### M9 — Diagnostics, inspection and crowd scale

- Capability: complete analysis tools and measurable Hybrid comparisons.
- Features: every overview/agent/navigation/assistance/hazard metric; per-exit data;
  direction/force/vision/congestion/trail overlays; timing/allocations; paused Hybrid toggle.
- Architecture: diagnostic models, overlay batching, instrumentation and rendering
  optimizations that preserve simulation ordering.
- Automated: metric/overlay correctness, Hybrid fallback/fields/speed and benchmark adapters.
- Local: open/furnished runs at 220/500/1000, both Hybrid modes, every overlay while hidden.
- Complete when: diagnostic parity is accounted for and CPU/render/allocations/FPS
  are reported distinctly, without invented performance guarantees.

### M10 — Runtime complete-interface AVI recording

- Capability: record playable videos containing the composed world and application UI.
- Features: arm/start/stop/status/path, 1280x720/20 fps MJPEG AVI, bounded queue/drops,
  safe replay/scenario/quit cleanup and resizing/orientation handling.
- Architecture: Unity end-of-frame capture and compatible encoder integration; player
  recording must work independently of Editor-only recording tools.
- Automated: encode/decode known frames, format/rate/size/count, queue/error/lifecycle checks.
- Local: record both hazards while resizing/using controls; stop/play and verify UI inclusion.
- Complete when: output is valid and correctly oriented, memory bounded, lifecycle safe,
  and fixed-tick simulation behavior unchanged.

The user additionally authorized adjustable stage width/length and a concert-style
asset with M10 after accepting M9. Include builder fields for the next/placed single
stage, rotated usable-room/perimeter bounds and overlap rejection; keep preview,
physical footprint, authored save/load and replay consistent. Preserve the native
15×6 m default and stage's fixed/non-walkable/unoccupied behavior. Custom dimensions
use explicit Unity v5 files; v1–v4 imports/default v4 exports remain compatible and
the original app explicitly rejects v5. Concert rig details remain visual and inside
the footprint. Validate geometry/guards/persistence/undo in cloud; locally test fields,
quarter turns, small rooms, conflicts, appearance, save/load/replay and both hazards.
This is the user-requested extension to M10, not implementation of M11.

After M10 delivery, the user requested a performance fix using M10 as the base,
including GPU rendering, in response to a slow furnished Editor scene. This fix
iteration adds exact CPU optimizations and phase profiling, GPU-instanced volumetric
crowd/outline/ring batches with compatibility fallback, and a valid dense comparison
fixture. Preserve all M10 features and physical behavior. Gate it on unchanged
regressions/traces, paired baseline state hashes and honest CPU timing evidence;
locally verify actual shaders/instancing/picking/POV/recording/FPS on the user's
hardware. This fixes the current stage before M11 and does not implement M11 or
imply GPU simulation, Jobs/Burst acceleration or a guaranteed frame rate.

### M11 — Complete replacement acceptance and distribution

- Capability: all supported workflows in an independent Unity standalone application.
- Features: integrated startup/quit, examples, docs, notices, build configuration and downloads.
- Architecture: production consolidation; preserve original reference and comparison tools.
- Automated: full reference/portable suites, traces, files, recording and packaging;
  actual Unity tests/builds only where an Editor exists.
- Local: Editor and agreed desktop player acceptance from authoring through both hazards,
  replay, files, cameras, diagnostics and recording.
- Complete when: every parity entry is verified or an explicit difference approved,
  no blocking regressions remain, and the player needs no original .NET executable.

## Delivery and acceptance

Implement exactly one approved milestone per task. Continue through implementation,
available validation and a single milestone-level commit/push. Update STATUS,
FEATURE_PARITY and VALIDATION. Deliver immutable commit/download links, exact scene,
numbered local checks, expected results, limitations and unexecuted Unity checks.
Stop for user testing and fix problems before advancing. Major discoveries require
explaining the necessary roadmap change; routine choices do not require reapproval.
Never access the user's computer. Never merge or push main without a separate request.

## Outstanding operational choices

Confirm local OS and the proposed Windows x64/Mono player target before target-specific
work. Hardware is useful before setting performance gates. Actual Unity compilation,
Mono numeric behavior, package resolution and recording integration remain checks,
not assumptions. IL2CPP or additional platform acceptance needs an explicit target.


## Ongoing visual design requirement (M8 authorization)

After accepting M7, the user requested user-centred GUI colours and typography in
subsequent steps. Apply the shared DESIGN.md conventions throughout M8–M11:
bundled readable type, consistent labelled states, sufficient text contrast, visible
focus and intuitive navigation. M8 includes these refinements with humanoid/camera
presentation. Milestone ownership and one-milestone acceptance gates are unchanged.


## M9 authorized architectural theme

After accepting M8, the user requested black fonts, off-white square/circular pillar
assets with black outlines, and an off-white gridded simulation floor. M9 applies
that presentation change with the diagnostic/scale work. Physical blockage geometry,
room containment, simulation ordering and the remaining M10/M11 gates are unchanged.
