using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #417 the life jar (docs/design/placed-life.md §2, §3, §4.3, §9): when the land can hold a species its jar opens (Ready),
    /// the Keeper places one founding group where they choose (Placed), and nothing big ever arrives by itself. Readiness is read
    /// from the step's own counts at the end of each season (and again after a placing, so the next rung can open at once);
    /// reached while away it just waits (<see cref="LifeJarEntry.readySeen"/> stays false for the view's wiggle). No dice.
    /// The settlement's ground (lots, paths, the green) is not known here: <see cref="CanPlace"/> takes it as a predicate.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>Jars that opened since SimCore last raised them (as <see cref="newMilestones"/>).</summary>
        public readonly List<LifeSpecies> newJars = new List<LifeSpecies>();

        /// <summary>One entry per species, in <see cref="LifeJar.All"/> order; a save from before the jar is migrated once
        /// (spec §9): rabbits that arrived count as placed, and so does the thicket when any bush stands (at the biggest old
        /// thicket's centre). Species added later are simply appended Locked.</summary>
        void EnsureJars()
        {
            st.jars ??= new List<LifeJarEntry>();
            foreach (var s in LifeJar.All)
                if (FindJar(s) == null) st.jars.Add(new LifeJarEntry { species = s.ToString() });
            if (st.jarOn) return;
            st.jarOn = true;
            AdoptPresent();
        }

        LifeJarEntry FindJar(LifeSpecies s)
        {
            string n = s.ToString();
            foreach (var e in st.jars) if (e.species == n) return e;
            return null;
        }

        /// <summary>The species' jar (never null on a running ecology).</summary>
        public LifeJarEntry Jar(LifeSpecies s) { var e = FindJar(s); if (e == null) { EnsureJars(); e = FindJar(s); } return e; }

        public JarState JarStateOf(LifeSpecies s) => Jar(s).State;
        /// <summary>At least one founding group of this species was placed (a restock's open jar still counts as placed once).</summary>
        public bool IsPlaced(LifeSpecies s) => Jar(s).placings > 0;

        /// <summary>A species already in the box counts as placed at its current spot (spec §9): an old save's rabbits and wild bushes.
        /// Runs once when an old save loads, and at each step (a no-op once nothing arrives by itself: #418, #419).</summary>
        void AdoptPresent()
        {
            if (st.rabbits > 0f && !IsPlaced(LifeSpecies.Rabbits))
            {
                var j = Jar(LifeSpecies.Rabbits);
                long m = st.MonthOf(EcoMilestones.FirstRabbit);
                MarkPlaced(j, st.rabbitArrivalCell, m < 0 ? 0 : m);
            }
            if (!IsPlaced(LifeSpecies.Thicket) && AnyBush())
            {
                int centre = BiggestOldThicket();
                if (centre >= 0)
                {
                    st.thicketA = centre; st.thicketB = -1; st.thicketRadius = t.thicketRadius; // #418: one thicket; the bushes outside it never spread
                    long m = st.MonthOf(EcoMilestones.FirstBush);
                    MarkPlaced(Jar(LifeSpecies.Thicket), centre, m < 0 ? 0 : m);
                }
            }
        }

        bool AnyBush() { for (int c = 0; c < EcoLand.N; c++) if (Cv(c) == EcoCover.Bush) return true; return false; }

        static void MarkPlaced(LifeJarEntry j, int cell, long month)
        {
            j.state = (int)JarState.Placed; j.placings = Math.Max(1, j.placings); j.placedCell = cell; j.placedMonth = month;
            j.readySeen = true; j.arrivalSeen = true;
        }

        /// <summary>The old save's thicket with the most bushes in it (#385's thicketA / thicketB at their old radii), or, with
        /// neither set, the bush nearest the camp pushed out from it, as #385 founded its first one.</summary>
        int BiggestOldThicket()
        {
            int a = st.thicketA, b = st.thicketB;
            if (a < 0 && b < 0)
            {
                int c = NearestBush(land.campX, land.campZ, -1, 0f);
                return c >= 0 ? PushedFromCamp(c) : -1;
            }
            if (a < 0) return b;
            if (b < 0) return a;
            int na = 0, nb2 = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (Cv(c) != EcoCover.Bush) continue;
                if (Zone(c, a, t.thicketR1, 1) > 0f) na++;
                if (Zone(c, b, t.thicketR2, 2) > 0f) nb2++;
            }
            return nb2 > na ? b : a;
        }

        // ---------------------------------------------------------------- readiness (spec §3)
        /// <summary>The land condition and the rung below (spec §3, §2.3), whatever the jar's state.</summary>
        public bool LandReady(LifeSpecies s, long month)
        {
            var below = LifeJar.RungBelow(s);
            if (below.HasValue && !IsPlaced(below.Value)) return false;
            switch (s)
            {
                case LifeSpecies.Thicket: return MeadowFrac >= t.jarThicketMeadow;
                case LifeSpecies.Rabbits: return MeadowFrac >= t.jarRabbitsMeadow;
                case LifeSpecies.Deer: return st.cTrees >= t.jarDeerTrees;      // the Keeper's own grown tree counts
                case LifeSpecies.Ducks: return st.cPond >= t.jarDucksPond;
                case LifeSpecies.Fish:
                {
                    var d = Jar(LifeSpecies.Ducks);
                    return d.placedMonth >= 0 && month - d.placedMonth >= t.jarFishAfterDucks && st.cReeds >= t.jarFishReeds;
                }
                // Wolves (deer >= 5 for a year, wood >= 20, a den spot >= 1.5 u from the fire): #437. Until then the jar stays shut.
                case LifeSpecies.Wolves: return false;
            }
            return false;
        }

        /// <summary>Opens the jars whose land is ready, and the restocks (spec §4.3). End of each season step, and after a placing.</summary>
        public void UpdateJars(long month)
        {
            AdoptPresent();
            foreach (var s in LifeJar.All)
            {
                var j = Jar(s);
                if (j.State == JarState.Locked)
                {
                    if (LandReady(s, month)) Open(j, s, month);
                }
                else if (j.State == JarState.Placed && LifeJar.Restocks(s))
                {
                    float pop = Population(s), floor = Floor(s);
                    if (pop < 0f) continue; // no population model yet (#420, #421)
                    if (pop > floor + 0.5f) { j.atFloorSince = -1; continue; }
                    if (j.atFloorSince < 0) j.atFloorSince = month;
                    else if (month - j.atFloorSince >= t.jarRestockMonths)
                    {
                        j.restocks++; j.atFloorSince = -1;
                        Open(j, s, month);
                    }
                }
            }
        }

        void Open(LifeJarEntry j, LifeSpecies s, long month)
        {
            j.state = (int)JarState.Ready; j.readyMonth = month; j.readySeen = false;
            newJars.Add(s);
        }

        /// <summary>The species' count the floor reads, or -1 while it has no population in the sim (deer, ducks, fish: #420, #421).</summary>
        public float Population(LifeSpecies s) => s == LifeSpecies.Rabbits ? st.rabbits : -1f;

        /// <summary>The floor no taker crosses (spec §4.1): the rabbits' is the hunt's (<see cref="SimCore.HuntFloor"/>).</summary>
        public static float Floor(LifeSpecies s) => s switch
        {
            LifeSpecies.Rabbits => SimCore.HuntFloor,
            LifeSpecies.Deer => 2f,
            LifeSpecies.Ducks => 2f,
            LifeSpecies.Fish => 3f,
            LifeSpecies.Wolves => 2f,
            _ => 0f,
        };

        // ---------------------------------------------------------------- valid ground (spec §5)
        /// <summary>Distance from a cell to the camp's fire.</summary>
        float FromFire(int c) => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), land.campX, land.campZ);

        static bool IsMeadow(EcoCover c) => c == EcoCover.Grass || c == EcoCover.Tall;

        /// <summary>Debug only (SimCore.DebugPlaceNearCamp on a barren box): bare ground counts as meadow for the thicket and the rabbits.</summary>
        public bool cheatBareGround;
        bool MeadowOk(EcoCover c) => IsMeadow(c) || (cheatBareGround && c == EcoCover.Bare);

        /// <summary>The species can be placed on this cell now: its jar is Ready and the ground is valid.</summary>
        public bool CanPlace(LifeSpecies s, int cell, Func<int, bool> settled = null)
            => JarStateOf(s) == JarState.Ready && GroundOk(s, cell, settled);

        /// <summary>The ground is valid for the species (spec §5), whatever the jar says. <paramref name="settled"/>: cells the
        /// settlement holds (lots, paths, the green, yards); null = none.</summary>
        public bool GroundOk(LifeSpecies s, int cell, Func<int, bool> settled = null)
        {
            if (cell < 0 || cell >= EcoLand.N) return false;
            var cv = Cv(cell);
            switch (s)
            {
                case LifeSpecies.Thicket:
                    return MeadowOk(cv) && FromFire(cell) >= t.bushCampMargin && ThicketDiscClear(cell, settled);
                case LifeSpecies.Rabbits:
                    return MeadowOk(cv) && FromFire(cell) >= t.bushCampMargin && !land.yard[cell] && !(settled?.Invoke(cell) ?? false);
                case LifeSpecies.Deer:
                    return cv != EcoCover.Water && !land.InBasin(cell) && FromFire(cell) >= t.bushCampMargin && NearGrownTree(cell, t.deerNearTree)
                        && !(settled?.Invoke(cell) ?? false);
                case LifeSpecies.Ducks:
                    return cv == EcoCover.Water && WaterNeighbours(cell) < EcoLand.Neighbours8(cell, nb);
                case LifeSpecies.Fish:
                    return cv == EcoCover.Water && WaterNeighbours(cell) == 8;
                case LifeSpecies.Wolves:
                    return false; // the den spot: #437
            }
            return false;
        }

        int WaterNeighbours(int c)
        {
            int n = EcoLand.Neighbours8(c, nb), w = 0;
            for (int k = 0; k < n; k++) if (Cv(nb[k]) == EcoCover.Water) w++;
            return w;
        }

        bool NearGrownTree(int c, float r)
        {
            float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
            int reach = (int)MathF.Ceiling(r / SiteMap.Cell);
            int i0 = SiteMap.I(c), j0 = SiteMap.J(c);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                {
                    int o = SiteMap.Index(i, j);
                    if (Cv(o) == EcoCover.Tree && Terrain.Dist(SiteMap.CXc(o), SiteMap.CZc(o), x, z) <= r + 1e-4f) return true;
                }
            return false;
        }

        /// <summary>The thicket's whole area round <paramref name="centre"/> is on the land (inside the glass), dry (no hollow, no
        /// pond) and clear of the settlement (spec §5.1).</summary>
        bool ThicketDiscClear(int centre, Func<int, bool> settled)
        {
            float R = t.thicketRadius * (1f + t.thicketEdge * 0.5f) + 1e-4f; // the ragged edge's widest reach
            float cx = SiteMap.CXc(centre), cz = SiteMap.CZc(centre);
            if (MathF.Abs(cx) > Terrain.HX - R || MathF.Abs(cz) > Terrain.HZ - R) return false;
            int reach = (int)MathF.Ceiling(R / SiteMap.Cell);
            int i0 = SiteMap.I(centre), j0 = SiteMap.J(centre);
            for (int j = j0 - reach; j <= j0 + reach; j++)
                for (int i = i0 - reach; i <= i0 + reach; i++)
                {
                    if (i < 0 || j < 0 || i >= SiteMap.NX || j >= SiteMap.NZ) return false;
                    int c = SiteMap.Index(i, j);
                    if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), cx, cz) > R) continue;
                    if (land.InBasin(c) || Cv(c) == EcoCover.Water || land.yard[c] || (settled?.Invoke(c) ?? false)) return false;
                }
            return true;
        }

        /// <summary>The valid cell nearest the camp's fire for the species, or -1 (ties by cell). The debug panel's "place here"
        /// and scripted Keepers use it.</summary>
        public int NearestPlaceCell(LifeSpecies s, Func<int, bool> settled = null)
        {
            int best = -1; float bd = float.MaxValue;
            for (int c = 0; c < EcoLand.N; c++)
            {
                float d = FromFire(c);
                if (d >= bd || !GroundOk(s, c, settled)) continue;
                bd = d; best = c;
            }
            return best;
        }

        // ---------------------------------------------------------------- placing (spec §6)
        /// <summary>Places the species' founding group at <paramref name="cell"/> (its jar must be Ready and the ground valid).
        /// Records the cell and month and starts the group in the land; the jar is Placed. False, and nothing changes, otherwise.</summary>
        public bool Place(LifeSpecies s, int cell, long month, Func<int, bool> settled, out bool restock)
        {
            restock = false;
            if (!CanPlace(s, cell, settled)) return false;
            var j = Jar(s);
            restock = j.placings > 0;
            j.state = (int)JarState.Placed; j.placings++; j.placedCell = cell; j.placedMonth = month;
            j.readySeen = true; j.arrivalSeen = false; j.atFloorSince = -1;
            Found(s, cell, month, restock);
            Count();
            UpdateJars(month);
            return true;
        }

        /// <summary>The founding group comes into the land. Thicket (#418) and rabbits (#419) live here; deer, ducks and fish only
        /// record their placing until their populations come (#420, #421).</summary>
        void Found(LifeSpecies s, int cell, long month, bool restock)
        {
            switch (s)
            {
                case LifeSpecies.Thicket: FoundThicket(cell, month); break; // #418
                case LifeSpecies.Rabbits: FoundRabbits(cell, month, restock); break; // #419
            }
        }

        /// <summary>The founding pair of rabbits that every placing brings (spec §4.1).</summary>
        public const float RabbitPair = 2f;

        /// <summary>#419 (spec §5.2): a pair hops in at the placed cell and the warren is dug there (RabbitHerd reads
        /// <see cref="EcoState.rabbitArrivalCell"/>); the tracks show from now on (D3: only after placing). A restock brings another
        /// pair to the same herd; the warren stays where it is.</summary>
        void FoundRabbits(int cell, long month, bool restock)
        {
            st.rabbits = MathF.Max(st.rabbits, 0f) + RabbitPair;
            if (restock && st.rabbitArrivalCell >= 0) return;
            st.rabbitArrivalCell = cell; st.rabbitArrivalSeen = false;
            Reach(EcoMilestones.RabbitTracks, month);
            Reach(EcoMilestones.FirstRabbit, month);
        }

        // ---------------------------------------------------------------- debug
        /// <summary>Debug ("ready every species"): every shut jar opens now, whatever the land says (the wolves too, though nothing
        /// can be placed for them before #437). How many opened.</summary>
        public int DebugReadyAll(long month)
        {
            int n = 0;
            foreach (var s in LifeJar.All) { var j = Jar(s); if (j.State == JarState.Locked) { Open(j, s, month); n++; } }
            return n;
        }

        /// <summary>Debug ("jar again"): a placed species' jar opens again now, as a restock would (any species that can restock).</summary>
        public bool DebugJarAgain(LifeSpecies s, long month)
        {
            var j = Jar(s);
            if (j.State != JarState.Placed || !LifeJar.Restocks(s)) return false;
            j.restocks++; j.atFloorSince = -1;
            Open(j, s, month);
            return true;
        }
    }
}
