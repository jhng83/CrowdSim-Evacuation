# M4 validation evidence

Executed in Codex cloud on 2026-10-20, Linux x64, SDK 8.0.415/runtime 8.0.21.
The user reported M3 tested successfully before authorizing M4; no local Editor
logs were supplied. Historical evidence is in [M3_VALIDATION.md](M3_VALIDATION.md).

| Check | Actual result |
| --- | --- |
| Original Release solution build | Passed |
| Original regression suite | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: all 404 unchanged original cases plus 11 new furniture presentation cases |
| Migration suite | **70 passed, 0 failed, 0 skipped**: 57 existing plus 13 furniture command cases |
| Production portable compilation | Passed, C#9/netstandard2.1: same engine, picker, session, snapshot and geometry source consumed by Unity |
| Original and portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly in pinned cloud runtime |
| Static Unity contract | Passed: 121 unique GUIDs, startup/example entries, picker resource/assembly wiring, serialized references, exact bundled data and 106 unchanged original files |
| Reference preservation | Original source/tests/scenarios/solution/dependency pins, M1 goldens and portable engine algorithms unchanged |
| Shell syntax, whitespace, handoff links | Passed |
| GitHub Actions remote run | **Not verified** |
| Unity M4 import/UPM/frontend compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode | **17 supplied, not executed**: 13 existing + 4 furniture cases |
| Unity PlayMode | **5 supplied, not executed**: 3 existing + 2 furniture interactions |
| Native input/picking/resizing/rendering | **Not executed** |
| Standalone/IL2CPP build/run | **Not executed** |

The remaining original picker and barricade-angle tests now link against the
ported production helper. The original test sources/assertions are unchanged;
none are excluded, disabled or skipped. Existing xUnit2031 in original rectangular
table test source can emit an advisory; no original test was rewritten to silence it.

New workspace checks cover all five kinds, occupant counts, preview/commit geometry,
whole-set undo, failed-placement atomicity, exact/per-asset angles, one-stage guards,
parallel snapping on/off, example starts/seating, locks, both Hybrid modes and R
focus/modifier policy. New presentation checks cover all chair states, detached
body status/collections, live height/heading/position, actual timed seat departure,
round/rectangular tipped profiles, precise quarter-turn table footprints and actual
blast destruction/removal. The unchanged original suite retains pushing/chains,
flight/impacts, support/landing, fixed-asset and dynamic navigation checks.

Supplied Unity interactions enter through actual Bootstrap buttons and operate the
picker/angle/rotation/stage/undo/finalise/transport controls. Exact world positions
are passed through the controller's world-click command. A pooled-view primitive
replacement check is included. These tests do not establish screen picking or native
R/Shift+R dispatch; manual tests remain required. No actual Editor result is inferred
from cloud tests or from prior accepted milestones.

## Reproduce

From the repository root with SDK 8.0.415/runtime 8.0.21 installed:

```bash
bash Migration/Tools/validate.sh
```

Private SDK example for this worker:

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

Tooling restores use committed lockfiles. Captures verify without overwriting
baselines. Actual TRX results are under ignored `Migration/artifacts/tests/` and
are uploaded by the GitHub workflow when it executes. Follow
[M4_LOCAL_TESTING.md](M4_LOCAL_TESTING.md) before accepting M4.
