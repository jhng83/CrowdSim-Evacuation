# Low-Poly Animated Humanoids

This revision upgrades the render-only pedestrian model without changing crowd physics, routing, panic, bottleneck behaviour, exit capacity, collision radii or simulation guardrails.

## Visual model

Standing pedestrians are now assembled from a small set of faceted cuboid/segment primitives:

- pelvis;
- tapered/leaning torso segment;
- neck and head;
- separate upper/lower arms;
- separate upper/lower legs;
- simple feet.

The existing state colour convention is retained. Panic remains a ground-ring diagnostic rather than changing body colour.

## Speed-driven gait

Animation is driven by each agent's actual simulated velocity and base walking speed.

- Near-zero speed: neutral standing pose.
- Walking: moderate stride, alternating arm swing and slight body bob.
- Faster emergency movement: longer stride, higher cadence, greater knee lift, bent running arms and a small forward lean.
- Congestion: because animation uses actual velocity, a blocked agent naturally shortens/slows the gait even when its desired movement remains urgent.

Each agent receives a stable gait phase offset based on its id so the crowd does not march in synchrony. The gait clock is integrated at render time for smooth limb motion, but it is scaled by the simulation-speed control and freezes while the simulation is paused. This keeps the animation visually smooth without changing the fixed physics timestep.

## Casualty poses

- Fallen, temporarily incapacitated, incapacitated and dead agents retain a prone low-poly pose.
- Crawling agents now receive a small alternating arm/leg crawl animation when they are actually moving.

## Performance and safety

The model remains low-poly and uses the existing ImGui projected-prism renderer. All new state is held inside `WorldRenderer` and is cleared with the existing visual-state reset path. No animation value is written back to `Agent` or `Simulation`.
