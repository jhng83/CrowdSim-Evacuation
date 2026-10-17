# Movable tables and 15 m x 6 m stage: verification record

## Exact base and requested changes

Base archive: `CrowdSimCs_Assets_Visibility.zip`.
SHA-256: `c9ba50c5a16b044dca708773bcb52dd5936415ad28eb4f8ee71b9f23912ea0c7`.

This update corrects the asset-test setup, makes 3 m x 1 m rectangular tables movable and
destructible by the existing explosion pipeline, and enlarges the single fixed stage to
15 m x 6 m. It retains the light-grey floor and agent outlines for both scenarios.

## Reported test failure

The original `FixedAssetTests.FixedAssetsStayFixedAfterExplosion` called `SetRunning(true)`
without creating an exit. The unchanged guard in `Simulation.SetRunning` rejected the run.
The explosion was not triggered, `Step()` did not advance, and the assertion at original
line 275 (`FrameIndex > 0`) failed. This was a test-setup defect, not evidence of stage drift.

The replacement test creates an exit and an unrelated exited agent, asserts `Running`,
asserts that the explosion exists, checks exactly 30 steps, then checks the fixed stage and
destroyed rectangular table separately. A no-exit runtime guard was not removed.

## Completed checks

- Inspected the input source and traced the failed assertion to the public run guard.
- Lexical C# delimiter scan: 111 files, zero lexer/delimiter errors. This is NOT a C#
  compiler, type checker or complete syntax/semantic validation.
- Parsed 13 project/property XML files and four JSON files (excluding VS Code JSONC).
- Compared 12 selected baseline files byte-for-byte, covering package/build settings,
  application project files, agent configuration, SFM, fume system, timestep, natural
  navigation controllers and the previous GuardrailTests namespace correction.
- Verified no old 10 m x 4 m stage labels/constants remain in live source/tests.
- Reviewed rectangular placement, current footprint replacement, spatial-hash removal,
  shape-specific furniture contacts, shared-field occupancy, rendering, save/replay and
  the fixed stage's unchanged single-instance rule.
- `git diff --check`: passed.
- ZIP CRC/inventory checks and byte comparison of the packaged files to the working source:
  passed during packaging.
- Applied the supplied patch to a fresh copy of the base; compared every result file with
  the packaged source: passed during packaging.

## Tests included, NOT executed here

`FixedAssetTests.cs`: 35 source-defined cases (15 facts, 20 inline cases).
`RectangularTableBlastTests.cs`: 20 source-defined cases (12 facts, eight inline cases).

The latter checks launch/landing, exact footprints/orientation, configurable central
destruction, full-height shielding, wall/stage contacts, boundary clearance, narrow-phase
contacts, long-table grid contacts, spatial-index relocation/removal, shared-field cleanup,
fume stability, authored-position replay and deterministic trajectories.

The entire remaining original test suite is retained. Counts above describe source cases,
not a completed xUnit run. No test was skipped to hide the reported failure.

## NOT verified in this environment

- `dotnet restore`, compilation, xUnit execution and benchmark execution: NOT RUN.
- `dotnet --info` returned `dotnet: command not found`; no SDK/runtime was available.
- Attempting to access the SDK installer failed because the external host did not resolve.
- OpenGL launch, rendered appearance, interaction and recording: NOT RUN.
- 600-agent CPU/GPU/frame-rate measurements: NOT MEASURED.

The source checks above do not establish that the solution builds or that tests pass.
Rebuild before invoking `dotnet test --no-build` on the Windows machine.

## Runtime/model limits

Rectangular tables retain their authored quarter-turn heading. They translate, fly, land
and slide using the existing lightweight furniture model, not full six-degree-of-freedom
rotation/fracture. Destruction removes their visible body and current navigation footprint;
no new fragment bodies are spawned. The existing abstract blast radii are reused, not a
real-world destruction/yield calibration. The stage remains fixed and non-walkable.

Only touched rectangular wall-grid cells are updated. Shared exit fields update rectangle
occupancy at the existing refresh cadence; local clearance uses current geometry. Stage
render chunks/static wall caches/fume precomputation are not rebuilt just because a table
moves. More movable furniture can still increase CPU time; no latency guarantee is made.

Existing saved stages load at the new dimensions and may need repositioning when they no
longer fit. Existing overlap/bounds validation is retained rather than silently accepting
invalid layouts. Save/restart restores intact tables at their authored positions.

## Reproduction

From the extracted folder containing `CrowdSim.sln`:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

The optional `benchmarks/RectangularAssetsLatency` runner includes 600 agents, 24 rectangular
tables, one enlarged stage and three seeds. See `docs/FIXED_ASSETS_AND_VISIBILITY.md` for
commands. It times CPU steps, not rendering or recording, and excludes fume precomputation.

Older verification notes/benchmark CSVs in the project are historical, not results for this
revision. Original dependency versions and existing project definitions are preserved.
