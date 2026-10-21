using System;

namespace CrowdSim.Diagnostics
{
    /// <summary>Bounded CPU/main-thread instrumentation, independent of fixed simulation time. No GPU/FPS inference from step timings.</summary>
    public sealed class FrameStatistics
    {
        private readonly double[] _intervals = new double[120];
        private int _count, _next;
        private double _total;
        public int Samples => _count;
        public double FramesPerSecond => _total > 0 ? _count * 1000 / _total : 0;
        public double FrameMilliseconds { get; private set; }
        public double PhysicsCommandsMilliseconds { get; private set; }
        public double WorldCommandsMilliseconds { get; private set; }
        public double UiCommandsMilliseconds { get; private set; }
        public long ManagedBytes { get; private set; }
        public void Clear()
        {
            Array.Clear(_intervals, 0, _intervals.Length); _count = 0; _next = 0; _total = 0;
            FrameMilliseconds = 0; PhysicsCommandsMilliseconds = 0; WorldCommandsMilliseconds = 0;
            UiCommandsMilliseconds = 0; ManagedBytes = 0;
        }
        public void Add(double intervalMilliseconds, double physics, double world, double ui, long bytes)
        {
            if (double.IsNaN(intervalMilliseconds) || double.IsInfinity(intervalMilliseconds) || intervalMilliseconds <= 0) return;
            if (_count == _intervals.Length) _total -= _intervals[_next]; else _count++;
            _intervals[_next] = intervalMilliseconds; _total += intervalMilliseconds; _next = (_next + 1) % _intervals.Length;
            FrameMilliseconds = intervalMilliseconds; PhysicsCommandsMilliseconds = Math.Max(0, physics);
            WorldCommandsMilliseconds = Math.Max(0, world); UiCommandsMilliseconds = Math.Max(0, ui); ManagedBytes = Math.Max(0, bytes);
        }
        public double Percentile(double fraction)
        {
            if (_count == 0) return 0;
            double[] sorted = new double[_count]; Array.Copy(_intervals, sorted, _count); Array.Sort(sorted);
            return sorted[(int)Math.Clamp(Math.Ceiling(Math.Clamp(fraction, 0, 1) * _count) - 1, 0, _count - 1)];
        }
    }
}
