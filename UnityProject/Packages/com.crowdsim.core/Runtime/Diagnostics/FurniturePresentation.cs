#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using CrowdSim.Core.Model;

namespace CrowdSim.Diagnostics
{
    /// <summary>Detached physical furniture status, retained after destruction even when its views disappear.</summary>
    public sealed class FurnitureBodyFrame
    {
        public string Id { get; }
        public ViewKind Kind { get; }
        public Vector3 Position { get; }
        public float Heading { get; }
        public ChairState State { get; }
        public bool IsFixed { get; }
        public int? OccupantId { get; }
        public FurnitureBodyFrame(string id, ViewKind kind, Vector3 position, float heading,
            ChairState state, bool isFixed, int? occupantId = null)
        { Id = id; Kind = kind; Position = position; Heading = heading; State = state; IsFixed = isFixed; OccupantId = occupantId; }
    }

    /// <summary>Reference-shaped furniture and basic seated/rising views, driven solely by engine values.</summary>
    public static class FurniturePresentation
    {
        private static Vector2 Local(Vector2 center, float heading, float forward, float side)
            => center + new Vector2(MathF.Cos(heading) * forward - MathF.Sin(heading) * side,
                MathF.Sin(heading) * forward + MathF.Cos(heading) * side);

        private static void Box(List<ViewBody> views, string id, ViewKind kind, Vector2 center, float heading,
            float x1, float x2, float z1, float z2, float bottom, float top, ChairState state = ChairState.Upright,
            AgentState agentState = AgentState.Wandering)
        {
            Vector2 position = Local(center, heading, (x1 + x2) / 2, (z1 + z2) / 2);
            views.Add(new ViewBody(id, ViewShape.Box, kind, PresentationFrame.ToPresentation(position, (bottom + top) / 2),
                new Vector3(x2 - x1, top - bottom, z2 - z1), heading, agentState, motionState: state));
        }

        public static void AddSet(List<ViewBody> views, FurnitureSet set, string id)
        {
            if (set.Kind == FurnitureKind.Stage && set.StaticObstacle is Wall stage)
            {
                views.Add(new ViewBody(id, ViewShape.Box, ViewKind.Stage, PresentationFrame.ToPresentation(set.Center, FurnitureSet.StageHeight / 2),
                    new Vector3(stage.X2 - stage.X1, FurnitureSet.StageHeight, stage.Y2 - stage.Y1)));
                return;
            }
            if (set.Kind == FurnitureKind.Barricade)
            {
                Box(views, id, ViewKind.Barricade, set.Center, set.Rotation, -.75f, .75f, -.2f, .2f, 0, .16f);
                Box(views, id + "-upper", ViewKind.Barricade, set.Center, set.Rotation, -.6f, .6f, -.09f, .09f, .16f, 1);
                return;
            }
            if (set.Table is FurnitureBody table && table.State != ChairState.Destroyed)
            {
                string tableId = id + "-table";
                bool tipped = table.State == ChairState.Airborne || table.State == ChairState.Fallen;
                float h = table.Height;
                if (table is RectangularFurnitureBody)
                {
                    if (!tipped)
                        for (int x = -1; x <= 1; x += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            float forward = x * 1.4f, side = z * .4f;
                            Box(views, tableId + "-leg-" + x + "-" + z, ViewKind.Table, table.Position, table.Heading,
                                forward - .045f, forward + .045f, side - .045f, side + .045f, h, h + .72f, table.State);
                        }
                    Box(views, tableId, ViewKind.Table, table.Position, table.Heading, -1.5f, 1.5f, -.5f, .5f,
                        h + (tipped ? .02f : .72f), h + (tipped ? .18f : .78f), table.State);
                }
                else
                {
                    float top = h + (tipped ? .12f : .72f);
                    Box(views, tableId + "-pedestal", ViewKind.Table, table.Position, table.Heading,
                        -.13f, .13f, -.13f, .13f, h, top, table.State);
                    views.Add(new ViewBody(tableId, ViewShape.Cylinder, ViewKind.Table,
                        PresentationFrame.ToPresentation(table.Position, top + .03f),
                        new Vector3(table.Radius * 2, .06f, table.Radius * 2), table.Heading, motionState: table.State));
                }
            }
            for (int i = 0; i < set.Chairs.Count; i++)
                AddChair(views, set.Chairs[i], id + "-chair-" + i);
        }

