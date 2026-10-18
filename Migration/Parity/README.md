# Reference and portable capture contract — M2

The unchanged goldens are observations of the original .NET engine at commit
`01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`. M2 runs the portable engine against those same goldens;
they are never regenerated to fit a port. The original src/, tests/, fixtures and dependency files
are protected by a SHA-256 source manifest covering 106 tracked reference files.

## Captured cases

Open Explosion (50 agents), furnished Explosion (61 including 11 seated occupants),
and the existing 205-person Toxic Fume fixture, each with Hybrid off and on.
The furnished scene includes every furniture kind, square/circular obstacles and
an angled barricade. Preset construction fails rather than silently dropping an item.
Initial traits and authored definitions are retained with each trace.

Each case observes ticks 0–120 inclusive. The hazard triggers after observation 10
and before the next Step; tick 11 is the first post-event observation. Every step
must advance exactly once, keep positions/velocities finite, and leave a replay
checkpoint capable of restoring the exact starting positions at paused frame zero.

Tick hashes cover the full ordered observed population, route state, furniture,
debris, exposure, exit metrics and Hybrid counters. Readable checkpoints at
0/1/10/11/30/60/120 retain the first/last three agents plus full furniture/debris/
metrics. `SampledAgentsOnly` distinguishes those diagnostic views from the complete
state used for hashing. These fields are observations, not a complete runtime-state
serialization: hidden RNG/navigation internals are not asserted by the hash alone.

The compatibility oracle contains seeded Random integer/double/byte sequences and
equal-priority dequeue order. Wall-clock timestamps/performance measurements are
excluded, so timing noise cannot masquerade as behavioral drift.

## Running

Use SDK 8.0.415 (runtime 8.0.21), installed explicitly by CI. From the repository root:

```bash
dotnet run --project Migration/Parity/ReferenceCapture/ReferenceCapture.csproj -c Release -- --verify "$PWD"
dotnet run --project Migration/Parity/PortableCapture/PortableCapture.csproj -c Release -- --verify "$PWD"
```

From Migration/ the pinned global.json enforces the SDK. Full cloud validation uses
`bash Migration/Tools/validate.sh` and restores committed tooling lockfiles.
`--verify` regenerates observations in memory and compares them without changing
the checked-in baselines. `--write` is an explicit baseline-authoring operation;
never use it to hide a regression. Diagnose source, runtime and platform differences.

Both cloud captures share the capture program with conditional engine/compatibility
references. Reference uses the native .NET 8 Random/PriorityQueue oracle; portable
uses ReferenceRandom/ReferencePriorityQueue. All six traces and the oracle match
exactly on the pinned runtime. This establishes reference-environment agreement,
not bitwise agreement on Mono/IL2CPP.

## Actual Unity runtime checks

Production DiagnosticSession independently constructs presets and compares initial
traits, the seven readable checkpoints and finalized replay. Its comparator allows
absolute 1e-4 plus relative 1e-5 for float fields, and exact integer/state/string/
boolean agreement. Checkpoint views sample first/last three agents while retaining
full furniture/debris/metrics; cloud hashes observe the full population. Neither
is a complete serialization of hidden navigation/RNG state.

The scene and six supplied EditMode cases run that same session on the actual Unity
host runtime, with bundled exact copies of the reference JSON. Cloud tests execute
the session too, but cannot substitute for Mono results. The scene reports up to
12 mismatch paths with expected/actual values. A mismatch requires investigation,
not golden regeneration or tolerance changes. Unity checks remain unexecuted until
a real Editor runs them. Follow [the M2 checklist](../../docs/unity-migration/M2_LOCAL_TESTING.md).
