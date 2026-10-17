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

# Shared navigation update: Explosion and Toxic Fume

Extract this revision into a new folder. From the folder containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Select **Toxic Fume**, set up the room/furniture/population, place a fume source, and run the
existing plume precomputation workflow before starting/releasing it. Shared natural recovery
is enabled by default. Exposure and permanent incapacitation settings are unchanged.

A verification script stops immediately on a failing restore/build/test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-natural-navigation.ps1 -NavigationOnly
```

To also measure the headless paired 600-agent Toxic Fume workload:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-natural-navigation.ps1 -NavigationOnly -Benchmark -Scenario ToxicFume -Agents 600 -Frames 400
```

The process-local execution-policy flag does not change the machine-wide policy.
Benchmark results exclude graphics/recording and fume precomputation. The code has not been
compiled or benchmarked in this delivery environment; these are local verification commands.
See `docs/NATURAL_NAVIGATION.md` and `docs/TOXIC_NATURAL_NAVIGATION_QA.md`.

---

## Earlier run notes (retained for reference)

## Adaptive explosion navigation - current source revision

Natural navigation is now enabled by default for explosion evacuation: persistent progress
memory, bounded table/obstacle recovery, deliberate temporary retreat, event-driven rerouting
and queued work for approximately 600 agents. See [docs/NATURAL_NAVIGATION.md](docs/NATURAL_NAVIGATION.md)
for behaviour, settings, verification commands and the new paired 600-agent benchmark.

**Verification:** this revision has not been compiled, executed or benchmarked in the delivery
environment because no .NET SDK was available. Static checks are recorded in
[docs/NATURAL_NAVIGATION_QA.md](docs/NATURAL_NAVIGATION_QA.md). Older claims or measurements below
refer to prior revisions and do not validate this update. No FPS guarantee is made.

## Experimental hybrid crowd update

See [docs/HYBRID_1000.md](docs/HYBRID_1000.md) for enabling the new mode, Release build/test commands and the 220/500/1,000-agent benchmark. This revision has not been compiled or benchmarked in the delivery environment. Historical validation statements below apply to earlier revisions only.

## Limited chair chain pushing

See [docs/LIMITED_CHAIR_CHAIN_PUSHING.md](docs/LIMITED_CHAIR_CHAIN_PUSHING.md) for this update.

## Injured walker chair clearance

See [docs/INJURED_CHAIR_CLEARANCE.md](docs/INJURED_CHAIR_CLEARANCE.md) for this update.

## Blast-start CPU update

See [docs/BLAST_CPU_UPDATE.md](docs/BLAST_CPU_UPDATE.md) for performance changes and the new timing readouts.

## Seat-departure and navigation-cost update

See [docs/SEAT_DEPARTURE_AND_NAVIGATION_COST.md](docs/SEAT_DEPARTURE_AND_NAVIGATION_COST.md) for changes and verification steps.

## Open-space detour update

See [docs/OPEN_SPACE_DETOURS.md](docs/OPEN_SPACE_DETOURS.md) for earlier crawler overtaking, temporary detours and verification commands.

## Explosion furniture update

See [docs/FURNITURE_BLAST_UPDATE.md](docs/FURNITURE_BLAST_UPDATE.md) for the new table/chair blast behaviour, verification commands and validation limitations.

# Furniture version

**Latest correction:** load/replay and furniture-related CPU stalls are addressed in
[docs/FURNITURE_LATENCY_FIX.md](docs/FURNITURE_LATENCY_FIX.md), including measured results
and Windows Release build instructions. Use that document for current validation status;
older revision notes below describe historical checks.

Follow [docs/FURNITURE_UPDATE.md](docs/FURNITURE_UPDATE.md) for the new furniture workflow and build/test commands.

The primary lifecycle controls are always at the top of the left panel:

- **Save Scenario** writes the current authored setup to
  `%LOCALAPPDATA%\CrowdSimCs\scenarios` on Windows. The status bar shows the exact path.
- **Replay from Scene Start** returns to the scene captured by **Finalise Scene**, paused at
  frame 0 and ready for **Start Simulation**.
