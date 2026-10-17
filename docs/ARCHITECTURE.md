# Architecture

## Goal

Keep the validated Python crowd model as the behaviour source of truth while replacing the Python
OpenCV/keypad frontend with a single C#/.NET 8 OpenGL application using a visible UI and simple 3D
humanoids.

## Layering

### CrowdSim.Core

Contains all behaviour and physics. It has no dependency on Silk.NET or ImGui.

- `Simulation`: orchestration, spatial hashes, spawn, fixed-step state.
- `SfmEngine`: desired-force, social repulsion, body contact, tangential sliding, wall force,
  vision gate and repulsion soft cap.
- `EvacuationSystem`: direct/social/running-crowd panic contagion, decay, herding, casualty
  avoidance, route commitment/reselection, local navigation, deadlock escape and width-aware exit flow.
- `ExplosionSystem`: wall-aware propagating shock front, ordered casualty outcomes, bounded wind/knockback
  and swept-collision debris with normalized impact severity.
- `VisibilitySystem`: wall-aware temporary perception degradation used by contagion/herding/route choice.
- `Simulation.Collisions`: wall/casualty collision handling, micro-substeps, containment and
  wall-follow safeguards.

### CrowdSim.OpenGL

Presentation only.

- Silk.NET creates the native OpenGL window and ImGui renderer.
- `Camera3D` performs an orthographic 3D projection with UI-controlled azimuth/elevation/zoom.
- `WorldRenderer` projects raised geometry and checkpoint-style humanoid prisms into ImGui draw
  lists, which are then rendered through the OpenGL backend.
- `SimulationUi` owns visible controls and mouse world interaction.

The renderer maintains smoothing/trail caches only; it never writes to an agent's physical position,
velocity, panic, state or force.

## Simulation timing

The app uses an accumulator based on `AgentConfig.Dt` (`0.15` by default). This makes one model step
correspond to the same fixed `dt` used by SFM integration. A catch-up cap prevents an OS pause or
debugger breakpoint from freezing the UI with an unbounded number of delayed steps.

## 3D humanoids

Each standing agent is built from six low-poly rectangular prisms:

- two legs;
- torso;
- two arms;
- head.

The local geometry is rotated to the agent's heading. Fallen/incapacitated/fatal/crawling states use
a prone pose. Humanoid body colour shows state only; panic is shown separately with a red/orange
ground halo so a green evacuating agent never becomes visually ambiguous.

## Stage 1-3 interaction pipeline

```text
propagating event front + debris
        |
        +--> direct physical outcomes / reaction onset
        |
        +--> direct panic
                  |
visible panicked/running neighbours --> social + run contagion
                  |
             bounded panic
                  |
       panic-influenced SFM + herding
                  |
casualty avoidance / local congestion / route hysteresis
                  |
temporary visibility --> perception radius + herd/route uncertainty
                  |
      collision/wall/finite-value guardrails
```
