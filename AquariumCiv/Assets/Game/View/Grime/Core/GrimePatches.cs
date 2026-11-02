using System;
using System.Collections.Generic;

namespace AquariumCiv.View.Grime
{
    /// <summary>
    /// Where a pane's grime leaves clean glass, as pure maths (#121). The old look repeated: the glass shader tiled one 128 px
    /// detail texture every 1.67 u with the same offset on every pane, and the growth weights were one Perlin octave. Here
    /// the growth weight of a pane is built from a seed: a low-frequency field with its own scale, rotation and warp, and a
    /// handful of clean patches whose count, size, aspect, tilt and ragged outline all vary. No two panes share a pattern,
    /// and no pattern repeats within a pane. UnityEngine-free so it is unit-tested with the sim.
    /// </summary>
    public static class GrimePatches
    {
        public struct Patch
        {
            public float cx, cy;        // centre, world units from the pane's lower-left
            public float rx, ry;        // radii before tilt (world units)
            public float tilt;          // radians
            public float lobeAmp, lobeFreq, lobePhase; // outline wobble: radius * (1 + amp * sin(freq * angle + phase))
            public float clean;         // 0..1 how clean the middle gets
        }

        /// <summary>The clean patches of one pane. Dart-throwing with a spacing that follows the sizes, so patches neither stack nor sit on a grid.</summary>
        public static List<Patch> Place(int seed, float width, float height)
        {
            var rng = new Rng(seed * 7919 + 13);
            float area = width * height;
            float density = rng.Range(0.22f, 0.6f);                 // patches per square unit varies pane to pane
            int want = Math.Max(2, (int)MathF.Round(area * density + rng.Range(-1.5f, 1.5f)));
            var list = new List<Patch>();
            for (int tries = 0; tries < want * 30 && list.Count < want; tries++)
            {
                // Sizes: many small, a few big (a long smear down the glass is possible).
                float baseR = 0.12f * MathF.Exp(rng.Range(0f, 2.1f));       // 0.12 .. ~0.98 u
                float aspect = rng.Range(0.3f, 1f);
                var p = new Patch
                {
                    cx = rng.Range(0f, width), cy = rng.Range(0f, height),
                    rx = baseR, ry = baseR * aspect, tilt = rng.Range(0f, MathF.PI),
                    lobeAmp = rng.Range(0.05f, 0.38f), lobeFreq = rng.Int(2, 6), lobePhase = rng.Range(0f, 6.2832f),
                    clean = rng.Range(0.7f, 1f),
                };
                bool ok = true;
                foreach (var q in list)
                {
                    float dx = p.cx - q.cx, dy = p.cy - q.cy;
                    float min = (Math.Max(p.rx, p.ry) + Math.Max(q.rx, q.ry)) * 0.7f;
                    if (dx * dx + dy * dy < min * min) { ok = false; break; }
                }
                if (ok) list.Add(p);
            }
            return list;
        }

        /// <summary>0 at a patch's edge or outside, 1 in its middle (soft edge), for the strongest patch at the point.</summary>
        public static float Cleanness(List<Patch> patches, float x, float y)
        {
            float best = 0f;
            foreach (var p in patches)
            {
                float dx = x - p.cx, dy = y - p.cy;
                float c = MathF.Cos(p.tilt), s = MathF.Sin(p.tilt);
                float u = (dx * c + dy * s) / p.rx, v = (-dx * s + dy * c) / p.ry;
                float d = MathF.Sqrt(u * u + v * v);
                if (d > 1.6f) continue;
                float ang = MathF.Atan2(v, u);
                float edge = 1f + p.lobeAmp * MathF.Sin(p.lobeFreq * ang + p.lobePhase);
                float k = d / edge;                       // 1 = the ragged outline
                float t = Clamp01((1.05f - k) / 0.75f);   // a wide soft band inside the outline: with less, heavy grime cut the patch out like a paper shape
                best = Math.Max(best, t * t * (3f - 2f * t) * p.clean);
            }
            return best;
        }

        /// <summary>The growth weights for a width x height mask of a pane of this world size: about 0.55..1.45 on dirty ground
        /// (as before), falling to ~0.05 inside a clean patch. Row-major, y up, like GlassMask.</summary>
        public static float[] Weights(int seed, int width, int height, float sizeX, float sizeY)
        {
            var patches = Place(seed, sizeX, sizeY);
            var rng = new Rng(seed * 104729 + 7);
            // The base field: its own scale per axis, a rotation, a domain warp and an offset, all from the seed.
            float scale = rng.Range(0.7f, 1.9f), stretch = rng.Range(0.6f, 1.6f), rot = rng.Range(0f, MathF.PI * 2f);
            float ox = rng.Range(0f, 400f), oy = rng.Range(0f, 400f), warp = rng.Range(0.25f, 0.9f);
            float cr = MathF.Cos(rot), sr = MathF.Sin(rot);
            int salt = seed * 31 + 5;
            var w = new float[width * height];
            for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                float x = (i + 0.5f) / width * sizeX, y = (j + 0.5f) / height * sizeY;
                float u = (x * cr - y * sr) * scale, v = (x * sr + y * cr) * scale * stretch;
                float wx = Noise(u * 0.8f + 11f, v * 0.8f, salt + 1) - 0.5f, wy = Noise(u * 0.8f, v * 0.8f + 23f, salt + 2) - 0.5f;
                u += wx * warp * 2f; v += wy * warp * 2f;
                float n = Noise(u + ox, v + oy, salt) * 0.62f + Noise(u * 2.3f + ox, v * 2.3f + oy, salt + 3) * 0.38f;
                float baseW = 0.55f + 0.9f * Clamp01((n - 0.25f) / 0.5f);
                float fall = 1.2f - 0.4f * (y / sizeY);               // dirtier towards the bottom, as before
                w[j * width + i] = baseW * fall * (1f - 0.95f * Cleanness(patches, x, y));
            }
            return w;
        }

