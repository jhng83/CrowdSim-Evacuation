using CrowdSim.Core.Model;
using CrowdSim.OpenGL.UI;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Tests the real picker state, linked without any window/OpenGL dependency.</summary>
public sealed class BuilderAssetSelectionTests
{
    [Fact]
    public void PickerIncludesEveryFurnitureKindExactlyOnce()
    {
        BuilderAssetSelection selection = new();
        List<FurnitureKind> kinds = new();
        foreach (BuilderAsset asset in BuilderAssetSelection.Options)
        {
            selection.Select(asset);
            Assert.False(string.IsNullOrWhiteSpace(BuilderAssetSelection.Label(asset, 1.2f)));
            Assert.False(string.IsNullOrWhiteSpace(selection.Hint));
            if (selection.Furniture is FurnitureKind kind) kinds.Add(kind);
        }
        Assert.Equal(Enum.GetValues<FurnitureKind>().OrderBy(k => k).ToArray(), kinds.OrderBy(k => k).ToArray());
        Assert.Equal(8, BuilderAssetSelection.Options.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NonDirectionalBlocksDoNotOfferOrAccumulateRotation(int choice)
    {
        BuilderAssetSelection selection = new();
        selection.Select((BuilderAsset)choice);
        Assert.False(selection.SupportsRotation);
        selection.RotateQuarterTurn();
        Assert.Equal(0f, selection.RotationDegrees);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void TablesAndStageRotateByQuarterTurnsAndWrapAt360(int choice)
    {
        BuilderAssetSelection selection = new();
        selection.Select((BuilderAsset)choice);
        Assert.True(selection.UsesQuarterTurns);
        foreach (float expected in new[] { 90f, 180f, 270f, 0f })
        {
            selection.RotateQuarterTurn();
            Assert.Equal(expected, selection.RotationDegrees);
        }
        selection.SetRotation(68f);
        Assert.Equal(90f, selection.RotationDegrees);
        selection.SetRotation(-90f);
        Assert.Equal(270f, selection.RotationDegrees);
    }

    [Fact]
    public void EveryAssetRemembersItsOwnFacingWhileSwitchingTools()
    {
        BuilderAssetSelection selection = new();
        selection.Select(BuilderAsset.Barricade);
        selection.RotateQuarterTurn();
        selection.Select(BuilderAsset.RectangularTable);
        Assert.Equal(0f, selection.RotationDegrees);
        selection.SetRotation(180f);
        selection.Select(BuilderAsset.SingleChair);
        selection.SetRotation(37f);
        Assert.False(selection.UsesQuarterTurns);
        selection.Select(BuilderAsset.Barricade);
        Assert.Equal(90f, selection.RotationDegrees);
        selection.Select(BuilderAsset.SingleChair);
        Assert.Equal(37f, selection.RotationDegrees);
        selection.Select(BuilderAsset.RectangularTable);
        Assert.Equal(180f, selection.RotationDegrees);
    }

    [Fact]
    public void NewScenarioClearsSelectionsAndRestoresDefaultSnap()
    {
        BuilderAssetSelection selection = new();
        selection.Select(BuilderAsset.Barricade);
        selection.RotateQuarterTurn();
        selection.SnapBarricadeEnds = false;
        selection.Reset();
        Assert.Equal(BuilderAsset.SquareBlock, selection.Selected);
        Assert.True(selection.SnapBarricadeEnds);
        selection.Select(BuilderAsset.Barricade);
        Assert.Equal(0f, selection.RotationDegrees);
    }

    [Fact]
    public void InvalidSelectionOrRotationCannotPoisonThePreview()
    {
        BuilderAssetSelection selection = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.Select((BuilderAsset)999));
        selection.Select(BuilderAsset.Barricade);
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.SetRotation(float.NaN));
        Assert.Equal(0f, selection.RotationDegrees);
    }
}
