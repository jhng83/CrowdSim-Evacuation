#nullable enable
using System;
using System.Linq;
using CrowdSim.Core.Model;
using CrowdSim.OpenGL.UI;

namespace CrowdSim.Workspace
{
    public sealed partial class WorkspaceSession
    {
        private FurnitureSet? Stage => _simulation.Environment.Furniture.FirstOrDefault(f => f.Kind == FurnitureKind.Stage);
        public float StageLength => Stage?.AuthoredStageLength ?? _assets.StageLength;
        public float StageWidth => Stage?.AuthoredStageWidth ?? _assets.StageWidth;
        private bool StageSwapped => MathF.Abs(MathF.Sin(Stage?.Rotation ?? _assets.RotationRadians)) > .5f;
        // Retain the physical perimeter inset. UI limits follow the footprint's rotated world axes.
        public float MaximumStageLength => (StageSwapped ? _simulation.Environment.Height : _simulation.Environment.Width) - 2 * _simulation.Environment.BoundaryThickness;
        public float MaximumStageWidth => (StageSwapped ? _simulation.Environment.Width : _simulation.Environment.Height) - 2 * _simulation.Environment.BoundaryThickness;

        private void FitStageSelection()
        {
            if (_assets.Selected != BuilderAsset.Stage) return;
            if (Stage != null)
            {
                _assets.StageLength = Stage.AuthoredStageLength; _assets.StageWidth = Stage.AuthoredStageWidth;
                _assets.SetRotation(Stage.Rotation * 180f / MathF.PI); return;
            }
            _assets.StageLength = Math.Clamp(_assets.StageLength, 1f, MaximumStageLength);
            _assets.StageWidth = Math.Clamp(_assets.StageWidth, 1f, MaximumStageWidth);
        }

        /// <summary>Edit the next stage or atomically resize the placed stage, only in the builder.</summary>
        public bool SetStageSize(float length, float width)
        {
            if (!IsBuilding || !float.IsFinite(length) || !float.IsFinite(width) ||
                length < 1f || width < 1f || length > MaximumStageLength || width > MaximumStageWidth)
            { Message = "Stage size rejected: use at least 1 m and stay within the room's usable span (perimeter inset retained)."; return false; }
            if (HasStage && !_simulation.Environment.ResizeStage(length, width))
            { Message = "Stage resize rejected: footprint would cross the boundary, an exit, obstacle, reserved seat space or planned people."; return false; }
            _assets.StageLength = length; _assets.StageWidth = width;
            if (HasStage)
            {
                _simulation.BuildWallGrid(); InvalidateFumeForGeometry();
                Changed("Stage resized in place. Undo still removes placements in their original order.", true);
            }
            else Message = "Next stage dimensions updated. Preview and placement use the exact footprint.";
            return true;
        }
    }
}
