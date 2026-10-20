# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`.

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | Delivered in `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1 evidence](M1_VALIDATION.md) | User reported successful testing; accepted |
| M2 — Portable simulation | Delivered in `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2 evidence](M2_VALIDATION.md) | User reported successful testing; accepted |
| M3 — Common builder/workspace | Delivered in `3e55f9c992ac8260cce714970b71631a1b3b601a` | [M3 evidence](M3_VALIDATION.md) | User reported successful testing; accepted |
| M4 — Furniture | Delivered in `bbc7a3ad64817ccd46d540c695a96831346af817` | [M4 evidence](M4_VALIDATION.md) | User reported successful testing; accepted |
| M5 — Files and replay | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M6 — Explosion | Planned | Unrun | Unrun |
| M7 — Toxic Fume | Planned | Unrun | Unrun |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M5 adds named Save/Save As, normalized target paths, list/refresh, read-only imports,
paused transactional loading, dirty Save/Discard/Keep decisions for load/new/leave,
and overwrite confirmation. Writes use `Application.persistentDataPath/scenarios`;
bundled examples and legacy native save directories are read-only sources. Failures
retain the current session and unsaved changes. Loaded files open in the builder;
finalisation validates/spawns and captures an independent starting checkpoint.

Runtime saving uses that authored checkpoint. Replay reconstructs before replacing
the model, restores exact starts, intact furniture and frame zero, and regenerates
an unreleased Fume cache. Legacy seeded random starts remain repeatable. Bundled
examples now exercise schemas 1–4. Saved Fume scenes can finalise/precompute and use
**Release saved Fume**, making 205-person replay locally testable. Full source,
settings and preview authoring remains M7; M6 has not started.

Cloud validation passes 404 original, 415 portable and 91 migration tests, unchanged
six-case traces/oracle on both engines, and five native/portable file round trips.
Original source/tests/examples/pins, M1 goldens and portable engine algorithms are
unchanged. Main remains unchanged. Exact Editor/dependency pins remain unchanged.

Next action: [M5_LOCAL_TESTING.md](M5_LOCAL_TESTING.md). Actual M5 Unity import,
frontend compilation, 25 supplied EditMode and 7 PlayMode cases, native input,
layout/rendering and player builds were not executed in cloud. GitHub Actions
remote execution is unverified. Fix any M5 issue before M6. Desktop OS/player target
confirmation remains pending before target-specific work.
