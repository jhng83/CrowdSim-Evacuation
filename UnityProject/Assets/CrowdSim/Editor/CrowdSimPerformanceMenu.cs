using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CrowdSim.Unity.Editor
{
    /// <summary>Read-only Editor diagnostics. Never changes compilation mode or triggers a domain reload.</summary>
    public static class CrowdSimPerformanceMenu
    {
        [MenuItem("CrowdSim/Performance/Show Editor Optimization Status")]
        private static void ShowStatus()
        {
            Debug.Log("CrowdSim: Editor C# optimization is " + CompilationPipeline.codeOptimization +
                ". Status only; no setting changed and no recompilation requested. " +
                "Keep the current mode while testing M10; switching modes caused a native reload crash in the reported Editor session.");
        }
    }
}
