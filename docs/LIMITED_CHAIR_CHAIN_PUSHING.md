# Limited chair chain pushing

Green and blue walking agents first try the existing single-chair forward/diagonal/
sideways shifts. If those fail, they can attempt to translate a contacting group of
at most three unoccupied upright/fallen chairs.

The existing injury-dependent push speed is calculated first. Two chairs reduce
that displacement to two-thirds; three reduce it to one-half. All chairs translate
by the same displacement, preserving their relative spacing. This is a lightweight
small-group approximation, not a detailed chair-leg/interlocking solver.

The algorithm discovers obstructing chairs over the short proposed sweep (up to
0.12 m), caps the group at three, and validates every member's reduced swept path
against people, walls, tables, room boundaries and non-group furniture before any
positions change. It rejects an occupied/airborne chair, a fourth blocking chair,
or a member already moved this tick. Proxies update only after all checks pass.
The group cannot be partially moved when validation fails.

Crawling people keep their existing solid envelope. Existing overlapping blast
pieces may separate; translating a group does not deepen its internal overlaps.
A legitimately wedged group remains blocked and the existing detour logic applies.

Added ChairChainPushTests for two/three chairs, the four-chair limit, occupied chairs,
wall/crawler blocking, per-tick movement limits and normal-push fallback in a narrow
passage. No new all-room path search or physics engine was added. CPU impact has
not been measured; compilation/tests/visual verification could not be run because
the editing environment has no .NET SDK.

```powershell
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL -c Release
```
