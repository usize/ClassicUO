using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.RestApi
{
    // Captures the world render target (the isometric scene, without UI overlays)
    // as PNG bytes for the REST API.
    //
    // The API request thread parks on a TaskCompletionSource; the game thread
    // fills it from ApiGameController.Draw right after a frame has been rendered
    // (render-target contents are still valid until they are next bound, and
    // nothing binds them between present and the end of Draw).
    internal static class ScreenshotCapture
    {
        private static TaskCompletionSource<byte[]> _pending;

        // Returns null on timeout (no game-thread frame arrived in time).
        public static async Task<byte[]> RequestAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _pending, tcs);

            var finished = await Task.WhenAny(tcs.Task, Task.Delay(timeout)).ConfigureAwait(false);
            if (finished != tcs.Task)
            {
                Interlocked.CompareExchange(ref _pending, null, tcs);
                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        // Game thread, once per frame. Completes the pending request (if any).
        public static void TryComplete(RenderTargets renderTargets)
        {
            var tcs = Interlocked.Exchange(ref _pending, null);
            if (tcs == null)
            {
                return;
            }

            try
            {
                var rt = renderTargets?.WorldRenderTarget;
                if (rt == null || rt.IsDisposed)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                int width = rt.Width;
                int height = rt.Height;

                var pixels = new Color[width * height];
                rt.GetData(0, null, pixels, 0, pixels.Length);

                tcs.TrySetResult(PngEncoder.EncodeRgb24(width, height, pixels));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }
    }

    // Minimal 24-bit RGB PNG writer (no deps beyond System.IO.Compression).
    internal static class PngEncoder
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly uint[] CrcTable = BuildCrcTable();

        public static byte[] EncodeRgb24(int width, int height, Color[] pixels)
        {
            int stride = width * 3;
            var raw = new byte[height * (stride + 1)];

            int o = 0;
            for (int y = 0; y < height; y++)
            {
                raw[o++] = 0; // filter: None
                int i = y * width;
                for (int x = 0; x < width; x++)
                {
                    Color c = pixels[i++];
                    raw[o++] = c.R;
                    raw[o++] = c.G;
                    raw[o++] = c.B;
                }
            }

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                {
                    z.Write(raw, 0, raw.Length);
                }
                compressed = ms.ToArray();
            }

            using var png = new MemoryStream();
            png.Write(Signature, 0, Signature.Length);

            var ihdr = new byte[13];
            WriteBigEndian(ihdr, 0, (uint)width);
            WriteBigEndian(ihdr, 4, (uint)height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 2;  // color type: RGB
            ihdr[10] = 0; // compression
            ihdr[11] = 0; // filter
            ihdr[12] = 0; // interlace
            WriteChunk(png, "IHDR", ihdr);

            foreach (var idat in SplitChunks(compressed))
            {
                WriteChunk(png, "IDAT", idat);
            }

            WriteChunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        // Keep IDAT chunks under ~64KB (deflate back-compression guard).
        private static System.Collections.Generic.IEnumerable<byte[]> SplitChunks(byte[] data)
        {
            const int MaxChunk = 65536;
            for (int off = 0; off < data.Length; off += MaxChunk)
            {
                int len = Math.Min(MaxChunk, data.Length - off);
                var chunk = new byte[len];
                Buffer.BlockCopy(data, off, chunk, 0, len);
                yield return chunk;
            }
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBigEndian(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);

            var typeBytes = new byte[4];
            for (int i = 0; i < 4; i++)
                typeBytes[i] = (byte)type[i];
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);

            var crcInput = new byte[4 + data.Length];
            Buffer.BlockCopy(typeBytes, 0, crcInput, 0, 4);
            Buffer.BlockCopy(data, 0, crcInput, 4, data.Length);

            var crc = new byte[4];
            WriteBigEndian(crc, 0, Crc32(crcInput));
            s.Write(crc, 0, 4);
        }

        private static void WriteBigEndian(byte[] buf, int offset, uint value)
        {
            buf[offset + 0] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)value;
        }

        private static uint Crc32(byte[] bytes)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in bytes)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFF;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }
    }
}
