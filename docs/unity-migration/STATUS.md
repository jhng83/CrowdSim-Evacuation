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
| M9 — Diagnostics/scale + requested light architecture theme | `86ae9d16d8a7f928b6d004d84fd8b639bc7f5ad2` | [M9](M9_VALIDATION.md), [CPU comparisons](M9_BENCHMARK.md) | Accepted by user |
| M10 — Recording + requested adjustable concert stage | This milestone commit | [Current validation](VALIDATION.md) | Awaiting user testing |
| M11 — Complete replacement | Planned | Unrun | Unrun |

M10 supplies runtime complete-window/UI 1280×720/20 fps MJPEG AVI capture, next-Start
arming, manual start/stop, path/folder/status, bounded three-frame encoding queue,
drop/capture/encoding counters, resize letterboxing and lifecycle finalization.
It uses Unity's native JPEG API on a worker and a portable indexed AVI muxer;
no Editor-only recording tool or new external runtime codec binary is needed.

The additional user request supplies editable stage length/depth (also in-place,
builder-only), rotated room/perimeter/overlap guards, exact preview/collision/save/
replay geometry and a concert deck/truss/LED/speakers/lamp appearance. Stage stays
fixed/unoccupied/non-walkable; the rig is visual only. Optional custom dimensions
use explicit Unity schema v5, which the original app rejects. Default-size stages
and ordinary saves remain v4; v1–v4 imports/interoperability are preserved.

Cloud passes 404 original, 415 portable and 217 migration cases, both exact six-case
captures/oracle, five original schema interop cases, v5 Unity round trip/native
rejection and independent FFmpeg decode. Static contract covers 181 unique GUIDs
and 106 unchanged original files. Portable package is 0.10.0. Editor/URP pins stay
fixed; only the required built-in imageconversion/screencapture modules are added.
Original source/tests/scenarios/pins, main and golden baselines remain unchanged.
The portable engine has the narrow, user-authorized stage sizing/persistence
extension described in COMPATIBILITY.md; default simulation traces remain exact.

No Unity Editor is installed: frontend compilation/import, 51 supplied EditMode and
21 PlayMode cases, complete UI capture/orientation, concert appearance, graphics
resizing, native controls and player builds were not executed. Remote CI is
unverified. Cloud AVI evidence tests the production muxer/worker with a cloud-only
JPEG encoder; it does not establish Unity capture or its JPEG API's native result.

Next: [M10_LOCAL_TESTING.md](M10_LOCAL_TESTING.md). Stop for user results and fix
M10 problems before M11. Supported desktop OS/player target confirmation belongs
to M11's platform-specific build/acceptance work.
