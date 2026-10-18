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
    /// <summary>Posture is independent of injury, panic and evacuation state.</summary>
    public enum SeatingPosture { Walking, Seated, StandingUp }
    /// <summary>Stable persisted asset identifiers. Only the first two kinds create seated occupants.</summary>
    public enum FurnitureKind
    {
        RoundTableSet = 0,
        SingleChair = 1,
        RectangularTable = 2,
        Stage = 3,
        Barricade = 4,
    }
    /// <summary>Furniture motion state; the original ChairState name is retained for compatibility.</summary>
    public enum ChairState { Upright, Airborne, Fallen, Destroyed }

    /// <summary>
    /// Authored furniture in metres. Round tables/chairs retain their existing movable bodies;
    /// the unoccupied rectangular table is blast-movable; stages and road barricades are fixed.
    /// </summary>
    public sealed class FurnitureSet
    {
        public const float TableRadius = 0.90f;
        public const float SeatRingRadius = 1.50f;
        public const float ChairRadius = 0.30f;
        public const float RectangularTableLength = 3f;
        public const float RectangularTableWidth = 1f;
        public const float RectangularTableHeight = 0.78f;
        public const float StageLength = 15f;
        public const float StageWidth = 6f;
        public const float StageHeight = 0.80f;
        public const float BarricadeLength = 1.50f;
        /// <summary>Assumed base depth for the road barricade; not a certified product dimension.</summary>
        public const float BarricadeWidth = 0.40f;
        public const float BarricadeHeight = 1.00f;

        public FurnitureSet(FurnitureKind kind, Vector2 center, float rotation = 0f)
        {
            if (!Enum.IsDefined(typeof(FurnitureKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
                throw new ArgumentOutOfRangeException(nameof(center));
            if (!float.IsFinite(rotation)) throw new ArgumentOutOfRangeException(nameof(rotation));

            Kind = kind;
            Center = center;
            Rotation = kind == FurnitureKind.Barricade ? NormalizeRotation(rotation)
                : IsRectangular(kind) ? SnapRectangleRotation(rotation) : rotation;
            if (kind == FurnitureKind.Barricade)
            {
                OrientedRectangle footprint = new(center,
                    new Vector2(BarricadeLength, BarricadeWidth) * 0.5f, Rotation);
                _fixedObstacle = new Wall(footprint.Minimum.X, footprint.Minimum.Y,
                    footprint.Maximum.X, footprint.Maximum.Y)
                { IsFurniture = true, OrientedFootprint = footprint };
                return;
            }
            if (IsRectangular(kind))
            {
                (float length, float width) = kind switch
                {
                    FurnitureKind.Stage => (StageLength, StageWidth),
                    FurnitureKind.Barricade => (BarricadeLength, BarricadeWidth),
                    _ => (RectangularTableLength, RectangularTableWidth),
                };
                bool swapped = MathF.Abs(MathF.Sin(Rotation)) > 0.5f;
                float halfX = (swapped ? width : length) * 0.5f;
                float halfY = (swapped ? length : width) * 0.5f;
                // Use the existing AABB spatial index and route/collision checks. No circular
                // proxy: a long table/stage must not acquire an oversized invisible footprint.
                if (kind == FurnitureKind.RectangularTable)
                    Table = new RectangularFurnitureBody(center, Rotation, new Vector2(halfX, halfY));
                else
                    _fixedObstacle = new Wall(center.X - halfX, center.Y - halfY,
                        center.X + halfX, center.Y + halfY)
                    { IsFurniture = true };
                return;
            }

            if (kind == FurnitureKind.RoundTableSet)
                Table = new FurnitureBody(center, rotation, TableRadius);
            int count = GetSeatCount(kind);
            for (int i = 0; i < count; i++)
            {
                float angle = rotation + ReferenceMath.Tau * i / count;
                Vector2 outward = new(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 seat = kind == FurnitureKind.RoundTableSet ? center + outward * SeatRingRadius : center;
                Chairs.Add(new FurnitureChair(seat, kind == FurnitureKind.RoundTableSet ? angle + MathF.PI : rotation));
            }
        }

        public FurnitureKind Kind { get; }
        public Vector2 Center { get; }
        public float Rotation { get; }
        public float PlacementRadius => Kind switch
        {
            FurnitureKind.RoundTableSet => 2.25f,
            FurnitureKind.SingleChair => 1.05f,
            FurnitureKind.RectangularTable => MathF.Sqrt(10f) * 0.5f,
            FurnitureKind.Stage => MathF.Sqrt(StageLength * StageLength + StageWidth * StageWidth) * 0.5f,
            FurnitureKind.Barricade => MathF.Sqrt(BarricadeLength * BarricadeLength + BarricadeWidth * BarricadeWidth) * 0.5f,
            _ => throw new InvalidOperationException("Unsupported furniture kind."),
        };
        public FurnitureBody? Table { get; }
        public CircleBlockage? TableObstacle => Table?.Obstacle;
        private readonly Wall? _fixedObstacle;
        /// <summary>
        /// Current exact rectangular footprint (legacy property name retained). Moving tables
        /// update it after physics; destroyed tables return null. Stages and barricades remain fixed.
        /// These low footprints do not occlude full-height sight, blast or the fume field.
        /// </summary>
        public Wall? StaticObstacle => Table is RectangularFurnitureBody rectangle
            ? rectangle.RectangleObstacle : _fixedObstacle;
        public List<FurnitureChair> Chairs { get; } = new();

        /// <summary>Count authored occupants without allocating a preview or runtime furniture.</summary>
        public static int GetSeatCount(FurnitureKind kind) => kind switch
        {
            FurnitureKind.RoundTableSet => 10,
            FurnitureKind.SingleChair => 1,
            FurnitureKind.RectangularTable or FurnitureKind.Stage or FurnitureKind.Barricade => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static bool IsRectangular(FurnitureKind kind) =>
            kind is FurnitureKind.RectangularTable or FurnitureKind.Stage or FurnitureKind.Barricade;

        /// <summary>Normalize a free angle without snapping away the authored orientation.</summary>
        public static float NormalizeRotation(float radians)
        {
            if (!float.IsFinite(radians)) throw new ArgumentOutOfRangeException(nameof(radians));
            float wrapped = radians % ReferenceMath.Tau;
            return wrapped < 0f ? wrapped + ReferenceMath.Tau : wrapped;
        }

        /// <summary>Tables and stages retain quarter turns and inexpensive axis-aligned footprints.</summary>
        public static float SnapRectangleRotation(float radians)
        {
            if (!float.IsFinite(radians)) throw new ArgumentOutOfRangeException(nameof(radians));
            float wrapped = radians % ReferenceMath.Tau;
            if (wrapped < 0f) wrapped += ReferenceMath.Tau;
            int quarter = (int)MathF.Round(wrapped / (MathF.PI * 0.5f), MidpointRounding.AwayFromZero) % 4;
            return quarter * MathF.PI * 0.5f;
        }
    }

    /// <summary>Lightweight furniture rigid-body approximation; no calibrated explosive yield is implied.</summary>
    public class FurnitureBody
    {
        public FurnitureBody(Vector2 position, float heading, float radius)
        {
            StartPosition = Position = position; StartHeading = Heading = heading;
            Radius = radius;
            Obstacle = new CircleBlockage(position, radius) { IsFurniture = true };
        }
        public Vector2 StartPosition { get; }
        public Vector2 Position { get; internal set; }
        public float StartHeading { get; }
        public float Heading { get; internal set; }
        public ChairState State { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public float Height { get; internal set; }
        public float VerticalSpeed { get; internal set; }
        public float FlightSeconds { get; internal set; }
        public bool BlastApplied { get; internal set; }
        public bool ImpactApplied { get; internal set; }
        internal int PhysicsActiveFrame { get; set; } = -1;
        internal FurnitureBody? SupportBody { get; set; }
        public float Radius { get; }
        public CircleBlockage? Obstacle { get; internal set; }
    }

    /// <summary>A seat adds occupant identity to the shared lightweight furniture physics body.</summary>
    public sealed class FurnitureChair : FurnitureBody
    {
        public FurnitureChair(Vector2 position, float heading)
            : base(position, heading, FurnitureSet.ChairRadius)
        {
            Obstacle = Obstacle! with { ChairOwner = this };
        }

        internal int LastPushFrame { get; set; } = -1;

        public int? OccupantId { get; internal set; }
        public int? OriginalOccupantId { get; internal set; }
    }
}
