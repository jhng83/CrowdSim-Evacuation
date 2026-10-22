# M11 validation — dense CPU lag correction (v0.11.1)

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
Base: initial M11 `e76d7262bceadc1f314f306274d6df025c0f9468` (v0.11.0).
M10 remains user-accepted at `c97d49c`. M11's reported lag is being corrected;
**local performance and final Windows standalone acceptance remain pending**.

| Check | Actual result |
| --- | --- |
| Full validation | `bash Migration/Tools/validate.sh` passed; original Release solution and C#9/Standard2.1 portable production compile |
| Original / portable regressions | **404 / 415 passed**, zero failures/skips |
| Migration regressions | **272 passed**, zero failures/skips (263 prior + 9 CPU-query cases) |
| New broad-phase tests | Thousands of seeded contact/candidate and dense ordered-projection cases; cell edges/touching radii; source order/no duplicates; movement/removal/reordering; invalid/huge geometry fallback; same-count live replacement after a tick |
| Paired CPU/state comparison | See [benchmark evidence](M11_PERFORMANCE_FIX_BENCHMARK.md), including all trial timing/hash scope and before/after hazard cases |
| Reference / portable captures | Both six-case traces and compatibility oracle match unchanged goldens |
| File interop | Five native v1–v4 cases and custom-stage v5 round trip/native rejection pass |
| Recording | Production worker/muxer frames independently decoded with ffprobe/ffmpeg at 1280×720/20 fps, correct orientation |
| Static project contract | **202 unique GUIDs**, complete package graph/bindings/settings, read-only Editor status and **106 unchanged original files** pass |
| M11 release/quit regression coverage | Dirty pause/save/discard/cancel/overwrite/failure and recording worker/package checks retained; synthetic PE64 fixtures are not real Unity executables |
| Source export | Committed-HEAD exporter and source-only receipt retained; candidate and committed content/hash checks run at delivery |
| Unity frontend/helper/shader compilation and tests | **Not executed — no Unity Editor installed**; **55 EditMode / 26 PlayMode** cases supplied |
| Windows BuildPipeline/player ZIP/native execution | **Not executed**; no prebuilt .exe claimed |
| Actual Unity Mono/GPU/frame rate, UI/input, AVI capture and quit | **Not executed**; local retesting required |
| GitHub Actions artifact publication | Unverified; source export is configured, Git push/download are separately checked at delivery |

## What changed and what the checks establish

The screenshot reports ~94 ms engine / ~75 ms movement with GPU instancing on.
Initial M11 did not change accepted M10's engine, so this is evidence of substantial
CPU movement work, not a proven regression caused by distribution/quit changes.
Different hazard timing, scene state, Editor optimization, hardware and standalone
execution must be compared consistently. No cause is inferred from the screenshot's
unmeasured GPU time or frame-time remainder.

v0.11.1 adds an ordered conservative spatial candidate index for circular obstacle
contact, containment, chair departure/pushing and local navigation. Exact tests,
thresholds, original list order, positional arithmetic and all movement substeps/
collision iterations remain. Queries refresh after each position correction and
rebuild after chair proxy changes; direct geometry edits outside Step stay live.
The first entry is visited directly, so empty lists invoke no index lookup. Small,
invalid and oversized geometry retains a full scan. Seeded tests and paired hashes
support preservation for the tested inputs; they are not proof for all possible
inputs or calibrated physical accuracy.

Every SFM circle force, including the distant nonzero clamp floor, is still
computed and accumulated. Fixed dt .15, RNG/order, finite/speed/contact guards,
individual exposure/navigation and default Hybrid-off remain. No scheduling,
rendering/GPU, recording algorithm or Editor optimization switch changes are used.
Version is 0.11.1 with the same Editor/URP/dependencies/backend/company/product.
Debug moves build and read-only optimization status to the top of its card.

Original src/tests/scenarios/pins and main are preserved. M11 guarded quit, help,
notices, build settings/helper, source export and package validation remain. Their
[initial distribution evidence](https://github.com/jhng83/CrowdSim-Evacuation/blob/e76d7262bceadc1f314f306274d6df025c0f9468/docs/unity-migration/VALIDATION.md)
is historical; the full-suite results above are from this new correction run.

## Reproduce and accept

```bash
bash Migration/Tools/validate.sh
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-m11-released 200 3 e76d726
DOTNET=/path/to/dotnet bash Migration/Tools/profile_m10_comparison.sh /tmp/crowdsim-m11-unreleased 200 3 e76d726 --profile-unreleased
python3 Migration/Tools/package_unity_source.py --output /tmp/CrowdSim-M11-CPU-source.zip
```

Use a fresh output path. The default exporter requires this correction committed
at HEAD; `--worktree` labels an uncommitted candidate, never an executable player.
The benchmark never edits the original reference or regenerates goldens.

Follow [CPU fix local testing](M11_PERFORMANCE_FIX_LOCAL_TESTING.md), then
[M11 standalone acceptance](M11_LOCAL_TESTING.md) and F01–F29 in
[FEATURE_PARITY.md](FEATURE_PARITY.md). Test Editor and independently built player
with matched hazards/ticks/camera/modes; report hardware and CPU/frame timings.
No guaranteed FPS, Unity speedup, additional OS target, final replacement acceptance
or main merge is claimed. Fix any reported issues before closing M11.
