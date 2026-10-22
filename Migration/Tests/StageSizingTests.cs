using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class StageSizingTests
{
    private static WorkspaceSession Stage(float length = 8, float width = 4, float angle = 0)
    {
        WorkspaceSession s = new(); s.SelectAsset(BuilderAsset.Stage); s.SetAssetRotation(angle);
        Assert.True(s.SetStageSize(length, width)); return s;
    }

    [Theory]
    [InlineData(0, 8, 4)] [InlineData(90, 4, 8)] [InlineData(180, 8, 4)] [InlineData(270, 4, 8)]
    public void ExactRotatedFootprintMatchesPreviewPhysicsAndConcertGeometry(float angle, float worldX, float worldZ)
    {
        var s = Stage(angle: angle);
        var preview = s.Preview(PlacementTool.Asset, new Vector2(20, 15), 0);
        Assert.True(preview.Valid); Assert.True(s.Place(PlacementTool.Asset, new Vector2(20, 15), 0));
        ViewBody deck = Assert.Single(s.Frame.Bodies, b => b.Kind == ViewKind.Stage);
        Assert.Equal(new Vector3(worldX, .8f, worldZ), deck.Size);
        Assert.Contains(preview.Bodies, b => b.Kind == ViewKind.Stage && b.Size == deck.Size);
        Assert.Contains(s.Frame.Bodies, b => b.Id.Contains("-led-screen"));
        Assert.Contains(s.Frame.Bodies, b => b.Id.Contains("-speaker-"));
        Assert.Contains(s.Frame.Bodies, b => b.Id.Contains("-truss-"));
        ScenarioDefinition definition = ScenarioJson.Deserialize(s.ExportAuthoredJson("Custom"));
        Simulation restored = new(config: definition.CreateConfig(), randomSeed: definition.RandomSeed); definition.ApplyLayout(restored);
        Wall physical = Assert.Single(restored.Environment.Furniture).StaticObstacle!;
        Assert.Equal(worldX, physical.X2 - physical.X1); Assert.Equal(worldZ, physical.Y2 - physical.Y1);
        foreach (ViewBody b in s.Frame.Bodies.Where(b => b.Kind == ViewKind.StageDetail))
        {
            // Transform all four box corners into world ground axes: visuals never extend past the footprint.
            foreach (float x in new[] { -b.Size.X / 2, b.Size.X / 2 })
            foreach (float z in new[] { -b.Size.Z / 2, b.Size.Z / 2 })
            {
                float wx = b.Position.X + MathF.Cos(b.GroundAngle) * x - MathF.Sin(b.GroundAngle) * z;
                float wz = b.Position.Z + MathF.Sin(b.GroundAngle) * x + MathF.Cos(b.GroundAngle) * z;
                Assert.InRange(wx, physical.X1 - .0001f, physical.X2 + .0001f);
                Assert.InRange(wz, physical.Y1 - .0001f, physical.Y2 + .0001f);
                Assert.True(b.Size.X > 0 && b.Size.Y > 0 && b.Size.Z > 0);
            }
        }
    }

    [Theory]
    [InlineData(float.NaN, 4)] [InlineData(float.PositiveInfinity, 4)] [InlineData(0, 4)]
    [InlineData(-1, 4)] [InlineData(8, .99f)] [InlineData(161, 4)] [InlineData(8, 121)]
    public void InvalidDimensionsCannotChangeAuthoredScene(float length, float width)
    {
        var s = Stage(); Assert.True(s.Place(PlacementTool.Asset, new Vector2(20, 15), 0));
        string before = s.ExportAuthoredJson("Before"); int revision = s.Revision;
        Assert.False(s.SetStageSize(length, width)); Assert.Equal(revision, s.Revision);
        Assert.Equal(before, s.ExportAuthoredJson("Before"));
    }

    [Fact]
    public void SmallRoomAndQuarterTurnsConstrainBothDimensionsToUsableRoomAxes()
    {
        WorkspaceSession s = new(); Assert.True(s.Resize(10, 12)); s.SelectAsset(BuilderAsset.Stage);
        Assert.InRange(s.StageLength, 1, 10); Assert.InRange(s.StageWidth, 1, 12);
        Assert.False(s.SetStageSize(10, 12)); // physical perimeter remains present
        Assert.True(s.SetStageSize(s.MaximumStageLength, s.MaximumStageWidth));
        Assert.True(s.Preview(PlacementTool.Asset, new Vector2(5, 6), 0).Valid);
        s.SetAssetRotation(90);
        Assert.True(s.StageLength <= s.MaximumStageLength && s.StageWidth <= s.MaximumStageWidth);
        Assert.True(s.SetStageSize(s.MaximumStageLength, s.MaximumStageWidth));
        Assert.True(s.Place(PlacementTool.Asset, new Vector2(5, 6), 0));
        Assert.False(s.Place(PlacementTool.Asset, new Vector2(5, 6), 0));
    }

    [Fact]
    public void ExistingResizeIsAtomicForBoundariesObstaclesAndUndoPlacementOrder()
    {
        var s = Stage(); Assert.True(s.Place(PlacementTool.Asset, new Vector2(7, 12), 0));
        Assert.True(s.Place(PlacementTool.Square, new Vector2(14, 12), 1));
        string before = s.ExportAuthoredJson("same");
        Assert.False(s.SetStageSize(18, 4)); Assert.Equal(before, s.ExportAuthoredJson("same")); // boundary
        Assert.False(s.SetStageSize(14, 4)); Assert.Equal(before, s.ExportAuthoredJson("same")); // pillar
        Assert.True(s.SetStageSize(10, 5));
        Assert.True(s.Undo()); Assert.True(s.HasStage); // pillar remains the latest placement
        Assert.Equal(10, s.StageLength); Assert.True(s.Undo()); Assert.False(s.HasStage);
    }

    [Fact]
    public void ResizeCannotCoverExitPeopleOrOccupiedFurnitureReservations()
    {
        var s = Stage(4, 2); Assert.True(s.Place(PlacementTool.Asset, new Vector2(8, 10), 0));
        Assert.True(s.Place(PlacementTool.Agents, new Vector2(12, 10), 1));
        Assert.False(s.SetStageSize(10, 4));
        var seats = Stage(4, 2); Assert.True(seats.Place(PlacementTool.Asset, new Vector2(8, 10), 0));
        seats.SelectAsset(BuilderAsset.SingleChair); Assert.True(seats.Place(PlacementTool.Asset, new Vector2(12, 10), 0));
        Assert.False(seats.SetStageSize(8, 2));
        var exit = Stage(2, 2); Assert.True(exit.Place(PlacementTool.Asset, new Vector2(3, 10), 0));
        Assert.True(exit.Place(PlacementTool.Exit, new Vector2(0, 10), 3));
        Assert.False(exit.SetStageSize(4.7f, 2));
    }

    [Fact]
    public void CustomStageUsesExplicitV5AndSurvivesSaveLoadFinalizeReplayWithoutChangingLegacyV4()
    {
        var s = Stage(); Assert.True(s.Place(PlacementTool.Asset, new Vector2(20, 15), 0));
        Assert.True(s.Place(PlacementTool.Exit, new Vector2(0, 15), 1));
        Assert.True(s.Place(PlacementTool.Agents, new Vector2(8, 8), 5));
        string json = s.ExportAuthoredJson("Custom"); Assert.Equal(5, (int)JObject.Parse(json)["version"]!);
        s = WorkspaceSession.FromJson(json); Assert.Equal(8, s.StageLength); Assert.Equal(4, s.StageWidth);
        Assert.True(s.Finalise()); Assert.False(s.SetStageSize(10, 5));
        s.ToggleRunning(); s.Advance(s.StepSeconds); Assert.True(s.Replay());
        Assert.Equal(8, s.StageLength); Assert.Equal(4, s.StageWidth);
        Assert.Equal(json, s.ExportAuthoredJson("Custom"));
        var legacy = Stage(15, 6); Assert.True(legacy.Place(PlacementTool.Asset, new Vector2(20, 15), 0));
        JObject saved = JObject.Parse(legacy.ExportAuthoredJson("Legacy"));
        Assert.Equal(4, (int)saved["version"]!); Assert.Null(saved["furniture"]![0]!["stageLength"]);
        Assert.Equal(15, WorkspaceSession.FromJson(saved.ToString()).StageLength);
    }

    [Theory]
    [InlineData(4, 3, 8, 4)] [InlineData(5, 2, 8, 4)] [InlineData(5, 3, 8, 0)] [InlineData(5, 3, -8, 4)]
    public void InvalidCustomSchemaCannotSilentlyLoadDefaultSizedStage(int version, int kind, float length, float width)
    {
        var s = Stage(); s.Place(PlacementTool.Asset, new Vector2(20, 15), 0);
        JObject json = JObject.Parse(s.ExportAuthoredJson("test")); json["version"] = version;
        json["furniture"]![0]!["kind"] = kind; json["furniture"]![0]!["stageLength"] = length; json["furniture"]![0]!["stageWidth"] = width;
        Assert.Throws<InvalidDataException>(() => WorkspaceSession.FromJson(json.ToString()));
    }
}
