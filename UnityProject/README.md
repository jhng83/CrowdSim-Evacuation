# CrowdSim Unity migration — M3

Open **UnityProject/** with **Unity 6000.3.26f1** (URP 17.3.0). Then open
**Assets/CrowdSim/Scenes/Bootstrap.unity**, enter Play Mode and choose
**Build an Explosion scene**.

1. Set room size before placing anything.
2. Place a boundary exit and people; add squares/circles as desired.
3. Adjust behavior and Hybrid setup, then **Finalise scene**.
4. **Start**, **Arm Explosion**, then click inside the room. Pause/Resume and change
   playback speed with the fixed footer.
5. Test camera controls, inspector/F4, undo before finalisation and input focus.

Follow [the M3 acceptance checklist](../docs/unity-migration/M3_LOCAL_TESTING.md)
for exact expected results and **13 EditMode / 3 PlayMode** supplied tests.
M1 and M2 were accepted by the user; M3 awaits your actual Editor checks.

The common builder is also accessible from the Toxic Fume entry, with a readiness
explanation that source/precompute/run authoring arrives in M7. The accepted M2
scene remains **Assets/CrowdSim/Scenes/Validation/SimulationParity.unity** and is
reachable from startup; it runs all six bounded preset/Hybrid checks.

M3 uses 3D capsules/blocks, not the full M8 humanoids/poses. Furniture authoring,
file/replay workflows, detailed hazard controls/graphics, complete analysis and
recording retain their [approved milestones](../docs/unity-migration/ROADMAP.md).
No standalone player is delivered yet. No original .NET executable or SDK is needed
locally; first import needs Internet access to Unity's package registry.

Production simulation and workspace commands live under
`Packages/com.crowdsim.core/Runtime/` and compile in the cloud harness too. All model
changes pass through session commands; rendering consumes detached snapshots.
Transport schedules the engine's configured fixed step independently of render FPS
and Unity FixedUpdate. The original source/tests remain in the repository as the
unchanged behavioral reference.

The cloud has no Unity Editor. **M3 import, frontend compilation, Unity tests,
input/picking/layout/graphics and player builds were not executed.** Cloud results
are recorded separately in [VALIDATION.md](../docs/unity-migration/VALIDATION.md).
