using System;

namespace CrowdSim.OpenGL.UI
{
    /// <summary>Inspector destinations remain selected while the panel is hidden.</summary>
    internal enum InspectorTab { Overview, Hazard, Agent, Debug }

    /// <summary>
    /// Presentation-only inspector state. Hiding the panel does not clear the selected agent,
    /// debug overlay flags, hazard configuration, active tab, or running simulation.
    /// </summary>
    internal sealed class InspectorPanelState
    {
        public bool IsVisible { get; private set; } = true;
        public InspectorTab ActiveTab { get; private set; } = InspectorTab.Overview;

        public void ToggleVisibility() => IsVisible = !IsVisible;

        public void SelectTab(InspectorTab tab)
        {
            if (!Enum.IsDefined(typeof(InspectorTab), tab)) throw new ArgumentOutOfRangeException(nameof(tab));
            ActiveTab = tab;
        }

        /// <summary>Explicitly open a destination, for example from the selected-agent toolbar.</summary>
        public void Show(InspectorTab tab)
        {
            SelectTab(tab);
            IsVisible = true;
        }
    }

}
