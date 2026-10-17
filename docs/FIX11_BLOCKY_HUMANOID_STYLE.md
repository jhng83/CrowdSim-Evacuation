# FIX11 - Blocky Low-Poly Humanoid Style

## Scope

This revision is render-only. It does not change SFM, collision radii, routing, panic, assistance decisions, blast behaviour, exit flow, slow-crawler bypass logic, scenario persistence, or agent state transitions.

## Visual target

The normal pedestrian silhouette is intentionally close to a simple checkpoint / simulation-avatar style:

- rectangular block torso;
- small square/block head;
- long, slim, straight arms;
- separated slim legs;
- simple block feet;
- clear isometric silhouette at overview camera distances.

Existing state colours remain authoritative. FIX11 changes geometry/proportions, not the state colour scheme.

## State coverage

The same low-poly design language is used for:

- standing / waiting;
- walking / faster evacuation gait;
- injured mobile agents;
- crawling agents;
- helpers during the kneeling attendance phase;
- fallen, temporarily incapacitated, incapacitated and dead prone states;
- reduced 3D LOD and far screen-space LOD.

## Animation

Walking still uses the existing per-agent gait phase and actual simulated speed. The new upright model deliberately uses straight one-piece arm and leg blocks instead of full elbow/knee articulation. This better matches the requested visual reference and reduces the number of projected 3D primitives per full-detail pedestrian.

Crawling retains alternating limb motion. Helping retains the bounded crouch/kneel presentation. No animation state is written back into simulation physics.

## Metric renderer correction

`DrawSegmentPrism` previously retained a legacy safety floor of `0.2` directly after the world conversion to metres. That meant any requested arm/leg half-thickness smaller than `0.20 m` was silently expanded to `0.20 m`, producing a minimum total thickness of roughly `0.40 m`.

FIX11 converts that safety floor using:

```csharp
0.2f * LegacyRenderUnitMeters
```

Requested metre-scale limb dimensions therefore remain intact while still avoiding degenerate zero-width prisms.

## Performance intent

The normal-detail upright figure is simpler than the prior articulated model:

- one block torso instead of a long torso segment;
- one block per arm instead of upper + lower arm;
- one long block per leg instead of upper + lower leg;
- the existing adaptive reduced / far LOD thresholds are retained.

The change is intended to improve silhouette quality without increasing simulation or rendering load.

## QA checklist

1. Standing agents read as blocky humanoids rather than thick segmented figures.
2. Walking agents show alternating straight-leg and counter-swing arm animation.
3. Heads remain square/block-like at both near and far LOD.
4. Crawling yellow agents remain close to the floor and use the same blocky geometry.
5. Helpers kneel/crouch using the same torso/head/limb style.
6. Fallen/incapacitated/dead agents retain a clear prone blocky silhouette.
7. State colours and diagnostic overlays remain unchanged.
8. High-population / high-speed LOD behaviour remains active.
