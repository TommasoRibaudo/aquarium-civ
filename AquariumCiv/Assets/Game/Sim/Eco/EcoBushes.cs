using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>What a bush carries (P8 #288, keepers-hands 2 beat 3).</summary>
    public enum BerryState { Bare, Flowers, Berries }

    /// <summary>
    /// The berry bushes of a living land, as pure queries over <see cref="EcoState"/> (no dice, no EcoModel). A bush is a cell
    /// whose cover is <see cref="EcoCover.Bush"/>; seedlings are not bushes yet. Berries follow the season (flowers in
    /// spring, berries in summer and autumn, bare in winter); a picked bush has fewer berries and recovers over some days.
    /// Seasons are ints, 0 spring .. 3 winter, as in the ecology and the planner's Season.
    /// </summary>
    public static class EcoBushes
    {
        /// <summary>Days a fully picked bush takes to carry a full crop again.</summary>
        public const float RecoverDays = 7f;
        /// <summary>The share of a bush's berries one basket takes.</summary>
        public const float PickAmount = 0.35f;
        /// <summary>Below this a bush isn't worth a trip (gatherers pass it by).</summary>
        public const float WorthPicking = 0.25f;

        public static BerryState StateIn(int season) => season == 0 ? BerryState.Flowers : season == 3 ? BerryState.Bare : BerryState.Berries;

        public static bool IsBush(EcoState e, int cell) => e != null && e.on && e.cover != null && cell >= 0 && cell < e.cover.Length && (EcoCover)e.cover[cell] == EcoCover.Bush;

        /// <summary>Every grown bush cell, in cell order.</summary>
        public static void GrownCells(EcoState e, List<int> into)
        {
            into.Clear();
            if (e == null || !e.on || e.cover == null) return;
            for (int c = 0; c < e.cover.Length; c++) if ((EcoCover)e.cover[c] == EcoCover.Bush) into.Add(c);
        }

        /// <summary>The bush's crop as a share of a full one, ignoring the season: 1 untouched, less after picking, back to 1.</summary>
        public static float Crop(EcoState e, int cell, long day)
        {
            int i = e.bushPickCell.IndexOf(cell);
            if (i < 0) return 1f;
            float left = e.bushPickLeft[i] + Math.Max(0L, day - e.bushPickDay[i]) / RecoverDays;
            return Math.Min(1f, left);
        }

        /// <summary>Berries on a bush on a view day in a season: 0 off the berry season or off a bush.</summary>
        public static float Berries(EcoState e, int cell, long day, int season)
            => IsBush(e, cell) && StateIn(season) == BerryState.Berries ? Crop(e, cell, day) : 0f;

        /// <summary>A basket is picked at a bush: the crop drops, then recovers. Returns the share actually taken.</summary>
        public static float Pick(EcoState e, int cell, long day, float amount = PickAmount)
        {
            if (!IsBush(e, cell)) return 0f;
            float cur = Crop(e, cell, day), took = Math.Min(cur, amount);
            int i = e.bushPickCell.IndexOf(cell);
            if (i < 0) { e.bushPickCell.Add(cell); e.bushPickDay.Add(day); e.bushPickLeft.Add(cur - took); }
            else { e.bushPickDay[i] = day; e.bushPickLeft[i] = cur - took; }
            return took;
        }

        /// <summary>The bush cells nearest (x, z), best first, those worth a trip only when <paramref name="berriesOnly"/>.</summary>
        public static void Nearest(EcoState e, float x, float z, int max, long day, int season, bool berriesOnly, List<int> into)
        {
            into.Clear();
            var all = new List<int>();
            GrownCells(e, all);
            var keep = new List<(float d, int c)>();
            foreach (var c in all)
            {
                if (berriesOnly && Berries(e, c, day, season) < WorthPicking) continue;
                float dx = AquariumCiv.Sim.Settle.SiteMap.CXc(c) - x, dz = AquariumCiv.Sim.Settle.SiteMap.CZc(c) - z;
                keep.Add((dx * dx + dz * dz, c));
            }
            keep.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.c.CompareTo(b.c));
            for (int i = 0; i < keep.Count && i < max; i++) into.Add(keep[i].c);
        }
    }
}
