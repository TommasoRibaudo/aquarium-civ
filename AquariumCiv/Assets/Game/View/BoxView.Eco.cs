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
                KeeperTree(ib, land.OnGround(p), ec.treeStage, ec.treeNeed, co);
                mb.AppendFree(ib);
                near.Add((p, 0.08f + 0.06f * ec.treeStage));
            }
            // Then the trees, big to small, then bushes, then the grass (which claims nothing).
            for (int pass = 0; pass < 3; pass++)
                for (int c = 0; c < Site.N; c++)
                {
                    byte cv = ec.cover[c];
                    if (cv == EcoLayer.Bare || cv == EcoLayer.Water || c == ec.treeCell) continue;
                    var p = Spot(c, 1);
                    if (ChunkOf(p) != k) continue;
                    int i = Site.I(c), j = Site.J(c);
                    if (pass == 0 && (cv == EcoLayer.Tree || cv == EcoLayer.Young))
                    {
                        // A grown crown is two cells across: one per 2 × 2 block, plus a few in between for an uneven edge.
                        bool grown = cv == EcoLayer.Tree;
                        if (grown && ((i | j) & 1) != 0 && H(c, 3) > 0.15f) continue;
                        if (!grown && H(c, 3) > 0.45f) continue;
                        float s = grown ? Mathf.Lerp(0.75f, 1.15f, H(c, 4)) : Mathf.Lerp(0.42f, 0.58f, H(c, 4));
                        if (!Free(p, 0.12f * s, near)) continue;
                        var ib = Item(c, 1);
                        Pieces.Tree(ib, land.OnGround(p), s, H(c, 5) < 0.3f, co);
                        mb.AppendFree(ib);
                        near.Add((p, 0.16f * s));
                    }
                    else if (pass == 1 && (cv == EcoLayer.Bush || cv == EcoLayer.Seedling))
                    {
                        if (cv == EcoLayer.Bush)
                        {
                            if (H(c, 3) > 0.6f) continue;
                            float r = Mathf.Lerp(0.05f, 0.1f, H(c, 4));
                            if (!Free(p, r * 0.8f, near)) continue;
                            var ib = Item(c, 2);
                            Pieces.Bush(ib, land.OnGround(p), r, H(c, 5) < 0.2f, co);
                            mb.AppendFree(ib);
                            near.Add((p, r));
                        }
                        else
                        {
                            if (H(c, 3) > 0.55f) continue;
                            float s = Mathf.Lerp(0.2f, 0.3f, H(c, 4));
                            if (!Free(p, 0.04f, near)) continue;
                            var ib = Item(c, 3);
                            Pieces.Tree(ib, land.OnGround(p), s, H(c, 5) < 0.3f, co);
                            mb.AppendFree(ib);
                            near.Add((p, 0.05f));
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
                            Pieces.GrassTuft(ib, g, h, c3 < 0.33f ? co.tuftA : c3 < 0.66f ? co.tuftB : co.tuftC);
                            if (cv == EcoLayer.Tall && H(c, 34 + t) < 0.12f)
                                ib.Ico(g + Vector3.up * h * 0.8f, 0.014f, Vector3.one, Palette.Flowers[Mathf.Min(Palette.Flowers.Length - 1, (int)(H(c, 36) * Palette.Flowers.Length))], 0.1f);
                            mb.AppendFree(ib);
                        }
                    }
                }
        }

        /// <summary>The Keeper's one tree, by stage (0 seed in a mound, 1 sprout, 2 sapling, 3 young, 4 grown, larger than the
        /// forest's), showing one need at a time: 1 thirst (fallen leaves at its foot), 3 weeds (tufts crowding it).</summary>
        static void KeeperTree(MeshBuilder mb, Vector3 g, int stage, int need, LandColors co)
        {
            if (stage <= 1)
            {
                mb.Ico(g + Vector3.up * 0.004f, 0.035f, new Vector3(1.2f, 0.35f, 1.2f), Palette.Mud, 0.2f);
                if (stage == 1) Pieces.GrassTuft(mb, g + Vector3.up * 0.01f, 0.05f, co.sprout);
            }
            else Pieces.Tree(mb, g, stage == 2 ? 0.32f : stage == 3 ? 0.65f : 1.35f, false, co);
            float foot = 0.05f + 0.05f * stage;
            if (need == 1)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.3f;
                    mb.Ico(g + new Vector3(Mathf.Cos(a) * foot, 0.006f, Mathf.Sin(a) * foot), 0.014f, new Vector3(1f, 0.3f, 1f), Palette.Ember, 0.1f);
                }
            else if (need == 3)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.25f + 0.4f;
                    Pieces.GrassTuft(mb, g + new Vector3(Mathf.Cos(a) * foot, 0f, Mathf.Sin(a) * foot), 0.07f, co.tuftC);
                }
        }

        /// <summary>The pond: a flat sheet at the water level over the water cells and their neighbours; the ground above the
        /// water hides the sheet's square edge, so the shoreline is where the land meets the level.</summary>
        Mesh EcoWaterMesh()
        {
            var mb = new MeshBuilder(seed + 3) { jitter = 0f };
            var ec = layout.eco;
            var co = Co;
            if (ec.pondCells > 0)
            {
                float y = ec.waterY, h = Site.Cell * 0.5f + 0.003f;
                for (int c = 0; c < Site.N; c++)
                {
                    if (!ec.NearWater(c)) continue;
                    float x = Site.CXc(c), z = Site.CZc(c);
                    float depth = ec.waterY - land.Height(x, z);
                    var col = Color.Lerp(co.water, co.waterDeep, Mathf.Clamp01(depth / 0.12f));
                    QuadUp(mb, new Vector3(x - h, y, z - h), new Vector3(x - h, y, z + h), new Vector3(x + h, y, z + h), new Vector3(x + h, y, z - h), col);
                }
            }
            return mb.ToMesh("Water");
        }

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

        // ---------------- the water table in the soil cut (keepers-hands §5.2, #245) ----------------
        MeshFilter tableFilter;
        int tableKey = int.MinValue;

        /// <summary>The table's level as drawn, in steps of 2 mm of height (the band is rebuilt only when it moves a step).</summary>
        int TableKey => Eco ? Mathf.RoundToInt(layout.eco.tableY * 500f) : int.MinValue;

        /// <summary>The water table seen through the glass: below it the soil cut is wet and dark-blue, with a bright line where
        /// the water stands; it rises as the Keeper's mists soak down, and meets the ground where the pond surfaces.</summary>
        Mesh EcoTableMesh()
        {
            var mb = new MeshBuilder(seed + 5) { jitter = 0f };
            float HX = Land.HX, HZ = Land.HZ, y = layout.eco.tableY;
            var corners = new[] { new Vector2(-HX, -HZ), new Vector2(HX, -HZ), new Vector2(HX, HZ), new Vector2(-HX, HZ) };
            var wet = Color.Lerp(Palette.Loam, Co.waterDeep, 0.4f);
            var line = Color.Lerp(Co.water, Color.white, 0.25f);
            for (int side = 0; side < 4; side++)
            {
                var p0 = corners[side]; var p1 = corners[(side + 1) % 4];
                int segs = Mathf.RoundToInt(Vector2.Distance(p0, p1) / 0.0625f);
                var o = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized * 0.003f; // just proud of the soil cut
                for (int s = 0; s < segs; s++)
                {
                    var a = Vector2.Lerp(p0, p1, s / (float)segs); var b = Vector2.Lerp(p0, p1, (s + 1) / (float)segs);
                    float[] la = Layers(a), lb = Layers(b);
                    float ta = Mathf.Min(y, la[4] - 0.004f), tb = Mathf.Min(y, lb[4] - 0.004f);
                    float ba = la[1], bb = lb[1];
                    if (ta <= ba + 0.002f && tb <= bb + 0.002f) continue;
                    ta = Mathf.Max(ta, ba); tb = Mathf.Max(tb, bb);
                    Vector3 A(float h) => new Vector3(a.x, h, a.y) + o;
                    Vector3 B(float h) => new Vector3(b.x, h, b.y) + o;
                    mb.Quad(A(ba), A(Mathf.Max(ba, ta - 0.012f)), B(Mathf.Max(bb, tb - 0.012f)), B(bb), wet);
                    mb.Quad(A(Mathf.Max(ba, ta - 0.012f)), A(ta), B(tb), B(Mathf.Max(bb, tb - 0.012f)), line);
                }
            }
            return mb.ToMesh("WaterTable");
        }

        /// <summary>Called on every refresh: (re)draws the band when the living land is on and the table moved a step.</summary>
        void SyncEcoTable()
        {
            int key = TableKey;
            if (key == tableKey && (tableFilter != null || key == int.MinValue)) return;
            tableKey = key;
            if (tableFilter != null) { var oldm = tableFilter.sharedMesh; if (oldm != null) { if (Application.isPlaying) Destroy(oldm); else DestroyImmediate(oldm); } }
            if (!Eco) { if (tableFilter != null) { if (Application.isPlaying) Destroy(tableFilter.gameObject); else DestroyImmediate(tableFilter.gameObject); } tableFilter = null; return; }
            if (tableFilter == null) tableFilter = Spawn("WaterTable", EcoTableMesh(), Solid, false).GetComponent<MeshFilter>();
            else tableFilter.sharedMesh = EcoTableMesh();
        }
    }
}
