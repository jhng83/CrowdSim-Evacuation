using System.Text;
using System.Buffers.Binary;
using CrowdSim.Migration.Compatibility;
using Xunit;

namespace CrowdSim.Migration.Tests;

public sealed class RecordingDependencyTests
{
    [Fact]
    public void StandardCompatibleEncodingAssetsProduceAnIndexedTwoFrameMjpegAvi()
    {
        string path = Path.Combine(Path.GetTempPath(), "crowdsim-m1-" + Guid.NewGuid().ToString("N") + ".avi");
        try
        {
            RecordingDependencyProbe.WriteTwoFrames(path);
            byte[] file = File.ReadAllBytes(path);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(file, 0, 4));
            Assert.Equal("AVI ", Encoding.ASCII.GetString(file, 8, 4));
            string chunks = Encoding.Latin1.GetString(file);
            Assert.Contains("MJPG", chunks);
            Assert.Contains("idx1", chunks);
            int header = chunks.IndexOf("avih", StringComparison.Ordinal);
            int index = chunks.LastIndexOf("idx1", StringComparison.Ordinal);
            Assert.True(header >= 0 && index >= 0);
            Assert.Equal(50000u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(header + 8, 4)));
            Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(header + 24, 4)));
            Assert.Equal(64u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(header + 40, 4)));
            Assert.Equal(64u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(header + 44, 4)));
            Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(index + 4, 4)));
            string? artifact = Environment.GetEnvironmentVariable("CROWDSIM_PROBE_ARTIFACT");
            if (!string.IsNullOrWhiteSpace(artifact))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(artifact))!);
                File.Copy(path, artifact, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
