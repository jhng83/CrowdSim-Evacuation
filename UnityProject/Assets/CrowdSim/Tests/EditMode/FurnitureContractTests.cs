using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;
using CrowdSim.Workspace;
using NUnit.Framework;
using GroundPoint = System.Numerics.Vector2;

namespace CrowdSim.Unity.Tests
{
    public sealed class FurnitureContractTests
    {
        [Test]
        public void ExampleFinalisesWithAllFiveKindsAndExactlyElevenSeatedOccupants()
        {
            WorkspaceSession session = WorkspaceExamples.Furniture();
            Assert.That(session.FurnitureCount, Is.EqualTo(5));
            Assert.That(session.PlannedCount, Is.EqualTo(26));
            Assert.That(session.Frame.SeatedAgents, Is.EqualTo(11));
            Assert.That(session.Frame.FurnitureBodies.Count, Is.EqualTo(15));
            var starts = session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position).ToArray();
            Assert.That(session.Finalise(), Is.True);
            Assert.That(session.Frame.Bodies.Where(b => b.Kind == ViewKind.Agent).Select(b => b.Position), Is.EqualTo(starts));
            Assert.That(session.Frame.FurnitureBodies.Count(b => b.OccupantId.HasValue), Is.EqualTo(11));
        }

        [Test]
        public void BarricadeFreeAngleAndParallelEndSnapHaveTheSamePreviewAndCommit()
        {
            WorkspaceSession session = new WorkspaceSession();
            session.SelectAsset(BuilderAsset.Barricade); session.SetAssetRotation(37.5f);
            Assert.That(session.Place(PlacementTool.Asset, new GroundPoint(15, 15), 0), Is.True);
            float angle = 37.5f * System.MathF.PI / 180;
            GroundPoint requested = new GroundPoint(15, 15) + new GroundPoint(System.MathF.Cos(angle), System.MathF.Sin(angle)) * 1.65f;
            PlacementPreview preview = session.Preview(PlacementTool.Asset, requested, 0);
            Assert.That(preview.Valid, Is.True);
            Assert.That(session.Place(PlacementTool.Asset, requested, 0), Is.True);
            Assert.That(session.Frame.Bodies.Single(b => b.Id == "furniture-1").Position,
                Is.EqualTo(preview.Bodies.Single(b => b.Id == "candidate").Position));
            Assert.That(session.AssetRotationDegrees, Is.EqualTo(37.5f));
        }

        [Test]
        public void StageQuarterTurnAndSingleStageGuardSurviveUndo()
        {
            WorkspaceSession session = new WorkspaceSession();
            session.SelectAsset(BuilderAsset.Stage); session.SetAssetRotation(68);
            Assert.That(session.AssetRotationDegrees, Is.EqualTo(90));
            Assert.That(session.Place(PlacementTool.Asset, new GroundPoint(20, 17), 0), Is.True);
            Assert.That(session.Place(PlacementTool.Asset, new GroundPoint(42, 17), 0), Is.False);
            ViewBody stage = session.Frame.Bodies.Single(b => b.Kind == ViewKind.Stage);
            Assert.That(stage.Size, Is.EqualTo(new System.Numerics.Vector3(6, .8f, 15)));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.AssetUnavailable, Is.False);
        }

        [Test]
        public void PerAssetFacingAndRotationShortcutRespectTextFocus()
        {
            WorkspaceSession session = new WorkspaceSession();
            session.SelectAsset(BuilderAsset.SingleChair); session.SetAssetRotation(37.5f);
            session.SelectAsset(BuilderAsset.Barricade); session.RotateAsset();
            Assert.That(session.AssetRotationDegrees, Is.EqualTo(15));
            session.RotateAsset(true); Assert.That(session.AssetRotationDegrees, Is.Zero);
            session.SelectAsset(BuilderAsset.SingleChair); Assert.That(session.AssetRotationDegrees, Is.EqualTo(37.5f));
            Assert.That(WorkspaceShortcuts.Resolve("R", false, false, true), Is.EqualTo(WorkspaceShortcut.None));
            Assert.That(WorkspaceShortcuts.Resolve("R", false, false, false), Is.EqualTo(WorkspaceShortcut.RotateAsset));
        }
    }
}
