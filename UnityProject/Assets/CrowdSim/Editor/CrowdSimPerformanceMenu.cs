using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CrowdSim.Unity.Editor
{
    /// <summary>Explicit, reversible Editor compilation choices; never changed on project load.</summary>
    public static class CrowdSimPerformanceMenu
    {
        private const string ReleaseMenu = "CrowdSim/Performance/Use Release Code Optimization";
        private const string DebugMenu = "CrowdSim/Performance/Use Debug Code Optimization";

        [MenuItem(ReleaseMenu)]
        private static void UseRelease()
        {
            Debug.Log("CrowdSim: switching Editor code optimization to Release. Wait for recompilation before Play. " +
                "This is an Editor setting; use CrowdSim > Performance > Use Debug Code Optimization to restore script debugging.");
            CompilationPipeline.codeOptimization = CodeOptimization.Release;
        }

        [MenuItem(ReleaseMenu, true)]
        private static bool CanUseRelease() => CanChange() && CompilationPipeline.codeOptimization != CodeOptimization.Release;

        [MenuItem(DebugMenu)]
        private static void UseDebug()
        {
            Debug.Log("CrowdSim: restoring Debug code optimization and script debugging. Wait for recompilation before Play.");
            CompilationPipeline.codeOptimization = CodeOptimization.Debug;
        }

        [MenuItem(DebugMenu, true)]
        private static bool CanUseDebug() => CanChange() && CompilationPipeline.codeOptimization != CodeOptimization.Debug;

        private static bool CanChange() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
    }
}
