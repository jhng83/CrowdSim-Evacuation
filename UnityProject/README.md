# CrowdSim Unity migration — M9

Open **UnityProject/** with **Unity 6000.3.26f1**, URP **17.3.0**, then
**Assets/CrowdSim/Scenes/Bootstrap.unity** and press Play. Assets, Packages and
ProjectSettings are all inside UnityProject. No original .NET SDK/executable is
needed locally; first import needs Unity package-registry access.

M9 completes overview/agent/navigation/assistance/exit diagnostics, eight batched
vector/vision/congestion/trail overlays, paused Hybrid switching, distinct CPU/FPS/
managed-allocation readouts and seeded 220/500/1000-person comparison layouts.
The requested professional architectural theme uses black text, pale panels,
off-white square/circular pillars with black outlines and an off-white floor with
a default-on 1.2 m grid. Physical dimensions and simulation algorithms are preserved.

1. Try the furnished Explosion and 205-person Fume examples. Open Overview, Agent
   and Debug; exercise every overlay with the inspector hidden and Follow/POV.
2. Debug → Reproducible crowd comparisons creates exact-size open/furnished layouts
   for both hazards. Use replay and the paused Hybrid checkbox for comparisons.
3. Follow the numbered [M9 checklist](../docs/unity-migration/M9_LOCAL_TESTING.md),
   including **49 EditMode / 19 PlayMode** supplied cases and native visual/input review.

M1–M8 were accepted by the user; M9 awaits local testing. Cloud passes 404 original,
415 portable and 191 migration tests, unchanged traces/oracle, five file round trips
and [24 CPU comparison cases](../docs/unity-migration/M9_BENCHMARK.md).
**Unity import/frontend compilation, Unity tests, font/shader rendering, native
input, graphical scale/FPS/GPU checks and player builds were not executed** here.
See [validation](../docs/unity-migration/VALIDATION.md),
[design](../docs/unity-migration/DESIGN.md) and [status](../docs/unity-migration/STATUS.md).
Recording remains M10, final standalone acceptance M11.
