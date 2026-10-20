#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Diagnostics;
using CrowdSim.OpenGL.UI;

namespace CrowdSim.Workspace
{
    public enum PlacementTool { Select, Exit, Square, Circle, Agents, Explosion, Asset }
    public enum WorkspacePhase { Building, Ready, Running, Paused }

    /// <summary>Detached candidate geometry. Preview uses a throwaway model and never consumes the live RNG/history.</summary>
    public sealed class PlacementPreview
    {
        public bool Valid { get; }
        public IReadOnlyList<ViewBody> Bodies { get; }
        internal PlacementPreview(bool valid, IEnumerable<ViewBody> bodies)
        { Valid = valid; Bodies = Array.AsReadOnly(bodies.ToArray()); }
    }

    /// <summary>Authoritative command boundary for M3. UI receives snapshots; only commands mutate the model.</summary>
    public sealed partial class WorkspaceSession
    {
        private Simulation _simulation;
        private readonly BuilderAssetSelection _assets = new BuilderAssetSelection();
        private readonly InspectorPanelState _inspector = new InspectorPanelState();
        private readonly FixedStepScheduler _scheduler = new FixedStepScheduler();
        private double _speed = 1d;
        public WorkspacePhase Phase { get; private set; } = WorkspacePhase.Building;
        public bool IsBuilding => Phase == WorkspacePhase.Building;
        public bool CanResize => IsBuilding && _simulation.Environment.CanResizeRoom;
        public bool Running => Phase == WorkspacePhase.Running;
        public float StepSeconds => _simulation.Config.Agent.Dt;
        public int Tick => _simulation.FrameIndex;
        public BuilderAsset SelectedAsset => _assets.Selected;
        public float AssetRotationDegrees => _assets.RotationDegrees;
        public bool AssetSupportsRotation => _assets.SupportsRotation;
        public bool AssetUsesQuarterTurns => _assets.UsesQuarterTurns;
        public bool SnapBarricadeEnds => _assets.SnapBarricadeEnds;
        public string AssetHint => _assets.Hint;
        public bool HasStage => _simulation.Environment.HasStage;
        public bool AssetUnavailable => SelectedAsset == BuilderAsset.Stage && HasStage;
        public int FurnitureCount => _simulation.Environment.Furniture.Count;
        public int PlannedCount => _simulation.Environment.PlannedAgentCount > 0 ? _simulation.Environment.PlannedAgentCount : _requestedAgentCount;
        public int ExitCount => _simulation.Environment.Exits.Count;
        public bool Hybrid => _simulation.Config.HybridEnabled;
        public ScenarioType Scenario => _simulation.ScenarioType;
        public PresentationFrame Frame { get; private set; }
        public string Message { get; private set; } = "Choose a tool, then click the room. Place an exit and people before finalising.";
        public double Speed => _speed;
        public int Revision { get; private set; }
        public int? SelectedAgentId { get; private set; }
        public bool InspectorVisible => _inspector.IsVisible;
        public int InspectorTab => (int)_inspector.ActiveTab;
        public bool ShowGrid { get; set; }
        public string Readiness => !IsBuilding ? "Scene finalised. Geometry and behaviour are locked."
            : ExitCount == 0 ? "At least one exit is required."
            : PlannedCount == 0 ? "Place at least one agent group."
            : Scenario == ScenarioType.ToxicFume && _simulation.ToxicFumeSystem.SourcePosition == null ? "A saved toxic-fume source is required. Source authoring arrives in M7."
            : "Ready to finalise.";
        public bool CanFinalise => IsBuilding && ExitCount > 0 && PlannedCount > 0 && (Scenario == ScenarioType.Explosion || _simulation.ToxicFumeSystem.SourcePosition != null);

        public WorkspaceSession(ScenarioType scenario = ScenarioType.Explosion, int seed = 42)
        {
            _simulation = new Simulation(randomSeed: seed);
            _simulation.SetScenarioType(scenario);
            Frame = PresentationFrame.Capture(_simulation, true);
        }

        private void Changed(string message, bool authored = false)
        {
            Message = message;
            if (authored) IsDirty = true;
            Revision++;
            Frame = PresentationFrame.Capture(_simulation, IsBuilding);
        }

        public bool Resize(float width, float length)
        {
            if (!CanResize || !float.IsFinite(width) || !float.IsFinite(length) || width < 10 || width > 160 || length < 10 || length > 120)
            { Message = "Resize rejected: use 10–160 m width / 10–120 m length in an empty builder."; return false; }
            bool result = _simulation.Environment.TryResizeRoom(width, length);
            _simulation.BuildWallGrid();
            Changed("Room size updated in metres.", true);
            return result;
        }

