# Development guide

## Current implementation

CrowdSim-Evacuation is a native .NET 8/C# 12 application. Silk.NET creates the
window, input and OpenGL backend; ImGui.NET supplies controls and projected drawing
lists. SharpAvi and SharpAvi.ImageSharp support recording. The core has no direct
NuGet package dependencies. xUnit and Microsoft.NET.Test.Sdk provide tests. Exact
direct versions live in `../Directory.Packages.props` relative to this guide.

The simulation is planar with raised 3D presentation. A 3D humanoid or fume overlay
does not mean the underlying model solves three-dimensional fluid dynamics.

## Where to change things

| Area | Primary source | Useful tests |
| --- | --- | --- |
| Initialization, state and fixed steps | `src/CrowdSim.Core/Simulation.cs`, `Simulation.Step.cs` | `SimulationBasicTests`, `GuardrailTests` |
| Social forces and locomotion | `Systems/SfmEngine.cs`, `Simulation.Collisions.cs`, `Simulation.Contacts.cs` under Core | `AgentRunningSpeedTests`, `PanicMovementFixTests`, `BottleneckBehaviorTests` |
| Panic, exit choice and routes | Core `Systems/EvacuationSystem*.cs`, `VisibilityGraphPlanner.cs`, `FurnitureGridPlanner.cs` | `NaturalNavigationTests`, `OpenSpaceDetourTests`, `LocalNavigationRecoveryTests`, `RouteRetryCostTests` |
| Hybrid optimization | Core `Systems/HybridMovementSystem.cs`, `SharedExitFields.cs` | `HybridMovementTests` |
| Explosion and debris | Core `Systems/ExplosionSystem.cs` | `FurnitureBlastTests`, `RectangularTableBlastTests`, `GuardrailTests` |
| Toxic-fume cache/exposure | Core `Systems/ToxicFumeSystem.cs`, `Configuration/ToxicFumeConfig.cs` | `ToxicFumeScenarioTests`, `ToxicNaturalNavigationTests` |
| Room, furniture and geometry | Core `Model/SimulationEnvironment*.cs`, `Geometry/`, `Simulation.Furniture*.cs`, related partials | `FurnitureTests`, `FixedAssetTests`, `BarricadeAngleTests`, `ChairChainPushTests` |
| Save/load and replay | Core `Persistence/ScenarioDefinition.cs`, `ScenarioFileStore.cs`, `FinalizedSceneCheckpoint.cs` | `ScenarioFileStoreTests`, `Fix15Tests` |
| Window, input, accumulator | `src/CrowdSim.OpenGL/CrowdSimApplication.cs`, `Program.cs` | Native smoke test plus affected core tests |
| Panels and tools | OpenGL `UI/SimulationUi*.cs`, `DashboardLayout.cs`, `InspectorPanelState.cs` | `DashboardLayoutTests`, builder tests plus native smoke test |
| Agents, walls and camera | OpenGL `Rendering/WorldRenderer*.cs`, `Camera3D.cs` | Native checks at low/default/high zoom |
| Video output | OpenGL `Recording/SimulationVideoRecorder.cs` | Desktop record/start/stop smoke test |

Test class names above correspond to files in `tests/CrowdSim.Tests/`. Core and
OpenGL in the table refer to their respective directories under `src/`.

## Model flow and units

`CrowdSimApplication` accumulates elapsed time and advances `Simulation.Step()` at
the configured fixed interval, with a catch-up cap. Each step handles the selected
hazard, panic/reactions, furniture, navigation/Hybrid preparation, individual
states/movement and contact/exit bookkeeping. Inspect the full method before
changing ordering: one subsystem's output can affect another in the same step.

`AgentConfig.ReferenceStepSeconds` is 0.15 seconds. `Dt` and `StepScale` connect
legacy per-reference-step quantities with integration. Public velocities are
metres per reference step; force calculations convert to SI. Do not assume every
field with "speed" in its name is metres per second. Check configuration comments
and the existing conversion at each call site.

