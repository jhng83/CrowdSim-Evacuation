# Shared natural-navigation source verification

Revision: Explosion + Toxic Fume, 15 September 2026.
Baseline: CrowdSimCs_NaturalNavigation.zip, the preceding Explosion revision the user reported working.
Baseline SHA-256: `dbaa5dd0a3f7709d1d58ee7be1ea533e938e9b7f83d75293e7238b0a4f054b17`.

## Checks actually executed

- Compared the new source against the exact preceding ZIP; no project was reconstructed from memory.
- Lexed and checked delimiter balance in 10 added/modified C# files using Pygments.
- Parsed 12 project/props XML files and 7 JSON files; checked solution project paths.
- Confirmed the existing package inventory, hazard simulation, furniture physics, SFM and original
  Toxic Fume regression class are byte-for-byte unchanged (hashes below).
- Inspected both normal locomotion and optional hybrid call sites, and the selected-agent UI gate.
- Checked that the new normal-navigation FIFO defaults remain 4 route searches and 24 recovery
  probes per step; candidate loops remain 3 distances x 12 headings.
- Checked that each new local plume observation has at most five concentration samples, uses
  current release time, and does not read preview time or call plume precomputation.

Lexical/XML/source checks are NOT C# compilation, type checking, test execution, behavioral
validation, measured latency, or a proof that every trapped pedestrian can escape.

## Not executed here

`dotnet restore`, `dotnet build`, `dotnet test`, OpenGL GUI launch, Windows recording tests,
600-agent runtime trials, and the paired benchmark were not executed. This environment has
no dotnet SDK/compiler/runtime. Direct and tool-based SDK download attempts were unavailable.
There are no new passing-test counts or timing/FPS results to report. The prior ZIP working
on the user's machine does not establish that this new source revision has been run.

## Regression specifications included

`ToxicNaturalNavigationTests.cs` contains 16 facts and 2 theory rows (18
executable cases), covering shared enabling, unreleased/current/future plume distinction,
perception horizon and wall occlusion, clean recovery preference, contaminated detour
cancellation, improving retreat retention, noncompeting steering, hazard-driven exit changes,
hybrid integration, Explosion-only furniture avoidance, incapacitated-agent exclusion,
600-request deduplication, FIFO fairness, backward retreat and seeded reproducibility.

The earlier assertion that Toxic Fume must disable natural recovery was intentionally replaced
with a disabled-toggle theory for BOTH scenarios. No unrelated assertion was removed.
The original ToxicFumeScenarioTests.cs is unchanged and still covers permanent incapacitation.

## Local verification

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Paired Toxic Fume CPU benchmark (not rendered-frame latency):

```powershell
dotnet run --project .\benchmarks\NaturalNavigationLatency\NaturalNavigationLatency.csproj -c Release -- .\benchmark-results\toxic-natural-navigation 400 600 false ToxicFume
```

The old four-argument benchmark call still defaults to Explosion. Fume precomputation is
setup-only and outside step timings. Both natural-navigation toggle values are compared;
`false` in the command above controls hybrid movement. Use the same hardware, scene, seed,
hybrid setting and recording setting when comparing actual rendered performance.

## Preserved source hashes (SHA-256)

- `Directory.Packages.props`: `3e8c0849d91ea9f2b155943028907794157284366324c7c8e8e2f2e8a1f0d5d9`
- `src/CrowdSim.Core/Systems/ToxicFumeSystem.cs`: `3090f68965bb59e629bd745b4138f820681797828058e8e82af17bc0eb8e6502`
- `src/CrowdSim.Core/Systems/ExplosionSystem.cs`: `a2348069d61aa86ff8c3ca6c347b3eb51422acfa34e0799057d02c158d89c037`
- `src/CrowdSim.Core/Simulation.Furniture.cs`: `39b94dc717f81df64df88e66daf41ab85658689070e0e3b45dd1e6129af55a34`
- `src/CrowdSim.Core/Systems/SfmEngine.cs`: `3671ee4d50502911955bc7a467b3e10c52ff55aa951948011f261efdf0f666c7`
- `tests/CrowdSim.Tests/ToxicFumeScenarioTests.cs`: `3a26c7d7c1341d2f0b8db3acbd1c9a7f87458a8765443b77919294bbb1950f37`

## Added/modified C# checked

- `benchmarks/NaturalNavigationLatency/Program.cs`
- `src/CrowdSim.Core/Configuration/SimulationConfig.cs`
- `src/CrowdSim.Core/Model/NaturalNavigationMemory.cs`
- `src/CrowdSim.Core/Systems/EvacuationSystem.NaturalNavigation.cs`
- `src/CrowdSim.Core/Systems/EvacuationSystem.ToxicNavigation.cs`
- `src/CrowdSim.Core/Systems/EvacuationSystem.cs`
- `src/CrowdSim.Core/Systems/HybridMovementSystem.cs`
- `src/CrowdSim.OpenGL/UI/SimulationUi.cs`
- `tests/CrowdSim.Tests/NaturalNavigationTests.cs`
- `tests/CrowdSim.Tests/ToxicNaturalNavigationTests.cs`
