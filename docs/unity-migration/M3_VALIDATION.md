# M3 validation evidence (delivery snapshot)

Executed in Codex cloud on 2026-10-20, Linux x64, SDK 8.0.415/runtime 8.0.21.
The user reported M2 tested successfully before authorizing this task. Local Editor
logs were not supplied. Historical M2 evidence is in [M2_VALIDATION.md](M2_VALIDATION.md).

| Check | Actual result |
| --- | --- |
| Original Release solution build | Passed |
| Original xUnit suite | **404 passed, 0 failed, 0 skipped** |
| Linked portable suite | **388 passed, 0 failed, 0 skipped**; 376 core plus 12 unchanged DashboardLayout/InspectorPanelState cases |
| Foundation/diagnostic/workspace xUnit suite | **57 passed, 0 failed, 0 skipped**; 32 existing plus 25 workspace cases |
| Portable production compilation | Passed: same production engine, commands, tuning, layout and shortcut source as Unity, C#9/netstandard2.1 |
| Original and portable seeded captures | Both match all six unchanged M1 traces and compatibility oracle exactly in the pinned cloud runtime |
| Static Unity contract | Passed: 116 unique GUIDs, serialized references, bundled fixtures/parity data, startup entries, resource/assembly wiring and 106 unchanged reference files |
| Reference/baseline preservation | Original src/tests/scenarios/solution/dependency pins and golden JSON unchanged |
| Shell syntax, Git whitespace, handoff links | Passed |
| GitHub Actions execution | Workflow remains available; remote result **not verified** |
| M3 Unity import/UPM/frontend compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode | **13 supplied, not executed**: 9 existing + 4 workspace tests |
| Unity PlayMode | **3 supplied, not executed**: 2 existing + Bootstrap workspace interaction test |
| Unity picking/input/layout/graphics | **Not executed**; explicit local checks required |
| Standalone player/IL2CPP | **Not executed**; no player delivered |

New cloud cases cover placement validity, exit widths, previews without live mutation,
exact planned-to-runtime starts, undo/room locks, both Hybrid modes, single-event
and finalisation guards, paused scheduling, playback scaling, all 14 tuning bounds/
reset, hidden inspector selection/grid continuity, Fume readiness and shortcut focus/
Alt+F4 policy. The reference frontend helpers are ported into the portable package;
the unchanged original layout tests are linked against those implementations.
The two remaining original builder-asset/rotation test classes stay in the 404-case
reference suite and retain M4 ownership. No original assertions were edited.

The new PlayMode test enters through the actual Bootstrap button and operates its
UI controls, using the controller's world-click command for exact world positions.
It does **not** prove screen-ray picking works; manual pointer tests remain required.
The existing xUnit2031 advisory can appear in linked original source; it is not a
failure, and no tests are disabled/skipped.

## Reproduce

From the repository root with SDK 8.0.415/runtime 8.0.21 installed:

```bash
bash Migration/Tools/validate.sh
```

For the current worker's private SDK:

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

The script restores migration tooling in locked mode, builds/tests both engine
variants and verifies captures without rewriting goldens. Actual TRX outputs live
under ignored `Migration/artifacts/tests/`; CI uploads them when it executes.
See [M3_LOCAL_TESTING.md](M3_LOCAL_TESTING.md) for required Editor acceptance.

The user subsequently reported M3 tested successfully and authorized M4. No local
Editor logs were supplied; the execution evidence above remains the delivery snapshot.
