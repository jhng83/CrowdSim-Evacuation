#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    public readonly struct ExitDiagnostic
    {
        public string Name { get; }
        public float Density { get; }
        public float Flow { get; }
        public ExitDiagnostic(string name, float density, float flow) { Name = name; Density = density; Flow = flow; }
    }
    public sealed class AgentVectors
    {
        public int Id { get; }
        public Vector2 Position { get; }
        public Vector2 Heading { get; }
        public Vector2 Desired { get; }
        public Vector2 Avoidance { get; }
        public Vector2 Herd { get; }
        public Vector2 Force { get; }
        public float Congestion { get; }
        internal AgentVectors(Agent a)
        {
            Id = a.AgentId; Position = a.Position; Desired = a.LastDesiredDirection;
            Avoidance = a.LastAvoidanceDirection; Herd = a.LastHerdDirection; Force = a.LastSfmForce; Congestion = a.LocalCongestion;
            Heading = a.Velocity.LengthSquared() > 1e-8f ? a.Velocity : Desired.LengthSquared() > 1e-5f ? Desired
                : a.ExitTarget != null ? a.ExitTarget.Position - Position : Vector2.UnitX;
        }
    }
    /// <summary>Read-only diagnostic snapshot. No model references escape and no diagnostic operation steps the engine.</summary>
    public sealed class DiagnosticFrame
    {
        public int Tick { get; }
        public int Seed { get; }
        public PopulationMetrics Population { get; }
        public IReadOnlyList<DiagnosticRow> Selected { get; }
        public IReadOnlyList<DiagnosticRow> Model { get; }
        public IReadOnlyList<ExitDiagnostic> Exits { get; }
        public IReadOnlyList<AgentVectors> Agents { get; }
        public int Open { get; }
        public int Detailed { get; }
        public int Grouped { get; }
        public int Groups { get; }
        public int FieldBuilds { get; }
        public int GeometryRefreshes { get; }
        public double PhysicsMilliseconds { get; }
        public double FurnitureMilliseconds { get; }
        public double RouteMilliseconds { get; }
        public int RouteRequests { get; }
        public float VisionRange { get; }
        public float VisionHalfAngle { get; }
        public Vector2? BlastOrigin { get; }
        public bool BlastActive { get; }
        public int Squares { get; }
        public int Circles { get; }
        public int BlueWalls { get; }
        public DiagnosticFrame(Simulation model, int? selected = null)
        {
            Tick = model.FrameIndex; Seed = model.RandomSeed; Population = PopulationMetrics.Capture(model);
            Selected = AgentInspection.Capture(model, selected); Model = AgentInspection.Model(model);
            Exits = Array.AsReadOnly(model.ExitMetrics.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new ExitDiagnostic(p.Key, p.Value.Density, p.Value.Flow)).ToArray());
            Agents = Array.AsReadOnly(model.Agents.Where(a => a.IsActive).Select(a => new AgentVectors(a)).ToArray());
            Open = model.HybridMovement.OpenSpaceAgents; Detailed = model.HybridMovement.DetailedAgents;
            Grouped = model.HybridMovement.GroupedAgents; Groups = model.HybridMovement.GroupCount;
            FieldBuilds = model.HybridMovement.Fields.FieldBuilds; GeometryRefreshes = model.HybridMovement.Fields.GeometryRefreshes;
            PhysicsMilliseconds = model.LastStepMilliseconds; FurnitureMilliseconds = model.LastFurnitureMilliseconds;
            RouteMilliseconds = model.LastRoutePlanningMilliseconds; RouteRequests = model.LastRoutePlanRequests;
            VisionRange = model.Config.Agent.VisionRange; VisionHalfAngle = model.Config.Agent.VisionHalfAngleDeg;
            BlastOrigin = model.Environment.Explosion?.Position; BlastActive = BlastOrigin.HasValue && !model.BlastOver();
            Squares = model.Environment.Blockages.Count; Circles = model.Environment.CircularBlockages.Count(c => !c.IsFurniture);
            BlueWalls = model.Environment.BlueWalls.Count;
        }
    }
}
