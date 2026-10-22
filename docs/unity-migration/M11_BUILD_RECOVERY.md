# M11 build recovery — authentication, Burst and package import

The user reports v0.11.1 latency solved. Windows BuildPipeline then fails before
player packaging with the following independent evidence:

- Input System 1.14.2 download and Package Manager search explicitly lack valid
  authentication. The error says to sign in using Hub. Code cannot refresh the
  user's Unity account session from cloud.
- Burst's native-plugin build step cannot load
  `System.Runtime.CompilerServices.Unsafe, Version=6.0.3.0`. This is the concrete
  fatal build step in the supplied trace. Incomplete/stale package payloads or
  path/import problems are possible; the supplied messages do not prove which.
- URP/Core reports host-type errors for AutodeskInteractive.shadergraph and
  TraceVirtualOffset.urtshader. Restore/authentication/import must be healthy
  before treating each message as a distinct application bug. Their complete
  cause and resolution are not established by this Console excerpt.
- PlayerBuild.cs appears in the stack because it calls Unity's build pipeline.
  The final no-ZIP message is deliberate: failed builds must not be packaged as
  releases. The CS0618 PreventDefault warnings are not the shown fatal failure.

## Recovery steps on your computer

1. **Close the Editor. Sign in to Unity Hub.** If Hub already shows you signed
   in, sign out and back in, then restart Hub and open the project through Hub.
   Retry package resolution if offered. Do not retry the player build while
   Input System or Package Manager search still reports missing authentication.
2. **Use a fresh short-path extraction if import/DLL failures remain.** Keep the
   old project and user saves intact. Extract this correction separately, then
   place its UnityProject folder so the final path is exactly something like
   `C:\Unity\CrowdSim\UnityProject`. It must directly contain Assets, Packages
   and ProjectSettings, without repeated commit-named parent folders. Do not copy
   old Library, Temp or UserSettings caches. New extraction rebuilds the local
   cache without deleting the old project or per-user scenarios/recordings.
3. **Open through Hub using Unity 6000.3.26f1**, with Windows Build Support. Allow
   all pinned package downloads, imports and compilation to finish. Keep URP
   17.3.0 and the committed package pins, including Input System 1.14.2. Do not
   add a standalone Unsafe DLL, upgrade/remove packages, disable Burst or change
   Editor optimization as a workaround. Burst is also a URP dependency even
   though CrowdSim's simulation CPU optimizations do not use Burst.
4. **Run CrowdSim → Build → Check Package Readiness.** This new read-only menu
   checks the lock graph against registered package names/versions/errors and
   readable, matching package.json files. Built-in engine modules may be virtual.
   A failure identifies metadata/resolution blockers and recovery steps. A pass
   establishes only those checks: it cannot authenticate the Hub session, inspect
   every DLL dependency or prove shader import/Burst/Windows execution succeeds.
   You must also see package authentication/import errors clear in Console and
   Package Manager before building. The menu does not repair/download anything.
5. **Build again after recovery.** Stop Play, select Windows x64 in Build Profiles,
   then CrowdSim → Build → Windows x64 Player and ZIP. The build now runs package
   preflight before creating output or invoking BuildPipeline. Expect a fresh
   output directory under UnityProject/Builds/Windows-x64 containing Player and
   **CrowdSim-Windows-x64-0.11.1.zip** only after successful real build/validation.
   Previous builds are not overwritten. Runtime/portable version remains 0.11.1;
   this correction changes Editor diagnostics and documentation only.
6. **If authenticated clean short-path recovery still fails**, preserve the new
   Editor.log and upm.log immediately after one attempt. On Windows these are
   normally `%LOCALAPPDATA%\Unity\Editor\Editor.log` and
   `%LOCALAPPDATA%\Unity\Editor\upm.log`. Report the first remaining failure,
   exact project path length, Unity version and whether package resolution now
   completes. These distinguish an enduring package/compiler/import fault from
   the original explicit authentication failure. No global-cache deletion or
   registry/credential-file edits are part of this correction.
7. **After a successful build, test the application.** Extract the complete player
   ZIP elsewhere, close Unity and launch CrowdSim.exe. Open
   **Assets/CrowdSim/Scenes/Bootstrap.unity** for Editor testing. Follow
   [M11 local acceptance](M11_LOCAL_TESTING.md), including both hazards, files,
   cameras, stage, recording and guarded quit. Keep the accepted v0.11.1 latency
   behavior. Final M11 acceptance stays pending until the player works.

## Cloud evidence and limits

Eight focused tests execute the actual production metadata checker against
temporary package fixtures, covering absence, resolver errors, pins, installed
metadata, malformed files, embedded/virtual packages and read-only behavior.
These fixtures are not real Unity packages. Full cloud results are in
[VALIDATION.md](VALIDATION.md). No Unity Editor is installed in cloud: Hub sign-in,
actual resolution/import, preflight menu wiring, Burst/native plugin generation,
URP shaders, Windows build/ZIP and player execution require your local verification.
Unity 6000.3.26f1 reference source confirms the PackageInfo metadata/error API;
that is not frontend compilation or proof that package files are repaired.
