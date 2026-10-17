# Free-angle barricades and maximum voluntary running speed

## Builder workflow

Choose **ASSETS > Orange barricade - 1.5 m long**. Set **Angle** with the slider or
**Exact angle (degrees)** input. Values wrap through 360 degrees; 0 degrees is along X,
90 degrees along Y. Fractional angles are preserved (for example 22.5 or 37.5 degrees).
The **-15 deg / +15 deg** buttons are shortcuts, not angle quantisation.
With the pointer over the world canvas, **R** rotates the preview by +15 degrees and
**Shift+R** by -15 degrees. Text entry and inactive placement tools do not consume the shortcut.

Click the floor to place; repeat to place more. Each asset remembers its own heading
when switching dropdown choices. Angle changes affect the preview/next placement only.
They do not rotate already-placed objects: undo/re-place to change those.

**Snap barricades end-to-end** still defaults to enabled. At any chosen angle it snaps
nearby, parallel (including opposite-facing) barricades into an end-to-end row, provided
the candidate fits. It does not auto-rotate to another barricade's heading, join skew
angles, reach distant endpoints or override overlap checks. Toggle it off for free placement.
Table and stage controls remain quarter-turn-based; their moving physics has not been
expanded to arbitrary-angle tumbling. Square/circular blocks still show no rotation control.

## Shape, collision and navigation

A barricade remains a fixed orange 1.5 m long, 0.4 m wide, 1.0 m high low obstacle.
It adds no occupants or rigid-body physics. It is not destructible, climbable, an airtight
fume wall or a full-height blast shield. Those modelling assumptions are unchanged.

`OrientedRectangle` stores its axes and enclosing bounds once. The enclosing AABB is
used ONLY to shortlist nearby obstacles; the actual oriented shape determines clearance.
Placement uses a four-axis separating-axis test. Pedestrian contacts resolve against
rotated faces/corners; swept route clearance uses the exact rectangle expanded by a disc,
including rounded corners. Static visibility-graph corners and shared/fine-grid occupancy
use the oriented footprint. Ground preview outlines also match the real rotated shape.
Empty corners of the enclosing AABB do not become invisible walls.

The existing 0.1 mm placement tolerance remains, for floating-point edge contact only.
Saved barricade angles are restored without quarter-turn quantisation. Old quarter-turn
placements still load; an OLD executable would re-quantise a newly saved free-angle scene,
so use this executable when reopening new scenes. Existing square-block alignment is retained.
After altering a Toxic Fume layout, recompute the plume through the usual setup workflow.

## Running-speed control (not playback speed)

Before finalising, open **BEHAVIOUR TUNING**, then **Max agent running speed (m/s)**.
Use the slider or Ctrl+click it to type a value. The supported engineering control range
is **0.5 to 6.0 m/s**; it is not an empirical or safety-validated human speed range.
The default is `0.607 / 0.15`, approximately **4.0467 m/s** (displayed as 4.05).
Reset behaviour restores this default. Save/load, finalised checkpoints and restarted
scenes preserve the chosen value. Legacy scenes without the new JSON property use the default.

This is a ceiling on **self-propelled movement**, not an instruction to run at that value.
For example, a person whose model requests 1.2 m/s remains at or below that pace under a
2.0 m/s ceiling. A 1.0 m/s ceiling limits a requested 1.8 m/s. Raising the ceiling alone
cannot raise the existing baseline/urgency-derived desired speed. `Emergency urgency`
is still the separate control affecting desired movement within its existing bounds.

The ceiling is applied before the agent's injury, fume and local-navigation reductions,
so impairments and waiting continue to lower speed. SFM integration, hybrid open-space
movement, wandering and legacy motor recovery honour it. Conversion uses the existing
0.15-second reference units, independent of the selected integration timestep.

**Simulation speed** remains playback acceleration, and the source `AgentConfig.MaxSpeed`
remains the separate blast-velocity guard. Neither is overwritten. This new ceiling is
NOT a hard bound on displacement caused by blast throws, furniture impact, collision
separation or other physical correction; those are not voluntary running.
No in-run tuning UI was added: choose the value in the builder before finalising.

## Latency and tests

Static barricade orientation axes are cached; narrow-phase tests use value types and
reusable route/spatial caches. No new GPU pass, observation model, physics body or package
is introduced. One scalar speed ceiling replaces no existing crowd calculations.
Navigation work budgets are unchanged. Angled geometry has additional arithmetic costs;
there is no measured FPS or 600-agent latency guarantee in this delivery.

The three new suites declare **65 cases**: `BarricadeAngleTests` (36),
`AgentRunningSpeedTests` (22), `BuilderBarricadeAngleTests` (7). The obsolete barricade
quarter-turn expectation was removed from one existing theory; table/stage expectations
remain. These tests are INCLUDED but have NOT been executed here.

Run restore/build/all tests with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-barricade-angles-speed.ps1
```

`-OnlyNewTests` limits the test invocation to these three suites after building. Do not
use that option as a substitute for running the entire regression suite before accepting.

The existing 600-agent harness has an optional sixth argument for barricade angle:

```powershell
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\angle0 Explosion 400 600 40 0
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\angle37 Explosion 400 600 40 37.5
dotnet run --project .\benchmarks\RectangularAssetsLatency\RectangularAssetsLatency.csproj -c Release -- .\benchmark-results\angle37-fume ToxicFume 400 600 40 37.5
```

These compare three seeded end-to-end workloads, not isolated geometry CPU costs.
The scene has 24 rectangular tables, one stage and the requested barricade count. Timings
exclude setup, fume precomputation, rendering and recording; changed geometry may also
change accepted spawn positions and crowd paths. No results for this revision are bundled.
Earlier benchmark CSVs and earlier verification reports in the project are historical.

## Manual acceptance

In both scenarios, place barricades at 0, 22.5, 37.5, 90 and 135 degrees. Confirm that
preview outlines match the bases, parallel rows snap flush, real overlaps turn red, and
people move alongside diagonal faces rather than their enclosing boxes. Exercise
save/load, undo and restart; check the existing block, table and stage tools too.
Compare a low 0.5 or 1.0 m/s ceiling with the default on open floor. Inspect physical
agent speed rather than playback animation; confirm fume/injury impairment still slows
agents and that changing playback speed does not change physical metres-per-second values.
