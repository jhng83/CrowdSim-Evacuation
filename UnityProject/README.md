# CrowdSim Unity migration — M8

Open **UnityProject/** with **Unity 6000.3.26f1** (URP 17.3.0), then
**Assets/CrowdSim/Scenes/Bootstrap.unity** and press Play.

M8 adds pooled volumetric humanoids with all state/posture/assistance poses,
near/far detail, animation, dark silhouettes, panic/alarm/help/selection indicators,
rendered-body picking, Follow and head-height POV. Camera/selection cleanup follows
exit/replay/new/load. Inspector hiding retains selection and camera mode.
The bundled Atkinson Hyperlegible font, larger text, focus boundaries and checked
text contrast carry the user's design preference into both scenarios.

1. Choose **Open furnished example**, Finalise and Start, then inspect Explosion
   responses, low poses, selection, Follow/POV and zoom 0.45×/1×/2.5×.
2. Choose **Open 205-person Fume example**, Compute dispersion, Finalise, Start and
   Release Toxic Fume. Repeat camera/picking/pause/hidden-inspector checks.
3. For deterministic visual checks, open
   **Assets/CrowdSim/Scenes/Validation/HumanoidPresentation.unity**. It uses the same
   production renderer for 14 pose/state fixtures; no simulation is run there.

Follow the complete numbered [M8 checklist](../docs/unity-migration/M8_LOCAL_TESTING.md).
M1–M7 were accepted by the user; M8 awaits local testing. **41 EditMode / 15 PlayMode**
cases are supplied, unexecuted in cloud. First import needs Unity package-registry
access; no original .NET SDK/executable is needed locally. Saves remain in the
persistent directory displayed on Files.

Cloud passes 404 original, 415 portable and 159 migration tests, both exact six-case
captures and five file round trips. **Actual M8 Unity import/frontend compilation,
font/shader rendering, Unity tests, native input/layout and player builds were not
executed** (no Editor installed). See [validation](../docs/unity-migration/VALIDATION.md),
[design/font license](../docs/unity-migration/DESIGN.md) and
[roadmap](../docs/unity-migration/ROADMAP.md). M9 and later remain unimplemented.
