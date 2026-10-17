# Furniture update — implementation and run guide

## What changed

The scene builder now offers **Round table + 10 seated people** and **Single chair + 1 seated person**. Rotation applies to the next placement. A green footprint fits; red means overlap, room-boundary, exit-clearance or population-limit rejection. Click to place; Ctrl+Z removes the entire last set and its planned occupants. The footprint reserves space for chair departure between furniture sets. A table is 1.8 m in diameter, seats are arranged on a 1.5 m radius ring, and each chair has exactly one occupant. Furniture occupants add to the scene's planned population; they are not a separate uncounted visual population.

Finalise Scene instantiates the occupants. Before the event they sit still, remain room occupants and receive normal perception/hazard updates. The selected-agent panel shows posture separately from injury/evacuation state. “Standing - waiting for a gap” explains a congested chair departure.

The always-visible **SESSION** controls now save the authored setup, replay from the exact
Finalise Scene checkpoint, or return to the Explosion/Toxic Fume selection screen. Replay is
paused at frame 0 with the hazard unreleased and furniture intact. Exiting a scene with unsaved
authored changes requires confirmation.

## Architecture and library choices

- **Orthogonal seating state machine:** `Seated -> StandingUp -> Walking` is stored in `Agent.Posture`. Existing `AgentState` continues to own injury, incapacitation, death and evacuation. Existing threat-awareness/reaction decisions start the rise; the existing reaction timer can overlap the rise, but movement still waits for both gates. Terminal/crawling body states cancel seating without being reset to healthy evacuation.
- **Geometry composition:** `FurnitureSet` owns an immutable authored transform, a fixed round-table collision proxy and ten or one chair instances. Builder validation/undo and scenario persistence treat the set atomically. Chairs and tables reuse circular obstacle SFM, contact and navigation checks, so both upright and fallen chairs block pedestrians. A swept body-clear seat-departure check searches behind and beside the chair, waits under congestion, then moves at 0.7 m/s to the chosen gap before releasing into normal movement.
- **Bounded chair dynamics:** the existing normalized blast zones and shock-front timing drive chair destruction, launch and toppling. Walls shield chairs. Gravity, drag, swept wall/table/chair checks, a conservative ground footprint, and a three-second flight bound govern landing. Chair/person impacts reuse the existing normalized debris outcomes and counters at most once per chair; the original occupant is excluded because the blast body rules already handle that person. A hurried departure can topple an otherwise upright chair. Fume does not impart mechanical force to furniture.
- **Existing .NET stack retained:** System.Numerics supplies vectors and deterministic seeded Random supplies per-run variation without adding a physics dependency. Silk.NET/OpenGL and ImGui.NET remain the renderer and GUI, preserving the existing camera, projected humanoid style and interaction model. xUnit remains the regression framework. All NuGet versions remain pinned in Directory.Packages.props; requirements.txt is the matching informational inventory, not a pip input. No new library is required.
- **Static versus dynamic geometry:** low open furniture blocks bodies, but does not act as a full-height eye-level/blast wall or a gas-tight seal. Runtime chair changes invalidate navigation independently of the fume precompute's static-layout version. The cached fume field continues to expose seated people and remains usable when chairs fall.
- **Scenario format 4:** stores original furniture kind, centre and rotation. Runtime chair damage, flight and released occupants are not stored as authored geometry. Loading/restarting recreates intact occupied furniture. Formats 1, 2 and 3 are imported through migrations. Older application builds cannot load the new format.

## Timing and modelling bounds

The initial stand-up sample is 0.8–1.6 seconds. High panic shortens it; low mobility, low urgency and calm response can extend it. The adjusted interval is clamped to 0.6–2.1 seconds. Existing stun and crowd blockage can extend the actual time until walking starts. The extra chair-exit step is physical movement, not part of the sampled rise time.

Chair destruction occurs inside 45% of the configured hard-kill radius. Launches occur inside the configured fallen radius; some chairs farther out, inside the injury radius, topple. These are **tunable normalized hobby-simulation heuristics**, not empirically calibrated pressure/impulse/material thresholds. Chairs use conservative circular collision envelopes: 0.30 m upright/airborne radius and 0.40 m fallen radius. The flight drawing tumbles in heading and uses a tipped pose; it is not a six-degree-of-freedom rigid-body solver. Tables are static and indestructible by design. Furniture effects depend on the configured abstract blast zones.

The existing visibility-graph planner was retained. Dense furniture layouts add many obstacle nodes and moving chairs invalidate routes; performance for large banquets has not been benchmarked. Start with a few sets and measure your intended scene scale. The fume model remains the existing precomputed floor-plane approximation; it does not model seated versus standing breathing height, under-table turbulence or furniture porosity.

