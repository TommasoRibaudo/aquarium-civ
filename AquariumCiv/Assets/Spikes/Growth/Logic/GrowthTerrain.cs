using System;

namespace AquariumCiv.Spikes.Growth
{
    /// <summary>
    /// One box's land: where the pond and the flat founding spot are, and whether the hills are mirrored. Preset 0 is
    /// exactly the style kit's box (StyleBox.Height); 1 and 2 vary it so the three time-lapse seeds don't share a map.
    /// </summary>
    public sealed class TerrainSpec
    {
        public string name;
        public bool mirror;
        public float noiseOff;
        public float pondX, pondZ;
        public float centreX, centreZ;

        public static TerrainSpec Preset(int i)
        {
            switch (((i % 3) + 3) % 3)
            {
                case 1: return new TerrainSpec { name = "mirror", mirror = true, noiseOff = 13.7f, pondX = -1.75f, pondZ = -1.15f, centreX = 0.35f, centreZ = -0.2f };
                case 2: return new TerrainSpec { name = "backpond", mirror = false, noiseOff = 29.3f, pondX = 1.85f, pondZ = 0.7f, centreX = -0.45f, centreZ = -0.5f };
                default: return new TerrainSpec { name = "stylebox", mirror = false, noiseOff = 0f, pondX = 1.75f, pondZ = -1.15f, centreX = -0.35f, centreZ = -0.25f };
            }
        }
    }

    /// <summary>The style kit's terrain function (StyleBox.Height and its noise), copied into pure C# (research 08 §5.1).</summary>
    public static class GrowthTerrain
    {
        public const float HX = 3f, HZ = 2.2f, G = 1f, WaterY = G - 0.08f, GlassTop = 2.95f;
        /// <summary>No lot within this distance of the glass (readability, research 08 §3.3).</summary>
        public const float GlassMargin = 0.25f;

        public static float Height(TerrainSpec t, float x, float z)
        {
            float xm = t.mirror ? -x : x;
            float nx = xm + t.noiseOff, nz = z + t.noiseOff * 0.7f;
            float h = G + 0.07f * Fbm(nx * 0.9f, nz * 0.9f);
            float back = Smooth(0.1f, 2.1f, z), left = Smooth(2.6f, -2.8f, xm);
            h += 0.62f * back * (0.3f + 0.7f * left) * (0.7f + 0.6f * Fbm(nx * 0.6f, nz * 0.6f + 11f));
            float dp = Dist(x, z, t.pondX, t.pondZ);
            h -= 0.24f * (1f - Smooth(0.45f, 0.98f, dp));
            float dc = Dist(x, z, t.centreX, t.centreZ);
            h = Lerp(h, G + 0.03f, (1f - Smooth(0.9f, 1.75f, dc)) * 0.92f);
            return h;
        }

        /// <summary>Where trees grow at the start: the back and the "left" hills (as StyleBox's nature bias).</summary>
        public static float ForestBias(TerrainSpec t, float x, float z)
        {
            float xm = t.mirror ? -x : x;
            return Smooth(-0.6f, 1.8f, z) * 0.6f + Smooth(1.5f, -2.5f, xm) * 0.5f;
        }

        public static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Smooth(float a, float b, float x)
        {
            float t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>A stable 0..1 hash of three ints (tie-breaks, lot sizes, jitter): never draws from the sim's dice.</summary>
        public static float Hash3(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Noise(float x, float z)
        {
            int xi = (int)MathF.Floor(x), zi = (int)MathF.Floor(z);
            float xf = x - xi, zf = z - zi;
            float u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), d = Hash(xi + 1, zi + 1);
            return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float z) => Noise(x, z) * 0.57f + Noise(x * 2.03f + 17f, z * 2.03f) * 0.29f + Noise(x * 4.1f, z * 4.1f + 31f) * 0.14f;
    }
}
