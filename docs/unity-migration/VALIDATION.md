# M8 validation evidence

Executed in Codex cloud on 2026-10-21, Linux x64, SDK 8.0.415/runtime 8.0.21.
The user reported M7 successfully tested and authorized M8 plus ongoing user-centred
colours/fonts. No local Editor logs were supplied. [M7 evidence](M7_VALIDATION.md).

| Check | Actual result |
| --- | --- |
| Original Release solution build | Passed; existing xUnit2031 advisory retained |
| Original regressions | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: 404 unchanged original + 11 furniture presentation |
| Migration suite | **159 passed, 0 failed, 0 skipped**: 135 existing + 24 humanoid/camera cases |
| Portable production compilation | Passed, C#9/netstandard2.1, same package source used by Unity |
| Reference and portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly, both hazards/Hybrid modes |
| Native/portable file interop | **5 passed**, actual native/portable codecs and stores, schemas 1–4 |
| Static Unity/download contract | Passed: 161 unique GUIDs, package/resource/assembly/scene wiring, bundled font/license, exact reference data and 106 unchanged original files |
| Authored UI contrast | Key solid text/surface pairs pass 4.5:1, including both hazard primary-button hover/focus; not a rendered accessibility audit |
| Reference preservation | Original source/tests/scenarios/solution/pins, M1 goldens and portable Engine directory unchanged |
| Whitespace, shell syntax, local handoff links | Passed |
| GitHub Actions remote run | **Not verified** |
| M8 Unity import/UPM/frontend compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode | **41 supplied, not executed**: 34 previous + 7 M8 cases |
| Unity PlayMode | **15 supplied, not executed**: 12 previous + 3 M8 cases |
| Actual font import/glyphs, URP shader/rendering, native pointer/keyboard, resize/layout | **Not executed** |
| Standalone/IL2CPP build/run, GPU performance | **Not executed** |

The 24 new migration cases cover all nine active health/response states at both
positive-volume detail levels, pose precedence, seated/rising/kneeling geometry,
walking/running/still gait, state palette and independent alarm/help/panic cues,
bounded interpolation/pause/replay, camera projection/axes/head height at all three
zoom checkpoints, hidden-inspector selection, camera commands/clear/replay and an
actual selected-agent exit. A cloud test links the unchanged native Camera3D source
and compares the real POV projection, including its 72° FOV and 0.04 m near plane.
The exit fixture detonates within the native panic radius; no model parameter was changed to make it evacuate.

Paired 80-tick runs in both hazards and both Hybrid modes retain identical ordered
positions, states, furniture motion, debris energy, exposure, exits and authored
exports despite repeated geometry/camera/selection operations. Snapshots retain
values after the source model changes and contain no mutable Agent references.
Existing traces and original tests continue to guard the simulation itself.

New supplied Unity cases cover positive-volume poses, indicators, coordinate/camera
mapping, interpolation and hidden-panel replay; real workspace Follow/POV buttons,
head-height camera, self hiding, independent inspector state, font resource,
rendered-volume picking for standing/seated/crawling/prone poses, pool retirement/
reuse, and production-renderer pose-gallery loading/cleanup. They were not executed.
Synthetic submits and explicit coordinates do not prove native pointer behaviour.

No generated image or historical screenshot is presented as M8 graphical evidence.
The isolated pose gallery is an actual runnable Unity scene, not a simulation result.
Large-crowd render performance/instrumentation remain M9. Player acceptance remains M11.

## Reproduce

Run from repository root with the pinned .NET 8 SDK:

```bash
bash Migration/Tools/validate.sh
```

Private cloud SDK example:

```bash
DOTNET_ROOT=/workspace/crowdsim-tools/dotnet \
DOTNET_CLI_HOME=/workspace/crowdsim-cache/dotnet-home \
NUGET_PACKAGES=/workspace/crowdsim-cache/nuget \
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet \
bash Migration/Tools/validate.sh
```

The final migration suite was rerun after the independent combined alarm/help label
contrast/rise refinements and direct native camera projection check. Static validation and git preservation/packaging checks
were rerun on the final delivery. Use [M8_LOCAL_TESTING.md](M8_LOCAL_TESTING.md) for
actual Editor tests and visual/input acceptance.
