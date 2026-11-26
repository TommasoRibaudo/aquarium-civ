using System;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #709 (B71, decision 2026-11-15 "The bird from the bushes"): the nest in the thicket, seen from the outside. One nest, one parent bird in
    /// spring, eggs, a gatherer who takes one now and then (never the last), kids who look in. The cycle itself is EcoModel.Nest.cs; this is what the
    /// view, the planner, the chronicle and the debug panel read. The bird is outside <see cref="AnimalBudget"/> (Q4, <see cref="AnimalBudget.NestBirdSlots"/>).
    /// Off the ecology none of this exists. Everything here is [est] until the user answers B71's open questions.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Something happened at the nest (the kind, the bush cell). The view draws the parent, the twigs, the eggs and the scold from these.</summary>
        public event Action<NestEventKind, int> NestChanged;

        /// <summary>A nest stands (in a bush of a thicket).</summary>
        public bool NestStands => EcologyOn && State.eco.nest != null && State.eco.nest.built;
        /// <summary>The bush cell the nest sits in, or -1.</summary>
        public int NestCell => NestStands ? State.eco.nest.cell : -1;
        /// <summary>The centre cell of the nest's thicket, or -1.</summary>
        public int NestThicketCell => NestStands ? State.eco.nest.thicketCell : -1;
        public NestPhase NestPhaseNow => NestStands ? State.eco.nest.Phase : NestPhase.None;
        public int NestEggs => NestStands ? State.eco.nest.eggs : 0;
        public int NestChicks => NestStands ? State.eco.nest.chicks : 0;
        /// <summary>The parent bird is at the nest (spring only; the view sits it on the eggs, or has it fly the small circle).</summary>
        public bool NestParentPresent => NestStands && State.eco.nest.parent;
        public int NestEggsTakenThisSpring => NestStands ? State.eco.nest.eggsTaken : 0;
        /// <summary>How many times the kids have looked in, in all.</summary>
        public int NestLooks => NestStands ? State.eco.nest.looks : 0;
        /// <summary>The eggs a gatherer may take now (never the last; <see cref="EcoTuning.nestTakeMax"/> a spring). 0 = none.</summary>
        public int NestEggsSpare => NestStands ? EcoM.NestEggsSpare() : 0;
        /// <summary>The kids may look in: eggs are in the nest (B71.c).</summary>
        public bool NestKidsMayLook => NestStands && State.eco.nest.Phase == NestPhase.Eggs && State.eco.nest.eggs > 0;

        /// <summary>The nest's cycle for this month (SimCore.Eco's EcoStep calls it every month).</summary>
        void NestMonthStep(int cal)
        {
            var m = EcoM;
            m.NestBlocked = NestNearPath;
            m.NestMonth(cal, (int)((State.month + State.seasonShift) / 12));
            NestTell();
        }

        /// <summary>No nest in a bush with a path, a lot or the camp's ring within 0.3 u (B71: the nest sits quiet, off the walked ground).</summary>
        bool NestNearPath(int cell)
        {
            if (!Config.settle || Settlement?.Map == null) return false;
            var map = Settlement.Map;
            int reach = (int)Math.Ceiling(0.3f / SiteMap.Cell), i0 = SiteMap.I(cell), j0 = SiteMap.J(cell);
            for (int j = Math.Max(0, j0 - reach); j <= Math.Min(SiteMap.NZ - 1, j0 + reach); j++)
                for (int i = Math.Max(0, i0 - reach); i <= Math.Min(SiteMap.NX - 1, i0 + reach); i++)
                {
                    int c = SiteMap.Index(i, j);
                    if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(cell), SiteMap.CZc(cell)) > 0.3f) continue;
                    if (map.lot[c] != 0 || map.tier[c] >= PathNet.Trail) return true;
                }
            return false;
        }

        /// <summary>Raises what happened at the nest since last time, and tells the three heavy ones once each in the chronicle (seen, not read:
        /// the bird's first story leads on here, <c>eco_bird</c> then <c>eco_nest</c>).</summary>
        void NestTell()
        {
            var m = EcoM;
            if (m.newNestEvents.Count == 0) return;
            var evs = m.newNestEvents.ToArray();
            m.newNestEvents.Clear();
            var n = State.eco.nest;
            foreach (var e in evs)
            {
                if (e.kind == NestEventKind.Built && (n.told & 1) == 0)
                {
                    n.told |= 1;
                    Chronicle("eco_nest", "The bird came back to the bushes with twigs in its beak. It built a nest and sat in it. The children crept up to look.", 0.6f);
                }
                else if (e.kind == NestEventKind.EggTaken && (n.told & 2) == 0)
                {
                    n.told |= 2;
                    Chronicle("eco_nest_egg", "One of the gatherers took an egg from the nest and left the others. The bird flew at her and scolded.", 0.5f);
                }
                else if (e.kind == NestEventKind.Hatched && (n.told & 4) == 0)
                {
                    n.told |= 4;
                    Chronicle("eco_nest_chicks", "Small birds hatched in the nest, with their beaks open. The children watched them from far off.", 0.5f);
                }
                NestChanged?.Invoke(e.kind, e.cell);
            }
        }

        /// <summary>A gatherer takes an egg from the nest (B71.d; the planner's NestPlan settles it here at the pick): food +<see cref="EcoTuning.nestEggFood"/> at the
        /// stash. Never the last egg, never more than <see cref="EcoTuning.nestTakeMax"/> a spring; false and nothing changes otherwise. No hunting: nothing else ever takes from the bird.</summary>
        public bool TakeNestEgg()
        {
            if (!EcologyOn || !EcoM.TakeNestEgg()) { return false; }
            State.food = Math.Min(FoodCap, State.food + Math.Max(0f, Config.eco.nestEggFood));
            NestTell();
            return true;
        }

        /// <summary>The kids looked in at the nest (B71.c, noted by the planner when the look starts). Counts, and raises <see cref="NestEventKind.KidsLooked"/>.</summary>
        public void NoteNestLook()
        {
            if (!NestStands) return;
            State.eco.nest.looks++;
            NestChanged?.Invoke(NestEventKind.KidsLooked, State.eco.nest.cell);
        }

        /// <summary>Test hook: the nest's month for calendar month <paramref name="cal"/> of spring number <paramref name="year"/>, as EcoStep runs it.</summary>
        public void DebugNestMonth(int cal, int year)
        {
            if (!EcologyOn) return;
            EcoM.NestBlocked = NestNearPath;
            EcoM.NestMonth(cal, year);
            NestTell();
        }

        // ---------- debug (the panel's "Nest (#709)" block): straight to a stage, whatever the calendar says ----------
        /// <summary>Debug "Place the nest now": the first bird has come, a thicket stands (placed by the camp if none, with at least the nest's bushes), and a nest is built
        /// in it with the parent building. The bush cell, or -1 (no ecology, no ground).</summary>
        public int DebugNestPlace()
        {
            if (!EcologyOn) return -1;
            if (!LifeIsPlaced(LifeSpecies.Thicket) && DebugPlaceNearCamp(LifeSpecies.Thicket) < 0) return -1;
            int have = EcoM.ThicketList.Count > 0 ? EcoM.BushesIn(EcoM.ThicketList[0]) : 0;
            if (have < Config.eco.nestMinBushes) DebugAddBushes(Config.eco.nestMinBushes - have);
            if (!State.eco.Reached(EcoMilestones.FirstBird)) EcoM.Reach(EcoMilestones.FirstBird, State.month);
            EcoM.NestBlocked = NestNearPath;
            if (!EcoM.DebugNestSpring((int)((State.month + State.seasonShift) / 12))) return -1;
            NestTell();
            return NestCell;
        }

        /// <summary>Debug "Skip to spring": the nest at the start of its year (parent building, no eggs). Places it first if there is none.</summary>
        public int DebugNestSpring() => DebugNestPlace();

        /// <summary>Debug "Lay eggs": the eggs come now (places the nest first). The egg count, or 0.</summary>
        public int DebugNestLay()
        {
            if (!NestStands && DebugNestPlace() < 0) return 0;
            if (!EcoM.DebugNestLay()) return 0;
            NestTell();
            return NestEggs;
        }

        /// <summary>Debug "Hatch": the eggs hatch now (or, with chicks off, the eggs go). Lays them first if the nest has none. The chick count.</summary>
        public int DebugNestHatch()
        {
            if (NestEggs == 0 && DebugNestLay() == 0) return 0;
            EcoM.DebugNestHatch();
            NestTell();
            return NestChicks;
        }

        /// <summary>Debug "Chicks fly off" (#709 view): the end of spring now: the parent leaves with the chicks (hatching them first if the nest has
        /// eggs), the empty nest stays. True when someone flew off.</summary>
        public bool DebugNestFlyOff()
        {
            if (!NestStands) return false;
            if (NestChicks == 0 && NestEggs > 0) DebugNestHatch();
            if (!EcoM.DebugNestFlyOff()) return false;
            NestTell();
            return true;
        }

        /// <summary>Debug "Nest: next stage" (#709 view): none → built (the parent building) → eggs → chicks → flown off → a new spring. The stage now.</summary>
        public NestPhase DebugNestNextStage()
        {
            switch (NestPhaseNow)
            {
                case NestPhase.None: DebugNestPlace(); break;
                case NestPhase.Building: DebugNestLay(); break;
                case NestPhase.Eggs: DebugNestHatch(); break;
                case NestPhase.Chicks: DebugNestFlyOff(); break;
                default: DebugNestSpring(); break;
            }
            return NestPhaseNow;
        }

        /// <summary>Debug "Take an egg": the sim half of a gather, through the same rules (never the last). Lays eggs first if the nest has none.</summary>
        public bool DebugNestTakeEgg()
        {
            if (NestEggs == 0 && NestPhaseNow != NestPhase.Eggs && DebugNestLay() == 0) return false;
            return TakeNestEgg();
        }
    }
}
