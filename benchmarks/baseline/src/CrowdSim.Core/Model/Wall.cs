using System.Numerics;

namespace CrowdSim.Core.Model;

/// <summary>Axis-aligned rectangular wall; X1/Y1 is top-left and X2/Y2 is bottom-right.</summary>
public sealed record Wall(float X1, float Y1, float X2, float Y2)
{
    /// <summary>Return a unit tangent along the longer wall axis.</summary>
    public Vector2 TangentDirection()
    {
        float dx = X2 - X1;
        float dy = Y2 - Y1;
        if (MathF.Abs(dx) < 1e-5f && MathF.Abs(dy) < 1e-5f)
        {
            return Vector2.Zero;
        }

        return MathF.Abs(dx) >= MathF.Abs(dy) ? Vector2.UnitX : Vector2.UnitY;
    }

    /// <summary>Return whether a point lies inside the rectangle.</summary>
    public bool Contains(Vector2 position)
        => X1 <= position.X && position.X <= X2 && Y1 <= position.Y && position.Y <= Y2;

    public Vector2 ClosestPoint(Vector2 position)
        => new(Math.Clamp(position.X, X1, X2), Math.Clamp(position.Y, Y1, Y2));
}
