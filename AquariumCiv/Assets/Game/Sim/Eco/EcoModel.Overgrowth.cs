using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// What overgrows, and what the Keeper's tweezers take (#311, #314; keepers-hands 5.5): the weeds round the Keeper's tree, the
    /// forest's edge (wild seedlings and saplings that crept into the meadow, the yard and the paths) and the creeping vine. All of it is
    /// rule and hash dice: nothing here draws from the sim's random streams. The Keeper's tree, grown trees and the heart of a wood are
    /// never plucked.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>The weeds round the tree show as this many tufts; each pluck takes one.</summary>
        public const int TreeWeedSlots = 3;
        const float TreeWeedMin = 0.05f;
        static readonly int[] VDI = { 1, 1, 0, -1, -1, -1, 0, 1 }, VDJ = { 0, 1, 1, 1, 0, -1, -1, -1 };

        // ---------------------------------------------------------------- the forest's edge
        /// <summary>The grown trees round this cell (of its 8 neighbours).</summary>
        int TreeNeighbours(int c)
        {
            int n = EcoLand.Neighbours8(c, nb), trees = 0;
            for (int k = 0; k < n; k++) if (Cv(nb[k]) == EcoCover.Tree) trees++;
            return trees;
        }

        /// <summary>The heart of a wood: a young tree among grown ones is the forest's own, and stays.</summary>
        public bool InForestHeart(int c) => TreeNeighbours(c) >= t.heartTrees;

        /// <summary>A wild seedling or sapling the tweezers may take: never the Keeper's tree, never a grown tree, never the heart of a wood.</summary>
        public bool CanPluckSapling(int c)
        {
            if (c < 0 || c >= EcoLand.N || c == st.treeCell) return false;
            var cv = Cv(c);
            if (cv != EcoCover.Seedling && cv != EcoCover.Young) return false;
            return !InForestHeart(c);
        }

        /// <summary>Takes a wild seedling or sapling out (the ground under it is meadow again if it's damp enough, else bare).</summary>
        public bool PluckSapling(int c)
        {
            if (!CanPluckSapling(c)) return false;
            st.cover[c] = (byte)(st.moisture[c] >= t.grassAt ? EcoCover.Grass : EcoCover.Bare);
            Count();
            return true;
        }

        /// <summary>The nearest pluckable sapling within <paramref name="radius"/> (box units) of a point, or -1.</summary>
        public int NearestSapling(float x, float z, float radius)
        {
            int best = -1;
            float bestD = radius * radius;
            int span = (int)Math.Ceiling(radius / SiteMap.Cell) + 1;
            int c0 = SiteMap.CellAt(x, z);
            if (c0 < 0) return -1;
            int i0 = SiteMap.I(c0), j0 = SiteMap.J(c0);
            for (int j = Math.Max(0, j0 - span); j <= Math.Min(SiteMap.NZ - 1, j0 + span); j++)
                for (int i = Math.Max(0, i0 - span); i <= Math.Min(SiteMap.NX - 1, i0 + span); i++)
                {
                    int c = SiteMap.Index(i, j);
                    if (!CanPluckSapling(c)) continue;
                    float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z, d = dx * dx + dz * dz;
                    if (d <= bestD) { bestD = d; best = c; }
                }
            return best;
        }

        /// <summary>Debug: a few seedlings creep in near a grown tree (or the Keeper's tree, or the yard): into meadow, bare ground and the yard. Returns how many came.</summary>
        public int CreepEdge(int count)
        {
            int src = -1;
            for (int c = 0; c < EcoLand.N && src < 0; c++) if (Cv(c) == EcoCover.Tree && c != st.treeCell) src = c;
            if (src < 0) src = st.treeCell;
            if (src < 0) src = SiteMap.CellAt(land.campX, land.campZ);
            if (src < 0) return 0;
            int placed = 0;
            for (int k = 0; k < 400 && placed < count; k++)
            {
                float ang = Terrain.Hash3(st.seed + 61, k, 1) * MathF.PI * 2f, rr = 2f + Terrain.Hash3(st.seed + 62, k, 2) * 5f;
                int i = SiteMap.I(src) + (int)MathF.Round(MathF.Cos(ang) * rr), j = SiteMap.J(src) + (int)MathF.Round(MathF.Sin(ang) * rr);
                if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) continue;
                int c = SiteMap.Index(i, j);
                var cv = Cv(c);
                if (c == st.treeCell || land.InBasin(c) || (cv != EcoCover.Grass && cv != EcoCover.Tall && cv != EcoCover.Bare)) continue;
                st.cover[c] = (byte)EcoCover.Seedling; placed++;
            }
            if (placed > 0) Count();
            return placed;
        }

        // ---------------------------------------------------------------- weeds round the Keeper's tree
        /// <summary>The tufts of weed standing round the Keeper's tree (0 when it is clear or no tree stands).</summary>
        public int TreeWeedPieces => st.treeCell >= 0 && st.treeWeeds >= TreeWeedMin ? Math.Min(TreeWeedSlots, (int)MathF.Ceiling(st.treeWeeds * TreeWeedSlots - 1e-3f)) : 0;

        /// <summary>Plucks one tuft of the weeds round the tree.</summary>
        public bool PluckTreeWeed()
        {
            int n = TreeWeedPieces;
            if (n == 0) return false;
            st.treeWeeds = (n - 1) / (float)TreeWeedSlots;
            return true;
        }

        /// <summary>Debug: the weeds round the tree grow thick now. False when no tree stands.</summary>
        public bool GrowTreeWeeds()
        {
            if (st.treeCell < 0) return false;
            st.treeWeeds = 1f;
            return true;
        }

        // ---------------------------------------------------------------- the creeping vine (#314)
        bool VineOk(int c)
        {
            if (c < 0 || c >= EcoLand.N || c == st.treeCell || land.InBasin(c) || land.yard[c]) return false;
            var cv = Cv(c);
            return cv == EcoCover.Bare || cv == EcoCover.Grass || cv == EcoCover.Tall || cv == EcoCover.Bush;
        }

        bool Shaded(int c)
        {
            int n = EcoLand.Neighbours8(c, nb);
            for (int k = 0; k < n; k++) { var cv = Cv(nb[k]); if (cv == EcoCover.Tree || cv == EcoCover.Young || cv == EcoCover.Seedling) return true; }
            return false;
        }

        /// <summary>The direction a strand runs over this stretch of ground (0..7): one heading per block of cells, so strands run in lines.</summary>
        int VineDir(int c) => (int)(Terrain.Hash3(st.seed * 13 + 7, SiteMap.I(c) / 8, SiteMap.J(c) / 8) * 8f) & 7;

        static int NeighbourAt(int c, int dir)
        {
            int i = SiteMap.I(c) + VDI[dir & 7], j = SiteMap.J(c) + VDJ[dir & 7];
            return i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ ? -1 : SiteMap.Index(i, j);
        }

        /// <summary>Once a season, camp onwards: the vine creeps on from its tips along the ground, and now and then a new strand starts where
        /// the ground is damp and shaded by young trees. Slower than the weeds, never random: hash dice of (seed, cell, season).</summary>
        void Vines(int s)
        {
            if (st.campGateMonth < 0) return;
            st.vine ??= new List<int>();
            int n0 = st.vine.Count;
            for (int k = 0; k < n0 && st.vine.Count < t.vineCap; k++)
            {
                int c = st.vine[k];
                if (Chance(40, c, k) >= t.vineGrowChance) continue;
                int d = VineDir(c), f = NeighbourAt(c, d);
                if (f >= 0 && st.vine.Contains(f)) continue; // not the tip
                if (!VineOk(f)) { f = NeighbourAt(c, d + (Chance(43, c, k) < 0.5f ? 1 : -1)); if (f >= 0 && st.vine.Contains(f)) continue; }
                if (VineOk(f)) st.vine.Add(f);
            }
            if (st.vine.Count >= t.vineCap || Chance(41, 0) >= t.vineSeedChance) return;
            for (int k = 0; k < 24; k++)
            {
                int c = (int)(Chance(42, 0, k) * EcoLand.N) % EcoLand.N;
                if (VineOk(c) && st.moisture[c] >= t.vineDamp && Shaded(c) && !st.vine.Contains(c)) { st.vine.Add(c); break; }
            }
        }

        /// <summary>A strand on this cell (the view draws one piece per cell).</summary>
        public bool HasVine(int c) => st.vine != null && st.vine.Contains(c);

        /// <summary>Plucks the vine cell (a strand piece comes away).</summary>
        public bool PluckVine(int c) => st.vine != null && st.vine.Remove(c);

        /// <summary>Debug: lays a strand of up to <paramref name="len"/> cells from the cell at a point, running with the ground's heading. Returns the cells added.</summary>
        public int GrowVineAt(float x, float z, int len)
        {
            st.vine ??= new List<int>();
            int c = SiteMap.CellAt(x, z), added = 0;
            for (int k = 0; k < len && c >= 0; k++)
            {
                if (!VineOk(c)) break;
                if (!st.vine.Contains(c)) { st.vine.Add(c); added++; }
                c = NeighbourAt(c, VineDir(c));
            }
            return added;
        }
    }
}
