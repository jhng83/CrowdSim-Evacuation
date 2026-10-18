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
using CrowdSim.Core.Geometry;

namespace CrowdSim.Core.Model
{
    /// <summary>
    /// Rectangular obstacle. X1/Y1 and X2/Y2 are its broad-phase bounds; a rotated
    /// barricade additionally supplies an exact cached narrow-phase footprint.
    /// </summary>
    public sealed record Wall(float X1, float Y1, float X2, float Y2)
    {
        /// <summary>
        /// A low furniture footprint blocks locomotion, but is not a full-height wall
        /// for visual perception, blast shielding or the existing simplified fume field.
        /// </summary>
        public bool IsFurniture { get; init; }

        /// <summary>Moving furniture uses live body contacts, not its previous-frame wall proxy.</summary>
        public bool IsMovableFurniture { get; init; }

        /// <summary>Present only for free-angle assets; ordinary walls retain the original fast path.</summary>
        public OrientedRectangle? OrientedFootprint { get; init; }

        public OrientedRectangle AsRectangle() => OrientedFootprint ?? new OrientedRectangle(
            new Vector2((X1 + X2) * 0.5f, (Y1 + Y2) * 0.5f),
            new Vector2((X2 - X1) * 0.5f, (Y2 - Y1) * 0.5f), 0f);

        /// <summary>True only for positive-area overlap, ignoring the supplied rounding tolerance.</summary>
        public bool Overlaps(Wall other, float tolerance = 0f)
        {
            if (MathF.Min(X2, other.X2) - MathF.Max(X1, other.X1) <= tolerance ||
                MathF.Min(Y2, other.Y2) - MathF.Max(Y1, other.Y1) <= tolerance) return false;
            return (OrientedFootprint is null && other.OrientedFootprint is null) ||
                AsRectangle().Overlaps(other.AsRectangle(), tolerance);
        }

        public bool OverlapsCircle(Vector2 center, float radius) =>
            Vector2.DistanceSquared(center, ClosestPoint(center)) < radius * radius;

        public void CopyExpandedCorners(Span<Vector2> corners, float margin)
        {
            if (OrientedFootprint is OrientedRectangle rotated)
            {
                rotated.CopyExpandedCorners(corners, margin);
                return;
            }
            if (corners.Length < 4) throw new ArgumentException("Four corners are required.", nameof(corners));
            corners[0] = new Vector2(X1 - margin, Y1 - margin);
            corners[1] = new Vector2(X2 + margin, Y1 - margin);
            corners[2] = new Vector2(X2 + margin, Y2 + margin);
            corners[3] = new Vector2(X1 - margin, Y2 + margin);
        }

        /// <summary>Return a unit tangent along the longer wall axis.</summary>
        public Vector2 TangentDirection()
        {
            if (OrientedFootprint is OrientedRectangle rotated)
                return rotated.HalfExtents.X >= rotated.HalfExtents.Y ? rotated.AxisX : rotated.AxisY;
            float dx = X2 - X1;
            float dy = Y2 - Y1;
            if (MathF.Abs(dx) < 1e-5f && MathF.Abs(dy) < 1e-5f)
            {
                return Vector2.Zero;
            }

            return MathF.Abs(dx) >= MathF.Abs(dy) ? Vector2.UnitX : Vector2.UnitY;
        }

        /// <summary>Return whether a point lies inside the rectangle.</summary>
        public bool Contains(Vector2 position, float margin = 0f)
            => OrientedFootprint is OrientedRectangle rotated
                ? rotated.Contains(position, margin)
                : X1 - margin <= position.X && position.X <= X2 + margin &&
                    Y1 - margin <= position.Y && position.Y <= Y2 + margin;

        public Vector2 ClosestPoint(Vector2 position)
            => OrientedFootprint is OrientedRectangle rotated ? rotated.ClosestPoint(position)
                : new Vector2(Math.Clamp(position.X, X1, X2), Math.Clamp(position.Y, Y1, Y2));
    }
}
