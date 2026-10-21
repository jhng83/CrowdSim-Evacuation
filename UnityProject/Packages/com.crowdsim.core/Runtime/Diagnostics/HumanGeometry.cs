using System;
using System.Collections.Generic;
using System.Numerics;

namespace CrowdSim.Diagnostics
{
    /// <summary>One positive-volume box in humanoid-local coordinates: X forward, Y up, Z left.</summary>
    public readonly struct HumanPart
    {
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public Quaternion Rotation { get; }
        public float Shade { get; }
        public HumanPart(Vector3 center, Vector3 size, Quaternion rotation, float shade = 1)
        { Center = center; Size = size; Rotation = rotation; Shade = shade; }
    }

    /// <summary>Read-only procedural poses. Far detail retains a head, torso and four volumetric limbs; never sprites.</summary>
    public static class HumanGeometry
    {
        public static float Motion(float ratio) => Smooth(.08f, .70f, ratio);
        public static float Cadence(float ratio) => (1.35f + 1.2f * Smooth(1.25f, 2.20f, ratio)) * Motion(ratio);
        private static float Smooth(float a, float b, float value)
        { float t = Math.Clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

        public static void Build(AgentVisualFrame agent, bool detailed, float phase, List<HumanPart> parts, float? riseProgress = null)
        {
            parts.Clear();
            float motion = Motion(agent.SpeedRatio), run = Smooth(1.25f, 2.20f, agent.SpeedRatio);
            float wave = MathF.Sin(phase) * motion;
            HumanPose pose = agent.Pose;
            if (pose == HumanPose.Prone || pose == HumanPose.Crawling)
            {
                bool crawl = pose == HumanPose.Crawling;
                float hip = crawl ? .30f : .17f, shoulder = crawl ? .38f : .17f;
                Segment(parts, new Vector3(-.12f, hip, 0), new Vector3(.28f, shoulder, 0), .36f, .22f, 1);
                Box(parts, .50f, crawl ? .44f : .18f, 0, .23f, .21f, .21f, 1.08f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float swing = crawl ? wave * .10f * side : 0;
                    Vector3 arm = new Vector3(.22f, shoulder, side * .20f);
                    Vector3 hand = new Vector3(.53f + swing, .10f, side * .25f);
                    Vector3 leg = new Vector3(-.12f, hip, side * .09f);
                    Vector3 foot = new Vector3(-.69f - swing, .07f, side * .13f);
                    if (crawl && detailed)
                    {
                        Vector3 elbow = new Vector3(.36f + swing * .5f, .25f, side * .23f);
                        Vector3 knee = new Vector3(-.38f - swing, .17f, side * .16f);
                        Segment(parts, arm, elbow, .065f, .058f, .82f); Segment(parts, elbow, hand, .06f, .052f, .82f);
                        Segment(parts, leg, knee, .086f, .070f, .82f); Segment(parts, knee, foot, .078f, .064f, .82f);
                    }
                    else
                    {
                        Segment(parts, arm, hand, .065f, .058f, .82f);
                        Segment(parts, leg, foot, .086f, .070f, .82f);
                    }
                }
                return;
            }
            bool seated = pose == HumanPose.Seated || pose == HumanPose.Rising;
            bool assist = pose == HumanPose.Assisting;
            float rise = pose == HumanPose.Rising ? Math.Clamp(riseProgress ?? agent.Rise, 0, 1) : 0;
            float hipY = seated ? .49f + .31f * rise : assist ? .48f : .80f;
            float torsoTop = hipY + .58f;
            float bob = seated || assist ? 0 : (.003f + .010f * run) * motion * (.5f + .5f * MathF.Cos(phase * 2));
            float lean = assist ? .12f + .02f * MathF.Sin(phase) : .048f * run * motion;
            Box(parts, lean * .55f, hipY + .31f + bob, 0, .26f, .54f, .38f);
            Box(parts, lean, torsoTop + .205f + bob, 0, .21f, .23f, .21f, 1.08f);
            if (detailed)
            {
                Box(parts, 0, hipY + bob, 0, .20f, .10f, .26f, .92f);
                Box(parts, lean * .82f, torsoTop + .045f + bob, 0, .11f, .09f, .11f);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float stride = seated || assist ? 0 : wave * side * (.10f + .17f * run);
                float lift = Math.Max(0, wave * side) * (.018f + .067f * run);
                Vector3 hip = new Vector3(0, hipY + bob, side * .082f);
                Vector3 foot = new Vector3(seated ? .38f * (1 - rise) : assist ? (side < 0 ? .34f : -.43f) : stride,
                    .075f + (seated || assist ? 0 : lift), side * .10f);
                if (seated || assist)
                {
                    Vector3 knee = new Vector3(seated ? .38f * (1 - rise) : side < 0 ? .14f : -.20f,
                        seated ? hipY - .08f : .18f, side * .10f);
                    Segment(parts, hip, knee, .09f, .075f, .82f); Segment(parts, knee, foot, .082f, .068f, .82f);
                }
                else Segment(parts, hip, foot, .09f, .074f, .82f);
                Vector3 shoulder = new Vector3(lean * .58f, torsoTop - .07f + bob, side * .22f);
                Vector3 hand = new Vector3(assist ? .42f : seated ? .18f * (1 - rise) : -stride * .75f,
                    assist ? .48f : hipY + .02f + bob, side * (assist ? .14f : .226f));
                Segment(parts, shoulder, hand, .066f, .058f, .82f);
                if (detailed) Box(parts, foot.X + .05f, foot.Y - .025f, foot.Z, .175f, .07f, .104f, .70f);
            }
        }
        private static void Box(List<HumanPart> parts, float x, float y, float z, float sx, float sy, float sz, float shade = 1)
            => parts.Add(new HumanPart(new Vector3(x, y, z), new Vector3(sx, sy, sz), Quaternion.Identity, shade));

        private static void Segment(List<HumanPart> parts, Vector3 a, Vector3 b, float width, float depth, float shade)
        {
            Vector3 delta = b - a;
            float length = delta.Length();
            Vector3 axis = length > 1e-6f ? delta / length : Vector3.UnitY;
            float dot = Vector3.Dot(Vector3.UnitY, axis);
            Quaternion rotation = dot < -.9999f ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI)
                : Quaternion.Normalize(new Quaternion(Vector3.Cross(Vector3.UnitY, axis), 1 + dot));
            parts.Add(new HumanPart((a + b) * .5f, new Vector3(depth, Math.Max(.01f, length), width), rotation, shade));
        }
    }
}
