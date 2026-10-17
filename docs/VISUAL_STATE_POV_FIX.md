# Visual State, Blast-Zone Clipping, and Agent POV Revision

This revision changes only the OpenGL/ImGui presentation and camera layer. `CrowdSim.Core` and its panic-influenced Social Force Model, explosion physics, collision rules, evacuation logic, casualty state transitions, wall/deadlock handling, and numerical guardrails are unchanged.

## 1. Explosion-zone rendering respects walls

The earlier renderer drew complete circular blast-zone outlines even when part of a radius extended beyond a room wall or behind an internal blockage. Physics already used `ExplosionSystem.BlastPathIsBlocked(...)`, so this was a visual mismatch.

The renderer now samples each blast-zone ring as short arc segments and draws an arc only when:

- its endpoints and midpoint remain inside the room interior; and
- the same wall-ray test used by the blast shielding model reports a clear path from the explosion origin.

This means blast-zone outlines stop visually at boundary walls and internal blockages instead of appearing through them or outside the room.

## 2. Agent body colour represents state only

The earlier renderer blended every active humanoid toward orange/red as panic increased. This made a green evacuating agent appear brown/orange even though its simulation state was still `Evacuating`.

Body colour is now reserved for discrete agent state:

- Green: Evacuating
- Amber: Waiting to react / reaction freeze
- Blue: Injured
- Yellow: Crawling
- Red: Fallen
- Dark red: Incapacitated / temporary incapacity
- White: Wandering / normal

Panic remains visible through the red/orange ground ring. The ring radius/intensity continues to reflect `PanicLevel`; no panic physics were removed.

## 3. Follow camera and first-person Agent POV

After selecting a humanoid, the sidebar now offers:

- **Follow Selected Agent**: keeps the selected agent centred while retaining the elevated 3D view.
- **Enter Agent POV**: switches to a head-height forward-looking perspective camera that follows the selected agent's movement/goal heading.
- **Exit Agent POV**: returns to the follow camera.
- **Stop Following Agent** or **Reset Camera**: returns to the normal isometric view.

If the selected agent exits the simulation, follow/POV mode ends automatically and the normal camera is restored. First-person mode also hides the selected agent's own humanoid so its body does not obstruct the camera.

World placement/selection clicks are intentionally disabled while Agent POV is active; the sidebar remains available for exiting POV and inspecting live metrics.
