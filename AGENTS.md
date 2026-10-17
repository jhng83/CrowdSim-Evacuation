# CrowdSim-Evacuation: instructions for coding agents

## Scope and first steps

These instructions cover this repository. Read this file before editing. Read
[docs/DEVELOPMENT_GUIDE.md](docs/DEVELOPMENT_GUIDE.md) for the code map and validation
workflow, and [docs/CODEX_CLOUD.md](docs/CODEX_CLOUD.md) for cloud environment setup.
Read the relevant implementation and tests before changing behavior.

This is a C#/.NET 8 desktop evacuation simulator with Explosion and Toxic Fume
scenarios, Silk.NET/OpenGL rendering, and Dear ImGui controls. It is not a web app
or a Unity project. Work in the current C# implementation unless the task explicitly
requests a migration. The repository source and tests describe current behavior;
older delivery reports describe the revision at which they were written.

Briefly state the intended change, then implement and validate it. Ask a concise
question only when a missing decision materially affects correctness or scope.
Use reasonable judgment for routine implementation choices. Do not stop at a plan
when the task requests working changes.

## Build and test

Run from the directory containing `CrowdSim.sln`, using a .NET 8 SDK:

```bash
dotnet restore CrowdSim.sln
dotnet build CrowdSim.sln -c Release --no-restore
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore
```

For a desktop smoke test after building:

```bash
dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build -- --seed 42
```

If setup installed the SDK privately, use the executable path documented in
`docs/CODEX_CLOUD.md` instead of `dotnet`. Do not use `--no-build` after changing
source until the matching configuration has been rebuilt. A graphical desktop
and working OpenGL context are required for the application, but not the core
tests. A headless GUI launch failure is not evidence of a simulation failure.

## Architecture and change boundaries

- `src/CrowdSim.Core/`: simulation, geometry, navigation, hazards, furniture and
  scenario persistence. Keep it independent of Silk.NET, ImGui and window APIs.
- `src/CrowdSim.OpenGL/`: application timing/input, UI, camera, rendering and video.
  Rendering may keep visual caches; it must not mutate physical agent state.
- `tests/CrowdSim.Tests/`: xUnit regressions and JSON fixtures. Pure UI helpers are
  linked into the test project so they can be tested without a graphics context.
- `benchmarks/`: optional CPU comparisons. `benchmarks/baseline/` is a historical
  comparison implementation, not the active core; do not edit it for normal fixes.
- `scenarios/`: authored examples. Preserve save/load compatibility when changing
  configuration or persistence.

Find the owning partial class or system rather than adding unrelated code to a
large central file. Keep UI-only changes in the presentation layer. Avoid broad
refactors, dependency upgrades and whole-file formatting unrelated to the task.

## Simulation rules to preserve unless explicitly changed by the task

- Simulation advances in fixed steps using `Config.Agent.Dt`, not render FPS.
  The current reference step is 0.15 seconds. Positions use metres; public velocity
  values retain the metres-per-reference-step convention documented in
  `AgentConfig`. Check conversions before changing movement, timers or speed caps.
- Preserve finite-value checks, boundary containment, obstacle/casualty collisions,
  individual contact handling, exit admission, and existing movement speed limits.
  Do not bypass these to make a scene evacuate faster or hide a numerical fault.
- Use the simulation's seeded random generator for simulation randomness. Avoid
  introducing wall-clock or render-dependent behavior into the model. Preserve
  deterministic ordering where existing outcomes depend on it.
- Hybrid crowd mode is an optional approximation for eligible agents, disabled by
  default. With it off, agents still use individual SFM **and navigation**. With it
  on, shared steering does not merge individual positions, collisions or exposure.
  Preserve detailed fallback near hazards/obstacles/congestion and for injured
  agents. Do not describe disabling Hybrid as disabling route planning.
- Toxic Fume currently uses a setup-time, obstacle-aware normalized field cache,
  with runtime concentration/exposure queries. It is not chemical CFD or NIST FDS.
  Preserve cache invalidation/precompute requirements. Do not add FDS or a chemical
  solver unless the current task explicitly requests one.
- Perimeter walls are visually transparent but retain physical geometry,
  containment, exit placement, navigation and hazard interactions. Do not delete
  room boundaries or hide authored internal walls/blockages as a visual shortcut.
- Runtime humanoids retain simplified world-space 3D bodies at low zoom and more
  detail close up. Do not restore the removed flat screen-space humanoid fallback.
  Builder planning markers are separate from runtime body rendering.
- The right inspector can be hidden/shown with its top-bar button or F4. Hiding
  expands the viewport and retains the active tab, selected agent and debug flags;
  overlays continue working. Selecting an agent alone must not force it open.
  Respect text-entry focus and avoid intercepting Alt+F4.
- Saved scenarios describe authored configuration/initial placement rather than
  serializing current injuries, panic or moved runtime positions. Preserve the
  distinction between saved definitions and finalized replay checkpoints.

## C# and dependency conventions

Follow `.editorconfig`: four spaces, LF, file-scoped namespaces, explicit types,
and existing brace style. Use C# 12, nullable annotations and typed APIs. Write XML
documentation for new public APIs and comments for non-obvious algorithm choices.
Prefer small cohesive methods and the existing partial-class organization.

`Directory.Build.props` defines shared build settings. `Directory.Packages.props`
is the source of direct NuGet versions; project references use central versions.
`requirements.txt` is an informational .NET inventory, not a pip requirements file.
Keep versions pinned. If a task needs a dependency change, explain why and update
the inventory consistently. Do not change the target framework as an incidental fix.

## Validation and honest reporting

For code changes, build Release and run relevant regressions; run the complete
test project before handing off when feasible. Add focused tests for corrected
behavior, numerical boundaries or regressions. Do not weaken assertions, remove
fixtures or change expected outcomes just to make a failing test pass.

For UI changes, test extracted layout/state logic where appropriate and visually
check the actual application when graphics are available. Verify both scenarios,
inspector hide/show, selection, and the affected zoom/input paths. A passing
headless layout test does not prove native input or appearance works.

For documentation-only changes, check commands, paths and links; no simulation
rerun is required. If restore, tests or graphics are blocked, give the exact command
and failure reason. Never report historical screenshots, JSON reports or a prior
404-test run as validation of a new change. Do not alter scientific parameters to
work around infrastructure failures.

## Repository hygiene and handoff

Do not commit `bin/`, `obj/`, SDK downloads, dependency caches, local IDE state,
recordings or unrelated generated outputs. Use a temporary directory for benchmark
output unless the task explicitly asks to update checked-in results. Do not add
credentials. Preserve unrelated user changes; avoid destructive Git commands.

Update the relevant guide when behavior or setup changes. Historical reports and
`docs/COMPLETE_SOURCE.md` are snapshots, not alternative implementation files;
do not routinely regenerate them or claim they stay synchronized with current code.

Finish with what changed, why, the validation actually performed, and any remaining
limitation. For a PR, describe final behavior and review-relevant evidence. Do not
merge or push to `main` unless requested by the user.

## Code Review Rules

Flag changes that accidentally remove physical perimeter geometry, mutate physics
from rendering, tie simulation integration to render FPS, bypass collision/speed
guards, or break scenario compatibility. Flag hidden-inspector state loss and
reintroduced flat runtime agents. For hazard/navigation changes, check both
scenarios and the relevant Hybrid-off/Hybrid-on paths. Distinguish abstract fume
behavior from calibrated CFD and measured CPU timings from application FPS.
