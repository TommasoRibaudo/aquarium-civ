using System;
using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The box's buildings and props, drawn with <see cref="MeshBuilder"/> and the <see cref="Palette"/> (ported from the
    /// style-kit spike's StyleBox and the Growth Lab's GrowthArt). Every function draws in the builder's local space: the
    /// caller sets <c>mb.M</c> to the lot's frame (origin = lot centre on the ground, local -z = the front). Windows and
    /// lanterns are lamps (<see cref="MeshBuilder.lamp"/>): they light at night through <see cref="NightLamps"/>.
    /// </summary>
    public static class Pieces
    {
        /// <summary>Lot-local ground height: maps a local point onto the terrain and back (for fences and crops on slopes).</summary>
        public delegate Vector3 Ground(Vector3 local);

        // ---------------- dispatcher ----------------
        /// <summary>Draws one <see cref="PieceData"/> into <paramref name="mb"/>, whose M must already be the lot's frame.</summary>
        public static void Draw(MeshBuilder mb, PieceData p, Ground ground)
        {
            float w = p.w, d = p.d;
            bool camp = p.era == Era.Camp, town = p.era == Era.Town;
            Vector3 Front(float depth) => new(0, 0, -d / 2f + 0.02f + depth / 2f);
            switch (p.kind)
            {
                case PieceKind.Home:
                    if (camp)
                    {
                        Teepee(mb, Vector3.zero, Mathf.Clamp(Mathf.Min(w, d) * 0.42f, 0.14f, 0.38f), Mathf.Clamp(Mathf.Min(w, d) * 1.1f, 0.42f, 0.66f),
                            p.seed % 2 == 0 ? Palette.Hide : Palette.Canvas, 0f, p.seed);
                        break;
                    }
                    {
                        int storeys = p.storeys > 0 ? p.storeys : town ? 2 : 1;
                        float hd = Mathf.Min(d * 0.9f, town ? 0.32f : 0.42f), hw = w * 0.92f;
                        float wallH = storeys > 1 ? 0.2f : town ? 0.27f : 0.3f;
                        float roofH = Mathf.Clamp(hd * 0.62f, 0.2f, 0.3f);
                        House(mb, Front(hd), hw, hd, wallH, roofH, Wall(p.seed), RoofColor(p.roof, p.seed, p.era), p.chimney, true, storeys, Phase(p.seed));
                    }
                    break;
                case PieceKind.Fire: Firepit(mb, Vector3.zero, true); break;
                case PieceKind.Well: Well(mb, Vector3.zero, !camp); break;
                case PieceKind.Woodpile: Woodpile(mb, Vector3.zero, 0f); break;
                case PieceKind.DryingRack: DryingRack(mb, Vector3.zero); break;
                case PieceKind.LeanTo: LeanTo(mb); break;
                case PieceKind.Bench: Bench(mb, Vector3.zero); break;
                case PieceKind.Lantern: Lantern(mb, Vector3.zero, Phase(p.seed)); break;
                case PieceKind.Granary: Granary(mb, Vector3.zero); break;
                case PieceKind.Crates: Crates(mb, Vector3.zero); break;
                case PieceKind.LogSeat: Log(mb, new Vector3(0, 0.035f, 0), 0.035f, Mathf.Max(0.2f, w), 90f, Palette.Bark); break;
                case PieceKind.Stall: Stall(mb, Vector3.zero, 0f, Palette.Tunics[Mathf.Abs(p.seed) % Palette.Tunics.Length]); break;
                case PieceKind.Windmill:
                    if (camp) Granary(mb, Vector3.zero);
                    else Windmill(mb, Vector3.zero, p.seed * 37f, town ? Palette.Stone : Palette.Plaster, town, Phase(p.seed));
                    break;
                case PieceKind.Lumbermill:
                    if (camp) { Woodpile(mb, new Vector3(0, 0, -d * 0.1f), 20f); Log(mb, new Vector3(w * 0.2f, 0.03f, d * 0.2f), 0.035f, 0.3f, 70f, Palette.Bark); }
                    else Lumbermill(mb, Vector3.zero, w, d);
                    break;
                case PieceKind.Market:
                    if (camp)
                    {
                        mb.Box(new Vector3(0, 0.005f, 0), new Vector3(0.3f, 0.01f, 0.2f), Palette.Hide);
                        Crates(mb, new Vector3(-0.05f, 0, 0));
                    }
                    else MarketHall(mb, Vector3.zero, w, d, town, Phase(p.seed));
                    break;
                case PieceKind.Tavern:
                    if (camp) { Firepit(mb, Vector3.zero, true); break; }
                    House(mb, Front(0.34f), w * 0.9f, 0.34f, town ? 0.2f : 0.28f, 0.26f, Palette.PlasterWarm, town ? Palette.Terracotta : Palette.Thatch, true, true, town ? 2 : 1, Phase(p.seed));
                    {
                        var sign = new Vector3(w * 0.5f, 0, -d / 2f + 0.02f);
                        mb.Box(sign + new Vector3(0, 0.16f, 0), new Vector3(0.02f, 0.32f, 0.02f), Palette.Timber);
                        mb.Box(sign + new Vector3(-0.05f, 0.27f, 0), new Vector3(0.08f, 0.06f, 0.01f), Palette.Ember);
                        mb.Box(new Vector3(-w * 0.25f, 0.04f, -d / 2f + 0.01f), new Vector3(0.18f, 0.016f, 0.05f), Palette.WoodLight);
                    }
                    break;
                case PieceKind.Church:
                    if (camp) StandingStones(mb, Vector3.zero);
                    else Church(mb, Vector3.zero, w, d, Phase(p.seed));
                    break;
                case PieceKind.StandingStones: StandingStones(mb, Vector3.zero); break;
                case PieceKind.Watermill: Watermill(mb, Vector3.zero, w, d, Phase(p.seed)); break;
                case PieceKind.Smithy: Smithy(mb, Vector3.zero, w, d, Phase(p.seed)); break;
                case PieceKind.Dyer: Dyer(mb, Vector3.zero, w, d); break;
            }
        }

        /// <summary>A lamp's phase in 0..1 from a seed: when in the dusk it comes on.</summary>
        public static float Phase(int seed) => Land.Hash3(seed, 7, 3);

        public static Color RoofColor(RoofStyle style, int seed, Era era)
        {
            switch (style)
            {
                case RoofStyle.Thatch: return Palette.Thatch;
                case RoofStyle.Terracotta: return Palette.Terracotta;
                case RoofStyle.Slate: return Palette.Slate;
            }
            float h = Land.Hash3(seed, 5, 9);
            if (era == Era.Village) return h < 0.62f ? Palette.Thatch : h < 0.85f ? Palette.Terracotta : Palette.Slate;
            return h < 0.25f ? Palette.Thatch : h < 0.7f ? Palette.Terracotta : Palette.Slate;
        }

        public static Color Wall(int seed) => Land.Hash3(seed, 6, 2) < 0.35f ? Palette.PlasterWarm : Palette.Plaster;

        // ---------------- houses ----------------
        /// <summary>The half-timbered house: stone footing, plaster walls, timber posts, a gable roof along x with an
        /// overhang, a door and windows on the front (-z), optional chimney and jettied upper storeys.</summary>
        public static void House(MeshBuilder mb, Vector3 at, float w, float d, float wallH, float roofH, Color wall, Color roof, bool chimney, bool timber = true, int storeys = 1, float lampPhase = 0.5f)
        {
            mb.Box(at + new Vector3(0, 0.05f, 0), new Vector3(w + 0.05f, 0.1f, d + 0.05f), Palette.Stone);
            float y0 = at.y + 0.1f;
            for (int s = 0; s < storeys; s++)
            {
                float jut = s * 0.025f;
                float sw = w + 2 * jut, sd = d + 2 * jut;
                var c = new Vector3(at.x, y0 + wallH / 2f, at.z);
                mb.Box(c, new Vector3(sw, wallH, sd), s == 0 ? wall : Color.Lerp(wall, Palette.PlasterWarm, 0.4f));
                if (timber)
                {
                    foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        mb.Box(new Vector3(at.x + x * (sw / 2f - 0.01f), y0 + wallH / 2f, at.z + z * (sd / 2f - 0.01f)), new Vector3(0.033f, wallH, 0.033f), Palette.Timber);
                    mb.Box(new Vector3(at.x, y0 + wallH - 0.015f, at.z - sd / 2f - 0.003f), new Vector3(sw, 0.03f, 0.012f), Palette.Timber);
                    if (s > 0) mb.Box(new Vector3(at.x, y0 + 0.012f, at.z - sd / 2f - 0.004f), new Vector3(sw, 0.024f, 0.014f), Palette.Timber);
                }
                float wy = y0 + wallH * 0.6f;
                mb.emission = 0.35f;
                mb.lamp = Mathf.Repeat(lampPhase + s * 0.17f, 1f);
                if (s == 0) mb.Box(new Vector3(at.x - sw * 0.2f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.075f, 0.07f, 0.012f), Palette.WindowLit);
                else
                {
                    mb.Box(new Vector3(at.x - sw * 0.22f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.065f, 0.06f, 0.012f), Palette.WindowLit);
                    mb.Box(new Vector3(at.x + sw * 0.22f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.065f, 0.06f, 0.012f), Palette.WindowLit);
                }
                mb.Box(new Vector3(at.x + sw / 2f + 0.006f, wy, at.z), new Vector3(0.012f, 0.065f, 0.07f), Palette.WindowLit);
                mb.Box(new Vector3(at.x - sw / 2f - 0.006f, wy, at.z), new Vector3(0.012f, 0.065f, 0.07f), Palette.WindowLit);
                mb.emission = 0f;
                mb.lamp = -1f;
                if (s == 0) mb.Box(new Vector3(at.x - sw * 0.2f, wy - 0.045f, at.z - sd / 2f - 0.012f), new Vector3(0.1f, 0.015f, 0.02f), Palette.Timber); // sill
                y0 += wallH;
            }
            float top = y0, ww = w + 2 * (storeys - 1) * 0.025f, dd = d + 2 * (storeys - 1) * 0.025f;
            float oh = 0.065f, half = dd / 2f + oh;
            float slope = Mathf.Sqrt(half * half + roofH * roofH), ang = Mathf.Atan2(roofH, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(at.x, top + roofH / 2f, at.z + s * half / 2f), new Vector3(ww + 2 * oh, 0.034f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                float x = at.x + s * ww / 2f;
                var a = new Vector3(x, top, at.z - dd / 2f); var b = new Vector3(x, top + roofH * (dd / 2f) / half, at.z); var c = new Vector3(x, top, at.z + dd / 2f);
                if (s > 0) mb.Tri(a, b, c, wall); else mb.Tri(c, b, a, wall);
            }
            mb.Box(new Vector3(at.x + w * 0.18f, at.y + 0.1f + 0.09f, at.z - d / 2f - 0.006f), new Vector3(0.1f, 0.18f, 0.012f), Palette.Door);
            if (chimney) mb.Box(new Vector3(at.x + ww * 0.28f, top + roofH * 0.75f, at.z + dd * 0.12f), new Vector3(0.07f, roofH * 0.9f, 0.07f), Palette.Stone);
        }

        // ---------------- camp ----------------
        /// <summary>A teepee with its door flap facing local -z rotated by <paramref name="doorYaw"/>, and poles out of the top.</summary>
        public static void Teepee(MeshBuilder mb, Vector3 p, float r, float h, Color cover, float doorYaw, int seed = 0)
        {
            p -= Vector3.up * 0.02f;
            mb.Prism(p, r, 0.03f, h, 7, cover, Quaternion.Euler(0, Land.Hash(seed, 3) * 50f, 0), true, false, Palette.Bark);
            // A painted band round the cover, so a white tent isn't plain (spike polish note).
            float bandY = h * 0.32f, bandR = Mathf.Lerp(r, 0.03f, bandY / h) + 0.004f;
            var band = Palette.Accents[Mathf.Abs(seed) % Palette.Accents.Length];
            mb.Prism(p + Vector3.up * bandY, bandR, Mathf.Lerp(r, 0.03f, (bandY + 0.035f) / h) + 0.004f, 0.035f, 7, Color.Lerp(band, cover, 0.25f),
                Quaternion.Euler(0, Land.Hash(seed, 3) * 50f, 0), false);
            var fwd = Quaternion.Euler(0, doorYaw, 0) * Vector3.back;
            var right = Vector3.Cross(Vector3.up, fwd);
            var basePt = p + fwd * (r * 0.93f);
            mb.Tri(basePt - right * r * 0.5f + Vector3.up * 0.01f, basePt + Vector3.up * h * 0.38f - fwd * r * 0.3f, basePt + right * r * 0.5f + Vector3.up * 0.01f, Palette.Door);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var tip = p + Vector3.up * (h + 0.09f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.05f;
                var mid = p + Vector3.up * h * 0.85f;
                var dir = tip - mid;
                mb.Prism(mid, 0.008f, 0.006f, dir.magnitude, 4, Palette.Bark, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            }
        }

        /// <summary>
        /// The fire pit: a stone ring, crossed logs, an ember bed and flames. Flames are 1.5× the spike's (props 1.5–2×,
        /// life-in-the-box §5) with a bright core, so the fire reads and blooms at full-box zoom; the view adds a glow
        /// billboard (<see cref="BoxView"/>) and a warm pool on the ground. <paramref name="seats"/> adds three log seats.
        /// </summary>
        public static void Firepit(MeshBuilder mb, Vector3 p, bool seats)
        {
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                mb.Ico(p + new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.14f, 0.042f, new Vector3(1, 0.7f, 1), i % 2 == 0 ? Palette.Rock : Palette.RockDark, 0.25f);
            }
            for (int i = 0; i < 3; i++) Log(mb, p + Vector3.up * 0.025f, 0.02f, 0.22f, i * 60f, Palette.Bark);
            mb.emission = 0.6f;
            mb.Prism(p, 0.085f, 0.075f, 0.016f, 7, Palette.Ember);
            mb.emission = 0.75f; mb.sway = 1.4f; // below full glow: the orange stays orange after tonemapping
            // A main flame, three tongues leaning out and a short bright core, so it reads as fire, not a cone.
            mb.Prism(p + Vector3.up * 0.012f, 0.085f, 0f, 0.27f, 5, Palette.Fire, Quaternion.Euler(0, 20f, 0));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f + 35f;
                var lean = Quaternion.Euler(0, a, 0) * Quaternion.Euler(18f, 0, 0);
                mb.Prism(p + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.012f, 0.035f), 0.045f, 0f, 0.15f + 0.03f * i, 4, i == 1 ? Palette.Ember : Palette.Fire, lean);
            }
            mb.emission = 1f;
            mb.Prism(p + new Vector3(0.008f, 0.012f, -0.012f), 0.038f, 0f, 0.12f, 5, Palette.FireCore, Quaternion.Euler(0, 50f, 0));
            mb.emission = 0f; mb.sway = 0f;
            if (!seats) return;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.4f;
                var q = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.4f;
                Log(mb, q + Vector3.up * 0.035f, 0.035f, 0.3f, -a * Mathf.Rad2Deg + 90f, Palette.Bark);
            }
        }

        public static void Log(MeshBuilder mb, Vector3 centre, float r, float len, float yaw, Color col)
        {
            var rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90, 0, 0);
            mb.Prism(centre - rot * Vector3.up * (len / 2f), r, r, len, 6, col, rot, true, true, Palette.WoodLight);
        }

        public static void Woodpile(MeshBuilder mb, Vector3 p, float yaw)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    var c = p + r * new Vector3((i - (3 - row) / 2f) * 0.055f, 0.028f + row * 0.048f, 0);
                    Log(mb, c, 0.026f, 0.26f, yaw, i % 2 == 0 ? Palette.Bark : Palette.WoodLight);
                }
        }

        public static void DryingRack(MeshBuilder mb, Vector3 p)
        {
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.15f, 0.14f, 0), new Vector3(0.02f, 0.28f, 0.02f), Palette.Bark);
            mb.Box(p + new Vector3(0, 0.27f, 0), new Vector3(0.34f, 0.018f, 0.018f), Palette.Bark);
            mb.sway = 0.25f;
            mb.Box(p + new Vector3(-0.06f, 0.19f, 0), new Vector3(0.1f, 0.15f, 0.008f), Palette.Hide);
            mb.Box(p + new Vector3(0.07f, 0.2f, 0), new Vector3(0.08f, 0.12f, 0.008f), Palette.Canvas);
            mb.sway = 0f;
        }

        /// <summary>A hide lean-to, open side facing local -z.</summary>
        public static void LeanTo(MeshBuilder mb)
        {
            mb.Box(new Vector3(0, 0.2f, 0.05f), new Vector3(0.55f, 0.02f, 0.46f), Palette.Hide, Quaternion.Euler(-48f, 0, 0), true);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.25f, 0.17f, -0.11f), new Vector3(0.025f, 0.34f, 0.025f), Palette.Bark);
            mb.Box(new Vector3(0, 0.34f, -0.11f), new Vector3(0.6f, 0.025f, 0.025f), Palette.Bark);
            mb.Box(new Vector3(0, 0.012f, 0.08f), new Vector3(0.4f, 0.02f, 0.26f), Palette.Canvas);
        }

        public static void Bush(MeshBuilder mb, Vector3 g, float r, bool berries)
        {
            mb.sway = 0.3f;
            mb.Ico(g + Vector3.up * r * 0.5f, r, new Vector3(1.2f, 0.8f, 1.1f), Palette.LeafB, 0.25f);
            mb.sway = 0f;
            if (berries)
                for (int k = 0; k < 4; k++)
                    mb.Ico(g + new Vector3(mb.Range(-r, r) * 0.7f, r * mb.Range(0.6f, 1.1f), mb.Range(-r, r) * 0.7f), 0.016f, Vector3.one, Palette.Ember, 0.1f);
        }

        // ---------------- village ----------------
        public static void Well(MeshBuilder mb, Vector3 p, bool roofed)
        {
            mb.Prism(p, 0.13f, 0.13f, 0.12f, 9, Palette.Stone, topCol: Palette.RockDark);
            mb.Prism(p + Vector3.up * 0.121f, 0.09f, 0.09f, 0.001f, 9, Palette.WaterDeep);
            if (!roofed) return;
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.11f, 0.2f, 0), new Vector3(0.025f, 0.28f, 0.025f), Palette.Timber);
            mb.Box(p + new Vector3(0, 0.32f, 0), new Vector3(0.26f, 0.02f, 0.02f), Palette.Timber);
            mb.Prism(p + new Vector3(0.0f, 0.2f, 0), 0.02f, 0.02f, 0.05f, 6, Palette.WoodLight); // bucket
            foreach (int s in new[] { -1, 1 })
                mb.Box(p + new Vector3(0, 0.37f, s * 0.07f), new Vector3(0.3f, 0.02f, 0.17f), Palette.Thatch, Quaternion.Euler(s * 35f, 0, 0), true);
        }

        public static void Bench(MeshBuilder mb, Vector3 p)
        {
            mb.Box(p + new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.018f, 0.06f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.09f, 0.022f, 0), new Vector3(0.02f, 0.045f, 0.05f), Palette.Timber);
        }

        /// <summary>A lantern on a post: dark iron cage round a lamp that lights at night (flickering on, NightLamps).</summary>
        public static void Lantern(MeshBuilder mb, Vector3 p, float lampPhase)
        {
            mb.Box(p + new Vector3(0, 0.16f, 0), new Vector3(0.025f, 0.32f, 0.025f), Palette.Timber);
            mb.Box(p + new Vector3(0.035f, 0.31f, 0), new Vector3(0.08f, 0.015f, 0.015f), Palette.Timber);
            var lp = p + new Vector3(0.065f, 0.255f, 0);
            mb.Box(lp + new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.012f, 0.05f), Palette.Iron);
            mb.emission = 0.15f;
            mb.lamp = lampPhase;
            mb.Box(lp + new Vector3(0, 0.02f, 0), new Vector3(0.036f, 0.048f, 0.036f), Palette.Lamp);
            mb.lamp = -1f;
            mb.emission = 0f;
            mb.Box(lp + new Vector3(0, -0.008f, 0), new Vector3(0.044f, 0.01f, 0.044f), Palette.Iron);
        }

        /// <summary>A fence along a polyline in lot-local (or world) space, posts every ~0.2 u.</summary>
        public static void Fence(MeshBuilder mb, Ground ground, Vector3[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.2f));
                for (int k = 0; k <= n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)n);
                    var gp = ground(p);
                    mb.Box(gp + Vector3.up * 0.07f, new Vector3(0.025f, 0.15f, 0.025f), Palette.Timber, Quaternion.Euler(0, mb.Range(-8f, 8f), mb.Range(-4f, 4f)));
                    if (k == n) continue;
                    var gq = ground(Vector3.Lerp(a, b, (k + 1) / (float)n));
                    var dir = gq - gp;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    foreach (float y in new[] { 0.06f, 0.11f })
                        mb.Box((gp + gq) / 2f + Vector3.up * y, new Vector3(0.014f, 0.016f, dir.magnitude), Palette.WoodLight, rot);
                }
            }
        }

        /// <summary>One crop plant at a ground point (wheat ear or green shoot), swaying in the wind.</summary>
        public static void Crop(MeshBuilder mb, Vector3 p, Color col)
        {
            mb.sway = 0.7f;
            mb.Prism(p, 0.022f, 0f, mb.Range(0.09f, 0.13f), 4, col, Quaternion.Euler(0, mb.Range(0, 90f), 0));
            mb.sway = 0f;
        }

        public static void Granary(MeshBuilder mb, Vector3 at)
        {
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(at + new Vector3(x * 0.09f, 0.06f, z * 0.09f), new Vector3(0.03f, 0.12f, 0.03f), Palette.Bark);
            mb.Box(at + new Vector3(0, 0.17f, 0), new Vector3(0.24f, 0.1f, 0.24f), Palette.WoodLight);
            mb.Prism(at + new Vector3(0, 0.22f, 0), 0.2f, 0f, 0.16f, 6, Palette.Thatch);
        }

        public static void Crates(MeshBuilder mb, Vector3 p)
        {
            mb.Box(p + new Vector3(0, 0.035f, 0), new Vector3(0.07f, 0.07f, 0.07f), Palette.WoodLight, Quaternion.Euler(0, 15, 0));
            mb.Box(p + new Vector3(0.08f, 0.03f, 0.02f), new Vector3(0.06f, 0.06f, 0.06f), Palette.Hide, Quaternion.Euler(0, -10, 0));
            mb.Prism(p + new Vector3(0.03f, 0.0f, 0.08f), 0.035f, 0.03f, 0.06f, 7, Palette.Bark, topCol: Palette.Wheat);
        }

        public static void Stall(MeshBuilder mb, Vector3 p, float yaw, Color canopy)
        {
            var keep = mb.M;
            mb.M = keep * Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
            mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.2f, 0.03f, 0.11f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(new Vector3(s * 0.09f, 0.1f, 0.04f), new Vector3(0.016f, 0.2f, 0.016f), Palette.Timber);
            mb.Box(new Vector3(0, 0.2f, 0.0f), new Vector3(0.24f, 0.012f, 0.16f), canopy, Quaternion.Euler(-14, 0, 0), true);
            mb.Ico(new Vector3(-0.04f, 0.1f, -0.01f), 0.022f, Vector3.one, Palette.Flowers[1], 0.1f);
            mb.Ico(new Vector3(0.04f, 0.1f, 0.0f), 0.022f, Vector3.one, Palette.Ember, 0.1f);
            mb.M = keep;
        }

        /// <summary>An open-sided shed: posts and a pitched roof (lumber mill, market hall, dyer's shed).</summary>
        public static void Shed(MeshBuilder mb, Vector3 at, float w, float d, float h, Color roof, Color post)
        {
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (0, 1) })
                mb.Box(at + new Vector3(x * (w / 2f - 0.02f), h / 2f, z * (d / 2f - 0.02f)), new Vector3(0.03f, h, 0.03f), post);
            float half = d / 2f + 0.05f, rh = 0.12f;
            float slope = Mathf.Sqrt(half * half + rh * rh), ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(at + new Vector3(0, h + rh / 2f, s * half / 2f), new Vector3(w + 0.1f, 0.03f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
        }

        public static void Windmill(MeshBuilder mb, Vector3 at, float yaw, Color body, bool stone, float lampPhase)
        {
            float h = 0.56f;
            mb.Prism(at, 0.13f, 0.085f, h, 8, body, topCol: body);
            mb.Prism(at + Vector3.up * h, 0.105f, 0.0f, 0.14f, 8, stone ? Palette.Slate : Palette.Thatch);
            mb.Box(at + new Vector3(0, 0.08f, -0.125f), new Vector3(0.07f, 0.13f, 0.02f), Palette.Door);
            mb.emission = 0.3f; mb.lamp = lampPhase;
            mb.Box(at + new Vector3(0, 0.34f, -0.1f), new Vector3(0.045f, 0.05f, 0.02f), Palette.WindowLit);
            mb.emission = 0f; mb.lamp = -1f;
            var hub = at + new Vector3(0, h - 0.02f, -0.13f);
            mb.Box(hub, new Vector3(0.04f, 0.04f, 0.05f), Palette.Timber);
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0, 0, 45f + k * 90f + yaw * 0.13f);
                mb.Box(hub + r * new Vector3(0, 0.2f, 0) - new Vector3(0, 0, 0.02f), new Vector3(0.016f, 0.4f, 0.01f), Palette.Timber, r);
                mb.Box(hub + r * new Vector3(0.035f, 0.23f, 0) - new Vector3(0, 0, 0.024f), new Vector3(0.06f, 0.3f, 0.005f), Palette.Canvas, r);
            }
        }

        public static void MarketHall(MeshBuilder mb, Vector3 at, float w, float d, bool town, float lampPhase)
        {
            if (town)
            {
                mb.Box(at + new Vector3(0, 0.02f, 0), new Vector3(w, 0.04f, d), Palette.Stone);
                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (0, 1) })
                    mb.Box(at + new Vector3(x * (w / 2f - 0.03f), 0.12f, z * (d / 2f - 0.03f)), new Vector3(0.05f, 0.2f, 0.05f), Palette.Stone);
                House(mb, at + new Vector3(0, 0.12f, 0), w - 0.02f, d - 0.02f, 0.18f, 0.2f, Palette.PlasterWarm, Palette.Terracotta, false, true, 1, lampPhase);
            }
            else Shed(mb, at, w - 0.06f, d - 0.06f, 0.22f, Palette.Terracotta, Palette.Timber);
            Crates(mb, at + new Vector3(-w * 0.2f, 0, -d * 0.1f));
        }

        public static void Church(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            float nw = w * 0.8f, nd = d * 0.72f;
            var nave = at + new Vector3(0, 0, d * 0.12f);
            mb.Box(nave + new Vector3(0, 0.03f, 0), new Vector3(nw + 0.04f, 0.06f, nd + 0.04f), Palette.RockDark);
            mb.Box(nave + new Vector3(0, 0.06f + 0.16f, 0), new Vector3(nw, 0.32f, nd), Palette.Stone);
            float rh = 0.2f, half = nw / 2f + 0.05f;
            float slope = Mathf.Sqrt(half * half + rh * rh), ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(nave + new Vector3(s * half / 2f, 0.38f + rh / 2f, 0), new Vector3(slope, 0.032f, nd + 0.08f), Palette.Slate, Quaternion.Euler(0, 0, -s * ang), true);
            foreach (int s in new[] { -1, 1 })
            {
                float z = nave.z + s * nd / 2f;
                var a = new Vector3(nave.x - nw / 2f, at.y + 0.38f, z); var b = new Vector3(nave.x, at.y + 0.38f + rh * (nw / 2f) / half, z); var c = new Vector3(nave.x + nw / 2f, at.y + 0.38f, z);
                if (s < 0) mb.Tri(a, b, c, Palette.Stone); else mb.Tri(c, b, a, Palette.Stone);
            }
            mb.emission = 0.3f; mb.lamp = lampPhase;
            for (int k = -1; k <= 1; k++)
                foreach (int s in new[] { -1, 1 })
                    mb.Box(nave + new Vector3(s * (nw / 2f + 0.006f), 0.26f, k * nd * 0.28f), new Vector3(0.012f, 0.1f, 0.045f), Palette.WindowLit);
            mb.emission = 0f; mb.lamp = -1f;
            var tower = at + new Vector3(0, 0, -d / 2f + 0.12f);
            mb.Box(tower + new Vector3(0, 0.33f, 0), new Vector3(0.2f, 0.66f, 0.2f), Palette.Stone);
            mb.Prism(tower + new Vector3(0, 0.66f, 0), 0.15f, 0f, 0.3f, 4, Palette.Slate, Quaternion.Euler(0, 45, 0));
            mb.Box(tower + new Vector3(0, 0.1f, -0.106f), new Vector3(0.08f, 0.16f, 0.012f), Palette.Door);
            mb.Box(tower + new Vector3(0, 0.5f, -0.106f), new Vector3(0.05f, 0.08f, 0.012f), Palette.RockDark);
        }

        public static void StandingStones(MeshBuilder mb, Vector3 at)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                mb.Box(at + new Vector3(Mathf.Cos(a) * 0.17f, 0.08f, Mathf.Sin(a) * 0.17f), new Vector3(0.05f, mb.Range(0.13f, 0.2f), 0.035f), i % 2 == 0 ? Palette.Rock : Palette.RockLight, Quaternion.Euler(mb.Range(-6, 6), -a * Mathf.Rad2Deg, 0));
            }
        }

        public static void Watermill(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            House(mb, at + new Vector3(0, 0, -0.03f), w * 0.78f, d * 0.6f, 0.28f, 0.2f, Palette.Stone, Palette.Slate, true, false, 1, lampPhase);
            var axle = at + new Vector3(w * 0.12f, 0.12f, d / 2f + 0.03f);
            var rot = Quaternion.Euler(0, 0, 90);
            mb.Prism(axle - new Vector3(0.04f, 0, 0), 0.17f, 0.17f, 0.08f, 10, Palette.Timber, rot, true, true, Palette.WoodLight);
            for (int k = 0; k < 8; k++)
            {
                var r = Quaternion.Euler(k * 45f, 0, 0);
                mb.Box(axle + r * new Vector3(0, 0.17f, 0), new Vector3(0.1f, 0.012f, 0.05f), Palette.WoodLight, r);
            }
        }

        public static void Smithy(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            House(mb, at + new Vector3(0, 0, 0.04f), w * 0.8f, d * 0.55f, 0.24f, 0.18f, Palette.Stone, Palette.Slate, false, false, 1, lampPhase);
            var ch = at + new Vector3(w * 0.28f, 0, 0.1f);
            mb.Box(ch + new Vector3(0, 0.3f, 0), new Vector3(0.09f, 0.6f, 0.09f), Palette.RockDark);
            mb.emission = 1f;
            mb.Box(ch + new Vector3(0, 0.08f, -0.05f), new Vector3(0.06f, 0.05f, 0.012f), Palette.Fire);
            mb.emission = 0f;
            var f = at + new Vector3(-w * 0.15f, 0, -d * 0.36f);
            mb.Box(f + new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.06f, 0.05f), Palette.Bark);
            mb.Box(f + new Vector3(0, 0.07f, 0), new Vector3(0.09f, 0.025f, 0.04f), Palette.RockDark);
            mb.Prism(f + new Vector3(0.12f, 0, 0.0f), 0.035f, 0.032f, 0.07f, 8, Palette.Bark, topCol: Palette.WaterDeep);
        }

        public static void Dyer(MeshBuilder mb, Vector3 at, float w, float d)
        {
            Shed(mb, at + new Vector3(0, 0, d * 0.18f), w * 0.7f, d * 0.45f, 0.2f, Palette.Thatch, Palette.Timber);
            Color[] dyes = { Palette.Tunics[0], Palette.Tunics[1], Palette.Tunics[2] };
            for (int k = 0; k < 3; k++)
                mb.Prism(at + new Vector3(-w * 0.28f + k * 0.11f, 0, -d * 0.22f), 0.045f, 0.045f, 0.06f, 8, Palette.Bark, topCol: dyes[k]);
            var p = at + new Vector3(w * 0.18f, 0, -d * 0.18f);
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.12f, 0.11f, 0), new Vector3(0.016f, 0.22f, 0.016f), Palette.Timber);
            mb.sway = 0.35f;
            for (int k = 0; k < 3; k++)
                mb.Box(p + new Vector3(-0.07f + k * 0.07f, 0.15f, 0), new Vector3(0.055f, 0.12f, 0.006f), dyes[(k + 1) % 3]);
            mb.sway = 0f;
        }

        public static void Lumbermill(MeshBuilder mb, Vector3 at, float w, float d)
        {
            Shed(mb, at + new Vector3(0, 0, d * 0.12f), w * 0.62f, d * 0.5f, 0.2f, Palette.WoodLight, Palette.Timber);
            Woodpile(mb, at + new Vector3(-w * 0.2f, 0, -d * 0.25f), 90f);
            Log(mb, at + new Vector3(w * 0.18f, 0.03f, -d * 0.25f), 0.03f, 0.3f, 10f, Palette.Bark);
            mb.Prism(at + new Vector3(w * 0.3f, 0, d * 0.32f), 0.045f, 0.045f, 0.05f, 7, Palette.Bark, topCol: Palette.WoodLight);
        }

        // ---------------- nature ----------------
        public static void Tree(MeshBuilder mb, Vector3 g, float s, bool pine)
        {
            g -= Vector3.up * 0.02f;
            mb.Prism(g, 0.045f * s, 0.032f * s, 0.3f * s, 6, Palette.Bark);
            mb.sway = 1f;
            if (pine)
            {
                for (int i = 0; i < 3; i++)
                    mb.Prism(g + Vector3.up * (0.2f + i * 0.17f) * s, (0.24f - i * 0.06f) * s, 0f, 0.3f * s, 7, Palette.Pine, Quaternion.Euler(0, i * 20f, 0), false, true);
            }
            else
            {
                var col = mb.Range(0, 3) switch { 0 => Palette.LeafA, 1 => Palette.LeafB, _ => Palette.LeafC };
                mb.Ico(g + Vector3.up * 0.42f * s, 0.2f * s, new Vector3(1.1f, 0.95f, 1.1f), col, 0.18f, Palette.LeafB);
                mb.Ico(g + new Vector3(0.1f, 0.55f, 0.04f) * s, 0.14f * s, Vector3.one, col, 0.18f);
                mb.Ico(g + new Vector3(-0.09f, 0.5f, -0.06f) * s, 0.13f * s, Vector3.one, Color.Lerp(col, Palette.LeafC, 0.4f), 0.18f);
            }
            mb.sway = 0f;
        }

        public static void Rock(MeshBuilder mb, Vector3 g, float r, bool light)
        {
            mb.Ico(g + Vector3.up * r * 0.2f, r, new Vector3(1.2f, 0.6f, 1f), light ? Palette.RockLight : Palette.Rock, 0.22f, Palette.RockDark);
        }

        public static void GrassTuft(MeshBuilder mb, Vector3 g, float h, Color col)
        {
            mb.sway = 1f;
            for (int b = 0; b < 3; b++)
            {
                float a = b * 2.1f + mb.Range(0f, 1f);
                var lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * h * 0.35f;
                var side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.012f;
                mb.Tri(g - side, g + lean + Vector3.up * h, g + side, col);
                mb.Tri(g + side, g + lean + Vector3.up * h, g - side, col);
            }
            mb.sway = 0f;
        }

        public static void Reed(MeshBuilder mb, Vector3 g, Color col)
        {
            mb.sway = 0.8f;
            mb.Prism(g, 0.012f, 0f, mb.Range(0.14f, 0.24f), 3, col, Quaternion.Euler(mb.Range(-8f, 8f), 0, mb.Range(-8f, 8f)));
            mb.sway = 0f;
        }
    }
}
