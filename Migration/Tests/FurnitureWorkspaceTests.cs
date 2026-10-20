using System.Numerics;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class FurnitureWorkspaceTests
{
    [Theory]
    [InlineData(BuilderAsset.RoundTableSet, 10)]
    [InlineData(BuilderAsset.SingleChair, 1)]
    [InlineData(BuilderAsset.RectangularTable, 0)]
    [InlineData(BuilderAsset.Stage, 0)]
    [InlineData(BuilderAsset.Barricade, 0)]
    public void EachFurniturePreviewAndCommitAgreeOnPeopleAndUndoIsOneAtomicCommand(BuilderAsset asset, int occupants)
    {
        WorkspaceSession session = new();
        Assert.True(session.SelectAsset(asset));
        Assert.True(session.SetAssetRotation(asset == BuilderAsset.Barricade ? 37.5f : 90));
        Vector2 position = new(20, 17);
        PlacementPreview preview = session.Preview(PlacementTool.Asset, position, 0);
        Assert.True(preview.Valid);
        Assert.Equal(occupants, preview.Bodies.Count(b => b.Kind == ViewKind.Agent));
        Assert.Equal(0, session.PlannedCount);
        Assert.Equal(0, session.FurnitureCount);
        Assert.True(session.Place(PlacementTool.Asset, position, 0));
        Assert.Equal(occupants, session.PlannedCount);
        Assert.Equal(1, session.FurnitureCount);
        Assert.Equal(preview.Bodies.Select(Key).OrderBy(k => k), session.Frame.Bodies.Where(b => b.Kind != ViewKind.Floor).Select(Key).OrderBy(k => k));
        Assert.False(session.CanResize);
        Assert.False(session.Place(PlacementTool.Asset, position, 0));
        Assert.Equal(1, session.FurnitureCount);
        Assert.True(session.Undo());
        Assert.Equal(0, session.FurnitureCount);
        Assert.Equal(0, session.PlannedCount);
        Assert.Empty(session.Frame.FurnitureBodies);
        Assert.Single(session.Frame.Bodies);
        Assert.True(session.CanResize);
    }

    private static string Key(ViewBody b) => $"{b.Kind}:{b.Shape}:{b.Position}:{b.Size}:{b.GroundAngle}:{b.Posture}";

    [Fact]
    public void OneStageGuardAndQuarterTurnsSurviveUndoAndKeepOtherAssetFacing()
    {
        WorkspaceSession session = new();
        session.SelectAsset(BuilderAsset.SingleChair); session.SetAssetRotation(37.5f);
        session.SelectAsset(BuilderAsset.Stage); session.SetAssetRotation(68);
        Assert.Equal(90, session.AssetRotationDegrees);
        Assert.True(session.Place(PlacementTool.Asset, new Vector2(20, 17), 0));
        ViewBody stage = Assert.Single(session.Frame.Bodies, b => b.Kind == ViewKind.Stage);
        Assert.Equal(new Vector3(6, .8f, 15), stage.Size);
        Assert.True(session.HasStage && session.AssetUnavailable);
        Assert.False(session.Place(PlacementTool.Asset, new Vector2(42, 17), 0));
        Assert.False(session.RotateAsset());
        Assert.True(session.Undo());
        Assert.False(session.HasStage || session.AssetUnavailable);
        Assert.True(session.RotateAsset());
        session.SelectAsset(BuilderAsset.SingleChair);
        Assert.Equal(37.5f, session.AssetRotationDegrees);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void BarricadeSnapUsesTheSameParallelEndpointInPreviewAndCommit(bool snap)
    {
        WorkspaceSession session = new();
        session.SelectAsset(BuilderAsset.Barricade); session.SetAssetRotation(37.5f); session.SetBarricadeSnap(snap);
        Vector2 origin = new(15, 15), axis = new(MathF.Cos(37.5f * MathF.PI / 180), MathF.Sin(37.5f * MathF.PI / 180));
        Assert.True(session.Place(PlacementTool.Asset, origin, 0));
        Vector2 requested = origin + axis * 1.65f;
        PlacementPreview preview = session.Preview(PlacementTool.Asset, requested, 0);
        Assert.True(preview.Valid);
        ViewBody proposed = Assert.Single(preview.Bodies, b => b.Id == "candidate");
        Vector2 expected = snap ? origin + axis * 1.5f : requested;
        Assert.InRange(Vector2.Distance(expected, new Vector2(proposed.Position.X, proposed.Position.Z)), 0, .00001f);
        Assert.True(session.Place(PlacementTool.Asset, requested, 0));
        ViewBody committed = Assert.Single(session.Frame.Bodies, b => b.Id == "furniture-1");
        Assert.Equal(proposed.Position, committed.Position);
        Assert.Equal(proposed.GroundAngle, committed.GroundAngle);
    }

    [Fact]
    public void InvalidCandidateStillHasRedPreviewGeometryAndCannotChangeUndoHistory()
    {
        WorkspaceSession session = new();
        session.SelectAsset(BuilderAsset.RoundTableSet);
        PlacementPreview rejected = session.Preview(PlacementTool.Asset, new Vector2(1, 1), 0);
        Assert.False(rejected.Valid);
        Assert.NotEmpty(rejected.Bodies);
        Assert.False(session.Place(PlacementTool.Asset, new Vector2(1, 1), 0));
        Assert.False(session.Undo());
        Assert.True(session.CanResize);
        Assert.False(session.Preview(PlacementTool.Asset, new Vector2(float.NaN, 1), 0).Valid);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FurnishedExampleHasEveryAssetAndSeatsStayAtTheirAuthoredStartsBeforeThreat(bool hybrid)
    {
        WorkspaceSession session = WorkspaceExamples.Furniture();
        Assert.True(session.SetHybrid(hybrid));
        Assert.Equal(5, session.FurnitureCount);
        Assert.Equal(26, session.PlannedCount);
        Assert.Equal(11, session.Frame.SeatedAgents);
        Assert.Equal(15, session.Frame.FurnitureBodies.Count);
        var starts = session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
        Assert.True(session.Finalise());
        Assert.Equal(starts, session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position));
        Assert.Equal(11, session.Frame.FurnitureBodies.Count(b => b.OccupantId.HasValue));
        Assert.False(session.SetAssetRotation(15)); Assert.False(session.RotateAsset());
        Assert.False(session.SelectAsset(BuilderAsset.Stage)); Assert.False(session.SetBarricadeSnap(false));
        var seats = session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent && b.Posture == SeatingPosture.Seated).Select(b => b.Position).ToArray();
        session.ToggleRunning();
        for (int i = 0; i < 20; i++) session.Advance(session.StepSeconds);
        Assert.Equal(20, session.Tick);
        Assert.Equal(seats, session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent && b.Posture == SeatingPosture.Seated).Select(b => b.Position));
        Assert.True(session.TriggerExplosion(new Vector2(18, 12)));
        for (int i = 0; i < 120; i++) session.Advance(session.StepSeconds);
        Assert.Equal(140, session.Tick);
        Assert.Contains(session.Frame.FurnitureBodies, b => b.State != ChairState.Upright);
        Assert.All(session.Frame.Bodies, b => Assert.True(float.IsFinite(b.Position.X) && float.IsFinite(b.Position.Y) && float.IsFinite(b.Position.Z)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RShortcutRespectsTextEntryAndModifiers(bool editing)
    {
        Assert.Equal(editing ? WorkspaceShortcut.None : WorkspaceShortcut.RotateAsset, WorkspaceShortcuts.Resolve("R", false, false, editing));
        Assert.Equal(WorkspaceShortcut.None, WorkspaceShortcuts.Resolve("R", false, true, editing));
        Assert.Equal(WorkspaceShortcut.None, WorkspaceShortcuts.Resolve("R", true, false, editing));
    }
}
