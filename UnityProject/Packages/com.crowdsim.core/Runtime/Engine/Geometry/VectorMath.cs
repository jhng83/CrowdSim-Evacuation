#nullable enable
// M2 import of reference 01b4355; see docs/unity-migration/COMPATIBILITY.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowdSim.Core.Compatibility;
using System.Numerics;

namespace CrowdSim.Core.Geometry
{
    /// <summary>Numerically defensive 2D vector helpers shared by all simulation systems.</summary>
    public static class VectorMath
    {
        public const float Epsilon = 1e-5f;

        public static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

        public static Vector2 SafeNormalize(Vector2 value)
        {
            if (!IsFinite(value))
            {
                return Vector2.Zero;
            }

            float length = value.Length();
            return length > Epsilon && float.IsFinite(length) ? value / length : Vector2.Zero;
        }

        public static Vector2 SafeNormalize(Vector2 value, Vector2 fallback)
        {
            Vector2 normalized = SafeNormalize(value);
            return normalized.LengthSquared() > Epsilon * Epsilon ? normalized : SafeNormalize(fallback);
        }

        public static Vector2 ClampMagnitude(Vector2 value, float maxMagnitude)
        {
            if (!IsFinite(value))
            {
                return Vector2.Zero;
            }

            float length = value.Length();
            if (length <= maxMagnitude || length <= Epsilon)
            {
                return value;
            }

            return value / length * maxMagnitude;
        }

        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);

        /// <summary>
        /// Clamps a continuous metric world position to the inclusive room bounds.
        /// Width and height are floats because simulation coordinates are measured in metres.
        /// </summary>
        public static Vector2 ClampToBounds(Vector2 value, float width, float height)
        {
            float safeWidth = Math.Max(0f, width);
            float safeHeight = Math.Max(0f, height);

            if (!IsFinite(value))
            {
                return new Vector2(safeWidth / 2f, safeHeight / 2f);
            }

            return new Vector2(
                Math.Clamp(value.X, 0f, safeWidth),
                Math.Clamp(value.Y, 0f, safeHeight));
        }

        public static Vector2 Perpendicular(Vector2 v) => new(-v.Y, v.X);

        public static Vector2 RandomUnit(Random random)
        {
            double angle = random.NextDouble() * Math.PI * 2.0;
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        public static Vector2 RandomGaussian2(Random random)
            => new(NextGaussian(random), NextGaussian(random));

        private static float NextGaussian(Random random)
        {
            double u1 = Math.Max(double.Epsilon, random.NextDouble());
            double u2 = random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        public static float DistanceSquared(Vector2 a, Vector2 b)
        {
            Vector2 d = a - b;
            return d.X * d.X + d.Y * d.Y;
        }
    }
}
