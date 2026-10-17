# Free-angle barricades and speed-control verification

## Scope

Base archive: `CrowdSimCs_Barricade_AssetDropdown.zip`.
Base SHA-256: `199f5f14c1c14b29ebf715b9c4fdc59fcacc07cd1eedce9e657f8a8ec98ac9c3`.

This revision adds exact rotated barricade footprints and free-angle placement controls,
parallel diagonal end snapping, and a persisted maximum voluntary running-speed control.
The stage and rectangular-table rotation restrictions and all existing asset dimensions
are retained. Barricades remain fixed. The setting is an agent locomotion ceiling, not
playback speed or a bound on blast/collision displacement.

## Completed checks

| Check | Result |
|---|---|
| Project XML and explicit compile/project-reference paths | Passed: 12 XML files |
| Lexical balanced delimiters across source/tests/benchmarks | Passed: 121 C# files; not a compiler |
| New declared xUnit cases | 65: 36 geometry/integration, 22 speed/persistence, 7 picker |
| Independent float32 swept-disc clearance vs Shapely polygon distance | Passed: 5,000 cases |
| Independent point/body clearance vs polygon distance | Passed: 5,000 cases |
| Independent separating-axis overlap vs polygon intersection | Passed: 5,000 cases |
| Independent disc separation against angled faces/corners | Passed: 5,000 cases |
| Flush end-contact checks across 360 headings, same/opposite facing | Passed: 720 cases |
| Positive 1 cm end-overlap rejection across the same headings | Passed: 720 cases |
| Patch application to a fresh base plus byte comparison | Passed (packaging script aborts on mismatch) |
| ZIP CRC/integrity and packaged-byte comparison | Passed (packaging script aborts on mismatch) |

The **21,440 numerical checks execute an independent Python/float32 port of the geometry
formulas against polygon reference operations**. They do NOT execute the C# classes, UI,
navigation system or xUnit tests. Results are recorded in
`validation/angle_geometry_reference_results.json`; the accompanying optional Python
script uses NumPy and Shapely, neither of which is needed to build/run the .NET project.
Randomized numerical comparisons allow 0.02 mm near-boundary tolerance; placement's
existing tolerance is 0.1 mm. These do not validate human-behaviour or safety performance.

The obsolete quarter-turn barricade row in `BuilderAssetSelectionTests` was removed and
replaced by free-angle tests. The table/stage rows remain. No previous passing xUnit claim
is transferred to this revision.

## Source audit

- Barricade axes and broad-phase bounds are immutable/cached after construction.
- Placement, point clearance, swept clearance, static route corners, grid occupancy,
  crowd correction, collision normals, furniture impacts and ground previews use the
  oriented footprint; the spatial hash still uses enclosing bounds to shortlist.
- Normal AABB walls and movable axis-aligned tables keep their existing fast paths.
- The builder's preview and placement share snapping and shape construction.
- Native R polling recognises Shift+R and respects text-entry/canvas ownership.
- The speed ceiling is included in behavior capture, validation, apply and reset.
- SFM, hybrid desired-speed calculations, wandering and legacy recovery motor commands
  respect the ceiling. Injury/fume factors remain speed reductions. The core physical
  blast system and its separate velocity guard are unchanged.
- Existing natural-navigation workload budgets are unchanged. No new render pass or
  per-barricade rigid body is introduced; no runtime latency claim is made.

Byte-identical files audited against the base:

- `Directory.Packages.props`
- `Directory.Build.props`
- `CrowdSim.sln`
- `src/CrowdSim.Core/Systems/HybridMovementSystem.cs`
- `src/CrowdSim.Core/Systems/ToxicFumeSystem.cs`
- `src/CrowdSim.Core/Systems/ExplosionSystem.cs`
- `src/CrowdSim.OpenGL/Rendering/WorldRenderer.AgentOutlines.cs`

## Not verified

**No .NET SDK/compiler is installed in this environment.** The project has not been
restored, compiled or executed here. The 65 new xUnit cases and the full regression
suite have not been run; C# type checking remains unverified. OpenGL/UI appearance,
keyboard interaction and 600-agent wall-clock latency have not been measured. Earlier
benchmark files and reports retained in the archive are historical, not results for this build.

## Verification on the target Windows machine

Extract into a new folder, open PowerShell at `CrowdSim.sln`, then:

```powershell
dotnet restore .\CrowdSim.sln
dotnet build .\CrowdSim.sln -c Release --no-restore
dotnet test .\tests\CrowdSim.Tests\CrowdSim.Tests.csproj -c Release --no-build --no-restore
dotnet run --project .\src\CrowdSim.OpenGL\CrowdSim.OpenGL.csproj -c Release --no-build
```

Do not use `--no-build` until rebuilding this version. The helper
`scripts/verify-barricade-angles-speed.ps1` fails fast on restore/build/test failure.
See `docs/FREE_ANGLE_BARRICADES_AND_SPEED.md` for manual acceptance and seeded
600-agent benchmark commands. The benchmark's optional sixth positional argument is
the barricade angle in degrees; prior invocations remain supported.
