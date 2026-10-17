> Navigation update: normal walking evacuees now share the bounded natural-recovery
> controller with Explosion. See [NATURAL_NAVIGATION.md](NATURAL_NAVIGATION.md) for local
> fume-risk scoring, detour revalidation, exit reconsideration and performance limits.
> The dispersion/exposure/incapacitation model described below is unchanged.

# Toxic Fume Scenario

## Purpose

The **Toxic Fume** scenario is a second hazard mode alongside the existing **Explosion** scenario. It is designed to compare how an evolving contaminated region changes crowd dispersion while preserving the same pedestrian model: panic/social contagion, exit selection, obstacle routing, Social Force Model (SFM), collision handling, metrics, recording, and camera controls.

The fume model is intentionally **generic and normalized**. Concentration is represented on a 0–1 simulator scale and cumulative exposure uses normalized concentration-seconds. It is not calibrated to a named chemical, lethal-dose table, HVAC model, or CFD solver.

## Performance architecture

Fume propagation does **not** run as a live dispersion solver during the crowd simulation.

1. In Builder mode, choose a release point and fume configuration.
2. Press **Compute Fume Dispersion**.
3. `ToxicFumeSystem` performs one obstacle-aware Dijkstra-style propagation pass over a 0.5 m grid.
4. The runtime cache retains only:
   - arrival time per cell,
   - peak normalized concentration per cell,
   - blocked/open-cell information.
5. Temporary path-distance data is discarded after precomputation.
6. During the live run, each active agent performs only a cached concentration lookup plus exposure accumulation. Panic logic reuses that already-looked-up concentration.
7. Fume rendering automatically subsamples large grids so visualization does not require one draw primitive per cached cell.

The Builder displays the measured precomputation time and approximate cache size.

## User workflow

### 1. Choose scenario

At application start choose either:

- **Explosion** — existing blast/shock/debris scenario.
- **Toxic Fume** — precomputed spreading normalized plume.

The two modes are separate experimental conditions; selecting Toxic Fume does not enable explosion physics.

### 2. Build the common scene

Configure room size, exits, square/circular blockages, agent groups, and normal behavior tuning exactly as in the Explosion scenario.

### 3. Configure Toxic Fume

The Toxic Fume panel provides:

- **Release point** — placed by clicking clear floor in the world.
- **Initial cloud radius** — contaminated region present immediately when release is triggered.
- **Dispersion speed** — abstract propagation speed used only by the setup-time field calculation.
- **Maximum plume extent** — maximum obstacle-aware path distance from the source; constrained to be at least the initial radius.
- **Release duration** — duration for which reached cells remain supplied before generic decay.
- **Drift direction / directional drift** — optional abstract directional bias; zero drift produces symmetric propagation through available paths.
- **Awareness threshold** — normalized concentration that can directly alert an agent.
- **High-threat threshold** — normalized concentration that supplies stronger threat/panic stimulus.
- **Impairment / incapacitation exposure** — generic cumulative thresholds used for crowd-response experiments.

### 4. Precompute and preview

Press **Compute Fume Dispersion**. The UI reports cache readiness, reachable cells, grid resolution, cache size, precompute time, and maximum arrival time.

A preview timeline scrubber visualizes the cached plume before the scene can be finalised. Changing room geometry, the release point, or any fume parameter invalidates the cache and requires recomputation.

### 5. Run

Finalise the scene, start the crowd simulation, then press **Release Toxic Fume** when the experimental event should begin.

The plume evolution is replayed from the setup-time cache. Pedestrian response remains live:

- local concentration affects direct threat/panic,
- social contagion remains active,
- contaminated routes receive an exit-selection penalty,
- direct-threat motion can prefer locally lower concentration,
- cumulative exposure can reduce mobility or incapacitate an agent,
- all movement still passes through the existing SFM/collision/navigation pipeline.

## Visualization

The FUME inspector can toggle:

- concentration plume,
- fume grid cells.

The setup release-point tool shows a world-space preview of the initial cloud radius. The runtime field is shown as a translucent concentration overlay. Large cached grids are rendered with an adaptive stride for lower UI/GPU overhead.

## Metrics

The existing crowd metrics remain available. Toxic Fume additionally reports:

- ever exposed agents,
- impaired agents,
- fume-incapacitated agents,
- average cumulative normalized exposure,
- peak normalized concentration observed by the population,
- number of agents currently above the high-threat concentration.

Selected-agent inspection also shows current concentration, peak concentration, cumulative exposure, exposure status, and toxic mobility factor.

## Scenario persistence

Scenario files are now version 3 and save the scenario type plus Toxic Fume configuration/source. Legacy version-1 and version-2 files are migrated as **Explosion** scenarios. The precomputed cache itself is deliberately not saved; after loading a Toxic Fume scenario, recompute the plume so it is guaranteed to match current geometry and code.
