# M5 validation evidence

Executed in Codex cloud on 2026-10-20, Linux x64, SDK 8.0.415/runtime 8.0.21.
The user reported M4 tested successfully before authorizing M5; no local Editor
logs were supplied. Historical evidence is in [M4_VALIDATION.md](M4_VALIDATION.md).

| Check | Actual result |
| --- | --- |
| Original Release solution build | Passed |
| Original regression suite | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: all 404 unchanged original cases plus 11 presentation cases |
| Migration suite | **91 passed, 0 failed, 0 skipped**: 70 existing plus 21 file/lifecycle/replay cases |
| Production portable compilation | Passed, C#9/netstandard2.1, same production sources consumed by Unity |
| Original and portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly on pinned cloud runtime |
| Native/portable file interop | **5 cases passed** using actual original System.Text.Json codec/file store and portable production codec/file store: v1 units/blue walls/random starts, original v2, v3 default upgrade, furnished v4/free angle, original v4 Fume205 |
| Static Unity contract | Passed: 129 unique GUIDs, package/resource/assembly wiring, startup bindings, exact bundled reference copies and 106 unchanged original files |
| Reference preservation | Original source/tests/scenarios/solution/dependency pins, M1 goldens and portable engine algorithms unchanged |
| Shell syntax, whitespace, handoff links | Passed |
| GitHub Actions remote run | **Not verified** |
| Unity M5 import/UPM/frontend compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode | **25 supplied, not executed**: 17 existing + 8 file/schema/replay cases |
| Unity PlayMode | **7 supplied, not executed**: 5 existing + 2 Files/replay interactions |
| Native input/picking/resizing/rendering | **Not executed** |
| Standalone/IL2CPP build/run | **Not executed** |

The new migration tests cover portable names/device names/path containment,
read-only versus writable identity, list/refresh, Save/Save As/overwrite/back,
dirty load/new/leave decisions, failed writes/atomic cleanup, malformed schema and
layout rejection with session retention, legacy random starts, authored exports
after actual blast damage, and 205-person Fume replay in both Hybrid modes. Replay
checks exact initial positions, 80 seated occupants, intact furniture, zero tick,
zero exposure and a valid unreleased rebuilt cache. Existing tests retain all
original simulation, defaults, enum, strict JSON and upgrade coverage.

The interop executable references both actual assemblies through extern aliases.
Each input is upgraded/loaded by both engines, captured as an authored definition,
written by the portable store (including existing-file atomic replacement), read
by the unchanged native store, written by that native store and read by the port.
Canonical native deserialization compares schema values rather than whitespace or
float spelling. This is cloud .NET 8 interop evidence; Mono filesystem/codec behavior
still needs the supplied Unity tests and manual checklist. No cloud result implies
that Unity frontend source compiled.

Unity tests use temporary injected storage directories. New PlayMode cases operate
actual Files controls, prompts, paused-load enablement, saved Fume release/replay,
and failed import retention. They do not establish OS permissions, native pointer
picking, visual layout or persistence across Editor restarts. Manual testing remains
required. Existing xUnit2031 in original rectangular-table tests is an advisory;
no assertions were changed to silence it.

## Reproduce

From the repository root with SDK 8.0.415/runtime 8.0.21 installed:

```bash
bash Migration/Tools/validate.sh
```

Private SDK example for this worker:

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

Tooling restores use committed lockfiles. Captures and interop verify without
rewriting baselines or fixtures. TRX results are under ignored
`Migration/artifacts/tests/` and uploaded by the GitHub workflow when it executes.
Follow [M5_LOCAL_TESTING.md](M5_LOCAL_TESTING.md) before accepting M5.
