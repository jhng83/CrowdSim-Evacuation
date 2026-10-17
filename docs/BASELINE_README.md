# Historical notes from the uploaded project

These notes describe earlier revisions. See the current README and UI verification report for this release.

# Free-angle barricades and an agent running-speed ceiling

Base: `CrowdSimCs_Barricade_AssetDropdown.zip`, the latest barricade/dropdown revision.

- **ASSETS > Orange barricade** now offers an angle slider, exact numeric input and
  -15/+15 degree buttons. Hover the canvas: **R** adds 15 degrees; **Shift+R** subtracts
  15 degrees. Fractional angles such as 37.5 degrees are supported. Existing placed
  objects are not edited. Tables/stages keep their previous quarter-turn controls.
- **BEHAVIOUR TUNING > Max agent running speed (m/s)** sets a voluntary locomotion
  ceiling from **0.5 to 6.0 m/s**, before finalising. Ctrl+click the slider to type.
  Default: approximately **4.05 m/s**. This is not a target pace or playback control.
  Slower individuals remain slower; injuries, fumes, congestion and waiting still apply.
- The actual rotated 1.5 x 0.4 m base is used for placement, collision, navigation,
  footprint previews and parallel end snapping. Orange appearance and 1 m height remain.
- Both scenarios retain natural navigation, the light-grey floor, agent outlines,
  movable/destructible unoccupied tables, stage limit and existing speed-of-playback control.

See [the current guide](docs/FREE_ANGLE_BARRICADES_AND_SPEED.md) and
[verification report](docs/BARRICADE_ANGLES_SPEED_VERIFICATION.md).
No .NET SDK is installed in the delivery environment: **the C# build, xUnit execution,
OpenGL appearance and runtime latency have NOT been verified here**.

Extract into a new folder, then run from the directory containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Rebuild before using `--no-build`. All package versions remain unchanged.

---

## Historical revision notes (superseded where they describe quarter-turn barricades)

# Flush block placement, orange barricades and one asset picker

This update uses the uploaded **`CrowdSimCs(9).zip`** as its base. It fixes touching square
blocks, adds a rotatable **1.5 m long x 1 m high** orange barricade (assumed width **0.4 m**),
and groups all block/furniture assets in one dropdown with relevant rotation controls.
The movable tables, 15 x 6 m stage limit, natural navigation, grey floor and agent outlines remain.

See [the current asset guide](docs/BARRICADE_AND_ASSET_PICKER.md) and
[verification scope](docs/BARRICADE_ASSET_VERIFICATION.md). Compilation, xUnit execution,
OpenGL appearance and latency are **not verified in the delivery environment**: no .NET SDK
is installed. Source/reference checks are not a substitute for a C# build or application run.

Extract to a new folder and run these commands from the directory containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Choose an item under **ASSETS**, use **Rotate 90 deg (R)** where shown, and click to place.
Barricade end-to-end snapping is enabled by default; it can be disabled for free positioning.
Rebuild before using `--no-build`. No NuGet versions changed.

---

## Historical revision notes (retained below)

# Movable tables, enlarged stage and asset-test correction

This revision uses `CrowdSimCs_Assets_Visibility.zip` as its base. The **3 m x 1 m table**
is now blast-movable and destructible, without attached chairs or people. The stage is
**15 m x 6 m**, remains fixed/non-walkable, and is still limited to **one per scenario**.
The light-grey floor, black agent outlines and shared natural-navigation recovery remain.

The previous asset test omitted an exit, so `SetRunning(true)` correctly refused to start.
The replacement test creates an exit, checks that the run and explosion started, then
verifies stage stability and table destruction after exactly 30 simulation steps.

Extract into a new folder and run from the directory containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Do not test with `--no-build` until this updated source has been rebuilt. Existing Release
DLLs otherwise still contain the old tests and behaviour.

See `docs/FIXED_ASSETS_AND_VISIBILITY.md` for modelling and saved-layout compatibility,
and `docs/MOVABLE_TABLES_STAGE_VERIFICATION.md` for the exact validation scope.
Compilation, xUnit execution, OpenGL appearance and latency are **not verified here**:
the delivery environment has no .NET SDK. No NuGet/package versions were changed.

---

## Shared natural navigation: Explosion + Toxic Fume

This revision extends the previously working Explosion recovery to Toxic Fume. Both now
use persistent progress/loop memory, bounded detours, failed-solution memory and queued
recovery. Fume-specific checks prefer less contaminated local alternatives and reconsider
an exit intention when the perceived plume worsens. No new NuGet dependencies were added.

