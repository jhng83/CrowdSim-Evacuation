# Behaviour Refinement 2 — Bottleneck Pressure and Bounded Pushing

## Purpose

This refinement preserves the working panic, behavioural-intent, A* routing, SFM, exit-capacity and anti-banging systems. It adds a separate local **bottleneck pressure** model so a calm pedestrian can still become impatient, merge laterally or maintain forward pressure at a congested doorway.

The model deliberately separates three concepts:

- **Panic** — acute emotional stress produced by a current threat or alarm cue.
- **Bottleneck pressure** — local density, blocked progress, doorway load, delay and proximity.
- **Assertiveness/patience** — stable seed-reproducible individual tendencies controlling how pressure is expressed.

A low-panic crowd can therefore remain orderly in open space while showing more varied, credible interaction at a crowded exit.

## Response modes

Every eligible evacuating, injured or crawling agent near an exit has one bounded response mode:

1. **Free** — no meaningful bottleneck is present; ordinary route/SFM behaviour applies.
2. **Yield** — the agent accepts the queue or waits after a rejected admission attempt.
3. **SeekGap** — the agent probes the two lateral directions beside the doorway, selects the more open body-clear side and merges with a short commitment window.
4. **Press** — an assertive or long-delayed agent maintains stronger forward intent and accepts reduced social spacing. Physical contact forces and the hard exit-capacity limit remain authoritative.

Modes are committed briefly to prevent yield/seek/press switching every simulation frame. Pressure has a separate rise/decay time constant and is released immediately as a behaviour mode when congestion clears.

## Pressure inputs

The bounded pressure target combines:

- local pedestrian density;
- time spent making little progress;
- current doorway occupancy relative to exit capacity;
- distance to the target exit;
- the agent's stable assertiveness;
- urgency and panic as minor inputs;
- blocked-speed ratio;
- whether a one-setback exit-congestion episode is already active.

The target is clamped to `[0, 1]` and smoothed over time. Panic is intentionally not required for pressure to rise.

## Gap seeking

A `SeekGap` agent evaluates both lateral directions relative to the exit orientation. A candidate is rejected if:

- it falls outside the room;
- its body-clear point is blocked;
- the body-clear segment to the probe point intersects a wall;
- no finite candidate is available.

Valid candidates are scored using nearby crowding, nearest-agent clearance and progress toward the exit. A deterministic agent-ID tie-break avoids identical left/right choices.

## Pressing and physical contact

Pressing does not add an artificial impulse to another pedestrian. It changes only bounded locomotion inputs:

- slightly higher desired urgency within the existing behaviour-speed cap;
- shorter SFM relaxation time within a safe lower bound;
- reduced *social comfort distance* as bottleneck pressure rises.

The hard body/contact radius is unchanged. Overlap still activates the existing body and tangential sliding forces. Force soft caps, acceleration caps, final speed caps, wall containment, collision substeps and finite-value recovery remain active.

## Exit anti-banging integration

The FIX10 one-setback guardrail remains authoritative:

- the first failed admission in a congestion episode may apply one small setback;
- subsequent failed attempts are held at the same doorway threshold rather than repeatedly pushed backward;
- `Yield` agents use staggered retry times;
- `SeekGap` and `Press` agents remain eligible each frame but cannot bypass the hard capacity;
- crawling agents always yield;
- no mode permits teleporting through agents or walls.

Near a real bottleneck, the former direct-to-exit velocity shortcut is disabled so SFM/contact physics remains active for Yield, SeekGap and Press interactions.

## UI and persistence

The Builder **BEHAVIOUR TUNING** section now includes:

- Bottleneck assertiveness;
- Pressure sensitivity;
- Pressure space compression.

These values are saved in scenario JSON. The selected-agent panel displays assertiveness, patience, bottleneck mode, pressure, doorway wait and a pressure progress bar.

## Calibration boundary

The pressure weights and thresholds are bounded simulation assumptions. They are designed to create heterogeneous, stable bottleneck interaction without claiming to reproduce a particular real incident. Research use should calibrate the controls against the target venue, population and observed flow data.
