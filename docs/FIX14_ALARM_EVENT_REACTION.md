# FIX14 - Alarm Event Drives Reaction

## Purpose

FIX13 could create a valid finite social/running alarm relay while leaving a wandering observer in `Wandering` because `RelayedAlarmPanicFloor` (0.30) was below the independent `PanicActivationThreshold` (0.40). This made legitimate contagion difficult to see and left many agents moving at low panic urgency.

## Change

A successful `TryActivateRelayedAlarm(...)` is now treated as a genuine perception event. If the receiving agent is `Wandering`, it immediately enters the existing bounded `WaitingToReact` sequence. The numeric panic threshold remains as a fallback for other panic pathways.

The change does **not** increase base walking speed, bypass collision/exit capacity, remove line-of-sight checks, or allow repeated relay of the same event. Panic level still controls the SFM desired-speed multiplier.

## Guardrails retained

- finite active-alarm windows
- one social relay per event episode per agent
- wall/line-of-sight blocking
- calm fast evacuees are not panic-running sources
- residual low panic decays and cannot restart the same episode
- FIX12 body-clear navigation
- purple congestion heat and red/orange panic rings remain distinct

## Regression tests

- `RelayedSocialAlarm_TriggersReactionBelowNumericPanicThreshold`
- `RelayedSocialAlarm_PropagatesAcrossMultipleHops`

Expected total: **34 tests**.
