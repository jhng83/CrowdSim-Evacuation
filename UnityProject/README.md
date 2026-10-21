# CrowdSim Unity migration — M7

Open **UnityProject/** with **Unity 6000.3.26f1** (URP 17.3.0), then
**Assets/CrowdSim/Scenes/Bootstrap.unity** and press Play.

1. Choose **Build a Toxic Fume scene**, author exits/people, open **Hazard**, and
   **Set / move release point** on clear floor. Adjust the four basic and six advanced
   settings; geometry/source/settings changes require recomputation.
2. **Compute dispersion**, inspect cache statistics, then scrub the preview. People,
   time, exposure and authored data remain unchanged. Plume/grid visibility is independent.
3. Finalise/start and **Release Toxic Fume**. Inspect concentration/exposure, then
   replay to paused frame zero with restored starts, furniture and an unreleased cache.
4. For a ready example choose **Open 205-person Fume example** at startup. Expect
   205 people, 80 seated and the same counts/zero exposure after replay.

The requested visual refinement adds scenario cards, clear action/navigation states,
phase/readiness badges, a concentration legend, lit materials, procedural finishes,
softened furniture edges and owned studio lighting. Both scenarios retain original
physical dimensions/behavior; full humanoid poses/cameras remain M8.

Follow [M7_LOCAL_TESTING.md](../docs/unity-migration/M7_LOCAL_TESTING.md), including
**34 EditMode / 12 PlayMode** supplied cases, visual/input review and both Hybrid
modes. M1–M6 were accepted by the user; M7 awaits actual local Unity acceptance.
Saved files remain in the displayed persistent directory. No original .NET executable/
SDK is needed locally; first import needs access to Unity's package registry.

Cloud checks pass 404 original, 415 portable and 135 migration tests, both exact
six-case captures and five file interop cases. **M7 Unity import/frontend compilation,
Unity tests, input/layout/material/light/shader rendering and player builds were
not executed**; no Unity Editor is installed in cloud. See
[VALIDATION.md](../docs/unity-migration/VALIDATION.md) and the
[approved roadmap](../docs/unity-migration/ROADMAP.md). Standalone acceptance remains M11.
