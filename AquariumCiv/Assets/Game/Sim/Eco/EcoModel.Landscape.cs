using System;

namespace AquariumCiv.Sim.Eco
{
    /// <summary>
    /// #726 (docs/design/landscaping.md §8.1, decision 2026-11-14 "B3 trees, A3 bushes"): what the Keeper earns to plant as the civ grows.
    /// Two pouch counters, seeds (#727: each plants a normal tree seedling) and bushes (#728: each plants a small thicket), paid from the
    /// same civ steps so they feel like one gift. A step is a new era, or +<see cref="EcoTuning.landscapePeopleStep"/> people over the
    /// start at the box's PEAK (a shrinking village pays nothing: the peak never falls). At most <see cref="EcoTuning.landscapePouchCap"/>
    /// of each wait; a step that finds a full pouch stays owed and pays when there is room, so nothing is lost. The first seed and the
    /// first bush are not in the pouch: the seed is in the life jar from the start and the first bush is the thicket jar. No dice.
    /// </summary>
    public sealed partial class EcoModel
    {
        /// <summary>Seeds waiting in the pouch (each plants one normal seedling beside the Keeper's tree).</summary>
        public int SeedsInPouch => st.seedsPouch;

        /// <summary>Bushes waiting in the pouch (each plants one small thicket).</summary>
        public int BushesInPouch => st.bushesPouch;

        /// <summary>Civ steps the box has reached: one for each era past the camp, and one for each
        /// <see cref="EcoTuning.landscapePeopleStep"/> people the peak is over <see cref="EcoTuning.landscapeStartPeople"/>.</summary>
        public int LandscapeSteps(int era, int peakPeople)
        {
            int step = Math.Max(1, t.landscapePeopleStep);
            return Math.Max(0, era) + Math.Max(0, (peakPeople - t.landscapeStartPeople) / step);
        }

        /// <summary>Called every month: notes the peak and pays the steps now owed, each counter up to its cap. Returns how many
        /// seeds and bushes were added together (0 = nothing new). Idempotent: a step is paid once.</summary>
        public int EarnLandscape(int era, int people)
        {
            if (people > st.peakPeople) st.peakPeople = people;
            int steps = LandscapeSteps(era, st.peakPeople), cap = Math.Max(0, t.landscapePouchCap), gained = 0;
            while (st.seedSteps < steps && st.seedsPouch < cap) { st.seedSteps++; st.seedsPouch++; gained++; }
            while (st.bushSteps < steps && st.bushesPouch < cap) { st.bushSteps++; st.bushesPouch++; gained++; }
            return gained;
        }

        /// <summary>A seed leaves the pouch (it was planted). False when the pouch is empty.</summary>
        bool TakeSeed()
        {
            if (st.seedsPouch <= 0) return false;
            st.seedsPouch--;
            return true;
        }

        /// <summary>A bush leaves the pouch (it was placed). False when the pouch is empty.</summary>
        bool TakeBush()
        {
            if (st.bushesPouch <= 0) return false;
            st.bushesPouch--;
            return true;
        }

        /// <summary>Debug ("give seeds"): <paramref name="n"/> seeds into the pouch now, past the cap if need be.</summary>
        public void DebugGiveSeeds(int n) { st.seedsPouch = Math.Max(0, st.seedsPouch + n); }

        /// <summary>Debug ("give bushes"): <paramref name="n"/> bushes into the pouch now, past the cap if need be.</summary>
        public void DebugGiveBushes(int n) { st.bushesPouch = Math.Max(0, st.bushesPouch + n); }

        /// <summary>Debug ("next civ step"): the peak people rise by one step now (as if the village had grown that much), and what it
        /// owes is paid at once. Returns how many seeds and bushes it added (0 when both pouches are full).</summary>
        public int DebugNextCivStep(int era, int people)
        {
            int step = Math.Max(1, t.landscapePeopleStep);
            st.peakPeople = Math.Max(st.peakPeople, people) + step;
            return EarnLandscape(era, st.peakPeople);
        }
    }
}
