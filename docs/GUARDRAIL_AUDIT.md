# Guardrail Audit

| Guardrail / behaviour | C# location |
|---|---|
| Panic clamped to `[0,1]` | `EvacuationSystem.UpdatePanic` |
| Threat + social contagion + decay | `EvacuationSystem.UpdatePanic` |
| Panic changes desired SFM speed | `SfmEngine.ComputeForces` / `Agent.CurrentDesiredSpeed` |
| Panic changes herding/avoidance weighting | `EvacuationSystem.UpdateEvacAgent` |
| Agent-agent SFM repulsion soft cap | `SfmEngine.SoftCap` (`6000`) |
| Vision cone gate | `SfmEngine.InVisionCone` |
| Exponential overflow clamp | `SfmEngine.ComputeForces` |
| Acceleration cap | `EvacuationSystem.UpdateEvacAgent` (`30`) |
| Velocity/desired-speed cap | `EvacuationSystem.UpdateEvacAgent` |
| Non-finite force recovery | `SfmEngine`, `VectorMath` |
| Non-finite position/velocity recovery | `Simulation.Collisions`, `Debris` |
| Collision micro-substeps near geometry | `Simulation.MoveAgent` |
| Wall push-out / tangent sliding | `Simulation.ResolveSolidCollisions` |
| Final blockage containment | `Simulation.FinalBlockageContainment` |
| Wall-follow activation and cooldown | `EvacuationSystem`, `Simulation.Collisions` |
| One-reversal wall-follow policy | `Simulation.Collisions` |
| Six-second wall-follow failsafe | `Simulation.Collisions` |
| Concave-corner escape breaker | `EvacuationSystem.UpdateEvacAgent` |
| Zero-velocity equilibrium breaker | `EvacuationSystem.UpdateEvacAgent` |
| Stuck exploration | `EvacuationSystem.EnterExploration` |
| LoS-gated deliberate escape | `EvacuationSystem` |
| Casualty pressure permeability | `SfmEngine`, `Simulation.Collisions` |
| Width-aware exit flow | `EvacuationSystem.ApplyExitFlows` |
| Doorway density / occupancy | `EvacuationSystem` |
| Rejected-exit pushback / damping | `EvacuationSystem.ApplyExitFlows` |
| Real openings carved in boundary walls | `SimulationEnvironment.RebuildWalls` |
| Exit/blockage overlap rejection | `SimulationEnvironment` |
| Blast wall shielding | `ExplosionSystem.BlastPathIsBlocked` |
| Ordered non-overlapping blast casualty bands | `ExplosionSystem.ApplyExplosionEffects` |
| One shockwave hit per agent | `Agent.ShockwaveHit` / `ExplosionSystem` |
| Collision-aware knockback | `ExplosionSystem` + `Simulation.MoveAgent` |
| Blast wind duration bound | `ExplosionSystem` |
| One-explosion guardrail | `SimulationEnvironment.HasExplosion` / `ExplosionSystem` |
| Debris drag, TTL and bounds | `Debris.Advance` |
| Debris casualty escalation | `ExplosionSystem.UpdateDebris` |
| Renderer cannot alter physics | `WorldRenderer` uses separate render caches |
| Cannot run without agents + exit | `Simulation.SetRunning` / `ToggleRunning` |
| UI catch-up step cap | `CrowdSimApplication.AdvanceSimulation` |

## Regression coverage

`CrowdSim.Tests` includes checks for finite/capped SFM behaviour, panic increasing the desired SFM
drive, ordered explosion bands, blast shielding, real exit openings, line of sight, non-finite state
recovery, start guardrails and exit flow.

## Stage 1-3 additions

| Added guardrail / behaviour | C# location |
|---|---|
| Direct event panic starts after per-agent shock-front exposure | `ExplosionSystem` + `Agent.ShockwaveHitFrame` + `EvacuationSystem.UpdatePanic` |
| Wall shielding re-evaluated at front arrival | `ExplosionSystem.ApplyExplosionEffects` |
| Running-crowd contagion requires minimum visible runners | `EvacuationSystem.UpdatePanic` |
| Running signal requires abnormal speed | `EvacuationSystem.UpdatePanic` |
| Running signal requires directional coherence | `EvacuationSystem.UpdatePanic` |
| Running/social contagion blocked by walls/LoS | `EvacuationSystem.UpdatePanic` |
| Poor visibility reduces contagion/herding perception range | `EvacuationSystem.UpdatePanic` / `ComputeHerdDirection` |
| Visibility field propagates, builds/holds/decays and is bounded | `VisibilitySystem` |
| Visibility field blocked by walls/blockages | `VisibilitySystem` |
| Visibility alters perception/navigation, never adds a physical force | `VisibilitySystem` / `EvacuationSystem` |
| Casualty avoidance uses spatial grid | `EvacuationSystem.UpdateEvacAgent` |
| Casualties remain collision/SFM obstacles | `Agent.IsCasualtyObstacle`, `SfmEngine`, `Simulation.Collisions` |
| Dynamic rerouting has minimum evaluation interval | `EvacuationSystem.MaybeReselectExit` |
| Route commitment prevents exit thrashing | `Agent.ExitCommitUntilFrame` / `MaybeReselectExit` |
| Alternative exit must be materially better | `ExitSwitchImprovementRatio` / `MaybeReselectExit` |
| Near-door commitment preserved | `EvacuationSystem.MaybeReselectExit` |
| Debris swept wall collision prevents tunnelling | `ExplosionSystem.UpdateDebris` |
| Debris swept agent collision prevents tunnelling | `ExplosionSystem.UpdateDebris` |
| Debris normalized energy decays with travel | `Debris.Advance` |
| One debris fragment can cause at most one person impact | `ExplosionSystem.UpdateDebris` |
| Debris injury uses startle/reaction path for mobile survivors | `ExplosionSystem.ApplyDebrisImpact` |
| Debris severity is normalized, not real ordnance calibration | `AgentConfig` / `ExplosionSystem` documentation |
| Explosion placement rejects solid wall/blockage locations | `SimulationUi.HandleWorldInput` |