See [docs/NATURAL_NAVIGATION.md](docs/NATURAL_NAVIGATION.md) for behavior, settings and the
600-agent paired benchmark, including the new `ToxicFume` benchmark argument.

**Verification:** the changes have been source-reviewed but not compiled, run or benchmarked
in this delivery environment; no .NET SDK is installed and its download was unavailable.
See [docs/TOXIC_NATURAL_NAVIGATION_QA.md](docs/TOXIC_NATURAL_NAVIGATION_QA.md). The user's
successful run of the previous Explosion ZIP is not a runtime test of this new revision.
No FPS, guaranteed escape, or empirical human-behavior validation is claimed.

## Previous revision: adaptive Explosion navigation

Natural navigation is now enabled by default for explosion evacuation: persistent progress
memory, bounded table/obstacle recovery, deliberate temporary retreat, event-driven rerouting
and queued work for approximately 600 agents. See [docs/NATURAL_NAVIGATION.md](docs/NATURAL_NAVIGATION.md)
for behaviour, settings, verification commands and the new paired 600-agent benchmark.

**Verification:** this revision has not been compiled, executed or benchmarked in the delivery
environment because no .NET SDK was available. Static checks are recorded in
[docs/NATURAL_NAVIGATION_QA.md](docs/NATURAL_NAVIGATION_QA.md). Older claims or measurements below
refer to prior revisions and do not validate this update. No FPS guarantee is made.

# Furniture update

See [the furniture guide](docs/FURNITURE_UPDATE.md) for features, modelling bounds, run steps and verification status.

## Scenario workflow correction

The left panel now keeps **Save Scenario**, **Replay from Scene Start**, and
**Exit to Scenario Selection** at the top. Replay restores the checkpoint captured when
**Finalise Scene** was pressed: frame 0, paused, no released hazard, original occupant starts,
and intact occupied furniture. Exiting returns to a fresh Explosion/Toxic Fume selection screen;
unsaved authored changes require confirmation.

New saves use `%LOCALAPPDATA%\CrowdSimCs\scenarios` on Windows and are written atomically.
The saved-scenario list and loader still recognize JSON files in the former working-directory
`scenarios` folder.

# Panic/SFM and performance correction candidate

This revision corrects panic-driven movement/contact and reduces repeated CPU work. **Build, regression tests and no-latency-regression acceptance are pending on a .NET/Windows machine.**

Start with `run_release.cmd`. Run `scripts/compare-performance.ps1` for the before/after checks. Read [the change and validation notes](docs/PANIC_SFM_PERFORMANCE_FIX.md) before interpreting performance or simulation outcomes.

The original project documentation follows as history; the note above supersedes old integration and validation claims.

---

# CrowdSim Metric FIX11 - 3D Emergency Crowd Behaviour + SFM

A VS Code-ready .NET 8 C# port of the validated Python crowd-evacuation simulator, with a single 3D OpenGL/ImGui frontend.

The validated Python-derived numerical and collision guardrails remain in `CrowdSim.Core`, but the emergency **decision/navigation layer has been rebuilt** so panic is no longer the master switch for every pedestrian. The current model combines bounded acute stress, heterogeneous response intent, social/behavioural awareness, body-clear static routing, the Social Force Model (SFM), exit-flow controls, wall-aware event effects, casualties and finite-value safeguards.

The retained event/crowd interaction layers are:

- **Stage 1:** rapidly expanding translucent wall-clipped shock front + outward-moving physical debris only;
- **Stage 2:** direct startle/reaction on front exposure, social panic and coherent running-crowd panic contagion feeding the existing panic-induced SFM;
- **Stage 3A:** fallen/casualty avoidance and naturally emerging local congestion;
- **Stage 3B:** dynamic exit rerouting with commitment/hysteresis to avoid route thrashing;
- **Stage 3C:** temporary wall-aware visibility degradation affecting perception, herding and route uncertainty.

Agent POV remains available for inspection/following, but there are **no explosion-specific POV effects** such as flash, camera shake or smoke overlay.

## Presentation

### Blocky low-poly humanoid model (FIX11)

FIX11 changes the render-only pedestrian model to a clean checkpoint-style blocky humanoid: rectangular torso, square/block head, long slim straight arms and legs, and simple block feet. The same design language is used for standing/walking, injured, crawling, helping/kneeling and prone casualty states. Walking/crawl/assistance animation remains render-only and state colours are unchanged.

