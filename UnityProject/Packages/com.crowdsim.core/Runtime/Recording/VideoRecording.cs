#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CrowdSim.Recording
{
    /// <summary>Single-producer bounded encoding worker. Stop drains accepted frames and finalizes the AVI.
    /// The encoder receives owned fixed-size bottom-up RGBA pixels; it must be safe on a worker thread.</summary>
    public sealed class VideoRecording : IDisposable
    {
        public const int Width = 1280, Height = 720, FramesPerSecond = 20, QueueCapacity = 3;
        private readonly BlockingCollection<(byte[] Pixels, bool Pooled)> _queue = new BlockingCollection<(byte[], bool)>(new ConcurrentQueue<(byte[], bool)>(), QueueCapacity);
        private readonly Task _worker;
        private readonly MjpegAviWriter _writer;
        private Exception? _error;
        private int _frames, _dropped, _stopped;
        private long _encodingTicks;
        public string Path { get; }
        public int FramesWritten => Volatile.Read(ref _frames);
        public int DroppedFrames => Volatile.Read(ref _dropped);
        public int QueueCount => _stopped != 0 ? 0 : _queue.Count;
        public Exception? Error => Volatile.Read(ref _error);
        public bool Accepting => Volatile.Read(ref _stopped) == 0 && Error == null;
        public double LastEncodingMilliseconds => Interlocked.Read(ref _encodingTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;

        public VideoRecording(string path, Func<byte[], byte[]> encode)
        {
            if (encode == null) throw new ArgumentNullException(nameof(encode));
            Path = path;
            _writer = new MjpegAviWriter(path);
            _worker = Task.Run(() =>
            {
                try
                {
                    foreach ((byte[] pixels, bool pooled) in _queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            long started = System.Diagnostics.Stopwatch.GetTimestamp();
                            byte[] jpeg = encode(pixels);
                            _writer.WriteFrame(jpeg);
                            Interlocked.Exchange(ref _encodingTicks, System.Diagnostics.Stopwatch.GetTimestamp() - started);
                            Interlocked.Increment(ref _frames);
                        }
                        finally { if (pooled) ArrayPool<byte>.Shared.Return(pixels); }
                    }
                }
                catch (Exception exception) { Volatile.Write(ref _error, exception); }
                finally
                {
                    while (_queue.TryTake(out var packet)) if (packet.Pooled) ArrayPool<byte>.Shared.Return(packet.Pixels);
                    try { _writer.Dispose(); } catch (Exception exception) { Volatile.Write(ref _error, exception); }
                }
            });
        }

        /// <summary>Transfer an owned frame. When pooled, ownership includes returning it to ArrayPool
        /// after encode, rejection or error. Padding beyond the fixed RGBA extent is ignored.</summary>
        public bool TrySubmit(byte[] pixels, bool pooled = false)
        {
            if (pixels == null || (pooled ? pixels.Length < Width * Height * 4 : pixels.Length != Width * Height * 4))
                throw new ArgumentException("Expected a fixed 1280 × 720 RGBA frame.", nameof(pixels));
            if (!Accepting) { if (pooled) ArrayPool<byte>.Shared.Return(pixels); return false; }
            if (_queue.TryAdd((pixels, pooled))) return true;
            if (pooled) ArrayPool<byte>.Shared.Return(pixels);
            Interlocked.Increment(ref _dropped); return false;
        }
        public void CountSkipped(int count) { if (count > 0) Interlocked.Add(ref _dropped, count); }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            _queue.CompleteAdding(); _worker.GetAwaiter().GetResult();
            // A producer may have enqueued just as a worker faulted; reclaim that final packet too.
            while (_queue.TryTake(out var packet)) if (packet.Pooled) ArrayPool<byte>.Shared.Return(packet.Pixels);
            _queue.Dispose();
        }
    }

    /// <summary>Real-time capture cadence, independent of simulation ticks/speed. Missed slots are counted, never caught up in a burst.</summary>
    public sealed class CaptureClock
    {
        private double _next = double.NaN;
        public int Skipped { get; private set; }
        public bool Due(double now)
        {
            Skipped = 0;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) return false;
            if (double.IsNaN(_next)) _next = now;
            if (now + 1e-8 < _next) return false;
            int slots = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, now - _next) * VideoRecording.FramesPerSecond + 1e-7));
            Skipped = slots; _next += (slots + 1d) / VideoRecording.FramesPerSecond;
            return true;
        }
    }
}
