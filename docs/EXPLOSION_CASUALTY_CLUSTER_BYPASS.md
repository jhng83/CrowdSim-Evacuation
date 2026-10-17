# Explosion casualty-cluster bypass

## Problem

During the short post-blast `ThreatEscape` intent, the previous movement layer disabled the established slow-crawler bypass. A mobile survivor could therefore keep driving along the radial escape direction into a yellow casualty cluster even when clear local floor existed to the side or slightly behind. Symmetric casualty tangent vectors could also cancel, leaving the radial direction dominant.

## Current behavior

The ordinary slow-crawler bypass is unchanged for normal evacuation. During `ThreatEscape`, any local casualty obstacle in the forward corridor may trigger an immediate cluster-aware detour. The detour sampler checks +/-35, +/-60, +/-90 and +/-120 degrees around the current threat-escape heading.

Candidate points must:

- have body-clear static geometry;
- avoid chords through nearby casualty bodies;
- avoid highly occupied endpoints; and
- not reduce radial distance from the explosion by more than 0.45 m.

Forward/lateral candidates score higher when similarly clear. The +/-120-degree candidates provide a bounded back-side step only when the forward hemisphere is blocked. Pure 180-degree reversal is never sampled.

The chosen local goal is committed for 1.2 seconds to reduce oscillation. SFM, crowd contact, walls and collision resolution remain authoritative for the actual trajectory.

## Performance

The extra candidate scoring runs only for mobile agents currently in `ThreatEscape` that have a casualty obstacle in the forward corridor. Nearby dynamic bodies are queried once and reused for all eight candidate headings.

## Regression coverage

- `ThreatEscape_CasualtyCluster_ChoosesClearLocalDetourInsteadOfPressingForward`
- `ThreatEscape_WhenForwardAndSidewaysAreBlocked_AllowsBoundedBackSideStep`
