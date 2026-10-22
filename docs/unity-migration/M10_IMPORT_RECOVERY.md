# M10 local import recovery

The reported log identifies two independent blockers:

- **Source:** recording line 150 used `Unity.Collections` inside
  `CrowdSim.Unity.Workspace`, causing CS0234. The corrected download uses
  `global::Unity.Collections.NativeArray<byte>.Copy(...)`. No package or simulation
  changes are needed for this correction.
- **Local packages:** the Input System download was rejected for missing
  authentication. Collections IL support and Unsafe assembly loads also failed,
  followed by Burst and URP importer errors. Resolve the explicit authentication
  failure and reimport before treating each later message as a separate source bug.
  The complete cause of those later errors is not established by the log alone.

1. **Close the Unity Editor. Sign in to Unity Hub.** If Hub already shows you signed
   in, sign out and back in to refresh the session. Restart Hub and launch the
   project through Hub after signing in. This responds to the exact package error:
   "You are not signed in. Please sign in using the Hub and retry."
2. **Download the corrected commit ZIP and extract into a fresh, short path**, for
   example `C:\Unity\CrowdSim-M10\`. Avoid the nested long commit-named Downloads
   folders shown in the error. Keep the old extraction intact; the new folder
   should have no previous `UnityProject\Library` cache. Save any unsaved authored
   work in the old project before closing it. Do not copy Library/Temp from it.
3. **Add the extracted UnityProject folder to Hub**: the folder containing Assets,
   Packages and ProjectSettings. Use **Unity 6000.3.26f1**, with committed URP 17.3.0
   and other package pins. Allow package downloads, asset import and compilation
   to finish. If the resolver offers Retry after sign-in, retry it. Do not install
   replacement DLLs or upgrade packages to suppress these errors.
4. **Check the Console.** Expect package resolution to complete and no recording
   CS0234 error. If the authenticated Input System download still fails, copy the
   new resolver message; Play Mode cannot be accepted until packages resolve.
   If Collections/Unsafe/Burst or URP host-type errors remain after clean resolution,
   copy the earliest remaining error and the Editor log for investigation. Their
   resolution has not been verified in cloud.
5. **Open `Assets/CrowdSim/Scenes/Bootstrap.unity` and press Play.** Expect the
   PERFORMANCE UPDATE startup, then the workspace with Recording and Debug controls.
   Finalise a small scene; start/stop a short recording and play the saved AVI.
   Then continue the [performance checklist](M10_PERFORMANCE_LOCAL_TESTING.md),
   including GPU rendering, your original furnished scene and the generated dense
   800-person/32-table layout. In Debug, expand Reproducible crowd comparisons and
   choose **New dense 800-person layout (32 tables)**; it is generated, not a bundled
   JSON file. Files → Save As can save it as `dense_800_32_tables.json`.

Cloud reproduced the exact CS0234 in a reduced compiler probe using the original
production statement; its corrected statement compiled as C#9/Standard2.1 against
minimal diagnostic API shims. That establishes the namespace fix, not Unity API
compatibility or frontend compilation. Actual Editor package authentication/import,
shader/GPU execution, Play Mode, recording and Unity tests still require local
verification. M10 acceptance remains pending; do not advance to M11 yet.
