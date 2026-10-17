# Shock-front / Direct Panic Timing Fix

## Symptom

`DirectThreatPanic_WaitsForShockFrontArrival` could finish with `ShockwaveHit == true`
but `LastThreatPanicStimulus == 0`.

## Cause

`Simulation.Step()` updated panic before evaluating shock-front arrival. If the front reached
an agent on the final test frame, the hit was recorded after the panic update and direct threat
panic was therefore delayed by one simulation tick.

## Fix

The per-frame order is now:

1. rebuild the agent spatial grid;
2. apply shock-front/explosion exposure;
3. update direct/social/running-crowd panic;
4. update debris;
5. continue the existing reaction, SFM, collision, routing and exit-flow pipeline.

This preserves the intended invariant: an exposed agent's direct threat panic starts on the same
simulation frame that the propagating shock front reaches the agent.

No force constants, panic gains, casualty radii, debris severity thresholds, SFM guardrails,
wall shielding, routing rules, or collision rules were changed.
