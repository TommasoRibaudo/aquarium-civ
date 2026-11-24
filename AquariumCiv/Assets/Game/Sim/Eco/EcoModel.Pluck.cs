using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>What the landscaping tool can take out of a cell (#730).</summary>
    public enum PluckKind { None, Sapling, Bush, Tree }

    /// <summary>
    /// #730 phase 1 (docs/design/landscaping.md 5, 8.3; decision 2026-11-14 "one tool that plucks trees and bushes"): the sim side of the
    /// one landscaping tool (its name is the user's, still open: <c>[est]</c>). It takes wild seedlings and young trees (the pluck that
    /// was the tweezers', #311: never the Keeper's tree, never the heart of a wood), berry bushes, and, only when
    /// <see cref="EcoTuning.landscapePluckGrown"/> is on (OFF: the user has not said), grown forest trees. The tweezers keep the weeds
    /// round the Keeper's tree, the creeping vine and the overgrowth cloth; they no longer take saplings. The ground left is meadow if
    /// damp, else bare (today's rule). A plucked thicket bush lowers that thicket's own target by one (<see cref="Thicket.trim"/>), so it
    /// does not grow back; the last bush of a thicket takes the thicket with it (the list and the planner's reserve shrink, the others'
    /// targets rise, #729). No dice, no undo.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>The landscaping tool may take grown forest trees (a tuning flag, off by default).</summary>
        public bool PluckGrownTrees => t.landscapePluckGrown;

        /// <summary>What the tool would take from this cell, or <see cref="PluckKind.None"/>: never the Keeper's tree.</summary>
        public PluckKind PluckKindAt(int c)
        {
            if (c < 0 || c >= EcoLand.N || c == st.treeCell) return PluckKind.None;
            switch (Cv(c))
            {
                case EcoCover.Seedling: case EcoCover.Young: return CanPluckSapling(c) ? PluckKind.Sapling : PluckKind.None;
                case EcoCover.Bush: return PluckKind.Bush;
                case EcoCover.Tree: return t.landscapePluckGrown ? PluckKind.Tree : PluckKind.None;
            }
            return PluckKind.None;
        }

        public bool CanPluck(int c) => PluckKindAt(c) != PluckKind.None;

        /// <summary>The nearest cell the tool could take within <paramref name="radius"/> (box units) of a point, or -1 (ties go to the later cell, as
        /// the tweezers' sapling pick did). The view shows a ghost on it before the lift.</summary>
        public int NearestPluckable(float x, float z, float radius)
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
                    if (!CanPluck(c)) continue;
                    float dx = SiteMap.CXc(c) - x, dz = SiteMap.CZc(c) - z, d = dx * dx + dz * dz;
                    if (d <= bestD) { bestD = d; best = c; }
                }
            return best;
        }

        /// <summary>Takes what is on the cell out. The kind taken, or <see cref="PluckKind.None"/> (nothing changes).</summary>
        public PluckKind Pluck(int c)
        {
            var kind = PluckKindAt(c);
            switch (kind)
            {
                case PluckKind.Sapling: PluckSapling(c); break;
                case PluckKind.Tree: LeaveGround(c); Count(); break;
                case PluckKind.Bush: PluckBush(c); break;
            }
            return kind;
        }

        /// <summary>The ground under a plucked plant: meadow when it is damp enough, else bare.</summary>
        void LeaveGround(int c) => st.cover[c] = (byte)(st.moisture[c] >= t.grassAt ? EcoCover.Grass : EcoCover.Bare);

        /// <summary>A berry bush comes out. In a thicket it lowers that thicket's target by one; the thicket's last bush takes the thicket with it.</summary>
        void PluckBush(int c)
        {
            var ts = Ts;
            int at = ThicketAt(c);
            LeaveGround(c);
            if (at >= 0)
            {
                var th = ts[at];
                th.trim++;
                if (BushesIn(th) == 0) { ts.RemoveAt(at); SyncFirstThicket(); }
                else if (at == 0) SyncFirstThicket();
            }
            NestCheck(); // #709: a plucked nest bush moves the nest to another bush of its thicket, or it goes with the thicket
            Count();
        }

        /// <summary>Debug ("pluck all in an area"): everything the tool could take within <paramref name="radius"/> of a point comes out now.
        /// Returns what was taken as (saplings, bushes, trees).</summary>
        public (int saplings, int bushes, int trees) DebugPluckArea(float x, float z, float radius)
        {
            int s = 0, b = 0, tr = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), x, z) > radius) continue;
                switch (Pluck(c)) { case PluckKind.Sapling: s++; break; case PluckKind.Bush: b++; break; case PluckKind.Tree: tr++; break; }
            }
            return (s, b, tr);
        }
    }
}