        private static bool ApplyPlacement(Simulation simulation, PlacementTool tool, Vector2 point, int option, BuilderAssetSelection assets)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;
            switch (tool)
            {
                case PlacementTool.Exit: return (option == 1 || option == 3 || option == 5) && simulation.Environment.AddExitFromClick(point.X, point.Y, option);
                case PlacementTool.Square: return (option == 1 || option == 2) && simulation.Environment.AddBlockageFromClick(point.X, point.Y, option);
                case PlacementTool.Circle: return simulation.Environment.AddCircularBlockageFromClick(point.X, point.Y);
                case PlacementTool.Agents: return (option == 1 || option == 5 || option == 15 || option == 50) && simulation.Environment.AddAgentSpawnGroupFromClick(point.X, point.Y, option);
                case PlacementTool.Asset:
                    if (assets.Furniture is FurnitureKind kind)
                    {
                        if (kind == FurnitureKind.Barricade && assets.SnapBarricadeEnds)
                            point = simulation.Environment.SnapBarricadePlacement(point, assets.RotationRadians);
                        return simulation.Environment.AddFurniture(kind, point, assets.RotationRadians);
                    }
                    return assets.Selected == BuilderAsset.CircularBlock
                        ? simulation.Environment.AddCircularBlockageFromClick(point.X, point.Y)
                        : simulation.Environment.AddBlockageFromClick(point.X, point.Y, assets.BlockageCells);
                default: return false;
            }
        }

        public PlacementPreview Preview(PlacementTool tool, Vector2 point, int option)
        {
            if (!IsBuilding) return new PlacementPreview(false, Array.Empty<ViewBody>());
            if (tool == PlacementTool.Asset && _assets.Furniture is FurnitureKind kind)
            {
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return new PlacementPreview(false, Array.Empty<ViewBody>());
                Vector2 snapped = kind == FurnitureKind.Barricade && _assets.SnapBarricadeEnds
                    ? _simulation.Environment.SnapBarricadePlacement(point, _assets.RotationRadians) : point;
                bool fits = _simulation.Environment.CanPlaceFurniture(kind, snapped, _assets.RotationRadians);
                List<ViewBody> bodies = new List<ViewBody>();
                FurnitureSet proposed = new FurnitureSet(kind, snapped, _assets.RotationRadians);
                FurniturePresentation.AddSet(bodies, proposed, "candidate");
                int seat = 0;
                foreach (FurnitureChair chair in proposed.Chairs)
                    FurniturePresentation.AddSeatedAgent(bodies, "candidate-person-" + seat++, chair.StartPosition,
                        chair.StartHeading, SeatingPosture.Seated, 0, AgentState.Wandering);
                return new PlacementPreview(fits, bodies);
            }
            ScenarioDefinition definition = ScenarioDefinition.Capture(_simulation, 0, "Preview");
            Simulation candidate = new Simulation(config: definition.CreateConfig(), randomSeed: definition.RandomSeed);
            definition.ApplyLayout(candidate);
            bool valid = ApplyPlacement(candidate, tool, point, option, _assets);
            HashSet<(ViewKind, ViewShape, Vector3, Vector3, float)> existing = new HashSet<(ViewKind, ViewShape, Vector3, Vector3, float)>(
                Frame.Bodies.Select(b => (b.Kind, b.Shape, b.Position, b.Size, b.GroundAngle)));
            return new PlacementPreview(valid, valid ? PresentationFrame.Capture(candidate, true).Bodies.Where(b => !existing.Contains((b.Kind, b.Shape, b.Position, b.Size, b.GroundAngle))) : Array.Empty<ViewBody>());
        }

        public bool Place(PlacementTool tool, Vector2 point, int option)
        {
            if (!IsBuilding) { Message = "Geometry is locked after finalisation."; return false; }
            bool result = ApplyPlacement(_simulation, tool, point, option, _assets);
            if (!result) { Message = tool == PlacementTool.Asset && AssetUnavailable ? "Only one stage is allowed. Undo its placement before replacing it." : "Placement rejected: check boundary, overlap, occupancy and available floor."; return false; }
            _simulation.BuildWallGrid();
            Changed(tool + " placed. Ctrl+Z undoes the last placement.", true);
            return true;
        }

        /// <summary>Picker state is session-owned and locked with authored geometry.</summary>
        public bool SelectAsset(BuilderAsset asset)
        {
            if (!IsBuilding) return false;
            _assets.Select(asset);
            Message = _assets.Hint;
            return true;
        }
        public bool SetAssetRotation(float degrees)
        {
            if (!IsBuilding || AssetUnavailable) return false;
            _assets.SetRotation(degrees);
            Message = "Next placement: " + _assets.RotationDegrees.ToString("F1") + " degrees. Existing assets are unchanged.";
            return true;
        }
        public bool RotateAsset(bool reverse = false)
        {
            if (!IsBuilding || !AssetSupportsRotation || AssetUnavailable) return false;
            _assets.RotateStep(reverse);
            Message = "Next placement: " + _assets.RotationDegrees.ToString("F1") + " degrees. Existing assets are unchanged.";
            return true;
        }
        public bool SetBarricadeSnap(bool enabled)
        {
            if (!IsBuilding) return false;
            _assets.SnapBarricadeEnds = enabled;
            return true;
        }

