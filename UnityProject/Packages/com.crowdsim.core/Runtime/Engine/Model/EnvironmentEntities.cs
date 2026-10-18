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
    /// <summary>Exit opening carved into a boundary wall.</summary>
    public sealed class Exit
    {
        public Exit(string name, Vector2 position, float size = 0.32f, string side = "N")
        {
            Name = name;
            Position = position;
            Size = size;
            Side = side;
        }

        public string Name { get; }
        public Vector2 Position { get; }
        public float Size { get; }
        public string Side { get; }
    }

    /// <summary>Explosion trigger position and simulation frame.</summary>
    public sealed record Explosion(Vector2 Position, int TriggeredFrame);

    /// <summary>
    /// Builder-time population placement. Positions are resolved when the user clicks the room so
    /// the exact start layout can be reviewed, saved and reproduced on restart.
    /// </summary>
    public sealed class AgentSpawnGroup
    {
        public AgentSpawnGroup(Vector2 center, IEnumerable<Vector2> positions)
        {
            if (positions is null) throw new ArgumentNullException(nameof(positions));
            Center = center;
            Positions = positions.ToList();
        }

        public Vector2 Center { get; }
        public List<Vector2> Positions { get; }
        public int Count => Positions.Count;
    }

    /// <summary>Per-exit raw density and instantaneous flow rate.</summary>
    public readonly struct ExitMetrics : IEquatable<ExitMetrics>
    {
        public float Density { get; }
        public float Flow { get; }
        public ExitMetrics(float Density, float Flow)
        {
            this.Density = Density;
            this.Flow = Flow;
        }
        public void Deconstruct(out float Density, out float Flow) { Density = this.Density; Flow = this.Flow; }
        public bool Equals(ExitMetrics other) => EqualityComparer<float>.Default.Equals(Density, other.Density) && EqualityComparer<float>.Default.Equals(Flow, other.Flow);
        public override bool Equals(object? other) => other is ExitMetrics value && Equals(value);
        public override int GetHashCode()
        {
            int hash = 0;
            hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(Density));
            hash = unchecked(hash * -1521134295 + EqualityComparer<float>.Default.GetHashCode(Flow));
            return hash;
        }
        public static bool operator ==(ExitMetrics left, ExitMetrics right) => left.Equals(right);
        public static bool operator !=(ExitMetrics left, ExitMetrics right) => !left.Equals(right);
    }
}
