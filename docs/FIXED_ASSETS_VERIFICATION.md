> Historical verification record for the previous fixed-table/10 m x 4 m stage revision.
> Superseded by `MOVABLE_TABLES_STAGE_VERIFICATION.md`. It is not evidence for this revision.

# Fixed assets and visibility: verification record

## Scope and exact input

Input: `CrowdSimCs(8).zip` supplied in this conversation.

Input SHA-256:
`6f48539f1590cbf6193cde0a1a59f7c46f6e910baaa5030a98a4848a89733c6c`

This record concerns the new unoccupied 3 m x 1 m fixed table, the single 10 m x 4 m
fixed stage, the light-grey floor and black agent outlines. The previous natural-navigation
and benchmark documentation remains historical, not evidence for this new revision.

## Completed checks

| Check | Result and scope |
| --- | --- |
| Exact base | Modified the extracted uploaded source, not an earlier generated version. |
| Source preservation | All 218 non-build files from the base are retained, with documented edits. |
| Dependency preservation | All existing `.csproj` and `.props` files are byte-identical to the input. No package/version changes. |
| Prior build fix | `tests/CrowdSim.Tests/GuardrailTests.cs` is byte-identical, preserving its Configuration namespace import. |
| Navigation preservation | NaturalNavigation and ToxicNavigation controllers, SFM, timestep, configuration and existing furniture-physics files are byte-identical. |
| Integration review | Traced builder preview/placement, undo/reset, save/load/replay, occupancy counts, both rectangle-route corner paths, shared/grid occupancy, wall collision, LOS and low-furniture hazard exclusions. |
| Rendering review | Traced overview/POV floor, builder dots/seated previews, detailed body parts and fast-screen figures. Both scenarios use these shared render paths before/after triggering. |
| Lexical delimiter scan | 78 source/test C# files scanned with a C# lexer; no unbalanced braces/brackets/parentheses. This is NOT a compiler, type checker or full syntax parser. |
| Project XML | 12 existing project/property XML files parsed successfully. |
| Independent outline algorithm check | A Python translation of the convex-hull loop matched SciPy hulls for 2,000 random eight-point sets and handled three degenerate/duplicate cases. This does NOT execute the C# renderer or verify actual screen output. |
| Archive check | ZIP CRC check, expected source-file inventory and file-by-file byte comparison with the working source passed during packaging. |
| Patch check | The supplied patch applied to a fresh source-only copy of the input and reproduced the output source byte-for-byte. |

The clean output excludes stale `bin`, `obj`, `.git`, `.vs` and Python cache directories.
The original simulation fixtures, source, pinned dependencies and historical documents/data
are otherwise retained. `PROJECT_FILES.txt` is refreshed to describe this package.

## Checks NOT completed

| Check | Status |
| --- | --- |
| `dotnet restore` / solution build | NOT RUN: the delivery environment has no .NET SDK. |
| xUnit execution | NOT RUN, including all new cases and the existing suite. |
| Application launch / actual rendered appearance | NOT RUN: no launched .NET/OpenGL application. |
| Interactive placement, save/load and replay | Source-reviewed and covered by tests, but those tests/UI interactions are NOT executed here. |
| 600-agent CPU/GPU frame time | NOT MEASURED. Caching and low-allocation code are design choices, not measured latency guarantees. |

There is no claim that static checks establish compilation or runtime correctness.

## Added tests

`tests/CrowdSim.Tests/FixedAssetTests.cs` contains 34 executable test cases:
14 facts plus 20 inline cases across eight theory methods.

They cover exact dimensions/quarter-turn orientation, no attached occupants or bodies,
multiple tables, the stage limit in both scenarios, undo/clear, room resizing, invalid
coordinates, overlaps and chair-departure reservations, spawn exclusion, saved duplicate
stages, save/load/replay population preservation, old enum values, fixed post-explosion
footprints, body-clear visibility routes, shared exit fields, dense-grid routing, and the
spatial-hash versus simple-scan sight branches.

These are included regression specifications, not a report of passing xUnit execution.

## Local build and test

From the directory containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Or run `scripts/verify-fixed-assets.ps1 -Configuration Release` for restore/build/test.
Add `-AssetTestsOnly` to execute only the new class after building the entire solution.
Do not use `--no-build` until a successful build of the same configuration.

## Interactive acceptance checks

In each scenario, place two new tables and one stage, and verify the population counter
is unchanged. Check both rectangle orientations and invalid-overlap previews. Verify the
second stage cannot be placed, undo the stage, and place it again. Save/load the layout and
replay from scene start. Check that agents spawn outside the assets and use alternative
routes around their exact footprints.

Inspect black outlines before and after triggering, including seated, walking/running,
assisting, crawling and incapacitated/fallen agents. Check close/far zoom and POV. Existing
status/category colours should remain intact against the light-grey floor.

For approximately 600 agents, use Release, the same seed, layout, camera, zoom, recording
state and simulation speed for before/after comparisons. Inspect both simulation-step and
rendering time; the existing headless benchmark does not measure outline rendering cost.

## Explicit modelling bounds

The two new assets are fixed and non-walkable. They are not blasted into motion and add no
seats or occupants. Rotation is quarter-turn snapped to use exact AABBs; the UI exposes X/Y
orientations. Agents cannot climb onto the stage or walk under the table. Like existing low
furniture, these footprints do not seal the simplified fume field or act as full-height
visual/blast barriers. This is not a new calibrated 3D hazard or furniture-dynamics model.
