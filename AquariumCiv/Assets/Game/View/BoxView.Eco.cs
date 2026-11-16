using System.Collections.Generic;
using UnityEngine;
using Site = AquariumCiv.Sim.Settle.SiteMap;

namespace AquariumCiv.View
{
    /// <summary>
    /// P8, the living land (keepers-hands.md §2, #244): with <see cref="BoxLayout.eco"/> on, the ground's colour, what grows
    /// on it and the pond come from the sim's ecology layer instead of the fixed style-kit land. Bare ground is dry and
    /// cracked, darker where it's wet; green spreads cell by cell (blended over the four nearest cells); tufts, tall grass,
    /// bushes, seedlings, young and grown trees and reeds stand where the cover says; the pond is a flat sheet at the
    /// sim's water level over the cells it covers, the ground hiding its edge. Gray-box: the style kit's own pieces.
    /// </summary>
    public partial class BoxView
    {
        static readonly Color DirtDry = LandColors.C(0xC9AA80), DirtWet = LandColors.C(0x8C6C4C), DirtCrack = LandColors.C(0x7A5E42);

        /// <summary>Signed distance to the eco pond, coarse (by cells): negative on the water.</summary>
        float EcoShoreSD(float x, float z)
        {
            var ec = layout.eco;
            if (ec.pondCells <= 0) return ShoreRange;
            int c = Site.CellAt(x, z);
            if (c < 0) return ShoreRange;
            if (ec.WaterCell(c)) return -0.2f;
            int i = Site.I(c), j = Site.J(c), best = 9;
            for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int a = i + di, b = j + dj;
                    if (a < 0 || b < 0 || a >= Site.NX || b >= Site.NZ || !ec.WaterCell(Site.Index(a, b))) continue;
                    best = Mathf.Min(best, Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)));
                }
            return best == 1 ? 0.04f : best == 2 ? 0.13f : ShoreRange;
        }

        /// <summary>The pond's bed and its wet rim, when this point is under or just over the eco water.</summary>
        bool EcoPondBed(float x, float z, float y, out Color col)
        {
            col = default;
            var ec = layout.eco;
            if (ec.pondCells <= 0 || !ec.NearWater(Site.CellAt(x, z))) return false;
            var co = Co;
            if (y < ec.waterY - 0.004f) { col = Color.Lerp(co.sandWet, co.pondBed, Mathf.Clamp01((ec.waterY - y) / 0.06f)); return true; }
            if (y < ec.waterY + 0.02f) { col = Color.Lerp(co.sandWet, Palette.Mud, 0.45f); return true; }
            return false;
        }

        /// <summary>The ground under the living land: dry cracked dirt, darker when wet, turning to the box's grass as the cover greens.</summary>
        Color EcoGround(float x, float z, float y, float n)
        {
            var ec = layout.eco;
            float m = ec.MoistAt(x, z);
            var dry = Color.Lerp(DirtDry, DirtWet, Mathf.Clamp01(m * 1.4f));
            dry = Color.Lerp(dry, DirtWet, (n - 0.5f) * 0.25f);
            // Cracks: where two noises cross, a thin dark line; they close up as the ground gets wet.
            float cr = Mathf.Abs(Land.Noise(x * 6f, z * 6f) - Land.Noise(x * 6f + 7.3f, z * 6f - 3.1f));
            if (cr < 0.02f && m < 0.35f) dry = Color.Lerp(dry, DirtCrack, 0.35f * (1f - m / 0.35f));
            float g = ec.GreenAt(x, z);
            if (g <= 0.001f) return dry;
            var grass = Grass(n, y);
            byte cv = ec.At(x, z);
            if (EcoLayer.Wooded(cv)) grass = Color.Lerp(grass, Co.moss, 0.35f);          // the forest floor
            else if (cv == EcoLayer.Tall) grass = Color.Lerp(grass, Co.grassC, 0.3f);
            grass = Color.Lerp(grass, grass * 0.82f, Mathf.Clamp01(m - 0.4f));         // wet grass is a little darker
            return Color.Lerp(dry, grass, g);
        }

        /// <summary>P8 #288: where each bush was drawn (site cell -> ground point, radius), filled as the chunks build; BushView hangs the berries on them.</summary>
        public readonly Dictionary<int, (Vector3 pos, float r)> BushSpots = new();
        /// <summary>P9 #424 (B12.a): bush cells drawn by the thicket's arrival while it plays (they push up out of the soil), so the chunk
        /// leaves them out; their spot is still written to <see cref="BushSpots"/>. Change it through <see cref="HideBushes"/>.</summary>
        public readonly HashSet<int> HiddenBushes = new();

        /// <summary>P9 #424: hide (or show again) these bush cells; their chunks rebuild on the next <see cref="Refresh"/>.</summary>
        public void HideBushes(IEnumerable<int> cells, bool hide)
        {
            lock (BushSpots) foreach (int c in cells) { if (hide) HiddenBushes.Add(c); else HiddenBushes.Remove(c); }
        }
        /// <summary>#359: where each grown tree was drawn (site cell -> ground point, crown radius), as BushSpots: the kids play round the real trunks.</summary>
        public readonly Dictionary<int, (Vector3 pos, float r)> TreeSpots = new();
        /// <summary>#692 part 3 (B34): where each wild young tree or seedling was drawn (site cell -> ground point, size), so the people water and ring
        /// the little trees the player sees (a young tree is drawn on ~80 % of its cells).</summary>
        public readonly Dictionary<int, (Vector3 pos, float r)> SaplingSpots = new();
        /// <summary>#692 part 3 (B33.c): the flowers baked into tall-grass tufts (cell x 2 + tuft -> the flower's point), so a picker walks to a real one.</summary>
        public readonly Dictionary<int, Vector3> FlowerSpots = new();

        /// <summary>#730 (landscaping phase 2): the spots are written as chunks build and were never taken out, so a tree or a bush the landscaping
        /// tool lifted (or one that died) stayed a "tree to play round" until the box was rebuilt. Each refresh drops the spots whose cell no
        /// longer holds what was drawn there.</summary>
        void PruneSpots()
        {
            var ec = layout.eco;
            if (ec == null || ec.cover == null) return;
            var gone = new List<int>();
            lock (TreeSpots)
            {
                foreach (var kv in TreeSpots)
                {
                    bool keepers = kv.Key == ec.treeCell && ec.treeStage >= 4;
                    if (!keepers && (kv.Key < 0 || kv.Key >= ec.cover.Length || ec.cover[kv.Key] != EcoLayer.Tree)) gone.Add(kv.Key);
                }
                foreach (int c in gone) TreeSpots.Remove(c);
            }
            gone.Clear();
            lock (SaplingSpots)
            {
                foreach (var kv in SaplingSpots) if (kv.Key < 0 || kv.Key >= ec.cover.Length || ec.cover[kv.Key] != EcoLayer.Young && ec.cover[kv.Key] != EcoLayer.Seedling) gone.Add(kv.Key);
                foreach (int c in gone) SaplingSpots.Remove(c);
            }
            gone.Clear();
            lock (FlowerSpots)
            {
                foreach (var kv in FlowerSpots) { int c = kv.Key / 2; if (c < 0 || c >= ec.cover.Length || ec.cover[c] != EcoLayer.Tall) gone.Add(kv.Key); }
                foreach (int c in gone) FlowerSpots.Remove(c);
            }
            gone.Clear();
            lock (BushSpots)
            {
                foreach (var kv in BushSpots) if (kv.Key < 0 || kv.Key >= ec.cover.Length || ec.cover[kv.Key] != EcoLayer.Bush) gone.Add(kv.Key);
                foreach (int c in gone) BushSpots.Remove(c);
            }
        }

        /// <summary>The radius round the Keeper's tree inside which a grown wild tree drops branches the people gather (SimCore.DeadfallTreeCell).</summary>
        static readonly float DeadfallRadius = new AquariumCiv.Sim.Eco.EcoTuning().woodsClearRadius;

        /// <summary>#677 (the user, 2026-11-14): a grown wild tree near the Keeper's tree has fallen branches lying round its foot, the deadfall
        /// the people pick up by hand: two to four, each a shape of four and maybe mirrored, its own size, angle and weathering,
        /// all from the cell's hash (a rebuild never shuffles them).</summary>
        void FallenBranches(MeshBuilder mb, int c, Vector2 p, float s, EcoLayer ec, List<(Vector2 p, float r)> near, System.Func<int, int, float> H)
        {
            if (ec.treeCell < 0) return;
            if (Vector2.Distance(p, new Vector2(Site.CXc(ec.treeCell), Site.CZc(ec.treeCell))) >= DeadfallRadius) return;
            int n = 2 + (int)(H(c, 40) * 2.99f);
            float a0 = H(c, 41) * 360f;
            for (int b = 0; b < n; b++)
            {
                float ang = (a0 + b * (360f / n) + (H(c, 42 + b) - 0.5f) * 50f) * Mathf.Deg2Rad;
                float d = (0.19f + 0.10f * H(c, 46 + b)) * s;
                var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
                if (!AquariumCiv.Sim.Settle.Terrain.InBox(q.x, q.y, AquariumCiv.Sim.Settle.Terrain.GlassMargin)) continue;
                if (!Free(q, 0.025f, near)) continue;
                var ib = MeshBuilder.Rent(seed * 7919 + c * 104729 + (20 + b) * 15485863);
                int variant = (int)(H(c, 50 + b) * 3.99f);
                bool mirror = H(c, 54 + b) < 0.5f;
                float len = Mathf.Lerp(0.10f, 0.18f, H(c, 58 + b)) * (variant == 3 ? 0.8f : 1f);
                float yaw = ang * Mathf.Rad2Deg + 90f + (H(c, 62 + b) - 0.5f) * 70f; // lying roughly round the trunk, never all one way
                Pieces.FallenBranch(ib, land.OnGround(q), len, yaw, variant, mirror, H(c, 66 + b));
                mb.AppendFree(ib);
                near.Add((q, 0.03f));
            }
        }

        /// <summary>What grows in this chunk, from the cover cell by cell (deterministic per cell, so a rebuild never shuffles it).</summary>
        void EcoNature(MeshBuilder mb, int k, List<(Vector2 p, float r)> near)
        {
            var ec = layout.eco;
            var co = Co;
            float H(int c, int salt) => Land.Hash3(seed * 613 + 41, c, salt);
            MeshBuilder Item(int c, int cat) => MeshBuilder.Rent(seed * 7919 + c * 104729 + cat * 15485863);
            Vector2 Spot(int c, int salt) => new(Site.CXc(c) + (H(c, salt) - 0.5f) * 0.08f, Site.CZc(c) + (H(c, salt + 1) - 0.5f) * 0.08f);

            // The Keeper's tree first: it claims its ground.
            if (ec.treeCell >= 0 && ChunkOf(Site.CXc(ec.treeCell), Site.CZc(ec.treeCell)) == k)
            {
                var p = new Vector2(Site.CXc(ec.treeCell), Site.CZc(ec.treeCell));
                var ib = Item(ec.treeCell, 9);
                KeeperTree(ib, land.OnGround(p), ec.treeStage, ec.treeNeed, co, land);
                mb.AppendFree(ib);
                if (ec.treeStage >= 4) lock (TreeSpots) TreeSpots[ec.treeCell] = (land.OnGround(p), 0.12f + 0.06f * ec.treeStage); // #359: the Keeper's grown tree is a tree to play round
                near.Add((p, 0.12f + 0.06f * ec.treeStage));
            }
            // Then the grown trees, then the young ones, then bushes and seedlings, then the grass (which claims nothing). Grown before young (#402):
            // in one pass a young tree earlier in cell order took a grown tree's room, so plucking it let a grown tree pop up in its place.
            for (int pass = -1; pass < 3; pass++)
                for (int c = 0; c < Site.N; c++)
                {
                    byte cv = ec.cover[c];
                    if (cv == EcoLayer.Bare || cv == EcoLayer.Water || c == ec.treeCell) continue;
                    var p = Spot(c, 1);
                    if (ChunkOf(p) != k) continue;
                    int i = Site.I(c), j = Site.J(c);
                    if ((pass == -1 && cv == EcoLayer.Tree) || (pass == 0 && cv == EcoLayer.Young))
                    {
                        // A grown crown is two cells across: one per 2 × 2 block, plus a few in between for an uneven edge.
                        bool grown = cv == EcoLayer.Tree;
                        if (grown && ((i | j) & 1) != 0 && H(c, 3) > 0.15f) continue;
                        if (!grown && H(c, 3) > 0.8f) continue; // #569: a young tree was drawn on 45% of its cells, so a misted half's saplings did not read
                        float s = grown ? Mathf.Lerp(0.75f, 1.15f, H(c, 4)) : Mathf.Lerp(0.42f, 0.58f, H(c, 4));
                        if (!Free(p, 0.12f * s, near)) continue;
                        var ib = Item(c, 1);
                        Pieces.Tree(ib, land.OnGround(p), s, H(c, 5) < 0.3f, co);
                        mb.AppendFree(ib);
                        near.Add((p, 0.16f * s));
                        if (grown) lock (TreeSpots) TreeSpots[c] = (land.OnGround(p), 0.16f * s); // #359
                        else lock (SaplingSpots) SaplingSpots[c] = (land.OnGround(p), 0.16f * s); // #692 part 3
                        if (grown) FallenBranches(mb, c, p, s, ec, near, H);
                    }
                    else if (pass == 1 && (cv == EcoLayer.Bush || cv == EcoLayer.Seedling))
                    {
                        if (cv == EcoLayer.Bush)
                        {
                            // P8 #288: every bush cell has its bush (the gatherers walk to cells, so none may be missing); its berries are
                            // not in this mesh: BushView draws them by season and by what was picked.
                            float r = Mathf.Lerp(0.05f, 0.1f, H(c, 4));
                            var ib = Item(c, 2);
                            var bg = land.OnGround(p);
                            bool hidden; lock (BushSpots) hidden = HiddenBushes.Contains(c);
                            if (!hidden) { Pieces.Bush(ib, bg, r, false, co); mb.AppendFree(ib); } else ib.Free(); // #424: the arrival draws it
                            near.Add((p, r));
                            lock (BushSpots) BushSpots[c] = (bg, r); // chunks build in parallel (#279: a big pond rebuilt several at once and corrupted the dictionary)
                        }
                        else
                        {
                            if (H(c, 3) > 0.75f) continue; // #569: was 0.55
                            float s = Mathf.Lerp(0.2f, 0.3f, H(c, 4));
                            if (!Free(p, 0.04f, near)) continue;
                            var ib = Item(c, 3);
                            Pieces.Tree(ib, land.OnGround(p), s, H(c, 5) < 0.3f, co);
                            mb.AppendFree(ib);
                            near.Add((p, 0.05f));
                            lock (SaplingSpots) SaplingSpots[c] = (land.OnGround(p), 0.05f); // #692 part 3
                        }
                    }
                    else if (pass == 2)
                    {
                        if (cv == EcoLayer.Reeds)
                        {
                            for (int r = 0; r < 2; r++)
                            {
                                var q = Spot(c, 10 + r * 2);
                                var ib = Item(c, 4 + r);
                                float y = ec.pondCells > 0 ? Mathf.Max(land.Height(q.x, q.y), ec.waterY) : land.Height(q.x, q.y);
                                Pieces.Reed(ib, new Vector3(q.x, y, q.y), H(c, 12 + r) < 0.33f ? co.sprout : co.leafB);
                                mb.AppendFree(ib);
                            }
                            continue;
                        }
                        // Grass: one short tuft on most cells; tall grass two taller ones; a little under bushes and young trees.
                        int tufts = cv == EcoLayer.Tall ? 2 : cv == EcoLayer.Grass ? (H(c, 6) < 0.6f ? 1 : 0) : (H(c, 6) < 0.3f ? 1 : 0);
                        for (int t = 0; t < tufts; t++)
                        {
                            var q = Spot(c, 20 + t * 2);
                            if (!Free(q, 0.015f, near)) continue;
                            var ib = Item(c, 6 + t);
                            float h = cv == EcoLayer.Tall ? Mathf.Lerp(0.08f, 0.13f, H(c, 30 + t)) : Mathf.Lerp(0.035f, 0.065f, H(c, 30 + t));
                            float c3 = H(c, 32 + t);
                            var g = land.OnGround(q);
                            Pieces.GrassTuft(ib, g, h, c3 < 0.33f ? co.tuftA : c3 < 0.66f ? co.tuftB : co.tuftC, cv == EcoLayer.Tall);
                            if (cv == EcoLayer.Tall && H(c, 34 + t) < 0.12f)
                            {
                                ib.Ico(g + Vector3.up * h * 0.8f, 0.014f, Vector3.one, Palette.Flowers[Mathf.Min(Palette.Flowers.Length - 1, (int)(H(c, 36) * Palette.Flowers.Length))], 0.1f);
                                lock (FlowerSpots) FlowerSpots[c * 2 + t] = g + Vector3.up * h * 0.8f; // #692 part 3 (B33.c)
                            }
                            mb.AppendFree(ib);
                        }
                    }
                }
        }

        /// <summary>The Keeper's one tree, by stage (0 seed in a mound, 1 sprout, 2 sapling, 3 young, 4 grown, larger than the
        /// forest's), showing one need at a time: 1 thirst (fallen leaves at its foot), 3 weeds (tufts crowding it).</summary>
        static void KeeperTree(MeshBuilder mb, Vector3 g, int stage, int need, LandColors co, Land land) => KeeperTreeKit.Build(mb, g, stage, need, co, land);

        /// <summary>How far past the land's edge the glass stands inside (the water meets the pane, #269): the panes sit 7.5 cm out
        /// and are 2 cm thick.</summary>
        public const float GlassInner = 0.062f;

        /// <summary>#701 round 2: where the pond's sheet and its face at a pane end: 4 mm proud of the land's edge (the soil cut), not out at the
        /// glass. The water stands on the cut face like the land does, so nothing of it hangs past the dug edge and the pane's own
        /// thickness reads the same beside water as beside land.</summary>
        public const float WaterFace = 0.004f;

        /// <summary>The pond: a flat sheet at the water level over the water cells and their neighbours; the ground above the
        /// water hides the sheet's square edge, so the shoreline is where the land meets the level. Where the water reaches the
        /// box's edge the sheet runs on to the glass (clamped there) and a wall of water stands from the ground up to the level,
        /// so you see the pond through the side pane (#269).</summary>
        /// <summary>#701: how far under the level the ground must be, at all four corners, for a piece of the sheet to be drawn (the mud band reads up to +2 cm).</summary>
        public const float RimMargin = 0.012f;

        /// <summary>#701: whether a piece of the water sheet (the box-local rectangle) lies under the level by <see cref="RimMargin"/> at all four corners.
        /// The water and the ice share it, so the ice ends where the water ends.</summary>
        public bool WaterPieceUnder(float x0, float z0, float x1, float z1)
        {
            float lvl = layout.eco.waterY;
            float hi = Mathf.Max(Mathf.Max(land.Height(x0, z0), land.Height(x0, z1)), Mathf.Max(land.Height(x1, z0), land.Height(x1, z1)));
            return hi < lvl - RimMargin;
        }

        Mesh EcoWaterMesh()
        {
            var mb = new MeshBuilder(seed + 3) { jitter = 0f };
            var ec = layout.eco;
            var co = Co;
            if (ec.pondCells > 0)
            {
                float y = ec.waterY, h = Site.Cell * 0.5f + 0.003f;
                float HX = Land.HX;
                for (int c = 0; c < Site.N; c++)
                {
                    if (!ec.NearWater(c)) continue;
                    float x = Site.CXc(c), z = Site.CZc(c);
                    int i = Site.I(c), j = Site.J(c);
                    // #701: the sheet is cut into a grid inside the cell and keeps only the pieces whose corners are all under the
                    // level by a margin: it follows the dug bank and ends a little inside the dirt rim, never over the grass.
                    const int Sub = 3;
                    float step = 2f * h / Sub;
                    for (int sj = 0; sj < Sub; sj++)
                        for (int si = 0; si < Sub; si++)
                        {
                            float x0 = x - h + si * step, x1 = x0 + step, z0 = z - h + sj * step, z1 = z0 + step;
                            bool eL = i == 0 && si == 0 && land.Height(-HX + 0.001f, (z0 + z1) * 0.5f) < y, eR = i == Site.NX - 1 && si == Sub - 1 && land.Height(HX - 0.001f, (z0 + z1) * 0.5f) < y;
                            bool eF = j == 0 && sj == 0 && land.Height((x0 + x1) * 0.5f, Land.ZMin + 0.001f) < y, eB = j == Site.NZ - 1 && sj == Sub - 1 && land.Height((x0 + x1) * 0.5f, Land.ZMax - 0.001f) < y;
                            if (!WaterPieceUnder(x0, z0, x1, z1)) continue; // at the glass too: the piece against the pane still ends inside the bank
                            // At the box's edge the sheet ends on the soil cut's face (4 mm proud), where the ground there is under the water.
                            if (eL) x0 = -HX - WaterFace; if (eR) x1 = HX + WaterFace;
                            if (eF) z0 = Land.ZMin - WaterFace; if (eB) z1 = Land.ZMax + WaterFace;
                            x0 = Mathf.Max(x0, -HX - WaterFace); x1 = Mathf.Min(x1, HX + WaterFace);
                            z0 = Mathf.Max(z0, Land.ZMin - WaterFace); z1 = Mathf.Min(z1, Land.ZMax + WaterFace);
                            float depth = ec.waterY - land.Height((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
                            var col = Color.Lerp(co.water, co.waterDeep, Mathf.Clamp01(depth / 0.12f));
                            QuadUp(mb, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), col);
                            // The wall at the glass, seen through the pane: from the ground at the edge up to the level.
                            if (eL) WaterWall(mb, new Vector2(-HX, z0), new Vector2(-HX, z1), -HX - WaterFace, new Vector3(-1, 0, 0), y, co);
                            if (eR) WaterWall(mb, new Vector2(HX, z0), new Vector2(HX, z1), HX + WaterFace, new Vector3(1, 0, 0), y, co);
                            if (eF) WaterWall(mb, new Vector2(x0, Land.ZMin), new Vector2(x1, Land.ZMin), Land.ZMin - WaterFace, new Vector3(0, 0, -1), y, co);
                            if (eB) WaterWall(mb, new Vector2(x0, Land.ZMax), new Vector2(x1, Land.ZMax), Land.ZMax + WaterFace, new Vector3(0, 0, 1), y, co);
                        }
                }
            }
            return mb.ToMesh("Water");
        }

        /// <summary>One cell's face of the pond against a pane: its top edge at the water level, its foot on the soil cut's top,
        /// lighter at the top and deeper below, drawn facing out (to the eye behind the glass). <paramref name="a"/>/<paramref name="b"/>
        /// are the points on the land's edge (their height gives the foot); the wall stands at <paramref name="glassAt"/> on its axis.</summary>
        void WaterWall(MeshBuilder mb, Vector2 a, Vector2 b, float glassAt, Vector3 outward, float y, LandColors co)
        {
            bool alongX = outward.x != 0f;
            Vector3 P(Vector2 q, float h) => alongX ? new Vector3(glassAt, h, q.y) : new Vector3(q.x, h, glassAt);
            // The foot sits just under the soil cut's top so no gap shows between the ground and the wall.
            float ha = land.Height(a.x, a.y) - 0.03f, hb = land.Height(b.x, b.y) - 0.03f;
            if (ha >= y && hb >= y) return;
            ha = Mathf.Min(ha, y - 0.01f); hb = Mathf.Min(hb, y - 0.01f);
            var top = Color.Lerp(co.water, Color.white, 0.12f);
            var da = Color.Lerp(co.water, co.waterDeep, Mathf.Clamp01((y - ha) / 0.3f));
            var db = Color.Lerp(co.water, co.waterDeep, Mathf.Clamp01((y - hb) / 0.3f));
            Vector3 fa = P(a, ha), fb = P(b, hb), ta = P(a, y), tb = P(b, y);
            // Front faces are the ones whose cross product points at the viewer (as QuadUp has it).
            if (Vector3.Dot(Vector3.Cross(ta - fa, tb - fa), outward) > 0f) { mb.TriC(fa, ta, tb, da, top, top); mb.TriC(fa, tb, fb, da, top, db); }
            else { mb.TriC(fa, tb, ta, da, top, top); mb.TriC(fa, fb, tb, da, db, top); }
        }

        /// <summary>The meshes as drawn, for the tests and the shots (#269, #270).</summary>
        public Mesh DebugWaterMesh => waterFilter != null ? waterFilter.sharedMesh : null;
        public Mesh DebugDampMesh => tableFilter != null ? tableFilter.sharedMesh : null;

        void RefreshEcoWater()
        {
            if (waterFilter == null) return;
            var old = waterFilter.sharedMesh;
            waterFilter.sharedMesh = EcoWaterMesh();
            if (old != null) { if (Application.isPlaying) Destroy(old); else DestroyImmediate(old); }
            waterHash = layout.eco.WaterHash();
        }

        // ---------------- droplets on the glass (keepers-hands §5.1, #245) ----------------
        static readonly int DropletsId = Shader.PropertyToID("_Droplets");
        /// <summary>The droplets and fog drawn on the panes, 0..1 (the sim's droplets; the mist view eases it live).</summary>
        public float Droplets { get; private set; }

        public void SetDroplets(float v)
        {
            Droplets = Mathf.Clamp01(v);
            foreach (var m in panes) if (m != null) m.SetFloat(DropletsId, Droplets);
        }

        // ---------------- the water table in the soil cut (keepers-hands §5.2, #245, #270) ----------------
        MeshFilter tableFilter;
        MeshFilter sheenFilter;
        Material dampMat, sheenMat;
        int tableKey = int.MinValue;

        /// <summary>The table's level as drawn, in steps of 2 mm of height (the damp earth is rebuilt only when it moves a step).</summary>
        int TableKey => Eco ? Mathf.RoundToInt(layout.eco.tableY * 500f) : int.MinValue;

        /// <summary>How damp the soil cut is at a height, 0 (dry) to 1: full under the water table, fading out over about 20 cm
        /// above it, the fade's height wobbling along the glass and darker streaks running down inside (#270). Smooth in
        /// <paramref name="y"/> everywhere: there is no line or edge to the damp earth.</summary>
        public static float DampAmount(float along, int side, float y, float table)
        {
            float w = (Land.Noise(along * 2.3f + side * 7f, 1.1f) - 0.5f) * 0.09f + (Land.Noise(along * 9f + side * 3f, 5.3f) - 0.5f) * 0.05f;
            float t = Mathf.Clamp01((y - (table - 0.11f + w)) / 0.2f);
            float damp = 1f - t * t * (3f - 2f * t);
            float streak = 0.8f + 0.2f * Land.Noise(along * 17f + side * 5f, y * 2.2f + 9f); // a few darker damp streaks
            return damp * streak;
        }

        /// <summary>The water table seen through the glass as damp earth: the soil cut below it is darker and a little wet, and
        /// fades into the dry layers above with no line. The mesh carries only an amount in its vertex alpha; the EcoWet material
        /// multiplies the soil by a darker, warmer colour and EcoSheen adds a faint glint. It rises as the Keeper's mists soak
        /// down, and reaches the top of the cut where the pond surfaces.</summary>
        Mesh EcoTableMesh()
        {
            float HX = Land.HX, table = layout.eco.tableY;
            var corners = new[] { new Vector2(-HX, Land.ZMin), new Vector2(HX, Land.ZMin), new Vector2(HX, Land.ZMax), new Vector2(-HX, Land.ZMax) };
            // The heights the damp is sampled at: coarse far under the table, fine around the fade.
            var ys = new List<float> { 0f };
            float lo = Mathf.Max(0.05f, table - 0.3f), hi = table + 0.12f;
            for (float y = 0.15f; y < lo; y += 0.15f) ys.Add(y);
            for (float y = lo; y < hi; y += 0.03f) ys.Add(y);
            ys.Add(hi);
            int rows = ys.Count;
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int side = 0; side < 4; side++)
            {
                var p0 = corners[side]; var p1 = corners[(side + 1) % 4];
                int segs = Mathf.RoundToInt(Vector2.Distance(p0, p1) / 0.0625f);
                var o = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized * 0.004f; // just proud of the soil cut
                int first = verts.Count;
                for (int s = 0; s <= segs; s++)
                {
                    var a = Vector2.Lerp(p0, p1, s / (float)segs);
                    float top = Layers(a)[4] - 0.004f;
                    float along = side < 2 ? a.x : a.y;
                    for (int k = 0; k < rows; k++)
                    {
                        float y = Mathf.Min(ys[k], top);
                        verts.Add(new Vector3(a.x, y, a.y) + o);
                        cols.Add(new Color(1f, 1f, 1f, DampAmount(along, side, y, table)));
                    }
                }
                for (int s = 0; s < segs; s++)
                    for (int k = 0; k < rows - 1; k++)
                    {
                        int v = first + s * rows + k, w = v + rows;
                        tris.Add(v); tris.Add(v + 1); tris.Add(w + 1);
                        tris.Add(v); tris.Add(w + 1); tris.Add(w);
                    }
            }
            var m = new Mesh { name = "DampEarth", indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        MeshFilter SpawnDamp(string name, Material mat)
        {
            var go = new GameObject(name) { hideFlags = GenFlags };
            go.transform.SetParent(root, false);
            var f = go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return f;
        }

        /// <summary>Called on every refresh: (re)draws the damp earth when the living land is on and the table moved a step.</summary>
        void SyncEcoTable()
        {
            int key = TableKey;
            if (key == tableKey && (tableFilter != null || key == int.MinValue)) return;
            tableKey = key;
            if (tableFilter != null) Kill(tableFilter.sharedMesh);
            if (!Eco)
            {
                if (tableFilter != null) Kill(tableFilter.gameObject);
                if (sheenFilter != null) Kill(sheenFilter.gameObject);
                tableFilter = sheenFilter = null;
                return;
            }
            if (tableFilter == null)
            {
                dampMat ??= EcoSurface.MakeMaterial("EcoWet", 0, 2, 0);       // multiply: DstColor Zero
                sheenMat ??= EcoSurface.MakeMaterial("EcoSheen", 1, 1, 1);    // add
                if (sheenMat != null) sheenMat.SetFloat("_SheenAmount", 0.16f);
                tableFilter = SpawnDamp("DampEarth", dampMat);
                sheenFilter = SpawnDamp("DampSheen", sheenMat);
            }
            var mesh = EcoTableMesh();
            tableFilter.sharedMesh = mesh; sheenFilter.sharedMesh = mesh;
        }
    }
}
