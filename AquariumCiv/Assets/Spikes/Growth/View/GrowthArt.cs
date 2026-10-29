using System;
using AquariumCiv.Spikes.StyleKit;
using UnityEngine;

namespace AquariumCiv.Spikes.Growth.View
{
    /// <summary>
    /// The buildings of all three eras, drawn with the style kit (MeshBuilder + Palette) in a lot's local space: the
    /// caller sets <c>mb.M</c> to the lot's frame (origin = lot centre on the ground, local -z = the front, facing its
    /// path). Camp → Village → Town rebuild on the same lot; the lot never moves (research 08 §3.7).
    /// </summary>
    public static class GrowthArt
    {
        // ---------------- houses ----------------
        /// <summary>The style kit's half-timbered house (StyleBox.House), in local space, with an optional upper storey.</summary>
        public static void House(MeshBuilder mb, Vector3 at, float w, float d, float wallH, float roofH, Color wall, Color roof, bool chimney, bool timber = true, int storeys = 1)
        {
            mb.Box(at + new Vector3(0, 0.05f, 0), new Vector3(w + 0.05f, 0.1f, d + 0.05f), Palette.Stone);
            float y0 = at.y + 0.1f;
            for (int s = 0; s < storeys; s++)
            {
                float jut = s * 0.025f; // jettied upper floor
                float sw = w + 2 * jut, sd = d + 2 * jut;
                var c = new Vector3(at.x, y0 + wallH / 2f, at.z);
                mb.Box(c, new Vector3(sw, wallH, sd), s == 0 ? wall : Color.Lerp(wall, Palette.PlasterWarm, 0.4f));
                if (timber)
                {
                    foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                        mb.Box(new Vector3(at.x + x * (sw / 2f - 0.01f), y0 + wallH / 2f, at.z + z * (sd / 2f - 0.01f)), new Vector3(0.03f, wallH, 0.03f), Palette.Timber);
                    mb.Box(new Vector3(at.x, y0 + wallH - 0.015f, at.z - sd / 2f - 0.003f), new Vector3(sw, 0.03f, 0.012f), Palette.Timber);
                    if (s > 0) mb.Box(new Vector3(at.x, y0 + 0.012f, at.z - sd / 2f - 0.004f), new Vector3(sw, 0.024f, 0.014f), Palette.Timber);
                }
                // windows on the front and the sides
                mb.emission = 0.35f;
                float wy = y0 + wallH * 0.6f;
                if (s == 0) mb.Box(new Vector3(at.x - sw * 0.2f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.07f, 0.065f, 0.012f), Palette.WindowLit);
                else
                {
                    mb.Box(new Vector3(at.x - sw * 0.22f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.065f, 0.06f, 0.012f), Palette.WindowLit);
                    mb.Box(new Vector3(at.x + sw * 0.22f, wy, at.z - sd / 2f - 0.006f), new Vector3(0.065f, 0.06f, 0.012f), Palette.WindowLit);
                }
                mb.Box(new Vector3(at.x + sw / 2f + 0.006f, wy, at.z), new Vector3(0.012f, 0.06f, 0.065f), Palette.WindowLit);
                mb.Box(new Vector3(at.x - sw / 2f - 0.006f, wy, at.z), new Vector3(0.012f, 0.06f, 0.065f), Palette.WindowLit);
                mb.emission = 0f;
                y0 += wallH;
            }
            float top = y0, ww = w + 2 * (storeys - 1) * 0.025f, dd = d + 2 * (storeys - 1) * 0.025f;
            float oh = 0.06f, half = dd / 2f + oh;
            float slope = Mathf.Sqrt(half * half + roofH * roofH), ang = Mathf.Atan2(roofH, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(at.x, top + roofH / 2f, at.z + s * half / 2f), new Vector3(ww + 2 * oh, 0.032f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                float x = at.x + s * ww / 2f;
                var a = new Vector3(x, top, at.z - dd / 2f); var b = new Vector3(x, top + roofH * (dd / 2f) / half, at.z); var c = new Vector3(x, top, at.z + dd / 2f);
                if (s > 0) mb.Tri(a, b, c, wall); else mb.Tri(c, b, a, wall);
            }
            mb.Box(new Vector3(at.x + w * 0.18f, at.y + 0.1f + 0.085f, at.z - d / 2f - 0.006f), new Vector3(0.09f, 0.17f, 0.012f), Palette.Door);
            if (chimney) mb.Box(new Vector3(at.x + ww * 0.28f, top + roofH * 0.75f, at.z + dd * 0.12f), new Vector3(0.065f, roofH * 0.9f, 0.065f), Palette.Stone);
        }

        public static Color Roof(int id, Era era)
        {
            float h = GrowthTerrain.Hash3(id, 5, 9);
            if (era == Era.Village) return h < 0.62f ? Palette.Thatch : h < 0.85f ? Palette.Terracotta : Palette.Slate;
            return h < 0.25f ? Palette.Thatch : h < 0.7f ? Palette.Terracotta : Palette.Slate;
        }

        public static Color Wall(int id) => GrowthTerrain.Hash3(id, 6, 2) < 0.35f ? Palette.PlasterWarm : Palette.Plaster;

        // ---------------- camp ----------------
        public static void Teepee(MeshBuilder mb, Vector3 p, float r, float h, Color cover, float doorYaw)
        {
            p -= Vector3.up * 0.02f;
            mb.Prism(p, r, 0.03f, h, 7, cover, Quaternion.Euler(0, doorYaw * 0.37f, 0), true, false, Palette.Bark);
            var fwd = Quaternion.Euler(0, doorYaw, 0) * Vector3.back;
            var right = Vector3.Cross(Vector3.up, fwd);
            var basePt = p + fwd * (r * 0.93f);
            mb.Tri(basePt - right * 0.08f + Vector3.up * 0.01f, basePt + Vector3.up * 0.2f - fwd * 0.06f, basePt + right * 0.08f + Vector3.up * 0.01f, Palette.Door);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var tip = p + Vector3.up * (h + 0.08f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.045f;
                var mid = p + Vector3.up * h * 0.85f;
                var dir = tip - mid;
                mb.Prism(mid, 0.008f, 0.006f, dir.magnitude, 4, Palette.Bark, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            }
        }

        public static void Firepit(MeshBuilder mb, Vector3 p)
        {
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                mb.Ico(p + new Vector3(Mathf.Cos(a), 0.01f, Mathf.Sin(a)) * 0.13f, 0.04f, new Vector3(1, 0.7f, 1), i % 2 == 0 ? Palette.Rock : Palette.RockDark, 0.25f);
            }
            for (int i = 0; i < 3; i++) Log(mb, p + Vector3.up * 0.025f, 0.018f, 0.2f, i * 60f, Palette.Bark);
            mb.Prism(p, 0.06f, 0.06f, 0.012f, 7, Palette.Ember);
            mb.emission = 1f; mb.sway = 1f;
            mb.Prism(p + Vector3.up * 0.01f, 0.07f, 0f, 0.2f, 5, Palette.Fire, Quaternion.Euler(0, 20f, 0));
            mb.Prism(p + new Vector3(0.015f, 0.01f, 0.01f), 0.04f, 0f, 0.14f, 5, Palette.FireCore, Quaternion.Euler(0, 50f, 0));
            mb.emission = 0f; mb.sway = 0f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.4f;
                var q = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.34f;
                Log(mb, q + Vector3.up * 0.035f, 0.035f, 0.28f, -a * Mathf.Rad2Deg + 90f, Palette.Bark);
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
                    var c = p + r * new Vector3((i - (3 - row) / 2f) * 0.05f, 0.026f + row * 0.044f, 0);
                    Log(mb, c, 0.024f, 0.24f, yaw, i % 2 == 0 ? Palette.Bark : Palette.WoodLight);
                }
        }

