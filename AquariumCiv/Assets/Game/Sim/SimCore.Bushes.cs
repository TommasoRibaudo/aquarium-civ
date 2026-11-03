using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// P8 #288 (keepers-hands 2 beat 3, 2.1): the berry bushes of a living land. The bushes are the cover's own Bush cells;
    /// what they carry follows the season and what the gatherers picked (<see cref="EcoBushes"/>, saved in EcoState). The
    /// first bush is a sign: the chronicle says so, <see cref="FirstBushSeen"/> fires once, and for a short while after
    /// <see cref="FirstBushSignActive"/> is true so the view (a bird lands on it, one points) and the planner (one walks to
    /// look) can show it. Off the ecology none of this exists.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The first bush came up, at this site cell. Raised once, from the season step (or the debug hook).</summary>
        public event Action<int> FirstBushSeen;

        /// <summary>Months the first bush stays "news": the people point at it and one goes to look.</summary>
        public const int FirstBushSignMonths = 1;

        /// <summary>The first bush's cell, or -1 (no ecology, or none yet).</summary>
        public int FirstBushCell => EcologyOn && State.eco.Reached(EcoMilestones.FirstBush) && EcoBushes.IsBush(State.eco, State.eco.firstBushCell)
            ? State.eco.firstBushCell : -1;

        /// <summary>The first bush is new: reached this game month or the one before.</summary>
        public bool FirstBushSignActive
        {
            get
            {
                if (!EcologyOn || FirstBushCell < 0) return false;
                long m = State.eco.MonthOf(EcoMilestones.FirstBush);
                return m >= 0 && State.month - m <= FirstBushSignMonths;
            }
        }

        /// <summary>Every grown bush cell (site-grid indices), in cell order. Empty off the ecology.</summary>
        public void BushCells(List<int> into) => EcoBushes.GrownCells(EcologyOn ? State.eco : null, into);

        /// <summary>The bush cells nearest a point, best first. <paramref name="berriesOnly"/>: only those with enough berries.</summary>
        public void NearestBushes(float x, float z, int max, long day, int season, bool berriesOnly, List<int> into)
            => EcoBushes.Nearest(EcologyOn ? State.eco : null, x, z, max, day, season, berriesOnly, into);

        /// <summary>What a bush carries in a season (0 spring .. 3 winter).</summary>
        public BerryState BerryStateOf(int season) => EcoBushes.StateIn(season);

        /// <summary>Berries on a bush (0..1 of a full crop) on a view day, in a season.</summary>
        public float BerriesAt(int cell, long day, int season) => EcologyOn ? EcoBushes.Berries(State.eco, cell, day, season) : 0f;

        /// <summary>A gatherer (or a child) picked at a bush on a view day: it has fewer berries for a while. Returns the share taken.</summary>
        public float PickBush(int cell, long day, float amount = EcoBushes.PickAmount) => EcologyOn ? EcoBushes.Pick(State.eco, cell, day, amount) : 0f;

        /// <summary>The bush nearest the camp's green, or -1: the first bush chosen when the land has none yet marked.</summary>
        int NearestBushToCamp()
        {
            var g = EcoGround;
            var near = new List<int>();
            EcoBushes.Nearest(State.eco, g.campX, g.campZ, 1, 0, 1, false, near);
            return near.Count > 0 ? near[0] : -1;
        }

        /// <summary>Called by <see cref="EcoTell"/> when the first bush milestone is raised.</summary>
        void FirstBushTell()
        {
            if (State.eco.firstBushCell < 0 || !EcoBushes.IsBush(State.eco, State.eco.firstBushCell)) State.eco.firstBushCell = NearestBushToCamp();
            Chronicle("eco_bush", "A bush stood up out of the grass, low and dark, with small flowers on it. The people came to look at it.", 0.7f);
            FirstBushSeen?.Invoke(State.eco.firstBushCell);
        }

        // ---------- debug hooks (the panel's "first bush now" and "+N bushes")
        /// <summary>Debug: the first bush now. Turns the grass nearest the camp into a bush if none stands, reaches the milestone.
        /// Returns the cell, or -1 (no ecology or no ground for it).</summary>
        public int DebugFirstBushNow()
        {
            if (!EcologyOn) return -1;
            if (NearestBushToCamp() < 0 && DebugAddBushes(1) == 0) return -1;
            if (EcoM.Reach(EcoMilestones.FirstBush, State.month)) EcoTell();
            return FirstBushCell;
        }

        /// <summary>Debug: +N bushes on the grass nearest the camp (never on water, trees or the pond). Returns how many came up.
        /// The first one also reaches the first-bush milestone.</summary>
        public int DebugAddBushes(int n)
        {
            if (!EcologyOn || n <= 0) return 0;
            var e = State.eco;
            var g = EcoGround;
            var order = new List<(float d, int c)>();
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = (EcoCover)e.cover[c];
                if (cv != EcoCover.Grass && cv != EcoCover.Tall && cv != EcoCover.Bare) continue;
                if (g.noBuild[c] || g.yard[c]) continue;
                float dx = SiteMap.CXc(c) - g.campX, dz = SiteMap.CZc(c) - g.campZ;
                order.Add((dx * dx + dz * dz, c));
            }
            order.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.c.CompareTo(b.c));
            int made = 0;
            // Spread them a little: every third cell of the nearest ground, so they read as bushes and not a hedge.
            for (int i = 0; i < order.Count && made < n; i += 3) { e.cover[order[i].c] = (byte)EcoCover.Bush; made++; }
            e.cBush += made;
            if (made > 0 && EcoM.Reach(EcoMilestones.FirstBush, State.month)) EcoTell();
            return made;
        }
    }
}
