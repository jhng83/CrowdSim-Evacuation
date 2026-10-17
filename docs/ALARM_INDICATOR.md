# Alarm / Startle Exclamation Indicator

## Behaviour

A red exclamation mark is rendered above an active standing humanoid while that agent is in the existing `WaitingToReact` state.

This state is the short bounded startle / reaction pause used when an emergency is perceived. It can be entered because of:

- direct shock-front exposure;
- social panic contagion; or
- running-crowd contagion.

The marker disappears automatically when the agent leaves `WaitingToReact`, including transition to `Evacuating` or `Injured`. Fatal, fallen, incapacitated, crawling and already-evacuating agents do not receive the marker.

## Guardrail

This is a rendering-only cue. It does not create a new simulation state and does not modify panic, reaction timing, SFM force calculation, collision handling, routing, blast effects or any guardrail.
