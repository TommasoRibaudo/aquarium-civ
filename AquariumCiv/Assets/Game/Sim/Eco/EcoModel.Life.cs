using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>Who takes from a placed species (placed-life spec §4.2). Every taker respects the species' floor; the people's
    /// takes also pause in spring once a notable has ruled on them (#422). The wolves (#437) take within the floors but keep no
    /// people's rule.</summary>
    public enum LifeTaker { Hunters, EggGatherers, Fishers, Wolves }

    /// <summary>The white stag (spec §5.3, D5): once per box. Seen = the choice waits for the Keeper; then Hunted or LetBe.</summary>
    public enum WhiteStag { None = 0, Seen = 1, Hunted = 2, LetBe = 3 }

    /// <summary>
    /// #420-#422: the placed populations beyond the rabbits (docs/design/placed-life.md §4, §5.3-§5.5): the deer herd, the wild
    /// ducks and the fish, as counts with the few named individuals the view and the choices need. They come only from a placing
    /// (<see cref="Found"/>), grow to what the land holds in their season, and every take goes through <see cref="TryTake"/>, which
    /// never crosses a species' floor. No dice: every chance is a hash of (seed, kind, season).
    /// </summary>
    public sealed partial class EcoModel
    {
        // ---------------------------------------------------------------- the deer (#420, spec §5.3)
        /// <summary>The founding group (spec §4.1): a stag and two does.</summary>
        public const int DeerFoundStags = 1, DeerFoundDoes = 2;

        public int DeerCount => st.deerStags + st.deerDoes;

        /// <summary>What the wood holds (spec §4.1): one deer per 3 grown trees, at least 3, at most 8.</summary>
        public int DeerCap => Math.Max(t.deerMin, Math.Min(t.deerMax, st.cTrees / Math.Max(1, t.deerTreesPer)));

        /// <summary>The white stag was seen this step (SimCore raises it).</summary>
        public bool newWhiteStag;

        void FoundDeer(int cell, bool restock)
        {
            st.deerStags += DeerFoundStags; st.deerDoes += DeerFoundDoes;
            if (!restock || st.deerHome < 0) st.deerHome = cell; // a restock joins the same herd
        }

        /// <summary>A season of the herd: one fawn per doe in spring while under the cap (a hashed half are stags); the fawns are
        /// grown by autumn; over the cap (the wood thinned, or a restock joined a herd near it) the extra deer move off, never below the
        /// floor (the cap is at least 3).</summary>
        void DeerSeason(int s, long month)
        {
            if (DeerCount <= 0) return;
            int cap = DeerCap;
            if (s == 0)
            {
                int fawns = Math.Max(0, Math.Min(st.deerDoes, cap - DeerCount));
                for (int k = 0; k < fawns; k++)
                    if (Chance(41, k) < 0.5f) st.deerStags++; else st.deerDoes++;
                st.deerFawns = fawns;
            }
            else if (s == 2) st.deerFawns = 0;
            while (DeerCount > cap && DeerCount > Floor(LifeSpecies.Deer) && TakeDeer(out _)) { }
            WhiteStagCheck(month);
        }

        /// <summary>The white stag shows once per box (spec §5.3): a herd of at least <see cref="EcoTuning.whiteStagHerd"/>, two years
        /// after the first placing, then a hashed chance a season.</summary>
        void WhiteStagCheck(long month)
        {
            if (st.whiteStag != (int)WhiteStag.None || !IsPlaced(LifeSpecies.Deer)) return;
            long placed = Jar(LifeSpecies.Deer).placedMonth;
            if (placed < 0 || month - placed < t.whiteStagAfter || DeerCount < t.whiteStagHerd) return;
            if (Chance(42, 0) >= t.whiteStagChance) return;
            RaiseWhiteStag(month);
        }

        /// <summary>The white stag shows now (also the debug "white stag now"). False when it already has.</summary>
        public bool RaiseWhiteStag(long month)
        {
            if (st.whiteStag != (int)WhiteStag.None) return false;
            st.whiteStag = (int)WhiteStag.Seen; st.whiteStagMonth = month; newWhiteStag = true;
            return true;
        }

        /// <summary>One deer leaves the herd: a stag while there are two or more (the antlers), else a doe while there are two or
        /// more; never the last stag or the last doe. False when the herd is that small.</summary>
        bool TakeDeer(out bool stag)
        {
            stag = false;
            if (st.deerStags >= 2) { st.deerStags--; stag = true; return true; }
            if (st.deerDoes >= 2) { st.deerDoes--; return true; }
            return false;
        }

        /// <summary>#437: the wolves take an old doe while there are two or more does, else a spare stag (never the last stag or doe).</summary>
        bool TakeDeerForWolves(out bool stag)
        {
            stag = false;
            if (st.deerDoes >= 2) { st.deerDoes--; return true; }
            if (st.deerStags >= 2) { st.deerStags--; stag = true; return true; }
            return false;
        }

        // ---------------------------------------------------------------- the ducks (#421, spec §5.4)
        public const int DuckPair = 2;

        void FoundDucks(int cell, bool restock)
        {
            st.ducks += DuckPair;
            if (!restock || st.duckHome < 0) st.duckHome = cell;
        }

        /// <summary>A season of the wild flock: each pair lays a clutch in spring (3-5 eggs, hashed); at the summer step the eggs left
        /// in the nests hatch and about half the ducklings grow up, up to the wild cap; the feathers of the first summer's moult fletch
        /// the hunters' arrows (once). Pen ducks (#431) are never counted here.</summary>
        void DuckSeason(int s, long month)
        {
            if (st.ducks <= 0) return;
            if (st.ducks > t.duckCap) st.ducks = t.duckCap; // a restock over the cap: the extra fly on (the cap is above the floor)
            if (s == 0)
            {
                int eggs = 0, pairs = st.ducks / 2, span = Math.Max(0, t.duckClutchMax - t.duckClutchMin) + 1;
                for (int p = 0; p < pairs; p++) eggs += t.duckClutchMin + Math.Min(span - 1, (int)(Chance(43, p) * span));
                st.duckEggs = eggs; st.ducklings = 0; st.eggsTakenSpring = 0;
            }
            else if (s == 1)
            {
                if (st.eggsTakenSpring > 0 && st.duckEggs == 0) LeanSeason(LifeSpecies.Ducks, month); // #422: the people emptied the nests
                int grow = (int)MathF.Round(st.duckEggs * t.duckGrowShare);
                st.ducklings = st.duckEggs;
                if (st.ducks < t.duckCap) st.ducks = Math.Min(t.duckCap, st.ducks + grow);
                st.duckEggs = 0;
                if (st.fletchedMonth < 0) { st.fletchedMonth = month; newFletched = true; }
            }
            else if (s == 2) st.ducklings = 0;
        }

        /// <summary>The feathers fletched the arrows this step (SimCore tells it).</summary>
        public bool newFletched;

        /// <summary>The reeds grow first where the ducks were placed (spec §5.4): their chance there, as a factor.</summary>
        float DuckReedBoost(int c)
        {
            if (st.duckHome < 0 || st.duckHome >= EcoLand.N) return 1f;
            return Settle.Terrain.Dist(Settle.SiteMap.CXc(c), Settle.SiteMap.CZc(c), Settle.SiteMap.CXc(st.duckHome), Settle.SiteMap.CZc(st.duckHome)) <= t.duckReedNear
                ? t.duckReedBoost : 1f;
        }

        // ---------------------------------------------------------------- the fish (#421, spec §5.5)
        /// <summary>What the pond holds: about one fish per 3 pond cells, never less than the founding school.</summary>
        public int FishCap => Math.Max(t.fishSchool, st.cPond / Math.Max(1, t.fishPondPer));

        void FoundFish(int cell, bool restock)
        {
            st.fish += t.fishSchool;
            if (!restock || st.fishHome < 0) st.fishHome = cell;
        }

        /// <summary>A season of the stock: it grows toward the pond's cap within about a year (not in winter).</summary>
        void FishSeason(int s)
        {
            if (st.fish <= 0) return;
            int cap = FishCap;
            if (st.fish > cap) { st.fish = cap; return; } // a restock over the cap: the pond holds what it holds
            if (s == 3) return;
            if (st.fish < cap) st.fish = Math.Min(cap, st.fish + Math.Max(1, (int)MathF.Ceiling((cap - st.fish) * t.fishGrow)));
        }

        /// <summary>The placed populations' season (from <see cref="Animals"/>).</summary>
        void PlacedSeason(int s, long month)
        {
            KeepToTheWater(); // #519: first, so the season's ducks and fish live on water the Keeper can see
            DeerSeason(s, month);
            DuckSeason(s, month);
            FishSeason(s);
            WolfSeason(s, month); // #437: the pack's year and the wolves' jar clock
        }

        // ---------------------------------------------------------------- #519 never on water you can't see
        /// <summary>The flocks that left the box's water this step, for SimCore's history line (as <see cref="newRulings"/>).</summary>
        public readonly List<LifeSpecies> newLeftWater = new List<LifeSpecies>();

        /// <summary>#519: the ducks and the fish live on open water (<see cref="OpenWater"/>). A home that is not open water (the pond
        /// gave way, or an old save's flock was adopted with no pond) moves to the nearest of their ground, else to the nearest open
        /// water; with no open water in the box at all the flock is gone (a history line says so). The established pond never shrinks
        /// today (EcoModel.Water), so this is the guard, not the rule.</summary>
        public void KeepToTheWater()
        {
            if (st.ducks > 0 && !OpenWater(st.duckHome))
            {
                int c = WaterNear(LifeSpecies.Ducks, st.duckHome);
                if (c >= 0) st.duckHome = c;
                else { st.ducks = 0; st.duckEggs = 0; st.ducklings = 0; st.duckHome = -1; newLeftWater.Add(LifeSpecies.Ducks); }
            }
            if (st.fish > 0 && !OpenWater(st.fishHome))
            {
                int c = WaterNear(LifeSpecies.Fish, st.fishHome);
                if (c >= 0) st.fishHome = c;
                else { st.fish = 0; st.fishHome = -1; newLeftWater.Add(LifeSpecies.Fish); }
            }
        }

        /// <summary>The species' ground nearest <paramref name="from"/> (the fire when -1), else the nearest open water; -1 none.</summary>
        int WaterNear(LifeSpecies s, int from)
        {
            float fx = from >= 0 && from < EcoLand.N ? Settle.SiteMap.CXc(from) : land.campX;
            float fz = from >= 0 && from < EcoLand.N ? Settle.SiteMap.CZc(from) : land.campZ;
            int best = -1, wet = -1; float bd = float.MaxValue, wd = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (!OpenWater(c)) continue;
                float d = Settle.Terrain.Dist(Settle.SiteMap.CXc(c), Settle.SiteMap.CZc(c), fx, fz);
                if (d < wd) { wd = d; wet = c; }
                if (d < bd && GroundOk(s, c)) { bd = d; best = c; }
            }
            return best >= 0 ? best : wet;
        }

        // ---------------------------------------------------------------- takes (spec §4.2)
        /// <summary>How many the takers may still take now: the count above the floor (eggs: the eggs in the nests while the flock is
        /// above its floor). 0 for a species with nothing above its floor.</summary>
        public int Surplus(LifeSpecies s, LifeTaker who)
        {
            switch (s)
            {
                case LifeSpecies.Rabbits: return Math.Max(0, (int)MathF.Round(st.rabbits) - (int)Floor(s));
                case LifeSpecies.Deer:
                    // never the last stag or the last doe: above the floor of 2 there is always one of them to spare
                    return Math.Max(0, Math.Min(DeerCount - (int)Floor(s), Math.Max(0, st.deerStags - 1) + Math.Max(0, st.deerDoes - 1)));
                case LifeSpecies.Ducks:
                    if (who == LifeTaker.EggGatherers) return st.ducks > Floor(s) ? st.duckEggs : 0;
                    return Math.Max(0, st.ducks - (int)Floor(s));
                case LifeSpecies.Fish: return Math.Max(0, st.fish - (int)Floor(s));
            }
            return 0;
        }

        /// <summary>One take: a rabbit, a deer, an egg (never a duck, for the people), a wild duck (the wolves only), a fish. It never
        /// crosses the species' floor (spec §4.2: no species ever dies out). False, and nothing changes, when there is none to spare.</summary>
        public bool TryTake(LifeSpecies s, LifeTaker who, long month, out bool stag)
        {
            stag = false;
            if (!MayTake(s, who, month) || Surplus(s, who) <= 0) return false;
            bool took = Take(s, who, out stag);
            // #422: taken down to the floor this season (an egg never brings the flock down)
            if (took && !(s == LifeSpecies.Ducks && who == LifeTaker.EggGatherers) && Population(s) <= Floor(s) + 0.5f) Jar(s).floorHit = true;
            return took;
        }

        bool Take(LifeSpecies s, LifeTaker who, out bool stag)
        {
            stag = false;
            switch (s)
            {
                case LifeSpecies.Rabbits: st.rabbits = MathF.Max(0f, st.rabbits - 1f); st.rabbitsCaught++; return true; // as CatchRabbit always did
                case LifeSpecies.Deer:
                    if (!(who == LifeTaker.Wolves ? TakeDeerForWolves(out stag) : TakeDeer(out stag))) return false;
                    st.deerTaken++;
                    return true;
                case LifeSpecies.Ducks:
                    if (who == LifeTaker.EggGatherers) { st.duckEggs--; st.eggsTaken++; st.eggsTakenSpring++; return true; }
                    if (who != LifeTaker.Wolves) return false; // the people take eggs, never birds
                    st.ducks--; st.ducksTaken++;
                    return true;
                case LifeSpecies.Fish: st.fish--; st.fishCaught++; return true;
            }
            return false;
        }

        /// <summary>The taker may take from the species at all now (the floor aside): the right takers for it. #422 adds the spring
        /// pause after the ruling.</summary>
        public bool MayTake(LifeSpecies s, LifeTaker who, long month)
        {
            bool right = s switch
            {
                LifeSpecies.Rabbits or LifeSpecies.Deer => who == LifeTaker.Hunters || who == LifeTaker.Wolves,
                LifeSpecies.Ducks => who == LifeTaker.EggGatherers || who == LifeTaker.Wolves,
                LifeSpecies.Fish => who == LifeTaker.Fishers,
                _ => false,
            };
            return right && !SpringPaused(s, who, month);
        }

        /// <summary>The game season of a month as the land steps it (0 spring .. 3 winter; SimCore.EcoStep).</summary>
        public static int SeasonOf(long month) => (SeasonCalendar.OfMonth(month) / 3 + 3) % 4;

        /// <summary>#422: the people's take of the species pauses in spring once a notable has ruled on it (the wolves keep no rule).</summary>
        public bool SpringPaused(LifeSpecies s, LifeTaker who, long month) =>
            who != LifeTaker.Wolves && SeasonOf(month) == 0 && Jar(s).ruledMonth >= 0;
    }
}
