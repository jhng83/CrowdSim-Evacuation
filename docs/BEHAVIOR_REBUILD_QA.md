# Behaviour Rebuild 1 - QA Notes

## Static checks completed in the packaging environment

- C# brace/parenthesis/bracket balance checked across the solution.
- AgentConfig references checked against declared configuration members.
- `VisibilityGraphPlanner.cs` is included under `CrowdSim.Core/Systems` and is picked up by the SDK-style project automatically.
- Scenario persistence files are byte-for-byte unchanged from the validated FIX15 branch.
- No `bin` or `obj` build artefacts are included.
- A representative blockage/exit geometry was independently checked against the planner geometry: the direct body-clear segment is rejected and A* finds a multi-segment route around the inflated blockage corner.
- The retained plus new regression suite contains **50 `[Fact]` tests**.

## Behaviour regressions covered by new tests

- time-based panic decay remains visibly elevated for several seconds;
- persistent social alarm approaches a bounded acute-stress level rather than numerically saturating at 1.0;
- direct alarm recipients cannot later be socially reactivated by the same event episode after the direct window expires;
- directly exposed mobile agents enter short threat-escape intent;
- an already-evacuating agent can still switch to threat-escape when the physical front later reaches it;
- high-helping/low-stress agents may briefly attend a nearby casualty without changing casualty outcome;
- body-clear A* routing finds a route around an obstructing blockage;
- purposeful routing uses a waypoint before the legacy exploration fallback;
- purposeful evacuation remains brisk after acute panic subsides;
- seed-reproducible behavioural traits are deterministic.

## Validation still required on Windows

The packaging environment does not contain the .NET SDK, so it cannot execute `dotnet build` or `dotnet test`. Validate on the user's .NET 8 Windows environment with:

```powershell
dotnet restore .\CrowdSim.sln --force --disable-parallel --no-cache
dotnet build .\CrowdSim.sln -c Debug --no-restore
dotnet test .\CrowdSim.sln -c Debug --no-build --no-restore
```

Expected test total: **50 passed**.

## Visual acceptance checks

After a representative blast scenario:

1. Directly exposed mobile agents should show a heterogeneous reaction delay, visible panic rings, and a short threat-escape phase rather than all immediately walking to exits.
2. Socially alerted agents should not all reach panic 1.0; some should follow a coherent evacuation flow or purposefully evacuate at moderate stress.
3. Red/orange panic rings should persist for meaningful seconds and then decay when alarm cues expire; late residual re-panic from the same event should not occur.
4. Agents should route around blockages using body-clear waypoints instead of accumulating indefinitely at wall corners. Route look-ahead should round corners rather than forcing every agent through one exact corner coordinate.
5. Purposeful green evacuees should remain brisk even when panic has decayed.
6. Purple congestion remains a separate crowd-density diagnostic.
7. Scenario Save/Load should still restore agent count, seed, exits, blockages and normalized blast radii exactly as in FIX15.
