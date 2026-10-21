# Unity migration — Milestone 8

On `unity-migration`, open **UnityProject/** (containing **Assets/**, **Packages/**,
**ProjectSettings/**) with **Unity 6000.3.26f1**, then
**Assets/CrowdSim/Scenes/Bootstrap.unity**. M8 adds complete volumetric humanoid
poses/animation/indicators, rendered-body selection, Follow and head-height POV.
A separate **Assets/CrowdSim/Scenes/Validation/HumanoidPresentation.unity** scene
shows 14 deterministic pose fixtures. The bundled Atkinson Hyperlegible font,
larger text, focus boundaries and checked text contrast support the requested design.

M1–M7 were accepted by the user; M8 awaits local testing. Follow the numbered
[M8 checklist](docs/unity-migration/M8_LOCAL_TESTING.md) and
[migration status](docs/unity-migration/STATUS.md). Cloud passes 404 original,
415 portable and 159 migration tests; actual M8 Unity import/tests/input/rendering
were not executed. The original application/tests below remain the behavioural reference.

# Always-3D agents — current revision

This update builds on **CrowdSimCs_TransparentPerimeterWalls.zip**. Agents now use simplified 3D humanoids when zoomed out, with more detail as you zoom in. The flat screen-space fallback has been removed.

Transparent perimeter walls, the professional workspace UI and all simulation behaviour are retained. Dependencies and tests are unchanged.

**Release build passed; all 404 regression tests passed.** Native OpenGL checks covered 300 agents at minimum/default/maximum zoom and a Toxic Fume scene with 205 occupants. See [the guide and previews](docs/ALWAYS_3D_AGENTS.md).

Extract into a new folder and rebuild before using --no-build. Complete source, tests and setup instructions are included.

---

## Earlier revision notes

# Transparent perimeter walls — current revision

Using **CrowdSimCs(20261012-031833).zip** as the exact base, this update makes only the surrounding room walls fully transparent. Their physical and interaction behaviour remains intact. All other C# files and pinned dependencies are unchanged.

**Release build passed; all 404 existing tests passed.** Native OpenGL previews verified the change in both scenarios. See [the wall-change guide](docs/TRANSPARENT_PERIMETER_WALLS.md) and its screenshots.

Extract into a new folder and rebuild before running with --no-build. The updated source is in src/CrowdSim.OpenGL/Rendering/WorldRenderer.cs.

---

## Previous UI revision notes

# CrowdSim — professional workspace UI

This revision updates the attached CrowdSimCs(10) project with a clearer desktop workspace and a hideable **right inspector**.

- A persistent **Hide inspector / Show inspector** button in the top bar, plus **F4**.
- The simulation viewport expands when the inspector is hidden. Its active page, selected agent and debug settings are retained during the session.
- Left-side **Build/Run**, **Camera** and **Files** pages, with a fixed **Finalise / Start / Pause / Resume** area.
- Inspector pages for **Overview**, **Blast/Fume**, **Agent** and **Debug**, with summary cards and independently scrolling content.
- Clearer scenario cards, labels, help text, spacing, colours and button contrast.

The existing simulation engine and all pinned package versions are preserved. All 42 source/project files in CrowdSim.Core are byte-identical to the attachment.

## Run

Install the .NET 8 SDK, extract into a new folder, and run from the directory containing CrowdSim.sln:

```powershell
dotnet restore CrowdSim.sln
dotnet build CrowdSim.sln -c Release --no-restore
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build -- --seed 42
```

A desktop session with an OpenGL-capable driver is required. The seed argument is optional.

See [run_instructions.md](run_instructions.md) for the complete workflow, [UI guide](docs/PROFESSIONAL_UI.md), [verification](docs/PROFESSIONAL_UI_VERIFICATION.md), and [complete source in code blocks](docs/COMPLETE_SOURCE.md).

## Validation

Release build succeeded using .NET SDK 8.0.415. The actual xUnit 2.9.3 discovery/execution engine ran **404 tests: 404 passed, 0 failed, 0 skipped**, including 12 new layout/state cases. Native Silk.NET/OpenGL screenshots and mouse/keyboard checks covered 1600×900 and 1280×720 layouts, inspector hiding/restoring, retained debug options, selected-agent reveal, and the Explosion/Toxic Fume screens. Windows-specific rendering was not exercised here.

The screenshots are under docs/previews. Historical project notes are retained in docs/BASELINE_README.md and docs/BASELINE_RUN_INSTRUCTIONS.md.
