using System;
using System.IO;
using System.IO.Compression;

namespace EcologyLab
{
    /// <summary>An RGB canvas, a tiny 3 × 5 pixel font, and a PNG writer (zlib from System.IO.Compression).</summary>
    public sealed class Canvas
    {
        public readonly int w, h;
        public readonly byte[] px; // RGB

        public Canvas(int w, int h, uint fill = 0xFFFFFF)
        {
            this.w = w; this.h = h; px = new byte[w * h * 3];
            Rect(0, 0, w, h, fill);
        }

        public void Set(int x, int y, uint rgb)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int o = (y * w + x) * 3;
            px[o] = (byte)(rgb >> 16); px[o + 1] = (byte)(rgb >> 8); px[o + 2] = (byte)rgb;
        }

        public uint Get(int x, int y)
        {
            int o = (y * w + x) * 3;
            return (uint)(px[o] << 16 | px[o + 1] << 8 | px[o + 2]);
        }

        public void Rect(int x, int y, int rw, int rh, uint rgb)
        {
            for (int j = Math.Max(0, y); j < Math.Min(h, y + rh); j++)
                for (int i = Math.Max(0, x); i < Math.Min(w, x + rw); i++) Set(i, j, rgb);
        }

        public void Frame(int x, int y, int rw, int rh, uint rgb)
        {
            Rect(x, y, rw, 1, rgb); Rect(x, y + rh - 1, rw, 1, rgb); Rect(x, y, 1, rh, rgb); Rect(x + rw - 1, y, 1, rh, rgb);
        }

        public void Blit(Canvas src, int x, int y, int scaleDown = 1)
        {
            for (int j = 0; j < src.h / scaleDown; j++)
                for (int i = 0; i < src.w / scaleDown; i++)
                {
                    if (scaleDown == 1) { Set(x + i, y + j, src.Get(i, j)); continue; }
                    int r = 0, g = 0, b = 0, n = scaleDown * scaleDown;
                    for (int dj = 0; dj < scaleDown; dj++)
                        for (int di = 0; di < scaleDown; di++)
                        {
                            uint c = src.Get(i * scaleDown + di, j * scaleDown + dj);
                            r += (int)(c >> 16 & 255); g += (int)(c >> 8 & 255); b += (int)(c & 255);
                        }
                    Set(x + i, y + j, (uint)(r / n << 16 | g / n << 8 | b / n));
                }
        }

        public static uint Mix(uint a, uint b, float t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            int r = (int)((a >> 16 & 255) * (1 - t) + (b >> 16 & 255) * t);
            int g = (int)((a >> 8 & 255) * (1 - t) + (b >> 8 & 255) * t);
            int bl = (int)((a & 255) * (1 - t) + (b & 255) * t);
            return (uint)(r << 16 | g << 8 | bl);
        }

        public static uint Shade(uint a, float k)
        {
            int r = Math.Clamp((int)((a >> 16 & 255) * k), 0, 255), g = Math.Clamp((int)((a >> 8 & 255) * k), 0, 255), b = Math.Clamp((int)((a & 255) * k), 0, 255);
            return (uint)(r << 16 | g << 8 | b);
        }

        // ------------------------------------------------------------------ text
        const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ.:-%/()+= ";
        static readonly ushort[] Glyphs =
        {
            0x7B6F, 0x2C97, 0x73E7, 0x73CF, 0x5BC9, 0x79CF, 0x79EF, 0x7292, 0x7BEF, 0x7BCF,
            0x2BED, 0x6BAE, 0x3923, 0x6B6E, 0x79A7, 0x79A4, 0x396B, 0x5BED, 0x7497, 0x126A,
            0x5BAD, 0x4927, 0x5FED, 0x6B6D, 0x2B6A, 0x6BA4, 0x2B73, 0x6BAD, 0x388E, 0x7492,
            0x5B6F, 0x5B6A, 0x5BFD, 0x5AAD, 0x5A92, 0x72A7, 0x0002, 0x0410, 0x01C0, 0x52A5,
            0x12A4, 0x2922, 0x224A, 0x05D0, 0x0E38, 0x0000,
        };

        /// <summary>Draws upper-case text at a pixel scale; returns the width drawn.</summary>
        public int Text(int x, int y, string s, uint rgb, int scale = 2)
        {
            int cx = x;
            foreach (char ch0 in s.ToUpperInvariant())
            {
                int k = Chars.IndexOf(ch0);
                if (k < 0) k = Chars.Length - 1;
                ushort g = Glyphs[k];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if ((g >> (14 - (row * 3 + col)) & 1) != 0) Rect(cx + col * scale, y + row * scale, scale, scale, rgb);
                cx += 4 * scale;
            }
            return cx - x;
        }

        public static int TextWidth(string s, int scale = 2) => s.Length * 4 * scale;

        // ------------------------------------------------------------------ PNG
        public void SavePng(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h);
            ihdr[8] = 8; ihdr[9] = 2; // 8-bit RGB
            Chunk(fs, "IHDR", ihdr);
            using (var raw = new MemoryStream())
            {
                using (var z = new ZLibStream(raw, CompressionLevel.SmallestSize, true))
                    for (int y = 0; y < h; y++) { z.WriteByte(0); z.Write(px, y * w * 3, w * 3); }
                Chunk(fs, "IDAT", raw.ToArray());
            }
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; BE(len, 0, (uint)data.Length); s.Write(len);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Buffer.BlockCopy(data, 0, td, 4, data.Length);
            s.Write(td);
            var crc = new byte[4]; BE(crc, 0, Crc(td)); s.Write(crc);
        }

        static uint[] crcTable;
        static uint Crc(byte[] d)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; crcTable[n] = c; }
            }
            uint r = 0xFFFFFFFFu;
            foreach (byte b in d) r = crcTable[(r ^ b) & 0xFF] ^ (r >> 8);
            return r ^ 0xFFFFFFFFu;
        }
    }
}
