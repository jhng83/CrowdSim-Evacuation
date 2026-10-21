#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using CrowdSim.Core;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    public enum ViewShape { Box, Cylinder, Capsule, Sphere }
    public enum ViewKind { Floor, Wall, Block, Exit, Agent, Table, Chair, Stage, Barricade, Debris, Fume, Source, AgentDetail }

    /// <summary>Detached value description. Coordinates map (ground X,Y,height) to Unity X,Y,Z.</summary>
    public sealed class ViewBody
    {
        public string Id { get; }
        public ViewShape Shape { get; }
        public ViewKind Kind { get; }
        public Vector3 Position { get; }
        public Vector3 Size { get; }
        public float GroundAngle { get; }
        public AgentState State { get; }
        public SeatingPosture Posture { get; }
        public float RiseProgress { get; }
        public ChairState MotionState { get; }
        public float Energy { get; }
        public ViewBody(string id, ViewShape shape, ViewKind kind, Vector3 position, Vector3 size,
            float groundAngle = 0f, AgentState state = AgentState.Wandering,
            SeatingPosture posture = SeatingPosture.Walking, float riseProgress = 0f, ChairState motionState = ChairState.Upright, float energy = 1f)
        {
            Id = id; Shape = shape; Kind = kind; Position = position; Size = size; GroundAngle = groundAngle; State = state;
            Posture = posture; RiseProgress = riseProgress; MotionState = motionState; Energy = energy;
        }
    }

    /// <summary>Read-only scene values; no references to Agent, FurnitureBody or mutable engine collections escape.</summary>
    public sealed class PresentationFrame
    {
        public int Tick { get; }
        public float Width { get; }
        public float Length { get; }
        public int Population { get; }
        public int Exited { get; }
        public int Injured { get; }
        public int Dead { get; }
        public float MaximumExposure { get; }
        public int OpenSpaceAgents { get; }
        public bool HazardReleased { get; }
        public IReadOnlyList<ViewBody> Bodies { get; }
        public IReadOnlyList<AgentVisualFrame> Agents { get; }
        public IReadOnlyList<FurnitureBodyFrame> FurnitureBodies { get; }
        public int SeatedAgents { get; }
        public int RisingAgents { get; }

        private PresentationFrame(Simulation simulation, List<ViewBody> bodies)
        {
            Tick = simulation.FrameIndex; Width = simulation.Environment.Width; Length = simulation.Environment.Height;
            Population = simulation.Agents.Count; Exited = simulation.Agents.Count(a => a.State == AgentState.Exited);
            Injured = simulation.Agents.Count(a => a.State == AgentState.Injured); Dead = simulation.Agents.Count(a => a.State == AgentState.Dead);
            MaximumExposure = simulation.Agents.Count == 0 ? 0f : simulation.Agents.Max(a => a.CumulativeToxicExposure);
            OpenSpaceAgents = simulation.HybridMovement.OpenSpaceAgents;
            HazardReleased = simulation.Environment.HasExplosion() || simulation.ToxicFumeSystem.IsReleased;
            Bodies = new ReadOnlyCollection<ViewBody>(bodies);
            Dictionary<int, Agent> agents = simulation.Agents.ToDictionary(a => a.AgentId);
            Agents = Array.AsReadOnly(simulation.Agents.Where(a => a.IsActive).Select(a => AgentVisualFrame.Capture(a, simulation.FrameIndex, agents)).ToArray());
            FurnitureBodies = Array.AsReadOnly(simulation.Environment.Furniture.SelectMany((set, i) => FurniturePresentation.Status(set, "furniture-" + i)).ToArray());
            SeatedAgents = simulation.Agents.Count == 0 ? simulation.Environment.Furniture.Sum(set => set.Chairs.Count)
                : simulation.Agents.Count(a => a.Posture == SeatingPosture.Seated);
            RisingAgents = simulation.Agents.Count(a => a.Posture == SeatingPosture.StandingUp);
        }

        public static Vector3 ToPresentation(Vector2 ground, float height) => new Vector3(ground.X, height, ground.Y);

        public static PresentationFrame Capture(Simulation simulation, bool includePlanned = false)
        {
            List<ViewBody> bodies = new List<ViewBody>();
            bodies.Add(new ViewBody("floor", ViewShape.Box, ViewKind.Floor,
                new Vector3(simulation.Environment.Width / 2f, -0.08f, simulation.Environment.Height / 2f),
                new Vector3(simulation.Environment.Width, 0.12f, simulation.Environment.Height)));
            if (includePlanned && simulation.Agents.Count == 0)
            {
                int planned = 0;
                foreach (Vector2 position in simulation.Environment.AgentSpawnGroups.SelectMany(g => g.Positions))
                    bodies.Add(new ViewBody("planned-" + planned++, ViewShape.Capsule, ViewKind.Agent,
                        ToPresentation(position, 0.85f), new Vector3(0.4f, 1.7f, 0.4f)));
                foreach (FurnitureChair chair in simulation.Environment.Furniture.SelectMany(set => set.Chairs))
                    FurniturePresentation.AddSeatedAgent(bodies, "planned-" + planned++, chair.StartPosition,
                        chair.StartHeading, SeatingPosture.Seated, 0, AgentState.Wandering);
            }
            int index = 0;
            // Perimeter geometry stays in the model and is intentionally absent from the view.
            foreach (Wall wall in simulation.Environment.Blockages.Concat(simulation.Environment.BlueWalls))
                bodies.Add(new ViewBody("wall-" + index++, ViewShape.Box, ViewKind.Wall,
                    new Vector3((wall.X1 + wall.X2) / 2f, 1f, (wall.Y1 + wall.Y2) / 2f),
                    new Vector3(wall.X2 - wall.X1, 2f, wall.Y2 - wall.Y1)));
            foreach (CircleBlockage circle in simulation.Environment.CircularBlockages.Where(c => !c.IsFurniture))
                bodies.Add(new ViewBody("circle-" + index++, ViewShape.Cylinder, ViewKind.Block,
                    ToPresentation(circle.Center, 1f), new Vector3(circle.Diameter, 2f, circle.Diameter)));
            foreach (Exit exit in simulation.Environment.Exits)
            {
                bool vertical = exit.Side == "E" || exit.Side == "W";
                bodies.Add(new ViewBody("exit-" + exit.Name, ViewShape.Box, ViewKind.Exit, ToPresentation(exit.Position, 0.04f),
                    new Vector3(vertical ? 0.35f : exit.Size * 2f, 0.08f, vertical ? exit.Size * 2f : 0.35f)));
            }
            foreach (Agent agent in simulation.Agents.Where(a => a.State != AgentState.Exited))
            {
                if (agent.Posture != SeatingPosture.Walking && agent.Chair is FurnitureChair seat)
                    FurniturePresentation.AddSeatedAgent(bodies, "agent-" + agent.AgentId, agent.Position,
                        seat.StartHeading, agent.Posture, agent.StandUpProgress, agent.State);
                else
                    bodies.Add(new ViewBody("agent-" + agent.AgentId, ViewShape.Capsule, ViewKind.Agent,
                        ToPresentation(agent.Position, 0.85f), new Vector3(0.4f, 1.7f, 0.4f), state: agent.State));
            }
            index = 0;
            foreach (FurnitureSet furniture in simulation.Environment.Furniture)
                FurniturePresentation.AddSet(bodies, furniture, "furniture-" + index++);
            index = 0;
            foreach (Debris fragment in simulation.Debris)
            {
                float energy = Math.Clamp(fragment.Energy, 0, 1);
                float halfWidth = Math.Max(.036f, fragment.Radius * (.18f + .12f * energy));
                float halfLength = Math.Max(.073f, fragment.Radius * (.50f + .35f * energy));
                float top = .061f + halfWidth * 2;
                float angle = fragment.Velocity.LengthSquared() > 1e-6f ? MathF.Atan2(fragment.Velocity.Y, fragment.Velocity.X) : 0;
                bodies.Add(new ViewBody("debris-" + index++, ViewShape.Box, ViewKind.Debris,
                    ToPresentation(fragment.Position, (.02f + top) * .5f), new Vector3(halfLength * 2, top - .02f, halfWidth * 2),
                    groundAngle: angle, energy: energy));
            }
            if (simulation.Environment.Explosion is Explosion explosion)
                bodies.Add(new ViewBody("event", ViewShape.Cylinder, ViewKind.Source,
                    ToPresentation(explosion.Position, 0.04f), new Vector3(1f, 0.08f, 1f)));
            if (simulation.ToxicFumeSystem.SourcePosition is Vector2 source)
                bodies.Add(new ViewBody("source", ViewShape.Cylinder, ViewKind.Source,
                    ToPresentation(source, 0.05f), new Vector3(0.8f, 0.1f, 0.8f)));
            return new PresentationFrame(simulation, bodies);
        }
    }
}
