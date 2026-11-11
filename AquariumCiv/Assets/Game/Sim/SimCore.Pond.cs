using System;
using AquariumCiv.Sim.Eco;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #421 the ducks and the fish as placed stocks, the sim side the view reads (docs/design/placed-life.md §4.1, §5.4, §5.5).
    /// <list type="bullet">
    /// <item><b>Ducks:</b> the Keeper places a pair at the pond's edge; each pair lays a clutch in spring (3-5 eggs), and the eggs left
    /// in the nests hatch at the summer step, about half growing up, up to a wild flock of 8. The people take eggs, never birds, and
    /// only while the flock is above its floor of 2 (<see cref="TakeEgg"/>). The first summer's feathers fletch the hunters' arrows,
    /// once (<see cref="ArrowsFletched"/>). Pen ducks are #431 and never counted here: <see cref="EcoDucks"/> is the wild flock.</item>
    /// <item><b>Fish:</b> the Keeper places a school of 6 in the deeper water; it grows toward the pond's cap (1 per 3 pond cells)
    /// within a year. <b>No fishing of any kind until fish are placed</b> (<see cref="FishingOpen"/>: the fisher's job, the camp's
    /// fishing work and pastime, the night fishers and the debug bite all read it); a catch (<see cref="FishCatch"/>) never takes the
    /// stock below its floor of 3, and bites come slower near it (<see cref="FishBiteRate"/>).</item>
    /// <item><b>The village</b> (spec §3.1) also waits on the ducks and the fish placed (EcoModel.Gates).</item>
    /// </list>
    /// With the ecology off the old always-there pond: fishing is open, and there are no stocks.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The most fish shadows the view shows (spec §4.1).</summary>
        public const int FishShownCap = 8;
        /// <summary>The most wild ducks the view shows: the wild cap.</summary>
        public const int DucksShownCap = 8;

        /// <summary>The feathers fletched the hunters' arrows (the view shows fletching on their backs from now on).</summary>
        public event Action ArrowsFletched;

        // ---------- the ducks ----------
        /// <summary>The wild flock (pen ducks, #431, are apart). 0 before the Keeper places the pair; with the ecology off too.</summary>
        public int EcoDucks => EcologyOn ? State.eco.ducks : 0;
        /// <summary>How many ducks to show: the wild flock, capped (#428: PondLife reads this instead of the pond's size).</summary>
        public int EcoDucksShown(int cap = DucksShownCap) => Math.Max(0, Math.Min(cap, EcoDucks));
        /// <summary>Where the flock lives: the placed edge cell, or the pond's far edge while it is shy (#422). -1 with no ducks.</summary>
        public int EcoDuckHome => EcologyOn && EcoDucks > 0 ? EcoM.HomeCell(LifeSpecies.Ducks) : -1;
        /// <summary>Eggs in the nests now (laid at the spring step, hatched at the summer step).</summary>
        public int EcoDuckEggs => EcologyOn ? State.eco.duckEggs : 0;
        /// <summary>The ducklings of the last hatch: shown in a line behind their mother through the summer (#428).</summary>
        public int EcoDucklings => EcologyOn ? State.eco.ducklings : 0;
        public int EcoEggsTaken => EcologyOn ? State.eco.eggsTaken : 0;
        /// <summary>The eggs the gatherers may take now: the nests' eggs while the flock is above its floor, none in spring once the
        /// people have ruled on it (#422).</summary>
        public int EggSurplus => EcologyOn && EcoM.MayTake(LifeSpecies.Ducks, LifeTaker.EggGatherers, State.month) ? EcoM.Surplus(LifeSpecies.Ducks, LifeTaker.EggGatherers) : 0;

        /// <summary>A gatherer takes an egg from the reeds (#428's scene settles it here): one fewer duckling at the hatch. Never a bird;
        /// never while the flock is at its floor. False, and nothing changes, when there is none to take.</summary>
        public bool TakeEgg() => EcologyOn && EcoM.TryTake(LifeSpecies.Ducks, LifeTaker.EggGatherers, State.month, out _);

        /// <summary>Debug ("Gather an egg now", #574): put <paramref name="n"/> eggs in the nests (the model lays them in spring only), placing the
        /// pair first if no ducks are placed. Refused (returns the nests' count unchanged) when the flock is at its floor: an egg is never taken then.</summary>
        public int DebugEggsToNests(int n)
        {
            if (!EcologyOn || n <= 0) return EcoDuckEggs;
            if (EcoDucks == 0 && DebugPlaceNearCamp(LifeSpecies.Ducks) < 0) return 0;
            if (EcoDucks <= (int)EcoModel.Floor(LifeSpecies.Ducks)) return EcoDuckEggs;
            State.eco.duckEggs = Math.Max(State.eco.duckEggs, n);
            EcoTell();
            return State.eco.duckEggs;
        }

        /// <summary>The hunters' arrows are fletched with the ducks' feathers (once, from the first summer after the ducks came).</summary>
        public bool ArrowsAreFletched => EcologyOn && State.eco.fletchedMonth >= 0;
        public long ArrowsFletchedMonth => EcologyOn ? State.eco.fletchedMonth : -1;

        // ---------- the fish ----------
        /// <summary>The fish in the pond. 0 before the Keeper places the school.</summary>
        public int EcoFish => EcologyOn ? State.eco.fish : 0;
        /// <summary>How many fish shadows to show (#429): the stock, at most 8.</summary>
        public int EcoFishShown(int cap = FishShownCap) => Math.Max(0, Math.Min(cap, EcoFish));
        /// <summary>What the pond holds now (1 per 3 pond cells, never less than the founding school). 0 with no fish.</summary>
        public int EcoFishCap => EcologyOn && EcoFish > 0 ? EcoM.FishCap : 0;
        /// <summary>Where the school lives: the placed deep cell, or the pond's far side while it is shy (#422). -1 with no fish.</summary>
        public int EcoFishHome => EcologyOn && EcoFish > 0 ? EcoM.HomeCell(LifeSpecies.Fish) : -1;
        public int EcoFishCaught => EcologyOn ? State.eco.fishCaught : 0;

        /// <summary>Spec §5.5: fishing of any kind happens only once fish are placed (before that the pond is for drawing water,
        /// splashing and the ducks). Fish are placed in water and a pond never shrinks, so it stays open from then on (a job that
        /// opened never closes). With the ecology off the old box: always.</summary>
        public bool FishingOpen => !EcologyOn || LifeIsPlaced(LifeSpecies.Fish);

        /// <summary>#495: fishing happens only with fish placed AND open water at the shore (the drawn pond; a dried one stops the lines, the job stays open).</summary>
        public bool FishingWater => FishingOpen && OpenWater;

        /// <summary>How many fish the fishers may still take now: the stock above its floor; none in spring once the people have
        /// ruled on it (#422).</summary>
        public int FishSurplus => EcologyOn && EcoM.MayTake(LifeSpecies.Fish, LifeTaker.Fishers, State.month) ? EcoM.Surplus(LifeSpecies.Fish, LifeTaker.Fishers) : 0;

        /// <summary>How readily the fish bite (#429: the line's bites come slower near the floor): 1 with the stock at the pond's cap,
        /// falling to 0.15 one fish above the floor, 0 at the floor or while the take pauses (the fishers still sit and wait; nothing
        /// bites). 1 with the ecology off.</summary>
        public float FishBiteRate
        {
            get
            {
                if (!EcologyOn) return 1f;
                int spare = FishSurplus;
                if (spare <= 0) return 0f;
                int room = Math.Max(1, EcoM.FishCap - (int)EcoModel.Floor(LifeSpecies.Fish));
                return Math.Max(0.15f, Math.Min(1f, spare / (float)room));
            }
        }

        /// <summary>A fisher's catch (the line, the spear; #429 settles it here): one fish from the stock. Never below the floor of 3.
        /// False, and nothing changes, when none can be caught. Off the ecology the old pond: always true, nothing counted.</summary>
        public bool FishCatch()
        {
            if (!EcologyOn) return true;
            return EcoM.TryTake(LifeSpecies.Fish, LifeTaker.Fishers, State.month, out _);
        }

        /// <summary>The ducks' and the fish's news of the step (PlacedLifeTell).</summary>
        void PondTell()
        {
            var m = EcoM;
            if (!m.newFletched) return;
            m.newFletched = false;
            Chronicle("ducks_feathers", "The fletcher picked up duck feathers on the bank. From then on the hunters' arrows flew straighter.", 0.6f);
            ArrowsFletched?.Invoke();
        }

        // ---------- debug ----------
        /// <summary>Debug ("eggs now"): the ducks lay now (a clutch per pair in the nests, whatever the season). Places the pair first if
        /// there are none, and the flock gets one duck above its floor so the eggs can be taken. The eggs in the nests now.</summary>
        public int DebugEggsNow()
        {
            if (!EcologyOn) return 0;
            if (EcoDucks == 0 && DebugPlaceNearCamp(LifeSpecies.Ducks) < 0) return 0;
            var e = State.eco; var t = Config.eco;
            if (e.ducks <= (int)EcoModel.Floor(LifeSpecies.Ducks)) e.ducks = (int)EcoModel.Floor(LifeSpecies.Ducks) + 1; // one above the floor: the gatherers may take
            int pairs = Math.Max(1, e.ducks / 2);
            e.duckEggs = Math.Max(e.duckEggs, pairs * t.duckClutchMax);
            return e.duckEggs;
        }
    }
}
