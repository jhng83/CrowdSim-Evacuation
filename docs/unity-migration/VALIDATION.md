# M10 validation evidence

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
M9 was reported successful by the user; no Editor logs were supplied.
[M9 evidence](M9_VALIDATION.md). This task implements M10 recording and the explicitly
requested adjustable concert-stage asset. M11 is not implemented.

| Check | Actual result |
| --- | --- |
| Original Release solution | Passed; unchanged xUnit2031 advisory may appear on clean rebuild |
| Original regressions | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: all 404 original + 11 furniture presentation |
| Migration suite | **217 passed, 0 failed, 0 skipped**: 191 previous + 19 stage cases + 7 recording cases |
| Portable production source | C#9/netstandard2.1 compilation passed; no UnityEngine dependency |
| Original/portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly |
| Native/portable files | **5 legacy cases pass**, schemas 1–4; additional **v5 Unity round trip and unchanged-native rejection pass** |
| Production AVI muxer/worker | Known red/blue top/bottom frames with second-frame green channel; JPEG decode, indexed RIFF sizes, 1280×720, 20 fps and 2 frames pass |
| Independent video tools | **ffprobe + ffmpeg** decode both production AVI frames with correct dimensions/rate/count/colour/orientation |
| Queue/error/lifetime | Bounded 3-frame backpressure, drop counting, stop/drain/idempotence, exclusive file access after stop/error, encoder/invalid-output errors, overwrite prevention, finite 20 Hz cadence and 100,000-frame index limit pass |
| Stage | Quarter turns/physical bounds/visual footprint, room/perimeter limits, NaN/infinity/min/max rejection, atomic conflict rejection, occupied-space guards, undo order, v5 round trip/finalise/replay and default v4 omission pass |
| Benchmark adapter | Locked restore and Release build passed; M9's 24 CPU comparisons are historical and were not rerun |
| Static Unity/download contract | Passed: **181 unique GUIDs**, metadata/packages/scene/resource/capture wiring and **106 unchanged original files** |
| Style | Black authored text/selected solid contrast ≥4.5:1 and transparent workspace root retained; rendered appearance unverified |
| Original files/goldens | Source/tests/scenarios/solution/dependency pins and all M1 baselines unchanged |
| GitHub Actions | **Not verified** |
| Unity import/UPM/frontend compilation | **Not executed — no Editor installed** |
| Unity EditMode | **51 supplied, not executed**: 49 previous + 2 M10 |
| Unity PlayMode | **21 supplied, not executed**: 19 previous + 2 M10 |
| Unity full UI capture/native encoder/orientation/resize/counter UI | **Not executed** |
| Concert asset/font/shader/input/camera/hazard visual review | **Not executed** |
| Standalone Mono/IL2CPP recording/build/quit; real FPS/GPU/memory profiling | **Not executed** |

## Scope of recording evidence

The production portable `MjpegAviWriter` and `VideoRecording` worker run in cloud.
Tests inject the existing cloud-only ImageSharp probe dependency as JPEG encoder;
no codec binary is shipped to Unity. ffprobe reports `mjpeg`, 1280×720, `20/1`, two
frames, and ffmpeg independently verifies both decoded frames' top/bottom colours.
This establishes the container/worker contract, not Unity GPU composition or native
JPEG orientation. Unity's `EncodeArrayToJPG` signature/thread-safe declaration was
checked against the exact pinned Editor source. Supplied Editor cases exercise
that native worker API and composed UI markers in an interactive Game view.

Encoding has at most three queued 1280×720 RGBA buffers plus one active encoder
frame. Capture owns fixed output/readback resources and one bounded-size source
render texture, recreated on resize. AVI index is bounded at 100,000 frames; file
size is bounded at 1 GiB. Missed cadence/full-queue slots are counted and omitted;
there is no simulation slowdown to force video time and no audio. Real Unity GPU
allocations/native encoder peak memory, orientation and capture cost remain local
measurements. Graceful stop finalizes accepted frames; a forced process kill or
storage failure can still leave an incomplete file.

## Stage and compatibility evidence

Only portable stage construction/placement/resizing/persistence is extended; the
original source remains intact. Defaults retain the original 15×6×0.8 m stage.
Custom stages retain the same fixed low obstacle/no-occupants/no-walkable-deck rules.
The perimeter keeps its 0.6 m physical inset. All candidate and resized footprints
still reject geometry, exits, people and occupied-set reservations; resize never
moves the stage centre/facing and retains placement undo order. Finalisation locks
size and replay restores the authored footprint. Rig parts remain in that footprint
and do not add physical/hazard walls. V5 files signal the extension explicitly;
actual native-codec rejection is checked instead of allowing silent wrong sizing.

The first differential capture found only the new zero/default DTO fields in its
System.Text.Json authored section; ordered hashes/checkpoints/replay were identical.
The harness now honors the production Newtonsoft codec's omission rule for those
two optional fields only. It retains all original fields and nonzero custom values.
No baseline, state hash, physics assertion or tolerance was changed.

## Reproduce

```bash
bash Migration/Tools/validate.sh
```

With a private SDK, set DOTNET_ROOT/DOTNET_CLI_HOME/NUGET_PACKAGES as appropriate and
`DOTNET=/workspace/crowdsim-tools/dotnet/dotnet`. The script retains ignored TRX logs
and `Migration/artifacts/m10-known-frames.avi`; if ffprobe/ffmpeg are installed it
runs `Migration/Tools/verify_recording.py` independently. Otherwise it explicitly
reports that decoder check skipped. This cloud run had both tools and passed it.
See [M10_LOCAL_TESTING.md](M10_LOCAL_TESTING.md) for the unexecuted Editor checks.

Readback RGBA buffers use ArrayPool to avoid a full managed image allocation on
every frame. Ownership returns after encode, rejection, worker failure and stop
drain. Native JPEG allocations and actual graphics memory still require profiling.
