#nullable enable
using System;
using System.Diagnostics;
using System.Security.Cryptography;

// C# 9 init/record metadata marker absent from the .NET Standard reference surface.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace CrowdSim.Core.Compatibility
{
    /// <summary>Constants and diagnostic timing without newer runtime API dependencies.</summary>
    public static class ReferenceMath
    {
        public const float Tau = 6.283185307179586476925286766559f;
    }

    public static class ReferenceClock
    {
        public static double ElapsedMilliseconds(long started)
        {
            return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
        }
    }

    /// <summary>
    /// The seeded subtractive sequence used by .NET 8's compatibility Random.
    /// Explicit implementation makes the engine independent of the host Mono/BCL generator.
    /// </summary>
    public sealed class ReferenceRandom : Random
    {
        private readonly int[] _seed = new int[56];
        private int _next;
        private int _nextPartner = 21;

        public ReferenceRandom(int seed) : base(0)
        {
            int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
            int previous = 161803398 - subtraction;
            _seed[55] = previous;
            int value = 1;
            for (int i = 1; i < 55; i++)
            {
                int index = 21 * i % 55;
                _seed[index] = value;
                value = previous - value;
                if (value < 0) value += int.MaxValue;
                previous = _seed[index];
            }
            for (int round = 1; round < 5; round++)
                for (int i = 1; i < 56; i++)
                {
                    _seed[i] -= _seed[1 + (i + 30) % 55];
                    if (_seed[i] < 0) _seed[i] += int.MaxValue;
                }
        }

        public static int CreateSeed()
        {
            byte[] bytes = new byte[4];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                int seed;
                do
                {
                    generator.GetBytes(bytes);
                    seed = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
                }
                while (seed == int.MaxValue);
                return seed;
            }
        }

        private int NextSample()
        {
            int next = _next + 1;
            int partner = _nextPartner + 1;
            if (next >= 56) next = 1;
            if (partner >= 56) partner = 1;
            int result = _seed[next] - _seed[partner];
            if (result == int.MaxValue) result--;
            if (result < 0) result += int.MaxValue;
            _seed[next] = result;
            _next = next;
            _nextPartner = partner;
            return result;
        }

        protected override double Sample() => NextSample() * (1d / int.MaxValue);
        public override int Next() => NextSample();
        public override double NextDouble() => Sample();
        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(Sample() * maxValue);
        }
        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            long range = (long)maxValue - minValue;
            if (range <= int.MaxValue) return (int)(Sample() * range) + minValue;
            int sample = NextSample();
            if (NextSample() % 2 == 0) sample = -sample;
            double large = sample;
            large += int.MaxValue - 1;
            large /= 2d * int.MaxValue - 1;
            return (int)((long)(large * range) + minValue);
        }
        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(NextSample() % 256);
        }
        public override void NextBytes(Span<byte> buffer)
        {
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(NextSample() % 256);
        }
    }
}
