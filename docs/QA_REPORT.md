# QA Report

## Static QA completed in this workspace

- C# solution contains only `CrowdSim.Core`, `CrowdSim.OpenGL` and `CrowdSim.Tests`;
- no OpenCV package/project/source references;
- all `.csproj` and `.props` XML parsed successfully;
- all `.vscode/*.json` files parsed successfully;
- all C# source files passed comment/string-aware delimiter/bracket structural scanning;
- no `TODO`, `FIXME`, `NotImplementedException` or placeholder source markers;
- Stage 1-3 changes were checked for stale method names/signatures;
- regression coverage includes panic-induced SFM, shock-front timing, running-crowd contagion, wall-blocked contagion, temporary visibility degradation/recovery, wall-shielded visibility, rerouting, debris impact and debris-wall interception;
- final ZIP integrity is checked after packaging.

## Compiler limitation of this workspace

This execution container does not currently provide a .NET SDK and cannot reach the SDK download host from its runtime network, so `dotnet build` / `dotnet test` cannot be executed here. The prior FIX6 branch had already progressed through compilation on the user's Windows/.NET environment; this Stage 1-3 revision changes core behaviour and therefore **must still be compiled and tested locally** before it is treated as validated.

Run these from the extracted folder as the authoritative check:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Debug
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj
```

If the compiler reports an error, keep the complete terminal output including file, line and error code.
