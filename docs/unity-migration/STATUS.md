# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`.

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | Implemented; see delivery commit | See VALIDATION.md | Awaiting user testing |
| M2 — Portable simulation | Planned | Unrun | Unrun |
| M3 — Common builder/workspace | Planned | Unrun | Unrun |
| M4 — Furniture | Planned | Unrun | Unrun |
| M5 — Files and replay | Planned | Unrun | Unrun |
| M6 — Explosion | Planned | Unrun | Unrun |
| M7 — Toxic Fume | Planned | Unrun | Unrun |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M1 delivers a pinned Unity project, startup scene/interface, portable assembly
shell, two bundled reference examples, deterministic reference capture, a recording
dependency probe, local Unity tests, and a GitHub cloud-validation workflow.

No simulation port or parity claim is made in M1. GitHub workflow execution is
distinct from the locally executed cloud commands. Editor import, Unity tests,
native input, rendering and standalone builds are unverified until executed.

Next action: user follows [M1_LOCAL_TESTING.md](M1_LOCAL_TESTING.md) and reports
results. Fix M1 problems before M2; do not advance merely because cloud tests pass.
Local OS and standalone target confirmation remain pending. The project itself
contains no Windows-only startup code; the proposed final player target is Windows x64.
