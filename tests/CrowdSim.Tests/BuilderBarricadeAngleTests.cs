using CrowdSim.OpenGL.UI;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Free-angle input, per-asset memory and fine shortcut rotation.</summary>
public sealed class BuilderBarricadeAngleTests
{
    [Theory]
    [InlineData(15f, 15f)]
    [InlineData(37.5f, 37.5f)]
    [InlineData(68f, 68f)]
    [InlineData(-22.5f, 337.5f)]
    [InlineData(382.5f, 22.5f)]
    public void BarricadeAcceptsAnglesWithoutQuarterTurnSnapping(float input, float expected)
    {
        BuilderAssetSelection selection = new();
        selection.Select(BuilderAsset.Barricade);
        Assert.False(selection.UsesQuarterTurns);
        selection.SetRotation(input);
        Assert.Equal(expected, selection.RotationDegrees, 4);
    }

    [Fact]
    public void FineStepsAreReversibleAndWrapAround360()
    {
        BuilderAssetSelection selection = new();
        selection.Select(BuilderAsset.Barricade);
        selection.SetRotation(352.5f);
        selection.RotateStep();
        Assert.Equal(7.5f, selection.RotationDegrees);
        selection.RotateStep(reverse: true);
        Assert.Equal(352.5f, selection.RotationDegrees);
    }

    [Fact]
    public void SwitchingAssetsRetainsTheBarricadeAngleAndTableQuarterTurns()
    {
        BuilderAssetSelection selection = new();
        selection.Select(BuilderAsset.Barricade);
        selection.SetRotation(37.5f);
        selection.Select(BuilderAsset.RectangularTable);
        selection.RotateStep();
        Assert.Equal(90f, selection.RotationDegrees);
        selection.Select(BuilderAsset.Barricade);
        Assert.Equal(37.5f, selection.RotationDegrees);
        selection.Select(BuilderAsset.Stage);
        Assert.True(selection.UsesQuarterTurns);
        selection.SetRotation(68f);
        Assert.Equal(90f, selection.RotationDegrees);
    }
}
