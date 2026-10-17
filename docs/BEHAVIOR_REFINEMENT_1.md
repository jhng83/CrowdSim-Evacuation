# Behaviour Refinement 1

This revision builds on the validated Behaviour Rebuild 1 branch without replacing its routing, decision, panic-contagion, explosion, exit-capacity or collision architecture.

## Added realism

- Deterministic per-agent variation now includes stress recovery and preferred personal space, in addition to urgency, threat susceptibility, following, helping and route familiarity.
- Reaction latency and normal movement speed use bounded centre-weighted distributions instead of uniform extremes.
- Stress decay uses both a scenario-level persistence control and an individual recovery bias.
- Stress susceptibility affects direct, social and running-crowd cues while remaining clamped to the existing `[0,1]` panic guardrail.
- Panic can compress anticipatory social spacing, but the physical body/contact radius is unchanged. Collision, overlap, wall and force guardrails therefore remain authoritative.
- Emergency urgency scales purposeful evacuation and panic-related speed while retaining `MaximumBehaviorSpeedMultiplier` and final velocity caps.

## Behaviour tuning UI

A collapsible **BEHAVIOUR TUNING** section is available in Builder mode. It controls:

- trait variation;
- reaction variation;
- stress susceptibility;
- stress persistence;
- crowd following;
- personal space;
- panic-related space compression;
- emergency urgency;
- helping tendency;
- route familiarity.

The tuning values apply when agents are spawned by **Finalise Scene**. They are stored in scenario JSON files and restored by Scenario Load. Older scenario JSON files without a `behavior` section continue to load using validated defaults.

## Guardrails retained

- panic remains bounded `[0,1]`;
- residual panic cannot become a contagion source after the finite alarm window;
- physical contact radius is never reduced by personal-space tuning;
- SFM force soft cap, acceleration cap, velocity cap and non-finite recovery remain unchanged;
- exit capacity, anti-banging yield/push logic, A* body-clear routing and wall collision protection remain unchanged;
- all behaviour parameters are finite and clamped before use.

## Regression coverage

Four new tests cover individual recovery variation, panic-related social-space compression with retained contact forces, urgency tuning under the speed cap, and zero-variation population centring. Scenario round-trip and deterministic-trait tests were extended. Expected total: **54 tests**.
