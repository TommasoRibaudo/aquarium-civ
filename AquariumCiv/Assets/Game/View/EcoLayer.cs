using System;
using AquariumCiv.Sim.Settle;
using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// The living land on show (P8, keepers-hands.md; the sim's ecology layer, #243): cover and moisture per
    /// <see cref="SiteMap"/> cell, the pond's surface, the water table and the Keeper's tree. A snapshot the
    /// <see cref="SimLayout"/> fills from the sim; <see cref="BoxView"/> reads it on its chunk workers (read-only there).
    /// Off (<see cref="on"/> false) the box draws its old static land.
    /// </summary>
    [Serializable]
    public class EcoLayer
    {
        // The sim's cover values (Sim/Eco, the lab's enum): kept as bytes so the view needs no sim types on its workers.
        public const byte Bare = 0, Grass = 1, Tall = 2, Bush = 3, Seedling = 4, Young = 5, Tree = 6, Reeds = 7, Water = 8;

        public bool on;
        public byte[] cover = new byte[SiteMap.N];
        public float[] moist = new float[SiteMap.N];
        /// <summary>The pond's surface height (only meaningful while <see cref="pondCells"/> &gt; 0).</summary>
        public float waterY;
        public int pondCells;
        /// <summary>The water table and the main hollow's spill level (the flood line), in ground height units.</summary>
        public float tableY, floodY;
        /// <summary>The Keeper's tree: its cell (-1 = not planted), stage 0–4 and the need it shows (0 none, 1 water, 2 light, 3 weeds).</summary>
        public int treeCell = -1, treeStage, treeNeed;
        public float humidity, droplets;
        /// <summary>#287: while the time-lapse blends two years, each cell's greenness already lerped (null live = Greenness of the cover).</summary>
        [NonSerialized] public float[] greenMix;

        public static bool Wooded(byte c) => c == Seedling || c == Young || c == Tree;

        public byte At(int c) => c >= 0 && c < SiteMap.N ? cover[c] : Bare;
        public byte At(float x, float z) => At(SiteMap.CellAt(x, z));
        public bool WaterCell(int c) => At(c) == Water;

        /// <summary>How green the ground reads under a cover, 0 (bare, cracked) to 1 (lush).</summary>
        public static float Greenness(byte c) => c switch
        {
            Grass => 0.75f, Tall => 0.9f, Bush => 0.85f, Seedling => 0.8f, Young => 0.9f, Tree => 0.95f, Reeds => 0.85f, Water => 0.6f,
            _ => 0f,
        };

        /// <summary>Greenness at a point, blended over the four nearest cells so a patch's edge is soft, not the grid's squares.</summary>
        public float GreenAt(float x, float z)
        {
            float fx = (x - SiteMap.X0) / SiteMap.Cell - 0.5f, fz = (z - SiteMap.Z0) / SiteMap.Cell - 0.5f;
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            float u = fx - i, v = fz - j;
            float G(int a, int b)
            {
                int c = SiteMap.Index(Mathf.Clamp(a, 0, SiteMap.NX - 1), Mathf.Clamp(b, 0, SiteMap.NZ - 1));
                return greenMix != null ? greenMix[c] : Greenness(At(c));
            }
            return Mathf.Lerp(Mathf.Lerp(G(i, j), G(i + 1, j), u), Mathf.Lerp(G(i, j + 1), G(i + 1, j + 1), u), v);
        }

        public float MoistAt(float x, float z)
        {
            int c = SiteMap.CellAt(x, z);
            return c >= 0 && c < SiteMap.N ? moist[c] : 0f;
        }

        /// <summary>A cell next to (or on) the pond: wet mud and reeds.</summary>
        public bool NearWater(int c)
        {
            if (pondCells <= 0 || c < 0) return false;
            if (WaterCell(c)) return true;
            int i = SiteMap.I(c), j = SiteMap.J(c);
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int a = i + di, b = j + dj;
                    if (a < 0 || b < 0 || a >= SiteMap.NX || b >= SiteMap.NZ) continue;
                    if (WaterCell(SiteMap.Index(a, b))) return true;
                }
            return false;
        }

        /// <summary>The cover of the cells inside a box, hashed (BoxView's chunks rebuild only when theirs changed).</summary>
        public uint HashRect(float x0, float x1, float z0, float z1)
        {
            uint h = 2166136261u;
            int i0 = Mathf.Clamp(Mathf.FloorToInt((x0 - SiteMap.X0) / SiteMap.Cell) - 1, 0, SiteMap.NX - 1);
            int i1 = Mathf.Clamp(Mathf.CeilToInt((x1 - SiteMap.X0) / SiteMap.Cell) + 1, 0, SiteMap.NX - 1);
            int j0 = Mathf.Clamp(Mathf.FloorToInt((z0 - SiteMap.Z0) / SiteMap.Cell) - 1, 0, SiteMap.NZ - 1);
            int j1 = Mathf.Clamp(Mathf.CeilToInt((z1 - SiteMap.Z0) / SiteMap.Cell) + 1, 0, SiteMap.NZ - 1);
            unchecked
            {
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int c = SiteMap.Index(i, j);
                        // Moisture in 4 steps only: the ground darkens when it's wet, without a rebuild per drop.
                        h = (h ^ (uint)(cover[c] * 5 + Mathf.Clamp((int)(moist[c] * 4f), 0, 4))) * 16777619u;
                    }
                if (pondCells > 0) h = (h ^ (uint)Mathf.RoundToInt(waterY * 400f)) * 16777619u;
                if (greenMix != null) for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) h = (h ^ (uint)Mathf.RoundToInt(greenMix[SiteMap.Index(i, j)] * 12f)) * 16777619u;
                h = (h ^ (uint)(treeCell + 7)) * 16777619u;
                h = (h ^ (uint)(treeStage * 4 + treeNeed)) * 16777619u;
            }
            return h;
        }

        /// <summary>The pond's own hash: the water mesh is rebuilt only when it rises or spreads.</summary>
        public uint WaterHash()
        {
            if (pondCells <= 0) return 0;
            uint h = (uint)Mathf.RoundToInt(waterY * 400f) * 2654435761u;
            unchecked { for (int c = 0; c < SiteMap.N; c++) if (cover[c] == Water) h = (h ^ (uint)c) * 16777619u; }
            return h;
        }

        public void CopyFrom(EcoLayer o)
        {
            on = o.on;
            Array.Copy(o.cover, cover, SiteMap.N);
            Array.Copy(o.moist, moist, SiteMap.N);
            waterY = o.waterY; pondCells = o.pondCells; tableY = o.tableY; floodY = o.floodY;
            treeCell = o.treeCell; treeStage = o.treeStage; treeNeed = o.treeNeed; humidity = o.humidity; droplets = o.droplets;
        }
    }
}
