# CrowdSim Unity migration — M6

Open **UnityProject/** with **Unity 6000.3.26f1** (URP 17.3.0), open
**Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play and choose
**Build furnished example (26 people)**.

1. Open **Hazard**. Edit the five ordered metric Explosion radii with normal text:
   Enter/click elsewhere commits, Escape cancels, +/-0.10 increments and Reset Zone
   Defaults restores values. Radii remain editable until detonation, including
   during a run. Invalid input retains committed values.
2. Finalise/start, **Arm Explosion**, and click clear floor once. Cancel/Escape/pause
   clears arming; blocked clicks retain it. Detonation locks radius editing while
   visibility stays adjustable.
3. Inspect the timed front, outward debris, dust/visibility field, persistent clipped
   zone bands/outlines and event/agent metrics. Hiding the inspector keeps overlays
   and flags. Toggling display must not change damage or physics.
4. Save and **Replay from start**. Expect paused tick zero, original positions,
   **11 seated**, intact furniture, no event effects and unlocked radii. Settings
   edited before detonation remain; runtime damage/motion never becomes authored data.

Follow [the complete M6 checklist](../docs/unity-migration/M6_LOCAL_TESTING.md),
including blocked/edge/shielding experiments, both Hybrid modes, saved Fume regression,
resize/input and **29 EditMode / 9 PlayMode** supplied tests. M1–M5 were accepted
by the user; M6 awaits actual Unity testing. Original source/tests and all pins
are preserved. Full Fume authoring remains M7 and standalone acceptance M11.

The accepted Files workflow and schema 1–4 examples remain. Saves use the displayed
`Application.persistentDataPath/scenarios` directory. No original .NET executable/
SDK is needed locally; first import needs Unity's package registry. The accepted
M2 diagnostics are also available on startup and at
**Assets/CrowdSim/Scenes/Validation/SimulationParity.unity**.

Cloud checks pass **404 original**, **415 portable** and **112 migration** tests,
both exact six-case captures and five file interop cases. **M6 Unity import,
frontend compilation, supplied Unity tests, native input/layout/shader rendering
and player builds were not executed**; there is no Unity Editor installed in cloud.
See [VALIDATION.md](../docs/unity-migration/VALIDATION.md) and the
[approved roadmap](../docs/unity-migration/ROADMAP.md).
