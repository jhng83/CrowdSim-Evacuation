using SharpAvi.Codecs;
using SharpAvi.Output;

namespace CrowdSim.Migration.Compatibility
{
    /// <summary>Cloud-only dependency probe; does not implement Unity capture or install plugins.</summary>
    public static class RecordingDependencyProbe
    {
        /// <summary>Encode a red frame then a blue frame using the pinned Standard-compatible assets.</summary>
        public static void WriteTwoFrames(string path)
        {
            using (AviWriter writer = new AviWriter(path))
            {
                writer.FramesPerSecond = 20;
                writer.EmitIndex1 = true;
                IAviVideoStream stream = writer.AddMJpegImageSharpVideoStream(64, 64, quality: 78);
                byte[] pixels = new byte[64 * 64 * 4];
                for (int frame = 0; frame < 2; frame++)
                {
                    for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                    {
                        pixels[pixel] = frame == 1 ? (byte)255 : (byte)0;
                        pixels[pixel + 2] = frame == 0 ? (byte)255 : (byte)0;
                        pixels[pixel + 3] = 255;
                    }
                    stream.WriteFrame(true, pixels, 0, pixels.Length);
                }
            }
        }
    }
}
