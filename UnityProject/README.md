# CrowdSim Unity migration — M2

Open this directory with **Unity 6000.3.26f1** (URP **17.3.0**, UI Toolkit,
.NET Standard 2.1). No .NET SDK or original simulator process is required.

1. Add `UnityProject/` in Unity Hub and allow packages to import.
2. Open **Assets/CrowdSim/Scenes/Validation/SimulationParity.unity**.
3. Enter Play Mode. Use the three presets, Hybrid switch, single-step, 1x playback,
   pause/reset and **Run full 120-tick check**. Expect **PASS · 7/7 checkpoints**
   and **Replay restored frame zero** in all six preset/mode combinations.
4. Follow [the full M2 checklist](../docs/unity-migration/M2_LOCAL_TESTING.md),
   including the supplied **9 EditMode / 2 PlayMode** tests.

The diagnostic camera/world are created at runtime. Simplified raised capsules and
blocks consume detached portable-engine snapshots; Unity physics does not move
the crowd. The preset event triggers between observations 10 and 11, and runs stop
at tick120. Full builder, files workflow, hazard UI/graphics, humanoids, cameras,
analysis and recording retain their [approved milestones](../docs/unity-migration/ROADMAP.md).

The accepted M1 scene remains **Assets/CrowdSim/Scenes/Bootstrap.unity**, now with
an **Open M2 parity** button. Return to startup is available in the diagnostic scene.
The original source/tests remain under `../src/` and `../tests/`; portable production
source lives under `Packages/com.crowdsim.core/Runtime/` and is also built/tested
by the cloud .NET harness. The engine uses its own configured fixed step (0.15 s
default), independent of Unity FixedUpdate and render cadence.

Internet access to Unity's registry is needed for first import. M2 cloud validation
passes 404 original tests, 376 linked portable tests, 32 foundation/diagnostic tests
and exact six-case captures. **No Unity Editor is installed in cloud:** M2 import,
frontend compilation, Unity tests, graphics/input and standalone builds were not
executed. See [validation evidence](../docs/unity-migration/VALIDATION.md).