The metric renderer also corrects a leftover legacy safety clamp in `DrawSegmentPrism`: the old `0.2` minimum half-thickness is now converted through `LegacyRenderUnitMeters` instead of being interpreted as `0.20 m`. This prevents nominally slim limbs from being forced to approximately `0.40 m` total thickness. The normal-detail upright model uses fewer articulated limb segments than before, while the existing reduced/far LOD safeguards remain active.

### Assistance diagnostics (FIX7)

FIX7 makes the existing casualty-assistance phases observable without changing the assistance decision or movement rules. Assigned helpers show **GOING TO HELP** while approaching; the white medical cross, **HELPING** label and crouched pose are reserved for the bounded attendance phase. Selecting a helper shows target distance, total assistance assignment time, time since the last meaningful 0.05 m approach improvement, and the remaining attendance time. Metrics separately report **Going to assist** and **Currently helping**.

- one OpenGL application only; no OpenCV project;
- projected 3D world with raised walls and simple checkpoint-style humanoids;
- Dear ImGui controls instead of keypad shortcuts;
- UI camera controls, follow camera and head-height Agent POV;
- humanoid body colour encodes state only; panic is a separate red/orange ground ring;
- UI-driven scene building, run/pause, event placement and agent selection;
- render-only SFM/avoidance/herding/vision/congestion/trail overlays.


## Metric world scale

The simulation world now uses **metres** for ground-plane geometry instead of the legacy pixel-like world units. The default room is **56.0 m x 34.0 m**; every standing humanoid is fixed at **1.70 m** high with a **0.20 m** body radius; the builder grid is **1.20 m**; and the boundary wall thickness is **0.60 m**. Room dimensions, exits, square/circular blockages, spawn locations, route waypoints, vision/congestion ranges, blast radii and other world distances are stored/displayed in metres.

The Builder also includes a **Circular Blockage** tool that places a true **2.50 m diameter (1.25 m radius)** solid cylinder. It participates in collision, SFM obstacle repulsion, line-of-sight/shielding, A* body-clear routing, undo, rendering and scenario save/load. Legacy scenario version 1 files are upgraded on load using the former 42-unit humanoid height as the scale reference (`1.70 / 42` metres per legacy unit); new saves use scenario version 2.

Pedestrian/debris velocities remain expressed as **metres per simulation step** in this compatibility conversion because the baseline integrator advances position once per step without multiplying position by `Dt`. Their numerical values were scaled with the geometry so motion proportions remain close to the baseline. UI/framebuffer dimensions and video resolution remain pixels because they are display-space, not simulation-space.

## Projects

```text
CrowdSim.sln
|
|-- src/
|   |-- CrowdSim.Core/
|   |   |-- Configuration/
|   |   |-- Geometry/
|   |   |-- Model/
|   |   |-- Systems/
|   |   |   |-- EvacuationSystem.cs
|   |   |   |-- ExplosionSystem.cs
|   |   |   |-- SfmEngine.cs
|   |   |   |-- VisibilityGraphPlanner.cs
|   |   |   `-- VisibilitySystem.cs
|   |   |-- Simulation.cs
|   |   |-- Simulation.Step.cs
|   |   `-- Simulation.Collisions.cs
|   |
|   `-- CrowdSim.OpenGL/
|       |-- Rendering/
|       |   |-- Camera3D.cs
|       |   `-- WorldRenderer.cs
|       |-- UI/
|       |   |-- CaretNumberEditor.cs
|       |   |-- InteractionModes.cs
|       |   |-- RenderDebugOptions.cs
|       |   |-- SimulationUi.Layout.cs
|       |   `-- SimulationUi.cs
|       |-- CrowdSimApplication.cs
|       `-- Program.cs
|
|-- tests/
|   `-- CrowdSim.Tests/
|
|-- docs/
|   |-- ARCHITECTURE.md
|   |-- EXPLOSION_PANIC_STAGE3.md
|   |-- GUARDRAIL_AUDIT.md
|   |-- PORT_MAPPING.md
|   `-- QA_REPORT.md
|
|-- Directory.Build.props
|-- Directory.Packages.props
`-- run_instructions.md
```

## Quick run

From the folder containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln --force --disable-parallel --no-cache
dotnet build .\CrowdSim.sln -c Debug
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj
```

Optional deterministic startup seed:

```powershell
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -- --seed 42
```

Population is now created spatially in Builder mode with the mouse rather than by typing a global agent count.

## Behaviour, panic and movement physics

