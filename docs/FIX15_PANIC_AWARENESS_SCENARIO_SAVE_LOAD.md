# FIX15 - Panic/Awareness Split, Evacuation Urgency, and Scenario Save/Load

## Why this revision exists

FIX13/FIX14 successfully prevented late residual-panic feedback, but the finite alarm gating also made the later evacuation visually too slow and allowed an unaware white agent to remain wandering even while surrounded by obvious evacuees.

FIX15 separates three concepts that were previously coupled through one panic value:

1. **Emotional panic contagion** - finite, event-driven, wall/LOS-aware and bounded by the one-relay-per-event guardrail.
2. **Evacuation awareness** - an unaware pedestrian can infer that evacuation is under way from visible evacuating neighbours, without becoming a new panic source.
3. **Evacuation urgency** - green Evacuating/Injured agents retain a modest minimum desired-speed multiplier even after panic decays; panic can still increase desired speed above that floor.

## Panic contagion changes

- Only finite active-alarm sources can transmit social/running panic.
- Active social alarm uses the source panic level directly during that finite window rather than subtracting the observer's current panic.
- Relayed alarm floor is 0.45 and the relay window is 4.0 s by default.
- A relayed alarm still activates at most once per event episode for each agent.
- Expired residual panic is not a source, preserving the FIX12/FIX13 late-repanic protection.

## Evacuation awareness

After an emergency exists, a wandering agent may begin the existing reaction sequence when it sees either:

- at least two nearby visible evacuees moving with a coherent flow; or
- an obvious local crowd response of at least four visible evacuees, even when congestion has slowed or temporarily stalled them.

This cue:

- respects wall line-of-sight;
- starts `WaitingToReact` with a shorter awareness reaction-delay scale;
- does **not** set an active panic episode;
- does **not** add a panic floor;
- therefore cannot regenerate a late panic epidemic.

The selected-agent panel exposes `Evac awareness` for diagnosis.

## Evacuation speed floor

`Agent.CurrentDesiredSpeed` now applies a default 1.65x urgency floor only to `Evacuating` and `Injured` states. Panic still uses the original `1 + PanicSpeedGain * PanicLevel` multiplier and can exceed the floor. `SfmEngine` consumes the same `CurrentDesiredSpeed`, avoiding a UI/physics mismatch.

## Scenario Save / Load

The left sidebar now includes **SCENARIO SAVE / LOAD**.

Saved JSON contains configuration only:

- schema version;
- scenario name;
- agent count;
- random seed;
- room dimensions;
- five blast-zone radii;
- exits (name, side, position, half-width/size);
- blockage rectangles.

It intentionally does not save runtime state such as current positions, injuries, panic, debris, elapsed time or exited agents.

Files are stored by default under:

```text
scenarios/
    scenario_name.json
```

Loading creates a fresh simulation/configuration, restores the saved layout/radii/seed, and returns to Builder mode. The user can inspect or edit the layout and then press **Finalise Scene**. Because the saved seed is restored before spawning agents, the same scenario can reproduce the same initial population placement when no builder edits consume randomness.

## Preserved behaviour

- FIX10 exit congestion anti-banging/yield logic;
- FIX11 purple congestion overlay;
- FIX12 body-clear navigation and meaningful-progress stuck detection;
- FIX13 one-relay-per-event anti-feedback guardrail;
- FIX14 alarm-event-to-reaction handoff;
- blast-zone radius locking after detonation;
- wall-aware event effects, debris, casualty handling, SFM collision guardrails and Agent POV.
