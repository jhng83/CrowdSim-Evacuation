# Compatibility decisions and evidence

## Approved baseline

- Unity **6000.3.26f1**, URP **17.3.0**, API compatibility **.NET Standard 2.1**.
- UI Toolkit runtime UI; Input System **1.14.2**, both input backends enabled.
  Project input actions and simulator shortcuts remain M3 work.
- Unity Test Framework **1.4.6**; Unity Newtonsoft JSON package **3.2.2**.
- Portable source uses C# 9, block namespaces and explicit imports. The cloud
  `Migration/Build/CrowdSim.Portable.csproj` compiles the same production files
  under `UnityProject/Packages/com.crowdsim.core/Runtime/` as netstandard2.1.
- Migration build settings and central dependency declarations are isolated from
  the original .NET 8/C# 12 settings. The only new NuGet pin is Newtonsoft.Json
  **13.0.2**, matching Unity's package, under `Migration/Directory.Packages.props`.

Pins were checked against
[Unity's Editor source](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.3.26f1)
and [URP metadata](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/package.json).
M1 was accepted by the user. M2's new package/assembly wiring and frontend still
require actual Editor import; a static lock check cannot establish resolution.

## M2 source and runtime adaptations

All 41 original core files are represented in `Runtime/Engine/`, preserving
namespaces and subsystem order. Production changes are maintained there; the
reference `src/`, tests, solution, examples and dependency pins remain unchanged.
`Migration/Tools/port_reference.py` is an initial mechanical import aid targeting
an empty directory, not a generator for the now manually adapted production source.

- Six positional record structs and the oriented rectangle record struct become
  readonly value structs with equality/hash/operators and positional deconstruction
  where applicable. Record classes and init-only properties stay immutable using
  an internal `IsExternalInit` metadata shim; Unity's C# 9 supports their syntax.
- Newer argument guards, enum APIs, array clearing, OS checks, elapsed-time APIs,
  collection-expression syntax and `MathF.Tau` use equivalent Standard-compatible
  code. Timing is diagnostic, never simulation integration time.
- `ReferenceRandom` implements .NET 8's seeded subtractive sequence, independent
  of the host Mono random implementation. Differential tests cover five seeds,
  1,000 mixed calls each, wide integer ranges and both byte APIs. Unseeded sessions
  create an unpredictable integer seed; seeded runs remain repeatable.
- `ReferencePriorityQueue` retains the reference's quaternary heap and equal-key
  dequeue order. A stable tie-breaker would change routes. A 4,000-operation
  differential test and the unchanged compatibility oracle protect this behavior.
- Native System.Text.Json is replaced with `ScenarioJson` over Newtonsoft 13.0.2.
  Camel-case output, case-insensitive input, defaults, numeric enum identities,
  v1–v4 upgrades and authored-state semantics are retained. Lexical/schema checks
  reject comments, nonstandard numbers/strings, trailing commas, primitive coercion,
  integer overflow and nonfinite values rather than accepting Newtonsoft extensions.
  Existing-file replacement uses `File.Replace`; new files use `File.Move`.
- Unity's JSON package supplies **precompiled `Newtonsoft.Json.dll`**, not a
  `Unity.Newtonsoft.Json` asmdef. The core asmdef explicitly references that DLL
  with `overrideReferences`, and keeps `noEngineReferences: true`. Package 3.2.2
  metadata was inspected; no duplicate codec DLL is vendored into the project.

The portable production assembly does not reference UnityEngine or the reference
net8.0 engine. Simulation owns physics, random ordering, contacts, navigation,
furniture, hazard caches and exit admission. Presentation receives detached,
read-only frames and maps ground XY to Unity XZ; it cannot edit engine entities.
Primitive colliders are removed and Unity physics never advances this model.

The diagnostic scheduler follows the reference's configured step, elapsed clamp,
speed multiplication, catch-up cap and CPU budget. Pause stops scheduling and
clears accumulated time; engine `Running` stays true for reference query semantics.
Single-step advances exactly once. The fast check performs up to four steps per
render frame without changing integration dt. All preset runs stop at tick 120.

## Parity and tolerances

Cloud capture verifies all 121 observed tick hashes, complete initial traits,
readable checkpoints and replay against unchanged M1 goldens in six cases. The
376 applicable original regressions run against portable production source with
unchanged assertions. The other 28 original tests exercise desktop frontend helpers
and remain covered by the unchanged 404-case reference suite; frontend parity
is assigned to later milestones.

Unity diagnostics compare seven checkpoints (0/1/10/11/30/60/120), all initial traits,
and replay, using the readable baseline schema. Floats use absolute **1e-4** plus
relative **1e-5** tolerance; integers, strings, booleans, states and counts are exact.
Readable checkpoints sample six agents and retain full furniture/debris/metrics;
they are not a complete serialized runtime state. Full ordered observed-population
hashes are compared in cloud. No bitwise cross-runtime guarantee is claimed.
A Unity checkpoint failure blocks acceptance and must be investigated without
rewriting goldens or weakening assertions. See [the capture contract](../../Migration/Parity/README.md).

## Recording probe and limits

The unchanged reference pins SharpAvi 3.0.1, SharpAvi.ImageSharp 3.0.0 and transitive
ImageSharp 1.0.4. M1's Standard-compatible probe encodes two 64x64 frames at 20 fps,
using `WriteFrame(bool, byte[], int, int)`. No recording codec binary or Unity
capture implementation is delivered yet; end-of-frame whole-UI capture, bounded
queues, distribution notices and Mono/player recording remain M10 work.

The cloud worker has no Unity Editor. C# 9 / netstandard2.1 compilation and all
reported cloud tests ran; Editor frontend compilation/import, supplied Unity tests,
visuals/input and standalone/IL2CPP builds did not. These checks must be executed
locally, and [VALIDATION.md](VALIDATION.md) records the distinction.
