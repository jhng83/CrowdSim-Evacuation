# M11 validation — standalone application/distribution implementation

Executed 2026-10-22 (Asia/Singapore), Linux x64, SDK 8.0.415/runtime 8.0.21.
Base: user-accepted M10 `c97d49c84ecf9c79d08239fa83e4703eeb7353bc`.
M11 implements the approved roadmap's Windows x64/Mono target and delivery path.
**Final standalone acceptance is pending local tests. No prebuilt .exe is claimed.**

| Check | Actual result |
| --- | --- |
| Full validation command | `bash Migration/Tools/validate.sh` passed; original Release solution and C#9/Standard2.1 portable production build pass |
| Original / portable regressions | **404 / 415 passed**, zero failures/skips; original source/tests/pins preserved |
| Migration regressions | **263 passed**, zero failures/skips (239 prior + 24 release/quit cases) |
| Quit transaction | Dirty quit pauses and retains authored data on cancel; clean quit completes; save/overwrite/failure decisions gate completion; existing transitions cannot be silently replaced |
| Player package contract | Actual portable packager tested against explicitly synthetic PE64 headers/layouts; missing runtime/guide/examples/licenses, unsupported receipts/architecture, editor/test assemblies and external links are rejected |
| ZIP integrity/output safety | Required payload and per-file SHA256 verified; symbols/debug backups excluded; existing archive never overwritten and ZIP cannot include itself |
| Reference / portable captures | Both six-case traces and compatibility oracle match unchanged goldens |
| File interop | Five native v1–v4 cases and custom-stage v5 portable round trip/native rejection pass |
| Recording | Known production AVI worker/muxer frames independently decoded by ffprobe/ffmpeg at 1280×720/20 fps with correct orientation |
| Static project contract | **200 unique GUIDs**, complete package graph, startup bindings, Editor-only assembly, version/product/Mono/stripping/help/license contracts and **106 unchanged original files** pass |
| Source ZIP | Git-selected working candidate exported with short-root UnityProject, no caches/builds, source-only label/commit and SHA256 receipt; ZIP integrity/all file digests verified. Delivery also verifies default committed-HEAD export |
| Selected Unity APIs | BuildPipeline.BuildPlayer/IsBuildTargetSupported, BuildSummary fields, NamedBuildTarget settings and PackageInfo.GetAllRegisteredPackages verified against 6000.3.26f1 reference source; this is not Editor compilation |
| Unity frontend/Editor build-helper/shader compilation | **Not executed — no Unity Editor installed** |
| Unity tests | **55 EditMode / 26 PlayMode supplied, not executed**; new player settings/legal texts and UI quit/recording-finalization cases |
| Actual Windows BuildPipeline build, real player ZIP and independent launch | **Not executed** |
| Player native window X/Alt+F4/save/recording exit, graphics/input/files/cameras/diagnostics/scale/hazards | **Not executed** |
| Remote GitHub Actions/source artifact publication | **Unverified** — current GitHub API request returns 403; workflow/source exporter supplied |

## Scope and evidence boundaries

The original 106 protected files remain byte-identical. Production portable
Engine/physics sources are unchanged from accepted M10; no dt/RNG/order, contacts,
collision guards, navigation, exposure, Hybrid default, GPU/visual or recording
algorithm change is used for M11. Only migration metadata, portable quit lifecycle
and distribution helpers are added there. Company/product identity remains
CrowdSim / CrowdSim Unity, preserving the persistent-data root; version is 0.11.0.
Existing M10 CPU/GPU improvements and the read-only optimization menu remain.

Synthetic binary/package fixtures verify validation and packaging logic. They are
**not executable Unity players**, license activation, Windows builds, native smoke
tests or proof that all frontend APIs compile. The Editor build command first
requires real BuildPipeline success, then inventories actual resolved-package legal
texts and validates its real files before creating a player ZIP. No fake player or
fixture ZIP is delivered as the application. Actual build settings/packaging still
need local verification using the pinned Editor and Windows Build Support.

The source exporter defaults to committed Git HEAD; --worktree is explicitly labeled
a candidate for cloud validation. GitHub artifact upload is configured after cloud
checks, but cannot be claimed published without run evidence. The normal immutable
GitHub commit ZIP remains the delivered source download. No release is merged into
main or published as a tested binary. Original reference/cloud tooling stays in the
full repository; the short-root source artifact includes UnityProject and migration
docs and requires no local .NET SDK for Editor/player use.

## Local acceptance

Follow [M11_LOCAL_TESTING.md](M11_LOCAL_TESTING.md), then report F01–F29 coverage in
[FEATURE_PARITY.md](FEATURE_PARITY.md). Test Editor and the standalone independently,
including normal/dirty/failed-save quit, active AVI finalization, examples/licenses,
legacy/custom-stage persistence, both hazards, views/diagnostics and scale. A forced
OS kill/crash or Editor toolbar Stop cannot promise the normal save/exit contract.
No extra OS/IL2CPP target or final complete-replacement acceptance is inferred.

## Reproduce available cloud checks

```bash
bash Migration/Tools/validate.sh
python3 Migration/Tools/package_unity_source.py --output /tmp/CrowdSim-UnityProject-M11.zip
```

The default source export needs the M11 commit at HEAD and a fresh output path.
For an uncommitted validation candidate use --worktree and do not describe that ZIP
as a committed release. No repeated CPU benchmark was needed: M11 changes no physics
or hot rendering paths. Historical evidence remains in
[M10 menu correction](M10_MENU_CORRECTION_VALIDATION.md), [M10 feature validation](M10_VALIDATION.md)
and the M10 performance reports; those are separate from this new full-suite run.