Panic is still physical rather than cosmetic, but it is now **one bounded acute-stress modifier** instead of the single evacuation switch. `EvacuationSystem.UpdatePanic()` combines direct event exposure, finite social alarm sources and coherent alarmed-running cues, then applies time-based decay. Low residual panic cannot become a new contagion source after the event-alarm window expires.

After the orient/startle period, each mobile agent receives a bounded behavioural intent such as immediate threat escape, purposeful evacuation, following an obvious crowd response, or briefly attending a nearby casualty. `SfmEngine.ComputeForces()` remains the local locomotion/contact engine. Static blockage navigation is supplied by a body-clear A* visibility graph so agents choose a viable side of an obstacle before SFM handles local people, contacts and congestion.

## Important validation boundary

The original Python-derived SFM/guardrails are retained as the behavioural baseline. The newly added running-crowd contagion, normalized debris severity, visibility field and rerouting weights are **simulation assumptions that should be calibrated/validated for any research claim**. The event model deliberately does not implement real explosive-yield, fragmentation or medical injury calculations.

See `docs/EXPLOSION_PANIC_STAGE3.md` for the detailed interaction model and `docs/GUARDRAIL_AUDIT.md` for the safeguard mapping.

## FIX8 - right-side blast-zone panel

The existing left sidebar is retained unchanged. A separate compact `BLAST ZONES` window is anchored to the top-right unused black area of the default 3D view. It provides visibility toggles and editable normalized radii for Kill, Incapacitation, Fallen/Severe, Injury, and Panic/Threat zones. The five radii are strictly ordered by guardrail logic and lock after detonation so the persistent wall-clipped analysis overlays remain consistent with the physics used for that event.

## FIX9 - Alarm indicator

Agents in the existing bounded `WaitingToReact` startle/freeze state now show a momentary red `!` above the head. The cue disappears automatically when the agent transitions to evacuation/injury or another state. This is render-only and does not alter panic/SFM behaviour.


## FIX10 - Exit congestion anti-banging

The exit flow now uses one setback per congestion episode, panic-dependent yielding with staggered retries, and high-panic persistent pressure without repeated bounce. The alarm `!` is approximately twice as thick as before. See `docs/EXIT_CONGESTION_ANTI_BANGING.md`.

## FIX11 - Environment-aware exploration + purple congestion

Stuck evacuating/injured agents no longer choose a random wall tangent as their only exploration strategy. When direct exit line-of-sight is unavailable they probe multiple nearby wall-clear temporary waypoints, prefer candidates that recover exit visibility, favour open/less-crowded space, and commit briefly before replanning to avoid left/right spinning. If the exit becomes directly visible again, the temporary exploration target is dropped immediately and normal evacuation resumes.

The optional congestion heat overlay is now purple so it remains visually distinct from the red/orange individual panic ring. Panic-ring behaviour and thresholds are unchanged.

See `docs/FIX11_EXPLORATION_PURPLE_CONGESTION.md`.

## FIX12 - Body-clear navigation + panic stability

Wall-end recovery now separates visual line-of-sight from physical traversability. Navigation checks the agent's body radius against inflated wall geometry, so seeing an exit no longer cancels exploration while the body still clips a wall endpoint. The stuck detector now measures net translation over a short window rather than tiny frame-to-frame distance jitter.

Social panic is also stabilised: residual panic below a source threshold does not transmit, and social contagion acts on the difference between a meaningfully more-panicked source and the observer. Calm remaining evacuees therefore decay toward zero instead of recursively re-amplifying one another, while genuine higher-panic neighbours, direct threat, and coherent running-crowd contagion can still raise panic.

Purple congestion heat from FIX11 is retained. See `docs/FIX12_BODY_CLEAR_NAV_PANIC_STABILITY.md`.

## FIX13 - Bounded active-alarm panic propagation

FIX13 restores meaningful visible/behavioural panic after a real blast without reintroducing the late residual-panic feedback loop. Direct shock/debris exposure now starts a finite active-alarm source window; genuinely alarmed visible agents and coherent alarmed runners can relay that event for a shorter bounded window. Residual panic after the window expires continues to decay but no longer acts as a contagion source.

A relayed agent can transmit a given event episode only once, so the same alarm cannot bounce indefinitely between the last few evacuees. Fast calm evacuees also no longer count as panic-running sources. FIX12 body-clear navigation and the purple congestion overlay are retained unchanged.

See `docs/FIX13_ACTIVE_ALARM_PANIC_PROPAGATION.md`.

## FIX14: alarm event drives reaction

