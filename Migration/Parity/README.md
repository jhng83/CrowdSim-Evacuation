# Reference capture contract — M1

These are observations of the original .NET engine at commit
`01b435568f211f5c8ddf99b32b05e7ceb1ad3a24`, not results from a Unity engine port.
The latter begins in M2. The original src/, tests/, fixtures and dependency files
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
```

From Migration/ the pinned global.json enforces the SDK. Full cloud validation uses
`bash Migration/Tools/validate.sh` and restores committed tooling lockfiles.
`--verify` regenerates observations in memory and compares them without changing
the checked-in baselines. `--write` is an explicit baseline-authoring operation;
never use it to hide a regression. Diagnose source, runtime and platform differences.

M1 requires exact repeatability in its reference environment. Future Mono/IL2CPP
comparisons must distinguish numeric tolerance from exact discrete-state agreement.
These exact hashes do not promise bitwise results on all runtimes/platforms. M2
must establish documented tolerances before acceptance and investigate divergence.
