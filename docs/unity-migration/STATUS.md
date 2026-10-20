# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`.

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | Delivered in `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1 evidence](M1_VALIDATION.md) | User reported successful testing; accepted |
| M2 — Portable simulation | Delivered in `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2 evidence](M2_VALIDATION.md) | User reported successful testing; accepted |
| M3 — Common builder/workspace | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M4 — Furniture | Planned | Unrun | Unrun |
| M5 — Files and replay | Planned | Unrun | Unrun |
| M6 — Explosion | Planned | Unrun | Unrun |
| M7 — Toxic Fume | Planned | Unrun | Unrun |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M3 adds an entry-to-builder-to-run workflow in Bootstrap: metric room sizes/locks,
1/3/5-person exits, both square sizes, circles, exact 1/5/15/50-person groups,
candidate previews, undo, readiness reasons, all 14 behavior controls/reset,
Hybrid setup, finalisation, Start/Pause/Resume and 0.25x–4x scheduling. The workspace
has Build/Run, Camera and Files destinations, a fixed action footer, hideable
inspector tabs, selection, a persistent grid overlay and focus-aware shortcuts.
The basic Explosion action is armed during a run and triggers once on a floor click.

The common builder is available in both scenario entries. Toxic Fume finalisation
still requires the source/precompute workflow assigned to M7, with an explicit
readiness explanation. The accepted M2 diagnostics continue to run both hazards.
Furniture, file workflows, full hazard graphics, humanoids and analysis retain
their approved milestones; M4 has not started.

Cloud checks: 404 original cases, 388 linked portable cases (376 core plus 12
unchanged frontend layout/inspector cases), and 57 foundation/diagnostic/workspace
cases. Original and portable six-case captures still match unchanged goldens.
Actual Unity M3 import/compilation, 13 supplied EditMode and 3 PlayMode cases,
picking, native input, layout and graphics await local execution. Remote GitHub
Actions execution is unverified. The original reference files and main are unchanged.

Next action: follow [M3_LOCAL_TESTING.md](M3_LOCAL_TESTING.md). Fix any reported M3
problems before M4. Local OS and proposed Windows x64/Mono player confirmation
remain pending before target-specific work; no standalone build is delivered here.
