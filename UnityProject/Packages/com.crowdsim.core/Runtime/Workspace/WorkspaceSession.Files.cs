#nullable enable
using System;
using CrowdSim.Core;
using CrowdSim.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace CrowdSim.Workspace
{
    public sealed partial class WorkspaceSession
    {
        private int _requestedAgentCount;
        private FinalizedSceneCheckpoint? _checkpoint;
        public string Name { get; private set; } = "Untitled";
        public bool IsDirty { get; private set; }
        public bool CanReplay => _checkpoint != null;
        public bool HasFumeCache => _simulation.ToxicFumeSystem.IsCacheValidFor(_simulation);
        public bool FumeReleased => _simulation.ToxicFumeSystem.IsReleased;
        public bool UsesRandomStarts => _simulation.Environment.PlannedAgentCount == 0 && _requestedAgentCount > 0;

        /// <summary>Validate into an independent builder before exposing it to the application.</summary>
        public static WorkspaceSession FromJson(string json)
        {
            ScenarioDefinition definition = ScenarioJson.Deserialize(json);
            Simulation model = new Simulation(config: definition.CreateConfig(), randomSeed: definition.RandomSeed);
            definition.ApplyLayout(model);
            return new WorkspaceSession(model, definition.AgentCount, definition.Name);
        }
        private WorkspaceSession(Simulation model, int requested, string name)
        {
            _simulation = model;
            _requestedAgentCount = requested;
            Name = name;
            Frame = Diagnostics.PresentationFrame.Capture(model, true);
            Blast = new Diagnostics.ExplosionFrame(model, _blastCache);
            Fume = Diagnostics.FumeFrame.Capture(model, true);
            Message = "Loaded paused in the builder. Review the authored layout, then finalise.";
        }
        /// <summary>Runtime save uses the private finalised definition, never current damage/positions.</summary>
        public string ExportAuthoredJson(string name)
        {
            ScenarioDefinition definition = _checkpoint == null
                ? ScenarioDefinition.Capture(_simulation, PlannedCount, name) : _checkpoint.Definition;
            JObject document = JObject.Parse(ScenarioJson.Serialize(definition));
            document["name"] = name;
            return ScenarioJson.Serialize(ScenarioJson.Deserialize(document.ToString()));
        }
        public void MarkSaved(string name)
        {
            Name = name;
            IsDirty = false;
        }
        /// <summary>Reconstruct and validate first; only then replace the current model with frame zero.</summary>
        public bool Replay()
        {
            if (_checkpoint == null) { Message = "Finalise the scene before replaying."; return false; }
            Simulation ready = _checkpoint.CreateReadySimulation();
            _simulation = ready;
            SelectedAgentId = null;
            CameraMode = Diagnostics.AgentCameraMode.Overview;
            ExplosionArmed = false;
            Phase = WorkspacePhase.Ready;
            _scheduler.Reset();
            Changed("Replay ready at finalised frame zero, paused, with intact furniture and unreleased hazard.");
            return true;
        }
    }
}
