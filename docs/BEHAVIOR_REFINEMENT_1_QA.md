# Behaviour Refinement 1 - QA Notes

## Static checks completed

- all C# source files passed delimiter/brace/parenthesis balance checks after comments and strings were removed;
- project XML and JSON scenario files parse successfully;
- no `bin` or `obj` directories are included in the delivery;
- scenario JSON contains the new behaviour section and older files remain compatible through default property initializers;
- all new tuning inputs are finite-checked and clamped;
- physical body/contact radius remains independent from panic-related social-space compression;
- existing force, acceleration, speed, collision, wall, exit-capacity and non-finite guardrails were not removed;
- four new tests were added and two existing regression areas were extended.

## Expected local validation

```powershell
dotnet restore .\CrowdSim.sln --force --disable-parallel --no-cache
dotnet build .\CrowdSim.sln -c Debug --no-restore
dotnet test .\CrowdSim.sln -c Debug --no-build --no-restore
```

Expected result: **54 passed, 0 failed**.

## Environment limitation

The packaging environment does not contain the .NET SDK, so the authoritative compile and runtime validation remains the local VS Code/.NET 8 run above.