        public static void DryingRack(MeshBuilder mb, Vector3 p)
        {
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.13f, 0.12f, 0), new Vector3(0.02f, 0.24f, 0.02f), Palette.Bark);
            mb.Box(p + new Vector3(0, 0.235f, 0), new Vector3(0.3f, 0.016f, 0.016f), Palette.Bark);
            mb.sway = 0.25f;
            mb.Box(p + new Vector3(-0.05f, 0.17f, 0), new Vector3(0.09f, 0.12f, 0.008f), Palette.Hide);
            mb.Box(p + new Vector3(0.06f, 0.18f, 0), new Vector3(0.07f, 0.1f, 0.008f), Palette.Canvas);
            mb.sway = 0f;
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

        // ---------------- fields ----------------
        /// <summary>Crop rows along the strip (local z), a fence on three sides; the gate side (front) stays open.</summary>
        public static void Field(MeshBuilder mb, Func<Vector3, Vector3> ground, float w, float d, Color crop, bool fence)
        {
            if (crop != Palette.FieldSoil)
                for (float x = -w / 2f + 0.06f; x < w / 2f - 0.04f; x += 0.08f)
                    for (float z = -d / 2f + 0.06f; z < d / 2f - 0.04f; z += 0.075f)
                    {
                        var p = ground(new Vector3(x + mb.Range(-0.012f, 0.012f), 0, z + mb.Range(-0.012f, 0.012f)));
                        mb.sway = 0.7f;
                        mb.Prism(p, 0.02f, 0f, mb.Range(0.08f, 0.12f), 4, crop, Quaternion.Euler(0, mb.Range(0, 90f), 0));
                        mb.sway = 0f;
                    }
            if (!fence) return;
            var pts = new[] { new Vector3(-w / 2f + 0.12f, 0, -d / 2f), new Vector3(-w / 2f, 0, -d / 2f), new Vector3(-w / 2f, 0, d / 2f), new Vector3(w / 2f, 0, d / 2f), new Vector3(w / 2f, 0, -d / 2f), new Vector3(w / 2f - 0.12f, 0, -d / 2f) };
            Fence(mb, ground, pts);
        }

