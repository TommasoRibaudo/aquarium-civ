using System;
using System.Collections.Generic;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #709 (B71; decision 2026-11-15 "The bird from the bushes"): one nest in a placed thicket. The parent comes the first month of spring and
    /// builds, lays <see cref="EcoTuning.nestEggsMin"/> to <see cref="EcoTuning.nestEggsMax"/> eggs the next month, and at the end of spring either
    /// chicks hatch (<see cref="EcoTuning.nestChicks"/> above 0, Q5) or the eggs are simply gone. From summer the nest stands empty (it stays through
    /// the winter, <see cref="EcoTuning.nestWinterGone"/> aside). A gatherer may take an egg now and then (<see cref="TakeNestEgg"/>), never the last,
    /// never more than <see cref="EcoTuning.nestTakeMax"/> a spring. There is no hunting: the bird is never a target. Its own dice
    /// (a hash of seed and year, never the main streams); every number is [est]. The nest goes with its thicket (plucked or shrunk away) and
    /// comes back in the oldest thicket of 6 bushes or more next spring (Q6: <see cref="EcoTuning.nestWhichThicket"/>).
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>The calendar months: the parent comes (2), lays (3), the end of spring (4); summer starts at 5, winter at 11.</summary>
        public const int NestArriveMonth = 2, NestLayMonth = 3, NestEndMonth = 4, NestSummerMonth = 5, NestWinterMonth = 11;

        /// <summary>What happened at the nest since SimCore last drained it.</summary>
        public readonly List<NestEvent> newNestEvents = new List<NestEvent>();

        /// <summary>A cell the nest must not sit on (a path or lot nearby). SimCore sets it from the settlement. Null = none.</summary>
        public Func<int, bool> NestBlocked;

        Nest N => st.nest ?? (st.nest = new Nest());
        public Nest NestNow => N;

        float NestHash(int kind, int a, int b) => Terrain.Hash3(st.seed * 7919 + 9170 + kind, a, b);

        Thicket ThicketAtCell(int cell)
        {
            if (cell < 0) return null;
            foreach (var th in Ts) if (th.cell == cell) return th;
            return null;
        }

        /// <summary>The thicket the nest should be in this spring: the oldest with enough bushes (Q6 default), or the biggest (nestWhichThicket 1). Null = none.</summary>
        public Thicket NestThicketChoice()
        {
            Thicket best = null; int bestN = -1;
            foreach (var th in Ts)
            {
                int n = BushesIn(th);
                if (n < t.nestMinBushes) continue;
                if (t.nestWhichThicket == 0) return th; // the list is in the order they were placed
                if (n > bestN) { bestN = n; best = th; }
            }
            return best;
        }

        /// <summary>The bush for the nest in a thicket: the one nearest the camp that nobody is picking now and no path or lot is by; -1 = none.</summary>
        int NestBushIn(Thicket th)
        {
            CellsOf(th, thCells, thZones);
            int best = -1; float bestD = float.MaxValue;
            foreach (var c in thCells)
            {
                if (Cv(c) != EcoCover.Bush || (BushBusy?.Invoke(c) ?? false) || (NestBlocked?.Invoke(c) ?? false)) continue;
                float d = Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), land.campX, land.campZ);
                if (d < bestD - 1e-6f) { bestD = d; best = c; }
            }
            return best;
        }

        void NestRaise(NestEventKind k, int cell) => newNestEvents.Add(new NestEvent(k, cell));

        void NestRemove()
        {
            var n = N;
            if (!n.built) return;
            int cell = n.cell;
            n.built = false; n.thicketCell = -1; n.cell = -1; n.phase = (int)NestPhase.None; n.eggs = 0; n.chicks = 0; n.parent = false;
            NestRaise(NestEventKind.Gone, cell);
        }

        /// <summary>The nest still has its thicket and its bush; if only the bush went (shed, plucked) it moves to another in the same thicket,
        /// and when the thicket itself is gone the nest goes. Called every month, after a pluck, and after the thickets' season.</summary>
        public void NestCheck()
        {
            var n = N;
            if (!n.built) return;
            var th = ThicketAtCell(n.thicketCell);
            if (th == null || BushesIn(th) == 0) { NestRemove(); return; }
            if (Cv(n.cell) == EcoCover.Bush && ZoneIn(th, n.cell) > 0f) return;
            int to = NestBushIn(th);
            if (to < 0) { NestRemove(); return; }
            n.cell = to;
            NestRaise(NestEventKind.Moved, to);
        }

        /// <summary>Starts a cycle: in thicket <paramref name="th"/> (the nest keeps its bush when it stands in it, else takes the nearest one), the parent
        /// building, nothing laid. False when the thicket has no bush to give. Raises Built (the parent arrives).</summary>
        bool NestBegin(Thicket th, int year, NestPhase phase)
        {
            if (th == null) return false;
            var n = N;
            bool same = n.built && n.thicketCell == th.cell && Cv(n.cell) == EcoCover.Bush && ZoneIn(th, n.cell) > 0f;
            int bush = same ? n.cell : NestBushIn(th);
            if (bush < 0) return false;
            n.built = true; n.thicketCell = th.cell; n.cell = bush; n.year = year;
            n.phase = (int)phase; n.parent = true; n.eggs = 0; n.chicks = 0; n.eggsTaken = 0;
            NestRaise(NestEventKind.Built, bush); // every spring the parent comes and builds, in the same bush or a new one
            return true;
        }

        void NestLay()
        {
            var n = N;
            int span = Math.Max(0, t.nestEggsMax - t.nestEggsMin) + 1;
            n.eggs = t.nestEggsMin + Math.Min(span - 1, (int)(NestHash(1, n.cell, n.year) * span));
            n.phase = (int)NestPhase.Eggs;
            NestRaise(NestEventKind.Laid, n.cell);
        }

        void NestHatchOrEmpty()
        {
            var n = N;
            if (t.nestChicks > 0 && n.eggs > 0)
            {
                n.chicks = Math.Min(t.nestChicks, n.eggs); n.eggs = 0; n.phase = (int)NestPhase.Chicks;
                NestRaise(NestEventKind.Hatched, n.cell);
            }
            else { n.eggs = 0; n.phase = (int)NestPhase.Empty; n.parent = false; NestRaise(NestEventKind.ParentLeft, n.cell); }
        }

        void NestParentLeaves()
        {
            var n = N;
            n.eggs = 0; n.chicks = 0; n.phase = (int)NestPhase.Empty;
            if (n.parent) { n.parent = false; NestRaise(NestEventKind.ParentLeft, n.cell); }
        }

        /// <summary>The nest's month: <paramref name="cal"/> the calendar month (0..11, 2 = first of spring), <paramref name="year"/> the spring's number.
        /// Nothing starts before the first bird has come down (the milestone: the story leads into the nest). A thicket placed in the second month
        /// of spring gets a nest that month (the eggs just come), never in the last month of spring.</summary>
        public void NestMonth(int cal, int year)
        {
            var n = N;
            NestCheck();
            if ((cal == NestArriveMonth || cal == NestLayMonth) && n.year != year && st.Reached(EcoMilestones.FirstBird))
            {
                var keep = n.built ? ThicketAtCell(n.thicketCell) : null;
                if (keep != null && BushesIn(keep) >= t.nestMinBushes) NestBegin(keep, year, NestPhase.Building);
                else
                {
                    var pick = NestThicketChoice();
                    if (n.built && (pick == null || pick.cell != n.thicketCell)) { if (pick != null) { n.built = false; n.cell = -1; } else NestRemove(); }
                    NestBegin(pick, year, NestPhase.Building);
                }
            }
            if (cal == NestLayMonth && n.built && n.year == year && n.phase == (int)NestPhase.Building) NestLay();
            else if (cal == NestEndMonth && n.built && n.year == year && n.phase == (int)NestPhase.Eggs) NestHatchOrEmpty();
            else if (cal >= NestSummerMonth && n.built && (n.parent || n.phase != (int)NestPhase.Empty)) NestParentLeaves();
            if (cal == NestWinterMonth && t.nestWinterGone) NestRemove();
        }

        /// <summary>Eggs the people may take now: the nest has 2 or more, it is the egg season, and fewer than <see cref="EcoTuning.nestTakeMax"/> went this spring.
        /// The last egg is never taken: the spare eggs are the count less one.</summary>
        public int NestEggsSpare()
        {
            var n = N;
            if (!n.built || n.phase != (int)NestPhase.Eggs || n.eggsTaken >= t.nestTakeMax) return 0;
            return Math.Max(0, Math.Min(n.eggs - 1, t.nestTakeMax - n.eggsTaken));
        }

        /// <summary>A gatherer takes an egg. False, and nothing changes, when it would be the last or the spring's share is taken.</summary>
        public bool TakeNestEgg()
        {
            if (NestEggsSpare() <= 0) return false;
            var n = N;
            n.eggs--; n.eggsTaken++;
            NestRaise(NestEventKind.EggTaken, n.cell);
            return true;
        }

        // ---------- debug (the panel's Nest (#709) block): straight to a stage, whatever the calendar says ----------
        /// <summary>Debug: a nest in the thicket the model would choose (or the oldest thicket if none has enough bushes), the parent building. False without a thicket.</summary>
        public bool DebugNestSpring(int year)
        {
            var th = NestThicketChoice() ?? (Ts.Count > 0 ? Ts[0] : null);
            var n = N;
            if (n.built && n.thicketCell != th?.cell) n.built = false;
            return NestBegin(th, year, NestPhase.Building);
        }

        public bool DebugNestLay() { var n = N; if (!n.built || n.Phase == NestPhase.None || n.Phase == NestPhase.Empty) return false; n.parent = true; NestLay(); return true; }

        public bool DebugNestHatch() { var n = N; if (!n.built || n.eggs <= 0) return false; NestHatchOrEmpty(); return true; }
    }
}
