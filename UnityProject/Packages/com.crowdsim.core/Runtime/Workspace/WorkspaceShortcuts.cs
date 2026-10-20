using System;

namespace CrowdSim.Workspace
{
    public enum WorkspaceShortcut { None, ToggleInspector, Undo, ToggleRunning, CancelTool, RotateAsset }
    /// <summary>Focus/modifier policy shared by the UI adapter and headless tests.</summary>
    public static class WorkspaceShortcuts
    {
        public static WorkspaceShortcut Resolve(string key, bool control, bool alt, bool editing)
        {
            if (alt || editing) return WorkspaceShortcut.None;
            if (key == "F4") return WorkspaceShortcut.ToggleInspector;
            if (control && key == "Z") return WorkspaceShortcut.Undo;
            if (control) return WorkspaceShortcut.None;
            if (key == "Space") return WorkspaceShortcut.ToggleRunning;
            if (key == "R") return WorkspaceShortcut.RotateAsset;
            if (key == "Escape") return WorkspaceShortcut.CancelTool;
            return WorkspaceShortcut.None;
        }
    }
}