        public static void Fence(MeshBuilder mb, Func<Vector3, Vector3> ground, Vector3[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.18f));
                for (int k = 0; k <= n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)n);
                    var gp = ground(p);
                    mb.Box(gp + Vector3.up * 0.06f, new Vector3(0.022f, 0.13f, 0.022f), Palette.Timber, Quaternion.Euler(0, mb.Range(-8f, 8f), mb.Range(-4f, 4f)));
                    if (k == n) continue;
                    var gq = ground(Vector3.Lerp(a, b, (k + 1) / (float)n));
                    var dir = gq - gp;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    foreach (float y in new[] { 0.05f, 0.095f })
                        mb.Box((gp + gq) / 2f + Vector3.up * y, new Vector3(0.012f, 0.014f, dir.magnitude), Palette.WoodLight, rot);
                }
            }
        }

        // ---------------- village and town buildings ----------------
        public static void Well(MeshBuilder mb, Vector3 p, bool roofed)
        {
            mb.Prism(p, 0.12f, 0.12f, 0.11f, 9, Palette.Stone, topCol: Palette.RockDark);
            mb.Prism(p + Vector3.up * 0.111f, 0.085f, 0.085f, 0.001f, 9, Palette.WaterDeep);
            if (!roofed) return;
            foreach (int s in new[] { -1, 1 }) mb.Box(p + new Vector3(s * 0.1f, 0.19f, 0), new Vector3(0.024f, 0.26f, 0.024f), Palette.Timber);
            mb.Box(p + new Vector3(0, 0.3f, 0), new Vector3(0.24f, 0.02f, 0.02f), Palette.Timber);
            foreach (int s in new[] { -1, 1 })
                mb.Box(p + new Vector3(0, 0.35f, s * 0.065f), new Vector3(0.28f, 0.02f, 0.16f), Palette.Thatch, Quaternion.Euler(s * 35f, 0, 0), true);
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

        public static void Windmill(MeshBuilder mb, Vector3 at, float yaw, Color body, bool stone)
        {
            float h = 0.56f;
            mb.Prism(at, 0.13f, 0.085f, h, 8, body, topCol: body);
            mb.Prism(at + Vector3.up * h, 0.105f, 0.0f, 0.14f, 8, stone ? Palette.Slate : Palette.Thatch);
            mb.Box(at + new Vector3(0, 0.08f, -0.125f), new Vector3(0.07f, 0.13f, 0.02f), Palette.Door);
            mb.emission = 0.3f;
            mb.Box(at + new Vector3(0, 0.34f, -0.1f), new Vector3(0.045f, 0.05f, 0.02f), Palette.WindowLit);
            mb.emission = 0f;
            var hub = at + new Vector3(0, h - 0.02f, -0.13f);
            mb.Box(hub, new Vector3(0.04f, 0.04f, 0.05f), Palette.Timber);
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0, 0, 45f + k * 90f + yaw * 0.13f);
                mb.Box(hub + r * new Vector3(0, 0.2f, 0) - new Vector3(0, 0, 0.02f), new Vector3(0.016f, 0.4f, 0.01f), Palette.Timber, r);
                mb.Box(hub + r * new Vector3(0.035f, 0.23f, 0) - new Vector3(0, 0, 0.024f), new Vector3(0.06f, 0.3f, 0.005f), Palette.Canvas, r);
            }
        }

        public static void Granary(MeshBuilder mb, Vector3 at)
        {
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(at + new Vector3(x * 0.09f, 0.06f, z * 0.09f), new Vector3(0.03f, 0.12f, 0.03f), Palette.Bark);
            mb.Box(at + new Vector3(0, 0.17f, 0), new Vector3(0.24f, 0.1f, 0.24f), Palette.WoodLight);
            mb.Prism(at + new Vector3(0, 0.22f, 0), 0.2f, 0f, 0.16f, 6, Palette.Thatch);
        }

        public static void MarketHall(MeshBuilder mb, Vector3 at, float w, float d, bool town)
        {
            if (town)
            {
                // A stone ground floor of arches with a timber hall above (a town's market hall).
                mb.Box(at + new Vector3(0, 0.02f, 0), new Vector3(w, 0.04f, d), Palette.Stone);
                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (0, 1) })
                    mb.Box(at + new Vector3(x * (w / 2f - 0.03f), 0.12f, z * (d / 2f - 0.03f)), new Vector3(0.05f, 0.2f, 0.05f), Palette.Stone);
                House(mb, at + new Vector3(0, 0.12f, 0), w - 0.02f, d - 0.02f, 0.18f, 0.2f, Palette.PlasterWarm, Palette.Terracotta, false);
            }
            else Shed(mb, at, w - 0.06f, d - 0.06f, 0.22f, Palette.Terracotta, Palette.Timber);
            Crates(mb, at + new Vector3(-w * 0.2f, 0, -d * 0.1f));
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

        public static void Church(MeshBuilder mb, Vector3 at, float w, float d)
        {
            // Nave along the lot's depth, tower at the front (it faces the green).
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
            mb.emission = 0.3f;
            for (int k = -1; k <= 1; k++)
                foreach (int s in new[] { -1, 1 })
                    mb.Box(nave + new Vector3(s * (nw / 2f + 0.006f), 0.26f, k * nd * 0.28f), new Vector3(0.012f, 0.1f, 0.045f), Palette.WindowLit);
            mb.emission = 0f;
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

        public static void Watermill(MeshBuilder mb, Vector3 at, float w, float d)
        {
            // Stone mill house; the wheel turns in the water behind (local +z).
            House(mb, at + new Vector3(0, 0, -0.03f), w * 0.78f, d * 0.6f, 0.28f, 0.2f, Palette.Stone, Palette.Slate, true, false);
            var axle = at + new Vector3(w * 0.12f, 0.12f, d / 2f + 0.03f);
            var rot = Quaternion.Euler(0, 0, 90);
            mb.Prism(axle - new Vector3(0.04f, 0, 0), 0.17f, 0.17f, 0.08f, 10, Palette.Timber, rot, true, true, Palette.WoodLight);
            for (int k = 0; k < 8; k++)
            {
                var r = Quaternion.Euler(k * 45f, 0, 0);
                mb.Box(axle + r * new Vector3(0, 0.17f, 0), new Vector3(0.1f, 0.012f, 0.05f), Palette.WoodLight, r);
            }
        }

        public static void Smithy(MeshBuilder mb, Vector3 at, float w, float d)
        {
            House(mb, at + new Vector3(0, 0, 0.04f), w * 0.8f, d * 0.55f, 0.24f, 0.18f, Palette.Stone, Palette.Slate, false, false);
            var ch = at + new Vector3(w * 0.28f, 0, 0.1f);
            mb.Box(ch + new Vector3(0, 0.3f, 0), new Vector3(0.09f, 0.6f, 0.09f), Palette.RockDark);
            mb.emission = 1f;
            mb.Box(ch + new Vector3(0, 0.08f, -0.05f), new Vector3(0.06f, 0.05f, 0.012f), Palette.Fire);
            mb.emission = 0f;
            // anvil and a quench barrel out front
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
            // cloth drying on a line
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
    }
}
