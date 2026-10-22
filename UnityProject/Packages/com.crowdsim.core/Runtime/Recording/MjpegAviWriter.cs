#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CrowdSim.Recording
{
    /// <summary>Small indexed AVI 1.0 muxer. JPEG encoding is supplied by the host, never by an Editor package.</summary>
    public sealed class MjpegAviWriter : IDisposable
    {
        // Bound both AVI 1.0 offsets and the in-memory index; no unbounded OpenDML recording.
        public const long MaximumFileBytes = 1024L * 1024 * 1024;
        public const int MaximumFrames = 100000;
        private readonly BinaryWriter _writer;
        private readonly List<(uint Offset, uint Size)> _index = new List<(uint, uint)>();
        private readonly long _avihFrames, _streamFrames, _movi;
        private bool _closed;
        public int FramesWritten => _index.Count;

        public MjpegAviWriter(string path, int width = 1280, int height = 720, int framesPerSecond = 20)
        {
            if (width < 1 || width > short.MaxValue || height < 1 || height > short.MaxValue || framesPerSecond < 1 || framesPerSecond > 240)
                throw new ArgumentOutOfRangeException(nameof(width));
            _writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read), Encoding.ASCII);
            try
            {
                Four("RIFF"); _writer.Write(0u); Four("AVI ");
                long hdrl = List("hdrl");
                Four("avih"); _writer.Write(56u);
                _writer.Write((uint)(1000000 / framesPerSecond)); _writer.Write(0u); _writer.Write(0u); _writer.Write(0x10u);
                _avihFrames = Position; _writer.Write(0u); _writer.Write(0u); _writer.Write(1u); _writer.Write(0u);
                _writer.Write((uint)width); _writer.Write((uint)height); for (int i = 0; i < 4; i++) _writer.Write(0u);
                long strl = List("strl");
                Four("strh"); _writer.Write(56u); Four("vids"); Four("MJPG");
                _writer.Write(0u); _writer.Write((ushort)0); _writer.Write((ushort)0); _writer.Write(0u);
                _writer.Write(1u); _writer.Write((uint)framesPerSecond); _writer.Write(0u);
                _streamFrames = Position; _writer.Write(0u); _writer.Write(0u); _writer.Write(uint.MaxValue); _writer.Write(0u);
                _writer.Write((short)0); _writer.Write((short)0); _writer.Write((short)width); _writer.Write((short)height);
                Four("strf"); _writer.Write(40u); _writer.Write(40u); _writer.Write(width); _writer.Write(height);
                _writer.Write((ushort)1); _writer.Write((ushort)24); Four("MJPG"); _writer.Write((uint)(width * height * 3));
                for (int i = 0; i < 4; i++) _writer.Write(0u);
                EndList(strl); EndList(hdrl); _movi = List("movi");
            }
            catch { _writer.Dispose(); throw; }
        }

        /// <summary>Append one complete JPEG. Stop before the bounded AVI/index limit, retaining a playable partial file.</summary>
        public void WriteFrame(byte[] jpeg)
        {
            if (_closed) throw new ObjectDisposedException(nameof(MjpegAviWriter));
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8 || jpeg[jpeg.Length - 2] != 0xff || jpeg[jpeg.Length - 1] != 0xd9)
                throw new InvalidDataException("Encoder did not return a complete JPEG frame.");
            if (_index.Count >= MaximumFrames || Position + jpeg.Length + 32L + 16L * (_index.Count + 1) > MaximumFileBytes)
                throw new IOException("AVI limit reached (1 GiB or 100,000 frames). Start a new recording.");
            long chunk = Position; Four("00dc"); _writer.Write((uint)jpeg.Length); _writer.Write(jpeg);
            if ((jpeg.Length & 1) != 0) _writer.Write((byte)0);
            _index.Add(((uint)(chunk - (_movi + 4)), (uint)jpeg.Length));
        }

        private long Position => _writer.BaseStream.Position;
        private void Four(string value) => _writer.Write(Encoding.ASCII.GetBytes(value));
        private long List(string kind) { Four("LIST"); long size = Position; _writer.Write(0u); Four(kind); return size; }
        private void Patch(long offset, uint value) { long end = Position; _writer.BaseStream.Position = offset; _writer.Write(value); _writer.BaseStream.Position = end; }
        private void EndList(long size) => Patch(size, checked((uint)(Position - size - 4)));
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            try
            {
                EndList(_movi); Four("idx1"); _writer.Write((uint)(_index.Count * 16));
                foreach ((uint offset, uint size) in _index) { Four("00dc"); _writer.Write(0x10u); _writer.Write(offset); _writer.Write(size); }
                Patch(_avihFrames, (uint)_index.Count); Patch(_streamFrames, (uint)_index.Count); Patch(4, checked((uint)(Position - 8)));
                _writer.Flush();
            }
            finally { _writer.Dispose(); }
        }
    }
}
