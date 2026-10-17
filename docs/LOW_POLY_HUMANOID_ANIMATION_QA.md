# Low-Poly Humanoid Animation - QA Notes

## Scope

This revision is intentionally render-only. No files under `CrowdSim.Core` were modified.

Changed runtime renderer/UI files:

- `src/CrowdSim.OpenGL/Rendering/WorldRenderer.cs`
- `src/CrowdSim.OpenGL/UI/SimulationUi.cs` (passes the existing simulation-speed value to the renderer so gait animation stays synchronized with accelerated/slowed simulation time)

Documentation-only updates:

- `docs/LOW_POLY_HUMANOID_ANIMATION.md`
- `run_instructions.md`
- `README.md`

## Visual acceptance checks

1. Standing/paused agents settle into a neutral pose with no walking-in-place.
2. Slow agents use short strides and slow arm swing.
3. Normal walkers show alternating legs and counter-swinging arms.
4. Fast emergency movement blends toward a running gait with longer stride, higher cadence, more knee lift, bent elbows and forward torso lean.
5. Different agent ids have different gait phase offsets, so a crowd does not march in lockstep.
6. Pausing the simulation freezes gait animation.
7. Changing simulation speed changes gait timing consistently with the accelerated/slowed simulation.
8. Crawling agents animate only when they actually move.
9. Fallen/incapacitated/dead agents remain prone.
10. State colours, panic rings, alarm indicators, selection rings and debug overlays remain unchanged.

## Regression expectation

No crowd-physics tests were intentionally changed. The existing test suite should remain at **60 passed, 0 failed**.

Use:

```powershell
dotnet restore .\CrowdSim.sln --force --disable-parallel --no-cache
dotnet build .\CrowdSim.sln -c Debug --no-restore
dotnet test .\CrowdSim.sln -c Debug --no-build --no-restore
```
