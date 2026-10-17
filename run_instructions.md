# Current revision: always-3D agents

Agents retain a simplified 3D body at low zoom and use additional detail at close zoom. No extra setting is needed. The surrounding walls remain transparent.

Extract into a new folder, then follow the build/run commands below. Open **Camera** and test the Zoom slider from **0.45× to 2.50×** after finalising a scene. See docs/ALWAYS_3D_AGENTS.md for checks and validation.

---

# Current revision: transparent perimeter walls

Only the visual room perimeter is hidden. Use the setup/build commands below; there is no new setting to enable. The physical room bounds and all existing interactions remain active.

See docs/TRANSPARENT_PERIMETER_WALLS.md for validation and user checks. Extract to a fresh folder and rebuild; old compiled output is not included.

---

# Setup and execution

## 1. Prerequisites

Install the **.NET 8 SDK** and use a desktop with an OpenGL-capable graphics driver. This project targets net8.0 with C# 12. Visual Studio 2022 with .NET desktop development support is an optional alternative to the command line.

The validated SDK was **8.0.415**. All direct NuGet versions are pinned in Directory.Packages.props; requirements.txt is an informational .NET package inventory, not a Python/pip file.

## 2. Extract and build

Extract the archive to a new folder and open a terminal in CrowdSimCs, beside CrowdSim.sln. Do not reuse the old project's bin/obj folders.

```powershell
dotnet restore CrowdSim.sln
dotnet build CrowdSim.sln -c Release --no-restore
```

The same forward-slash commands work in PowerShell, Command Prompt and a Linux/macOS shell with the SDK installed.

## 3. Run the tests

```powershell
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build --no-restore
```

There are 404 expanded cases, including tests for panel width allocation and inspector page retention. See docs/PROFESSIONAL_UI_VERIFICATION.md for the delivery environment's execution details.

## 4. Launch

```powershell
dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build -- --seed 42
```

Remove `-- --seed 42` to use a random seed. Open CrowdSim.sln in Visual Studio and select CrowdSim.OpenGL as the startup project if preferred.

## 5. Build a scene

1. Choose **Explosion** or **Toxic Fume**, or open a saved scene.
2. On the **Build** page, set **1. Room size** before placing items.
3. Open **2. Exits**, choose the width and click **Place Exit**. Click a boundary wall in the scene.
4. Use **3. Assets & furniture** to choose and place objects. Rotation controls appear for applicable assets. Ctrl+Z undoes the last placement; R/Shift+R rotate supported placement previews.
5. Open **4. People**, choose a group size, click **Place Agents** and click clear floor. Seated furniture includes its occupants.
6. For Toxic Fume, set the release point and **compute dispersion** in the builder's fume section before finalising. The control page scrolls independently.
7. Click the fixed **Finalise Scene** button. If disabled, read the reason immediately below it; hover truncated text for its full message.

## 6. Run and inspect

- **Start simulation / Pause simulation / Resume simulation** stay visible beneath the left control page. Select **Record this run** before a fresh start to record AVI output.
- The **Run** page controls playback speed and event placement/release. Start before placing the explosion or releasing fumes.
- **Camera** offers labelled rotation, elevation and zoom controls. The mouse wheel over the scene also zooms.
- Click **Select Agent** on Run, then click near a person's feet in the scene. The **Agent** inspector opens its details; use its scrolling area for Follow and POV actions.
- **Overview** shows summary metrics. **Blast/Fume** shows hazard settings. **Debug** contains performance data, overlays and legends.
- **Hide inspector** in the top-right corner hides the whole right panel and expands the scene. **Show inspector** restores it. **F4** performs the same toggle outside text entry. Visibility and active page are retained during the current session.
- Selecting an agent while hidden retains the hidden panel. Click the **Agent N** button above the viewport to explicitly reopen its details.
- Debug overlays remain enabled while their settings panel is hidden.
- **Replay from scene start** restores the finalised scene checkpoint at frame zero; start it again when ready.

## 7. Save and load

**Save scene** is always available near the top of the left panel. On **Files**, edit the scenario name, save, refresh the list or load a saved configuration while paused. **New scenario** returns to scenario selection; unsaved changes have a discard/keep prompt.

Authored starting positions, room layout, hazard parameters and behavioural configuration are saved. Runtime moved positions, injuries, panic and elapsed time are excluded. Saved scenarios use the existing per-user CrowdSimCs/scenarios directory (on Windows: %LOCALAPPDATA%/CrowdSimCs/scenarios); the included scenarios folder remains a compatibility source.

AVI recording, asset dimensions, free-angle barricades, speed limits, both simulation models and existing replay behaviour remain available.

## 8. Rebuild after editing

```powershell
dotnet build CrowdSim.sln -c Release
dotnet test tests/CrowdSim.Tests/CrowdSim.Tests.csproj -c Release --no-build
dotnet run --project src/CrowdSim.OpenGL/CrowdSim.OpenGL.csproj -c Release --no-build
```

No precompiled binaries are distributed. Rebuild before using --no-build.
