# UI Refactor 1 - Docked Operator Dashboard

## Scope

This revision changes only the `CrowdSim.OpenGL` presentation and interaction layer. The
panic/stress model, SFM, A* routing, collision handling, blast/debris outcomes, bottleneck
pressure, exit admission and all numerical guardrails remain unchanged.

## Layout

The application now reserves five stable interface regions:

1. **Top status bar** - scenario name, unsaved-change marker, mode, simulated time,
   in-room/exited/fatal counts, FPS and simulation speed.
2. **Left control panel** - mode-aware Builder or Runtime actions, camera and scenario files.
3. **Centre 3D viewport** - dedicated reserved space; no floating blast window covers it.
4. **Right analysis inspector** - `BLAST`, `AGENT`, `METRICS` and `DEBUG` tabs.
5. **Bottom status bar** - current status message plus the active world tool and instruction.

The left panel automatically changes content between Builder and Simulation modes. Detailed
behaviour tuning remains collapsed by default, and the right inspector separates analysis
controls from operational controls.

## Blast radius text editing

Blast-zone radii no longer use `DragFloat`. Each radius is a normal text field backed by a
small editor state:

- single-click places the blinking insertion caret at the clicked location;
- typing inserts at the caret instead of selecting/replacing the complete number;
- click-and-drag selects a chosen range;
- double-click or `Ctrl+A` selects the complete value intentionally;
- Enter or clicking elsewhere commits a valid value;
- Escape restores the previous committed value;
- invalid entries retain the last valid simulation value and display a red field/error message;
- `-` and `+` buttons remain available for mouse-only one-unit adjustment.

The validated ordering remains:

```text
Kill < Incapacitation < Fallen < Injury < Panic
```

Once an explosion is placed, radius controls become visibly read-only. Zone visibility toggles
remain available.

## Mode-aware controls

### Builder

- caret-based agent-count entry;
- Place Exit / Place Blockage tools;
- exit-width selection;
- Undo Last Placement;
- collapsed Behaviour Tuning;
- scenario Save/Load;
- Finalise Scene.

### Simulation

- visually differentiated Start, Pause and Resume actions;
- simulation-speed control;
- Select Agent / Place Explosion world tools;
- Clear Selection and New Scenario;
- camera/follow/POV controls;
- scenario Save/Load.

## Right analysis inspector

### Blast

Persistent zone visibility, caret-based radii, reset, lock state and colour legend.

### Agent

Selected-agent state, intent, stress, movement, routing, bottleneck diagnostics, follow camera,
POV and clear-selection controls. The tab label shows the selected agent ID.

### Metrics

High-value outcomes and exit flow are visible first. Less frequently needed values are under
`ADVANCED METRICS` and `SCENARIO STATUS`.

### Debug

Render-only vector/vision/congestion/trail controls, the agent colour legend and model summary.

## Scenario dirty state

The top bar and Scenario Files panel indicate unsaved changes after changes to agent count,
behaviour tuning, exits, blockages or blast radii. Successful Save and Load operations clear the
marker. Display-only debug/zone visibility toggles do not mark the scenario as changed.
