# Movable 3 m x 1 m tables, one 15 m x 6 m stage, and agent visibility

## Scope

Base: `CrowdSimCs_Assets_Visibility.zip`. This update fixes the failed asset test, makes
unoccupied rectangular tables blast-movable/destructible, and enlarges the fixed stage.
Light-grey floors and black outlines are retained for both scenarios, before/after triggering.
The existing round-table/chair behaviour, navigation recovery settings, fume model and
NuGet versions are retained. Shape-aware furniture contact handling now supports rectangles.

## Builder

| Asset | Footprint | Added chairs/people | Limit |
| --- | --- | --- | --- |
| Rectangular table | 3 m x 1 m | None | Multiple, subject to space |
| Stage | 15 m x 6 m | None | One per scenario |

Choose the asset under FURNITURE / ASSETS and select Length along X or Length along Y.
Preview, placement validation, movement collision and rendering use the same dimensions.
Stage height stays 0.80 m. The upright table stays 0.78 m high. Agents cannot climb onto
these assets or walk under them. The stage button, core placement and load validation
all enforce the single-stage limit. Undo releases the limit.

The stage must fit inside the room boundary walls. For example, a 12 m x 12 m room no
longer accommodates this stage, although it could accommodate the previous 10 m x 4 m one.

## Explosion tables

Tables use the existing lightweight furniture blast pipeline, with no new yield model:

- The propagating shock front must first reach the table's centre. Full-height walls shield it.
- A table centre inside the configured `HeatIncapacityRadius` is destroyed. The default
  value in this project's configuration is 3.16 m; changing the blast-zone settings changes
  the threshold. This is a simulation rule, not a calibrated real-world destruction distance.
- Between that inner zone and `HeatRadius`, the existing table-scaled impulse can launch
  the table outward. Gravity, drag, contacts and ground friction bring it to rest.
- Beyond the outer zone, there is no direct blast impulse. Physical furniture contacts may
  still move it, just as contacts can move existing furniture.

The rectangular table can translate, become airborne, land and slide. It **keeps its authored
quarter-turn heading**: this is not a full rotating/tumbling rigid-body model. Its toppled
visual uses the existing simplified flattened-table convention. Both collision and drawing
retain the same true 3 m x 1 m horizontal footprint; no oversized circle blocks pedestrians.

Walls, the stage, circular obstacles and nearby furniture constrain table motion. Local
furniture contacts use rectangle/rectangle or rectangle/disc tests after spatial culling.
Nearby-agent impact candidates are also filtered against the actual table rectangle.
The existing debris-impact response handles a struck person.

Destroyed tables no longer draw or block paths; their original footprint is removed from
both the live wall list and spatial index. The authored table remains in scenario data so
Restart restores it. This does not add individually simulated fragments or a fracture solver;
the existing explosion debris system is unchanged.

## Toxic Fume

There is no blast launch or destruction from fume. Rectangular tables remain intact in
normal Toxic Fume use. The low table and stage footprints block people, but retain the
previous approximation of not sealing the floor-plane fume field or acting as full-height
vision/blast walls. Fume exposure, impairment and permanent incapacitation are unchanged.

## Navigation and latency

Current rectangular footprints are updated after furniture physics. Movement/destruction
increments the navigation version, not the static wall-layout version. Only spatial-hash
cells touched by the old/new footprint are updated; static walls, stage render chunks and
fume precomputation do not rebuild solely because a table moves.

Shared exit fields remove old rectangle occupancy and add new occupancy at their existing
bounded refresh cadence. Local segment collision checks use the live footprint even while
coarse guidance is waiting to refresh. Existing normal-route/recovery budgets are unchanged.
The stage is rendered from 24 cached pieces in either quarter-turn orientation.

Moving bodies incur CPU work, unlike the former fixed table. Sleeping tables do not receive
integration work, destruction adds no fragment bodies, and rectangle contacts reuse the
furniture spatial grid. These measures are not a measured 600-agent latency guarantee.

A dedicated CPU-step benchmark includes 600 agents, 24 rectangular tables, one 15 m x 6 m
stage and three seeds. It records full-step/furniture/route times, p95/p99 and allocation data.
Rendering, recording, setup and fume precomputation are excluded from timing.

```powershell
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\rectangular-explosion Explosion 400 600
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\rectangular-fume ToxicFume 400 600
```

The benchmark is included but has not been executed in the delivery environment.

## Tests and the reported failure

The old `FixedAssetsStayFixedAfterExplosion` did not create an exit. `SetRunning(true)`
therefore kept `Running` false; the trigger and steps did nothing, and `FrameIndex > 0`
failed. This did not establish that a fixed asset moved. The missing-exit guard is unchanged.

The replacement `StageStaysFixedWhileNearbyRectangularTableIsDestroyed` creates an exit,
asserts `Running`, asserts that the explosion exists, checks exactly 30 steps, and verifies
the new expected asset behaviour rather than retaining a now-invalid fixed-table assertion.

`FixedAssetTests.cs` now contains 35 source-defined cases. `RectangularTableBlastTests.cs`
adds 20 cases covering motion/settling, exact bounds, central/custom-zone destruction,
shielding, wall/stage contacts, boundary clearance, broad/narrow collision geometry,
spatial-hash relocation/removal, shared fields, Toxic Fume stability, replay and determinism.
No test is skipped or weakened to bypass the failed assertion. These cases are **not run here**.

```powershell
.\scripts\verify-fixed-assets.ps1 -Configuration Release -AssetTestsOnly
```

Omit `-AssetTestsOnly` for the complete regression suite. The script restores and rebuilds
before testing, and includes both asset test classes in its focused filter.

## Saved layouts

Furniture kind IDs are unchanged. Save/restart restores authored positions and intact tables,
not their transient blast positions or destroyed state. Existing saved stages are interpreted
at the new 15 m x 6 m dimensions. A previously valid layout can consequently fail validation
if the enlarged stage overlaps an obstacle/person or leaves the room; adjust that stage in
an earlier build before saving, or revise its saved placement. Overlap checks are not bypassed.

The older verification records and performance CSVs in this project are historical; they do
not certify this new revision. See `MOVABLE_TABLES_STAGE_VERIFICATION.md` for current checks.