        public bool Undo()
        {
            if (!IsBuilding || !_simulation.Environment.UndoLast()) { Message = "Nothing can be undone here."; return false; }
            _simulation.BuildWallGrid();
            Changed("Last placement undone.", true);
            return true;
        }

        public bool Finalise()
        {
            if (!CanFinalise) { Message = Readiness; return false; }
            FinalizedSceneCheckpoint checkpoint = FinalizedSceneCheckpoint.Capture(_simulation, PlannedCount, Name);
            Simulation ready = checkpoint.CreateReadySimulation();
            _simulation = ready;
            _checkpoint = checkpoint;
            Phase = WorkspacePhase.Ready;
            _scheduler.Reset();
            Changed("Scene finalised at frame zero. Start, then trigger the selected hazard.");
            return true;
        }

        public bool ToggleRunning()
        {
            if (IsBuilding) { Message = "Finalise the scene first."; return false; }
            _simulation.SetRunning(!Running);
            Phase = _simulation.Running ? WorkspacePhase.Running : WorkspacePhase.Paused;
            _scheduler.Reset();
            Changed(Running ? "Simulation running." : "Simulation paused.");
            return true;
        }

        public void SetSpeed(double speed)
        {
            if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0.25 || speed > 4) throw new ArgumentOutOfRangeException(nameof(speed));
            _speed = speed;
            _scheduler.Reset();
        }

        public int Advance(double elapsed)
        {
            if (!Running) return 0;
            int count = _scheduler.Advance(elapsed, _simulation.Config.Agent.Dt, _speed, _simulation.Step, () => false);
            if (count > 0)
            {
                if (SelectedAgentId.HasValue && _simulation.Agents[SelectedAgentId.Value].State == AgentState.Exited) SelectedAgentId = null;
                Changed("Simulation running.");
            }
            return count;
        }

        public bool TriggerExplosion(Vector2 point)
        {
            if (!Running || Scenario != ScenarioType.Explosion || _simulation.Environment.HasExplosion() ||
                !float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.X < 0 || point.Y < 0 || point.X > Frame.Width || point.Y > Frame.Length)
            { Message = "Explosion requires a running Explosion scene, an inside-room point and no previous detonation."; return false; }
            _simulation.TriggerExplosion(point.X, point.Y);
            Changed("Explosion triggered once. Pause to inspect; full hazard editing and overlays arrive in M6.");
            return true;
        }

        public bool SetHybrid(bool enabled)
        {
            if (!IsBuilding) return false;
            _simulation.Config.HybridEnabled = enabled;
            Changed("Hybrid " + (enabled ? "enabled" : "disabled") + "; individual navigation remains active in both modes.", true);
            return true;
        }

        public IReadOnlyList<float> BehaviourValues => Array.AsReadOnly(BehaviourControls.Read(_simulation.Config.Agent.Behavior));
        public bool SetBehaviour(int index, float value)
        {
            if (!IsBuilding) return false;
            BehaviourControls.Set(_simulation.Config.Agent.Behavior, index, value);
            Changed("Behaviour updated; values apply when finalised.", true);
            return true;
        }
        public bool ResetBehaviour()
        {
            if (!IsBuilding) return false;
            _simulation.Config.Agent.Behavior.Reset();
            Changed("Behaviour defaults restored.", true);
            return true;
        }
        public void ToggleInspector() => _inspector.ToggleVisibility();
        public void SelectInspectorTab(int tab)
        {
            if (tab < 0 || tab > 3) throw new ArgumentOutOfRangeException(nameof(tab));
            _inspector.SelectTab((CrowdSim.OpenGL.UI.InspectorTab)tab);
        }
        public void ShowAgentInspector() => _inspector.Show(CrowdSim.OpenGL.UI.InspectorTab.Agent);
        public void SelectAgent(Vector2 point)
        {
            Agent? selected = _simulation.Agents.Where(a => a.IsActive && Vector2.Distance(a.Position, point) <= 1.2f)
                .OrderBy(a => Vector2.DistanceSquared(a.Position, point)).FirstOrDefault();
            SelectedAgentId = selected?.AgentId;
            if (selected != null) _inspector.SelectTab(CrowdSim.OpenGL.UI.InspectorTab.Agent);
        }
        public string SelectedAgentSummary => SelectedAgentId.HasValue
            ? "Agent " + SelectedAgentId + "\nState: " + _simulation.Agents[SelectedAgentId.Value].State +
                "\nPosture: " + _simulation.Agents[SelectedAgentId.Value].Posture +
                "\nRise: " + _simulation.Agents[SelectedAgentId.Value].StandUpProgress.ToString("P0") +
                "\nPanic: " + _simulation.Agents[SelectedAgentId.Value].PanicLevel.ToString("F2")
            : "No agent selected. Click a runtime capsule to select it.";
    }
}
