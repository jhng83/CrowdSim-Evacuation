# FIX11 - Environment-aware exploration and purple congestion heat

## Scope

This revision builds on the user-validated FIX10 baseline (23/23 tests passed locally) and changes only local stuck-agent exploration plus the congestion debug colour.

## Environment-aware exploration

When an evacuating/injured pedestrian becomes stuck, exploration now:

1. immediately returns to the exit if direct wall-clear line-of-sight is available;
2. otherwise samples 16 nearby directions around the pedestrian;
3. rejects temporary goals that lie inside/too close to walls or whose centre-line is wall-occluded;
4. strongly prefers a temporary goal that restores line-of-sight to the selected exit;
5. otherwise scores candidates by exit progress, heading alignment, wall clearance, current-motion continuity, and a modest nearby-crowd penalty;
6. keeps the selected temporary heading for a short replan interval;
7. replans only when that interval has elapsed and the pedestrian is still moving very slowly or the waypoint becomes wall-occluded;
8. abandons the temporary goal as soon as the exit becomes directly visible again.

This is intended to remove the local wall-end spinning/orbiting behaviour without introducing a random escape force or bypassing collision/wall guardrails.

## Congestion heat colour

The congestion heat overlay changes from red/orange to purple. The individual panic ring remains red/orange, so the two diagnostics are visually separable when both overlays are enabled.

## Regression coverage

Two new guardrail tests were added:

- `Exploration_ChoosesClearWaypointThatRecoversExitSightAroundWallEnd`
- `Exploration_StopsImmediatelyWhenExitBecomesVisible`

Expected total after a successful local test run: 25 tests.

## Validation status

The current execution environment does not contain the .NET SDK, so this branch received structural/static QA only here. Run the normal local `dotnet build` and `dotnet test` validation before treating FIX11 as a validated checkpoint.
