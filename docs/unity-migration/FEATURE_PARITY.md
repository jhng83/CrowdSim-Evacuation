# Feature parity ledger

Reference: `01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`. A reference test passing
does not establish Unity parity. M1–M9 were accepted by the user; M10 awaits local Unity acceptance.
"Core" means programmatic behavior; "UI" means the complete Unity workflow.

Latest local import was blocked by recording CS0234 namespace shadowing plus failed
package authentication/missing package files. This fix qualifies the NativeArray
copy as `global::Unity.Collections`; a reduced compiler probe reproduces the failure
and passes with the corrected statement. Full Unity compilation, recording and GPU
acceptance remain pending after [package recovery](M10_IMPORT_RECOVERY.md). No feature
is marked accepted by this source correction; M11 remains pending.

| ID | Existing capability / preservation requirement | Core / UI ownership | Current status (M10 + performance fix; local acceptance pending) |
| --- | --- | --- | --- |
| F01 | Seeded population; default 0.15 s and smaller fixed steps; units; finite guards; acceleration and physical speed ceilings | M2 / M3 | Core regressions/unchanged traces pass; exact CPU sight/force/contact optimizations retain dt/RNG/order/guards, with 24 paired dense/open state hashes; transport/starts/bounds retained |
| F02 | SFM, reaction delays, panic, social alarms, crowd awareness, heterogeneous traits | M2 / M3, M9 | Core parity retained; all behaviour controls exposed; M9 population/stimulus/intent/alarm/model diagnostics supplied; M9 accepted |
| F03 | Exit choice/capacity; sight; routes; natural recovery; casualties/crawlers; doorway yield, gap and pressure modes | M2 / M9 | Core regressions/traces retained; M9 selected route/waypoint/recovery/stuck/exit-reconsideration/local block/bypass/bottleneck diagnostics supplied; native per-exit density/flow exposed |
| F04 | Injured/crawling movement; bounded assistance slots, progress, pauses and abandonment | M2 / M8, M9 | M8 poses/help indicators accepted; M9 approach/attendance/target/side/recovery/progress/timeout/helper-count and population assistance metrics supplied |
| F05 | Hybrid opt-in; shared fields; groups up to five; individual contacts/exposure; detailed fallback | M2 / M9 | Both-mode core regressions retained; M9 paused runtime toggle, authored checkpoint rebasing, native open/detailed/group/field counters and seeded comparisons supplied; Hybrid off retains navigation |
| F06 | Metric room sizing/locks; physical transparent perimeter; real exit openings; 1/3/5-person exits | M2 / M3, M8 | Metric room sizing and post-placement locks; 1/3/5-person exits and transparent physical perimeter implemented |
| F07 | Floor groups 1/5/15/50, exact starts, obstacle-aware distribution, occupancy counts | M2 / M3 | Exact 1/5/15/50-person placements, previews, counts, finalisation and obstacle-aware distribution implemented |
| F08 | Square block sizes, true circular obstacles, touching blocks and legacy blue walls | M2 / M3, M5 | All obstacle/legacy file choices retained; M9 square/circular assets become off-white black-outlined architectural pillars with exact physical dimensions; blue internal walls stay distinct |
| F09 | Round table plus ten seats; single occupied chair; 3x1 m table; 15x6 m single stage; 1.5 m barricade | M2 / M4, M10 | All five kinds/occupants/one-stage guard retained; M10 user-requested stage sizing (next/placed, builder-only), rotated room/perimeter/overlap checks, exact preview/collision/save/replay and concert rig supplied; default 15×6 m preserved |
| F10 | Per-asset remembered facing; quarter turns; free-angle barricades; exact angles; end snapping; R/Shift+R | M2 / M4 | Per-asset facing, quarter turns, free barricade angles, exact input, +/-15°, R/Shift+R and parallel end snapping implemented |
| F11 | Seated/stand-up/departure; loose-chair pushes and chains; furniture flight, impacts, support, landing/destruction; dynamic obstacles | M2 / M4 | Core regressions/traces retained; compound live furniture views, seated/rising poses and departure, airborne/fallen/destroyed status supplied; M4 accepted by user |
| F12 | Preview validity; atomic placement; undo/Ctrl+Z; finalization guards and explanations | M2 / M3, M4 | Furniture candidate geometry/validity, atomic placement/whole-set undo, stage explanation and finalisation locking implemented |
| F13 | Fourteen behavior controls: traits, reactions, susceptibility, persistence, following, space, panic compression, urgency, running ceiling, helping, familiarity, assertiveness, pressure sensitivity/compression; resets | M2 / M3 | All 14 reference controls with bounds, numeric input and default reset implemented; locked after finalisation |
| F14 | Scenario entry; Build/Run, Camera, Files; fixed transport; start/pause/resume; 0.25x–4x playback | M3 | Scenario entry/workspace/Start/Pause/Resume/0.25x–4x implemented; complete Fume source/compute/preview/release workflow supplied in M7 |
| F15 | Hide/show inspector/F4; retained tab, flags and selection; viewport expansion; text focus and Alt+F4 | M3 / M8, M9 | M3/M8 accepted; M9 all diagnostic flags/layers/trail progression remain active with inspector hidden, selection does not force reveal; focus/Alt+F4 policy retained; M9 accepted; M10 recording input remains local |
| F16 | Save/Save As; sanitized names; listing/refresh; stable per-user storage; paused loading; dirty prompts; New Scenario; errors | M2 codec / M5 | Save/Save As, portable names, persistent directory, list/refresh/import, paused transactional load, dirty/overwrite/new/leave prompts and failure retention implemented; M5 accepted by user |
| F17 | Schemas v1–v4; metric upgrades; enum identities; defaults; authored state only; .NET/Unity round trips | M2 codec / M5 | Five actual native/portable codec/store round trips pass in cloud across v1–v4; M5 accepted by user, actual Editor test logs not supplied; M10 adds explicit custom-stage v5 with Unity round-trip and native rejection, default/ordinary exports remain v4 |
| F18 | Finalized frame-zero replay; initial starts; restored furniture; regenerated unreleased fume field | M2 / M5 | Workspace replay restores paused frame zero/starts/intact furniture/unreleased regenerated cache, including Fume205 in both modes; runtime exports exclude damage; M5 accepted; M6 pre-event radius edits retain authored replay; M10 custom-stage sizes preserved and recording finalized before replay |
| F19 | Single armed explosion; ordered editable radii; numerical edit/commit/cancel/reset/locking; shock, shielding, debris, dust/visibility | M2 / M6 | Complete radius text/commit/cancel/increment/reset, pre-event edits, post-event locks, clear-floor one-shot arming/cancel, timed front, energy/heading debris, dust/visibility and event/agent analysis supplied; M6/M7 accepted by user; M8 accepted |
| F20 | Persistent wall-clipped five-zone overlays and visibility toggles; no reapplication of damage | M6 | Cached five colored bands/outlines, master/per-zone visibility, hidden-inspector persistence and replay cleanup supplied; both-mode noninterference tests pass; M6/M7 accepted; shared mesh renderer retained; M8 accepted by user; no Editor logs supplied |
| F21 | Fume source; radius, speed, extent, duration, drift, thresholds and exposure controls; precompute/invalidate; preview; release; permanent incapacity; local risk-aware navigation | M2 / M7 | Source/move, ten settings/dependencies/reset, cache compute/invalidation/readiness, isolated timeline preview, release and replay implemented; original exposure/permanent incapacity/local risk navigation retained; M7 accepted by user |
| F22 | Fume plume/grid display, cache/readiness/compute statistics, exposure metrics and reset | M7 | Batched full-resolution concentration/grid, source radius, normalized legend, cache/grid/arrival/size/compute metrics, release time, exposure counts/mean/peak and selected-agent concentration/status/mobility supplied |
| F23 | Volumetric humans at all zooms; near/far detail; walking/running/seated/stand-up/crawl/prone/help poses; state colors, outlines, panic/alarm/help indicators | M8 | M8/M9 accepted; M10 fix adds default GPU-instanced volumetric bodies/outlines/rings, bounded matrix blocks, state shades, CPU frustum/POV hiding and compatibility toggle; same poses/labels/picking, actual Unity/GPU visual acceptance pending |
| F24 | Isometric rotation/elevation/zoom/reset; 0.45x–2.5x zoom; selection, Follow, head-height POV and exit cleanup | M3 basic / M8 complete | M8 accepted; rotation/elevation/zoom/reset, body picking, Follow/POV and exit/clear/replay/new/load cleanup retained; black selection ring/pale label plate retained; M10 fix uses matrix-based volume picking for instanced and compatibility bodies, with Follow/POV/cleanup retained |
| F25 | Overview, all agent/navigation/help/hazard details; per-exit density/flow; legends and model descriptions | M3, M6, M7 / M9 complete | Complete native overview/selected navigation/help/model/per-exit metrics supplied alongside M6/M7 hazard analysis; native mobile/exposure denominators and m/s conversions retained; M9 accepted |
| F26 | Desired/avoidance/herd/SFM/blast vectors; vision cones; congestion; trails; overlays survive hidden inspector | M9 | All eight batched desired/avoidance/herd/SFM/blast/vision/congestion/trail layers supplied, including three-tick/40-point history, POV self exclusion, hidden-inspector persistence and replay/new/load cleanup; M9 accepted |
| F27 | Frame/physics/draw/UI/record timing; allocations; visible/active and Hybrid counters; CPU benchmark comparisons | M2 harness / M9 | Distinct wall FPS/frame p50/p95, physics commands/native last step/furniture/routes, world/UI commands, main-thread allocations and visible/active/Hybrid counters supplied; 24 CPU comparison cases executed; actual Unity/GPU scale testing unrun; M10 capture/worker counters retained; fix adds exclusive engine phases, nested SFM/collision/route, actual backend/instanced submissions and valid dense-800 fixture; 24 paired CPU hashes with 1.6–1.9× dense cloud improvement; Unity/GPU/FPS unrun |
| F28 | Runtime complete-UI 1280x720/20 fps MJPEG AVI; arm/start/stop/path; bounded queue/drops; replay/quit cleanup | M10 | Implemented: runtime full-window end-of-frame capture, next-Start/manual start/stop/path/folder, native JPEG worker/indexed AVI, 3-frame queue/drops, letterboxing and replay/new/exit cleanup; cloud production worker/muxer known frames + independent FFmpeg decode pass; actual Unity capture/encoder/orientation/player pending |
| F29 | Startup/quit; examples; independent player; dependency notices; source downloads and final acceptance | M1 foundation / M11 complete | M1–M9 accepted; M10 recording and requested stage extension supplied; independent player/distribution M11 |

Future milestones must record cloud evidence and user acceptance against these IDs.
Only an explicitly approved difference can close a parity item without equivalent behavior.

The M10 performance fix retains all 404 original cases against portable production
source plus 11 presentation cases (415 total). Migration tests pass 232 cases,
including 15 new instancing-plan/conservative-contact/valid dense authoring/sight-
lifetime/detached phase contracts. Both unchanged six-case captures/oracle, five
legacy interop cases, v5 stage round trip/native rejection and AVI decoding pass.
Three paired CPU trials produce 24 exact full-population observed hashes and dense
.NET mean-step ratios of 1.62–1.91×; these are not actual Unity/GPU/FPS measurements.
Static checks cover 187 GUIDs, shader inclusion, black text/contrast/transparent
viewport and unchanged original files. 52 EditMode / 22 PlayMode cases are supplied,
unexecuted. M10 recording/stage acceptance and GPU/Mono/performance checks remain
local; M11 final player/distribution ownership is unchanged. See VALIDATION.md and
M10_PERFORMANCE_LOCAL_TESTING.md.
