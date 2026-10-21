# M7 validation evidence

Executed in Codex cloud on 2026-10-21, Linux x64, SDK 8.0.415/runtime 8.0.21.
The user reported M6 tested successfully before authorizing M7 plus UX/UI/assets
refinement; no local Editor logs were supplied. [M6 evidence](M6_VALIDATION.md).

| Check | Actual result |
| --- | --- |
| Original Release solution build | Passed |
| Original regression suite | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: all 404 unchanged original cases plus 11 presentation cases |
| Migration suite | **135 passed, 0 failed, 0 skipped**: 112 existing plus 23 fume/lifecycle/display cases |
| Production portable compilation | Passed, C#9/netstandard2.1, same production sources consumed by Unity |
| Original and portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly on pinned cloud runtime, both hazards and both Hybrid modes |
| Native/portable file interop | **5 cases passed** across schemas 1–4 using actual original and portable codecs/file stores |
| Static Unity contract | Passed: 146 unique GUIDs, package/resource/assembly wiring, new startup bindings, exact bundled reference copies and 106 unchanged original files |
| Reference preservation | Original source/tests/scenarios/solution/pins, M1 goldens and portable engine algorithms unchanged |
| Shell syntax, whitespace, handoff links | Passed |
| GitHub Actions remote run | **Not verified** |
| Unity M7 import/UPM/frontend compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode | **34 supplied, not executed**: 29 existing + 5 Fume/display-mesh cases |
| Unity PlayMode | **12 supplied, not executed**: 9 existing + 3 Fume/UI/material interactions |
| Native input/picking/resize/style/material/light/shader appearance | **Not executed** |
| Standalone/IL2CPP build/run | **Not executed** |

New migration cases cover all ten controls against actual native configuration
clamps, dependent thresholds, invalid/nonfinite source/parameters, clear floor and
resize containment, compute/readiness, source/layout/undo/settings invalidation,
behavior/display independence, preview isolation and bounds, runtime locks, one-shot
release, authored export and replay. Paired Fume205 runs in both Hybrid modes have
identical ordered positions/states/exposure while preview/view flags differ. Tests
verify 205/80 seated replay, actual cache samples/blocked cells/mesh counts, permanent
incapacity after decay, and display-mesh unit bounds/normals/outward winding. Existing
original tests retain concentration/arrival/drift/obstacles/risk-route/finite guards.

Original/portable six-case traces and native file round trips still match unchanged
baselines. No original assertion, expected numerical outcome, golden, scientific
parameter or portable engine algorithm changed. One migration-only M3 placeholder
assertion now checks the release-point guard instead of future-milestone copy.
Supplied Unity button/overview/mesh-name assertions follow the completed workflow
and newly requested assets; their behavioral checks remain. These edits do not
report a Unity test pass.

New EditMode cases check preview/authoring isolation, invalidation/locks, parameter
dependencies, actual-host field sampling and display mesh bounds. New PlayMode cases
operate real startup/source/settings/compute/preview/release controls, hidden-panel
plume/grid behavior, the 205-person example/replay, lit materials/owned meshes/lights
and cleanup. Synthetic events/explicit world coordinates do not establish native
input or screen picking. These tests were not executed here. No rendered screenshot
or graphical performance claim is supplied. The visual refinement needs local review.

## Reproduce

From the repository root with SDK 8.0.415/runtime 8.0.21 installed:

```bash
bash Migration/Tools/validate.sh
```

Worker SDK example:

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

The restored worker initially had only the original branch; the accepted M6 branch
was fetched from GitHub and SDK 8.0.415 installed outside the repository. Tooling
restores use committed lockfiles. Captures/interop verify without rewriting data.
TRX results are in ignored Migration/artifacts/tests and uploaded by CI when it
executes. The existing original xUnit2031 advisory was not suppressed by changing
tests. Follow [M7_LOCAL_TESTING.md](M7_LOCAL_TESTING.md) before accepting M7.
