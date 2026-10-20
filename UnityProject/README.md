# CrowdSim Unity migration — M5

Open **UnityProject/** with **Unity 6000.3.26f1** (URP 17.3.0), open
**Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play, and choose
**Build furnished example (26 people)**.

1. Open **Files**, enter a name and **Save**. The displayed save directory is
   `Application.persistentDataPath/scenarios`. Save As creates copies; replacing
   another existing target asks for confirmation. Read-only sources are labelled.
2. Run/trigger the example's Explosion, then **Replay from start**. Expect paused
   frame zero, exact starts, 11 seated people and intact furniture. Runtime Save
   always writes authored starts, excluding current damage/motion.
3. Load the bundled schema 1–4 examples while paused. Loaded scenes open in the
   builder; finalise before running. Fume205 finalises/precomputes its saved source;
   Start > **Release saved Fume** > Replay restores 205 people, 80 seated and an
   unreleased regenerated cache. Full Fume source/settings/preview UI remains M7.
4. Test dirty Save/Discard/Keep decisions, New Scenario, failed imports and persistence
   using [the full M5 checklist](../docs/unity-migration/M5_LOCAL_TESTING.md). Run
   **25 EditMode / 7 PlayMode** supplied cases. M1–M4 were accepted by the user;
   M5 awaits actual Unity testing.

All accepted furniture/build/run capabilities remain. The original reference
application/tests are preserved. No .NET executable/SDK is required locally;
first import needs Unity's package registry. Assets/Packages/ProjectSettings are
inside this directory. This is an Editor project; standalone delivery remains M11.
The accepted M2 diagnostics remain at
**Assets/CrowdSim/Scenes/Validation/SimulationParity.unity** and on startup.

Cloud checks pass 404 original, 415 portable and 91 migration tests, both exact
six-case captures, and five native/portable file interop cases. **M5 Unity import,
frontend compilation, supplied Unity tests, native input/layout/rendering and
player builds were not executed**; no Unity Editor is installed in cloud. See
[VALIDATION.md](../docs/unity-migration/VALIDATION.md) and the
[approved roadmap](../docs/unity-migration/ROADMAP.md) for remaining milestones.
