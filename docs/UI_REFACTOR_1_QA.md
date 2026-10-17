# UI Refactor 1 - QA Notes

## Static checks completed

- only `CrowdSim.OpenGL` UI/application files were modified;
- no `CrowdSim.Core` simulation or guardrail code was changed;
- all C# source files have balanced braces and parentheses;
- project XML and scenario JSON remain parseable;
- no TODO/FIXME/NotImplemented placeholders were introduced;
- the obsolete floating blast-zone window is no longer invoked;
- the ZIP archive passes integrity testing.

## Required local validation

The current execution environment does not contain the .NET SDK. On Windows/VS Code run:

```powershell
dotnet restore .\CrowdSim.sln --force --disable-parallel --no-cache
dotnet build .\CrowdSim.sln -c Debug --no-restore
dotnet test .\CrowdSim.sln -c Debug --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Debug --no-build
```

Expected pre-existing regression total: **60 passed, 0 failed**.

## Manual UI acceptance checklist

- top and bottom status bars remain readable at the default 1600x900 window size;
- left and right panels reserve space instead of covering the 3D grid;
- Builder and Simulation modes show the appropriate controls;
- the right inspector switches among Blast, Agent, Metrics and Debug tabs;
- single-clicking a blast radius displays a caret rather than selecting the whole number;
- Enter and focus loss commit valid radii;
- Escape restores the prior value;
- invalid order/range values do not alter the live configuration;
- radius fields lock after detonation while zone visibility remains adjustable;
- agent selection, follow and POV continue to work;
- scenario save/load and unsaved-change indication work;
- simulation behaviour is unchanged from Behaviour Refinement 2.
