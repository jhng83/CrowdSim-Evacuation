# FIX12 - Body-clear navigation and panic stability

## Scope

This revision builds on the locally validated FIX11 baseline (25/25 tests passed) and addresses two observed behaviours without changing the blast zones, exit yield/push logic, panic-ring colour, purple congestion heat, POV, debris, injury logic, or SFM guardrails.

## 1. Wall-end navigation now distinguishes sight from traversability

A centre-point line of sight can pass a wall endpoint even when a pedestrian's finite body radius still overlaps the corner. FIX12 therefore keeps perception and navigation separate:

- `HasLineOfSight(...)` remains the visual/perception test used by the vision and social systems.
- `HasTraversablePath(...)` inflates each axis-aligned wall by the configured pedestrian navigation clearance and tests the full motion segment against that inflated geometry.
- exploration is no longer cancelled merely because the exit is visually visible;
- exploration ends only when the agent has a body-clear route to its target exit;
- temporary exploration waypoints must themselves be reachable with body clearance;
- candidate waypoints that restore a body-clear route to the exit receive the strongest score;
- the close-range exit-commit shortcut cannot force an agent directly through a wall-end clearance conflict.

This keeps the visible vision cone as a perception diagnostic while preventing a false "coast is clear" navigation decision.

## 2. Stuck detection measures net movement, not tiny distance jitter

The previous stuck detector reset whenever exit distance changed by almost any amount. An agent rotating or oscillating around one point could therefore avoid being recognised as stuck.

FIX12 samples net position and exit-distance progress over a short window. A window is considered useful progress only if the pedestrian either:

- translates a meaningful fraction of its expected baseline travel; or
- makes meaningful net progress toward the exit.

Small rotations/back-and-forth jitter no longer reset the stuck timer. Agents that are genuinely moving around a wall still count as progressing even when they temporarily move sideways rather than directly toward the exit.

## 3. Residual panic can no longer mutually self-amplify

The earlier social term added the nearby average panic directly every frame. Several agents with the same small residual panic could therefore recursively amplify one another even with no new threat.

FIX12 changes social contagion to a differential signal:

1. neighbours below `PanicSocialSignalThreshold` (default `0.25`) do not transmit residual social panic;
2. a meaningful source contributes its actual bounded panic level;
3. the receiver gets social stimulus only from the amount by which the source level exceeds the receiver's current panic;
4. equal-panic neighbours therefore decay together instead of forming a positive-feedback loop.

A genuinely more-panicked visible neighbour can still raise another agent's panic. Direct threat and coherent unusual-speed running-crowd contagion remain active and unchanged.

## New regression coverage

Four tests were added:

- `ResidualLowPanic_DecaysInsteadOfMutuallyReAmplifying`
- `MeaningfullyPanickedNeighbour_CanStillRaiseSocialPanic`
- `NavigationClearance_DistinguishesVisualSightFromBodyTraversability`
- `Exploration_ContinuesWhenExitIsVisibleButBodyClearanceIsBlocked`

Expected total after a successful local run: **29 tests**.

## Validation status

This execution environment does not contain the .NET SDK, so the branch received structural/static checks and archive-integrity checks here. Run the normal local restore, build, and test sequence before treating FIX12 as a validated checkpoint.
