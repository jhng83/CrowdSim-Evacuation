# Camera3D compile fix (FIX5)

This revision fixes four C# compiler errors introduced by the POV camera update.

## CS0136: cameraX / cameraY

The orthographic branch of `TryProject` used local names `cameraX` and `cameraY`, and the POV branch later declared the same names in the enclosing method scope. C# disallows that shadowing pattern.

The orthographic variables are now named `orthoCameraX` and `orthoCameraY`. This is a naming-only change; projection maths is unchanged.

## CS0177: screenB not definitely assigned

`TryProjectSegment` previously returned expressions of the form:

```csharp
return TryProject(a, out screenA) && TryProject(b, out screenB);
```

Because `&&` short-circuits, the second call may never execute, so C# cannot prove that `screenB` is assigned.

Both projections are now evaluated explicitly before combining their Boolean results. This retains the same success/failure semantics while guaranteeing both `out` parameters are assigned on every path.

## Simulation impact

None. `CrowdSim.Core`, panic-influenced SFM, panic contagion/decay, collision and wall guardrails, explosion physics, casualty logic, and evacuation logic are unchanged.
