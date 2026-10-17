# Earlier open-space detours

Healthy evacuating pedestrians observe a slow crawler for at most 0.15 seconds
before evaluating an overtake. If the immediate bypass fails, they search for a
reachable patch of open space before the older wait/press escalation.

The search samples twelve directions, including directly backward, at 0.75, 1.35
and 2.1 metres. Low crowding and clearance matter more than immediate progress
along the exit direction. A small deterministic side preference breaks ties.
The selected local goal is committed for at least 1.8 seconds; it is abandoned
if its path becomes obstructed, reached, or expires, then normal exit routing
resumes. Mixed furniture/casualty blockages may also enter this recovery mode.

Person checks retain the standing/casualty physical envelopes. They allow a
pedestrian already inside the additional clearance margin to move outward with
increasing separation, rather than rejecting every path at its starting point.
There is no step-over, teleportation or disabled body collision. Existing hard
collision correction continues to enforce separation. ThreatEscape detours retain
the configured maximum backtracking toward the explosion.

Candidate searches query nearby people once and are throttled to one attempt per
0.30 seconds per pedestrian. Committed paths are rechecked against people and static
geometry. CPU/FPS impact has not been benchmarked.

After two seconds of continuing blockage, exit reselection may override the normal
commitment interval. It still uses the existing congestion score, improvement threshold,
visibility rules and near-doorway guard to avoid pointless switching.

Tests added: escape from close crawler contact, a backward-only pocket, and rejection
of routes through a ring of people. The older regression requiring a healthy agent
to wait despite available space now requires a committed detour instead.

Build, tests and visual simulation could not be executed here because the editing
environment has no .NET SDK. Archive integrity and changed source call sites were
checked. From the extracted CrowdSimCs directory on Windows, run:

```powershell
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL -c Debug
```

Do not use --no-build for the first run of the updated tests.