FIX14 corrects the mismatch between a valid social/running alarm relay and the independent numeric panic activation threshold. When a wandering agent successfully receives a finite alarm episode, it now enters the existing `WaitingToReact` sequence immediately, shows the existing brief alarm indicator, and later transitions into evacuation according to its reaction delay. Panic magnitude remains bounded and continues to control SFM urgency/speed. The one-relay-per-event guardrail from FIX13 remains unchanged, so expired residual panic cannot restart the same event episode.

Regression coverage now includes below-threshold relay reaction and multi-hop alarm propagation. Expected test total: **34**.

## FIX15 - Panic/awareness split + evacuation urgency + scenario save/load

FIX15 restores a stronger but still finite event-driven panic wave while retaining the one-relay-per-event protection against late residual re-panic. Green evacuating/injured agents now retain a modest 1.65x minimum evacuation-urgency speed multiplier; panic can raise desired speed further through the existing panic-influenced SFM.

A separate **evacuation-awareness** path fixes the case where a white wandering agent remains unaware while surrounded by obvious evacuees. Visible coherent evacuation flow, or an obvious local evacuation crowd, can start the normal reaction sequence without creating a new panic source or panic floor.

The left sidebar also adds **SCENARIO SAVE / LOAD**. Versioned JSON files preserve agent count, random seed, exits, blockages, room dimensions and the five blast-zone radii. Runtime injuries, panic, positions, elapsed time and debris are intentionally excluded. Loaded scenarios return to Builder mode for review, then use the saved seed when agents are finalised/spawned.

See `docs/FIX15_PANIC_AWARENESS_SCENARIO_SAVE_LOAD.md`. Expected regression total: **40 tests**.

## BEHAVIOUR REFINEMENT 1 - bounded human variability and stress-dependent spacing

This revision preserves Behaviour Rebuild 1 and adds a Builder-mode **BEHAVIOUR TUNING** panel. Seed-reproducible agents now vary in stress recovery and preferred personal space as well as urgency, reaction time, following, helping and route familiarity. Stress decay uses individual recovery bias; panic compresses social spacing without reducing physical body/contact protection; emergency urgency remains bounded by the existing speed guardrails. Behaviour tuning is saved and restored with scenario JSON.

See `docs/BEHAVIOR_REFINEMENT_1.md`. Expected regression total: **54 tests**.

## BEHAVIOUR REBUILD 1 - heterogeneous response + body-clear A* routing

This revision supersedes the FIX11-FIX15 patch stack as the active emergency-behaviour layer; those sections remain below as revision history. It does **not** copy FIX9 behaviour.

- Directly exposed mobile survivors complete the existing bounded orient/startle period and then briefly create distance from the immediate event before transitioning toward safety.
- Indirectly alerted pedestrians can purposefully evacuate, follow a coherent visible crowd response, or (for a bounded high-helping/low-acute-stress subset) briefly attend a nearby casualty. Assistance does not alter the casualty medical state.
- Stable, seed-reproducible abstract traits vary threat sensitivity, following tendency, helping tendency, route familiarity and movement urgency without using demographic stereotypes.
- Acute panic rises from current event/social cues and decays using an 8 s time constant. Finite alarm-source windows and one relay per event prevent residual late re-panic loops.
- Purposeful evacuation remains brisk at low panic; panic can increase urgency further but is not required for an agent to evacuate.
- Static blockage routing uses agent-radius-aware A* visibility-graph waypoints. Legacy wall-follow/exploration remains only as a rare fallback, so ordinary purposeful agents should not accumulate indefinitely at blockage corners.
- Dynamic contacts, casualties, congestion, wall collision, exit capacity and anti-banging behaviour remain under SFM/guardrail control.
- Scenario Save/Load from FIX15 is retained unchanged.

Selected-agent diagnostics now expose behaviour intent, active-alarm status, current route waypoint, stuck time and the existing panic/awareness signals.

See `docs/BEHAVIOR_REBUILD_1.md`. Expected regression total after Behaviour Refinement 1: **54 tests**.

## BEHAVIOUR REFINEMENT 2 - bottleneck pressure, gap seeking and bounded pressing

This revision preserves Behaviour Refinement 1 and adds a local doorway-interaction model that is separate from panic. Density, delayed progress, doorway load, proximity and stable individual assertiveness/patience generate bounded `BottleneckPressure`. Eligible pedestrians temporarily choose `Yield`, `SeekGap` or `Press` responses with hysteresis, while open/uncongested movement remains unchanged.

