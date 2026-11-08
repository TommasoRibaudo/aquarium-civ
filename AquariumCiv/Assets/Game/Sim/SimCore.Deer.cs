using System;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #420 the deer, the sim side the view and the choices read (docs/design/placed-life.md §4.1, §5.3). The Keeper places a stag and
    /// two does on the wood's edge (SimCore.LifeJar); the herd grows a fawn per doe in spring up to what the wood holds (1 per 3 grown
    /// trees, 3 to 8) and lives round the placed cell (<see cref="EcoDeerHome"/>, the far side while shy, #422). The people's deer hunt
    /// is planned in Life/LifePlanner.DeerHunt.cs and settled here (<see cref="DeerHuntCatch"/>): it never takes the herd below its
    /// floor of 2; a hit brings two hides to dry by the fire, and the first stag's antlers go up over the chief's tent. The white stag
    /// shows once per box (<see cref="WhiteStagSeen"/>); the choice itself is #427, its two answers' state is here
    /// (<see cref="ResolveWhiteStag"/>). With the ecology off none of this exists.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>A deer's hide is a big one: it counts as this many rabbit hides for the tents (#284).</summary>
        public const int DeerHides = 2;

        /// <summary>The white stag was seen: the choice "Hunt it, or let it be?" waits for the Keeper (#427).</summary>
        public event Action WhiteStagSeen;
        /// <summary>The first stag's antlers went up over the chief's tent (the view dresses the tent, #426).</summary>
        public event Action AntlersRaised;

        public int EcoDeerCount => EcologyOn ? EcoM.DeerCount : 0;
        public int EcoDeerStags => EcologyOn ? State.eco.deerStags : 0;
        public int EcoDeerDoes => EcologyOn ? State.eco.deerDoes : 0;
        /// <summary>Fawns born this spring (shown with their mothers until autumn).</summary>
        public int EcoDeerFawns => EcologyOn ? State.eco.deerFawns : 0;
        /// <summary>What the wood holds now (1 per 3 grown trees, 3 to 8).</summary>
        public int EcoDeerCap => EcologyOn ? EcoM.DeerCap : 0;
        /// <summary>How many deer dolls to show: the sim count, capped for the phone.</summary>
        public int EcoDeerShown(int cap = DeerHerd.DefaultCap) => DeerHerd.Shown(EcoDeerCount, cap);
        /// <summary>Where the herd lives now: the placed cell, or the far side of the wood while it is shy (#422). -1 with no deer.</summary>
        public int EcoDeerHome => EcologyOn && EcoDeerCount > 0 ? EcoM.HomeCell(LifeSpecies.Deer) : -1;
        /// <summary>The deer taken by the hunt so far.</summary>
        public int EcoDeerTaken => EcologyOn ? State.eco.deerTaken : 0;

        /// <summary>The first stag's antlers hang over the chief's tent (from the first stag the hunt brought home, for good).</summary>
        public bool DeerAntlersUp => EcologyOn && State.eco.antlersMonth >= 0;
        public long DeerAntlersMonth => EcologyOn ? State.eco.antlersMonth : -1;

        /// <summary>The white stag: none yet, seen (the choice waits), hunted (the great antlers) or let be (it shows at dawn).</summary>
        public WhiteStag WhiteStagState => EcologyOn ? (WhiteStag)State.eco.whiteStag : WhiteStag.None;
        /// <summary>The white stag's choice waits for the Keeper (#427 stages it).</summary>
        public bool WhiteStagChoiceDue => WhiteStagState == WhiteStag.Seen;
        /// <summary>The people let the white stag be: it shows at dawn on later visits (#427), and they tell of it.</summary>
        public bool WhiteStagAtDawn => WhiteStagState == WhiteStag.LetBe;
        /// <summary>The people hunted the white stag: its great antlers hang over the chief's tent.</summary>
        public bool WhiteStagAntlers => WhiteStagState == WhiteStag.Hunted;

        /// <summary>Rebuilds the view's <see cref="DeerHerd.Field"/> from the land as it is now (at start, each season, and when
        /// <see cref="EcoDeerHome"/> moves). The deer keep off the lots and the camp's green as the rabbits do.</summary>
        public void EcoDeerRefresh(DeerHerd.Field field)
        {
            if (!EcologyOn) return;
            field.Refresh(State.eco.cover, EcoGround, State.eco.seed, EcoDeerHome, State.eco.deerStags, EcoRabbitBlocked());
        }

        // ---------- the deer hunt (Life/LifePlanner.DeerHunt.cs plans it; this settles it) ----------
        /// <summary>How many deer the hunt may still take now: above the floor, never the last stag or doe, none in spring once the
        /// people have ruled on it (#422). 0 with the ecology off.</summary>
        public int DeerHuntSurplus => EcologyOn && EcoM.MayTake(LifeSpecies.Deer, LifeTaker.Hunters, State.month) ? EcoM.Surplus(LifeSpecies.Deer, LifeTaker.Hunters) : 0;

        /// <summary>A deer hunt's hit, settled in sim time: one deer leaves the herd (a stag while there are two, else a doe), two hides
        /// dry by the fire, and the first stag's antlers go up over the chief's tent. False, and nothing changes, when the herd has
        /// none to spare (the hunt then counts as a miss). <paramref name="stag"/>: it was a stag.</summary>
        public bool DeerHuntCatch(out bool stag)
        {
            stag = false;
            if (!EcologyOn || !EcoM.TryTake(LifeSpecies.Deer, LifeTaker.Hunters, State.month, out stag)) return false;
            State.eco.hidesDrying += DeerHides; State.eco.hidesTotal += DeerHides;
            if (stag && State.eco.antlersMonth < 0)
            {
                State.eco.antlersMonth = State.month;
                Chronicle("deer_antlers", "The hunters carried a stag home on a pole. That night the fire was bigger, and its antlers went up over the chief's tent.", 0.8f);
                AntlersRaised?.Invoke();
            }
            return true;
        }

        /// <summary>How many of the deer hunts of view day <paramref name="day"/> are already settled (saved: a reload never settles one twice).</summary>
        public int DeerHuntsSettled(long day) => EcologyOn && State.eco.deerHuntDay == day ? State.eco.deerHuntDone : 0;

        /// <summary>One more deer hunt of that day is settled. A new day starts the count again.</summary>
        public void DeerHuntSettled(long day)
        {
            if (!EcologyOn) return;
            if (State.eco.deerHuntDay != day) { State.eco.deerHuntDay = day; State.eco.deerHuntDone = 0; }
            State.eco.deerHuntDone++;
        }

        // ---------- the white stag (spec §5.3, D5; the choice is #427) ----------
        /// <summary>The Keeper's answer to the white stag: hunt it (a feast, the great antlers, two hides; the herd's count is untouched,
        /// the white stag was never one of it), or let it be (it shows at dawn on later visits, and the people tell of it). False unless
        /// the choice waits.</summary>
        public bool ResolveWhiteStag(bool hunt)
        {
            if (!WhiteStagChoiceDue) return false;
            var e = State.eco;
            if (hunt)
            {
                e.whiteStag = (int)WhiteStag.Hunted;
                e.hidesDrying += DeerHides; e.hidesTotal += DeerHides;
                // a "festival" line: the feast is staged as a held moment (MomentQueue)
                Chronicle("festival", "The hunters brought the white stag home. There was a feast that lasted into the night, and its great antlers went up over the chief's tent.", 0.85f);
            }
            else
            {
                e.whiteStag = (int)WhiteStag.LetBe;
                Chronicle("white_stag", "The hunters lowered their bows and let the white stag go. The people told of it at the fire for a long time after.", 0.85f);
            }
            return true;
        }

        /// <summary>"Let them decide": the people answer the white stag themselves (a hash of the box's seed, never the economy's dice).
        /// Returns whether they hunted it; false too when no choice waits.</summary>
        public bool WhiteStagLetThemDecide()
        {
            if (!WhiteStagChoiceDue) return false;
            bool hunt = Settle.Terrain.Hash3(State.seed * 31 + 420, 7, (int)(State.eco.whiteStagMonth & 0x7fffffff)) < 0.5f;
            ResolveWhiteStag(hunt);
            return hunt;
        }

        /// <summary>Raises what the placed life did this step: the white stag, the fletched arrows, the rulings (EcoTell calls this).</summary>
        void PlacedLifeTell()
        {
            var m = EcoM;
            if (m.newWhiteStag)
            {
                m.newWhiteStag = false;
                Chronicle("white_stag_seen", "At first light the hunters saw a white stag at the edge of the trees. Nobody spoke.", 0.75f);
                WhiteStagSeen?.Invoke();
            }
            PondTell();
            RulingsTell();
        }

        // ---------- debug (the panel's "Placed life" block; CLAUDE.md "debug panel stays current") ----------
        /// <summary>Debug ("white stag now"): places the deer if there are none, then the white stag shows now. False when it already has.</summary>
        public bool DebugWhiteStagNow()
        {
            if (!EcologyOn) return false;
            if (EcoDeerCount == 0 && DebugPlaceNearCamp(LifeSpecies.Deer) < 0) return false;
            if (!EcoM.RaiseWhiteStag(State.month)) return false;
            EcoTell();
            return true;
        }

        /// <summary>Debug ("a deer hunt now"): settles one deer hunt's hit now, as the planner's would (the floor and the spring rule
        /// hold). Places the deer first if there are none. True when a deer was taken.</summary>
        public bool DebugDeerHuntNow()
        {
            if (!EcologyOn) return false;
            if (EcoDeerCount == 0 && DebugPlaceNearCamp(LifeSpecies.Deer) < 0) return false;
            return DeerHuntCatch(out _);
        }

        /// <summary>Debug ("deer to capacity"): the herd fills to what the wood holds now (does first). Returns the count.</summary>
        public int DebugDeerToCap()
        {
            if (!EcologyOn) return 0;
            if (EcoDeerCount == 0 && DebugPlaceNearCamp(LifeSpecies.Deer) < 0) return 0;
            var e = State.eco;
            while (EcoM.DeerCount < EcoM.DeerCap) { if (e.deerDoes <= e.deerStags) e.deerDoes++; else e.deerStags++; }
            return EcoDeerCount;
        }
    }
}
