# Blast-start CPU update

Source inspection identified repeated work that grows when the blast starts and many
agents/furniture pieces are active. Changes:

- Heavy furniture scenes no longer fall back from a failed grid route to building
  an all-pairs obstacle visibility graph. A 0.125 m grid handles the fallback instead
  of the normal 0.25 m grid. Returned paths are still checked against current geometry.
  Gaps too narrow for either grid can still return no route; local recovery remains.
- Failed route requests retry after 0.30–0.54 seconds, staggered by agent ID. A newly
  clear direct route or changed destination bypasses the cooldown immediately.
- Furniture microsteps integrate active pieces only. An impacted stationary piece
  joins the active list when it receives momentum; supported pieces are still updated.
- Furniture/static collision checks use nearby wall buckets and a prefiltered list
  of non-furniture circles, rather than scanning every chair on every microstep.
- Person-impact selection uses one nearest-person pass with the same ID tie-break,
  removing per-query sorting/iterator allocations.

Physics microstep size, destruction zones, collision clearances, momentum transfer,
chair departure and exit behaviour are retained. These are CPU changes, not a GPU
migration. No measured speedup is claimed: this environment has no .NET SDK and the
user's exact scene/seed was not available for a run.

The DEBUG inspector's PANIC / SFM MODEL section now shows Last tick CPU, Furniture
CPU and Route planning CPU (with search count). Route timing covers the static-route
requests from evacuation navigation; total tick time includes the other systems.
Use these readings during the initial lag to distinguish remaining costs.

Existing furniture impact/wall/landing tests cover integration behaviour. Added
RouteRetryCostTests verifies that duplicate failed requests are suppressed and a
clear direct path recovers without waiting. Build/tests/visual checks remain unrun
here; archive integrity was checked.

```powershell
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL -c Release
```
