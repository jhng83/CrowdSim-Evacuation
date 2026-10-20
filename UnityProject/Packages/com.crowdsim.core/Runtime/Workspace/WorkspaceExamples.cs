using System;
using System.Numerics;
using CrowdSim.OpenGL.UI;

namespace CrowdSim.Workspace
{
    /// <summary>Editable M4 example assembled through the same commands as manual authoring.</summary>
    public static class WorkspaceExamples
    {
        public static WorkspaceSession Furniture()
        {
            WorkspaceSession session = new WorkspaceSession();
            Require(session.Place(PlacementTool.Exit, new Vector2(0, 17), 5));
            Place(session, BuilderAsset.SquareBlock, 10, 28);
            Place(session, BuilderAsset.LargeSquareBlock, 14, 28);
            Place(session, BuilderAsset.CircularBlock, 20, 28);
            Place(session, BuilderAsset.RoundTableSet, 12, 12);
            Place(session, BuilderAsset.SingleChair, 20, 12, 37.5f);
            Place(session, BuilderAsset.RectangularTable, 28, 12, 90);
            Place(session, BuilderAsset.Stage, 42, 22);
            Place(session, BuilderAsset.Barricade, 30, 22, 37.5f);
            Require(session.Place(PlacementTool.Agents, new Vector2(8, 20), 15));
            session.SelectAsset(BuilderAsset.Barricade);
            return session;
        }
        private static void Place(WorkspaceSession session, BuilderAsset asset, float x, float y, float degrees = 0)
        {
            Require(session.SelectAsset(asset));
            Require(session.SetAssetRotation(degrees));
            Require(session.Place(PlacementTool.Asset, new Vector2(x, y), 0));
        }
        private static void Require(bool success)
        {
            if (!success) throw new InvalidOperationException("Furniture example placement failed; no asset may be silently omitted.");
        }
    }
}