        private static void AddChair(List<ViewBody> views, FurnitureChair chair, string id)
        {
            if (chair.State == ChairState.Destroyed) return;
            float h = chair.Height;
            bool tipped = chair.State == ChairState.Fallen || chair.State == ChairState.Airborne;
            if (tipped)
            {
                Box(views, id, ViewKind.Chair, chair.Position, chair.Heading, -.35f, .35f, -.24f, .24f, h + .04f, h + .13f, chair.State);
                Box(views, id + "-back", ViewKind.Chair, chair.Position, chair.Heading, -.35f, -.26f, -.24f, .24f, h + .10f, h + .50f, chair.State);
                return;
            }
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box(views, id + "-leg-" + x + "-" + z, ViewKind.Chair, chair.Position, chair.Heading,
                    x * .2f - .025f, x * .2f + .025f, z * .2f - .025f, z * .2f + .025f, h, h + .44f, chair.State);
            Box(views, id, ViewKind.Chair, chair.Position, chair.Heading, -.24f, .24f, -.24f, .24f, h + .43f, h + .49f, chair.State);
            Box(views, id + "-back", ViewKind.Chair, chair.Position, chair.Heading, -.25f, -.19f, -.24f, .24f, h + .49f, h + .94f, chair.State);
        }

        public static void AddSeatedAgent(List<ViewBody> views, string id, Vector2 position, float heading,
            SeatingPosture posture, float rise, AgentState state)
        {
            rise = posture == SeatingPosture.StandingUp ? Math.Clamp(rise, 0, 1) : 0;
            float hip = .49f + .40f * rise;
            views.Add(new ViewBody(id, ViewShape.Capsule, ViewKind.Agent,
                PresentationFrame.ToPresentation(position, hip + .24f), new Vector3(.25f, .48f, .38f), heading,
                state, posture, rise));
            views.Add(new ViewBody(id + "-head", ViewShape.Sphere, ViewKind.AgentDetail,
                PresentationFrame.ToPresentation(Local(position, heading, .02f, 0), hip + .625f), new Vector3(.24f), heading, state));
            for (int side = -1; side <= 1; side += 2)
            {
                float offset = side * .12f, knee = .38f * (1 - rise);
                Box(views, id + "-thigh-" + side, ViewKind.AgentDetail, position, heading,
                    -.08f, knee + .10f, offset - .075f, offset + .075f, hip - .12f, hip, agentState: state);
                Box(views, id + "-leg-" + side, ViewKind.AgentDetail, position, heading,
                    knee - .06f, knee + .08f, offset - .07f, offset + .07f, .04f, hip - .10f, agentState: state);
                Box(views, id + "-arm-" + side, ViewKind.AgentDetail, position, heading,
                    -.05f, .13f, offset * 1.9f - .06f, offset * 1.9f + .06f, hip + .04f, hip + .42f, agentState: state);
            }
        }

        internal static IEnumerable<FurnitureBodyFrame> Status(FurnitureSet set, string id)
        {
            if (set.Table is FurnitureBody table)
                yield return new FurnitureBodyFrame(id + "-table", ViewKind.Table,
                    PresentationFrame.ToPresentation(table.Position, table.Height), table.Heading, table.State, false);
            for (int i = 0; i < set.Chairs.Count; i++)
            {
                FurnitureChair chair = set.Chairs[i];
                yield return new FurnitureBodyFrame(id + "-chair-" + i, ViewKind.Chair,
                    PresentationFrame.ToPresentation(chair.Position, chair.Height), chair.Heading, chair.State, false, chair.OccupantId);
            }
            if (set.Kind == FurnitureKind.Stage || set.Kind == FurnitureKind.Barricade)
                yield return new FurnitureBodyFrame(id, set.Kind == FurnitureKind.Stage ? ViewKind.Stage : ViewKind.Barricade,
                    PresentationFrame.ToPresentation(set.Center, 0), set.Rotation, ChairState.Upright, true);
        }
    }
}
