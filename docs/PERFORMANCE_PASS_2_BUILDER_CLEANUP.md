# Performance Pass 2 + Builder Cleanup

This revision is based directly on the user-uploaded `CrowdSimCs(1).zip` branch.

## Builder cleanup

- Removed the separate **Blue Wall** placement tool from the Builder UI.
- Regular **Blockage** placement remains available as `1 x 1` or `2 x 2`, so there is now one obstacle-placement workflow instead of two overlapping tools.
- Legacy scenario files containing `BlueWalls` remain supported and continue to render/collide correctly when loaded; only new Blue Wall placement was removed.
- Added **Ctrl+Z** as the Builder hotkey for **Undo Last Placement**. The shortcut is ignored while a text field owns keyboard input so normal text editing is not disturbed.

## Performance Pass 2

The performance work targets the exact uploaded branch and does not lower the fixed simulation timestep or remove panic/SFM/herding/bottleneck behaviour.

- Reuses spatial-hash agent buckets instead of allocating fresh per-cell lists every physics step.
- Adds arbitrary-point spatial agent queries for exit occupancy, exit density, exploration crowd checks and debris collision candidates.
- Removes several LINQ arrays/lists/sorts from exit selection, exit-flow admission and UI metrics.
- Reuses exit/debris/collision scratch buffers in hot paths.
- Adds cached static visibility-graph geometry. Obstacle-corner visibility between static walls is rebuilt only when the room layout changes; individual route requests connect start/goal to the cached graph.
- Adds a monotonic wall-layout version so renderer/navigation caches invalidate without hashing the complete wall list each frame.
- Uses spatial wall queries for SFM wall forces in obstacle-heavy rooms instead of evaluating every wall for every pedestrian.
- Uses spatial wall culling for very wall-heavy line-of-sight/body-clear path tests while retaining the simpler exact scan for small/moderate layouts where it is cheaper.
- Explosion shielding and swept debris/wall checks reuse the same optimized line-of-sight path.
- Persistent blast-wall clipping and wall render chunks invalidate only when static geometry changes.
- Adds adaptive screen-space animated humanoid LOD under high render load/population while retaining moving limbs and keeping selected/followed agents detailed.
- Panic/selection rings use a single projected ImGui circle instead of dozens of projected world-line segments per agent.
- Render-position smoothing is time-based so an FPS drop no longer creates the strong trailing/delayed-motion appearance of a fixed interpolation factor.
- Congestion rendering reuses the simulation's existing `LocalCongestion` value.
- Existing asynchronous recording remains intact.

## Compatibility

- Scenario Save/Load still accepts historical `BlueWalls` entries.
- Existing 1x1/2x2 regular blockage persistence is unchanged.
- Crowd state colours, animated humanoids, blast zones, restart, recording and the performance HUD remain available.
- Expected regression-suite total remains **68 tests**.
