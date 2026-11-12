using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #417 the life jar, the sim side the view and the UI read (docs/design/placed-life.md §2, §6; the life slot is #423, the
    /// arrivals and the waiting hint #424). A jar opens when the land can hold its species (<see cref="LifeJarOpened"/>); the
    /// Keeper places one founding group on valid ground (<see cref="PlaceLife"/>, raising <see cref="LifePlaced"/> and a history
    /// line); nothing big arrives by itself. The view's "Held" state (the species in hand, a ghost shown) is never saved: it
    /// asks <see cref="CanPlaceLife"/> / <see cref="LifeGround"/> while the finger moves. With the ecology off none of this exists.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>A founding group was placed: the arrival plays at once (B12, #424). Raised after the history line.</summary>
        public event Action<LifePlacedInfo> LifePlaced;
        /// <summary>A jar opened (readiness reached, or a restock). Raised from the season step or after a placing; the wiggle
        /// itself waits on <see cref="JarWiggleDue"/>, which survives a time away.</summary>
        public event Action<LifeSpecies> LifeJarOpened;

        /// <summary>The jar's state (Locked when the ecology is off).</summary>
        public JarState JarStateOf(LifeSpecies s) => EcologyOn ? EcoM.JarStateOf(s) : JarState.Locked;
        public bool JarReady(LifeSpecies s) => JarStateOf(s) == JarState.Ready;
        /// <summary>At least one founding group of the species is in the box (a restock's open jar still counts).</summary>
        public bool LifeIsPlaced(LifeSpecies s) => EcologyOn && EcoM.IsPlaced(s);

        /// <summary>The ready jars, in <see cref="LifeJar.All"/> order (the chooser's row, spec §6). Empty off the ecology.</summary>
        public void ReadyJars(List<LifeSpecies> into)
        {
            into.Clear();
            if (!EcologyOn) return;
            foreach (var s in LifeJar.All) if (EcoM.JarStateOf(s) == JarState.Ready) into.Add(s);
        }

        /// <summary>The life slot shows (spec §6): at least one jar is Ready.</summary>
        public bool AnyJarReady { get { if (!EcologyOn) return false; foreach (var s in LifeJar.All) if (EcoM.JarStateOf(s) == JarState.Ready) return true; return false; } }

        /// <summary>#570 the shut jars, in <see cref="LifeJar.All"/> order (the chooser lists them greyed, after the ready ones).</summary>
        public void LockedJars(List<LifeSpecies> into)
        {
            into.Clear();
            if (!EcologyOn) return;
            foreach (var s in LifeJar.All) if (EcoM.JarStateOf(s) == JarState.Locked) into.Add(s);
        }

        /// <summary>#570 what a shut jar waits for, in the people's words ("Rabbits need a meadow"); null when it is not shut or its land is
        /// ready (it opens at the season's end).</summary>
        public string JarNeed(LifeSpecies s) => EcologyOn ? EcoM.JarNeed(s, State.month) : null;

        /// <summary>#570 the line the kit shows: what the camp, then the village, still waits for ("The village waits for: more trees (3/8)
        /// · fish in the pond"). Empty when the village stands, or every piece is there.</summary>
        public string VillageWaitsLine
        {
            get
            {
                if (!EcologyOn) return "";
                var pieces = new List<string>();
                EcoM.WaitsFor(pieces, out bool village);
                return pieces.Count == 0 ? "" : (village ? "The village waits for: " : "The camp waits for: ") + string.Join(" · ", pieces);
            }
        }

        /// <summary>The jar is Ready and its wiggle has not been shown yet (it opened since the last open, perhaps while away).</summary>
        public bool JarWiggleDue(LifeSpecies s) => JarReady(s) && !EcoM.Jar(s).readySeen;
        /// <summary>The view showed the wiggle. True the first time only; the dot stays until the jar is placed.</summary>
        public bool AckJarReady(LifeSpecies s)
        {
            if (!JarWiggleDue(s)) return false;
            EcoM.Jar(s).readySeen = true;
            return true;
        }

        /// <summary>The game month the jar last opened, or -1. The waiting hint grows with State.month minus this (spec §2.4).</summary>
        public long JarReadyMonth(LifeSpecies s) => EcologyOn ? EcoM.Jar(s).readyMonth : -1;
        /// <summary>The month and site cell of the last placing, or -1.</summary>
        public long LifePlacedMonth(LifeSpecies s) => EcologyOn ? EcoM.Jar(s).placedMonth : -1;
        public int LifePlacedCell(LifeSpecies s) => EcologyOn ? EcoM.Jar(s).placedCell : -1;
        /// <summary>How many restocks opened the jar again (spec §4.3).</summary>
        public int JarRestocks(LifeSpecies s) => EcologyOn ? EcoM.Jar(s).restocks : 0;

        /// <summary>The last placing's arrival has not been played yet (the view plays it, then <see cref="AckLifeArrival"/>).</summary>
        public bool LifeArrivalPending(LifeSpecies s) => EcologyOn && EcoM.Jar(s).placings > 0 && !EcoM.Jar(s).arrivalSeen;
        public bool AckLifeArrival(LifeSpecies s)
        {
            if (!LifeArrivalPending(s)) return false;
            EcoM.Jar(s).arrivalSeen = true;
            return true;
        }

        /// <summary>The species can be placed on the site cell now: its jar is Ready and the ground is valid (spec §5), off the
        /// settlement's lots, paths, green and yards.</summary>
        public bool CanPlaceLife(LifeSpecies s, int cell) => EcologyOn && EcoM.CanPlace(s, cell, SettledCell);

        /// <summary>The ground is valid for the species (spec §5), whatever its jar says: what glows while it is in hand.</summary>
        public bool LifeGroundOk(LifeSpecies s, int cell) => EcologyOn && EcoM.GroundOk(s, cell, SettledCell);

        /// <summary>Every site cell the species may be placed on (the glow while it is in hand, #423). Empty off the ecology.</summary>
        public void LifeGround(LifeSpecies s, List<int> into)
        {
            into.Clear();
            if (!EcologyOn) return;
            var m = EcoM;
            for (int c = 0; c < EcoLand.N; c++) if (m.GroundOk(s, c, SettledCell)) into.Add(c);
        }

        /// <summary>The valid cell nearest the camp's fire for the species, or -1.</summary>
        public int LifePlaceCellNearCamp(LifeSpecies s) => EcologyOn ? EcoM.NearestPlaceCell(s, SettledCell) : -1;

        /// <summary>The Keeper places the species' founding group on a site cell (spec §6: no undo). False, and nothing changes,
        /// when its jar isn't Ready or the ground isn't valid. Writes a history line and raises <see cref="LifePlaced"/>.</summary>
        public bool PlaceLife(LifeSpecies s, int cell)
        {
            if (!EcologyOn) return false;
            if (!EcoM.Place(s, cell, State.month, SettledCell, out bool restock)) return false;
            CountKeeperAction(KeeperAction.Place, false);
            if (Config.settle) Settlement.SyncEco(); // the thicket's ground is reserved at once (#418)
            Chronicle("life_" + s.ToString().ToLowerInvariant(), LifePlacedLine(s, restock), restock ? 0.6f : 0.85f);
            EcoTell();
            LifePlaced?.Invoke(new LifePlacedInfo { species = s, cell = cell, month = State.month, restock = restock });
            return true;
        }

        /// <summary>The history line, in the people's words (closed world: the gift comes from the sky).</summary>
        static string LifePlacedLine(LifeSpecies s, bool restock)
        {
            if (restock)
                return s switch
                {
                    LifeSpecies.Rabbits => "The rabbits had grown few. Then two more came down onto the grass, and the people let them be.",
                    _ => "There had been few of them for a long time. Then more came down from the sky, and the people were glad.",
                };
            return s switch
            {
                LifeSpecies.Thicket => "Low bushes came down from the sky onto the grass. The people walked round them and tasted the leaves.",
                LifeSpecies.Rabbits => "Two rabbits came down onto the grass and dug a hole. The people said the sky had sent game.",
                LifeSpecies.Deer => "Deer came down at the edge of the trees: a stag and two does. The people watched them without a sound.",
                LifeSpecies.Ducks => "Two ducks came down onto the water and swam about as if they had always lived there.",
                LifeSpecies.Fish => "Small dark shapes moved under the water. A child shouted that there were fish.",
                LifeSpecies.Wolves => "At dusk two grey shapes stood at the far edge of the wood. The people drew nearer the fire.",
                _ => "Something new came down from the sky.",
            };
        }

        /// <summary>A cell the settlement holds (a lot, a trail or more, a laid path, the green, a yard or commons, a family's ring):
        /// no founding group goes there. Nothing is held when the box isn't settled.</summary>
        bool SettledCell(int c)
        {
            var map = Config.settle ? Settlement?.Map : null;
            if (map == null || map.lot == null) return false;
            const byte block = SiteMap.FLaid | SiteMap.FGreen | SiteMap.FCommons | SiteMap.FYard;
            return map.lot[c] != 0 || map.tier[c] >= PathNet.Trail || (map.flags[c] & block) != 0 || (map.ring != null && map.ring[c] != 0);
        }

        /// <summary>Raises the jars that opened since the last time (EcoTell calls this).</summary>
        void JarsTell()
        {
            var m = EcoM;
            LeftWaterTell();
            if (m.newJars.Count == 0) return;
            var opened = m.newJars.ToArray();
            m.newJars.Clear();
            foreach (var s in opened) LifeJarOpened?.Invoke(s);
        }

        /// <summary>#519: open water the Keeper can see (the cells the view draws the pond's sheet over). The ducks and the fish live
        /// only here. False off the ecology.</summary>
        public bool EcoOpenWater(int cell) => EcologyOn && EcoM.OpenWater(cell);

        /// <summary>#519: a flock with no open water left in the box is gone; the history says so, in the people's words.</summary>
        void LeftWaterTell()
        {
            var m = EcoM;
            if (m.newLeftWater.Count == 0) return;
            var gone = m.newLeftWater.ToArray();
            m.newLeftWater.Clear();
            foreach (var s in gone)
                Chronicle("life_left_" + s.ToString().ToLowerInvariant(), s == LifeSpecies.Ducks
                    ? "The ducks had no water left to swim on. One morning they were gone, and only a few feathers lay in the dry hollow."
                    : "The water was gone from the hollow, and the fish with it. The children looked for them in the mud.", 0.6f);
        }

        // ---------- debug (the panel's "Life jar" block; CLAUDE.md "debug panel stays current") ----------
        /// <summary>Debug (#570 "Lock all jars"): every jar that is not placed shuts again, to see the greyed list. It opens again at the next
        /// season's end if its land is ready. How many shut.</summary>
        public int DebugLockJars()
        {
            if (!EcologyOn) return 0;
            int n = 0;
            foreach (var s in LifeJar.All) { var j = EcoM.Jar(s); if (j.State == JarState.Ready) { j.state = (int)JarState.Locked; n++; } }
            return n;
        }

        /// <summary>Debug ("ready every species"): every shut jar opens now, whatever the land says. How many opened.</summary>
        public int DebugJarsReady()
        {
            if (!EcologyOn) return 0;
            int n = EcoM.DebugReadyAll(State.month);
            EcoTell();
            return n;
        }

        /// <summary>Debug ("place X here"): opens the species' jar if it is shut and places its group at the valid cell nearest the
        /// camp's fire, through the same path as the Keeper's tap. On a box with no meadow yet, the thicket and the rabbits take bare
        /// ground (so the bush and rabbit cheats work on a barren box). The cell, or -1 (no valid ground).</summary>
        public int DebugPlaceNearCamp(LifeSpecies s)
        {
            if (!EcologyOn) return -1;
            var m = EcoM;
            try
            {
                int c = LifePlaceCellNearCamp(s);
                if (c < 0) { m.cheatBareGround = true; c = LifePlaceCellNearCamp(s); }
                if (c < 0) return -1;
                var j = m.Jar(s);
                if (j.State != JarState.Ready) { j.state = (int)JarState.Ready; j.readyMonth = State.month; j.readySeen = true; }
                return PlaceLife(s, c) ? c : -1;
            }
            finally { m.cheatBareGround = false; }
        }

        /// <summary>#519 debug ("pond dries"): every pond cell is bare ground now, and the ducks and the fish keep to the water at once
        /// (none left: they go, with their history line). Tester feedback 3: the water table, its peak and the pond's level go back down
        /// to the barren start too (undoing a "table" cheat), so the hollow stays dry until mist soaks down and raises the table again
        /// by the usual rules. Cells dried.</summary>
        public int DebugPondDries()
        {
            if (!EcologyOn) return 0;
            var e = State.eco; int n = 0;
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)e.cover[c] == EcoCover.Water) { e.cover[c] = (byte)EcoCover.Bare; n++; }
            float low = EcoM.land.minHeight - EcoM.t.tableStartBelow;
            e.table = Math.Min(e.table, low); e.tablePeak = Math.Min(e.tablePeak, low); e.pondLevel = Math.Min(e.pondLevel, low);
            EcoM.Count();
            EcoM.KeepToTheWater();
            EcoTell();
            return n;
        }

        /// <summary>Debug ("jar again"): a placed species' jar opens again now, as a restock does. False for the thicket and the
        /// wolves (they never restock) or a species not placed.</summary>
        public bool DebugJarAgain(LifeSpecies s)
        {
            if (!EcologyOn || !EcoM.DebugJarAgain(s, State.month)) return false;
            EcoTell();
            return true;
        }
    }
}
