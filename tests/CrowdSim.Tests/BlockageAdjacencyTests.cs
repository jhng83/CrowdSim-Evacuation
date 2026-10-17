using System.Numerics;
using System.Text.Json;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Xunit;

namespace CrowdSim.Tests;

/// <summary>Placement-grid seams, float tolerance and preview/commit consistency.</summary>
public sealed class BlockageAdjacencyTests
{
    private static Vector2 GridCenter(Simulation sim, int x, int y, int cells)
    {
        float t = sim.Environment.BoundaryThickness, step = sim.Config.Grid.CellSizeMeters;
        return new Vector2(t + (x + cells * 0.5f) * step, t + (y + cells * 0.5f) * step);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void TenBlocksCanTouchWithoutAnyGap(int cells, bool vertical)
    {
        Simulation sim = new(agentCount: 0);
        Wall? previous = null;
        for (int i = 0; i < 10; i++)
        {
            Vector2 click = GridCenter(sim, 3 + (vertical ? 0 : i * cells),
                3 + (vertical ? i * cells : 0), cells);
            Wall preview = sim.Environment.GetBlockagePlacementBounds(click.X, click.Y, cells);
            Assert.True(sim.Environment.CanPlaceBlockageFromClick(click.X, click.Y, cells));
            Assert.True(sim.Environment.AddBlockageFromClick(click.X, click.Y, cells));
            Wall placed = sim.Environment.Blockages[^1];
            Assert.Equal(preview, placed);
            if (previous is not null)
                Assert.Equal(vertical ? previous.Y2 : previous.X2, vertical ? placed.Y1 : placed.X1);
            previous = placed;
        }
        Assert.Equal(10, sim.Environment.Blockages.Count);
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, true)]
    public void MixedSizesShareTheSameGridEdges(int firstCells, int secondCells, bool vertical)
    {
        Simulation sim = new(agentCount: 0);
        Vector2 first = GridCenter(sim, 4, 4, firstCells);
        Vector2 second = GridCenter(sim, 4 + (vertical ? 0 : firstCells),
            4 + (vertical ? firstCells : 0), secondCells);
        Assert.True(sim.Environment.AddBlockageFromClick(first.X, first.Y, firstCells));
        Assert.True(sim.Environment.AddBlockageFromClick(second.X, second.Y, secondCells));
        Wall a = sim.Environment.Blockages[0], b = sim.Environment.Blockages[1];
        Assert.Equal(vertical ? a.Y2 : a.X2, vertical ? b.Y1 : b.X1);
        Assert.Equal(vertical ? a.X1 : a.Y1, vertical ? b.X1 : b.Y1);
    }

    [Fact]
    public void CornerContactIsAllowedButDuplicatePlacementIsRejected()
    {
        Simulation sim = new(agentCount: 0);
        Vector2 first = GridCenter(sim, 4, 4, 1), diagonal = GridCenter(sim, 5, 5, 1);
        Assert.True(sim.Environment.AddBlockageFromClick(first.X, first.Y));
        Assert.True(sim.Environment.AddBlockageFromClick(diagonal.X, diagonal.Y));
        int version = sim.Environment.WallLayoutVersion;
        Assert.False(sim.Environment.CanPlaceBlockageFromClick(first.X, first.Y));
        Assert.False(sim.Environment.AddBlockageFromClick(first.X, first.Y));
        Assert.Equal(version, sim.Environment.WallLayoutVersion);
        Assert.Equal(2, sim.Environment.Blockages.Count);
    }

    [Fact]
    public void AFloatRoundingSliverIsNotAnOverlapButOneMillimetreIs()
    {
        Simulation sim = new(agentCount: 0);
        sim.Environment.Blockages.Add(new Wall(10f, 10f, 11.2f, 11.2f));
        sim.Environment.RebuildWalls();
        Assert.False(sim.Environment.WallOverlapsAny(new Wall(11.2f - 0.000001f, 10f, 12.4f, 11.2f)));
        Assert.True(sim.Environment.WallOverlapsAny(new Wall(11.2f - 0.001f, 10f, 12.4f, 11.2f)));
    }