Route selection, steering and physical contact are separate responsibilities.
Hybrid-off keeps normal navigation and detailed SFM. Hybrid-on can reuse exit
fields and steering for compatible healthy agents, with individual bodies and
detailed fallback. Benchmark those modes from the same starting scene and seed.

Toxic Fume precomputes obstacle-aware arrival/peak fields during setup; runtime
queries use that cache. Its normalized concentration is an abstract stimulus,
not a calibrated chemical concentration. Changes to source/layout/configuration
must continue to invalidate the appropriate cache.

## Build and focused tests

From the repository root:

```bash
dotnet restore CrowdSim.sln
dotnet build CrowdSim.sln -c Release --no-restore
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore
```

For a focused iteration after rebuilding:

```bash
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~DashboardLayoutTests'
```

Substitute the relevant class. Tests link three pure UI helper files rather than
loading the native application. If adding another independently testable helper,
follow the existing test project structure deliberately; do not introduce a window
dependency into core tests.

Use fixed seeds and bounded frame counts for behavioral regressions. Assert
meaningful outcomes such as finite positions, collision clearance, successful
progress, state transitions and retained configuration. Avoid timing-sensitive
unit assertions about wall-clock performance. Test save/load changes against old
definitions and the fixture `tests/CrowdSim.Tests/Fixtures/toxic_fume_205.json` where
relevant; use temporary directories rather than overwriting user saves.

## Desktop acceptance checks

Launch after a successful build:

```bash
dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build -- --seed 42
```

For a presentation change, exercise the affected controls and these preserved
behaviors in Explosion and Toxic Fume:

1. Build or load a scene; finalise, start, pause and resume it. For fume, place the
   release point and compute dispersion before finalising.
2. Hide/show the right inspector using the button and F4; verify viewport resizing,
   retained tab/debug options, and continuing overlays. Test text-entry focus.
3. Select an agent with the inspector hidden; selection stays active without
   forcing the panel open. The viewport agent button explicitly opens its details.
4. Check low/default/high zoom (current slider range 0.45-2.50): runtime agents keep
   their raised 3D forms and appropriate states/poses. Inspect room edges, internal
   walls and furniture. Only perimeter wall drawing is suppressed.
5. For persistence changes, save/load an authored scene and replay from its
   finalized start. Runtime injuries/panic must not become authored starting data.

Record screenshots only from the application actually tested. When cloud graphics
are unavailable, report the checks as pending rather than inferring success from
unit tests or existing preview images. See [CODEX_CLOUD.md](CODEX_CLOUD.md).

## Optional performance comparison

The Hybrid runner is outside the solution and restores independently:

```bash
dotnet run --project benchmarks/HybridScale/HybridScale.csproj -c Release -- /tmp/crowdsim-hybrid-results 400
```

Use a fresh output path (or a Windows temporary folder) to avoid overwriting
checked-in results. It compares reference/Hybrid in open/furnished rooms at
220/500/1,000 agents with fixed seed 731. It measures CPU step durations and
allocations, not graphics FPS or calibrated evacuation realism. Record hardware,
runtime, build, seed and scene with any comparison. Run only when relevant to the
change; this is not a mandatory check for every UI or documentation edit.

## Reading existing documentation

Start with [../run_instructions.md](../run_instructions.md) for user workflows.
Read the implementation together with these focused notes as needed:

- [PROFESSIONAL_UI.md](PROFESSIONAL_UI.md)
- [TRANSPARENT_PERIMETER_WALLS.md](TRANSPARENT_PERIMETER_WALLS.md)
- [ALWAYS_3D_AGENTS.md](ALWAYS_3D_AGENTS.md)
- [NATURAL_NAVIGATION.md](NATURAL_NAVIGATION.md)
- [TOXIC_FUME_SCENARIO.md](TOXIC_FUME_SCENARIO.md)
- [HYBRID_1000.md](HYBRID_1000.md)

`ARCHITECTURE.md`, delivery QA documents, preview images, `validation/` outputs and
`COMPLETE_SOURCE.md` include historical snapshots. For example, older notes can
say a feature was untested before a later delivery tested it. Their status does
not establish current build/test results. Edit real `.cs` files, not copied source
inside documentation; keep new handoff claims tied to commands actually run.
