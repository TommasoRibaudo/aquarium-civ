using System;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// One pane's grime mask (#37, ported from the prototypes' GlassGrime): 0 = clean, 1 = filthy, one float per texel, no
    /// scene state, so the wipe maths is unit-tested. Mask coordinates (uv) run 0..1 across the pane; <see cref="size"/>
    /// is the pane in world units so a stroke's radius is a world distance. Growth is uneven (noise weights, dirtier
    /// towards the bottom), wiping uses a soft round brush whose rim leaves streaks for a second pass.
    /// </summary>
    public class GlassMask
    {
        public readonly int width, height;
        public readonly Vector2 size;
        public readonly float[] grime, weight;
        public double sum;
        float norm = 1f;   // the mean growth weight, so the mask's mean follows the level it is given
        public bool dirty;
        public float glint, peakMean;

        public GlassMask(Vector2 size, float texelsPerUnit, int index)
        {
            this.size = size;
            width = Mathf.Clamp(Mathf.RoundToInt(size.x * texelsPerUnit), 8, 512);
            height = Mathf.Clamp(Mathf.RoundToInt(size.y * texelsPerUnit), 8, 512);
            grime = new float[width * height];
            weight = new float[width * height];
            float ox = index * 13.7f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width * size.x, v = y / (float)height;
                float n = Mathf.PerlinNoise(ox + u * 1.3f, v * size.y * 1.3f);
                weight[y * width + x] = Mathf.Lerp(0.55f, 1.45f, n) * (1.2f - 0.4f * v);
            }
            Renormalise();
        }

        /// <summary>Nothing grows on the glass below the ground (the soil hides it, and a far pane there can't be reached):
        /// the growth weight fades to 0 under <paramref name="groundY"/>(u), the ground's height in the pane's own units (0..size.y).</summary>
        public void NoGrimeBelow(Func<float, float> groundY)
        {
            for (int x = 0; x < width; x++)
            {
                float g = groundY((x + 0.5f) / width);
                for (int y = 0; y < height; y++)
                {
                    float wy = (y + 0.5f) / height * size.y;
                    float t = Mathf.Clamp01((wy - (g - 0.03f)) / 0.15f);
                    weight[y * width + x] *= t * t * (3f - 2f * t);
                }
            }
            Renormalise();
        }

        void Renormalise()
        {
            double s = 0;
            for (int i = 0; i < weight.Length; i++) s += weight[i];
            norm = Mathf.Max(0.05f, (float)(s / weight.Length));
        }

        public int Texels => grime.Length;
        public float Mean => (float)(sum / grime.Length);

        /// <summary>Fresh, even grime at this level (a new session, a loaded save).</summary>
        public void ResetTo(float level)
        {
            sum = 0;
            for (int i = 0; i < grime.Length; i++) sum += grime[i] = Mathf.Clamp01(level * weight[i] / norm);
            dirty = true;
            peakMean = Mean;
        }

        /// <summary>Time passing: every texel gets <paramref name="delta"/> more, scaled by its weight.</summary>
        public void Grow(float delta)
        {
            sum = 0;
            for (int i = 0; i < grime.Length; i++) sum += grime[i] = Mathf.Min(1f, grime[i] + delta * weight[i] / norm);
            dirty = true;
            peakMean = Mathf.Max(peakMean, Mean);
        }

        /// <summary>Something else cleaned (debug, clean all): scale the grime so its mean is about <paramref name="targetMean"/>.</summary>
        public void FadeTo(float targetMean)
        {
            float m = Mean;
            float k = targetMean <= 0.001f || m <= 1e-5f ? 0f : targetMean / m;
            sum = 0;
            for (int i = 0; i < grime.Length; i++) sum += grime[i] = Mathf.Clamp01(grime[i] * k);
            dirty = true;
        }

        /// <summary>Wipes everything (the last streaks of a nearly clean pane).</summary>
        public void Clear()
        {
            Array.Clear(grime, 0, grime.Length);
            sum = 0;
            dirty = true;
        }

        /// <summary>Average grime in a disc (world radius) around uv.</summary>
        public float GrimeAt(Vector2 uv, float radius)
        {
            float s = 0; int n = 0;
            ForDisc(uv, radius, (i, f) => { s += grime[i]; n++; });
            return n == 0 ? 0f : s / n;
        }

        /// <summary>Wipes a disc; <paramref name="strength"/> is the share removed at the centre. Returns the grime removed in
        /// texel units (divide by the glass's total texels for a share of all the glass).</summary>
        public double Brush(Vector2 uv, float radius, float strength)
        {
            double removed = 0;
            ForDisc(uv, radius, (i, f) =>
            {
                float before = grime[i];
                float after = before * (1f - strength * f);
                if (after < 0.02f) after = 0f;
                grime[i] = after;
                removed += before - after;
            });
            if (removed <= 0) return 0;
            sum -= removed;
            dirty = true;
            return removed;
        }

        /// <summary>Wipes along a segment (stamps a third of the radius apart).</summary>
        public double BrushLine(Vector2 fromUv, Vector2 toUv, float radius, float strength)
        {
            var worldDelta = Vector2.Scale(toUv - fromUv, size);
            int steps = Mathf.Max(1, Mathf.CeilToInt(worldDelta.magnitude / Mathf.Max(1e-4f, radius * 0.33f)));
            double total = 0;
            for (int s = 1; s <= steps; s++) total += Brush(Vector2.Lerp(fromUv, toUv, s / (float)steps), radius, strength);
            return total;
        }

        /// <summary>Did a wipe just take this pane from dirty to sparkling? Re-arms after it grows dirty again.</summary>
        public bool CheckCleared()
        {
            float mean = Mean;
            if (peakMean > 0.2f && mean < 0.05f) { peakMean = mean; glint = 1f; return true; }
            return false;
        }

        /// <summary>The 8-bit mask bytes for the texture (R8).</summary>
        public void ToBytes(byte[] bytes)
        {
            for (int i = 0; i < grime.Length; i++) bytes[i] = (byte)(grime[i] * 255f + 0.5f);
        }

        void ForDisc(Vector2 uv, float radius, Action<int, float> visit)
        {
            float cx = uv.x * width, cy = uv.y * height;
            float rx = radius / size.x * width, ry = radius / size.y * height;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(cx + rx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(cy + ry));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float d2 = dx * dx + dy * dy;
                if (d2 >= 1f) continue;
                float f = 1f - d2; // soft edge
                visit(y * width + x, f * f * (3f - 2f * f));
            }
        }
    }

    /// <summary>The care layer's pure numbers (unit-tested): how mess dims the sun, and the spotless thresholds.</summary>
    public static class CareMath
    {
        /// <summary>Gate 1 (decisions.md): the sunlight dims by at most this share when the mess is full (48 h).</summary>
        public const float MaxSunDim = 0.25f;
        /// <summary>The glass counts as spotless below this mean grime (a few faint streaks), and they are wiped away.</summary>
        public const float SpotlessMean = 0.04f;

        /// <summary>The sun's intensity factor for a mess level 0..1: 1 when clean, 0.75 at full mess.</summary>
        public static float SunScale(float messLevel) => 1f - MaxSunDim * Mathf.Clamp01(messLevel);

        /// <summary>Mean grime over several panes, weighted by their texel counts.</summary>
        public static float WeightedMean(GlassMask[] panes)
        {
            double s = 0; long n = 0;
            foreach (var p in panes) { s += p.sum; n += p.Texels; }
            return n == 0 ? 0f : (float)(s / n);
        }
    }
}
