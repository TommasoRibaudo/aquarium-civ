using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The town-era kit (#34, game-definition §8: the industrious renaissance town). Same palette, shader and low-poly
    /// rules as the camp and village pieces; each piece is built silhouette first so it reads at the default zoom on a
    /// phone, where a lot is a few dozen pixels: the tower mill is the tallest thing but the guildhall's clock tower, the
    /// market hall stands on open arches, the fountain sits on the cobbled square, the market stalls wear striped awnings
    /// and every workshop shows its trade in the yard (cloth on lines, hides on frames, a barrel stack).
    /// </summary>
    public static partial class Pieces
    {
        /// <summary>The trade a town workshop shows in its yard.</summary>
        public enum Trade { Weaver, Tanner, Cooper }
        public const int TradeCount = 3;

        /// <summary>A workshop's trade: <see cref="PieceData.variant"/> 1..3 picks one, 0 picks from the seed.</summary>
        public static Trade TradeOf(PieceData p) =>
            p.variant >= 1 && p.variant <= TradeCount ? (Trade)(p.variant - 1) : (Trade)(int)(Land.Hash3(p.seed, 8, 5) * TradeCount * 0.9999f);

        /// <summary>Vertices one town piece may use (a 0.5 × 0.45 lot): about 10 houses' worth at most, for #35's
        /// mobile budget (the whole town stays well under a 100k-vertex box).</summary>
        public const int TownPieceVertexBudget = 4500;

        // ---------------- shared bits ----------------
        /// <summary>A gable roof along x over a w × d footprint whose wall tops are at <paramref name="top"/>, with
        /// gable-end triangles in <paramref name="wall"/>.</summary>
        public static void Gable(MeshBuilder mb, Vector3 at, float w, float d, float top, float roofH, Color roof, Color wall, float oh = 0.06f)
        {
            float half = d / 2f + oh;
            float slope = Mathf.Sqrt(half * half + roofH * roofH), ang = Mathf.Atan2(roofH, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                mb.Box(new Vector3(at.x, top + roofH / 2f, at.z + s * half / 2f), new Vector3(w + 2 * oh, 0.032f, slope), roof, Quaternion.Euler(s * ang, 0, 0), true);
            foreach (int s in new[] { -1, 1 })
            {
                float x = at.x + s * w / 2f;
                var a = new Vector3(x, top, at.z - d / 2f); var b = new Vector3(x, top + roofH * (d / 2f) / half, at.z); var c = new Vector3(x, top, at.z + d / 2f);
                if (s > 0) mb.Tri(a, b, c, wall); else mb.Tri(c, b, a, wall);
            }
        }

        static void LampBox(MeshBuilder mb, Vector3 c, Vector3 size, float phase)
        {
            mb.emission = 0.35f; mb.lamp = Mathf.Repeat(phase, 1f);
            mb.Box(c, size, Palette.WindowLit);
            mb.emission = 0f; mb.lamp = -1f;
        }

        /// <summary>A barrel: staves and two iron hoops.</summary>
        public static void Barrel(MeshBuilder mb, Vector3 p, float r = 0.03f, float h = 0.075f)
        {
            mb.Prism(p, r, r, h, 8, Palette.WoodLight, topCol: Palette.Bark);
            foreach (float y in new[] { 0.2f, 0.75f })
                mb.Prism(p + Vector3.up * h * y, r + 0.003f, r + 0.003f, 0.01f, 8, Palette.Iron, capTop: false);
        }

        // ---------------- the cobbled square ----------------
        /// <summary>The town's fountain (the well, once the green is a cobbled square): an octagonal basin, a column and
        /// a little bowl spilling water.</summary>
        public static void Fountain(MeshBuilder mb, Vector3 at, int seed)
        {
            mb.Prism(at, 0.16f, 0.16f, 0.07f, 8, Palette.Stone, topCol: Palette.RockLight);
            mb.Prism(at + Vector3.up * 0.071f, 0.135f, 0.135f, 0.001f, 8, Palette.Water);
            mb.Prism(at + Vector3.up * 0.06f, 0.03f, 0.024f, 0.16f, 6, Palette.RockLight);
            mb.Prism(at + Vector3.up * 0.2f, 0.035f, 0.07f, 0.03f, 8, Palette.Stone, topCol: Palette.Water);
            mb.Ico(at + Vector3.up * 0.25f, 0.02f, Vector3.one, Palette.RockLight, 0.05f);
            // Water spilling from the bowl: four thin pale sheets.
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0, k * 90f + 45f, 0);
                mb.Box(at + r * new Vector3(0, 0.165f, 0.07f), new Vector3(0.022f, 0.075f, 0.005f), Color.Lerp(Palette.Water, Palette.Plaster, 0.2f), r);
            }
        }

        /// <summary>A market stall on the square: a counter of goods under a striped awning (stripes read as "market" at
        /// any size), a crate beside it. Local -z is the customer side.</summary>
        public static void MarketStall(MeshBuilder mb, Vector3 at, int seed)
        {
            var a = Palette.Accents[Mathf.Abs(seed) % Palette.Accents.Length];
            var stripe = Color.Lerp(a, Palette.Plaster, 0.15f);
            mb.Box(at + new Vector3(0, 0.05f, 0), new Vector3(0.26f, 0.1f, 0.12f), Palette.WoodLight);
            mb.Box(at + new Vector3(0, 0.05f, -0.061f), new Vector3(0.26f, 0.05f, 0.004f), stripe); // a painted front board
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                mb.Box(at + new Vector3(x * 0.12f, 0.13f + (z > 0 ? 0.03f : 0f), z * 0.055f), new Vector3(0.016f, 0.26f + (z > 0 ? 0.06f : 0f), 0.016f), Palette.Timber);
            const int n = 4;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) / 2f) * 0.075f;
                mb.Box(at + new Vector3(x, 0.29f, -0.005f), new Vector3(0.075f, 0.012f, 0.2f), i % 2 == 0 ? stripe : Palette.Plaster, Quaternion.Euler(-16f, 0, 0), true);
            }
            // Goods by seed: produce, cloth bolts or pots.
            switch (Mathf.Abs(seed / 7) % 3)
            {
                case 0:
                    for (int i = 0; i < 4; i++) mb.Ico(at + new Vector3(-0.09f + i * 0.06f, 0.115f, 0f), 0.024f, Vector3.one, i % 2 == 0 ? Palette.Ember : Palette.Wheat, 0.1f);
                    break;
                case 1:
                    for (int i = 0; i < 3; i++) Log(mb, at + new Vector3(-0.07f + i * 0.07f, 0.12f, 0f), 0.02f, 0.09f, 0f, Palette.Tunics[Mathf.Abs(seed + i * 2) % Palette.Tunics.Length]);
                    break;
                default:
                    for (int i = 0; i < 3; i++) mb.Prism(at + new Vector3(-0.08f + i * 0.08f, 0.1f, 0f), 0.022f, 0.016f, 0.045f, 6, Color.Lerp(Palette.Terracotta, Palette.SoilLight, i * 0.25f));
                    break;
            }
            Crates(mb, at + new Vector3(0.05f, 0, 0.13f)); // stock behind the counter, inside the stall's width
        }

        // ---------------- mills ----------------
        /// <summary>The town's tower mill: a tapering stone tower with a timber gallery, a turning cap and four big
        /// lattice sails (the village's post mill is plaster and smaller): the tallest silhouette in the box.</summary>
        public static void TowerMill(MeshBuilder mb, Vector3 at, float yaw, float lampPhase)
        {
            const float h = 0.66f;
            mb.Prism(at, 0.16f, 0.105f, h, 8, Palette.Stone, topCol: Palette.Stone);
            mb.Prism(at, 0.17f, 0.165f, 0.05f, 8, Palette.RockDark);
            mb.Prism(at + Vector3.up * 0.38f, 0.17f, 0.17f, 0.018f, 8, Palette.Timber, capTop: true, capBottom: true);
            foreach (int k in new[] { 0, 2, 4, 6 })
            {
                float ang = k / 8f * Mathf.PI * 2f + Mathf.PI / 8f;
                mb.Box(at + new Vector3(Mathf.Cos(ang) * 0.16f, 0.35f, Mathf.Sin(ang) * 0.16f), new Vector3(0.012f, 0.06f, 0.012f), Palette.Timber, Quaternion.Euler(0, -ang * Mathf.Rad2Deg, 25f));
            }
            // The cap: a timber drum with a pointed roof.
            mb.Prism(at + Vector3.up * h, 0.12f, 0.12f, 0.05f, 8, Palette.Timber);
            mb.Prism(at + Vector3.up * (h + 0.05f), 0.13f, 0f, 0.13f, 8, Palette.Terracotta);
            mb.Box(at + new Vector3(0, 0.08f, -0.155f), new Vector3(0.07f, 0.14f, 0.02f), Palette.Door);
            LampBox(mb, at + new Vector3(0, 0.27f, -0.14f), new Vector3(0.045f, 0.05f, 0.02f), lampPhase);
            LampBox(mb, at + new Vector3(0, 0.5f, -0.125f), new Vector3(0.04f, 0.045f, 0.02f), lampPhase + 0.3f);
            var hub = at + new Vector3(0, h + 0.03f, -0.15f);
            mb.Box(hub, new Vector3(0.045f, 0.045f, 0.06f), Palette.Timber);
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0, 0, 30f + k * 90f + yaw * 0.13f);
                mb.Box(hub + r * new Vector3(0, 0.26f, 0) - new Vector3(0, 0, 0.03f), new Vector3(0.018f, 0.52f, 0.012f), Palette.Timber, r);
                mb.Box(hub + r * new Vector3(0.045f, 0.3f, 0) - new Vector3(0, 0, 0.034f), new Vector3(0.075f, 0.4f, 0.005f), Palette.Canvas, r);
                for (int b = 1; b <= 3; b++) // lattice bars over the canvas
                    mb.Box(hub + r * new Vector3(0.045f, 0.1f + b * 0.1f, 0) - new Vector3(0, 0, 0.038f), new Vector3(0.08f, 0.008f, 0.004f), Palette.Timber, r);
            }
        }

        /// <summary>The town's watermill: two stone storeys, a big paddle wheel at the back fed by a timber flume, sacks of
        /// flour at the door.</summary>
        public static void TownWatermill(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase)
        {
            // The house on the left, the wheel on its right gable (seen face-on from the usual camera, not edge-on).
            House(mb, at + new Vector3(-w * 0.16f, 0, -0.02f), w * 0.56f, d * 0.6f, 0.2f, 0.2f, Palette.Stone, Palette.Slate, true, false, 2, lampPhase);
            var axle = at + new Vector3(w * 0.12f + 0.07f, 0.17f, 0.0f);
            var rot = Quaternion.Euler(0, 0, -90); // prism axis along +x
            mb.Prism(axle - new Vector3(0.045f, 0, 0), 0.21f, 0.21f, 0.09f, 12, Palette.Timber, rot, true, true, Palette.WoodLight);
            mb.Box(axle, new Vector3(0.16f, 0.03f, 0.03f), Palette.Iron);
            for (int k = 0; k < 12; k++)
            {
                var r = Quaternion.Euler(k * 30f, 0, 0);
                mb.Box(axle + r * new Vector3(0, 0.215f, 0), new Vector3(0.11f, 0.012f, 0.055f), Palette.WoodLight, r);
            }
            // The flume: a trough from the back, dropping water onto the wheel's top.
            var f0 = axle + new Vector3(0, 0.25f, 0.0f);
            mb.Box(f0 + new Vector3(0, 0, d * 0.32f), new Vector3(0.07f, 0.035f, d * 0.6f), Palette.Timber);
            mb.Box(f0 + new Vector3(0, 0.012f, d * 0.32f), new Vector3(0.05f, 0.016f, d * 0.6f), Palette.WaterDeep);
            mb.Box(f0 + new Vector3(0, -0.21f, d * 0.58f), new Vector3(0.022f, 0.42f, 0.022f), Palette.Timber);
            for (int k = 0; k < 3; k++)
                mb.Ico(at + new Vector3(-w * 0.34f + k * 0.055f, 0.03f, -d * 0.42f), 0.03f, new Vector3(1f, 1.2f, 1f), Palette.Canvas, 0.1f);
        }

        /// <summary>A two-man saw over a log on trestles (the town's sawmill yard).</summary>
        public static void SawFrame(MeshBuilder mb, Vector3 p)
        {
            foreach (int s in new[] { -1, 1 })
            {
                mb.Box(p + new Vector3(s * 0.1f, 0.04f, 0), new Vector3(0.02f, 0.08f, 0.08f), Palette.Timber);
            }
            Log(mb, p + new Vector3(0, 0.1f, 0), 0.028f, 0.3f, 90f, Palette.Bark);
            mb.Box(p + new Vector3(0.02f, 0.17f, 0), new Vector3(0.006f, 0.16f, 0.05f), Palette.Iron);
            mb.Box(p + new Vector3(0.02f, 0.26f, 0), new Vector3(0.02f, 0.016f, 0.08f), Palette.Timber);
        }

        // ---------------- the market hall and the guildhall ----------------
        /// <summary>The town's market hall: an open stone arcade with goods under it, a timber-framed hall above, a
        /// terracotta roof and a little bell turret.</summary>
        public static void TownMarketHall(MeshBuilder mb, Vector3 at, float w, float d, float lampPhase, int seed)
        {
            float hw = w - 0.06f, hd = d - 0.08f, legH = 0.17f;
            mb.Box(at + new Vector3(0, 0.015f, 0), new Vector3(hw + 0.04f, 0.03f, hd + 0.04f), Palette.Stone);
            // Arcade: pillars round the edge, lintels between them (open arches front and sides).
            for (int i = 0; i < 4; i++)
            {
                float x = -hw / 2f + 0.025f + i * (hw - 0.05f) / 3f;
                foreach (int s in new[] { -1, 1 })
                    mb.Box(at + new Vector3(x, 0.03f + legH / 2f, s * (hd / 2f - 0.025f)), new Vector3(0.04f, legH, 0.04f), Palette.RockLight);
            }
            foreach (int s in new[] { -1, 1 })
                mb.Box(at + new Vector3(s * (hw / 2f - 0.025f), 0.03f + legH / 2f, 0), new Vector3(0.04f, legH, 0.04f), Palette.RockLight);
            float y0 = 0.03f + legH;
            mb.Box(at + new Vector3(0, y0 + 0.015f, 0), new Vector3(hw + 0.02f, 0.03f, hd + 0.02f), Palette.Stone);
            // Goods in the arcade's shade.
            for (int k = 0; k < 3; k++)
                mb.Box(at + new Vector3(-hw * 0.28f + k * hw * 0.28f, 0.06f, 0), new Vector3(0.07f, 0.06f, 0.07f), k == 1 ? Palette.Accents[Mathf.Abs(seed) % Palette.Accents.Length] : Palette.WoodLight, Quaternion.Euler(0, k * 17f, 0));
            // The hall above.
            float wy = y0 + 0.03f, wallH = 0.16f;
            mb.Box(at + new Vector3(0, wy + wallH / 2f, 0), new Vector3(hw + 0.04f, wallH, hd + 0.04f), Palette.PlasterWarm);
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(at + new Vector3(x * (hw / 2f + 0.01f), wy + wallH / 2f, z * (hd / 2f + 0.01f)), new Vector3(0.03f, wallH, 0.03f), Palette.Timber);
            mb.Box(at + new Vector3(0, wy + wallH - 0.012f, -hd / 2f - 0.024f), new Vector3(hw + 0.04f, 0.024f, 0.01f), Palette.Timber);
            for (int k = -1; k <= 1; k++)
                LampBox(mb, at + new Vector3(k * hw * 0.3f, wy + wallH * 0.55f, -hd / 2f - 0.025f), new Vector3(0.06f, 0.065f, 0.01f), lampPhase + k * 0.13f);
            float top = wy + wallH;
            Gable(mb, at, hw + 0.04f, hd + 0.04f, top, 0.19f, Palette.Terracotta, Palette.PlasterWarm, 0.05f);
            // Bell turret on the ridge.
            var t = at + new Vector3(0, top + 0.17f, 0);
            mb.Box(t + new Vector3(0, 0.05f, 0), new Vector3(0.07f, 0.1f, 0.07f), Palette.Timber);
            mb.Prism(t + new Vector3(0, 0.1f, 0), 0.065f, 0f, 0.1f, 4, Palette.Slate, Quaternion.Euler(0, 45, 0));
        }

        /// <summary>
        /// The guildhall: a long two-storey hall (stone below, plaster above, slate roof) behind a tall clock tower at the
        /// front with an open belfry and a terracotta spire, guild banners either side of the door. The church's tower is
        /// plain stone with a slate spire; this one has a clock face and colours.
        /// </summary>
        public static void TownGuildhall(MeshBuilder mb, float w, float d, float lampPhase, int seed)
        {
            float hw = w * 0.9f, hd = Mathf.Min(d * 0.62f, 0.3f), hz = d / 2f - hd / 2f - 0.03f;
            var hall = new Vector3(0, 0, hz);
            mb.Box(hall + new Vector3(0, 0.04f, 0), new Vector3(hw + 0.04f, 0.08f, hd + 0.04f), Palette.RockDark);
            mb.Box(hall + new Vector3(0, 0.08f + 0.09f, 0), new Vector3(hw, 0.18f, hd), Palette.Stone);
            mb.Box(hall + new Vector3(0, 0.26f + 0.08f, 0), new Vector3(hw + 0.03f, 0.16f, hd + 0.03f), Palette.Plaster);
            for (int k = 0; k <= 4; k++)
                mb.Box(hall + new Vector3(-hw / 2f + k * hw / 4f, 0.34f, -hd / 2f - 0.017f), new Vector3(0.022f, 0.16f, 0.01f), Palette.Timber);
            for (int k = 0; k < 4; k++)
            {
                float x = -hw / 2f + (k + 0.5f) * hw / 4f;
                if (Mathf.Abs(x) < 0.08f) continue; // the tower stands there
                LampBox(mb, hall + new Vector3(x, 0.35f, -hd / 2f - 0.018f), new Vector3(0.05f, 0.07f, 0.01f), lampPhase + k * 0.11f);
                LampBox(mb, hall + new Vector3(x, 0.17f, -hd / 2f - 0.004f), new Vector3(0.04f, 0.08f, 0.01f), lampPhase + k * 0.07f + 0.5f);
            }
            Gable(mb, hall, hw + 0.03f, hd + 0.03f, 0.42f, 0.2f, Palette.Slate, Palette.Plaster);
            // The clock tower, in front of the hall's middle.
            var tw = new Vector3(0, 0, hz - hd / 2f - 0.05f);
            const float th = 0.72f, ts = 0.15f;
            mb.Box(tw + new Vector3(0, th / 2f, 0), new Vector3(ts, th, ts), Palette.Stone);
            mb.Box(tw + new Vector3(0, 0.02f, 0), new Vector3(ts + 0.03f, 0.04f, ts + 0.03f), Palette.RockDark);
            mb.Box(tw + new Vector3(0, 0.1f, -ts / 2f - 0.004f), new Vector3(0.07f, 0.17f, 0.01f), Palette.Door);
            foreach (float y in new[] { 0.3f, th - 0.005f }) mb.Box(tw + new Vector3(0, y, 0), new Vector3(ts + 0.02f, 0.02f, ts + 0.02f), Palette.RockDark);
            // Clock face on the front.
            var clock = tw + new Vector3(0, th - 0.1f, -ts / 2f - 0.006f);
            mb.Prism(clock, 0.05f, 0.05f, 0.008f, 10, Palette.Plaster, Quaternion.Euler(-90, 0, 0), true, false, Palette.Plaster);
            mb.Box(clock + new Vector3(0, 0.015f, -0.01f), new Vector3(0.008f, 0.035f, 0.004f), Palette.Iron);
            mb.Box(clock + new Vector3(0.012f, 0f, -0.01f), new Vector3(0.028f, 0.007f, 0.004f), Palette.Iron);
            // Open belfry: corner posts, a bell, then the spire and a gilded ball.
            float by = th + 0.01f;
            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                mb.Box(tw + new Vector3(x * (ts / 2f - 0.015f), by + 0.05f, z * (ts / 2f - 0.015f)), new Vector3(0.03f, 0.1f, 0.03f), Palette.Stone);
            mb.Prism(tw + new Vector3(0, by + 0.02f, 0), 0.03f, 0.018f, 0.05f, 6, Palette.Wheat);
            mb.Box(tw + new Vector3(0, by + 0.11f, 0), new Vector3(ts + 0.02f, 0.02f, ts + 0.02f), Palette.RockDark);
            mb.Prism(tw + new Vector3(0, by + 0.12f, 0), ts * 0.78f, 0f, 0.24f, 8, Palette.Terracotta, Quaternion.Euler(0, 22.5f, 0));
            mb.Ico(tw + new Vector3(0, by + 0.37f, 0), 0.016f, Vector3.one, Palette.Wheat, 0.05f);
            // Guild banners either side of the tower.
            mb.sway = 0.35f;
            for (int s = -1; s <= 1; s += 2)
            {
                var col = Palette.Accents[(Mathf.Abs(seed) + (s > 0 ? 2 : 0)) % Palette.Accents.Length];
                mb.Box(hall + new Vector3(s * hw * 0.3f, 0.3f, -hd / 2f - 0.022f), new Vector3(0.05f, 0.16f, 0.006f), col);
            }
            mb.sway = 0f;
        }

        // ---------------- the bakery (#112) ----------------
        /// <summary>The town's bakery: a warm plaster shop under a terracotta roof, a tall brick oven chimney with a
        /// smoke-coloured cap, a lit shop window, a loaf sign over the door and a few loaves on a bench out front.
        /// Kept simple (about 300 vertices); needs an Editor look.</summary>
        public static void TownBakery(MeshBuilder mb, float w, float d, float lampPhase, int seed)
        {
            float hw = w * 0.82f, hd = Mathf.Min(d * 0.7f, 0.3f), hz = d / 2f - hd / 2f - 0.03f;
            var h = new Vector3(0, 0, hz);
            const float wallH = 0.2f, roofH = 0.17f;
            mb.Box(h + new Vector3(0, 0.02f, 0), new Vector3(hw + 0.04f, 0.04f, hd + 0.04f), Palette.Stone);
            mb.Box(h + new Vector3(0, 0.04f + wallH / 2f, 0), new Vector3(hw, wallH, hd), Palette.PlasterWarm);
            foreach (int s in new[] { -1, 1 })
                mb.Box(h + new Vector3(s * (hw / 2f), 0.04f + wallH / 2f, -hd / 2f), new Vector3(0.025f, wallH, 0.025f), Palette.Timber);
            // Door on the left of the front, shop window on the right.
            mb.Box(h + new Vector3(-hw * 0.22f, 0.04f + 0.065f, -hd / 2f - 0.004f), new Vector3(0.07f, 0.13f, 0.01f), Palette.Door);
            LampBox(mb, h + new Vector3(hw * 0.2f, 0.04f + 0.11f, -hd / 2f - 0.004f), new Vector3(0.11f, 0.075f, 0.01f), lampPhase);
            mb.Box(h + new Vector3(hw * 0.2f, 0.04f + 0.065f, -hd / 2f - 0.02f), new Vector3(0.13f, 0.012f, 0.03f), Palette.WoodLight); // sill / counter
            float top = 0.04f + wallH;
            Gable(mb, h, hw + 0.02f, hd + 0.02f, top, roofH, Palette.Terracotta, Palette.PlasterWarm, 0.04f);
            // Oven chimney: brick stack up the right gable, a smoke-grey cap.
            var c = h + new Vector3(hw * 0.32f, 0, hd * 0.12f);
            mb.Box(c + new Vector3(0, top + 0.13f, 0), new Vector3(0.075f, 0.3f, 0.075f), Palette.Terracotta);
            mb.Box(c + new Vector3(0, top + 0.285f, 0), new Vector3(0.095f, 0.03f, 0.095f), Palette.FireSmoke);
            // Loaf sign on a bracket over the door.
            var sg = h + new Vector3(-hw * 0.22f, 0.04f + 0.17f, -hd / 2f - 0.05f);
            mb.Box(sg + new Vector3(0, 0.03f, 0.03f), new Vector3(0.012f, 0.012f, 0.06f), Palette.Timber);
            mb.sway = 0.2f;
            mb.Ico(sg, 0.03f, new Vector3(1.5f, 0.8f, 0.9f), Palette.Wheat, 0.08f);
            mb.sway = 0f;
            // A bench with loaves out front.
            var bn = h + new Vector3(hw * 0.2f, 0, -hd / 2f - 0.1f);
            mb.Box(bn + new Vector3(0, 0.035f, 0), new Vector3(0.18f, 0.012f, 0.05f), Palette.WoodLight);
            foreach (int s in new[] { -1, 1 }) mb.Box(bn + new Vector3(s * 0.07f, 0.0175f, 0), new Vector3(0.015f, 0.035f, 0.04f), Palette.Timber);
            for (int k = 0; k < 3; k++) mb.Ico(bn + new Vector3(-0.055f + k * 0.055f, 0.06f, 0), 0.02f, new Vector3(1.4f, 0.8f, 0.9f), Palette.Wheat, 0.08f);
        }

        // ---------------- workshops ----------------
        /// <summary>A town workshop: a timber-framed house on the left of the lot, its trade out in the yard on the
        /// right, a hanging sign in the trade's colour.</summary>
        public static void TradeWorkshop(MeshBuilder mb, float w, float d, Trade trade, float lampPhase, int seed)
        {
            float hd = Mathf.Min(0.3f, d * 0.7f);
            var house = new Vector3(-w * 0.24f, 0, d / 2f - hd / 2f - 0.03f);
            Color wall = Wall(seed), roof = trade == Trade.Tanner ? Palette.Slate : Palette.Terracotta;
            House(mb, house, w * 0.48f, hd, 0.2f, 0.2f, wall, roof, true, true, 2, lampPhase);
            var yard = new Vector3(w * 0.3f, 0, -d * 0.2f);
            // The trade props draw 1.25x, out in front of the eaves: big enough to tell the trades apart at the default zoom.
            var keepM = mb.M;
            mb.M = keepM * Matrix4x4.TRS(yard, Quaternion.identity, Vector3.one * 1.25f);
            yard = Vector3.zero;
            Color sign;
            switch (trade)
            {
                case Trade.Weaver:
                {
                    sign = Palette.Tunics[1];
                    // A loom under a lean-to roof, then cloth drying on a line: long bright strips.
                    mb.Box(yard + new Vector3(0, 0.2f, 0.1f), new Vector3(0.2f, 0.016f, 0.14f), Palette.Thatch, Quaternion.Euler(-20f, 0, 0), true);
                    foreach (int s in new[] { -1, 1 }) mb.Box(yard + new Vector3(s * 0.09f, 0.1f, 0.04f), new Vector3(0.016f, 0.2f, 0.016f), Palette.Timber);
                    mb.Box(yard + new Vector3(0, 0.07f, 0.1f), new Vector3(0.12f, 0.1f, 0.06f), Palette.WoodLight);
                    mb.Box(yard + new Vector3(0, 0.1f, 0.07f), new Vector3(0.1f, 0.06f, 0.004f), Palette.Tunics[4]);
                    var line = yard + new Vector3(0, 0, -0.12f);
                    foreach (int s in new[] { -1, 1 }) mb.Box(line + new Vector3(s * 0.13f, 0.11f, 0), new Vector3(0.014f, 0.22f, 0.014f), Palette.Timber);
                    mb.Box(line + new Vector3(0, 0.215f, 0), new Vector3(0.27f, 0.006f, 0.006f), Palette.Timber);
                    mb.sway = 0.4f;
                    for (int k = 0; k < 3; k++)
                        mb.Box(line + new Vector3(-0.08f + k * 0.08f, 0.15f, 0), new Vector3(0.06f, 0.13f, 0.005f), Palette.Tunics[(k * 2 + Mathf.Abs(seed)) % Palette.Tunics.Length]);
                    mb.sway = 0f;
                    break;
                }
                case Trade.Tanner:
                {
                    sign = Palette.Hide;
                    // Two hides stretched on leaning frames, two soaking vats.
                    for (int k = 0; k < 2; k++)
                    {
                        var f = yard + new Vector3(-0.06f + k * 0.13f, 0, 0.06f);
                        var r = Quaternion.Euler(-15f, k * 10f - 5f, 0);
                        mb.Box(f + new Vector3(0, 0.11f, 0), new Vector3(0.11f, 0.15f, 0.004f), k == 0 ? Palette.Hide : Color.Lerp(Palette.Hide, Palette.Canvas, 0.4f), r);
                        foreach (int s in new[] { -1, 1 }) mb.Box(f + r * new Vector3(s * 0.06f, 0.11f, 0), new Vector3(0.014f, 0.22f, 0.014f), Palette.Timber, r);
                        mb.Box(f + r * new Vector3(0, 0.19f, 0), new Vector3(0.13f, 0.012f, 0.012f), Palette.Timber, r);
                    }
                    for (int k = 0; k < 2; k++)
                        mb.Prism(yard + new Vector3(-0.07f + k * 0.12f, 0, -0.12f), 0.05f, 0.05f, 0.06f, 8, Palette.Bark, topCol: k == 0 ? Palette.Mud : Color.Lerp(Palette.Ember, Palette.Mud, 0.5f));
                    break;
                }
                default:
                {
                    sign = Palette.WoodLight;
                    // A cooper's barrel stack: three, two, one.
                    int n = 0;
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 3 - row; i++, n++)
                        {
                            var c = yard + new Vector3((i - (2 - row) / 2f) * 0.07f, 0.035f + row * 0.062f, 0.04f);
                            var rot = Quaternion.Euler(90f, 0, 0);
                            mb.Prism(c - rot * Vector3.up * 0.04f, 0.032f, 0.032f, 0.08f, 8, Palette.WoodLight, rot, true, true, Palette.Bark);
                            mb.Prism(c - rot * Vector3.up * 0.025f, 0.035f, 0.035f, 0.01f, 8, Palette.Iron, rot, false);
                        }
                    Barrel(mb, yard + new Vector3(-0.06f, 0, -0.13f));
                    Barrel(mb, yard + new Vector3(0.05f, 0, -0.12f));
                    mb.Box(yard + new Vector3(0.12f, 0.02f, -0.14f), new Vector3(0.012f, 0.04f, 0.1f), Palette.Iron); // hoop staves
                    break;
                }
            }
            mb.M = keepM;
            // Hanging sign at the house's front left corner.
            var post = house + new Vector3(-w * 0.24f - 0.01f, 0, -hd / 2f - 0.06f);
            mb.Box(post + new Vector3(0, 0.17f, 0), new Vector3(0.018f, 0.34f, 0.018f), Palette.Timber);
            mb.Box(post + new Vector3(0.045f, 0.32f, 0), new Vector3(0.09f, 0.012f, 0.012f), Palette.Timber);
            mb.sway = 0.15f;
            mb.Box(post + new Vector3(0.06f, 0.26f, 0), new Vector3(0.07f, 0.07f, 0.01f), sign);
            mb.sway = 0f;
        }
    }
}
