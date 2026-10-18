#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;
using CrowdSim.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace CrowdSim.Diagnostics
{
    /// <summary>Preset construction and observations shared by Unity and cloud diagnostics.</summary>
    public static class DiagnosticPresets
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        public static Simulation CreatePreset(string preset, bool hybrid, string? toxicFixtureJson)
        {
            Simulation simulation;
            if (preset == "toxic-205")
            {
                ScenarioDefinition definition = ScenarioJson.Deserialize(toxicFixtureJson ?? throw new ArgumentNullException(nameof(toxicFixtureJson)));
                simulation = new Simulation(0, definition.CreateConfig(), definition.RandomSeed);
                definition.ApplyLayout(simulation);
                simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
                Require(simulation.Agents.Count == 205, "The fume regression fixture must retain 205 occupants.");
                simulation.PrecomputeToxicFume();
            }
            else if (preset == "explosion-open" || preset == "explosion-furnished")
            {
                simulation = new Simulation(randomSeed: 731);
                Require(simulation.Environment.AddExitFromClick(0.2f, 17f, 5), "West exit placement failed.");
                Require(simulation.Environment.AddExitFromClick(simulation.Environment.Width - 0.2f, 17f, 3), "East exit placement failed.");
                if (preset == "explosion-furnished")
                {
                    Require(simulation.Environment.AddFurniture(FurnitureKind.RoundTableSet, new Vector2(28f, 17f)), "Round table placement failed.");
                    Require(simulation.Environment.AddFurniture(FurnitureKind.SingleChair, new Vector2(36f, 20f)), "Chair placement failed.");
                    Require(simulation.Environment.AddFurniture(FurnitureKind.RectangularTable, new Vector2(24f, 12f)), "Rectangular table placement failed.");
                    Require(simulation.Environment.AddFurniture(FurnitureKind.Stage, new Vector2(42f, 27f)), "Stage placement failed.");
                    Require(simulation.Environment.AddFurniture(FurnitureKind.Barricade, new Vector2(15f, 12f), MathF.PI / 4f), "Angled barricade placement failed.");
                    Require(simulation.Environment.AddBlockageFromClick(20f, 20f, 2), "Square blockage placement failed.");
                    Require(simulation.Environment.AddCircularBlockageFromClick(12f, 23f), "Circular blockage placement failed.");
                }
                Require(simulation.Environment.AddAgentSpawnGroupFromClick(10f, 10f, 50), "Floor population placement failed.");
                simulation.BuildWallGrid();
                simulation.SpawnAgentsAtPositions(simulation.Environment.GetPlannedAgentPositions());
            }
            else throw new ArgumentOutOfRangeException(nameof(preset));
            simulation.Config.HybridEnabled = hybrid;
            return simulation;
        }

        private static object ObservedState(Simulation simulation, bool sampled = false) => new
        {
            SampledAgentsOnly = sampled,
            simulation.FrameIndex,
            simulation.Running,
            simulation.ScenarioType,
            simulation.ExplosionTriggerFrame,
            Fume = new
            {
                simulation.ToxicFumeSystem.IsReleased,
                simulation.ToxicFumeSystem.ReleaseFrame,
                simulation.ToxicFumeSystem.GridCellCount,
                simulation.ToxicFumeSystem.ReachableCellCount
            },
            Counts = ((AgentState[])Enum.GetValues(typeof(AgentState))).ToDictionary(s => s.ToString(), s => simulation.Agents.Count(a => a.State == s)),
            Agents = simulation.Agents.OrderBy(a => a.AgentId)
                .Where(a => !sampled || a.AgentId < 3 || a.AgentId >= simulation.Agents.Count - 3).Select(a => new
                {
                    a.AgentId,
                    a.Position,
                    a.Velocity,
                    a.State,
                    a.Posture,
                    a.Intent,
                    a.PanicLevel,
                    Exit = a.ExitTarget?.Name,
                    a.InjurySpeedFactor,
                    a.CumulativeToxicExposure,
                    a.CurrentToxicConcentration,
                    a.ToxicMobilityFactor,
                    a.StandUpProgress,
                    a.BottleneckMode,
                    a.BottleneckPressure,
                    a.LocalNavigationMode,
                    a.AssistTargetAgentId,
                    a.RouteWaypointIndex,
                    Route = a.RouteWaypoints.ToArray()
                }).ToArray(),
            Furniture = simulation.Environment.Furniture.Select(f => new
            {
                f.Kind,
                f.Center,
                f.Rotation,
                Table = f.Table is null ? null : Body(f.Table),
                Chairs = f.Chairs.Select(c => new { Body = Body(c), c.OccupantId }).ToArray()
            }).ToArray(),
            Debris = simulation.Debris.Select(d => new { d.Position, d.Velocity, d.Ttl, d.Energy }).ToArray(),
            Exits = simulation.ExitMetrics.OrderBy(p => p.Key).Select(p => new { Name = p.Key, p.Value.Density, p.Value.Flow }).ToArray(),
            Hybrid = new
            {
                simulation.HybridMovement.OpenSpaceAgents,
                simulation.HybridMovement.DetailedAgents,
                simulation.HybridMovement.GroupedAgents,
                simulation.HybridMovement.GroupCount
            },
            simulation.DebrisHits,
            simulation.DebrisInjuries,
            simulation.DebrisIncapacitations,
            simulation.DebrisFatalities
        };

        private static object Body(FurnitureBody body) => new { body.Position, body.Velocity, body.Height, body.State, body.Heading };

        public static JObject Observe(Simulation simulation) => JObject.FromObject(ObservedState(simulation, true));

        public static JArray Traits(Simulation simulation) => JArray.FromObject(simulation.Agents.Select(a => new
        {
            a.AgentId,
            a.BaseSpeed,
            a.ReactionDelayFrames,
            a.Strategy,
            a.ThreatSensitivity,
            a.FollowTendency,
            a.HelpingTendency,
            a.RouteFamiliarity,
            a.UrgencyBias,
            a.StressRecoveryBias,
            a.PersonalSpaceBias,
            a.Assertiveness,
            a.PatienceBias
        }).ToArray());
    }
}
