# Builder Pass 1 - Room Size, Blue Walls and Spatial Agent Placement

## Scope

This pass extends Builder mode only. It does not intentionally alter panic, SFM parameters,
behavioural traits, bottleneck pressure, exit capacity, blast outcomes or casualty rules.
Static navigation was extended so the new blue wall type is treated as solid geometry.

## Variable simulation area

- Builder exposes **Room width (m)** and **Room length (m)** sliders.
- Supported range: 320-4000 world units wide and 240-3000 world units long.
- Resize is allowed only while the builder is empty.
- The first exit, regular blockage, blue wall or agent group locks the room dimensions.
- Undoing all placements makes resizing available again.
- Room dimensions are persisted in Scenario Save/Load.

This changes the simulation-world rectangle; it is not a desktop-window-resolution control.

## Blue wall tool

**Place Blue Wall (2 x 2)** creates a fixed square solid obstacle that is two builder grid cells by
two builder grid cells. With the current 1.20 m metric grid, the legacy 2 x 2 footprint corresponds to 2.40 m x 2.40 m.

Blue walls:

- snap to the builder grid;
- must fit fully inside the room;
- cannot overlap existing walls, exits or planned people;
- are drawn blue in the 3D view;
- join `SimulationEnvironment.Walls`, so ordinary wall collision, line-of-sight and blast shielding
  see them automatically;
- are explicitly included in `VisibilityGraphPlanner`, so body-clear A* routes around their corners;
- are persisted separately from ordinary blockages so their blue visual identity survives Save/Load;
- participate in Undo Last Placement.

## Mouse-based population placement

The typed population input has been removed. Builder exposes four region-placement tools:

- **Place 1 Agent**
- **Place 5 Agents**
- **Place 15 Agents**
- **Place 50 Agents**

After choosing a quantity, the user clicks a preferred region in the world. The placement resolver
searches deterministically outward from that click and commits the complete group only if it can find
the requested number of safe starts.

Candidate starts must:

- remain inside the body-clear room interior;
- clear boundary walls, regular blockages and blue walls by slightly more than agent radius;
- maintain non-overlapping spacing from previously planned starts;
- maintain non-overlapping spacing within the new group.

If the click is on or beside blocked floor, the resolver keeps searching the surrounding free space.
This lets a 50-person region distribute around a wall instead of placing people through it. If the
room cannot fit the complete requested group, the click is rejected rather than silently adding a
partial population.

Cyan builder markers show the exact resolved starting positions and each group centre is labelled
with its requested count.

## Finalise, restart and persistence

When mouse-planned groups exist, **Finalise Scene** creates agents at those exact resolved positions.
The seeded behavioural trait generation is retained. **Restart from Beginning** reconstructs the same
scenario and respawns the same builder-defined starts.

Scenario JSON now stores:

- room width/length;
- exits;
- regular blockages;
- blue walls;
- agent spawn-group centres and exact starting positions;
- random seed;
- blast-zone radii;
- behaviour tuning.

Builder-defined initial positions are scenario configuration. Runtime-moved positions, injuries,
panic, debris, elapsed time and exited state remain intentionally excluded.

Legacy scenario JSON without `BlueWalls` or `AgentSpawnGroups` remains compatible through default
empty lists. A legacy `AgentCount` therefore continues to use the previous seeded random-spawn fallback
until the population is replaced with mouse-planned groups.

## Regression coverage

This pass adds six focused tests covering:

1. 2 x 2 blue-wall dimensions and solidity;
2. 50-person obstacle-aware regional placement;
3. exact final spawn positions;
4. A* navigation around blue walls;
5. room resizing/locking;
6. Scenario Save/Load of dimensions, blue walls and planned starts.

Expected total after this pass: **66 tests**.
