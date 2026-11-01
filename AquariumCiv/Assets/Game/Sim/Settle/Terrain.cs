using System;

namespace AquariumCiv.Sim.Settle
{
    /// <summary>
    /// One box's land: where the pond and the flat founding spot are, and whether the hills are mirrored. Preset 0 is
    /// exactly the style kit's box (StyleBox.Height); 1 and 2 vary it (Growth Lab's three maps). Ported from the spike.
    /// </summary>
    public sealed class TerrainSpec
    {
        public string name;
        public bool mirror;
        public float noiseOff;
        public float pondX, pondZ;
        public float centreX, centreZ;

        // Shape parameters (#115). The defaults ARE the river valley's numbers, so presets 0..2 (and any other int) come out
        // exactly as before; only the two setting presets below change them.
        public float groundNoise = 0.07f;                     // the ground's own roughness
        public float hillAmp = 0.62f, hillBase = 0.3f, hillLeft = 0.7f, hillMixA = 0.7f, hillMixB = 0.6f; // the back hills
        public float duneAmp = 0f, duneFreq = 1.6f;           // 0 = off: low ridged dunes
        public float pondDepth = 0.24f, pondR0 = 0.45f, pondR1 = 0.98f; // the dip that holds the water
        public float pondSX = 1f, pondSZ = 1f;                // water shape: a scale on x and z distance (below 1 = long that way: a stream)
        public float forestBackW = 0.6f, forestLeftW = 0.5f, forestScale = 1f, forestRing = 0f; // starting woods (ring = palms round the pond)
        public float forestCut = 0.62f;                       // SiteMap plants a tree where the bias passes this: higher = fewer
        public float outcropCut = 99f;                        // SiteMap puts rock where the rock noise passes this (99 = none)

        /// <summary>Distance to the pond's centre, stretched by pondSX/pondSZ (exactly the plain distance at 1/1).</summary>
        public float PondDist(float x, float z)
        {
            float dx = x - pondX, dz = z - pondZ;
            if (pondSX == 1f && pondSZ == 1f) return MathF.Sqrt(dx * dx + dz * dz);
            dx *= pondSX; dz *= pondSZ;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public const int Presets = 3;
        /// <summary>The land presets of box 2's settings. Kept out of 0..2 so no old value changes meaning; stored in
        /// <c>SettleState.terrain</c> as before (an int), so no save change.</summary>
        public const int DesertOasisPreset = 100, MountainVillagePreset = 101;

        /// <summary>The preset for a box's setting; <paramref name="fallback"/> (the config's) for the river valley and anything unknown.</summary>
        public static int PresetFor(string setting, int fallback = 0) => setting switch
        {
            BoxSetting.DesertOasis => DesertOasisPreset,
            BoxSetting.MountainVillage => MountainVillagePreset,
            _ => fallback,
        };

        /// <summary>The setting a land preset belongs to (the view's palette key).</summary>
        public static string SettingOf(int preset) => preset == DesertOasisPreset ? BoxSetting.DesertOasis : preset == MountainVillagePreset ? BoxSetting.MountainVillage : BoxSetting.RiverValley;

        public static TerrainSpec Preset(int i)
        {
            if (i == DesertOasisPreset)
                // Furthest from the valley: low dunes, hardly any hills, a small round oasis ringed with palms, little forest.
                return new TerrainSpec
                {
                    name = "desert_oasis", mirror = false, noiseOff = 41.9f, pondX = 1.3f, pondZ = 0.55f, centreX = -0.7f, centreZ = -0.45f,
                    groundNoise = 0.05f, hillAmp = 0.2f, hillBase = 0.5f, hillLeft = 0.5f, duneAmp = 0.16f, duneFreq = 1.5f,
                    pondDepth = 0.3f, pondR0 = 0.4f, pondR1 = 0.95f,
                    forestBackW = 0.1f, forestLeftW = 0.08f, forestScale = 0.5f, forestRing = 0.62f,
                };
            if (i == MountainVillagePreset)
                // A high meadow: steep slopes at the back and left, rock outcrops, dense pines, a stream across the front.
                return new TerrainSpec
                {
                    name = "mountain_village", mirror = true, noiseOff = 57.3f, pondX = 0.2f, pondZ = -1.6f, centreX = 0.5f, centreZ = -0.15f,
                    groundNoise = 0.09f, hillAmp = 1.25f, hillBase = 0.45f, hillLeft = 0.55f, hillMixA = 0.6f, hillMixB = 0.9f,
                    pondDepth = 0.22f, pondR0 = 0.2f, pondR1 = 0.55f, pondSX = 0.4f, pondSZ = 1f,
                    forestBackW = 0.7f, forestLeftW = 0.6f, forestScale = 1.25f, forestCut = 0.55f, outcropCut = 0.56f,
                };
            switch (((i % Presets) + Presets) % Presets)
            {
                case 1: return new TerrainSpec { name = "mirror", mirror = true, noiseOff = 13.7f, pondX = -1.75f, pondZ = -1.15f, centreX = 0.35f, centreZ = -0.2f };
                case 2: return new TerrainSpec { name = "backpond", mirror = false, noiseOff = 29.3f, pondX = 1.85f, pondZ = 0.7f, centreX = -0.45f, centreZ = -0.5f };
                default: return new TerrainSpec { name = "stylebox", mirror = false, noiseOff = 0f, pondX = 1.75f, pondZ = -1.15f, centreX = -0.35f, centreZ = -0.25f };
            }
        }
    }

