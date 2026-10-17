# Barricade / asset-picker verification report

## Delivery scope

Base: `CrowdSimCs(9).zip` (the current user upload).
Base SHA-256: `096b75eac8325b102ec9ca6e7861130264569c60bffbfc98085afaf7aff7322b`.

The update includes flush square-block placement, legacy-block edge compatibility,
a fixed orange 1.5 x 0.4 x 1.0 m road barricade, optional end-to-end snapping, a unified
asset dropdown and per-asset rotation state. The 0.4 m width and fixed/non-destructible
barricade behaviour are explicit modelling assumptions, not supplied product specifications.

## Checks performed in this environment

| Check | Result |
|---|---|
| Project XML and explicit project/source references | Passed for 10 project files |
| Lexical delimiter scan | Passed across 88 current source/test/benchmark files; not a compiler |
| BuilderTool / BuilderAsset / FurnitureKind reference audit | All referenced members exist |
| Existing physics, navigation, configuration, agent-outline and package audit | 25 audited files byte-identical to the uploaded base |
| New block-coordinate float32 reference | 500 same/mixed-size adjacent-edge comparisons passed |
| Old block-coordinate reproduction | 9 apparent overlaps in 40 sampled neighbouring cells; largest 0.000001907 m |
| Maximum benchmark asset-layout reference | 24 tables + 15 x 6 m stage + 64 barricades fit without overlaps |
| Patch whitespace validation | Passed |
| Patch reapplication and file-content comparison | Passed |
| Final ZIP CRC and source-content comparison | Passed |

The float32 and benchmark-layout checks are independent numerical reference calculations.
They do NOT execute the C# application, planner, simulation or regression tests.
Patch and archive checks establish delivery integrity, not behavioural correctness.

## Checks NOT performed

`dotnet --info` returned `dotnet: command not found`. No .NET SDK is installed here;
the SDK download endpoint was also unreachable from this environment. Consequently:

- No C# build or xUnit execution was performed.
- No OpenGL application launch, visual inspection, mouse or keyboard interaction was performed.
- No simulation-latency, rendering-FPS or 600-agent benchmark was run.

No compile success, passing-test count, visual success or performance guarantee is claimed.
The user's success with an earlier revision does not validate this new source revision.

## Added regression coverage (declared, not executed)

`BlockageAdjacencyTests.cs`: 17 cases. Same/mixed sizes, touching edges/corners,
non-finite coordinates, real overlap, collision seams, boundary placement, saved layouts,
legacy blue tiles and old half-cell-shifted block compatibility.

`BarricadeTests.cs`: 21 cases. Dimensions and rotation, stable IDs, multiple placement,
undo, optional snapping, obstruction rejection, no occupants, movement/visibility/hazard
semantics, routing, persistence, replay and actual started-hazard fixtures for both scenarios.

`BuilderAssetSelectionTests.cs`: 10 cases. Dropdown completeness, applicable rotation,
quarter-turn wraparound, per-asset facing memory, reset and invalid-input handling.
The picker-state file is linked into the tests without an OpenGL/window dependency.

Total: **48 additional declared regression cases**. Existing tests are retained unchanged;
only their project file is extended to compile the linked picker-state code.

## Local build and test commands

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Run these from the extracted directory containing `CrowdSim.sln`. `--no-build` tests must
follow a successful build of this revision. The ZIP intentionally omits stale bin/obj
outputs. The `scripts/verify-barricade-assets.ps1` helper stops on restore/build/test failure.

## Performance and compatibility notes

Barricades have a single static rectangular collision footprint and no moving physics body.
Existing natural-navigation work budgets and package versions are unchanged. The optional
fifth argument on the rectangular-assets benchmark adds 0..64 barricades; CPU timings exclude
graphics and setup/precomputation. Old benchmark results are not measurements of this update.

Existing furniture IDs and saved block positions are preserved. A pre-update application
will not recognise newly saved barricades. A 1 m barricade blocks walking in this model,
but is not a full-height blast shield or an airtight fume wall; no climbing is modelled.
The existing Toxic Fume precompute invalidation workflow still applies after layout edits.
