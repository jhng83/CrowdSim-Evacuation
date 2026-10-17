# Builder Pass 2 - Grouped Placement Controls

This pass changes Builder UI ergonomics only, plus the requested regular-blockage size option and 5-person exit width.

## Exit placement

The EXIT group now contains one width dropdown and one placement button. Width options are:

- 1-person
- 3-person
- 5-person

After selecting a width, press **Place Exit** and click a boundary wall. The existing boundary-only placement, overlap checks and one-person 1.2x clearance rule remain active.

## Regular blockage placement

The BLOCKAGE group now contains a size dropdown and one placement button. Options are:

- 1 x 1 builder grid cells
- 2 x 2 builder grid cells

The original regular blockage size was exactly one builder grid cell (`Grid.CellSizeMeters = 6 x agent radius` (1.20 m by default)), so the 1 x 1 option is backward-compatible with prior behaviour. Both sizes use the existing snap, room-boundary and overlap guardrails.

## Agent placement

The AGENTS group now uses a dropdown with:

- 1 agent
- 5 agents
- 15 agents
- 50 agents

Press **Place Agent(s)** and click a region. The existing deterministic outward placement search is unchanged: it avoids boundary walls, regular blockages, blue walls and previously planned agents, and rejects the whole group if all requested agents cannot be placed safely.

## Blue wall

The dedicated **Place Blue Wall (2 x 2)** tool is unchanged.

## Preview and persistence

Builder mouse previews now reflect the selected regular-blockage size, exit width and agent count. Scenario Save/Load remains compatible because the scenario stores the resulting wall rectangles, exit geometry and exact planned agent positions rather than transient dropdown selections.

## Regression

Two tests were added:

- regular blockage 1 x 1 / 2 x 2 sizing;
- 5-person exit width.

Current expected total: **68 tests**.


## Metric circular blockage

The current metric builder includes **Circular - Ø 2.5 m** inside the same Blockage dropdown as the two square sizes. The single **Place Blockage** action then places the selected shape. The circular choice creates a true 1.25 m-radius solid obstacle used by rendering, collision, SFM wall repulsion, visibility/shielding, and body-clear route planning.