`SeekGap` uses body-clear lateral probes to merge toward the more open side. `Press` maintains stronger forward intent and reduces social comfort spacing, but never changes the physical body radius, bypasses exit capacity, disables force/collision guards, or recreates the old forward/backward exit banging. Crawlers always yield. The existing one-setback congestion episode remains authoritative.

The Builder **BEHAVIOUR TUNING** panel now exposes bottleneck assertiveness, pressure sensitivity and pressure-related social-space compression. These settings are included in Scenario Save/Load JSON. Selected-agent diagnostics show current mode, pressure and doorway wait.

See `docs/BEHAVIOR_REFINEMENT_2_BOTTLENECK_PRESSURE.md`. Expected regression total: **60 tests**.

## UI REFACTOR 1 - docked operator dashboard and caret-based numeric editing

The application now uses a stable docked operator layout: a top status bar, mode-aware left
control panel, reserved centre 3D viewport, tabbed right analysis inspector and bottom active-tool
status bar. The previous floating blast-zone window no longer covers the grid.

The right inspector contains `BLAST`, `AGENT`, `METRICS` and `DEBUG` tabs. Blast radii use normal
text fields rather than `DragFloat`: a single click places the blinking insertion caret instead
of selecting the complete number. Enter or focus loss commits a valid value, Escape restores the
previous value, and invalid values retain the last valid configuration while showing an error.

Scenario configuration changes now display an unsaved-change marker. No panic, SFM, routing,
collision, blast, bottleneck or exit-flow behaviour was changed in this UI-only revision.

See `docs/UI_REFACTOR_1.md`. The existing regression total remains **60 tests**.

## UI REFACTOR 2 - readable tuning controls, restart, recording and exit capture

Behaviour and bottleneck tuning labels now appear above full-width sliders so their complete names remain visible in the narrow left panel.

Runtime controls add **Restart from Beginning**, which rebuilds the current scenario at frame 0 with the same static configuration and random seed. The restart preserves population, exits, blockages, blast-zone radii and behaviour tuning while clearing runtime positions, injuries, panic, debris, elapsed time and exit counts.

Before starting a run, **Record this run** can be armed. The complete application framebuffer is recorded to a fixed 1280x720, 20 FPS Motion-JPEG AVI under `recordings/`. Recording starts with **Start Simulation** and closes safely on Pause, Stop Recording, Restart, Load, New Scenario or application exit.

Final doorway approach now uses body-clear exit capture and stable lane targets for wide exits. Once an agent has a valid final route, obsolete herding is suppressed, bottleneck gap/press behavior stays directed toward the doorway, and SFM/contact/exit-capacity guardrails remain authoritative. This prevents herds from orbiting near an exit or peeling away immediately before crossing.

See `docs/UI_REFACTOR_2_RUNTIME_RECORDING_EXIT_CAPTURE.md`. The existing simulation regression total remains **60 tests**.


## Visual update

- **Animated low-poly humanoids:** articulated arms/legs use actual simulated speed to blend standing, walking, running and crawling presentation without feeding animation back into physics.

## PERFORMANCE PASS 1 - CPU hot-path and animated-render optimization

This revision keeps the animated low-poly humanoids and the validated crowd model while reducing CPU work. SFM candidates now come from the existing spatial hash; panic/collision/neighbour paths reuse scratch buffers; persistent wall-clipped blast geometry and wall chunks are cached; congestion rendering reuses the simulation's local-congestion value; and humanoid rendering uses viewport culling, screen-size LOD and allocation-free fixed face buffers. Normal overview LOD still has moving arms and legs, while close/zoomed views retain the full articulated model.

Recording now moves scaling/MJPEG encoding/AVI writing to a bounded background queue so the render/simulation thread is not responsible for video compression. The right **DEBUG** tab also includes a `PERFORMANCE` section for frame, physics, world-render, UI, recording, allocation and visible-agent diagnostics.

See `docs/PERFORMANCE_PASS_1.md`. Existing regression target remains **60 tests**.


## BUILDER PASS 1 - historical variable-room/spatial-placement pass

The Builder now treats population layout as part of scenario design rather than a single typed count.
Before the first placement, **Room width** and **Room length** can be varied from the left panel. The
room size then locks once an exit, obstacle or population group has been placed so existing
geometry cannot be silently clipped by a resize.

That historical pass introduced a separate Blue Wall tool. The current builder has retired that button:
use the normal `2 x 2` blockage instead. Legacy saved `BlueWalls` are retained only for backward-compatible loading.

