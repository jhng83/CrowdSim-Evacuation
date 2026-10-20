# CrowdSim Unity migration — M4

Open **UnityProject/** with **Unity 6000.3.26f1** (URP 17.3.0). Open
**Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play, and choose
**Build furnished example (26 people)** or **Build an Explosion scene**.

1. The editable example has all five furniture kinds, three obstacle choices,
   26 people (11 seated), one exit and tick zero.
2. In a blank scene, use the unified **Asset** picker for obstacles/furniture.
   Set facing, inspect green/red previews, place and undo whole sets.
3. Tables/stages use quarter turns. Barricades retain free/exact angles, +/-15°,
   R/Shift+R over the viewport and optional parallel end snapping. Each asset
   remembers its own facing; stages are limited to one.
4. Finalise/start, arm an Explosion and click the room. Inspect seated/rising
   people, moving/tipped furniture and destruction using pause and playback speed.

Follow [the full M4 checklist](../docs/unity-migration/M4_LOCAL_TESTING.md) for
precise dimensions, expected counts, manual controls and **17 EditMode / 5 PlayMode**
supplied tests. M1–M3 were accepted by the user; M4 awaits actual Unity testing.
The original reference application and tests remain unchanged.

Furniture positions, height, heading, states and occupancy come from detached
portable-engine snapshots. The model retains seating/departure, pushing/chains,
impacts/flight/support/landing, destruction and dynamic navigation; Unity physics
does not move the crowd or furniture. Simple seated/rising parts complement the
existing walking capsules. Full humanoids/animation/cameras remain M8.

Files/replay, full hazard controls, complete analysis, recording and standalone
acceptance retain their [approved milestones](../docs/unity-migration/ROADMAP.md).
The common Toxic Fume builder includes furniture, while source/precompute/run
authoring remains M7. The accepted M2 six-case diagnostics remain at
**Assets/CrowdSim/Scenes/Validation/SimulationParity.unity** and are accessible
from startup. No original .NET executable/SDK is needed locally; first import
needs access to Unity's package registry.

Cloud checks pass 404 original tests, 415 portable tests, 70 migration tests and
both exact six-case captures. **M4 Unity import, frontend compilation, supplied
Unity tests, input/picking/layout/graphics and player builds were not executed**;
there is no Unity Editor installed in cloud. See
[VALIDATION.md](../docs/unity-migration/VALIDATION.md).
