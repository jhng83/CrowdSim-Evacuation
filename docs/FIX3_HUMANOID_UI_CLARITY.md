# FIX3 - Humanoid Visual Restoration and UI Clarity

## Scope

This pass is presentation/UI focused. Crowd physics, panic, SFM, routing, exit flow, blast effects and metric collision geometry are not intentionally changed.

## Humanoids

The metric conversion correctly scaled body geometry, but several render-only motion thresholds still used their legacy numeric magnitudes. That made normal metric pedestrian velocities look almost stationary to the gait system. FIX3 scales those thresholds to metres per simulation step, restoring the standing/walking/running/crawling blends.

The renderer keeps three LOD levels:

1. Full articulated faceted 3D model for large/close/selected agents.
2. Reduced faceted 3D model for the normal overview.
3. Cheap screen-space model for very small agents or heavy scenes. The cheap model now uses split torso facets, shaded limbs, low-poly head and animated feet so it retains the low-poly character while remaining inexpensive.

Adaptive LOD still activates at high simulation speed, high population or sustained high draw cost.

## Builder UI

The blockage controls now use one dropdown:

- Square - 1 grid cell
- Square - 2 grid cells
- Circular - Ø 2.5 m

A single Place Blockage button activates the selected option. Changing the dropdown while blockage placement is already active immediately updates the placement preview/tool.

Ctrl+Z now uses a held-key latch (`IsKeyDown`) rather than relying on a late-frame `IsKeyPressed` edge, preventing missed undo presses in the docked ImGui layout.

## Explosion discoverability

Place Explosion is yellow while unused/idle and available, because it is the intended event trigger for crowd dispersion. When armed it changes to orange/red and its label explicitly tells the user to click the world.
