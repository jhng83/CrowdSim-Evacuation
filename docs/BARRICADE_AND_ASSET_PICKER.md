> Current angle and speed details: [FREE_ANGLE_BARRICADES_AND_SPEED.md](FREE_ANGLE_BARRICADES_AND_SPEED.md).

# Flush blocks, orange barricades and the unified asset picker

## Base and scope

The original asset-picker revision started from `CrowdSimCs(9).zip`. This guide now
includes free-angle barricades from the subsequent angle/speed-control revision. It preserves the existing 3 x 1 m movable/destructible table, 15 x 6 m fixed stage
(one per scenario), light-grey floor, black agent outlines and natural navigation for
Explosion and Toxic Fume. No new NuGet package, external model or texture is required.

## Builder workflow

The **ASSETS** dropdown includes both square-block sizes, the 2.5 m circular blockage,
round table with ten occupants, single occupied chair, unoccupied rectangular table,
stage and orange barricade. Choosing a dropdown item immediately activates its preview.
**Place selected asset** resumes that tool after using Exit, Agents or Fume Source; it is
not an extra mandatory confirmation after selecting an asset.

Rotation controls are shown only for assets with a meaningful orientation. The table
and stage use **Rotate 90 deg (R)** and an X/Y direction readout. Barricades have a free
angle slider, exact numeric input and +/-15 degree buttons (R / Shift+R over the canvas).
Occupied chairs and round-table sets retain a free facing-angle slider. Every asset remembers
its own orientation when switching between selections. Rotation affects the NEXT placement,
not objects already placed. Hover the world canvas and press R to rotate. The shortcut
is ignored while typing or while the pointer is over a sidebar/popup; the button is also
available for rotation.

The already-placed stage is labelled `1/1 placed` in the dropdown and cannot be selected
again. If it remains the active tool immediately after placement, its placement button is
disabled and its preview remains invalid until undo removes the stage. All other assets
remain available. Exits, population placement and the fume source remain separately grouped
because they are scenario setup tools rather than furniture assets.

Green preview means the footprint fits; red means it is blocked or outside the room.
Ctrl+Z removes the last placed object/set, using the existing undo stack.

## Square-block correction

There were two separate problems: one- and two-cell blocks were both snapped by their
centres, putting mixed-size edges half a cell out of alignment; and single-precision
centre-plus/minus-half-size arithmetic could turn touching edges into tiny apparent overlaps.

New block edges are calculated directly from integer grid coordinates. Preview and commit
use the same bounds and validation methods. A placement-only tolerance of **0.0001 m
(0.1 mm)** accommodates float rounding. Collision footprints are not reduced: this is NOT
implemented by drawing a bigger block around a smaller invisible collision shape.

Existing saved block coordinates are left unchanged. When placing next to an old, half-cell-
shifted block, a nearby valid face-adjacent placement can inherit that block's edge grid.
This allows old and new tiles to meet without rewriting the old layout. Real positive-area
overlaps beyond the tolerance, collisions with people and room-boundary violations are
still rejected. New blocks can also meet a boundary wall where room/exit checks permit it.

## Barricade dimensions and modelling choices

| Property | Implementation |
|---|---|
| Length | 1.5 m |
| Height | 1.0 m |
| Base width | 0.4 m, an explicit modelling assumption because width was not specified |
| Appearance | Orange broad base and tapered body, with white reflector panels on both long sides |
| Rotation | Any angle, including fractional degrees |
| Occupants | None |
| Limit | Multiple per scenario |
| Hazard behaviour | Fixed and non-destructible in both scenarios |
| Walking | Solid rectangular footprint; no climbing/jumping behaviour |
| Sight/blast/fume | Low furniture, not a full-height occluder or an airtight fume seal |

The appearance is a stylised road barrier, not a dimensional or protective-performance
claim about a certified Singapore product. One exact 1.5 x 0.4 m rectangular footprint is
used; no bounding-circle proxy and no runtime rigid body are added.

**Snap barricades end-to-end** is enabled by default. A cursor within 0.30 m of an eligible
parallel barricade endpoint snaps to a valid touching placement. Preview and actual
placement use the same snap function. The snap does not pull toward distant, perpendicular
or obstructed endpoints. Disable the checkbox for free centre placement. Rotation is not quantised. Snapping works at any parallel heading. Non-parallel corner
connections can be placed manually when their physical footprints do not overlap.

## Persistence and performance

The new furniture identifier is appended as `Barricade = 4`; existing IDs are unchanged.
Saved position and orientation are restored through the existing furniture persistence
and checkpoint workflow. Existing readers predating this asset cannot be expected to load
a scenario that contains the new ID. Stage validation still limits only stages, not barriers.

Static barricades reuse the wall spatial index and cached fixed-asset render-entity list.
They add no seats, agents, fragments, moving-body updates or per-agent observation model.
Snapping and the dropdown are builder-only work. More obstacles still mean additional
collision/routing/rendering work: no zero-cost or 600-agent FPS guarantee is made.

Geometry changes retain the existing Toxic Fume cache-invalidation workflow. Recompute the
plume after editing the room layout before finalising/starting a Toxic Fume run.

## Build and verification

From the directory containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

The verification helper runs restore, build and the complete test suite, stopping on failure:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-barricade-assets.ps1
```

Use `-OnlyAssetTests` to run only the three new suites after restoring/building. They contain
47 retained declared cases from that revision (one obsolete quarter-turn barricade case was replaced) for block seams, legacy alignment, barricade geometry/snapping, hazards,
save/load/replay and the window-independent picker state. These cases have NOT been run
in the delivery environment. See `BARRICADE_ANGLES_SPEED_VERIFICATION.md` for current checks; the earlier
`BARRICADE_ASSET_VERIFICATION.md` describes only the previous release.

### Optional 600-agent CPU benchmark

The existing rectangular-assets benchmark accepts a fifth optional argument: barricade
count, from 0 to 64. Its earlier four-argument commands still work unchanged.

```powershell
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\barricades-explosion Explosion 400 600 40
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\barricades-fume ToxicFume 400 600 40
```

These workloads include 24 rectangular tables, one stage and 40 barricades, over three
seeds. CPU timestep timing excludes rendering, recording and setup/fume precomputation.
Adding barriers can change accepted spawn positions and resulting crowd behaviour; this
is an end-to-end workload, not an isolated microbenchmark of a single collision check.
Older CSV results elsewhere in this project do not validate this update.

### Manual visual acceptance

Check both scenarios: place same-size and mixed-size blocks flush; join a new block to an
old saved two-cell tile; switch dropdown choices and confirm their headings are retained;
rotate and place a row of orange barricades; verify a red preview for real overlaps and a
green preview for valid snapped endpoints. Place a stage and check the single-stage lock,
undo, save/load and replay. Verify that barricades remain visible after triggering the hazard
and that agents retain their black outlines on the light-grey floor.

The executable source under `src/` and `tests/` is authoritative. Older source snapshots and
historical revision notes retained elsewhere in this project are not updated specifications.
