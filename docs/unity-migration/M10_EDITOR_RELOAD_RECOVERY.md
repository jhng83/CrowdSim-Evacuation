# M10 Editor reload crash — recovery and evidence

The user supplied an Editor.log from Unity 6000.3.26f1 after following the instruction
to switch C# optimization modes. **Those instructions are withdrawn. Do not retry
either old Release/Debug command, including as a way to restore the setting.**
This correction retains performance update 2 and replaces those commands with a
single read-only status command. It does not change the existing Editor mode.

## What the uploaded log establishes

- The recorded menu call is `CrowdSimPerformanceMenu.UseDebug`, with the message
  "restoring Debug code optimization and script debugging." The user intended the
  suggested Release action; the correction removes both commands regardless.
- Unity requests recompilation with debug information. The build reports
  `Tundra build success (8.69 seconds)`, with zero `error CS` messages in this log.
- Unity then prints `Reloading assemblies after forced synchronous recompile` and
  `Begin MonoManager ReloadAssembly`, followed by a native crash. The final stack
  starts in **mono-2.0-bdwgc / unload_thread_main / appdomain.c:3373**. No managed
  simulation stack accompanies it. This locates the crash in native assembly/domain
  reload, rather than identifying an inaccurate simulation result.
- The same log contains **149 distinct missing package paths**, all **260–320
  characters** long. The Collections IL support DLL path is 292 characters, and
  many URP/Shader Graph resource imports fail. This strongly suggests a Windows
  path-length import issue in the nested commit-named Downloads extraction.
- Burst/Unsafe errors also appear before the menu action. The log alone does not
  establish which package/runtime fault causes the native crash; a short clean
  import and local restart are needed. Do not claim a proven fix to Mono itself.

Log fingerprint: SHA256
`d35c9972f40c604361a5051018fdf96dde7749ac798cb46d0c49ba4b3e552ebb`.
Only this sanitized diagnosis is committed; the uploaded log, local paths and dump
are not added to the repository.

## Local recovery / acceptance

1. **Close the crashed Editor and Bug Reporter.** Keep the old extraction and crash
   files for now. Restart Unity Hub. Sending the listed report attachments to Unity
   is optional; this task does not submit them.
2. **Download this correction's ZIP.** Extract it separately, then copy its
   **UnityProject** folder so its final location is exactly:
   `C:\Unity\CrowdSimM10\UnityProject`.
   That folder must directly contain Assets, Packages and ProjectSettings.
   Avoid retaining one or two extra commit-named folders in the path. Use a fresh
   folder; do not copy old Library, Temp or UserSettings caches. Leave the old
   project and saved authored scenarios intact. Keep the rest of the downloaded
   repository for reference/cloud source; the Editor opens only UnityProject.
3. **Add that short UnityProject path to Hub**, using **6000.3.26f1** and the
   committed package pins. If registry authentication fails, sign in through Hub
   as described in M10_IMPORT_RECOVERY.md. Let initial package import finish; normal
   import compilation is expected. Do not change optimization mode or package
   versions, add replacement DLLs, or disable physics/guards to get through it.
4. **Check Console and the menu.** Expect the previous missing long package-path
   errors to clear after clean short-path resolution. Under CrowdSim → Performance,
   only **Show Editor Optimization Status** should exist. Clicking it should print
   the current mode and "no setting changed and no recompilation requested," with
   no mode-triggered reload. A Debug or Release readout is acceptable; keep it.
5. **Open Assets/CrowdSim/Scenes/Bootstrap.unity and Play.** Expect **PERFORMANCE
   UPDATE 2 · MENU CORRECTION**. Run the dense 800/32-table and open 1000 layouts,
   then continue M10_PERFORMANCE_2_LOCAL_TESTING.md, including both hazards,
   furniture movement, poses/colours, cameras/F4, stage and short recordings.
6. **If startup/import still crashes**, do not repeatedly toggle modes. Keep the new
   short-path Editor.log and crash.dmp and report the stage at which it fails. The
   current source removes the known menu trigger; successful restart and the
   native cause still require local evidence. The existing crash dump can support
   further diagnosis if the clean restart also fails.

## Change boundaries

The menu, runtime status text, startup label, static regression guard and handoff
documentation change. Production portable/physics sources, GPU/world rendering,
assets, recording, stage, package versions, original reference and main do not.
Prior CPU benchmark/state-hash evidence still applies to the identical portable
sources, checked against b127185. Rebuilt DLL hashes can differ with Git/build
metadata; the original measured DLL hash remains in the benchmark environment.
This iteration makes no new timing claim. Unity restart/import,
native crash absence, menu execution and local tests were not executed in cloud.
