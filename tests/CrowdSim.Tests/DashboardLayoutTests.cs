using CrowdSim.OpenGL.UI;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Check panel allocation and inspector continuity independently of an OpenGL context.</summary>
public sealed class DashboardLayoutTests
{
    [Theory]
    [InlineData(320f)]
    [InlineData(640f)]
    [InlineData(1000f)]
    [InlineData(1280f)]
    [InlineData(1600f)]
    [InlineData(2560f)]
    public void BothPanelStatesFitWithoutOverflowAndHidingExpandsTheViewport(float width)
    {
        DashboardLayout shown = DashboardLayout.Calculate(width, true);
        DashboardLayout hidden = DashboardLayout.Calculate(width, false);
        Assert.True(shown.ControlsWidth > 0f && shown.ViewportWidth > 0f && shown.InspectorWidth > 0f);
        Assert.Equal(width, shown.ControlsWidth + shown.ViewportWidth + shown.InspectorWidth + 2f * shown.Gap, 3);
        Assert.Equal(width, hidden.ControlsWidth + hidden.ViewportWidth + hidden.Gap, 3);
        Assert.Equal(shown.ControlsWidth, hidden.ControlsWidth);
        Assert.Equal(0f, hidden.InspectorWidth);
        Assert.Equal(shown.InspectorWidth + shown.Gap, hidden.ViewportWidth - shown.ViewportWidth, 3);
    }

    [Fact]
    public void OversizedSpacingCannotConsumeTheViewportInATinyWindow()
    {
        DashboardLayout layout = DashboardLayout.Calculate(100f, true, 300f);
        Assert.True(layout.ViewportWidth > 0f);
        Assert.Equal(100f, layout.ControlsWidth + layout.ViewportWidth + layout.InspectorWidth + 2f * layout.Gap, 3);
    }

    [Fact]
    public void HideShowRetainsTheDebugDestinationAndAgentSelectionDoesNotForceItOpen()
    {
        InspectorPanelState state = new();
        state.SelectTab(InspectorTab.Debug);
        state.ToggleVisibility();
        state.ToggleVisibility();
        Assert.Equal(InspectorTab.Debug, state.ActiveTab);
        Assert.True(state.IsVisible);

        state.ToggleVisibility();
        state.SelectTab(InspectorTab.Agent);
        Assert.False(state.IsVisible);
        state.Show(InspectorTab.Agent);
        Assert.True(state.IsVisible);
        Assert.Equal(InspectorTab.Agent, state.ActiveTab);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidDimensionsCannotReachImGui(float width)
        => Assert.Throws<ArgumentOutOfRangeException>(() => DashboardLayout.Calculate(width, true));
}