        // ---------- the measure ----------

        /// <summary>The field minus its own local average (a box blur of <paramref name="radiusUnits"/>): the smudges and patches
        /// without the slow gradient every pane shares (dirtier at the bottom), so smoothness alone does not read as repetition.</summary>
        public static float[] Detail(float[] f, int width, int height, float sizeX, float sizeY, float radiusUnits = 0.5f)
        {
            int rx = Math.Max(1, (int)(radiusUnits / (sizeX / width))), ry = Math.Max(1, (int)(radiusUnits / (sizeY / height)));
            var tmp = new float[f.Length]; var o = new float[f.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float sum = 0; int n = 0;
                    for (int k = Math.Max(0, x - rx); k <= Math.Min(width - 1, x + rx); k++) { sum += f[y * width + k]; n++; }
                    tmp[y * width + x] = sum / n;
                }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float sum = 0; int n = 0;
                    for (int k = Math.Max(0, y - ry); k <= Math.Min(height - 1, y + ry); k++) { sum += tmp[k * width + x]; n++; }
                    o[y * width + x] = f[y * width + x] - sum / n;
                }
            return o;
        }

        /// <summary>
        /// How much a field repeats: the largest normalised correlation of the field with itself shifted by any lag between
        /// <paramref name="minLagUnits"/> and <paramref name="maxLagUnits"/> world units. 1 = an exact repeat at some shift
        /// (a tiled texture); an unrelated pattern scores near 0.
        /// </summary>
        public static float Repetition(float[] f, int width, int height, float sizeX, float sizeY, float minLagUnits, float maxLagUnits)
        {
            double mean = 0;
            foreach (var v in f) mean += v;
            mean /= f.Length;
            double var0 = 0;
            foreach (var v in f) var0 += (v - mean) * (v - mean);
            if (var0 <= 1e-9) return 1f; // a flat field repeats trivially
            float best = 0f;
            float pxX = sizeX / width, pxY = sizeY / height;
            int maxLx = Math.Min(width - 8, (int)(maxLagUnits / pxX)), maxLy = Math.Min(height - 8, (int)(maxLagUnits / pxY));
            for (int ly = -maxLy; ly <= maxLy; ly++)
            for (int lx = -maxLx; lx <= maxLx; lx++)
            {
                float dxu = lx * pxX, dyu = ly * pxY;
                float d = MathF.Sqrt(dxu * dxu + dyu * dyu);
                if (d < minLagUnits || d > maxLagUnits) continue;
                double sum = 0, a2 = 0, b2 = 0; int n = 0;
                for (int y = Math.Max(0, -ly); y < Math.Min(height, height - ly); y++)
                for (int x = Math.Max(0, -lx); x < Math.Min(width, width - lx); x++)
                {
                    double a = f[y * width + x] - mean, b = f[(y + ly) * width + x + lx] - mean;
                    sum += a * b; a2 += a * a; b2 += b * b; n++;
                }
                if (n < width * height / 4 || a2 <= 1e-9 || b2 <= 1e-9) continue;
                best = Math.Max(best, (float)(sum / Math.Sqrt(a2 * b2)));
            }
            return best;
        }

        /// <summary>Normalised correlation of two same-sized fields at zero shift (how alike two panes look).</summary>
        public static float Similarity(float[] a, float[] b)
        {
            double ma = 0, mb = 0;
            for (int i = 0; i < a.Length; i++) { ma += a[i]; mb += b[i]; }
            ma /= a.Length; mb /= b.Length;
            double s = 0, aa = 0, bb = 0;
            for (int i = 0; i < a.Length; i++) { double x = a[i] - ma, y = b[i] - mb; s += x * y; aa += x * x; bb += y * y; }
            return aa <= 1e-9 || bb <= 1e-9 ? 1f : (float)(s / Math.Sqrt(aa * bb));
        }

        // ---------- small pure helpers ----------
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        sealed class Rng
        {
            uint s;
            public Rng(int seed) { s = (uint)seed * 2654435761u + 0x9E3779B9u; if (s == 0) s = 1; for (int i = 0; i < 4; i++) Next(); }
            uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
            public float Float() => (Next() & 0xFFFFFF) / 16777216f;
            public float Range(float a, float b) => a + (b - a) * Float();
            public int Int(int a, int b) => a + (int)(Float() * (b - a + 1) - 1e-4f);
        }

        static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float Noise(float x, float y, int salt)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, salt), b = Hash(xi + 1, yi, salt), c = Hash(xi, yi + 1, salt), d = Hash(xi + 1, yi + 1, salt);
            float top = a + (b - a) * u, bot = c + (d - c) * u;
            return top + (bot - top) * v;
        }
    }
}
