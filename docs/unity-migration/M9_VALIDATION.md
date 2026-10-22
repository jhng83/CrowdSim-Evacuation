# M9 validation evidence

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
M8 was accepted by the user before this task; no Editor logs were supplied.
[M8 evidence](M8_VALIDATION.md). This task implements M9 and the explicitly requested
black-text/off-white architectural theme; M10 is not implemented.

| Check | Actual result |
| --- | --- |
| Original Release solution | Passed; existing xUnit2031 advisory retained |
| Original regressions | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: 404 unchanged original + 11 furniture presentation |
| Migration suite | **191 passed, 0 failed, 0 skipped**: 159 previous + 32 diagnostic/overlay/scale cases |
| Portable production source | C#9/netstandard2.1 compilation passed |
| Original and portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly, both hazards and Hybrid modes |
| Native/portable files | **5 cases passed**, schemas 1–4, actual codecs/stores |
| Scale adapter | Release build and locked restore passed; **24 cases executed**, both hazards × open/furnished × 220/500/1000 × Hybrid off/on |
| CPU comparison | [Results/method](M9_BENCHMARK.md), [CSV](M9_BENCHMARK.csv), [environment](M9_BENCHMARK_ENVIRONMENT.json); engine step and pure snapshot/geometry CPU/bytes reported separately |
| Static Unity/download contract | Passed: 173 unique GUIDs; packages/assemblies/scene/resource/benchmark wiring; font/license; reference copies; 106 unchanged original files |
| Requested style contract | Authored text black; key solid text/background pairs ≥4.5:1; workspace root transparent so it cannot cover the camera; real rendering still unverified |
| Preservation/hygiene | Original source/tests/scenarios/solution/pins, portable Engine and M1 goldens unchanged; metadata/links/shell syntax/whitespace/package contents checked |
| Remote GitHub Actions | **Not verified** |
| Unity M9 import/UPM/frontend compilation | **Not executed — no Editor installed** |
| Unity EditMode | **49 supplied, not executed**: 41 previous + 8 M9 |
| Unity PlayMode | **19 supplied, not executed**: 15 previous + 4 M9 |
| Actual black font/glyphs, pillar outlines, off-white lighting/grid, layout, pointer/F4/cameras | **Not executed** |
| Unity 220/500/1000 rendering/FPS/GPU profiling; standalone/IL2CPP build | **Not executed** |

The 32 new migration cases verify native mobile/active/exposure denominators,
selected speed units, route/navigation/assistance details and frozen exit/vector
snapshots; all eight finite overlay batches and POV self exclusion; native arrow
length/cap and vision range/angle; empty/nonfinite vector suppression; bounded
three-tick/forty-point trails with redraw/replay/exit handling; paused-only Hybrid
changes and replay/export rebasing without runtime state capture; exact deterministic
comparison populations/seats/starts/separation; bounded wall-frame-rate versus CPU
statistics; pillar metadata/default grid and preserved physical blockages/perimeter.
Paired both-hazard/both-mode runs retain identical ordered bodies/states/debris energy,
exposure and authored exports while all diagnostics/overlays/selection are exercised.

The diagnostic trail fixture uses an active stationary casualty to fill the complete
history before exit retirement. Overlay length assertions allow a two-micrometre
float-coordinate tolerance. Original tests/physics assertions/goldens were not edited.

Supplied Unity cases operate actual Debug overlays, hidden inspector and replay,
Hybrid controls/running locks, dirty-protected comparison creation, black text and
transparent viewport, off-white floor/pillars/black outline resources, default grid,
metric text and cleanup. EditMode cases cover detached/native-unit contracts. These
are unexecuted source, and synthetic submits do not establish native mouse behaviour.
No screenshot, graphical FPS or GPU capacity claim is provided.

## Reproduce

From repository root with .NET SDK 8:

```bash
bash Migration/Tools/validate.sh
```

Private cloud executable example (also set DOTNET_ROOT/DOTNET_CLI_HOME/NUGET_PACKAGES
if your environment needs them):

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

The validator builds the benchmark adapter; timed comparisons are explicit separate
runs so routine validation has no wall-clock performance gate. Follow
[M9_BENCHMARK.md](M9_BENCHMARK.md) to reproduce measurements and
[M9_LOCAL_TESTING.md](M9_LOCAL_TESTING.md) for actual Editor acceptance.
