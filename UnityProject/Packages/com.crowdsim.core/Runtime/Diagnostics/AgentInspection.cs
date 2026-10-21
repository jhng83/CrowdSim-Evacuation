#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    public sealed class DiagnosticRow
    {
        public string Label { get; }
        public string Value { get; }
        public DiagnosticRow(string label, string value) { Label = label; Value = value; }
    }
    /// <summary>Native inspector values detached as text, including conditional navigation/assistance details.</summary>
    public static class AgentInspection
    {
        public static IReadOnlyList<DiagnosticRow> Capture(Simulation simulation, int? id)
        {
            List<DiagnosticRow> rows = new List<DiagnosticRow>();
            Agent? selected = simulation.Agents.FirstOrDefault(a => a.AgentId == id && a.IsActive);
            if (selected == null) return rows.AsReadOnly();
            void Add(string label, string value) => rows.Add(new DiagnosticRow(label, value));
            Add("State", selected.State.ToString());
            Add("Posture", selected.Posture == SeatingPosture.StandingUp && selected.StandUpProgress >= 1f
                ? "Standing - waiting for a gap" : selected.Posture.ToString());
            Add("Behaviour", selected.Intent.ToString());
            Add("Panic", selected.PanicLevel.ToString("F2"));
            Add("Position", $"({selected.Position.X:F1} m, {selected.Position.Y:F1} m)");
            Add("Desired speed", $"{selected.CurrentDesiredSpeed / AgentConfig.ReferenceStepSeconds:F2} m/s");
            Add("Actual speed", $"{selected.VelocityMetersPerSecond.Length():F2} m/s");
            Add("Strategy", selected.Strategy.ToString());
            Add("Exit", selected.ExitTarget?.Name ?? "None");
            Add("Visibility", selected.VisibilityFactor.ToString("F2"));
            Add("Local congestion", selected.LocalCongestion.ToString("F2"));
            Add("Threat stimulus", selected.LastThreatPanicStimulus.ToString("F2"));
            Add("Social stimulus", selected.LastSocialPanicStimulus.ToString("F2"));
            Add("Running stimulus", selected.LastRunningPanicStimulus.ToString("F2"));
            Add("Evac awareness", selected.LastEvacuationAwarenessStimulus.ToString("F2"));
            Add("Active alarm", selected.IsActiveAlarmSource(simulation.FrameIndex) ? "YES" : "No");
            if (simulation.ScenarioType == ScenarioType.ToxicFume)
            {
                Add("Fume status", selected.ToxicExposureStatus.ToString());
                Add("Current concentration", selected.CurrentToxicConcentration.ToString("F2", CultureInfo.InvariantCulture));
                Add("Peak concentration", selected.PeakToxicConcentration.ToString("F2", CultureInfo.InvariantCulture));
                Add("Cumulative exposure", selected.CumulativeToxicExposure.ToString("F2", CultureInfo.InvariantCulture));
                Add("Mobility factor", selected.ToxicMobilityFactor.ToString("F2", CultureInfo.InvariantCulture));
            }

            int routeRemaining = Math.Max(0, selected.RouteWaypoints.Count - selected.RouteWaypointIndex);
            Add("Route waypoints", routeRemaining.ToString());
            if (selected.CurrentRouteWaypoint is Vector2 waypoint)
            {
                Add("Next waypoint", $"({waypoint.X:F1} m, {waypoint.Y:F1} m)");
            }

            Add("Stuck time", $"{selected.TimeStuck:F1} s");
            if (simulation.Config.Agent.NaturalNavigationEnabled &&
                simulation.ScenarioType is ScenarioType.Explosion or ScenarioType.ToxicFume)
            {
                Add("Adaptive navigation", selected.NavigationMemory.Stage.ToString());
                Add("No useful progress", $"{selected.NavigationMemory.NoProgressSeconds:F1} s");
                Add("Recovery attempts", selected.NavigationMemory.TotalRecoveryAttempts.ToString());
                Add("Detours completed", selected.NavigationMemory.SuccessfulDetours.ToString());
                Add("Exit reconsiderations", selected.NavigationMemory.ExitChanges.ToString());
                if (selected.NavigationMemory.DetourGoal is Vector2 detour)
                    Add("Temporary escape", $"({detour.X:F1} m, {detour.Y:F1} m)");
            }
            if (selected.LocalNavigationMode != LocalNavigationMode.Free)
            {
                string localMode = selected.LocalNavigationMode switch
                {
                    LocalNavigationMode.CasualtyBypass => "Casualty bypass",
                    LocalNavigationMode.WaitingForGap => "Waiting for gap",
                    LocalNavigationMode.SeekingGap => "Seeking wider gap",
                    LocalNavigationMode.PressingThrough => "Controlled press",
                    LocalNavigationMode.CircleFollow => "Following circular obstacle",
                    _ => selected.LocalNavigationMode.ToString(),
                };
                Add("Local navigation", localMode);

                if (selected.LocalNavigationMode is
                    LocalNavigationMode.CasualtyBypass or
                    LocalNavigationMode.WaitingForGap or
                    LocalNavigationMode.SeekingGap or
                    LocalNavigationMode.PressingThrough)
                {
                    if (selected.LocalNavigationBlockerAgentId is int localBlockerId)
                    {
                        Add("Blocked by", $"Casualty {localBlockerId}");
                    }

                    if (selected.LocalNavigationBlockedSinceFrame >= 0)
                    {
                        float blockedSeconds = Math.Max(
                            0,
                            simulation.FrameIndex - selected.LocalNavigationBlockedSinceFrame) *
                            simulation.Config.Agent.Dt;
                        Add("Blocked time", $"{blockedSeconds:F1} s");
                    }
                }
                else if (selected.LocalNavigationMode == LocalNavigationMode.CircleFollow)
                {
                    Add("Obstacle", "Circular blockage");
                    Add(
                        "Follow side",
                        selected.CircleFollowDirection >= 0 ? "Counter-clockwise" : "Clockwise");
                }
            }

            if (selected.SlowObstacleBypassGoal is Vector2 bypassGoal &&
                selected.SlowObstacleBypassAgentId is int bypassAgentId &&
                simulation.FrameIndex <= selected.SlowObstacleBypassUntilFrame)
            {
                string blockerLabel = selected.Intent == BehaviorIntent.ThreatEscape
                    ? "Casualty"
                    : "Crawler";
                Add("Local bypass", $"{blockerLabel} {bypassAgentId}");
                Add("Bypass goal", $"({bypassGoal.X:F1} m, {bypassGoal.Y:F1} m)");
            }
            else if (selected.SlowObstacleCandidateAgentId is int observedBlockerId)
            {
                string blockerLabel = selected.Intent == BehaviorIntent.ThreatEscape
                    ? "casualty"
                    : "crawler";
                Add("Local bypass", $"Watching {blockerLabel} {observedBlockerId}");
            }

            Add("Follow tendency", selected.FollowTendency.ToString("F2"));
            Add("Helping tendency", selected.HelpingTendency.ToString("F2"));
            if (selected.Intent == BehaviorIntent.AssistCasualty && selected.AssistTargetAgentId is int assistTargetId)
            {
                bool attending = selected.AssistPauseUntilFrame >= 0 &&
                                 simulation.FrameIndex < selected.AssistPauseUntilFrame;
                string phase = attending ? "HELPING (at casualty)" : "GOING TO HELP";
                Agent? assistTarget = simulation.Agents.FirstOrDefault(candidate => candidate.AgentId == assistTargetId);
                Add("Target casualty", $"Agent {assistTargetId}");
                Add("Assist phase", phase);
                if (!attending)
                {
                    Add("Assist position", selected.AssistSlotIndex switch
                    {
                        0 => "Side A",
                        1 => "Side B",
                        _ => "Unassigned",
                    });
                    Add("Recovery", selected.AssistRecoveryStage switch
                    {
                        1 => "Route retry",
                        2 => "Alternate side tried",
                        _ => "Normal approach",
                    });
                }

                if (assistTarget is not null)
                {
                    float distance = Vector2.Distance(selected.Position, assistTarget.Position);
                    Add("Distance to casualty", $"{distance:F2} m");
                }

                if (!attending && selected.AssistAssignedFrame >= 0)
                {
                    float approachSeconds = Math.Max(0, simulation.FrameIndex - selected.AssistAssignedFrame) *
                        simulation.Config.Agent.Dt;
                    Add("Approach time", $"{approachSeconds:F1} s");
                }
                else if (attending && selected.AssistAssignedFrame >= 0 && selected.AssistLastProgressFrame >= 0)
                {
                    float approachDuration = Math.Max(0, selected.AssistLastProgressFrame - selected.AssistAssignedFrame) *
                        simulation.Config.Agent.Dt;
                    Add("Approach took", $"{approachDuration:F1} s");
                }

                if (!attending && selected.AssistLastProgressFrame >= 0)
                {
                    float noProgressSeconds = Math.Max(0, simulation.FrameIndex - selected.AssistLastProgressFrame) *
                        simulation.Config.Agent.Dt;
                    Add("No-progress time", $"{noProgressSeconds:F1} s");
                    float abandonRemaining = Math.Max(0f,
                        simulation.Config.Agent.AssistanceAbandonNoProgressSeconds - noProgressSeconds);
                    Add("Give-up guard", $"{abandonRemaining:F1} s remaining");
                }
                else if (attending)
                {
                    float remainingSeconds = Math.Max(0, selected.AssistPauseUntilFrame - simulation.FrameIndex) *
                        simulation.Config.Agent.Dt;
                    Add("Helping time left", $"{remainingSeconds:F1} s");
                }
            }

            bool selectedCanReceiveHelp = selected.IsActive && selected.State is
                AgentState.Fallen or AgentState.TempIncap or AgentState.Incapacitated or AgentState.Crawling;
            if (selectedCanReceiveHelp)
            {
                int helperCount = simulation.Agents.Count(helper =>
                    helper.IsActive &&
                    helper.Intent == BehaviorIntent.AssistCasualty &&
                    helper.AssistTargetAgentId == selected.AgentId);
                int helpersAtCasualty = simulation.Agents.Count(helper =>
                    helper.IsActive &&
                    helper.Intent == BehaviorIntent.AssistCasualty &&
                    helper.AssistTargetAgentId == selected.AgentId &&
                    helper.AssistPauseUntilFrame >= 0 &&
                    simulation.FrameIndex < helper.AssistPauseUntilFrame);
                Add("Helpers assigned", $"{helperCount}/{simulation.Config.Agent.AssistanceMaxHelpersPerCasualty}");
                Add("Helpers at casualty", helpersAtCasualty.ToString());
            }

            Add("Stress susceptibility", selected.ThreatSensitivity.ToString("F2"));
            Add("Stress recovery bias", selected.StressRecoveryBias.ToString("F2"));
            Add("Personal-space bias", selected.PersonalSpaceBias.ToString("F2"));
            Add("Urgency bias", selected.UrgencyBias.ToString("F2"));
            Add("Assertiveness", selected.Assertiveness.ToString("F2"));
            Add("Patience bias", selected.PatienceBias.ToString("F2"));
            Add("Bottleneck mode", selected.BottleneckMode.ToString());
            Add("Bottleneck pressure", selected.BottleneckPressure.ToString("F2"));
            Add("Doorway wait", $"{selected.BottleneckWaitSeconds:F1} s");

            Add("Shock hit / tick", selected.ShockwaveHit + " / " + selected.ShockwaveHitFrame);
            Add("Blast shielded", selected.BlastShielded.ToString());
            return rows.AsReadOnly();
        }
        public static IReadOnlyList<DiagnosticRow> Model(Simulation simulation)
        {
            List<DiagnosticRow> rows = new List<DiagnosticRow>();
            void Add(string label, string value) => rows.Add(new DiagnosticRow(label, value));
            Add("SFM A", simulation.Config.Agent.SfmA.ToString("F0"));
            Add("SFM B", $"{simulation.Config.Agent.SfmB:F2} m");
            Add("Repulsion soft cap", "6000");
            Add("Acceleration cap", "1.21 (metric-scaled)");
            Add("Agent running ceiling", $"{simulation.Config.Agent.Behavior.MaxRunningSpeedMetersPerSecond:F2} m/s");
            Add("Blast velocity guard", $"{simulation.Config.Agent.MaxSpeed / AgentConfig.ReferenceStepSeconds:F2} m/s");
            Add("Last tick CPU", $"{simulation.LastStepMilliseconds:F1} ms");
            Add("Furniture CPU", $"{simulation.LastFurnitureMilliseconds:F1} ms");
            Add("Route planning CPU", $"{simulation.LastRoutePlanningMilliseconds:F1} ms ({simulation.LastRoutePlanRequests} searches)");
            Add("Panic speed gain", simulation.Config.Agent.PanicSpeedGain.ToString("F1"));
            Add("Evac speed floor", $"{simulation.Config.Agent.EvacuationSpeedFloorMultiplier:F2}x");
            Add("Threat gain", simulation.Config.Agent.PanicKThreat.ToString("F1"));
            Add("Social contagion", simulation.Config.Agent.PanicKSocial.ToString("F2"));
            Add("Running contagion", simulation.Config.Agent.PanicKRunning.ToString("F2"));
            Add("Panic decay tau", $"{simulation.Config.Agent.PanicDecayTimeConstantSeconds:F1} s");
            Add("Stress persistence", $"{simulation.Config.Agent.Behavior.StressPersistence:F2}x");
            Add("Personal space", $"{simulation.Config.Agent.Behavior.PersonalSpace:F2}x");
            Add("Panic compression", simulation.Config.Agent.Behavior.PanicSpaceCompression.ToString("F2"));
            Add("Emergency urgency", $"{simulation.Config.Agent.Behavior.EmergencyUrgency:F2}x");
            Add("Bottleneck assertiveness", simulation.Config.Agent.Behavior.BottleneckAssertiveness.ToString("F2"));
            Add("Pressure sensitivity", $"{simulation.Config.Agent.Behavior.BottleneckPressureSensitivity:F2}x");
            Add("Pressure compression", simulation.Config.Agent.Behavior.BottleneckSpaceCompression.ToString("F2"));
            Add("Max herding", simulation.Config.Agent.HerdingLambdaMax.ToString("F2"));
            Add("Route commitment", $"{simulation.Config.Agent.RouteCommitmentSeconds:F1} s");

            return rows.AsReadOnly();
        }
    }
}
