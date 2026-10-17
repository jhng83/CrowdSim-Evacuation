# Chair departure and navigation-cost correction

The screenshot reports Standing/waiting with zero desired speed. The seating
controller runs before evacuation navigation, so the recent open-space detour logic
cannot help someone still in StandingUp posture.

The old controller required a complete straight departure path before taking any
step. It now takes short swept steps (0.7 m/s), tests sixteen directions, and requires
outward progress from the seat. Nearby people, casualties, walls and other furniture
remain checked. Existing close contact may separate but cannot deepen. If the chair
has moved away or is destroyed, a person who has completed standing is released
immediately to normal navigation. The release clearance also accommodates a fallen
chair footprint. A completed standing agent can push a reachable unoccupied chair;
occupied seats remain excluded. Fully blocked departure searches are retried at
staggered 0.20–0.30 second intervals rather than exhaustively every tick.

Potential sources of intermittent CPU spikes in navigation were identified by source
inspection: repeated whole-grid rebuilds after individual chair pushes, full-scene
collision queries on every A* edge, and long smoothing searches. The furniture grid
now rebuilds at most once per simulation tick for furniture movement. Static wall
changes still invalidate immediately. A* uses inflated raster cells and diagonal
corner checks; every returned continuous segment is validated against live geometry.
If geometry changed during the tick and validation fails, no route is returned and
normal retry/recovery applies. Smoothing considers at most twelve subsequent grid
nodes per segment.

These changes reduce identified repeated work, but no FPS or pause-duration result
has been measured. The exact screenshot scenario was not supplied as a saved layout.
The editing environment has no .NET SDK, so build, tests and visual verification
remain unexecuted here.

Added SeatDepartureRecoveryTests for partial departure, release from a destroyed
seat, and pushing after stand-up. Updated the congestion test to assert body
separation while waiting rather than requiring the person to remain at the exact
seat centre.

From the extracted CrowdSimCs folder:

```powershell
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL -c Release
```

For comparison, replay the same saved scene and seed, select a previously waiting
agent, and check whether posture changes from Standing to Walking. Observe any
remaining multi-second pauses separately from the displayed instantaneous FPS.
