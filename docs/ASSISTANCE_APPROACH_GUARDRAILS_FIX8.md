# FIX8 - Assistance Approach Guardrails

FIX8 addresses helpers that could remain in `AssistCasualty` for tens of seconds when crowding or geometry prevented them from reaching the casualty.

## Behaviour

- Each casualty exposes two opposite assistance positions at approximately 0.51 m from the casualty centre.
- Two assigned helpers reserve different positions.
- `HELPING` begins only after the helper reaches its reserved assistance position and is within the existing casualty reach radius.
- The actual attendance phase remains 2.0 seconds.
- No meaningful approach progress for 2 seconds clears/replans the route.
- No meaningful progress for 6 seconds attempts the alternate free assistance side.
- No meaningful progress for 10 seconds abandons the assignment and returns the helper to evacuation.
- A helper with no body-clear static route does not silently switch to exit movement while still labelled `GOING TO HELP`; it waits for the bounded recovery logic.

## Diagnostics

The FIX7 selected-agent diagnostics remain available. FIX8 additionally reports the reserved assistance side, recovery stage and remaining give-up guard time.

## Scope

Helping tendency/probability, casualty medical outcomes, SFM parameters, panic, blast logic and exit routing are otherwise unchanged.
