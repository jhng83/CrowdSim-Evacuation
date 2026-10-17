# Behaviour Rebuild 1

## Aim

This revision replaces incremental panic/navigation tuning with a clearer emergency-behaviour architecture. It is intended to produce heterogeneous, purposeful crowd response while preserving the simulator's existing numerical, collision, exit-flow, injury and abstract blast guardrails.

It is **not** a model of FIX9 movement and it is not a claim that every person responds identically to an explosion.

## Behaviour architecture

The physical `AgentState` remains responsible for stable body colour and mobility state. A separate `BehaviorIntent` layer represents short-lived behavioural purpose:

- `Unaware` - no emergency response has started;
- `Orienting` - bounded startle/reaction period;
- `ThreatEscape` - short movement that creates distance from the immediate event;
- `Evacuate` - purposeful movement toward a selected exit;
- `FollowCrowd` - temporary use of a coherent visible crowd direction;
- `AssistCasualty` - brief approach/attendance to a nearby casualty before resuming evacuation.

This separation means an agent can evacuate purposefully without needing a high panic value.

## Heterogeneity

Each spawned agent receives deterministic, seed-reproducible abstract traits:

- threat sensitivity;
- following tendency;
- helping tendency;
- route familiarity;
- urgency bias.

The traits are bounded simulation variables and do not encode demographic categories or stereotypes.

## Acute stress / panic

Panic remains clamped to `[0,1]` and can affect desired SFM speed and herding. The rebuild changes its temporal behaviour:

- direct event exposure and finite active alarm sources can raise panic quickly;
- decay uses a time constant (`PanicDecayTimeConstantSeconds`, default 8 s), rather than losing a fixed fraction every frame;
- residual panic after an active alarm expires cannot itself become a new contagion source;
- a relayed event episode can be transmitted once per agent, preserving the anti-ping-pong guardrail;
- an agent that was directly exposed cannot later be socially reactivated by that same episode after its direct alarm window expires (a genuine later physical debris impact may still create a new direct stimulus);
- calm evacuation awareness remains a behavioural cue and does not create a panic source.

The renderer's red/orange ground ring still visualises panic independently from body-state colour. Purple remains congestion heat.

## Purposeful response

After the existing bounded reaction delay:

1. A directly exposed mobile survivor can briefly enter `ThreatEscape` and seek a body-clear direction generally away from the event.
2. A socially alerted agent with a strong following tendency can temporarily enter `FollowCrowd`.
3. A bounded high-helping/low-acute-stress subset may briefly enter `AssistCasualty` if a visible nearby casualty has fewer than the configured number of helpers.
4. All temporary intents eventually transition to `Evacuate`.

The assistance behaviour is intentionally abstract: it does **not** heal, revive, diagnose or otherwise change the casualty outcome.

## Static routing and SFM

The old wall-end recovery patches are no longer the primary navigation method for purposeful agents.

`VisibilityGraphPlanner` constructs a small deterministic visibility graph from:

- the agent's current position;
- the current goal;
- body-inflated corners of static builder blockages.

A* selects a body-clear waypoint chain. Every graph edge is checked with `HasTraversablePath`, which inflates walls by the configured navigation clearance. The route planner therefore answers only the static question, "which side of this blockage is physically viable?"

SFM continues to handle local dynamics:

- pedestrian-pedestrian interaction;
- wall forces;
- casualty avoidance;
- contact/congestion;
- herding contribution;
- bounded acceleration and speed.

Exit capacity, one-setback anti-banging behaviour and panic-dependent doorway yielding/pressure are preserved.

Legacy local exploration/wall-follow remains only as a bounded fallback if no visibility-graph route can be found for unusual custom geometry.

## Scenario Save / Load

FIX15 scenario persistence is retained. JSON scenarios preserve the builder configuration and reproducibility inputs (agent count, seed, exits, blockages, room dimensions and normalized blast-zone radii). They do not save runtime casualty/panic/position/debris state.

Because behavioural traits are sampled from the saved seed, finalising the same saved scenario reproduces the same initial population and behaviour traits.

## Guardrail boundary

The event/injury model remains deliberately abstract. This revision does not add explosive yield, device design, fragmentation calibration or real-world medical injury calculations. Behavioural timings and weights are configurable simulator assumptions and require domain calibration before being used for research claims.

## Regression coverage

New tests cover:

- time-based panic persistence and decay;
- directly exposed threat-escape intent;
- bounded casualty attendance without medical-state changes;
- body-clear A* routing around a blockage;
- route waypoint use before legacy exploration;
- purposeful evacuation urgency at low panic;
- deterministic behaviour traits from the scenario seed.

Together with the retained regression suite, the expected total is **50 tests**.
