# Injured walkers clearing loose chairs

Injured agents were already allowed by the contact-push operation, but a failed
route could return zero movement while waiting for another route search. They now
explicitly try to shift a nearby loose chair when routing fails, including during
the failed-route cooldown. The operation does not run an additional path search.

A successful push retains the intended direction for that tick instead of immediately
switching back to circling the same chair. Injured agents can reach 0.45 m beyond
body-to-chair contact (healthy agents retain 0.30 m), representing reaching to shift
a chair before stepping forward.

Push speed is capped at 0.20 + 0.40 * mobility metres/second, with mobility based
on injury/toxic mobility factors clamped to 0.15–1.0. Injury severity and walking
speed are unchanged. A stationary injured agent can push gently. All existing wall,
furniture, people, occupancy and per-chair per-tick displacement checks remain.
Tables, occupied chairs, airborne chairs and destroyed pieces cannot be pushed by
this fallback. This does not allow stepping through a person or a jammed chair.

Added tests for gentle zero-momentum injured pushing and chair clearance during a
failed-route cooldown without another route-planning request. Compilation, tests
and visual behaviour remain unverified in this environment (no .NET SDK).

```powershell
dotnet test .\CrowdSim.sln -c Debug
dotnet run --project .\src\CrowdSim.OpenGL -c Release
```
