# Transparent room perimeter walls

Base: CrowdSimCs(20261012-031833).zip, the attached version.

Only the surrounding room walls are now fully transparent. The floor, grid, exit markers, internal blue walls, square/circular blockages, furniture, stages and orange barricades retain their existing appearance. This applies through the shared world renderer in both scenarios, builder/runtime and camera modes.

## How it works

WorldRenderer.DrawWall skips drawing wall chunks whose renderer flags identify them as neither blockages nor internal blue walls. The environment constructs Walls from its four perimeter sides, authored blockages, blue walls and furniture. Furniture already uses its own rendering path. No physical wall or opening is removed, resized or reclassified.

Collision, repulsion, room bounds, exit placement/capture, navigation, line-of-sight, blast occlusion and toxic-fume interactions continue to use the original geometry. All other C# source files, including every CrowdSim.Core file and every test, are byte-identical to the attachment. Package versions are unchanged.

## Build and run

Extract into a new folder and run beside CrowdSim.sln:

```powershell
dotnet restore CrowdSim.sln
dotnet build CrowdSim.sln -c Release --no-restore
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build
```

Rebuild before using --no-build. Stale bin/obj output from the attachment is excluded from this source release.

## Verification

Release solution build passed with .NET SDK 8.0.415. The existing xUnit 2.9.3 engine ran all **404 cases: 404 passed, 0 failed, 0 skipped**, using in-process execution in the delivery environment. The normal test suite and test project are unmodified. The pre-existing xUnit2031 advisory remains.

The native OpenGL application was rendered and inspected at 1600×900 for the Explosion builder, a ready Explosion scene with 50 agents, and the Toxic Fume builder with 205 planned occupants. Perimeter prisms are absent; exits, obstacles, furniture and occupants remain visible. Windows-specific appearance was not exercised here.

- [Explosion runtime preview](previews/explosion-runtime-transparent.png)
- [Toxic Fume builder preview](previews/fume-builder-transparent.png)

Exact checks are recorded in validation/transparent-walls-verification.json; per-test results are in validation/transparent-walls-regression-results.json. Earlier verification reports describe earlier revisions.

## User checks

1. Open either scenario: the grey raised perimeter walls should be invisible, with the floor and cyan exit markers still present.
2. Place blocks, furniture and barricades: their appearance and placement rules should behave as before.
3. Place an exit along a floor edge: the original wall-side snapping still applies.
4. Run the simulation: agents should remain constrained by the invisible perimeter and evacuate through the existing exits.
5. Toggle the inspector or change camera views: perimeter walls should remain invisible.
