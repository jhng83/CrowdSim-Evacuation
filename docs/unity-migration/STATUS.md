# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`.

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | Delivered in `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1 evidence](M1_VALIDATION.md) | User reported successful testing; accepted |
| M2 — Portable simulation | Delivered in `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2 evidence](M2_VALIDATION.md) | User reported successful testing; accepted |
| M3 — Common builder/workspace | Delivered in `3e55f9c992ac8260cce714970b71631a1b3b601a` | [M3 evidence](M3_VALIDATION.md) | User reported successful testing; accepted |
| M4 — Furniture | Delivered in `bbc7a3ad64817ccd46d540c695a96831346af817` | [M4 evidence](M4_VALIDATION.md) | User reported successful testing; accepted |
| M5 — Files and replay | Delivered in `a0fdb72f067bb2116861dcaa78e580da0c904b98` | [M5 evidence](M5_VALIDATION.md) | User reported successful testing; accepted |
| M6 — Explosion | Delivered in `03137845c858c6d2fd9f85a1bfb73152f724a06b` | [M6 evidence](M6_VALIDATION.md) | User reported successful testing; accepted |
| M7 — Toxic Fume + requested UX/assets refinement | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M7 completes source placement/movement, all ten fume settings with native clamps,
explicit compute/recompute and readiness, preview scrubbing/reset, release, batched
concentration/grid views, field statistics and exposure/selected-agent metrics.
New/edited sources, geometry and fume settings require recomputation before
finalisation. Preview never moves people, changes authored data, consumes model RNG
or accumulates exposure. Saved unedited sources retain M5's automatic finalise/cache
shortcut; Compute exposes the full preview workflow. Replay rebuilds an unreleased
cache and restores starts/furniture/exposure at paused frame zero.

The user additionally requested more professional, intuitive UI/assets. Startup now
uses scenario cards with direct examples; the workspace adds selected navigation,
phase badges, clear action hierarchy, a source/compute/preview/release guide and a
concentration legend. Owned procedural surfaces add lit furniture/people, wood/tile/
barrier finishes, soft edges on chairs/tables/barricades and key/fill lighting.
Dimensions, static stage geometry, simulation physics and authoring guards are
preserved. These presentation changes apply to both scenarios. Complete humanoid
poses/detail/cameras remain M8; M8 has not started.

Cloud validation passes 404 original, 415 portable and 135 migration tests, unchanged
six-case traces/oracle on both engines and five native/portable file round trips.
Static checks cover 146 GUIDs and 106 unchanged reference files. Original source,
tests, fixtures, dependency pins, M1 goldens and portable engine algorithms remain
unchanged. Main and Editor/dependency pins remain unchanged.

Next action: [M7_LOCAL_TESTING.md](M7_LOCAL_TESTING.md). Actual M7 Unity import,
frontend compilation, 34 supplied EditMode and 12 PlayMode cases, native input,
layout, material/lighting/shader appearance and player builds were not executed in
cloud. GitHub Actions remote execution is unverified. Fix M7 issues before M8.
Desktop OS/player target confirmation remains pending before target-specific work.
