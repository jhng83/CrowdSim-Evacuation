# Feature parity ledger

Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`. A reference test passing
does not establish Unity parity. M1–M3 were accepted by the user; M4 awaits local Unity acceptance.
"Core" means programmatic behavior; "UI" means the complete Unity workflow.

| ID | Existing capability / preservation requirement | Core / UI ownership | M4 status (cloud evidence; local acceptance pending) |
| --- | --- | --- | --- |
| F01 | Seeded population; default 0.15 s and smaller fixed steps; units; finite guards; acceleration and physical speed ceilings | M2 / M3 | Core regression/trace checks pass; M3 fixed-step transport, exact starts and bounds supplied |
| F02 | SFM, reaction delays, panic, social alarms, crowd awareness, heterogeneous traits | M2 / M3, M9 | Core parity retained; all behavior controls exposed; full analysis M9 |
| F03 | Exit choice/capacity; sight; routes; natural recovery; casualties/crawlers; doorway yield, gap and pressure modes | M2 / M9 | Core regressions + trace metrics pass; inspection UI later |
| F04 | Injured/crawling movement; bounded assistance slots, progress, pauses and abandonment | M2 / M8, M9 | Core regressions pass; humanoid/help display later |
| F05 | Hybrid opt-in; shared fields; groups up to five; individual contacts/exposure; detailed fallback | M2 / M9 | Both-mode core checks pass; Hybrid configurable before finalisation; runtime toggle/analysis M9 |
| F06 | Metric room sizing/locks; physical transparent perimeter; real exit openings; 1/3/5-person exits | M2 / M3, M8 | Metric room sizing and post-placement locks; 1/3/5-person exits and transparent physical perimeter implemented |
| F07 | Floor groups 1/5/15/50, exact starts, obstacle-aware distribution, occupancy counts | M2 / M3 | Exact 1/5/15/50-person placements, previews, counts, finalisation and obstacle-aware distribution implemented |
| F08 | Square block sizes, true circular obstacles, touching blocks and legacy blue walls | M2 / M3, M5 | All three obstacle choices retained in unified picker; legacy file UI remains M5 |
| F09 | Round table plus ten seats; single occupied chair; 3x1 m table; 15x6 m single stage; 1.5 m barricade | M2 / M4 | All five furniture kinds authored/previewed with exact dimensions, occupants and one-stage guard; editable example added |
| F10 | Per-asset remembered facing; quarter turns; free-angle barricades; exact angles; end snapping; R/Shift+R | M2 / M4 | Per-asset facing, quarter turns, free barricade angles, exact input, +/-15°, R/Shift+R and parallel end snapping implemented |
| F11 | Seated/stand-up/departure; loose-chair pushes and chains; furniture flight, impacts, support, landing/destruction; dynamic obstacles | M2 / M4 | Core regressions/traces retained; compound live furniture views, seated/rising poses and departure, airborne/fallen/destroyed status supplied; actual Unity acceptance pending |
| F12 | Preview validity; atomic placement; undo/Ctrl+Z; finalization guards and explanations | M2 / M3, M4 | Furniture candidate geometry/validity, atomic placement/whole-set undo, stage explanation and finalisation locking implemented |
| F13 | Fourteen behavior controls: traits, reactions, susceptibility, persistence, following, space, panic compression, urgency, running ceiling, helping, familiarity, assertiveness, pressure sensitivity/compression; resets | M2 / M3 | All 14 reference controls with bounds, numeric input and default reset implemented; locked after finalisation |
| F14 | Scenario entry; Build/Run, Camera, Files; fixed transport; start/pause/resume; 0.25x–4x playback | M3 | Scenario entry/workspace/Start/Pause/Resume/0.25x–4x implemented; Fume source-dependent run workflow M7 |
| F15 | Hide/show inspector/F4; retained tab, flags and selection; viewport expansion; text focus and Alt+F4 | M3 / M8, M9 | Hide/show/F4, tab/grid/selection retention and expanding viewport implemented; focus/Alt+F4 policy tested; later overlay/camera extensions M8/M9 |
| F16 | Save/Save As; sanitized names; listing/refresh; stable per-user storage; paused loading; dirty prompts; New Scenario; errors | M2 codec / M5 | Core file-store regressions/codec pass; user file workflow remains M5 |
| F17 | Schemas v1–v4; metric upgrades; enum identities; defaults; authored state only; .NET/Unity round trips | M2 codec / M5 | Portable codec/legacy regressions pass; actual Unity round trips remain M5 |
| F18 | Finalized frame-zero replay; initial starts; restored furniture; regenerated unreleased fume field | M2 / M5 | Cloud checkpoint/replay checks pass in all six cases; Unity replay test supplied |
| F19 | Single armed explosion; ordered editable radii; numerical edit/commit/cancel/reset/locking; shock, shielding, debris, dust/visibility | M2 / M6 | Core parity retained; basic one-shot arm/click event in M3; full radii/effects/overlays M6 |
| F20 | Persistent wall-clipped five-zone overlays and visibility toggles; no reapplication of damage | M6 | Planned |
| F21 | Fume source; radius, speed, extent, duration, drift, thresholds and exposure controls; precompute/invalidate; preview; release; permanent incapacity; local risk-aware navigation | M2 / M7 | Core regressions + exact 205-person traces/replay pass; full source/settings UI M7 |
| F22 | Fume plume/grid display, cache/readiness/compute statistics, exposure metrics and reset | M7 | Exposure metric/source marker only; plume/grid and full analysis remain M7 |
| F23 | Volumetric humans at all zooms; near/far detail; walking/running/seated/stand-up/crawl/prone/help poses; state colors, outlines, panic/alarm/help indicators | M8 | Basic volumetric seated/rising representation added for furniture workflow; walking remains capsules; complete humanoids/poses/detail remain M8 |
| F24 | Isometric rotation/elevation/zoom/reset; 0.45x–2.5x zoom; selection, Follow, head-height POV and exit cleanup | M3 basic / M8 complete | Basic azimuth/elevation/0.45x–2.5x zoom/reset implemented; runtime selection added; Follow/POV M8 |
| F25 | Overview, all agent/navigation/help/hazard details; per-exit density/flow; legends and model descriptions | M3, M6, M7 / M9 complete | Basic seating/rise and furniture motion/destruction counts added; complete analysis retains M6/M7/M9 |
| F26 | Desired/avoidance/herd/SFM/blast vectors; vision cones; congestion; trails; overlays survive hidden inspector | M9 | Planned |
| F27 | Frame/physics/draw/UI/record timing; allocations; visible/active and Hybrid counters; CPU benchmark comparisons | M2 harness / M9 | Core counters + seeded comparison harness available; full instrumentation/scale M9 |
| F28 | Runtime complete-UI 1280x720/20 fps MJPEG AVI; arm/start/stop/path; bounded queue/drops; replay/quit cleanup | M10 | Standard-asset encoder probe only; runtime recorder remains M10 |
| F29 | Startup/quit; examples; independent player; dependency notices; source downloads and final acceptance | M1 foundation / M11 complete | M1–M3 accepted; editable furnished example entry added; independent player and distribution remain M11 |

Future milestones must record cloud evidence and user acceptance against these IDs.
Only an explicitly approved difference can close a parity item without equivalent behavior.

M4 links all 404 original cases, including the remaining picker/angle helpers,
against portable production source with unchanged assertions. Eleven additional
presentation cases cover detached state/pose/geometry mapping. Seventy migration
cases exercise the foundation and command workflows. A cloud pass does not certify
native UI/input/graphics: the supplied Unity tests and local checklist still need
actual Editor execution. Later milestone ownership above remains unchanged.
