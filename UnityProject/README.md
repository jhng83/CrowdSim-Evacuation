# CrowdSim Unity — M11 standalone application

M1–M10 are accepted by the user. M11 integrates standalone quit, help/notices,
Windows player build/packaging and source downloads. Final M11 acceptance needs
local Editor and Windows player testing; no prebuilt executable is claimed.

**v0.11.1 fixes repeated dense-scene obstacle queries** while preserving exact
contact order, movement rules and every SFM contribution. Debug's performance card
now starts with build version and read-only optimization status. Follow
[CPU fix retesting](../docs/unity-migration/M11_PERFORMANCE_FIX_LOCAL_TESTING.md).

Open **UnityProject/** (Assets, Packages, ProjectSettings) with **Unity 6000.3.26f1**,
URP **17.3.0**, then **Assets/CrowdSim/Scenes/Bootstrap.unity**. Use a short path,
for example `C:\Unity\CrowdSim\UnityProject`. Startup shows **M11** and version
**0.11.1**. First import requires Unity registry access; sign in through Hub if
requested. CrowdSim → Performance remains read-only; keep Editor optimization mode.

Package authentication/Burst/URP build failures: follow
[build recovery](../docs/unity-migration/M11_BUILD_RECOVERY.md). Close Unity, sign
in through Hub and allow clean short-path package restoration. **CrowdSim → Build
→ Check Package Readiness** is read-only; its pass is not proof every DLL/import
or the real build will succeed. The user reports v0.11.1 latency solved.

## Build a standalone Windows app

1. Install Windows Build Support for the pinned Editor via Hub; select **Windows
   x64** in File → Build Profiles. Keep committed **Mono**, **.NET Standard 2.1**,
   **Disabled managed stripping**, and CrowdSim / CrowdSim Unity product identity.
2. Stop Play and choose **CrowdSim → Build → Windows x64 Player and ZIP**.
3. On success a fresh `Builds/Windows-x64/<run>/` contains **Player/** and
   **CrowdSim-Windows-x64-0.11.1.zip**. The build command validates real Unity output,
   preserves previous builds, copies package notices, records the build receipt and
   adds SHA256 file checksums. It never changes Editor optimization or active target.
4. Extract the entire ZIP separately, close Unity and run **CrowdSim.exe**. Keep
   UnityPlayer.dll, CrowdSim_Data and MonoBleedingEdge together. No original .NET
   executable, Unity Editor or separate .NET SDK is needed by the player.

[Full local tests and batch command](../docs/unity-migration/M11_LOCAL_TESTING.md).
A licensed Editor must actually execute the build. Cloud has no Editor, so Windows
build, executable launch, real ZIP output and native lifecycle remain unexecuted.
Other OS targets/IL2CPP are not covered by this Windows Mono acceptance path.

## Integrated workflows

All accepted M10 tools remain: both hazards, authored files/replay, every asset and
adjustable concert stage, black text/off-white outlined pillars/grid, volumetric
poses/cameras, GPU instancing with compatibility fallback, diagnostics/comparisons
and full-window AVI recording. Physics/dt/RNG/order/guards are unchanged.

Quit at startup/workspace and normal player window close use the existing authored
save/discard/cancel flow. Save failure keeps the app open; approved exit finalizes
recording before closing. Keep working leaves the simulation paused. In Editor,
Quit stops Play. Forced termination/toolbar Stop cannot guarantee a save prompt.
Startup opens bundled user guide and credits/licenses; Files can open the save
folder. Company/product identity is unchanged, preserving accepted M10 user data.
[User guide](Assets/StreamingAssets/Documentation/USER_GUIDE.txt) and
[credits/licenses](Assets/StreamingAssets/Documentation/THIRD_PARTY_NOTICES.txt)
are copied into every player. Full font OFL/JSON MIT texts are included; the build
copies available license/notice texts from actual resolved Unity packages.

Custom-size stages still use schema v5, explicitly rejected by the original app;
v1–v4 imports/default v4 exports remain compatible. AVI remains MJPEG 1280×720,
20 fps, no audio, bounded queue, 1 GiB/100,000-frame limits. Readback can stall;
Editor recording needs active Game view. No promised FPS or GPU physics.

## Validation and downloads

Cloud passes **404 original / 415 portable / 280 migration** cases, unchanged
six-case captures/oracle, files/AVI decode and source-package checks. Player ZIP
regressions use synthetic headers/layouts; they do not prove a real Unity build.
**55 EditMode / 26 PlayMode** cases are supplied, **not executed in cloud**.
[Validation](../docs/unity-migration/VALIDATION.md),
[feature parity](../docs/unity-migration/FEATURE_PARITY.md),
[status](../docs/unity-migration/STATUS.md). Original .NET source/tests/scenarios,
framework/dependency pins and main remain unchanged.

The GitHub workflow exports **CrowdSim-UnityProject-M11.zip** as a source artifact
with a short-root UnityProject folder and SOURCE_RECEIPT.json commit/file hashes.
Artifact publication is unverified with current API access. The commit's normal
GitHub source ZIP is also usable: copy its UnityProject directly to a short path.
Neither source ZIP is a player executable. Original/reference/cloud tools remain
available in the [full repository](https://github.com/jhng83/CrowdSim-Evacuation).