## Build and run on Windows

1. Extract the complete ZIP into a new folder and open a terminal in `CrowdSimCs`.
2. Ensure the **.NET 8 SDK** is installed: `dotnet --info`. A runtime-only installation cannot compile the project.
3. Restore the pinned dependencies: `dotnet restore CrowdSim.sln`.
4. Build: `dotnet build CrowdSim.sln -c Release --no-restore`.
5. Run all regression tests: `dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-restore`.
6. Run the desktop application: `dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build`.
7. Choose Explosion or Toxic Fume. Place at least one exit, then furniture and any additional floor agents. For fume, place the source and precompute after finishing the layout. Finalise, start the simulation, and trigger the selected event.

To run only the new tests:

```powershell
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --filter FullyQualifiedName~FurnitureTests
```

To build a self-contained Windows executable after successful validation:

```powershell
dotnet publish src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

## Verification status — read before use

The C# simulation core and the full OpenGL/ImGui application compile successfully with the .NET
8.0.413 Roslyn compiler. A reflection-based runner invoked all 123 xUnit Fact/Theory cases:
**123 passed, 0 failed**. A separate executable smoke test also passed scenario save/list/load and
post-explosion replay restoration. The standard `dotnet test` CLI could not start inside this
container because of a host process-introspection limitation, so run the normal Windows command
below as the final project-system check. An interactive OpenGL visual check and performance
benchmark still need to be performed on Windows.

Completed checks: complete C# syntax parsing, production-assembly compilation, 123 test cases,
save/replay smoke execution, furniture geometry arithmetic, source review of the
hazard/awareness/movement gates, and archive integrity.

`FurnitureTests.cs` covers exact counts/attachment, immobility before threat, atomic
rejection/undo, navigation versus visibility/fume blocking, timed rise, casualty preservation,
direct blast death/destruction, fume exposure, save/reload, bounded landing/static tables,
congested-seat waiting/release, restart after destruction, and explicit checkpoint replay.
`ScenarioFileStoreTests.cs` covers save/list/load, atomic overwrite, and the stable per-user path.

### Manual acceptance checks

- Place one table set and one single chair: total occupants 11. Rotate a second single chair. Verify seated poses, chair fronts/backs, preview validity and depth ordering at several zoom levels.
- Undo each set. Save/reload a layout, then finalise and confirm counts and seated poses. Repeat a restart after chairs are destroyed.
- Run 30 seconds before an event: seated positions must stay fixed while floor agents wander.
- Explosion: try a source at a chair, beside a table, outside the injury zone and behind a wall. Check existing casualty outcomes, fixed tables, toppled/flown/destroyed chairs, bounded room collisions and obstacles after landing.
- Toxic fume: test directly exposed and distant occupants. Check awareness-driven rise, delayed social reactions, ongoing exposure after chairs topple, and permanent incapacitation.
- Place floor agents around a chair to obstruct departure. Verify waiting, gap selection and release when the space opens. Check no walking through table/chair footprints.
- Compare repeated runs with the same seed and identical trigger frame; then test several seeds and the intended maximum furniture count. Record frame time and evacuation metrics.

## Suggested next additions

1. **Aisle and exit-access analysis:** show minimum clear aisle widths and disconnected free-space warnings before finalisation. The current placement footprint is a local check, not proof that every occupant can reach an exit.
2. **Mobility presets:** explicit age/mobility/assistive-device parameters and assistance policies, with uncertainty ranges. Current timing uses the existing mobility and urgency values.
3. **Batch experiments:** repeat seeds and report distributions of time-to-stand, time-to-leave-seat, congestion, evacuation time and incapacitation, rather than interpreting one run as a prediction.
4. **Furniture materials and stability presets:** calibrate light plastic versus heavy banquet chairs against observations before calling flight/toppling realistic. Consider a dedicated physics backend only if that accuracy becomes a primary requirement.
5. **Scene editing beyond undo:** select/move/delete/duplicate furniture groups, with the same atomic occupant linkage and overlap validation. This version supports placement, rotation of the next asset, and last-placement undo.

## How it works

Each step first applies the existing selected hazard, panic propagation and reaction transitions. Furniture updates chair states and collision proxies. The seating controller blocks walking while the person is seated or rising, cancels seating for casualties, and releases a capable person only through a clear chair-exit gap. Existing SFM, local avoidance, routing, contact projection and exit-flow logic then take over. Rendering draws the same authored furniture proxies in depth order and interpolates a seated body into its rising pose. Persistence saves authored transforms rather than runtime damage.