    /// <summary>The style kit's terrain function (StyleBox.Height and its noise) in pure C#, plus the hashes placement
    /// uses instead of dice. Ported from Growth Lab's GrowthTerrain unchanged.</summary>
    public static class Terrain
    {
        /// <summary>Half sizes of the box's land, ground height, water level.</summary>
        public const float HX = 3f, HZ = 2.2f, G = 1f, WaterY = G - 0.08f;
        /// <summary>No lot within this distance of the glass (readability, research 08 §3.3).</summary>
        public const float GlassMargin = 0.25f;

        public static float Height(TerrainSpec t, float x, float z)
        {
            float xm = t.mirror ? -x : x;
            float nx = xm + t.noiseOff, nz = z + t.noiseOff * 0.7f;
            float h = G + t.groundNoise * Fbm(nx * 0.9f, nz * 0.9f);
            float back = Smooth(0.1f, 2.1f, z), left = Smooth(2.6f, -2.8f, xm);
            h += t.hillAmp * back * (t.hillBase + t.hillLeft * left) * (t.hillMixA + t.hillMixB * Fbm(nx * 0.6f, nz * 0.6f + 11f));
            if (t.duneAmp != 0f) h += t.duneAmp * Dunes(t, nx, nz);
            float dp = t.PondDist(x, z);
            h -= t.pondDepth * (1f - Smooth(t.pondR0, t.pondR1, dp));
            float dc = Dist(x, z, t.centreX, t.centreZ);
            h = Lerp(h, G + 0.03f, (1f - Smooth(0.9f, 1.75f, dc)) * 0.92f);
            return h;
        }

        /// <summary>Where trees grow at the start: the back and the "left" hills (as StyleBox's nature bias).</summary>
        public static float ForestBias(TerrainSpec t, float x, float z)
        {
            float xm = t.mirror ? -x : x;
            float b = (Smooth(-0.6f, 1.8f, z) * t.forestBackW + Smooth(1.5f, -2.5f, xm) * t.forestLeftW) * t.forestScale;
            if (t.forestRing != 0f) b += t.forestRing * Ring(t.PondDist(x, z));
            return b;
        }

        /// <summary>A band of palms round the water: 0 in the pond and beyond ~1.7, strongest about 0.9..1.1 out.</summary>
        static float Ring(float d) => Smooth(0.5f, 0.85f, d) * (1f - Smooth(1.15f, 1.7f, d));

        /// <summary>Ridged noise in 0..1 (sharp crests, wide troughs): the desert's dunes.</summary>
        static float Dunes(TerrainSpec t, float nx, float nz)
        {
            float a = 1f - MathF.Abs(2f * Noise(nx * t.duneFreq * 0.7f + 3f, nz * t.duneFreq * 1.3f) - 1f);
            float b = 1f - MathF.Abs(2f * Noise(nx * t.duneFreq * 1.7f, nz * t.duneFreq * 0.9f + 9f) - 1f);
            return a * 0.7f + b * 0.3f;
        }

        /// <summary>Where the starting rock outcrops stand (the mountain): high, off the flat founding spot, in patches. Never
        /// true while <see cref="TerrainSpec.outcropCut"/> is at its default.</summary>
        public static bool OutcropAt(TerrainSpec t, float x, float z, float height)
        {
            if (t.outcropCut > 1f) return false;
            if (Dist(x, z, t.centreX, t.centreZ) < 1.4f) return false;
            return height > G + 0.3f && Fbm(x * 2.2f + t.noiseOff, z * 2.2f + 77f) > t.outcropCut;
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

        /// <summary>The land at a spot, from the terrain alone (no placement results), so the economy may read it
        /// without placement ever changing the economy: how high, how steep, how far from the pond.</summary>
        public static void Land(TerrainSpec t, float x, float z, out float height, out float slope, out float pondDist)
        {
            height = Height(t, x, z);
            const float e = 0.05f;
            float gx = (Height(t, x + e, z) - Height(t, x - e, z)) / (2 * e);
            float gz = (Height(t, x, z + e) - Height(t, x, z - e)) / (2 * e);
            slope = MathF.Sqrt(gx * gx + gz * gz);
            // The pond's rim is where the height drops below the water: ~0.6 u round its centre on every preset.
            pondDist = MathF.Max(0f, t.PondDist(x, z) - 0.6f);
        }
    }
}