    [Fact]
    public void BlockCanTouchBoundaryWithoutProtrudingOutside()
    {
        Simulation sim = new(agentCount: 0);
        Vector2 click = GridCenter(sim, 0, 4, 1);
        Assert.True(sim.Environment.AddBlockageFromClick(click.X, click.Y));
        Assert.Equal(sim.Environment.BoundaryThickness, sim.Environment.Blockages[0].X1);
        Vector2 outside = GridCenter(sim, -1, 4, 1);
        Assert.False(sim.Environment.AddBlockageFromClick(outside.X, outside.Y));
    }

    [Fact]
    public void JoinedBlockSeamDoesNotBecomeAWalkableCrack()
    {
        Simulation sim = new(agentCount: 0);
        Vector2 a = GridCenter(sim, 4, 4, 1), b = GridCenter(sim, 5, 4, 1);
        Assert.True(sim.Environment.AddBlockageFromClick(a.X, a.Y));
        Assert.True(sim.Environment.AddBlockageFromClick(b.X, b.Y));
        sim.BuildWallGrid();
        Wall block = sim.Environment.Blockages[0];
        Assert.False(sim.HasTraversablePath(new Vector2(block.X2, block.Y1 - 1f),
            new Vector2(block.X2, block.Y2 + 1f), sim.Config.Agent.Radius));
    }

    [Fact]
    public void NonFiniteClicksAreRejectedWithoutMutatingTheScene()
    {
        Simulation sim = new(agentCount: 0);
        int version = sim.Environment.WallLayoutVersion;
        Assert.False(sim.Environment.CanPlaceBlockageFromClick(float.NaN, 10f));
        Assert.False(sim.Environment.AddBlockageFromClick(10f, float.PositiveInfinity));
        Assert.False(sim.Environment.AddBlueWallFromClick(float.NegativeInfinity, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.Environment.GetBlockagePlacementBounds(float.NaN, 0f));
        Assert.Equal(version, sim.Environment.WallLayoutVersion);
        Assert.Empty(sim.Environment.Blockages);
    }

    [Fact]
    public void JoinedMixedBlocksSurviveSaveLoadWithoutBeingResnapped()
    {
        Simulation sim = new(agentCount: 0);
        Vector2 a = GridCenter(sim, 4, 4, 1), b = GridCenter(sim, 5, 4, 2);
        Assert.True(sim.Environment.AddBlockageFromClick(a.X, a.Y));
        Assert.True(sim.Environment.AddBlockageFromClick(b.X, b.Y, 2));
        ScenarioDefinition saved = ScenarioDefinition.Capture(sim, 0, "joined-blocks");
        ScenarioDefinition loaded = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(saved))!;
        Simulation restored = new(agentCount: 0);
        loaded.ApplyLayout(restored);
        Assert.Equal(sim.Environment.Blockages.ToArray(), restored.Environment.Blockages.ToArray());
        Assert.Equal(restored.Environment.Blockages[0].X2, restored.Environment.Blockages[1].X1);
    }

    [Fact]
    public void AdjacentLegacyBlueTileUsesTheSameEdgeGrid()
    {
        Simulation sim = new(agentCount: 0);
        Vector2 a = GridCenter(sim, 4, 4, 1), b = GridCenter(sim, 5, 4, 2);
        Assert.True(sim.Environment.AddBlockageFromClick(a.X, a.Y));
        Assert.True(sim.Environment.AddBlueWallFromClick(b.X, b.Y));
        Assert.Equal(sim.Environment.Blockages[0].X2, sim.Environment.BlueWalls[0].X1);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NewBlockCanMeetAnOldHalfCellShiftedBlockWithoutMovingIt(int cells)
    {
        Simulation sim = new(agentCount: 0);
        Wall legacy = new(10.8f, 10.8f, 13.2f, 13.2f);
        sim.Environment.Blockages.Add(legacy);
        sim.Environment.RebuildWalls();
        float size = cells * sim.Config.Grid.CellSizeMeters;
        Vector2 click = new(legacy.X2 + size * 0.5f, legacy.Y1 + size * 0.5f);
        Assert.True(sim.Environment.AddBlockageFromClick(click.X, click.Y, cells));
        Assert.Same(legacy, sim.Environment.Blockages[0]);
        Assert.Equal(legacy.X2, sim.Environment.Blockages[1].X1);
        Assert.Equal(legacy.Y1, sim.Environment.Blockages[1].Y1, 4);
    }

}
