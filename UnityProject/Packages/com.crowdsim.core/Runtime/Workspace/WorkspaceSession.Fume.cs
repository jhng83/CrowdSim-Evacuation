#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using CrowdSim.Core.Model;
using CrowdSim.Diagnostics;

namespace CrowdSim.Workspace
{
    public sealed partial class WorkspaceSession
    {
        private bool _fumeNeedsCompute;
        public FumeFrame Fume { get; private set; } = null!;
        public bool ShowFume { get; set; } = true;
        public bool ShowFumeGrid { get; set; }
        public bool CanEditFume => IsBuilding && Scenario == ScenarioType.ToxicFume;
        public bool CanComputeFume => CanEditFume && _simulation.ToxicFumeSystem.SourcePosition != null;
        public IReadOnlyList<float> FumeValues => Array.AsReadOnly(FumeControls.Read(_simulation.ToxicFumeSystem.Configuration));
        public string FumeReadiness => !IsBuilding ? FumeReleased ? "Release active. Pause to inspect, or replay to reset the experiment."
            : Running ? "Field ready. Release Toxic Fume when the event should begin." : "Scene ready. Start the simulation, then release the fume."
            : Fume.Source == null ? "Place a release point on clear floor."
            : HasFumeCache ? "Field ready. Preview the timeline, then finalise."
            : _fumeNeedsCompute ? "Source, layout or settings changed. Compute dispersion before finalising."
            : "Saved source loaded. Compute to preview, or Finalise to rebuild its cache.";
        private void InvalidateFumeForGeometry()
        {
            if (Scenario != ScenarioType.ToxicFume) return;
            _simulation.ToxicFumeSystem.InvalidatePrecomputation(); _fumeNeedsCompute = true;
        }
        public bool CanPlaceFumeSource(Vector2 point)
        {
            float margin = Math.Max(.02f, .25f * _simulation.Config.Agent.Radius);
            float inset = _simulation.Environment.BoundaryThickness + margin;
            return CanEditFume && float.IsFinite(point.X) && float.IsFinite(point.Y) &&
                point.X > inset && point.Y > inset && point.X < Frame.Width - inset && point.Y < Frame.Length - inset &&
                !_simulation.Environment.IsPointBlocked(point, margin);
        }
        public bool PlaceFumeSource(Vector2 point)
        {
            if (!CanEditFume || !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !_simulation.ToxicFumeSystem.TrySetSource(_simulation, point))
            { Message = "Release point rejected: use clear floor inside an editable Toxic Fume room."; return false; }
            _fumeNeedsCompute = true;
            Changed("Release point placed. Compute dispersion to preview the field.", true); return true;
        }
        public bool SetFumeValue(int index, float value)
        {
            if (index < 0 || index >= FumeControls.All.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (!CanEditFume || !float.IsFinite(value)) { Message = "Fume settings require finite values in the builder."; return false; }
            float[] values = FumeControls.Read(_simulation.ToxicFumeSystem.Configuration);
            if (values[index] == value) return true;
            values[index] = value; FumeControls.Apply(_simulation.ToxicFumeSystem.Configuration, values);
            InvalidateFumeForGeometry(); Changed("Fume settings updated. Recompute dispersion before finalising.", true); return true;
        }
        public bool ResetFumeDefaults()
        {
            if (!CanEditFume) { Message = "Fume settings are locked after finalisation."; return false; }
            _simulation.ToxicFumeSystem.Configuration.ResetDefaults(); InvalidateFumeForGeometry();
            Changed("Fume defaults restored. Recompute dispersion.", true); return true;
        }
        public bool ComputeFume()
        {
            if (!CanComputeFume) { Message = "Place a source in the Toxic Fume builder before computing."; return false; }
            try
            {
                _simulation.PrecomputeToxicFume(); _fumeNeedsCompute = false;
                Changed("Dispersion ready. Scrub the preview without moving people or accumulating exposure."); return true;
            }
            catch (Exception exception)
            {
                InvalidateFumeForGeometry(); Changed("Dispersion computation failed: " + exception.Message); return false;
            }
        }
        public bool SetFumePreview(float seconds)
        {
            if (!CanEditFume || !HasFumeCache || !float.IsFinite(seconds)) { Message = "Compute a valid field in the builder before previewing."; return false; }
            _simulation.ToxicFumeSystem.PreviewTimeSeconds = Math.Clamp(seconds, 0, Fume.PreviewMaximum);
            Fume = FumeFrame.Capture(_simulation, true); Revision++; return true;
        }
        public bool ReleaseFume()
        {
            if (!Running || !_simulation.TriggerToxicFumeRelease())
            { Message = "Start a finalised Toxic Fume scene with a valid cache before release."; return false; }
            Changed("Toxic Fume released. The cached field evolves while crowd response and exposure remain live."); return true;
        }
        // Retain the M5 programmatic entry point; the UI now offers the complete authoring workflow.
        public bool ReleaseSavedFume() => ReleaseFume();
        private bool SourceFitsRoom(float width, float length)
        {
            if (_simulation.ToxicFumeSystem.SourcePosition is not Vector2 point) return true;
            float inset = _simulation.Environment.BoundaryThickness + Math.Max(.02f, .25f * _simulation.Config.Agent.Radius);
            return point.X > inset && point.Y > inset && point.X < width - inset && point.Y < length - inset;
        }
    }
}
