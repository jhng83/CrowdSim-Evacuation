# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24` (main unchanged).

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1 evidence](M1_VALIDATION.md) | Accepted by user |
| M2 — Portable simulation | `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2 evidence](M2_VALIDATION.md) | Accepted by user |
| M3 — Common builder/workspace | `3e55f9c992ac8260cce714970b71631a1b3b601a` | [M3 evidence](M3_VALIDATION.md) | Accepted by user |
| M4 — Furniture | `bbc7a3ad64817ccd46d540c695a96831346af817` | [M4 evidence](M4_VALIDATION.md) | Accepted by user |
| M5 — Files and replay | `a0fdb72f067bb2116861dcaa78e580da0c904b98` | [M5 evidence](M5_VALIDATION.md) | Accepted by user |
| M6 — Explosion | `03137845c858c6d2fd9f85a1bfb73152f724a06b` | [M6 evidence](M6_VALIDATION.md) | Accepted by user |
| M7 — Toxic Fume + UX/assets | `4fc21756b1d2b97e28a3d5b5236b9276df4da47f` | [M7 evidence](M7_VALIDATION.md) | User reported successful testing; accepted |
| M8 — Humanoids/cameras | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M8 supplies pooled block humanoids with volumetric near/far detail, walking/running,
seated/rising, crawling/prone and timed kneeling assistance poses; state colours,
dark silhouettes, panic/selection rings and labelled alarm/help/selection indicators.
Picking intersects detached rendered body-part volumes, including low poses.
Isometric controls gain wheel zoom; Follow centres the selected person; head-height
POV follows movement heading and hides its own body. Exit POV returns to Follow.
Clear selection, agent exit, replay, New and load clean camera/selection state.
Inspector hiding retains selection, mode and overlay flags; only explicit reveal
opens it. A separate deterministic pose-reference scene uses the production renderer.

The user's ongoing colour/font requirement is recorded in UnityProject/AGENTS.md
and [DESIGN.md](DESIGN.md). M8 bundles the OFL Atkinson Hyperlegible font, increases
small UI text, adds focus boundaries and checks key solid text/background pairs
against 4.5:1, including primary-button hover/focus. Legends use normal text plus
swatches; selected state/pose names and symbol/text indicators supplement colours.
No simulation algorithm, original source/test/fixture/pin, M1 golden, Editor or
Unity dependency version changed. Portable package version is 0.8.0.

Cloud passes: 404 original, 415 portable and 159 migration tests; exact six-case
traces/oracle on both engines; five schema interop cases; 161 GUIDs and 106 unchanged
reference files. M8 Unity import, frontend compilation, 41 supplied EditMode and
15 PlayMode cases, rendering, native input and player builds were not executed:
no Editor is installed. Remote CI execution is unverified.

Next action: [M8_LOCAL_TESTING.md](M8_LOCAL_TESTING.md). Stop for user results and
fix M8 issues before M9. OS/player target confirmation remains pending before
platform-specific work; no such decision is needed for this Editor milestone.
