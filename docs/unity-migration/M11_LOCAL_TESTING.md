# M11 — Windows standalone acceptance

M10 was accepted by the user at `c97d49c`. M11 supplies integrated quit/help,
Windows x64/Mono build/packaging, notices and source distribution. This is not
final player acceptance until the steps below pass locally. Cloud has no Unity
Editor or Windows player: no executable build or native test is claimed.
The v0.11.1 CPU correction also needs
[performance retesting](M11_PERFORMANCE_FIX_LOCAL_TESTING.md) before acceptance.

1. **Open the source project.** Use a fresh short path such as
   `C:\Unity\CrowdSim\UnityProject`, directly containing Assets, Packages and
   ProjectSettings. Use **Unity 6000.3.26f1**, URP **17.3.0**. Open
   **Assets/CrowdSim/Scenes/Bootstrap.unity**. Do not change Editor code optimization;
   CrowdSim → Performance still only reports its current mode.
2. **Editor smoke test.** Play: expect the M11 startup and v0.11.1, black readable
   text, familiar scenario entry and all M10 visuals/features. Open User guide and
   Credits and licenses: expect local bundled text files. Build a dirty scene and
   click Quit: Keep working retains it paused; Save and continue saves then stops
   Play. Quit at startup simply stops Play. Editor toolbar Stop/forced termination
   does not provide the normal authored-data prompt.
3. **Run supplied Unity tests.** Stop Play, open Window → General → Test Runner.
   Run EditMode and PlayMode, keeping Game view visible for recording. Current
   counts/results are in VALIDATION.md. New tests exercise quit cancellation/save
   and recording finalization via a callback, without actually closing the Editor.
   Preserve test XML and any Console errors. These tests were not run in cloud.
4. **Build the player.** Resolve authentication/import/Burst errors first using
   [build recovery](M11_BUILD_RECOVERY.md), then run CrowdSim → Build → Check
   Package Readiness (metadata check only, not all DLLs/imports). Install Windows Build Support for 6000.3.26f1 through Hub.
   In File → Build Profiles select **Windows x64**. Keep committed Mono backend,
   .NET Standard 2.1, disabled managed stripping and product identity. Stop Play and
   choose **CrowdSim → Build → Windows x64 Player and ZIP**. The command validates
   settings; it never changes Editor optimization or switches the target for you.
   Expect a new `UnityProject/Builds/Windows-x64/<run>/` folder containing **Player/**
   and **CrowdSim-Windows-x64-0.11.1.zip**, with a successful build/ZIP Console message.
   A failed build or incomplete output must not yield a purported release ZIP.
   Keep the build log/receipt; previous builds are never overwritten.
5. **Run independently.** Extract the entire player ZIP into another short folder,
   close Unity and launch **CrowdSim.exe**. Keep UnityPlayer.dll, CrowdSim_Data and
   MonoBleedingEdge alongside it. Expect startup without Unity Editor or the
   original .NET executable/SDK. README.txt, build-receipt.json, release-files.sha256
   and Notices/package-inventory.json must accompany the files. Check the receipt
   says Succeeded, Windows64/Mono, development false, correct version/Editor.
6. **Authoring/files/stage.** In the player place every asset, exits and people;
   test room bounds, rotation/snapping, undo and finalisation guards. Resize the
   concert stage, save/load/replay it, including invalid overlaps/boundary sizes.
   Files → Open save folder must open the shown user directory. Confirm an M10
   saved scene still appears (company/product identity is unchanged). Import the
   bundled v1/v2/v3/205-person files; save copies. Custom stages remain schema v5,
   explicitly rejected by the original native app; ordinary saves remain v4.
7. **Both complete hazard workflows.** Run Explosion and Toxic Fume from authoring
   through event/compute/preview/release, pause/resume, selected-agent details,
   diagnostics and frame-zero replay. Exercise all prior controls/overlays, F4
   hidden-inspector retention, zoom/rotation/Follow/POV, poses/colours/picking and
   GPU/compatibility toggling. Perimeter walls remain physically solid and visually
   transparent; runtime humans stay volumetric at low zoom. Hybrid stays optional.
8. **Record and quit safely.** Record short AVIs in both hazards, resize the window,
   show/hide the inspector and play the saved output. Start another recording and
   quit via the application button/window X/Alt+F4: expect the active recording to
   drain/finalize before normal exit. With a dirty scene, test Keep working, Save
   and continue, overwrite cancellation/confirmation, and Discard. Failed saving
   must keep the player open. Reopen it and verify saved authored starts/AVI output.
   Forced OS termination/crashes cannot guarantee normal shutdown finalization.
9. **Scale and diagnostics.** Generate dense 800/32 tables and open 1000 in Debug;
   use equal seeds/events/ticks/zoom, 1× playback and recording off. Record engine,
   response/SFM/collision/world CPU and frame p50/p95, GPU backend and hardware.
   Compare with accepted M10; standalone performance is unmeasured in cloud and
   has no promised FPS. Note any CPU-bound case rather than changing dt/guards.
10. **Acceptance report.** Send the source commit, build receipt, OS/CPU/GPU,
    Unity tests, build/player logs, tested parity IDs F01–F29, before/after timings
    and any failures. On Windows Player.log is normally under
    `%USERPROFILE%\AppData\LocalLow\CrowdSim\CrowdSim Unity\Player.log`.
    The full feature checklist is FEATURE_PARITY.md. Mark M11/complete replacement
    accepted only after the agreed standalone workflows pass; fix failures first.

Batch equivalent (licensed Editor, Windows support installed, fresh output):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe' `
  -batchmode -quit -projectPath 'C:\Unity\CrowdSim\UnityProject' -buildTarget Win64 `
  -executeMethod CrowdSim.Unity.Editor.PlayerBuild.BuildFromCommandLine `
  -crowdsimOutput 'C:\Unity\CrowdSimBuild-M11' -logFile 'C:\Unity\CrowdSimBuild-M11.log'
```

The source ZIP artifact is source only. There is no prebuilt .exe until an actual
licensed Editor executes this build. Other OS targets and IL2CPP are not accepted
by this Windows Mono build path; adding them requires explicit target acceptance.
