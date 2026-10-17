# Scenario save, replay and navigation correction

## User-visible behavior

The **SESSION** group is fixed at the top of the left panel so it remains visible even when the
builder/runtime controls extend beyond the panel height.

- **Save Scenario** saves the authored configuration under
  `%LOCALAPPDATA%\CrowdSimCs\scenarios` on Windows. It is available in Builder, Ready, Running and
  Paused states. The status bar reports success with the complete path or shows the exact failure.
- **Replay from Scene Start** is visible in Simulation mode. It stops the current run/recording
  and reconstructs the explicit checkpoint captured when **Finalise Scene** was pressed. The
  result is paused at frame 0, ready for **Start Simulation**.
- **Exit to Scenario Selection** returns to a genuinely fresh Explosion/Toxic Fume selection
  page. It no longer reuses the previous room dimensions, blast settings or behavioral tuning.
  Unsaved authored changes require confirmation before they are discarded.

The detailed **SCENARIO FILES** section remains available for editing the name, Save As, loading,
and refreshing the list.

## Persistence correction

The former save path depended on `Environment.CurrentDirectory`, which is not a reliable or
necessarily writable location for a desktop executable. New saves use the user's stable local
application-data directory. Existing JSON scenarios in the old working-directory `scenarios`
folder remain visible and loadable.

Saving uses a temporary file in the destination directory followed by an atomic replacement.
This prevents a failed/interrupted write from truncating an existing scenario. Temporary files
are removed in a `finally` block.

## Replay checkpoint

`FinalizedSceneCheckpoint` captures configuration at the Builder → Simulation transition. It
stores original starts, furniture transforms, exits, obstacles, fume configuration, behavior,
blast radii and random seed—not runtime injuries, agent movement, released hazards, debris or
chair damage. Toxic replay recomputes its derived plume cache against the restored static layout.

## Verification

- C# tree-sitter parsing: 98 files, no syntax errors.
- `CrowdSim.Core`: compiled successfully with .NET 8.0.413 Roslyn.
- `CrowdSim.OpenGL`: compiled successfully with .NET 8.0.413 Roslyn.
- 123 xUnit Fact/Theory cases invoked by a reflection-based runner: 123 passed, 0 failed.
- Executable lifecycle smoke test: scenario save/list/load and damaged-scene replay passed.

The container could not start the normal `dotnet test` CLI because its process-introspection
API fails before test execution. Run the standard test command on Windows to verify NuGet/MSBuild
integration:

```powershell
dotnet test .\CrowdSim.sln -c Debug --no-restore
```

Then manually confirm button placement and the OpenGL result:

1. Build a small scene and press **Save Scenario**. Confirm the success path in the status bar.
2. Finalise, start, trigger the hazard, then press **Replay from Scene Start**. Confirm frame 0,
   paused state, original occupant positions, seated agents and intact chairs.
3. Press **Exit to Scenario Selection**. Confirm the Explosion/Toxic Fume page appears and a new
   choice starts with default configuration.
