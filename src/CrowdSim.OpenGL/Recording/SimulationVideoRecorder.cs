using System.Buffers;
using System.Collections.Concurrent;
using SharpAvi.Codecs;
using SharpAvi.Output;
using Silk.NET.OpenGL;

namespace CrowdSim.OpenGL.Recording;

/// <summary>
/// Captures the complete rendered application framebuffer to a fixed-size Motion-JPEG AVI.
/// OpenGL readback stays on the render thread, while scaling and MJPEG encoding run on a bounded
/// worker queue so video recording cannot monopolise the simulation/UI thread.
/// </summary>
public sealed class SimulationVideoRecorder : IDisposable
{
    public const int OutputWidth = 1280;
    public const int OutputHeight = 720;
    public const int FramesPerSecond = 20;

    private readonly AviWriter _writer;
    private readonly IAviVideoStream _videoStream;
    private readonly BlockingCollection<FramePacket> _frameQueue = new(
        new ConcurrentQueue<FramePacket>(),
        boundedCapacity: 3);
    private readonly Task _worker;
    private double _captureAccumulator;
    private bool _firstFrame = true;
    private bool _disposed;
    private int _framesWritten;
    private int _droppedFrames;
    private volatile Exception? _workerException;

    public SimulationVideoRecorder(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        OutputPath = outputPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        _writer = new AviWriter(outputPath)
        {
            FramesPerSecond = FramesPerSecond,
            EmitIndex1 = true,
        };
        _videoStream = _writer.AddMJpegImageSharpVideoStream(
            OutputWidth,
            OutputHeight,
            quality: 78);
        _videoStream.Name = "CrowdSim UI recording";
        _worker = Task.Run(EncodeWorkerLoop);
    }

    public string OutputPath { get; }
    public int FramesWritten => Volatile.Read(ref _framesWritten);
    public int DroppedFrames => Volatile.Read(ref _droppedFrames);
    public int QueuedFrames => _frameQueue.Count;

    public unsafe void Capture(GL gl, int sourceWidth, int sourceHeight, double deltaSeconds)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimulationVideoRecorder));
        }
        ArgumentNullException.ThrowIfNull(gl);
        ThrowIfWorkerFailed();
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        _captureAccumulator += Math.Max(0d, deltaSeconds);
        double interval = 1d / FramesPerSecond;
        if (!_firstFrame && _captureAccumulator < interval)
        {
            return;
        }

        if (_firstFrame)
        {
            _firstFrame = false;
            _captureAccumulator = 0d;
        }
        else
        {
            _captureAccumulator = Math.Max(0d, _captureAccumulator - interval);
        }

        int required = checked(sourceWidth * sourceHeight * 4);
        byte[] sourceFrame = ArrayPool<byte>.Shared.Rent(required);
        bool queued = false;
        try
        {
            fixed (byte* destination = sourceFrame)
            {
                gl.ReadPixels(
                    0,
                    0,
                    (uint)sourceWidth,
                    (uint)sourceHeight,
                    PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    destination);
            }

            queued = _frameQueue.TryAdd(new FramePacket(sourceFrame, sourceWidth, sourceHeight));
            if (!queued)
            {
                Interlocked.Increment(ref _droppedFrames);
            }
        }
        finally
        {
            if (!queued)
            {
                ArrayPool<byte>.Shared.Return(sourceFrame);
            }
        }
    }

    private void EncodeWorkerLoop()
    {
        byte[] outputFrame = new byte[OutputWidth * OutputHeight * 4];
        try
        {
            foreach (FramePacket packet in _frameQueue.GetConsumingEnumerable())
            {
                try
                {
                    ScaleAndFlip(packet, outputFrame);
                    _videoStream.WriteFrame(true, outputFrame);
                    Interlocked.Increment(ref _framesWritten);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(packet.Buffer);
                }
            }
        }
        catch (Exception exception)
        {
            _workerException = exception;
        }
        finally
        {
            while (_frameQueue.TryTake(out FramePacket pending))
            {
                ArrayPool<byte>.Shared.Return(pending.Buffer);
            }
        }
    }

    private static void ScaleAndFlip(FramePacket packet, byte[] outputFrame)
    {
        int sourceWidth = packet.Width;
        int sourceHeight = packet.Height;
        byte[] sourceFrame = packet.Buffer;

        // OpenGL readback is bottom-up. Flip vertically while scaling to the stable
        // recording resolution so window resizing cannot invalidate the AVI stream.
        for (int y = 0; y < OutputHeight; y++)
        {
            int sourceTopY = (int)((long)y * sourceHeight / OutputHeight);
            int sourceY = sourceHeight - 1 - Math.Clamp(sourceTopY, 0, sourceHeight - 1);
            int outputRow = y * OutputWidth * 4;
            int sourceRow = sourceY * sourceWidth * 4;
            for (int x = 0; x < OutputWidth; x++)
            {
                int sourceX = (int)((long)x * sourceWidth / OutputWidth);
                int sourceIndex = sourceRow + Math.Clamp(sourceX, 0, sourceWidth - 1) * 4;
                int outputIndex = outputRow + x * 4;
                outputFrame[outputIndex] = sourceFrame[sourceIndex];
                outputFrame[outputIndex + 1] = sourceFrame[sourceIndex + 1];
                outputFrame[outputIndex + 2] = sourceFrame[sourceIndex + 2];
                outputFrame[outputIndex + 3] = 255;
            }
        }
    }

    private void ThrowIfWorkerFailed()
    {
        Exception? failure = _workerException;
        if (failure is not null)
        {
            throw new InvalidOperationException("Video encoder worker failed.", failure);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frameQueue.CompleteAdding();
        try
        {
            _worker.GetAwaiter().GetResult();
            ThrowIfWorkerFailed();
        }
        finally
        {
            _writer.Close();
            _frameQueue.Dispose();
        }
    }

    private readonly record struct FramePacket(
        byte[] Buffer,
        int Width,
        int Height);
}
