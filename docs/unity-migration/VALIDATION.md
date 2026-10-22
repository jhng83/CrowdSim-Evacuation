# M11 validation — build package preflight and recovery

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
Base: `eeb484b65b94f349f1d677179ea33d9510d2e60a`, application version **0.11.1**.
The user reports **latency solved**, then supplies a failed Windows build with
explicit Input System/search authentication errors, missing Burst Unsafe assembly
and URP host-type import errors. **Windows build/player acceptance remains pending**.

| Check | Actual result |
| --- | --- |
| Full cloud validation | `bash Migration/Tools/validate.sh` passed; original Release solution and C#9/Standard2.1 portable production compile |
| Original / portable regressions | **404 / 415 passed**, zero failures/skips |
| Migration regressions | **280 passed**, zero failures/skips (272 prior + 8 package-readiness cases) |
| Production metadata checker | Eight new cases link actual Editor/PackageReadiness.cs: missing package, registered errors, pin mismatch, malformed/missing installed metadata, embedded/virtual modules and no file changes |
| C#9/Standard2.1 helper probe | Actual PackageReadiness.cs independently compiled in a temporary netstandard2.1/C#9 project against pinned Newtonsoft.Json 13.0.2; zero warnings/errors; no Unity API shim claimed |
| Preflight wiring guard | Static check enforces readiness before output creation/BuildPipeline, the read-only menu and no mutating helper calls |
| API source check | Official Unity 6000.3.26f1 PackageInfo reference confirms name/version/resolvedPath/errors metadata; this does not compile/execute Editor integration |
| Reference / portable captures | Both six-case traces and compatibility oracle match unchanged goldens |
| File interop | Five native v1–v4 cases and custom-stage v5 round trip/native rejection pass |
| Recording worker/muxer | Independent ffprobe/ffmpeg decoding at 1280×720/20 fps with correct orientation passes |
| Static project contract | **203 unique GUIDs**, package graph/bindings/settings and **106 unchanged original files** pass |
| Accepted performance implementation | Production runtime/portable/physics/rendering/recording sources, package pins and PlayerSettings unchanged from eeb484b; no repeated benchmark/FPS claim |
| Source export | Working candidate and committed-HEAD source content/hash validation run at delivery; source only |
| Unity frontend/Editor/menu/helper compilation, package resolution/import | **Not executed — no Unity Editor installed** |
| Unity tests | **55 EditMode / 26 PlayMode supplied, not executed in cloud** |
| Hub sign-in, real Burst/URP, Windows build/ZIP/native player | **Not executed**; actual failed local build is recorded, not replaced by synthetic fixture success |
| Remote Actions artifact publication | Unverified; source exporter configured; Git push/commit source download checked separately |

## Evidence boundaries

The preflight compares the local lock graph with registered package versions and
errors and matching readable package.json files. Built-in engine modules can be
virtual. It runs before output creation/build and is also available through
CrowdSim → Build → Check Package Readiness. It does not authenticate, clear caches,
resolve/upgrade/remove packages, change optimization, disable Burst, inspect every
DLL dependency or prove shader import/native compiler execution succeeds. A pass
must not be described as fixing the user's Hub/Burst/URP environment.

The actual fatal local step is Burst's native-plugin generation failing to load
Unsafe 6.0.3.0. The log explicitly establishes a separate missing-authentication
blocker. Incomplete cache/payload/path/import issues are possible causes for the
other failures, not established conclusions. The new actionable error guidance
keeps the real BuildPipeline success requirement and no-ZIP-on-failure behavior.
No standalone DLL is injected and no failed player is delivered as a release.

Registry/tarball inspection from this cloud worker was unavailable (proxy tunnel
403), which is distinct from the user's Unity authentication failure. Unity's
public PackageInfo source was readable; checking that API is not Editor compilation.
No credentials or user logs/dumps are added to the repository.

Original src/tests/scenarios/pins and main remain unchanged. Runtime/portable and
application version remain exactly the accepted CPU implementation at eeb484b.
The prior [48 matching state hashes and CPU measurements](M11_PERFORMANCE_FIX_BENCHMARK.md)
are historical evidence for those unchanged sources, not a new benchmark run.
The user reports latency solved but has not accepted a standalone player.

## Local recovery and reproduction

Follow [M11_BUILD_RECOVERY.md](M11_BUILD_RECOVERY.md): close Unity, refresh Hub
sign-in, reopen through Hub, allow package resolution/import, and use a fresh
short-path extraction without old caches if needed. Keep pins and the existing
optimization mode. Check Package Readiness and Console, then rebuild Windows x64.
If a clean authenticated attempt still fails, preserve the new Editor.log/upm.log
and first remaining error. Finish [M11 player testing](M11_LOCAL_TESTING.md) only
after a real successful build, independent launch and all F01–F29 workflows pass.

```bash
bash Migration/Tools/validate.sh
python3 Migration/Tools/package_unity_source.py --output /tmp/CrowdSim-M11-package-source.zip
```

Use a fresh output path. Default source export requires this correction committed
at HEAD; --worktree explicitly labels a candidate. Final M11 acceptance, real
Windows ZIP/player testing and any further milestone/main merge remain pending.