Population is placed by region using **Place 1 Agent**, **Place 5 Agents**, **Place 15 Agents** or
**Place 50 Agents**, followed by a click in the world. The clicked point is the preferred region centre;
a deterministic outward search fills nearby free floor, skips boundary/obstacle/blue-wall clearance and
already planned pedestrians, and continues around partially blocked areas instead of stacking bodies
inside geometry. Exact builder start positions are shown as cyan markers and are reproduced by Scenario
Save/Load and **Restart from Beginning**. Runtime-moved positions remain transient and are not saved.

The old typed population field and `--agents` startup argument are removed. Legacy saved scenarios that
contain only an `AgentCount` remain loadable and use their prior deterministic random-spawn fallback until
the user replaces the population with mouse-planned groups.

See `docs/BUILDER_PASS_1_ROOM_WALLS_SPATIAL_AGENTS.md`. Expected regression target: **66 tests**.


## BUILDER PASS 2 - grouped placement controls

Builder placement controls are now grouped as **option + action** instead of one button per variant.

- **EXIT**: choose `1-person`, `3-person` or `5-person` from a dropdown, then press `Place Exit` and click a boundary wall.
- **BLOCKAGE**: choose `Square - 1 x 1`, `Square - 2 x 2`, or **Circular - Ø 2.5 m** from one dropdown, then press the single **Place Blockage** button. All choices are true solid geometry for collision and routing.
- **AGENTS**: choose `1`, `5`, `15` or `50` agents from a dropdown, then press `Place Agent(s)` and click the desired region. Existing logical spreading around walls, blockages and already planned agents is unchanged.
- The separate **BLUE WALL** placement tool was retired in the current cleanup pass; use the Blockage `2 x 2` option. Legacy saved BlueWalls still load correctly.

The active placement preview uses the selected blockage size, selected exit width and selected agent-group count. Existing scenario JSON remains compatible because placed geometry and exact planned start positions are stored, not the temporary dropdown selection.

See `docs/BUILDER_PASS_2_GROUPED_PLACEMENT_CONTROLS.md`. Current regression source count: **78 tests** (including metric/circular-blockage and slow-crawler bypass coverage).

## PERFORMANCE PASS 2 + BUILDER CLEANUP

The current branch removes the redundant Blue Wall placement button, adds **Ctrl+Z** for Builder undo, and applies a second performance pass directly to the latest builder branch. The simulation now reuses spatial-agent buckets/scratch buffers, caches the static visibility graph, avoids repeated all-agent exit/debris/exploration scans, spatially culls SFM wall work in obstacle-heavy layouts, and uses wall-layout versioning for render/navigation cache invalidation. The animated renderer also uses adaptive low-cost articulated overview figures under load while keeping selected/followed agents detailed. No fixed-timestep or crowd-behaviour fidelity setting was reduced. See `docs/PERFORMANCE_PASS_2_BUILDER_CLEANUP.md`.


## FIX3 - humanoid visual restoration + UI clarity

- Metric render-motion thresholds now use the same legacy-to-metre scale as the world, restoring visible walking/crawling animation instead of treating normal metric motion as nearly stationary.
- Normal overview agents preferentially use faceted 3D low-poly models. Under heavy render load/population, the cheaper screen-space LOD remains available but is now faceted/shaded and keeps animated arms, legs and feet. This protects frame rate without reverting to a flat stick-figure look.
- Builder **Ctrl+Z** uses explicit key-down edge detection so one key press reliably maps to one Undo Last Placement operation.
- The blockage workflow is consolidated into one shape/size dropdown plus one **Place Blockage** action. The circular Ø 2.5 m option is no longer a separate placement button.
- **Place Explosion** is yellow while available/idle so the key scenario trigger is easy to discover, and changes to orange/red while armed.

UI changes should continue to favor discoverability, clear grouping, and visible system state without changing crowd physics unless explicitly requested.


## FIX8 assistance approach guardrails

- Helpers now approach one of two opposite, body-clear casualty-side positions instead of the casualty centre.
- Two helpers assigned to the same casualty reserve different sides.
- After 2 seconds without meaningful progress toward its assistance position, the helper clears/replans its static route. Sideways shuffling does not reset this timer.
- After 6 seconds without progress, it tries the alternate free side.
- After 10 seconds without progress, it abandons the assignment and resumes evacuation so assistance cannot remain stuck indefinitely.
- The 2-second HELPING timer begins only after the helper actually reaches its valid assistance position.
- FIX7 diagnostics remain visible, with the selected-agent inspector also showing the assistance side, recovery stage and give-up guard.


## FIX9 assistance no-progress correction

