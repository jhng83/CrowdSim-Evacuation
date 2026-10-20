# Migration status

Approved sequence: [ROADMAP.md](ROADMAP.md). Branch: `unity-migration`.
Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`.

| Milestone | Implementation | Cloud checks | Local Unity acceptance |
| --- | --- | --- | --- |
| M1 — Unity foundation | Delivered in `1ffc64d7239e4e9ec1af52d159e0a6887121b72f` | [M1 evidence](M1_VALIDATION.md) | User reported successful testing; accepted |
| M2 — Portable simulation | Delivered in `eefbf2c993c248c96b153c30874d5e4ce77fcc9a` | [M2 evidence](M2_VALIDATION.md) | User reported successful testing; accepted |
| M3 — Common builder/workspace | Delivered in `3e55f9c992ac8260cce714970b71631a1b3b601a` | [M3 evidence](M3_VALIDATION.md) | User reported successful testing; accepted |
| M4 — Furniture | Implemented | [Current evidence](VALIDATION.md) | Awaiting user testing |
| M5 — Files and replay | Planned | Unrun | Unrun |
| M6 — Explosion | Planned | Unrun | Unrun |
| M7 — Toxic Fume | Planned | Unrun | Unrun |
| M8 — Humanoids/cameras | Planned | Unrun | Unrun |
| M9 — Diagnostics/scale | Planned | Unrun | Unrun |
| M10 — Recording | Planned | Unrun | Unrun |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M4 completes furniture authoring through a single eight-option asset picker. All
five furniture kinds use the accepted engine's footprints, seat counts, placement,
undo and finalisation guards. Per-asset facing, table/stage quarter turns, free
barricade angles, exact input, +/-15° and R/Shift+R, and parallel end snapping are
available. Stage placement is limited to one and recovers after undo.

Detached compound views now show chair seats/backs/legs, table tops/supports,
fixed stage/barricade geometry, basic seated/rising people and live furniture
positions/heights/headings/states. Destroyed furniture loses its views while its
status remains observable. Core pushing/chains, flight/impacts/support/landing,
seating/departure and navigation behavior are unchanged. Full humanoids/animation
remain M8. Bootstrap also offers an editable furnished example: 26 people,
including 11 seated occupants, five furniture sets and all three obstacle choices.

Cloud validation: 404 original tests, 415 portable tests (all 404 unchanged original
cases plus 11 new presentation cases), and 70 migration tests (57 existing plus 13
furniture workspace cases) pass. Both original and portable six-case captures
match unchanged goldens. The original source/tests/examples/pins and portable
engine algorithms are unchanged. Main remains unchanged.

Next action: follow [M4_LOCAL_TESTING.md](M4_LOCAL_TESTING.md). Actual M4 Unity import,
frontend compilation, 17 supplied EditMode and 5 PlayMode cases, native input,
picking, rendering and player builds were not executed in cloud. GitHub Actions
remote execution is unverified. Fix any M4 issue before M5; files/replay have not
started. Toxic Fume source/precompute authoring remains M7, while accepted M2
Fume diagnostics still run. Desktop OS/player target confirmation remains pending
before target-specific work.
