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
    public enum ViewKind { Floor, Wall, Block, Exit, Agent, Table, Chair, Stage, Barricade, Debris, Fume, Source }

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
        public ViewBody(string id, ViewShape shape, ViewKind kind, Vector3 position, Vector3 size,
            float groundAngle = 0f, AgentState state = AgentState.Wandering)
        {
            Id = id; Shape = shape; Kind = kind; Position = position; Size = size; GroundAngle = groundAngle; State = state;
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

        private PresentationFrame(Simulation simulation, List<ViewBody> bodies)
        {
            Tick = simulation.FrameIndex; Width = simulation.Environment.Width; Length = simulation.Environment.Height;
            Population = simulation.Agents.Count; Exited = simulation.Agents.Count(a => a.State == AgentState.Exited);
            Injured = simulation.Agents.Count(a => a.State == AgentState.Injured); Dead = simulation.Agents.Count(a => a.State == AgentState.Dead);
            MaximumExposure = simulation.Agents.Count == 0 ? 0f : simulation.Agents.Max(a => a.CumulativeToxicExposure);
            OpenSpaceAgents = simulation.HybridMovement.OpenSpaceAgents;
            HazardReleased = simulation.Environment.HasExplosion() || simulation.ToxicFumeSystem.IsReleased;
            Bodies = new ReadOnlyCollection<ViewBody>(bodies);
        }

        public static Vector3 ToPresentation(Vector2 ground, float height) => new Vector3(ground.X, height, ground.Y);

        public static PresentationFrame Capture(Simulation simulation)
        {
            List<ViewBody> bodies = new List<ViewBody>();
            bodies.Add(new ViewBody("floor", ViewShape.Box, ViewKind.Floor,
                new Vector3(simulation.Environment.Width / 2f, -0.08f, simulation.Environment.Height / 2f),
                new Vector3(simulation.Environment.Width, 0.12f, simulation.Environment.Height)));
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
                    new Vector3(vertical ? 0.35f : exit.Size, 0.08f, vertical ? exit.Size : 0.35f)));
            }
            foreach (Agent agent in simulation.Agents.Where(a => a.State != AgentState.Exited))
                bodies.Add(new ViewBody("agent-" + agent.AgentId, ViewShape.Capsule, ViewKind.Agent,
                    ToPresentation(agent.Position, 0.85f), new Vector3(0.4f, 1.7f, 0.4f), state: agent.State));
            index = 0;
            foreach (FurnitureSet furniture in simulation.Environment.Furniture)
            {
                string id = "furniture-" + index++;
                if (furniture.StaticObstacle is Wall fixedWall && furniture.Table == null)
                {
                    bool barricade = furniture.Kind == FurnitureKind.Barricade;
                    float height = barricade ? FurnitureSet.BarricadeHeight : FurnitureSet.StageHeight;
                    Vector3 size = barricade ? new Vector3(FurnitureSet.BarricadeLength, height, FurnitureSet.BarricadeWidth)
                        : new Vector3(fixedWall.X2 - fixedWall.X1, height, fixedWall.Y2 - fixedWall.Y1);
                    bodies.Add(new ViewBody(id, ViewShape.Box, barricade ? ViewKind.Barricade : ViewKind.Stage,
                        ToPresentation(furniture.Center, height / 2f), size, barricade ? furniture.Rotation : 0f));
                }
                if (furniture.Table is FurnitureBody table && table.State != ChairState.Destroyed)
                {
                    Vector3 size = table is RectangularFurnitureBody rectangle
                        ? new Vector3(rectangle.HalfExtents.X * 2f, 0.78f, rectangle.HalfExtents.Y * 2f)
                        : new Vector3(table.Radius * 2f, 0.78f, table.Radius * 2f);
                    bodies.Add(new ViewBody(id + "-table", table is RectangularFurnitureBody ? ViewShape.Box : ViewShape.Cylinder,
                        ViewKind.Table, ToPresentation(table.Position, table.Height + 0.39f), size));
                }
                int chairIndex = 0;
                foreach (FurnitureChair chair in furniture.Chairs)
                {
                    string chairId = id + "-chair-" + chairIndex++;
                    if (chair.State == ChairState.Destroyed) continue;
                    float height = chair.State == ChairState.Fallen ? 0.15f : 0.65f;
                    bodies.Add(new ViewBody(chairId, ViewShape.Box, ViewKind.Chair,
                        ToPresentation(chair.Position, chair.Height + height / 2f), new Vector3(0.6f, height, 0.6f), chair.Heading));
                }
            }
            index = 0;
            foreach (Debris fragment in simulation.Debris)
                bodies.Add(new ViewBody("debris-" + index++, ViewShape.Sphere, ViewKind.Debris,
                    ToPresentation(fragment.Position, 0.2f), new Vector3(0.25f)));
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
