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
| M6 — Explosion | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M7 — Toxic Fume | Planned | Unrun | Unrun |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M6 adds the full Explosion inspector: five ordered metric radii with caret text,
Enter/blur commit, Escape cancel, 0.10 m increments, validation and reset. Radii
remain editable until detonation, including during a run; pre-event runtime edits
rebase the authored replay checkpoint without capturing moved people/furniture.
A running scene may arm one Explosion, cancel it, or click clear floor once.
Blocked/outside/nonfinite points are rejected; pausing cancels arming.

Detached presentation now includes cached persistent wall-clipped zone bands and
outlines, a timed shock front, a sampled dust/visibility field, reference-shaped
moving debris and Explosion metrics/selected-agent details. Master/per-zone and
shock/dust visibility remain adjustable after detonation and survive inspector
hiding. Visibility never reapplies damage. Replay removes the event/effects, restores
starts/furniture, preserves pre-event radii and unlocks editing. Full Fume authoring
remains M7; M7 has not started.

Cloud validation passes 404 original, 415 portable and 112 migration tests, unchanged
six-case traces/oracle on both engines, and five native/portable file round trips.
Static checks cover 136 GUIDs and 106 unchanged reference files. Original source,
tests, fixtures, dependency pins, M1 goldens and portable engine algorithms are
unchanged. Main and the approved Editor/dependency versions remain unchanged.

Next action: [M6_LOCAL_TESTING.md](M6_LOCAL_TESTING.md). Actual M6 Unity import,
frontend compilation, 29 supplied EditMode and 9 PlayMode cases, native input,
layout/rendering and player builds were not executed in cloud. GitHub Actions
remote execution is unverified. Fix any M6 issue before M7. Desktop OS/player target
confirmation remains pending before target-specific work.
