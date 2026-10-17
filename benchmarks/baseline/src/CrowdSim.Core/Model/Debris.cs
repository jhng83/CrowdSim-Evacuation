using System.Numerics;
using CrowdSim.Core.Configuration;
using CrowdSim.Core.Geometry;

namespace CrowdSim.Core.Model;

/// <summary>
/// Lightweight outward-moving debris fragment. Movement is deliberately abstract and deterministic:
/// fragments carry normalized impact energy, decay with travel, collide against walls using their
/// swept segment, and are removed after one person impact so a single fragment cannot repeatedly harm
/// the same person on successive frames.
/// </summary>
public sealed class Debris
{
    public Debris(
        Vector2 position,
        Vector2 velocity,
        int ttl,
        float radius,
        float energy = 1f)
    {
        Position = position;
        PreviousPosition = position;
        Velocity = velocity;
        Ttl = ttl;
        Radius = radius;
        Energy = Math.Clamp(energy, 0f, 1f);
    }

    public Vector2 Position { get; set; }
    public Vector2 PreviousPosition { get; private set; }
    public Vector2 Velocity { get; set; }
    public int Ttl { get; set; }
    public float Radius { get; }
    public float Energy { get; set; }

    /// <summary>Advance one frame. Returns false when the fragment should be discarded.</summary>
    public bool Advance(WorldConfig world, AgentConfig config)
    {
        PreviousPosition = Position;
        Velocity *= 0.96f;
        Energy *= Math.Clamp(config.DebrisEnergyDecay, 0f, 1f);

        if (!VectorMath.IsFinite(Velocity) || !float.IsFinite(Energy))
        {
            Velocity = Vector2.Zero;
            Energy = 0f;
            Ttl = 0;
            return false;
        }

        Position += Velocity;
        if (!VectorMath.IsFinite(Position))
        {
            Ttl = 0;
            return false;
        }

        // Debris does not wrap/clamp around the room. Leaving the simulation boundary ends it.
        if (Position.X < 0f || Position.Y < 0f || Position.X > world.WidthMeters || Position.Y > world.LengthMeters)
        {
            Ttl = 0;
            return false;
        }

        Ttl--;
        return Ttl > 0 && Energy > 0.04f;
    }

    public static List<Debris> SpawnCluster(
        Vector2 explosionPosition,
        int count,
        float speedMin,
        float speedMax,
        int ttlMin,
        int ttlMax,
        float radius,
        Random random)
    {
        List<Debris> fragments = new(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float speed = Lerp(speedMin, speedMax, (float)random.NextDouble());
            Vector2 velocity = new(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
            int ttl = ttlMin + (int)(random.NextDouble() * Math.Max(1, ttlMax - ttlMin));
            float normalizedEnergy = Math.Clamp((speed - speedMin) / Math.Max(1e-5f, speedMax - speedMin), 0f, 1f);
            // Keep even slower pieces relevant while still separating low/high energy impacts.
            normalizedEnergy = 0.30f + 0.70f * normalizedEnergy;
            fragments.Add(new Debris(explosionPosition, velocity, ttl, radius, normalizedEnergy));
        }

        return fragments;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
