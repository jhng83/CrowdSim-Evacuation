# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24` (main unchanged).

| Milestone | Implementation | Cloud evidence | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Foundation | `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1](M1_VALIDATION.md) | Accepted by user |
| M2 — Portable engine | `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2](M2_VALIDATION.md) | Accepted by user |
| M3 — Builder/workspace | `3e55f9c992ac8260cce714970b71631a1b3b601a` | [M3](M3_VALIDATION.md) | Accepted by user |
| M4 — Furniture | `bbc7a3ad64817ccd46d540c695a96831346af817` | [M4](M4_VALIDATION.md) | Accepted by user |
| M5 — Files/replay | `a0fdb72f067bb2116861dcaa78e580da0c904b98` | [M5](M5_VALIDATION.md) | Accepted by user |
| M6 — Explosion | `03137845c858c6d2fd9f85a1bfb73152f724a06b` | [M6](M6_VALIDATION.md) | Accepted by user |
| M7 — Toxic Fume + design | `4fc21756b1d2b97e28a3d5b5236b9276df4da47f` | [M7](M7_VALIDATION.md) | Accepted by user |
| M8 — Humanoids/cameras | `55652fc407d3eb96527f836e57067da42dc41459` | [M8](M8_VALIDATION.md) | User reported successful testing; accepted |
| M9 — Diagnostics/scale + requested light architecture theme | Implemented | [Current validation](VALIDATION.md), [CPU comparisons](M9_BENCHMARK.md) | Awaiting user testing |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M9 completes native population/overview, selected-agent/navigation/assistance/model
and per-exit metrics, all eight direction/force/vision/congestion/trail layers,
retained overlays with hidden inspector, paused Hybrid switching with authored
checkpoint rebasing, distinct performance/allocation readouts and deterministic
220/500/1000-person comparison fixtures for both hazards/open/furnished rooms.
Cloud CPU comparisons cover 24 combinations. View optimisations add frustum culling,
pose/material/part caches, retained pooled bodies and unchanged-ring reuse without
changing simulation order or replacing distant people with sprites.

The additional user request changes text to black on pale surfaces throughout
startup, workspace, hazard/agent legends, world labels, pose reference and legacy
validation panels. Square/circular blockages render as off-white architectural
pillars with black feature edges and silhouettes; blue internal walls retain their
identity. Floor is warm off-white with a subtle mineral finish and default-on 1.2 m
grid. Lighting is restrained; physical dimensions/colliders/navigation are unchanged.
Black selection rings keep contrast against the new floor. See [DESIGN.md](DESIGN.md).

Cloud passes 404 original, 415 portable and 191 migration cases, both exact six-case
captures/oracle and five schema interop cases. Static checks cover 173 GUIDs, black
text/contrast/transparent viewport, packages and 106 unchanged reference files.
Original source/tests/scenarios/pins, portable Engine algorithms and golden traces
are unchanged. Portable package is 0.9.0; Editor/UPM pins remain unchanged.

No Unity Editor is installed: M9 Unity import/frontend compilation, 49 supplied
EditMode and 19 PlayMode cases, actual shader/font/layout/input checks and graphical
scale/player/GPU checks were not executed. Remote CI execution is unverified.

Next: [M9_LOCAL_TESTING.md](M9_LOCAL_TESTING.md). Stop for user results; fix M9
problems before M10. Recording is M10; final standalone acceptance M11. OS/player
target confirmation remains pending before platform-specific work.
