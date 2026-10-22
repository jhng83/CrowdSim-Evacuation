using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using CrowdSim.Recording;
using CrowdSim.Workspace;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class VideoRecordingTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "crowdsim-m10-" + Guid.NewGuid().ToString("N") + ".avi");
    private static byte[] Encode(byte[] bottomUp)
    {
        byte[] topDown = new byte[VideoRecording.Width * VideoRecording.Height * 4]; int stride = VideoRecording.Width * 4;
        for (int row = 0; row < VideoRecording.Height; row++)
            Buffer.BlockCopy(bottomUp, row * stride, topDown, (VideoRecording.Height - 1 - row) * stride, stride);
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(topDown, VideoRecording.Width, VideoRecording.Height);
        using MemoryStream output = new(); image.Save(output, new JpegEncoder { Quality = 78 }); return output.ToArray();
    }
    private static byte[] Pixels(bool second = false)
    {
        byte[] pixels = new byte[VideoRecording.Width * VideoRecording.Height * 4];
        for (int y = 0; y < VideoRecording.Height; y++)
        for (int x = 0; x < VideoRecording.Width; x++)
        {
            int i = (y * VideoRecording.Width + x) * 4;
            pixels[i] = y >= VideoRecording.Height / 2 ? (byte)255 : (byte)0;
            pixels[i + 1] = second ? (byte)255 : (byte)0;
            pixels[i + 2] = y < VideoRecording.Height / 2 ? (byte)255 : (byte)0; pixels[i + 3] = 255;
        }
        return pixels;
    }

    [Fact]
    public void WorkerProducesIndexed720p20FpsAviWithDecodableOrientedKnownFrames()
    {
        string path = NewPath();
        try
        {
            using (VideoRecording recorder = new(path, Encode))
            {
                byte[] pooled = ArrayPool<byte>.Shared.Rent(VideoRecording.Width * VideoRecording.Height * 4);
                Pixels().CopyTo(pooled, 0);
                Assert.True(recorder.TrySubmit(pooled, pooled: true)); Assert.True(recorder.TrySubmit(Pixels(true)));
                recorder.Dispose(); Assert.Null(recorder.Error); Assert.Equal(2, recorder.FramesWritten);
            }
            byte[] bytes = File.ReadAllBytes(path); string ascii = Encoding.Latin1.GetString(bytes);
            Assert.Equal("RIFF", ascii[..4]); Assert.Equal("AVI ", ascii.Substring(8, 4));
            Assert.Equal(bytes.Length - 8, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
            int avih = ascii.IndexOf("avih", StringComparison.Ordinal);
            Assert.Equal(50000, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(avih + 8)));
            Assert.Equal(2, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(avih + 24)));
            Assert.Equal(1280, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(avih + 40)));
            Assert.Equal(720, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(avih + 44)));
            int movi = ascii.IndexOf("movi", StringComparison.Ordinal), index = ascii.LastIndexOf("idx1", StringComparison.Ordinal);
            Assert.Equal(32, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index + 4)));
            for (int f = 0; f < 2; f++)
            {
                int entry = index + 8 + 16 * f;
                int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entry + 8));
                int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(entry + 12));
                Assert.Equal("00dc", ascii.Substring(movi + offset, 4));
                using Image<Rgb24> image = Image.Load<Rgb24>(bytes.AsSpan(movi + offset + 8, size));
                Assert.Equal(1280, image.Width); Assert.Equal(720, image.Height);
                Assert.True(image[100, 100].R > 230 && image[100, 100].B < 25);
                Assert.True(image[100, 600].B > 230 && image[100, 600].R < 25);
                Assert.True(f == 0 ? image[100, 100].G < 25 : image[100, 100].G > 230);
            }
            string? artifact = Environment.GetEnvironmentVariable("CROWDSIM_RECORDING_ARTIFACT");
            if (!string.IsNullOrEmpty(artifact)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(artifact))!); File.Copy(path, artifact, true); }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void QueueIsBoundedDropsBackpressureAndDrainsOnStop()
    {
        string path = NewPath(); using ManualResetEventSlim entered = new(), release = new();
        VideoRecording recording = new(path, pixels => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); return Encode(pixels); });
        try
        {
            Assert.True(recording.TrySubmit(Pixels())); Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            for (int i = 0; i < 3; i++) Assert.True(recording.TrySubmit(Pixels()));
            Assert.Equal(3, recording.QueueCount); Assert.False(recording.TrySubmit(Pixels())); Assert.Equal(1, recording.DroppedFrames);
            release.Set(); recording.Dispose(); Assert.Equal(4, recording.FramesWritten); Assert.Null(recording.Error);
            Assert.False(recording.TrySubmit(Pixels())); Assert.Equal(0, recording.QueueCount); recording.Dispose();
        }
        finally { release.Set(); recording.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void EncoderErrorIsVisibleAndPartialFileIsFinalizedWithoutLeakingTheWriter()
    {
        string path = NewPath();
        try
        {
            using VideoRecording recording = new(path, _ => throw new InvalidOperationException("encoder failed"));
            Assert.True(recording.TrySubmit(Pixels())); recording.Dispose();
            Assert.IsType<InvalidOperationException>(recording.Error); Assert.False(recording.Accepting);
            using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using StreamReader reader = new(exclusive, Encoding.Latin1); Assert.Contains("idx1", reader.ReadToEnd());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidOutputAndFramesFailClearlyAndNeverOverwriteAnExistingFile()
    {
        string path = NewPath(); File.WriteAllText(path, "keep");
        try { Assert.Throws<IOException>(() => new VideoRecording(path, Encode)); Assert.Equal("keep", File.ReadAllText(path)); }
        finally { File.Delete(path); }
        try
        {
            using VideoRecording recording = new(path, _ => new byte[4]);
            Assert.Throws<ArgumentException>(() => recording.TrySubmit(new byte[4]));
            recording.TrySubmit(Pixels()); recording.Dispose(); Assert.IsType<InvalidDataException>(recording.Error);
        }
        finally { File.Delete(path); }
        Assert.Throws<DirectoryNotFoundException>(() => new VideoRecording(Path.Combine(path, "missing", "video.avi"), Encode));
    }

    [Fact]
    public void CaptureCadenceTracks20FpsAt60HzAndCountsMissedSlotsWithoutBurstCatchup()
    {
        CaptureClock clock = new(); int frames = 0;
        for (int i = 0; i < 60; i++) if (clock.Due(i / 60d)) frames++;
        Assert.Equal(20, frames); Assert.False(clock.Due(double.NaN));
        Assert.True(clock.Due(2)); Assert.Equal(20, clock.Skipped); Assert.False(clock.Due(2));
        Assert.True(clock.Due(2.05)); Assert.Equal(0, clock.Skipped);
    }

    [Fact]
    public void AviIndexLimitClosesAPartialFileWithoutUnboundedIndexGrowth()
    {
        string path = NewPath();
        try
        {
            using MjpegAviWriter writer = new(path);
            byte[] markerOnly = { 0xff, 0xd8, 0xff, 0xd9 }; // Muxer limit fixture; decoded colour frames are checked separately.
            for (int i = 0; i < MjpegAviWriter.MaximumFrames; i++) writer.WriteFrame(markerOnly);
            Assert.Throws<IOException>(() => writer.WriteFrame(markerOnly));
            writer.Dispose(); Assert.Equal(MjpegAviWriter.MaximumFrames, writer.FramesWritten);
            using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.Length < MjpegAviWriter.MaximumFileBytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RecordingDoesNotChangeFixedTickSimulationOrAuthoredReplayDefinition()
    {
        string path = NewPath();
        try
        {
            var first = WorkspaceExamples.Furniture(); var second = WorkspaceExamples.Furniture();
            Assert.True(first.Finalise()); Assert.True(second.Finalise()); first.ToggleRunning(); second.ToggleRunning();
            using VideoRecording recording = new(path, Encode);
            for (int i = 0; i < 30; i++)
            {
                first.Advance(first.StepSeconds); second.Advance(second.StepSeconds);
                if (i % 10 == 0) recording.TrySubmit(Pixels());
                Assert.Equal(first.Frame.Agents.Select(a => a.Position), second.Frame.Agents.Select(a => a.Position));
                Assert.Equal(first.Tick, second.Tick);
            }
            recording.Dispose(); Assert.Null(recording.Error);
            Assert.Equal(first.ExportAuthoredJson("test"), second.ExportAuthoredJson("test"));
        }
        finally { File.Delete(path); }
    }
}
