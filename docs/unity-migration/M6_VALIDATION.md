# M6 validation evidence

Executed in Codex cloud on 2026-10-21, Linux x64, SDK 8.0.415/runtime 8.0.21.
The user reported M5 tested successfully before authorizing M6; no local Editor
logs were supplied. Historical evidence is in [M5_VALIDATION.md](M5_VALIDATION.md).

| Check | Actual result |
| --- | --- |
| Original Release solution build | Passed |
| Original regression suite | **404 passed, 0 failed, 0 skipped** |
| Portable suite | **415 passed, 0 failed, 0 skipped**: all 404 unchanged original cases plus 11 presentation cases |
| Migration suite | **112 passed, 0 failed, 0 skipped**: 91 existing plus 21 Explosion command/presentation cases |
| Production portable compilation | Passed, C#9/netstandard2.1, same production sources consumed by Unity |
| Original and portable seeded captures | Both match all six unchanged traces and compatibility oracle exactly on pinned cloud runtime, covering both hazards and both Hybrid modes |
| Native/portable file interop | **5 cases passed** across schemas 1–4 using actual original and portable codecs/file stores |
| Static Unity contract | Passed: 136 unique GUIDs, package/resource/assembly wiring, startup bindings, exact bundled reference copies and 106 unchanged original files |
| Reference preservation | Original source/tests/scenarios/solution/dependency pins, M1 goldens and portable engine algorithms unchanged |
| Shell syntax, whitespace, handoff links | Passed |
| GitHub Actions remote run | **Not verified** |
| Unity M6 import/UPM/frontend compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode | **29 supplied, not executed**: 25 existing + 4 Explosion cases |
| Unity PlayMode | **9 supplied, not executed**: 7 existing + 2 Explosion interactions |
| Native text/pointer input, picking, resizing, rendering/shaders | **Not executed** |
| Standalone/IL2CPP build/run | **Not executed** |

New migration cases cover finite/invariant text, neighbor ordering, increments,
resets, pre-event edits in builder/ready/running, post-event locks, extreme float
bounds, no-op dirty behavior, checkpoint rebasing after actual motion, save/replay
with retained settings and original starts, arming/cancel/pause, square/circle/
furniture/perimeter/outside/nonfinite placement rejection and the one-event guard.
Geometry cases check actual room/static-wall/circle visibility, sampled vertices
and outline midpoints, cache reuse/invalidation and replay cleanup. Model-time front
and dust checks run at 0.15 and 0.075 s. Debris checks verify detached native prism
sizes, live headings/energy and immutability. Paired furnished sessions in both
Hybrid modes retain identical positions, states, furniture, hit counters and authored
exports while display flags change. Existing original tests retain ordered physical
outcomes, propagation, swept debris impacts, visibility/shielding and furniture damage.

The exact six-case captures still match unchanged M1 baselines on both engines.
No reference test assertion, golden, scientific parameter, dependency pin or portable
engine algorithm was changed. The original dense near-200 m float-clamp limitation
was diagnosed; the UI command rejects unrepresentable edits transactionally instead
of changing that algorithm. This is not a new physical model.

Supplied EditMode cases check invariant commits/replay, clipping/cache, blocked
one-shot detonation and front/dust values on the actual host. PlayMode cases operate
Bootstrap and the actual Hazard text/keyboard/blur/increment/reset/arming controls;
inspect transparent mesh material/depth settings, locked fields, hidden-inspector
visibility, paused noninterference, replay and exit cleanup. Synthetic events and
explicit world coordinates do not establish native input or screen picking. These
Unity tests have not run here. Actual shader blending/appearance, pointer caret,
resize behavior and lifecycle require local testing.

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
Original rectangular-table tests can emit the existing xUnit2031 advisory; no test
was rewritten to silence it. Follow [M6_LOCAL_TESTING.md](M6_LOCAL_TESTING.md)
before accepting M6.
