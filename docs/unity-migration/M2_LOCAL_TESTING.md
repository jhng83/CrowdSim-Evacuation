# M2 local Unity acceptance

Use Unity **6000.3.26f1** with the committed URP **17.3.0** packages. No .NET SDK or
original application process is needed. Work in a fresh extraction of the delivered
commit ZIP so results identify the exact revision; Codex does not access your files.

1. Download/extract the M2 commit ZIP. In Unity Hub add its **UnityProject/** folder,
   open with 6000.3.26f1 and allow package import/compilation. Expect no Console errors.
   M2 adds a Newtonsoft codec dependency to the core asmdef; first import of these
   new assets has not run in cloud. Report import/compile errors before proceeding.
2. Open **Assets/CrowdSim/Scenes/Validation/SimulationParity.unity** and enter Play
   Mode. A camera and world are created at runtime. Expect a left control panel,
   room floor, exits/obstacles and raised 3D capsules. Open Explosion is loaded,
   Hybrid off, tick **0/120**, population **50**, scheduling paused. The model
   must stay at zero while you wait. Physical perimeter walls are not drawn.
3. Click **Step one tick** once: tick **1**, then no further advancement while
   paused. Step to tick **10**: hazard still waiting. Step once more to **11**:
   hazard triggered. The event is applied after observation 10, before step 11.
   Single-step must never jump multiple ticks.
4. Click **Load / reset selected**: exact initial placement, tick zero and waiting
   hazard return. Click **Start 1x**, wait briefly, then **Pause** (or **Pause 1x**).
   Expect fixed 0.15-second ticks and movement; paused tick must remain unchanged
   for several seconds. Resume and pause again. Scheduling may run slower under
   load; dt and physical behavior must not depend on render FPS.
5. Click **Run full 120-tick check**. It resets the selected case and runs in
   bounded batches; this is faster than 1x. Expect **tick 120 / 18.00 s**, automatic
   pause and **PASS · 7/7 checkpoints / Replay restored frame zero.** Replay is an
   independently reconstructed model check; the displayed world remains at tick120.
   The Step/Start buttons stay disabled at the end; Load/reset restores tick zero.
6. Repeat the full check for each preset with **Hybrid off and on** (six cases).
   Change mode/preset while paused, then click Load/reset or Run full check.
   The selected case name updates when loaded. Expected starting populations and
   tick120 aggregate results are below. Every case must report PASS and replay.
7. In Furnished Explosion, inspect round/rectangular tables, occupied chairs,
   stage, angled barricade and square/circular obstacles. After the event, compare
   changed furniture/debris and state colors. In Toxic Fume, expect 205 capsules
   and a source marker; concentration/exposure calculations run, but plume/grid
   visualization is assigned to M7. No chemical CFD model is introduced.
8. Resize the Game view (for example 1280x720 and 1600x900). Controls should remain
   accessible by scrolling, and the room should remain framed beside the panel.
   Click **Return to startup**, then **Open M2 parity** to return. Stop/re-enter
   Play Mode and reset/reload cases; expect clean startup, no stale views or errors.
9. Open **Window > General > Test Runner**. Run all **EditMode** tests: expect
   **9 passing cases**, including six actual host-runtime checkpoint/replay cases.
   Run all **PlayMode** tests: expect **2 passing cases**. The new scene test steps,
   waits while paused, completes the check, resets and returns to startup. Depending
   on the Test Runner UI, parameterized cases may be grouped under their method.
10. Report import/Console results, six-case PASS/failure results, transport/resize
    observations and actual EditMode/PlayMode totals. On mismatch include preset,
    Hybrid mode, tick and the displayed failure paths/values; attach logs through
    your usual workflow. A failed Unity comparison blocks the next milestone.

| Preset | Initial population | Tick120 population / exited | Tick120 injured / dead | Other state detail |
| --- | --- | --- | --- | --- |
| Open Explosion | 50 | 50 / 2 | 0 / 0 | 48 evacuating |
| Furnished Explosion | 61 (11 initially seated) | 61 / 2 | 1 / 9 | 48 evacuating, 1 temporarily incapacitated |
| Toxic Fume | 205 | 205 / 0 | 0 / 0 | 205 evacuating within this bounded 18-second run |

These aggregate counts are the same for both modes; detailed motion, counters and
exposure are checked against each mode's own baseline. Population includes exited
agents; their runtime views may disappear. The panel's injured metric follows the
reference definition and does not include every incapacitated state.

## Scope and known limits

This scene is a diagnostic capability, not the complete simulator workspace. It
uses capsules/blocks rather than M8 humanoids/poses, a fixed fitted camera rather
than full camera controls, and has no builder, user file workflow, full hazard
editing/overlays, analysis panels or recording. Those features retain their approved
milestones. The scene is deliberately bounded to 120 ticks and six reproducible
cases; it is not a crowd-scale performance benchmark. Core regression tests cover
behavior beyond those observations.

Floats compare within absolute 1e-4 plus relative 1e-5; discrete states/counts remain
exact. Readable checkpoints sample six agents plus full furniture/debris/metrics;
cloud trace hashing observes all agents. See [COMPATIBILITY.md](COMPATIBILITY.md).
No Unity import, Editor compilation/tests, graphics or standalone build was executed
in cloud. File-based StreamingAssets access targets desktop Editor/player workflows;
WebGL/mobile and IL2CPP are not verified or added as targets.
