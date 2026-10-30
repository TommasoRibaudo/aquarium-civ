using System;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The box's land: its size, the ground's height function and the pond. The default is exactly the style kit's
    /// terrain (StyleBox.Height: hills at the back and left, a pond front right, a flat founding spot); the Growth Lab's
    /// presets (mirrored, pond at the back) are the same function with other parameters. Pure maths, no scene state.
    /// </summary>
    [Serializable]
    public class Land
    {
        // Box interior half-size, the ground's base height, the glass top and the pond's water level.
        public const float HX = 3f, HZ = 2.2f, G = 1f, GlassTop = 2.95f, WaterY = G - 0.08f;

        public bool mirror;
        public float noiseOffset;
        public Vector2 pond = new(1.75f, -1.15f);
        public Vector2 centre = new(-0.35f, -0.25f);

        public static Land StyleKit() => new();
        public static Land Mirrored() => new() { mirror = true, noiseOffset = 13.7f, pond = new Vector2(-1.75f, -1.15f), centre = new Vector2(0.35f, -0.2f) };
        public static Land BackPond() => new() { noiseOffset = 29.3f, pond = new Vector2(1.85f, 0.7f), centre = new Vector2(-0.45f, -0.5f) };

        public float Height(float x, float z)
        {
            float xm = mirror ? -x : x;
            float nx = xm + noiseOffset, nz = z + noiseOffset * 0.7f;
            float h = G + 0.07f * Fbm(nx * 0.9f, nz * 0.9f);
            float back = Smooth(0.1f, 2.1f, z), left = Smooth(2.6f, -2.8f, xm);
            h += 0.62f * back * (0.3f + 0.7f * left) * (0.7f + 0.6f * Fbm(nx * 0.6f, nz * 0.6f + 11f));
            float dp = Vector2.Distance(new Vector2(x, z), pond);
            h -= 0.24f * (1f - Smooth(0.45f, 0.98f, dp));
            float dc = Vector2.Distance(new Vector2(x, z), centre);
            h = Mathf.Lerp(h, G + 0.03f, (1f - Smooth(0.9f, 1.75f, dc)) * 0.92f);
            return h;
        }

        public Vector3 OnGround(float x, float z) => new(x, Height(x, z), z);
        public Vector3 OnGround(Vector2 p) => OnGround(p.x, p.y);

        public bool Inside(Vector2 p, float margin) => Mathf.Abs(p.x) <= HX - margin && Mathf.Abs(p.y) <= HZ - margin;

        public bool IsWater(float x, float z) => Height(x, z) < WaterY;

        /// <summary>
        /// The pond's shoreline: for each of <paramref name="segments"/> directions from the pond centre, the first point
        /// where the ground rises above the water (found by marching out, then bisecting). The water mesh and the sand
        /// rim follow this smooth curve, so the rim no longer shows the terrain grid's facets (spike polish note).
        /// </summary>
        public Vector2[] Shoreline(int segments = 64)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float lo = 0f, hi = 0f;
                for (float r = 0.02f; r < 2.5f; r += 0.02f)
                {
                    var p = pond + dir * r;
                    if (!Inside(p, 0.02f) || Height(p.x, p.y) >= WaterY) { hi = r; break; }
                    lo = r;
                    hi = r;
                }
                for (int k = 0; k < 12; k++)
                {
                    float m = (lo + hi) * 0.5f;
                    var p = pond + dir * m;
                    if (Inside(p, 0.02f) && Height(p.x, p.y) < WaterY) lo = m; else hi = m;
                }
                pts[i] = pond + dir * hi;
            }
            return pts;
        }

        // ---------------- noise ----------------
        public static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Hash3(int a, int b, int c) => Hash(a * 92821 + c * 31, b * 68917 - c * 17);

        public static float Noise(float x, float z)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float xf = x - xi, zf = z - zi;
            float u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), d = Hash(xi + 1, zi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float z) => Noise(x, z) * 0.57f + Noise(x * 2.03f + 17f, z * 2.03f) * 0.29f + Noise(x * 4.1f, z * 4.1f + 31f) * 0.14f;

        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
