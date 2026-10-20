#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using CrowdSim.Diagnostics;

namespace CrowdSim.Workspace
{
    public sealed partial class WorkspaceSession
    {
        private readonly BlastGeometryCache _blastCache = new BlastGeometryCache();
        private readonly bool[] _zoneVisibility = { true, true, true, true, true };
        public ExplosionFrame Blast { get; private set; } = null!;
        public bool ExplosionArmed { get; private set; }
        public bool CanArmExplosion => Running && Scenario == ScenarioType.Explosion && !_simulation.Environment.HasExplosion();
        public bool CanEditBlastRadii => Scenario == ScenarioType.Explosion && !_simulation.Environment.HasExplosion();
        public bool ShowBlastZones { get; set; } = true;
        public bool ShowShockFront { get; set; } = true;
        public bool ShowDust { get; set; } = true;
        public IReadOnlyList<bool> ZoneVisibility => Array.AsReadOnly(_zoneVisibility);
        public int BlastGeometryBuildCount => _blastCache.BuildCount;
        public IReadOnlyList<float> BlastRadii => Array.AsReadOnly(new[] { _simulation.Config.Agent.HardKillRadius,
            _simulation.Config.Agent.HeatIncapacityRadius, _simulation.Config.Agent.FallenRadius,
            _simulation.Config.Agent.InjuryRadius, _simulation.Config.Agent.HeatRadius });
        public void SetZoneVisible(int index, bool visible) => _zoneVisibility[index] = visible;
        public bool ArmExplosion()
        {
            if (!CanArmExplosion) { Message = "Start an Explosion run before arming; only one detonation is allowed."; return false; }
            ExplosionArmed = true; Message = "Explosion armed: click clear floor inside the room. Escape or Cancel Explosion cancels."; return true;
        }
        public void CancelExplosion()
        { ExplosionArmed = false; Message = "Explosion tool cancelled; no event applied."; }
        // Include the committed representable float at a tightly packed boundary; avoid inverted clamp intervals.
        public float BlastMinimum(int index) => Math.Min(BlastRadii[index], index == 0 ? .1f : BlastRadii[index - 1] + .1f);
        public float BlastMaximum(int index) => Math.Max(BlastRadii[index], index == 4 ? 200 : BlastRadii[index + 1] - .1f);
        /// <summary>Validate a text commit against its neighbors; rejection leaves config/checkpoint/dirty state untouched.</summary>
        public bool SetBlastRadius(int index, string text)
        {
            if (index < 0 || index > 4) throw new ArgumentOutOfRangeException(nameof(index));
            if (!CanEditBlastRadii) { Message = "Radii are read-only after detonation. Zone visibility remains adjustable."; return false; }
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value))
            { Message = "Enter a finite numeric radius in metres."; return false; }
            if (value < BlastMinimum(index) || value > BlastMaximum(index))
            { Message = "Radius must be between " + BlastMinimum(index).ToString("0.##", CultureInfo.InvariantCulture) + " and " + BlastMaximum(index).ToString("0.##", CultureInfo.InvariantCulture) + " m."; return false; }
            if (value == BlastRadii[index]) { Message = "Radius unchanged."; return true; }
            float[] values = new float[5]; for (int i = 0; i < 5; i++) values[i] = BlastRadii[i];
            values[index] = value; return ApplyBlastRadii(values);
        }
        public bool AdjustBlastRadius(int index, int direction)
        {
            if (!CanEditBlastRadii) { Message = "Radii are read-only after detonation."; return false; }
            float next = Math.Clamp(BlastRadii[index] + Math.Sign(direction) * .1f, BlastMinimum(index), BlastMaximum(index));
            return SetBlastRadius(index, next.ToString("R", CultureInfo.InvariantCulture));
        }
        public bool ResetBlastRadii()
        {
            if (!CanEditBlastRadii) { Message = "Radii are read-only after detonation."; return false; }
            return ApplyBlastRadii(new[] { AgentConfig.DefaultHardKillRadius, AgentConfig.DefaultHeatIncapacityRadius,
                AgentConfig.DefaultFallenRadius, AgentConfig.DefaultInjuryRadius, AgentConfig.DefaultHeatRadius });
        }
        private bool ApplyBlastRadii(float[] radii)
        {
            // Near the 200 m ceiling float rounding can invert the reference setter's nested clamp bounds.
            // Probe before changing the live config/checkpoint; retain the original algorithm and report rejection.
            try { new AgentConfig().SetBlastZoneRadii(radii[0], radii[1], radii[2], radii[3], radii[4]); }
            catch (ArgumentException)
            { Message = "These radii exceed the model's representable ordered range. Reduce an inner radius."; return false; }
            FinalizedSceneCheckpoint? updated = null;
            if (_checkpoint != null)
            {
                // Rebase only radii on the old authored scene, never capture moved runtime furniture/people.
                Simulation authored = _checkpoint.CreateReadySimulation();
                authored.Config.Agent.SetBlastZoneRadii(radii[0], radii[1], radii[2], radii[3], radii[4]);
                updated = FinalizedSceneCheckpoint.Capture(authored, PlannedCount, Name);
            }
            _simulation.Config.Agent.SetBlastZoneRadii(radii[0], radii[1], radii[2], radii[3], radii[4]);
            if (updated != null) _checkpoint = updated;
            Changed("Blast radii updated: Kill < Incapacitation < Fallen < Injury < Panic. Locked after detonation.", true);
            return true;
        }
    }
}
