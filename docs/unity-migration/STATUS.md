# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`.

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | Delivered in `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1 evidence](M1_VALIDATION.md) | User reported successful testing; accepted |
| M2 — Portable simulation | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M3 — Common builder/workspace | Planned | Unrun | Unrun |
| M4 — Furniture | Planned | Unrun | Unrun |
| M5 — Files and replay | Planned | Unrun | Unrun |
| M6 — Explosion | Planned | Unrun | Unrun |
| M7 — Toxic Fume | Planned | Unrun | Unrun |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M2 ports all 41 original core source files to the maintained C# 9 / .NET Standard
2.1 package. It adds runtime compatibility utilities, a strict scenario codec,
immutable presentation frames, a fixed-step scheduler, and a diagnostic scene
with three presets, both Hybrid modes, step/play/pause/reset and checkpoint results.
The original application, tests, scenarios, dependency pins and M1 goldens remain
unchanged. No later milestone has been implemented.

Cloud results: 404 original regressions, 376 linked portable regressions and 32
foundation/diagnostic tests pass. Both original and portable captures match the
unchanged six-case traces and compatibility oracle exactly in the pinned cloud
runtime. These results do not establish Mono/Editor numeric or graphical parity.

Next action: follow [M2_LOCAL_TESTING.md](M2_LOCAL_TESTING.md) in Unity 6000.3.26f1.
M2 remains pending acceptance until its actual Unity runtime checks pass. Fix
reported M2 problems before starting M3. Remote GitHub Actions execution is
unverified; the cloud worker has no Unity Editor. Local OS and proposed Windows
x64/Mono player target confirmation remain pending before target-specific work.