- **Exit to Scenario Selection** returns to the Explosion/Toxic Fume choice page. Unsaved
  authored changes require a second confirmation click.

The original setup instructions follow.

# Updated revision: start here

Use a fresh extraction and run `run_release.cmd` for the optimized Release build. Do not run an old executable from a previous `publish` folder.

To run the regression suite and compare original versus updated CPU performance:

```powershell
./scripts/compare-performance.ps1
```

Results go to `validation/`. The script stops on a test failure and reports latency regressions. See [PANIC_SFM_PERFORMANCE_FIX.md](docs/PANIC_SFM_PERFORMANCE_FIX.md) for the validation limitations and full change details. This candidate has not been compiled or benchmarked in the editing environment.

---

# Run Instructions - VS Code / Windows

## 1. Prerequisites

- Windows 10 or Windows 11
- .NET 8 SDK
- VS Code
- Microsoft C# extension for VS Code
- GPU/driver with desktop OpenGL support

Verify .NET:

```powershell
dotnet --version
```

The version should start with `8.` or a newer SDK capable of targeting `net8.0`.

## 2. Open the correct folder

Extract the ZIP and open the folder that directly contains:

```text
CrowdSim.sln
Directory.Build.props
Directory.Packages.props
src\
tests\
```

Do not open only the `src` folder.

## 3. Restore, build and test

In the VS Code terminal:

```powershell
dotnet restore .\CrowdSim.sln --force --disable-parallel --no-cache
dotnet build .\CrowdSim.sln -c Debug --no-restore
dotnet test .\CrowdSim.sln -c Debug --no-build --no-restore
```

Expected xUnit cases in this revision: **102** (100 `[Fact]` cases plus 2 `[InlineData]` cases). Run the command above to verify them on your machine. The latest local-navigation regressions cover circular-obstacle tangent following, casualty waiting after a failed bypass, and hard body separation during controlled pressing.

## Docked UI and behaviour tuning

The UI reserves the centre for the 3D viewport. Builder/Runtime actions and camera controls are on the left; the first right-side tab is scenario-specific (`BLAST` for Explosion or `FUME` for Toxic Fume), followed by `AGENT`, `METRICS` and `DEBUG`. The top bar shows mode, scenario, outcomes, FPS and speed. The bottom bar shows the active world tool and its instruction.

Before **Finalise Scene**, expand **BEHAVIOUR TUNING** in the left Builder panel. The values are applied when the population is spawned and are included in Scenario Save/Load JSON. In addition to reaction, stress, following, personal-space, urgency, helping and familiarity controls, Behaviour Refinement 2 adds **Bottleneck assertiveness**, **Pressure sensitivity** and **Pressure space compression**.

In the right **BLAST** tab, radius values are normal caret-based text fields. Single-click places the blinking caret; Enter or clicking elsewhere commits; Escape restores the previous value. Double-click or `Ctrl+A` selects the entire value intentionally.

## 4. Run the 3D application

```powershell
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj
```

Optional deterministic random seed:

```powershell
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -- --seed 42
```

There is no `--agents` argument in the current Builder workflow. Population is placed directly in the simulation area with the mouse.

You can also use **Terminal -> Run Task -> run: CrowdSim 3D**.

## 5. Normal workflow

1. In **SCENE BUILDER**, set **Room width (m)** and **Room length (m)** before placing anything. The default room is 56.0 m x 34.0 m. Room size locks after the first exit, blockage or agent group; undo all placements if you need to resize again.
2. Under **EXIT**, select `1-person`, `3-person` or `5-person`, press `Place Exit`, then click close to a room boundary.
3. Under **BLOCKAGE**, choose a square size (default grid spacing 1.20 m) and press **Place Blockage**, or press **Place Circular Blockage (Ø 2.5 m)** for the fixed 1.25 m-radius circular obstacle. Then click clear floor.
4. The separate Blue Wall placement tool remains removed; legacy scenarios containing BlueWalls still load normally. Both square and circular blockages affect collision, line-of-sight and body-clear routing.
5. Under **AGENTS**, select `1`, `5`, `15` or `50` agents from the dropdown, press `Place Agent(s)`, then click where that group should begin. Repeat in different parts of the room as needed. There is no typed total-population field.
6. For multi-agent placement, the clicked location is the preferred centre. The planner spreads starts outward over nearby free floor, skips walls/blockages (including any legacy loaded blue walls) and existing planned agents, and can fill around a partially blocked click region rather than putting people inside geometry. Cyan dots show the exact planned starts.
7. Enter a name under **SCENARIO FILES**, then use the always-visible **Save Scenario** button
   or `Save / Save As`. This saves metric room dimensions, seed, exits, furniture, blockages,
   exact builder starts, blast-zone radii and behaviour settings under
   `%LOCALAPPDATA%\CrowdSimCs\scenarios`. Runtime casualties, panic and moved positions are not saved.
