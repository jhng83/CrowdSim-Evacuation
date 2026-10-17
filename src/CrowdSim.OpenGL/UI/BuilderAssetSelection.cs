using CrowdSim.Core.Model;

namespace CrowdSim.OpenGL.UI;

/// <summary>All non-population assets exposed by the single builder asset picker.</summary>
internal enum BuilderAsset
{
    SquareBlock,
    LargeSquareBlock,
    CircularBlock,
    RoundTableSet,
    SingleChair,
    RectangularTable,
    Stage,
    Barricade,
}

/// <summary>
/// Window-independent selection state. Each asset remembers its own orientation;
/// square/circular obstacles never inherit an irrelevant furniture-rotation control.
/// </summary>
internal sealed class BuilderAssetSelection
{
    public static IReadOnlyList<BuilderAsset> Options { get; } =
        Array.AsReadOnly(Enum.GetValues<BuilderAsset>());
    private readonly float[] _rotations = new float[Enum.GetValues<BuilderAsset>().Length];

    public BuilderAsset Selected { get; private set; } = BuilderAsset.SquareBlock;
    public int BlockageCells => Selected == BuilderAsset.LargeSquareBlock ? 2 : 1;
    public float RotationDegrees => _rotations[(int)Selected];
    public float RotationRadians => RotationDegrees * MathF.PI / 180f;
    public bool SupportsRotation => Furniture is not null;
    public bool UsesQuarterTurns => Furniture is FurnitureKind.RectangularTable or FurnitureKind.Stage;
    public float ShortcutRotationStep => Selected == BuilderAsset.Barricade ? 15f : 90f;
    public bool SnapBarricadeEnds { get; set; } = true;

    public FurnitureKind? Furniture => Selected switch
    {
        BuilderAsset.RoundTableSet => FurnitureKind.RoundTableSet,
        BuilderAsset.SingleChair => FurnitureKind.SingleChair,
        BuilderAsset.RectangularTable => FurnitureKind.RectangularTable,
        BuilderAsset.Stage => FurnitureKind.Stage,
        BuilderAsset.Barricade => FurnitureKind.Barricade,
        _ => null,
    };

    public void Select(BuilderAsset asset)
    {
        if (!Enum.IsDefined(asset)) throw new ArgumentOutOfRangeException(nameof(asset));
        Selected = asset;
    }

    /// <summary>Normalize degrees; barricades and occupied sets/chairs retain free angles.</summary>
    public void SetRotation(float degrees)
    {
        if (!float.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
        if (!SupportsRotation) return;
        float normalized = (degrees % 360f + 360f) % 360f;
        if (UsesQuarterTurns)
            normalized = (MathF.Round(normalized / 90f, MidpointRounding.AwayFromZero) * 90f) % 360f;
        _rotations[(int)Selected] = normalized;
    }

    public void RotateQuarterTurn() => SetRotation(RotationDegrees + 90f);

    public void RotateStep(bool reverse = false) =>
        SetRotation(RotationDegrees + (reverse ? -ShortcutRotationStep : ShortcutRotationStep));

    public void Reset()
    {
        Array.Clear(_rotations);
        Selected = BuilderAsset.SquareBlock;
        SnapBarricadeEnds = true;
    }

    public static string Label(BuilderAsset asset, float cellSize) => asset switch
    {
        BuilderAsset.SquareBlock => $"Square block - {cellSize:F1} x {cellSize:F1} m",
        BuilderAsset.LargeSquareBlock => $"Square block - {cellSize * 2f:F1} x {cellSize * 2f:F1} m",
        BuilderAsset.CircularBlock => "Circular block - 2.5 m diameter",
        BuilderAsset.RoundTableSet => "Round table + 10 seated people",
        BuilderAsset.SingleChair => "Single chair + 1 seated person",
        BuilderAsset.RectangularTable => "Table - 3 x 1 m (unoccupied)",
        BuilderAsset.Stage => "Stage - 15 x 6 m (max 1)",
        BuilderAsset.Barricade => "Orange barricade - 1.5 m long",
        _ => throw new ArgumentOutOfRangeException(nameof(asset)),
    };

    public string Hint => Selected switch
    {
        BuilderAsset.SquareBlock or BuilderAsset.LargeSquareBlock =>
            "Grid-aligned edges: blocks can touch, including different sizes. Real overlaps are rejected.",
        BuilderAsset.CircularBlock => "Solid circular obstacle. Rotation is not applicable.",
        BuilderAsset.RoundTableSet => "One table, ten chairs and ten seated people. Reserve space for people to stand up.",
        BuilderAsset.SingleChair => "One chair and one seated person. Rotation changes their starting facing direction.",
        BuilderAsset.RectangularTable => "3 m long x 1 m wide. No occupants. Movable and destructible in Explosion.",
        BuilderAsset.Stage => "15 m long x 6 m wide. Fixed, non-walkable; one stage per scenario. No occupants.",
        BuilderAsset.Barricade => "1.5 m long x 0.4 m wide x 1 m high. Any angle; fixed, non-walkable. No occupants.",
        _ => string.Empty,
    };
}
