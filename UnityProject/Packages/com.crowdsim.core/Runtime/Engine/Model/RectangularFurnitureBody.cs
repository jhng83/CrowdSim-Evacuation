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

namespace CrowdSim.Core.Model
{
    /// <summary>
    /// Translating, airborne or fallen rectangular table. The authored quarter-turn heading
    /// stays fixed, keeping the visible 3 x 1 m body and its inexpensive AABB footprint aligned.
    /// Radius is broad-phase only; contacts use the exact rectangle. No chairs or occupants.
    /// </summary>
    public sealed class RectangularFurnitureBody : FurnitureBody
    {
        public RectangularFurnitureBody(Vector2 position, float heading, Vector2 halfExtents)
            : base(position, heading, halfExtents.Length())
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                throw new ArgumentOutOfRangeException(nameof(position));
            if (!float.IsFinite(heading)) throw new ArgumentOutOfRangeException(nameof(heading));
            if (!float.IsFinite(halfExtents.X) || !float.IsFinite(halfExtents.Y) ||
                halfExtents.X <= 0f || halfExtents.Y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(halfExtents));
            HalfExtents = halfExtents;
            Obstacle = null; // Never register an oversized circular navigation obstacle.
            RectangleObstacle = BoundsAt(position);
        }

        public Vector2 HalfExtents { get; }
        public Wall? RectangleObstacle { get; internal set; }

        /// <summary>Exact world-axis footprint at a proposed position.</summary>
        internal Wall BoundsAt(Vector2 position) => new(
            position.X - HalfExtents.X, position.Y - HalfExtents.Y,
            position.X + HalfExtents.X, position.Y + HalfExtents.Y)
        { IsFurniture = true, IsMovableFurniture = true };
    }
}
