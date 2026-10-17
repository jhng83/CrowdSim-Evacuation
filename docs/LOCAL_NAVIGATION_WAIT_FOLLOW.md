# Local navigation recovery: circular follow and casualty wait/press

This revision addresses two visual-QA failure modes in the Explosion scenario without changing the global SFM model or agent health/state colours.

## 1. Circular blockage anti-banging

An evacuating/injured pedestrian whose desired motor direction would cut through a nearby circular blockage now enters a short `CircleFollow` local-navigation mode. The pedestrian commits to one clockwise/counter-clockwise tangent, with a small outward component, until the original route is body-clear again. Physical circle collision remains authoritative; contact resolution can also latch the same mode as a fallback if crowd pressure causes overlap before the proactive check runs.

This is a local motor layer only. Exit choice, route planning, panic, SFM forces and hard obstacle containment remain unchanged.

## 2. Casualty blockage sequence

A casualty cluster no longer causes repeated left/right bypass flips. The local sequence is:

1. `CasualtyBypass` - try one immediate body-clear detour.
2. `WaitingForGap` - if the first bypass does not clear the blockage, stop briefly instead of oscillating.
3. `SeekingGap` - probe a wider set of body-clear directions.
4. `PressingThrough` - after prolonged blockage, a more assertive/panicked pedestrian may accept reduced *social* spacing through a narrow but physically valid gap.

Wait duration is bounded and trait-sensitive. Assertiveness/panic shorten the delay before controlled pressing; patience lengthens it.

`PressingThrough` never changes body radius or disables casualty collision. The larger mobile-to-casualty hard separation remains active even for a zero-speed waiting pedestrian. If no physically valid gap exists, the pedestrian remains in `WaitingForGap` and retries rather than crossing through a casualty.

## Inspector diagnostics

The selected-agent inspector now shows `Local navigation` when one of these modes is active. Casualty modes also show the blocking casualty id and blocked time. `CircleFollow` shows the circular obstacle and committed clockwise/counter-clockwise side.

## Regression coverage

`LocalNavigationRecoveryTests` protects:

- circular contact latching tangent-follow rather than repeated inward drive;
- failed casualty bypass entering `WaitingForGap` rather than flipping sides;
- controlled press retaining hard casualty body separation even at zero speed.
