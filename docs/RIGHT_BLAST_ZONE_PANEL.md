# Right-side Blast Zone Panel

This revision leaves the existing left-hand scene/runtime/camera/status UI unchanged and adds one compact blast-zone panel anchored at the top-right of the application. The default 3D camera leaves that area as unused black background, so the panel does not resize the central scene or cover the floor/grid in the normal view.

## Zones

The normalized simulation keeps five ordered direct-event bands:

1. Kill
2. Incapacitation
3. Fallen / severe
4. Injury
5. Panic / threat

The panel provides an independent visibility checkbox for each band and a master `Show blast zones` switch. Once an explosion has occurred, the wall-clipped bands remain visible as semi-transparent analysis overlays after the active shock front has disappeared. They do not re-apply damage.

## Radius editing guardrails

The radii can be edited before detonation. `AgentConfig.SetBlastZoneRadii` enforces strict inner-to-outer ordering and finite bounded values. Radius controls lock after detonation so the displayed zones cannot diverge from the radii used to calculate that event's outcomes. `Reset Zone Defaults` restores the validated simulation defaults.

The values are abstract normalized crowd-simulation parameters, not real-world explosive-yield, fragmentation, or medical-effect calibration.
