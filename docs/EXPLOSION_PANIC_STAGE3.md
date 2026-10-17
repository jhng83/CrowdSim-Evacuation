# Explosion, Panic Contagion and Stage 3 Crowd Interaction

## Scope

This revision keeps the validated panic-influenced Social Force Model and adds the staged behaviour discussed for the C# 3D simulator. The event model is intentionally abstract and normalized for crowd/HCI simulation; it is not a real explosive-yield, fragmentation, medical, or weapons-effects model.

## Stage 1 - Event presentation and debris

Normal 3D presentation shows only:

- a rapidly expanding translucent shock-front band;
- outward-moving debris fragments.

The active event presentation remains the fast translucent shock front plus debris. After detonation, optional persistent wall-clipped analysis bands show the configured Kill, Incapacitation, Fallen/Severe, Injury, and Panic/Threat zones. These overlays are visual references only and never re-apply damage. The shock front and persistent bands are clipped by room boundaries and wall/blockage line-of-sight using the same shielding geometry used by the event physics.

Debris is simulated as lightweight normalized fragments:

- each fragment has position, previous position, velocity, normalized energy and TTL;
- swept-segment checks prevent fast fragments tunnelling through thin walls or agents;
- walls/blockages stop a fragment;
- one fragment is removed after one person impact, preventing repeated damage across frames;
- impact severity can produce injury, fallen/incapacitated, or fatal states;
- energy decays during travel;
- non-finite values discard the fragment safely.

## Stage 2 - Human response and panic-induced SFM

Direct response is synchronized to the propagating front. A mobile exposed survivor enters the bounded startle/reaction path when the front reaches them; physical outcomes are applied once per agent. Wall shielding is re-evaluated at front arrival, so reaching cover before arrival can protect the agent.

Panic has three bounded inputs:

1. **Direct threat** - starts after actual front exposure and decays from that agent's exposure time.
2. **Social contagion** - visible nearby panic influences the observer.
3. **Running-crowd contagion** - visible nearby agents moving unusually fast in a sufficiently coherent direction add an extra panic stimulus.

Running-crowd contagion has guardrails:

- minimum visible runner count;
- minimum runner speed relative to that runner's baseline speed;
- direction-coherence threshold;
- distance weighting;
- wall/line-of-sight gating;
- reduced perception radius under poor visibility;
- bounded contribution and final panic clamp `[0,1]`.

Panic remains part of movement rather than a cosmetic state. It increases desired speed and strengthens herding/avoidance blending before the SFM force is constrained by the existing acceleration, speed, collision and numerical guardrails.

## Stage 3A - Fallen-person avoidance and emergent congestion

Fallen, incapacitated, temporary-incapacitated, fatal and crawling agents remain in the room as dynamic casualty obstacles.

Moving agents use the spatial grid to find nearby casualty obstacles and add a local tangent-avoidance preference. The existing SFM/contact/collision logic remains authoritative for close interaction. Congestion is not injected artificially: it emerges because casualties reduce usable space and crowd streams converge around them.

Local congestion is also measured per agent and displayed in the UI. Exit scoring separately penalizes casualty accumulation near a doorway.

## Stage 3B - Dynamic rerouting with hysteresis

Evacuating agents periodically reassess exits rather than switching every frame. Exit scores consider:

- distance;
- local exit density;
- doorway occupancy;
- casualty accumulation near the exit;
- wall line-of-sight/occlusion;
- visibility-related uncertainty.

A route changes only when the commitment interval has expired and another route is materially better, or when a visible alternative provides meaningful recovery from an occluded route. Near a usable doorway, commitment is preserved. This prevents A/B/A/B route thrashing.

## Stage 3C - Temporary visibility degradation

The event creates an abstract short-lived visibility field used for perception and route choice. It intentionally has no smoke/fire visual effect because the requested event presentation is shock front plus debris only.

The visibility field:

- propagates locally after the same abstract event front reaches a point;
- builds, holds, then decays;
- is bounded by a minimum visibility factor;
- is blocked by solid walls/blockages;
- reduces social/run perception radius;
- increases reliance on local crowd/herd information;
- contributes distance-weighted uncertainty to exit selection.

Visibility never adds a physical force by itself. It changes perceived information, which changes navigation/herding inputs, and those inputs then pass through the normal panic-influenced SFM.

## No POV event effects

Agent POV remains available for following/inspection, but the event does not add camera shake, flash, disorientation, smoke overlay, or other POV-specific effects.
