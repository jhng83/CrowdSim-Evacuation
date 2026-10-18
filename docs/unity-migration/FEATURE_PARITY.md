# Feature parity ledger

Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`. A reference test passing
does not establish Unity parity. At M1 every simulation feature remains planned.
"Core" means programmatic behavior; "UI" means the complete Unity workflow.

| ID | Existing capability / preservation requirement | Core / UI ownership | M1 status |
| --- | --- | --- | --- |
| F01 | Seeded population; default 0.15 s and smaller fixed steps; units; finite guards; acceleration and physical speed ceilings | M2 / M3 | Reference traces captured |
| F02 | SFM, reaction delays, panic, social alarms, crowd awareness, heterogeneous traits | M2 / M3, M9 | Reference traces captured |
| F03 | Exit choice/capacity; sight; routes; natural recovery; casualties/crawlers; doorway yield, gap and pressure modes | M2 / M9 | Planned |
| F04 | Injured/crawling movement; bounded assistance slots, progress, pauses and abandonment | M2 / M8, M9 | Planned |
| F05 | Hybrid opt-in; shared fields; groups up to five; individual contacts/exposure; detailed fallback | M2 / M9 | Both modes captured |
| F06 | Metric room sizing/locks; physical transparent perimeter; real exit openings; 1/3/5-person exits | M2 / M3, M8 | Planned |
| F07 | Floor groups 1/5/15/50, exact starts, obstacle-aware distribution, occupancy counts | M2 / M3 | Planned |
| F08 | Square block sizes, true circular obstacles, touching blocks and legacy blue walls | M2 / M3, M5 | Legacy fixture bundled |
| F09 | Round table plus ten seats; single occupied chair; 3x1 m table; 15x6 m single stage; 1.5 m barricade | M2 / M4 | Furnished reference captured |
| F10 | Per-asset remembered facing; quarter turns; free-angle barricades; exact angles; end snapping; R/Shift+R | M2 / M4 | Planned |
| F11 | Seated/stand-up/departure; loose-chair pushes and chains; furniture flight, impacts, support, landing/destruction; dynamic obstacles | M2 / M4 | Reference traces captured |
| F12 | Preview validity; atomic placement; undo/Ctrl+Z; finalization guards and explanations | M2 / M3, M4 | Planned |
| F13 | Fourteen behavior controls: traits, reactions, susceptibility, persistence, following, space, panic compression, urgency, running ceiling, helping, familiarity, assertiveness, pressure sensitivity/compression; resets | M2 / M3 | Planned |
| F14 | Scenario entry; Build/Run, Camera, Files; fixed transport; start/pause/resume; 0.25x–4x playback | M3 | M1 has roadmap overviews only |
| F15 | Hide/show inspector/F4; retained tab, flags and selection; viewport expansion; text focus and Alt+F4 | M3 / M8, M9 | Planned |
| F16 | Save/Save As; sanitized names; listing/refresh; stable per-user storage; paused loading; dirty prompts; New Scenario; errors | M2 codec / M5 | Planned |
| F17 | Schemas v1–v4; metric upgrades; enum identities; defaults; authored state only; .NET/Unity round trips | M2 codec / M5 | v2/v4 examples bundled unchanged |
| F18 | Finalized frame-zero replay; initial starts; restored furniture; regenerated unreleased fume field | M2 / M5 | Reference replay assertions captured |
| F19 | Single armed explosion; ordered editable radii; numerical edit/commit/cancel/reset/locking; shock, shielding, debris, dust/visibility | M2 / M6 | Explosion reference traces captured |
| F20 | Persistent wall-clipped five-zone overlays and visibility toggles; no reapplication of damage | M6 | Planned |
| F21 | Fume source; radius, speed, extent, duration, drift, thresholds and exposure controls; precompute/invalidate; preview; release; permanent incapacity; local risk-aware navigation | M2 / M7 | 205-person traces captured |
| F22 | Fume plume/grid display, cache/readiness/compute statistics, exposure metrics and reset | M7 | Planned |
| F23 | Volumetric humans at all zooms; near/far detail; walking/running/seated/stand-up/crawl/prone/help poses; state colors, outlines, panic/alarm/help indicators | M8 | Planned |
| F24 | Isometric rotation/elevation/zoom/reset; 0.45x–2.5x zoom; selection, Follow, head-height POV and exit cleanup | M3 basic / M8 complete | Planned |
| F25 | Overview, all agent/navigation/help/hazard details; per-exit density/flow; legends and model descriptions | M3, M6, M7 / M9 complete | Planned |
| F26 | Desired/avoidance/herd/SFM/blast vectors; vision cones; congestion; trails; overlays survive hidden inspector | M9 | Planned |
| F27 | Frame/physics/draw/UI/record timing; allocations; visible/active and Hybrid counters; CPU benchmark comparisons | M2 harness / M9 | Harness foundation only |
| F28 | Runtime complete-UI 1280x720/20 fps MJPEG AVI; arm/start/stop/path; bounded queue/drops; replay/quit cleanup | M10 | Standard-asset encoder probe only |
| F29 | Startup/quit; examples; independent player; dependency notices; source downloads and final acceptance | M1 foundation / M11 complete | Startup implemented; local acceptance pending |

Future milestones must record cloud evidence and user acceptance against these IDs.
Only an explicitly approved difference can close a parity item without equivalent behavior.
