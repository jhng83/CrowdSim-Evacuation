# Performance Pass 1

This revision targets frame-time and allocation hotspots without reducing the simulation timestep or removing the animated low-poly humanoids.

## Renderer

- Keeps articulated humanoids and speed-driven gait animation.
- Adds distance/screen-size LOD. The normal overview uses a lower-cost articulated body while zoomed/follow/close views retain the full elbow/knee model.
- Uses a more aggressive visual LOD at high simulation-speed multipliers.
- Culls agents and debris outside the visible viewport before building their render entities.
- Reuses render/entity/id buffers instead of allocating LINQ arrays and temporary collections every frame.
- Replaces per-prism `List<Face>` allocation/sort with fixed stack buffers and a tiny insertion sort.
- Reuses precomputed `sin/cos` values inside oriented prisms.
- Caches wall chunk geometry until the Builder layout changes.
- Caches the wall-clipped persistent blast bands instead of recomputing thousands of blast/wall visibility tests every rendered frame.
- Congestion visualization reuses `Agent.LocalCongestion` instead of performing a second all-agents density scan.
- The orthographic camera measures projected humanoid height once per frame rather than once per agent.
- Camera bounds evaluation no longer allocates temporary arrays every frame.

## Simulation CPU path

- SFM pedestrian interaction now obtains candidates from the existing spatial hash before applying the unchanged vision, cutoff, social-force and body-contact equations.
- Candidate SFM agents are sorted by `AgentId` before accumulation to preserve deterministic force ordering.
- Panic/awareness snapshots use reusable dense arrays instead of rebuilding three dictionaries every simulation step.
- Panic, congestion, herding, evacuation-flow, bottleneck-gap and casualty-avoidance neighbour searches reuse caller-owned buffers.
- Collision neighbour and nearby-wall searches reuse scratch buffers.
- Wall collision correction no longer allocates a four-element vector array and LINQ ordering on each contact.
- Line-vs-wall visibility checks no longer allocate wall-corner arrays.

These changes are computational. SFM force constants, physical body radius, routing decisions, panic rules, bottleneck modes, exit capacity and blast outcomes are not intentionally changed.

## Recording

Framebuffer readback still has to occur on the OpenGL/render thread, but scaling, vertical flip, MJPEG compression and AVI writing now run on a bounded worker queue. If encoding falls behind, capture frames are dropped rather than allowing an unbounded queue to stall the simulation.

## Performance HUD

Open the right **DEBUG** tab. The new `PERFORMANCE` section shows:

- total frame time;
- physics time;
- projected world-draw time;
- UI time;
- recording capture time;
- bytes allocated on the render thread per frame;
- visible/active agents;
- recording queue depth and dropped recording frames while recording.

Use this panel to distinguish simulation CPU pressure from renderer or recording pressure.

## GPU note

The OpenGL presentation is already submitted to the GPU, but this application still builds projected ImGui geometry on the CPU and CrowdSim.Core remains CPU simulation code. Merely selecting a GPU execution environment does not move SFM/routing/panic calculations to CUDA. A later renderer rewrite can use true OpenGL instancing if populations grow beyond what this optimized projected renderer handles comfortably.

## Regression target

The existing test suite remains **60 tests**. This environment does not contain the .NET SDK, so restore/build/test must be run on the Windows development machine.