FIX9 corrects the FIX8 helper recovery guardrail. Only a meaningful reduction in distance to the current assistance position refreshes the no-progress clock. Lateral shuffling or oscillation while blocked no longer keeps an agent in `AssistCasualty` indefinitely. When the 6-second alternate-side recovery changes the goal point, a new distance baseline is established without resetting the existing no-progress timer; only subsequent real approach progress can reset it.


## METRIC FIX10 - slow crawler bypass

- Yellow `Crawling` casualties remain physical SFM/collision obstacles and can still communicate finite panic/alarm cues, but they are no longer used as normal movement leaders for herding or `FollowCrowd` direction.
- A mobile green/blue pedestrian that remains directly behind a materially slower crawler for a short observation window can choose a clear left/right bypass point when adjacent floor space is genuinely usable.
- The chosen side is briefly committed to avoid left/right thrashing; static walls/blockages, nearby casualties, SFM and hard collision remain authoritative.
- Final doorway capture and assistance retain priority over the ordinary slow-crawler bypass. During explosion `ThreatEscape`, a dedicated casualty-cluster detour may temporarily override the radial heading when the forward sector is blocked.
- The selected-agent inspector shows crawler observation/bypass state for verification.

See `docs/METRIC_FIX10_SLOW_CRAWLER_BYPASS.md`. Current regression source count: **78 tests**.

## Explosion and Toxic Fume scenarios

The application now opens with a scenario chooser. **Explosion** preserves the existing blast/shock/debris workflow. **Toxic Fume** adds a separate obstacle-aware normalized plume scenario while keeping the same SFM, panic/social contagion, navigation, crowd metrics, recording, and camera systems.

Toxic Fume propagation is precomputed in Builder mode and replayed from a compact arrival-time/peak-concentration cache during the live simulation. This avoids running a dispersion solver alongside the pedestrian physics. The Builder includes release-point placement, plume configuration, a **Compute Fume Dispersion** action, a timeline preview, cache/precompute diagnostics, and automatic cache invalidation after geometry/configuration changes.

See `docs/TOXIC_FUME_SCENARIO.md` for the model boundary, workflow, performance architecture, and metrics. Concentration/exposure values are normalized research stimuli and are not calibrated to a named chemical or CFD model.


## EXPLOSION CASUALTY-CLUSTER BYPASS

Explosion `ThreatEscape` no longer suppresses all casualty bypass behavior. If a crawling, fallen, incapacitated, temporary-incapacitated or dead body occupies the forward escape corridor, the agent evaluates a small deterministic set of nearby body-clear detours. Forward/lateral options remain preferred; a +/-120-degree back-side step is allowed only when clearer options are unavailable, and radial backtracking toward the blast is capped at 0.45 m. The selected detour is committed for 1.2 s to prevent left/right oscillation, after which the normal threat-escape objective resumes. SFM and hard collision remain authoritative for the actual motion.

Two regression cases cover casualty-cluster lateral detouring and bounded back-side stepping. Those cases remain in the current suite; the latest local-navigation recovery additions bring the current expected xUnit case count to **102**.

## Local navigation recovery - circular follow and casualty wait/press

Visual QA of the Explosion scenario identified two remaining local deadlocks: an evacuating pedestrian could repeatedly drive into a circular blockage, and casualty clusters could cause repeated left/right bypass oscillation. This revision adds runtime-only `LocalNavigationMode` guidance while keeping the existing `AgentState`, SFM, panic, routing and collision model authoritative.

Circular blockages now trigger a short committed tangent-follow response (`CircleFollow`) until the original route becomes clear. Casualty blockage now follows `CasualtyBypass -> WaitingForGap -> SeekingGap -> PressingThrough`. Controlled pressing reduces only social/personal-space preference; it never shrinks the physical body radius or disables casualty collision. If no body-clear gap exists, the pedestrian waits and retries instead of penetrating the casualty cluster.

The selected-agent inspector reports the active local-navigation mode, blocker id/blocked time for casualty modes, and clockwise/counter-clockwise side for circular following. See `docs/LOCAL_NAVIGATION_WAIT_FOLLOW.md`. Expected xUnit case count in this revision: **102**.
# Latest: furniture load/replay and latency correction

The supplied 205-occupant scene now loads and replays. Furniture-heavy routing avoids
the usual all-pairs graph rebuild on chair changes, and expensive updates yield back to
rendering. See [the correction notes](docs/FURNITURE_LATENCY_FIX.md) for measurements,
verification limits and Release build instructions.
