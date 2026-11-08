using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #437 the wolf pack in the land (docs/design/placed-life.md §3, §4.1, §5.6; research 19 §3-§5). The jar opens when the deer have
    /// held at least 5 for a full game year, the wood has 20 grown trees, and there is a den spot (a grown tree inside the wood, the wood
    /// between it and the fire, at least 1.5 u from the fire). The Keeper places a pair at a den; pups come in spring (2-4), last spring's
    /// young leave at the box's edge in autumn, so the pack stays 2-6; one pack per box; the pair is the floor and never leaves, so the jar
    /// never restocks. A chase a month or so (more in winter), most fail; a kill takes an old doe or a fawn, a rabbit or a wild duck (a pen
    /// duck only while the pen is unguarded, #446), always through <see cref="TryTake"/>, so no prey ever goes below its floor. No dice:
    /// hashes of (seed, kind, month).
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>The kill log keeps this many (the newest).</summary>
        public const int WolfKillsKept = 8;
        public const int WolfPair = 2;

        /// <summary>A white wolf howled this step (SimCore tells it: a held moment and a book line).</summary>
        public bool newWhiteWolf;
        /// <summary>The kills of this step, for SimCore to tell (cleared there).</summary>
        public readonly List<WolfKill> newKills = new List<WolfKill>();

        public int WolfCount => st.wolves;
        public bool WolvesPlaced => st.wolves > 0 && st.wolfDen >= 0;

        // ---------------------------------------------------------------- the jar (spec §3)
        /// <summary>The herd's clock for the wolves' jar: the first season-end the deer held at least <see cref="EcoTuning.wolvesDeer"/>
        /// in a row (each season's end; a dip restarts it).</summary>
        void WolfJarClock(long month)
        {
            if (IsPlaced(LifeSpecies.Deer) && DeerCount >= t.wolvesDeer) { if (st.deerFiveSince < 0) st.deerFiveSince = month; }
            else st.deerFiveSince = -1;
        }

        /// <summary>Spec §3: deer placed (the rung, checked by the caller), the herd at 5 or more for a year, the wood of 20, a den spot.</summary>
        bool WolvesLandReady(long month) =>
            st.deerFiveSince >= 0 && month - st.deerFiveSince >= t.wolvesDeerMonths && st.cTrees >= t.wolvesTrees && AnyDen();

        bool AnyDen() { for (int c = 0; c < EcoLand.N; c++) if (DenOk(c, null)) return true; return false; }

        /// <summary>A den spot (spec §5.6): a grown tree inside the wood (at least <see cref="EcoTuning.denTrees"/> grown trees round it),
        /// at least <see cref="EcoTuning.denFromFire"/> from the fire, on the far side of the wood (a grown tree between it and the fire, within
        /// 0.4 u), off the hollow and the settlement, inside the glass.</summary>
        bool DenOk(int c, Func<int, bool> settled)
        {
            if (Cv(c) != EcoCover.Tree || land.InBasin(c) || FromFire(c) < t.denFromFire) return false;
            float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
            if (!WolfRules.OnLand(x, z)) return false;
            int n = EcoLand.Neighbours8(c, nb), trees = 0;
            for (int k = 0; k < n; k++) if (Cv(nb[k]) == EcoCover.Tree) trees++;
            if (trees < t.denTrees) return false;
            float dx = land.campX - x, dz = land.campZ - z, len = MathF.Sqrt(dx * dx + dz * dz);
            bool wood = false;
            for (float r = SiteMap.Cell; r <= 0.4f + 1e-4f && !wood; r += SiteMap.Cell * 0.5f)
            {
                int o = SiteMap.CellAt(x + dx / len * r, z + dz / len * r);
                if (o >= 0 && o != c && Cv(o) == EcoCover.Tree) wood = true;
            }
            return wood && !(settled?.Invoke(c) ?? false);
        }

        /// <summary>The pair comes to the den (the jar's placing). The wolves never restock: a placing only ever happens once.</summary>
        void FoundWolves(int cell)
        {
            st.wolves = Math.Max(st.wolves, 0) + WolfPair; st.wolfDen = cell;
            st.wolfBold = t.boldStart;
        }

        // ---------------------------------------------------------------- the pack's year (spec §4.1)
        /// <summary>Spring: last spring's pups are yearlings and this spring's come (2-4, never past the cap); autumn: the yearlings leave
        /// at the box's edge; winter, once, after the first pups: the white wolf. The pair never leaves.</summary>
        void WolfSeason(int s, long month)
        {
            WolfJarClock(month);
            if (!WolvesPlaced) return;
            if (s == 0)
            {
                st.wolfYearlings = st.wolfPups;
                int room = Math.Max(0, t.wolfCap - WolfPair - st.wolfYearlings), span = Math.Max(0, t.wolfPupsMax - t.wolfPupsMin) + 1;
                int pups = Math.Min(room, t.wolfPupsMin + Math.Min(span - 1, (int)(ChanceM(60, 0, month) * span)));
                st.wolfPups = pups;
                if (pups > 0 && st.wolfFirstPupsMonth < 0) st.wolfFirstPupsMonth = month;
            }
            else if (s == 2 && st.wolfYearlings > 0)
            {
                st.wolvesLeft += st.wolfYearlings; st.wolvesLeftLast = st.wolfYearlings; st.wolvesLeftMonth = month;
                st.wolfYearlings = 0;
            }
            else if (s == 3 && st.whiteWolfMonth < 0 && st.wolfFirstPupsMonth >= 0 && month > st.wolfFirstPupsMonth
                     && ChanceM(61, 0, month) < t.whiteWolfChance)
                RaiseWhiteWolf(month);
            st.wolves = WolfPair + st.wolfYearlings + st.wolfPups;
        }

        /// <summary>The white wolf howls on the ridge now (once per box; the debug "white wolf now" too). False when it already has.</summary>
        public bool RaiseWhiteWolf(long month)
        {
            if (st.whiteWolfMonth >= 0 || !WolvesPlaced) return false;
            st.whiteWolfMonth = month; newWhiteWolf = true;
            return true;
        }

        // ---------------------------------------------------------------- chases and kills (spec §5.6, B20)
        /// <summary>A month of the pack: a hashed chance of a chase (more in winter); a chase only when some prey has one to spare above its
        /// floor and a hidden spot to fall in; one chase in four ends in a kill. Returns the kill, or null.</summary>
        public WolfKill WolfMonth(int s, long month, Func<int, bool> blocked = null)
        {
            if (!WolvesPlaced) return null;
            float chance = t.wolfChase * (s == 3 ? t.wolfWinter : 1f);
            if (ChanceM(62, 0, month) >= chance) return null;
            return Chase(month, ChanceM(63, 0, month) < t.wolfCatch, ChanceM(64, 0, month), blocked);
        }

        /// <summary>One chase now: the prey by a hash among those with one to spare (deer first: an old doe or a fawn), where it would fall;
        /// a catch takes it through <see cref="TryTake"/> (the floor holds). The kill, or null for a miss (or no prey to chase).</summary>
        public WolfKill Chase(long month, bool catches, float roll, Func<int, bool> blocked = null)
        {
            if (!WolvesPlaced) return null;
            var prey = new List<(LifeSpecies s, bool pen, float w)>(4);
            if (Surplus(LifeSpecies.Deer, LifeTaker.Wolves) > 0) prey.Add((LifeSpecies.Deer, false, 0.55f));
            if (Surplus(LifeSpecies.Rabbits, LifeTaker.Wolves) > 0) prey.Add((LifeSpecies.Rabbits, false, 0.3f));
            if (Surplus(LifeSpecies.Ducks, LifeTaker.Wolves) > 0) prey.Add((LifeSpecies.Ducks, false, 0.15f));
            if (PenOpenToWolves && st.penDucks > (int)Floor(LifeSpecies.Ducks)) prey.Add((LifeSpecies.Ducks, true, 0.1f));
            if (prey.Count == 0) return null;
            float sum = 0f; foreach (var p in prey) sum += p.w;
            float r = roll * sum; int pick = prey.Count - 1;
            for (int k = 0; k < prey.Count; k++) { r -= prey[k].w; if (r < 0f) { pick = k; break; } }
            var (sp, pen, _) = prey[pick];
            int cell = KillCell(sp, pen, month, blocked);
            if (cell < 0) return null;
            st.wolfChases++; st.wolfChaseMonth = month; st.wolfChaseCaught = false;
            if (!catches) return null;
            bool fawn = sp == LifeSpecies.Deer && st.deerFawns > 0;
            if (pen) st.penDucks--;
            else if (!TryTake(sp, LifeTaker.Wolves, month, out _)) return null;
            if (fawn) st.deerFawns--;
            st.wolfChaseCaught = true; st.wolfKills++;
            var kill = new WolfKill { cell = cell, prey = (int)sp, month = month, fawn = fawn, pen = pen };
            st.wolfKillLog ??= new List<WolfKill>();
            st.wolfKillLog.Add(kill);
            while (st.wolfKillLog.Count > WolfKillsKept) st.wolfKillLog.RemoveAt(0);
            newKills.Add(kill);
            return kill;
        }

        /// <summary>Where the prey would fall: deer at the wood's edge round their home (beside a grown tree, so the kill is behind the
        /// trees), a rabbit in the grass round the warren, a duck at the pond's edge round its home; always at least
        /// <see cref="EcoTuning.wolfKillFromFire"/> from the fire, outside the settlement's yard and off <paramref name="blocked"/> (the
        /// lots and the tent ring, SimCore.WolfBlocked). Hashed among the candidates; -1 none.</summary>
        int KillCell(LifeSpecies s, bool pen, long month, Func<int, bool> blocked)
        {
            int home = s switch
            {
                LifeSpecies.Deer => HomeCell(LifeSpecies.Deer),
                LifeSpecies.Rabbits => st.rabbitArrivalCell,
                LifeSpecies.Ducks => st.duckHome,
                _ => -1,
            };
            if (home < 0) home = st.wolfDen;
            if (home < 0) return -1;
            float hx = SiteMap.CXc(home), hz = SiteMap.CZc(home);
            var cand = new List<int>();
            for (int c = 0; c < EcoLand.N; c++)
            {
                var cv = Cv(c);
                if (cv == EcoCover.Water || cv == EcoCover.Tree || land.yard[c] || FromFire(c) < t.wolfKillFromFire || (blocked?.Invoke(c) ?? false)) continue;
                if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), hx, hz) > 0.7f) continue;
                int n = EcoLand.Neighbours8(c, nb);
                bool cover = false, water = false;
                for (int k = 0; k < n; k++)
                {
                    var o = Cv(nb[k]);
                    if (o == EcoCover.Tree || o == EcoCover.Bush || o == EcoCover.Young) cover = true;
                    if (o == EcoCover.Water || o == EcoCover.Reeds) water = true;
                }
                if (s == LifeSpecies.Ducks ? water : s == LifeSpecies.Deer ? cover : (cover || cv == EcoCover.Tall)) cand.Add(c);
            }
            if (cand.Count == 0) return -1;
            return cand[(int)(ChanceM(65, (int)s, month) * cand.Count) % cand.Count];
        }

        /// <summary>The newest kill whose meat is still open (the ravens circle it), or null.</summary>
        public WolfKill OpenKill()
        {
            var log = st.wolfKillLog;
            if (log == null) return null;
            for (int k = log.Count - 1; k >= 0; k--) if (log[k].eatenBy == (int)KillEaten.Open) return log[k];
            return null;
        }

        /// <summary>#446: wolves may still take from the duck pen (built, not yet guarded).</summary>
        public bool PenOpenToWolves => st.duckPen && st.penGuard == (int)PenGuard.None;

        // ---------------------------------------------------------------- the human shield (research 19 §1)
        /// <summary>0..1: how far the deer have drawn toward the camp because of the pack (1 while the pack is in the box, 0 without).</summary>
        public float DeerShield => WolvesPlaced && DeerCount > 0 ? 1f : 0f;

        /// <summary>The deer's home with the pack about: their valid ground nearest the point <see cref="EcoTuning.deerShieldPull"/> of the
        /// way from the home to the fire (ties by cell); the home itself when none is nearer.</summary>
        int ShieldedDeerHome(int home)
        {
            float shield = DeerShield;
            if (home < 0 || shield <= 0f) return home;
            float hx = SiteMap.CXc(home), hz = SiteMap.CZc(home), f = t.deerShieldPull * shield;
            float tx = hx + (land.campX - hx) * f, tz = hz + (land.campZ - hz) * f;
            int best = home; float bd = Terrain.Dist(hx, hz, tx, tz);
            for (int c = 0; c < EcoLand.N; c++)
            {
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), tx, tz);
                if (d >= bd || !GroundOk(LifeSpecies.Deer, c)) continue;
                bd = d; best = c;
            }
            return best;
        }
    }
}