8. To reuse a saved setup, choose it under **Saved scenarios** and press `Load Selected Scenario`. The room, geometry, planned starts, radii and seed return in Builder mode for review.
9. Click `Finalise Scene`. Mouse-planned agents are instantiated at the exact cyan start locations.
10. Optional: before the first start, enable **Record this run**. Recording begins automatically with the simulation and writes a 1280x720, 20 FPS AVI to `recordings\`.
11. Click `Start Simulation`.
12. Click `Place Explosion`, then click clear floor inside the room (solid walls/blockages are rejected).
13. The translucent front propagates, debris moves outward, and direct reactions start only when the front physically reaches an exposed agent.
14. Mobile responders complete a heterogeneous orient/startle delay, then may briefly create distance from the event, purposefully evacuate, follow an obvious crowd response, or briefly attend a nearby casualty. Acute panic is a bounded urgency modifier rather than the sole evacuation switch.
15. Static obstacles, including legacy blue walls loaded from older scenarios, use body-clear A* waypoints; SFM still handles local people, casualty avoidance, contact, congestion and wall forces. At genuine crowded exits, agents may yield, seek a lateral gap or maintain bounded forward pressure according to density, delay, assertiveness and patience. Exit capacity/anti-banging guardrails remain active.
16. Use `Select Agent`, click a humanoid, then open the right **AGENT** tab to inspect behaviour intent, active-alarm status, panic/awareness signals, route waypoint/stuck time, visibility and local congestion.
17. After selecting an agent, use `Follow Selected Agent` for a centred follow camera, or `Enter Agent POV` for a head-height first-person view. POV ends automatically when the agent exits or immediately when you press `Exit Agent POV`.


## Runtime reset and recording

- **Replay from Scene Start** reconstructs the explicit Finalise Scene checkpoint at frame 0 using the same random seed, then pauses it ready for another run.
- **Record this run** is available before the first start. Recording starts automatically with **Start Simulation**.
- **Stop Recording** closes the AVI manually. Pause, Replay, Load, Exit to Scenario Selection and application exit also close it safely.
- Recordings are saved under `recordings\` as Motion-JPEG AVI files at 1280x720 and 20 FPS.

## GPU note

The OpenGL renderer already uses the graphics card. The crowd decision, routing, panic and SFM calculations in `CrowdSim.Core` remain CPU code. Selecting a GPU environment or changing the launch command does not move those calculations to the RTX GPU; a separate compute-kernel implementation would be required.

## Animated low-poly humanoids

Pedestrians now use articulated low-poly limbs. Walk/run animation follows each agent's actual simulated speed: standing agents settle into a neutral pose, normal walkers use a moderate stride, and faster emergency movement increases cadence, stride, knee lift, arm bend and forward body lean. Gait phases are offset per agent so the crowd does not move in lockstep. Crawling agents also receive a small movement-driven crawl cycle. These changes are render-only and do not alter SFM forces, collision radii, routing, panic or exit logic.

## 6. 3D controls

All primary operation is in the UI. No keypad shortcuts are required.

- **Azimuth**: rotates the 3D viewing direction.
- **Elevation**: changes camera height angle.
- **Zoom**: controls orthographic zoom.
- **Reset Camera**: restores the default checkpoint-style perspective.
- **Follow Selected Agent**: keeps the selected agent centred in the elevated 3D view.
- **Enter Agent POV**: switches to a first-person camera at the selected agent head position and follows its movement heading.
- **Exit Agent POV**: leaves first-person mode and returns to the follow view.
- Mouse wheel over the world also zooms.

## 7. Debug visualisation

Open the right **DEBUG** tab, then expand **DEBUG VISUALISATION** to enable render-only overlays:

- desired direction;
- avoidance direction;
- herd/lane direction;
- total SFM force;
- blast direction;
- vision cone;
- congestion heat;
- trajectory trails.

These visualisations never write into simulation state.

## 8. If a build error occurs

Copy the complete `dotnet build .\CrowdSim.sln` output. Compiler errors are more useful than a
screenshot of only the last line because the file, line number and exact API mismatch are included.

## Performance Pass 1 diagnostics

Open the right **DEBUG** tab and use the `PERFORMANCE` section while the scenario is running. It reports frame time, physics time, projected world-draw time, UI time, recording capture time, per-frame render-thread allocation, visible/active agents and (while recording) queue depth/dropped frames.

The normal overview automatically uses a lower-cost articulated humanoid LOD while retaining moving arms/legs. Zoomed/follow/close views restore the full elbow/knee model. High simulation-speed multipliers use a more aggressive visual LOD so CPU time is prioritised toward the fixed-step simulation.

The OpenGL presentation already uses the GPU. No launch/environment switch will move the CPU SFM, routing or panic code onto CUDA; that would require a separate compute implementation.


## Builder Pass 1 notes

- Room dimensions and world geometry are in **metres**, not desktop-window pixels. The default room is **56.0 m x 34.0 m**.
- Every standing agent is **1.70 m** high with a **0.20 m** body radius. The default builder grid is **1.20 m**.
- **Circular Blockage** is fixed at **2.50 m diameter / 1.25 m radius**.
- Display/UI sizes and the 1280x720 recording resolution remain pixels because they are framebuffer measurements, not world geometry.
- Agent region placement is deterministic for the selected click and current layout. A requested 50-person group commits only when all 50 safe non-overlapping starts can be found; otherwise no partial group is added.
- **Undo Last Placement** removes the most recent exit, blockage or agent group. Press **Ctrl+Z** for the same action. Legacy loaded blue-wall placements can still participate in the historical undo stack when applicable. Once every placement has been undone, room resizing becomes available again.
- Builder-defined starting positions are configuration and are saved. Positions after the simulation starts moving are runtime state and are intentionally excluded from scenario JSON.

### Performance Pass 2

The latest branch includes additional CPU/render optimizations for larger mouse-placed populations and obstacle-rich rooms. The Debug -> Performance panel remains the quickest way to distinguish Physics ms from World Draw ms. No simulation timestep or behavioural fidelity setting is reduced by these optimizations.


## FIX11 visual check

After launching, verify the humanoids use the blocky low-poly style (rectangular torso, block head, slim straight limbs) in standing/walking, crawling, helping and prone states. This is a render-only revision; the existing build/test commands are unchanged.

## Toxic Fume scenario workflow

1. Launch the application and choose **Toxic Fume** on the scenario-selection screen.
2. Configure the room, exits, blockages, agent groups, and shared behavior tuning.
3. Open **TOXIC FUME CONFIGURATION** and press **Set Release Point**, then click clear floor in the 3D world.
4. Set the initial cloud radius, dispersion speed, maximum plume extent, release duration, and optional advanced normalized-response settings.
5. Press **Compute Fume Dispersion**. Review the reported precompute time/cache size and drag the preview timeline to inspect the plume.
6. **Finalise Scene** becomes available only when exits, population, release point, and a valid plume cache are present.
7. Press **Start Simulation**. When you want the event to begin, press **Release Toxic Fume**.
8. Use the **FUME**, **AGENT**, **METRICS**, and **DEBUG** tabs to inspect the cached field and live crowd response.

If room geometry, the release point, or fume parameters change, recompute the plume before finalising/running. The fume field is a normalized crowd-response stimulus, not a real-chemical CFD/toxicity model.
