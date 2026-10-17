# Explosion furniture physics update

This patch extends the existing .NET 8/C# 12 application; no new packages are required.
The complete source, centrally pinned NuGet versions, existing tests and new xUnit
regression tests are included. requirements.txt is an informational .NET inventory.

## Behaviour

- The existing expanding shock front and wall shielding determine when each table
  or chair is affected. Distances use the authored centre of each individual piece.
- At or inside the configured HeatIncapacityRadius (default 3.16 m), tables and
  chairs are destroyed and their collision proxies removed. This includes the entire
  inner dead zone. Solid walls still shield furniture from the blast.
- Surviving furniture between HeatIncapacityRadius and HeatRadius (default 18.21 m)
  receives an outward impulse. Strength is (outer radius - distance) / (outer radius
  - incap radius), so movement falls to zero at the outer boundary. Tables receive
  65% of chair horizontal launch speed and a reduced vertical speed.
- Furniture beyond the outer radius stays unchanged. Distance is measured from the
  current centre of each individual piece when reached by the shock front. Obstructions
  and impacts can shorten actual travel.
- Flight uses gravity (9.81 m/s²), horizontal drag and shared small physics steps.
  Step count accounts for horizontal/vertical speed and possible speed gain on impact. Room boundaries, wall rectangles and non-furniture circular
  obstacles stop horizontal motion. Bodies then finish their fall.
- Furniture now collides with other furniture when their horizontal footprints and
  vertical extents overlap. Sufficiently high pieces can pass overhead. Descending
  pieces stop at a supporting surface instead of penetrating it.
- Horizontal collisions use restitution 0.15 and a relative table/chair mass ratio
  of 6:1. Momentum can move a stationary target; heavier targets slow the incoming
  piece more. Occupied chairs and targets pinned against static geometry do not
  receive movement. Ground friction dissipates transferred momentum.
- Supported pieces remain elevated until they slide off or their support is removed,
  then fall. This uses simple cylinders and a single support reference, not detailed
  mesh contacts, full rotational dynamics or an engineering-grade stacking solver.
- Flight/sliding can apply the existing debris-impact response to one person per piece;
  the piece loses speed on that impact. Chairs exclude their original occupant.
- Landed tables use a 0.90 m circular footprint; fallen chairs use 0.40 m. Moving
  footprints update the existing navigation system. Destroyed furniture has no
  footprint. Settled pieces stop generating navigation changes.
- Table visuals follow runtime position and height and flatten on landing. Chairs
  keep their tipped visual. These are simplified poses, not full rotational dynamics.
- Save/replay retains the authored layout, so replay starts with intact furniture
  at its original positions. Toxic fume does not invoke furniture blast impulses.

## Pushable loose chairs

Evacuating/injured walking agents can shift reachable, unoccupied upright/fallen
chairs. Movement contact, circular-obstacle avoidance and waiting-for-gap recovery
all use the same push operation. An agent first pushes away from their body, then
tries diagonal and lateral shifts if blocked. Occupied/airborne chairs and tables
cannot be pushed by pedestrians.

Each displacement is limited to at most 0.12 m per tick, with a speed limit of
1.5 m/s and 0.025 m collision substeps. A chair moves at most once per simulation
tick, regardless of crowd size. Friction stops it when contact ends. Swept checks
include room boundaries, walls, tables, other chairs and people. Existing overlapping
blast piles may separate; pushes cannot deepen overlaps. Landed/moved proxies remain
in navigation and collision detection at their new positions.

Crawling people retain the existing solid casualty envelope (1.25 times body radius),
and pedestrians keep the existing bypass/wait/seek-gap behaviour. A waiting person
can move a reachable chair with zero running velocity. Furniture motion does not
allow walking through a crawling person. No CPU performance claim is made; no
benchmark or visual crowd-throughput run was possible in this environment.

## Design and dependencies

FurnitureBody shares the existing chair physics state between tables and chairs;
FurnitureChair adds occupant identity and a per-tick push limit. FurnitureSet keeps its immutable authored
centre separately from its runtime table body. The legacy ChairState enum name is
retained to avoid breaking existing chair consumers.

System.Numerics supplies the existing vector calculations. Silk.NET/ImGui.NET
continue to render the scene and xUnit provides lifecycle regression tests. No
additional physics package, native dependency or GPU compute pipeline is introduced.
Flight and local push checks use the CPU; CPU timing has not been benchmarked.
Furniture impacts use reusable 2 m grid buckets and inspect neighbouring cells rather
than every furniture pair. Obstacle proxies update once per tick after microsteps.

## Validation

Added FurnitureBlastTests.cs covering table/chair destruction, visible flight,
outward displacement, persistent landing obstacles, wall shielding, swept wall
collision, distant-blast immunity, replay restoration, toxic-mode isolation and
same-seed repeatability. Added configured-incap-boundary and near-versus-far
travel tests. ChairPushingTests covers movement integration, forward/sideways
pushes, crawler clearance, jammed chairs, zero-velocity waiting, occupied chairs and
per-tick movement limits. Updated old tests that required tables to remain fixed or
placed surviving furniture inside the newly enlarged destruction zone.

FurnitureImpactTests covers impulse momentum/energy behaviour, relative mass,
pinned targets, low-flight collision, overhead clearance, supported landing and
falling after support removal. Unobstructed distance tests isolate furniture because
collisions can legitimately shorten travel.

Source call sites and archive contents were checked. Build, xUnit execution and
OpenGL visual verification were NOT run: this editing environment has no .NET SDK,
and an attempted SDK download timed out. Run these checks on your Windows machine:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\CrowdSim.sln -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL -c Release --no-build
```

## Visual check

1. Extract the complete ZIP into a new directory and open that CrowdSimCs folder.
2. Select Explosion, place a table set with open space around it, then start.
3. Trigger approximately 5 m to one side of the table centre: the table and nearby
   chairs should lift, move outward and settle. People should avoid landed pieces.
4. Restart and trigger within the configured incapacitation zone of the table centre: the table should disappear.
5. Restart again: the authored table and occupied chairs should be intact.
6. After occupants leave their chairs, watch agents approaching loose chairs:
   chairs should shift if there is free space and stop if wedged against geometry/people.

See run_instructions.md for the full existing setup guide.

## Additional visual checks for impacts

- In Explosion mode, place two furniture groups along the outward blast direction.
  Surviving low-moving pieces should slow down, stop/rebound or shove other pieces.
- Pieces well above table/chair height should pass overhead, then collide on descent.
- Check an impact near a wall: a wedged target should stay in place.
- Let the scene settle: all remaining ground/support footprints should remain obstacles.

These scenarios still require Windows/OpenGL verification; the editing environment
cannot run the .NET application. No claim of passing xUnit tests or measured FPS is made.

## Impact test fixture correction

The initial FurnitureImpactTests.Scene fixture omitted an exit. SetRunning(true)
therefore correctly kept Running false, and Step() never advanced physics. The
fixture now adds an exit, asserts Running, and verifies the exact frame count after
advancing. Motion, clearance and support assertions have not been weakened. This
also prevents the pinned-wall test from passing merely because nothing ran.

The user reported 160 passing tests and three failures before this correction.
The corrected suite has not been executed in the editing environment (no .NET SDK).
Rebuild before running it; --no-build alone would reuse the previous test assembly:

```powershell
dotnet test .\CrowdSim.sln -c Debug
```
