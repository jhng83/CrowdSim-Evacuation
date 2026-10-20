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
    public enum PlacementTool { Select, Exit, Square, Circle, Agents, Explosion }
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
    public sealed class WorkspaceSession
    {
        private readonly Simulation _simulation;
        private readonly InspectorPanelState _inspector = new InspectorPanelState();
        private readonly FixedStepScheduler _scheduler = new FixedStepScheduler();
        private double _speed = 1d;
        public WorkspacePhase Phase { get; private set; } = WorkspacePhase.Building;
        public bool IsBuilding => Phase == WorkspacePhase.Building;
        public bool CanResize => IsBuilding && _simulation.Environment.CanResizeRoom;
        public bool Running => Phase == WorkspacePhase.Running;
        public float StepSeconds => _simulation.Config.Agent.Dt;
        public int Tick => _simulation.FrameIndex;
        public int PlannedCount => _simulation.Environment.PlannedAgentCount;
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
            : Scenario == ScenarioType.ToxicFume ? "Toxic Fume source and precomputation controls arrive in M7. Use the M2 diagnostic for fume runs."
            : "Ready to finalise.";
        public bool CanFinalise => IsBuilding && ExitCount > 0 && PlannedCount > 0 && Scenario == ScenarioType.Explosion;

        public WorkspaceSession(ScenarioType scenario = ScenarioType.Explosion, int seed = 42)
        {
            _simulation = new Simulation(randomSeed: seed);
            _simulation.SetScenarioType(scenario);
            Frame = PresentationFrame.Capture(_simulation, true);
        }

        private void Changed(string message)
        {
            Message = message;
            Revision++;
            Frame = PresentationFrame.Capture(_simulation, IsBuilding);
        }

        public bool Resize(float width, float length)
        {
            if (!CanResize || !float.IsFinite(width) || !float.IsFinite(length) || width < 10 || width > 160 || length < 10 || length > 120)
            { Message = "Resize rejected: use 10–160 m width / 10–120 m length in an empty builder."; return false; }
            bool result = _simulation.Environment.TryResizeRoom(width, length);
            _simulation.BuildWallGrid();
            Changed("Room size updated in metres.");
            return result;
        }

        private static bool ApplyPlacement(Simulation simulation, PlacementTool tool, Vector2 point, int option)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;
            switch (tool)
            {
                case PlacementTool.Exit: return (option == 1 || option == 3 || option == 5) && simulation.Environment.AddExitFromClick(point.X, point.Y, option);
                case PlacementTool.Square: return (option == 1 || option == 2) && simulation.Environment.AddBlockageFromClick(point.X, point.Y, option);
                case PlacementTool.Circle: return simulation.Environment.AddCircularBlockageFromClick(point.X, point.Y);
                case PlacementTool.Agents: return (option == 1 || option == 5 || option == 15 || option == 50) && simulation.Environment.AddAgentSpawnGroupFromClick(point.X, point.Y, option);
                default: return false;
            }
        }

        public PlacementPreview Preview(PlacementTool tool, Vector2 point, int option)
        {
            if (!IsBuilding) return new PlacementPreview(false, Array.Empty<ViewBody>());
            ScenarioDefinition definition = ScenarioDefinition.Capture(_simulation, 0, "Preview");
            Simulation candidate = new Simulation(config: definition.CreateConfig(), randomSeed: definition.RandomSeed);
            definition.ApplyLayout(candidate);
            bool valid = ApplyPlacement(candidate, tool, point, option);
            HashSet<(ViewKind, ViewShape, Vector3, Vector3, float)> existing = new HashSet<(ViewKind, ViewShape, Vector3, Vector3, float)>(
                Frame.Bodies.Select(b => (b.Kind, b.Shape, b.Position, b.Size, b.GroundAngle)));
            return new PlacementPreview(valid, valid ? PresentationFrame.Capture(candidate, true).Bodies.Where(b => !existing.Contains((b.Kind, b.Shape, b.Position, b.Size, b.GroundAngle))) : Array.Empty<ViewBody>());
        }

        public bool Place(PlacementTool tool, Vector2 point, int option)
        {
            if (!IsBuilding) { Message = "Geometry is locked after finalisation."; return false; }
            bool result = ApplyPlacement(_simulation, tool, point, option);
            if (!result) { Message = "Placement rejected: check boundary, overlap, occupancy and available floor."; return false; }
            _simulation.BuildWallGrid();
            Changed(tool + " placed. Ctrl+Z undoes the last placement.");
            return true;
        }

        public bool Undo()
        {
            if (!IsBuilding || !_simulation.Environment.UndoLast()) { Message = "Nothing can be undone here."; return false; }
            _simulation.BuildWallGrid();
            Changed("Last placement undone.");
            return true;
        }

        public bool Finalise()
        {
            if (!CanFinalise) { Message = Readiness; return false; }
            _simulation.SpawnAgentsAtPositions(_simulation.Environment.GetPlannedAgentPositions());
            _simulation.SetRunning(false);
            Phase = WorkspacePhase.Ready;
            _scheduler.Reset();
            Changed("Scene finalised at frame zero. Start, then arm an Explosion and click the floor.");
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
            Changed("Hybrid " + (enabled ? "enabled" : "disabled") + "; individual navigation remains active in both modes.");
            return true;
        }

        public IReadOnlyList<float> BehaviourValues => Array.AsReadOnly(BehaviourControls.Read(_simulation.Config.Agent.Behavior));
        public bool SetBehaviour(int index, float value)
        {
            if (!IsBuilding) return false;
            BehaviourControls.Set(_simulation.Config.Agent.Behavior, index, value);
            Changed("Behaviour updated; values apply when finalised.");
            return true;
        }
        public bool ResetBehaviour()
        {
            if (!IsBuilding) return false;
            _simulation.Config.Agent.Behavior.Reset();
            Changed("Behaviour defaults restored.");
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
                "\nPanic: " + _simulation.Agents[SelectedAgentId.Value].PanicLevel.ToString("F2")
            : "No agent selected. Click a runtime capsule to select it.";
    }
}
