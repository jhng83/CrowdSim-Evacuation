# Python -> C# Port Mapping

| Validated Python source | C# destination |
|---|---|
| `crowd_sim/config.py` | `CrowdSim.Core/Configuration/SimulationConfig.cs` |
| `crowd_sim/agents.py` | `CrowdSim.Core/Model/Agent.cs`, `AgentState.cs` |
| `crowd_sim/walls.py` | `CrowdSim.Core/Model/Wall.cs` |
| `crowd_sim/environment.py` | `CrowdSim.Core/Model/SimulationEnvironment.cs` |
| `crowd_sim/debris.py` | `CrowdSim.Core/Model/Debris.cs` |
| `crowd_sim/physics_sfm.py` | `CrowdSim.Core/Systems/SfmEngine.cs` |
| `crowd_sim/evacuation_logic.py` | `CrowdSim.Core/Systems/EvacuationSystem.cs` |
| `crowd_sim/explosion_system.py` | `CrowdSim.Core/Systems/ExplosionSystem.cs` |
| `crowd_sim/simulation.py` | `Simulation.cs`, `Simulation.Step.cs`, `Simulation.Collisions.cs` |
| `crowd_sim/app.py` | `CrowdSim.OpenGL/UI/SimulationUi.cs` + `CrowdSimApplication.cs` |
| Python 2D/OpenGL renderers | `CrowdSim.OpenGL/Rendering/Camera3D.cs`, `WorldRenderer.cs` |

## Agent behaviour

`distance_to`, `choose_exit`, exit/injury/fallen/crawling/incapacitation transitions and active-state
semantics are represented by typed methods/properties on `Agent`.

## Panic and SFM

- threat panic;
- social panic contagion;
- bounded panic decay/update;
- panic-driven reaction state;
- panic-scaled desired speed;
- panic-scaled herding;
- social repulsion;
- body/contact compression;
- tangential sliding;
- wall repulsion/contact;
- vision-cone gating.

These remain model algorithms in `CrowdSim.Core`, not renderer approximations.

## Environment and navigation

Exit placement, true boundary openings, blockage snapping, overlap rejection, undo, line-of-sight,
exit reselection, local density/capacity, wall following, exploration and deadlock/corner escape are
retained.

## Explosion and casualties

The ordered hard-kill, temporary-incapacitation, fallen, injury and panic-only ranges are retained,
as are wall shielding, shockwave impulse, blast wind, debris, crawling recovery and collision-aware
movement after blast effects.

## UI adaptation

The Python keyboard controls are deliberately not ported as the primary interaction scheme. Their
functions are exposed as visible controls: builder tools, exit width, undo, finalise, start/pause,
explosion placement, camera, debug visualisation and application exit.

## Post-port Stage 1-3 extensions

These are deliberate C# simulation extensions layered on top of the Python-derived baseline, not claims that the original Python contained them:

| Extension | C# implementation |
|---|---|
| Propagating presentation/physical front synchronization | `ExplosionSystem.ApplyExplosionEffects`, `WorldRenderer.DrawExplosionGroundEffects` |
| Normalized swept-collision debris severity | `Debris`, `ExplosionSystem.UpdateDebris` |
| Per-agent direct-exposure timing | `Agent.ShockwaveHitFrame`, `EvacuationSystem.UpdatePanic` |
| Coherent running-crowd panic contagion | `EvacuationSystem.UpdatePanic` |
| Casualty obstacle avoidance | `Agent.IsCasualtyObstacle`, `EvacuationSystem.UpdateEvacAgent` |
| Dynamic exit scoring + route hysteresis | `EvacuationSystem.MaybeReselectExit` |
| Temporary wall-aware visibility/perception field | `VisibilitySystem` |
| Fatal casualty state used by normalized event/debris outcomes | `AgentState.Dead`, `Agent.MarkDead` |

These additions preserve the existing SFM, collision, wall-follow, deadlock, exit-flow and numerical guardrails. They should be calibrated/validated separately if used for research conclusions.
