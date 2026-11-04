using System;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #274: the bridge where a path crosses a canal. Planks on stringers with a low rail in the camp and the village; a
    /// slightly arched stone deck with parapets from the town. <see cref="Deck"/> is the walking surface: people on the
    /// span follow it (<see cref="BoxView.SurfaceHeight"/>), so they cross on the deck, not through the water.
    /// </summary>
    public static class Bridges
    {
        public const float Lift = 0.03f, Arch = 0.03f;

        /// <summary>The deck's top at a point of the span (x/z along and across), or false if the point is off the bridge.</summary>
        public static bool Deck(BridgeData b, Func<float, float, float> ground, Vector2 p, out float y)
        {
            y = 0f;
            Vector2 d = b.dir.normalized, n = new(-d.y, d.x), r = p - b.centre;
            float t = Vector2.Dot(r, d), s = Vector2.Dot(r, n), half = b.length / 2f;
            if (Mathf.Abs(t) > half || Mathf.Abs(s) > b.width / 2f) return false;
            y = DeckAt(b, ground, t);
            return true;
        }

        /// <summary>The deck's top at distance t along the span from its centre.</summary>
        public static float DeckAt(BridgeData b, Func<float, float, float> ground, float t)
        {
            Vector2 d = b.dir.normalized, half = d * (b.length / 2f);
            Vector2 a = b.centre - half, e = b.centre + half;
            float ya = ground(a.x, a.y), ye = ground(e.x, e.y), yc = ground(b.centre.x, b.centre.y);
            float u = Mathf.Clamp01(t / b.length + 0.5f);
            float line = Mathf.Max(Mathf.Lerp(ya, ye, u), yc);
            return line + Lift + (b.Stone ? Arch * (1f - (2f * u - 1f) * (2f * u - 1f)) : 0f);
        }

        public static void Build(MeshBuilder mb, BridgeData b, Func<float, float, float> ground)
        {
            Vector2 d = b.dir.normalized, n = new(-d.y, d.x);
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            var yawQ = Quaternion.Euler(0, yaw, 0);
            float L = b.length, W = b.width;
            Vector3 At(float t, float s, float y) => new(b.centre.x + d.x * t + n.x * s, y, b.centre.y + d.y * t + n.y * s);
            // Local frame: +z along the span, +x across.
            if (b.Stone)
            {
                var slab = Palette.Stone; var dark = Color.Lerp(Palette.Stone, Palette.RockDark, 0.5f);
                const int segs = 8;
                for (int k = 0; k < segs; k++)
                {
                    float t0 = -L / 2f + L * k / segs, t1 = t0 + L / segs, tm = (t0 + t1) / 2f;
                    float y0 = DeckAt(b, ground, t0), y1 = DeckAt(b, ground, t1), ym = (y0 + y1) / 2f;
                    var tilt = yawQ * Quaternion.Euler(-Mathf.Atan2(y1 - y0, t1 - t0) * Mathf.Rad2Deg, 0, 0);
                    float seg = (t1 - t0) * 1.04f;
                    var c = k % 2 == 0 ? slab : Color.Lerp(slab, Palette.RockLight, 0.35f);
                    mb.Box(At(tm, 0, ym - 0.022f), new Vector3(W, 0.044f, seg), c, tilt);
                    foreach (int sd in new[] { -1, 1 })
                        mb.Box(At(tm, sd * (W / 2f - 0.01f), ym + 0.018f), new Vector3(0.026f, 0.036f, seg), dark, tilt);
                }
                foreach (int sd in new[] { -1, 1 }) // abutment blocks where the span meets the banks
                    foreach (int e in new[] { -1, 1 })
                        mb.Box(At(e * (L / 2f - 0.02f), sd * (W / 2f - 0.02f), DeckAt(b, ground, e * L / 2f) - 0.03f), new Vector3(0.05f, 0.07f, 0.05f), dark, yawQ);
            }
            else
            {
                var plank = Palette.WoodLight; var dk = Color.Lerp(Palette.WoodLight, Palette.Bark, 0.35f);
                float y = DeckAt(b, ground, 0f);
                float yA = DeckAt(b, ground, -L / 2f), yE = DeckAt(b, ground, L / 2f);
                var pitch = yawQ * Quaternion.Euler(-Mathf.Atan2(yE - yA, L) * Mathf.Rad2Deg, 0, 0);
                foreach (int sd in new[] { -1, 1 }) // stringers under the deck
                    mb.Box(At(0, sd * (W / 2f - 0.02f), y - 0.03f), new Vector3(0.022f, 0.026f, L * 1.02f), Palette.Timber, pitch);
                int n2 = Mathf.Max(4, Mathf.RoundToInt(L / 0.045f));
                for (int k = 0; k < n2; k++)
                {
                    float t = -L / 2f + L * (k + 0.5f) / n2;
                    mb.Box(At(t, 0, DeckAt(b, ground, t) - 0.008f), new Vector3(W, 0.016f, L / n2 * 0.88f), k % 2 == 0 ? plank : dk, pitch);
                }
                foreach (int sd in new[] { -1, 1 }) // a low rail on posts
                {
                    for (int e = 0; e < 3; e++)
                    {
                        float t = -L / 2f + L * 0.04f + (L * 0.92f) * e / 2f;
                        mb.Box(At(t, sd * (W / 2f - 0.008f), DeckAt(b, ground, t) + 0.02f), new Vector3(0.016f, 0.07f, 0.016f), Palette.Timber, yawQ);
                    }
                    mb.Box(At(0, sd * (W / 2f - 0.008f), y + 0.045f), new Vector3(0.012f, 0.012f, L * 0.96f), Palette.Timber, pitch);
                }
            }
        }
    }
}
