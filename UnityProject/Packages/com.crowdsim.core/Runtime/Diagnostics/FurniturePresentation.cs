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
                AddConcertRig(views, set, id);
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

        // Decorative rig stays inside the authored floor footprint. It adds no physical walls,
        // people, hazard shielding or navigation state; even a 1 × 1 m stage remains finite.
        private static void AddConcertRig(List<ViewBody> views, FurnitureSet set, string id)
        {
            float half = set.AuthoredStageLength / 2, depth = set.AuthoredStageWidth / 2;
            float post = MathF.Min(.16f, half * .12f), rail = post * .45f;
            float top = .8f + MathF.Min(3.2f, MathF.Max(1.4f, set.AuthoredStageLength * .24f));
            Vector2 c = set.Center; float angle = set.Rotation;
            void Part(string suffix, float x1, float x2, float z1, float z2, float y1, float y2) =>
                Box(views, id + suffix, ViewKind.StageDetail, c, angle, x1, x2, z1, z2, y1, y2);
            // Silver perimeter lip and pale front edge make the raised deck readable.
            Part("-edge-front", -half, half, -depth, -depth + rail, .77f, .82f);
            Part("-edge-left", -half, -half + rail, -depth, depth, .77f, .82f);
            Part("-edge-right", half - rail, half, -depth, depth, .77f, .82f);
            float back = depth - post * 2;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (half - post * 2);
                // Twin-column truss towers and regular crossbars.
                Part("-truss-" + side + "-a", x - post, x - rail, back - post, back + post, .8f, top);
                Part("-truss-" + side + "-b", x + rail, x + post, back - post, back + post, .8f, top);
                for (int rung = 0; rung <= 5; rung++)
                {
                    float h = .8f + (top - .8f - rail) * rung / 5;
                    Part("-truss-rung-" + side + "-" + rung, x - post, x + post, back - post, back + post, h, h + rail);
                }
                float speakerWidth = MathF.Min(.65f, half * .24f), speakerDepth = MathF.Min(.5f, depth * .3f);
                float sx = side * (half - speakerWidth * .7f);
                Part("-speaker-" + side, sx - speakerWidth / 2, sx + speakerWidth / 2,
                    -depth + rail, -depth + rail + speakerDepth, .82f, 1.65f);
                for (int driver = 0; driver < 2; driver++)
                    Part("-speaker-grille-" + side + "-" + driver, sx - speakerWidth * .34f, sx + speakerWidth * .34f,
                        -depth + rail * .5f, -depth + rail, .95f + driver * .3f, 1.15f + driver * .3f);
            }
            Part("-truss-top", -half + post, half - post, back - post, back + post, top - post, top);
            float screenHalf = half * .68f;
            Part("-screen-frame", -screenHalf, screenHalf, back - post * 1.1f, back - post * .3f, 1.1f, top - post * 2);
            Part("-led-screen", -screenHalf * .95f, screenHalf * .95f, back - post * 1.2f, back - post * 1.1f, 1.16f, top - post * 2.5f);
            // A restrained blue/purple equalizer motif reads as an LED concert backdrop.
            for (int i = 0; i < 9; i++)
            {
                float x = -screenHalf * .85f + screenHalf * 1.7f * i / 8;
                float h = (top - post * 2.5f - 1.2f) * (.3f + .5f * MathF.Abs(MathF.Sin(i * 1.7f)));
                Part("-led-bar-" + i, x - screenHalf * .035f, x + screenHalf * .035f,
                    back - post * 1.3f, back - post * 1.2f, 1.2f, 1.2f + h);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = -half * .6f + half * .4f * i;
                Part("-lamp-" + i, x - post, x + post, back - post * 2, back, top - post * 3, top - post);
                Part("-lamp-lens-" + i, x - post * .7f, x + post * .7f, back - post * 2.1f, back - post * 2, top - post * 2.7f, top - post * 1.3f);
            }
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
