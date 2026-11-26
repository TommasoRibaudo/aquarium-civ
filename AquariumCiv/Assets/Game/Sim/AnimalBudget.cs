using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #710: the one visible-animal budget. The most animals of each kind the box DRAWS (the sim may count more; these only cap what
    /// the views show). The table is stated for the 6 x 6 land at 25 animals (about 0.7 per u2; 33 before the 2026-11-24 cut); every
    /// cap scales with the land's area and is rounded, so the old 6 x 4.4 land would draw 18 and the deeper box (6 x 6) draws 25.
    /// A NEW KIND OF ANIMAL MUST TAKE ITS SHARE FROM THE OTHERS: add a row, lower the others so <see cref="BaseTotal"/> stays about 25,
    /// and keep the total test (AnimalBudgetTests) green. Never add a kind on top.
    /// Outside the table, by the user's call: the nest's parent bird (#709 Q4, <see cref="NestBirdSlots"/>) and the one goat in the pond
    /// family's pen (2026-11-16 Q7, <see cref="SimCore.PenGoat"/>: it lives only in the pen, is never hunted and is in no herd).
    /// </summary>
    public static class AnimalBudget
    {
        public enum Kind { Rabbits, Deer, Ducks, Fish, Frogs, Wolves }

        /// <summary>The land the table is stated for (6 x 6 u2), and the caps there.</summary>
        public const float BaseArea = 36f;
        // Wolves count the whole drawn pack, pups included.
        // 2026-11-24 (#710, "every kind gets fewer"): 8, 6, 6, 5, 4, 4 (33) -> 6, 5, 4, 4, 3, 3 (25), about a quarter fewer drawn.
        // The floors that still read as a group: rabbits enough for a hunt, a kit and the dog's flush; deer a stag, does and a fawn;
        // ducks two pairs; frogs more at night (3) than by day (2); wolves the pair and a pup. docs/design/animal-caps.md.
        static readonly int[] Base = { 6, 5, 4, 4, 3, 3 };

        /// <summary>The land's area now (2*HX by 2*HZ). The deeper box raises HZ and the caps follow.</summary>
        public static float LandArea => 2f * Terrain.HX * 2f * Terrain.HZ;

        /// <summary>The most of <paramref name="kind"/> drawn on land of <paramref name="area"/> u2 (at least 1).</summary>
        public static int Cap(Kind kind, float area)
        {
            int cap = (int)MathF.Round(Base[(int)kind] * area / BaseArea);
            if (kind == Kind.Frogs) cap -= NestBirdSlots; // #709 Q4: the nest's parent bird, if it is made to take a slot, takes it from the frogs
            return Math.Max(1, cap);
        }

        /// <summary>#709 Q4 [est, the user decides]: animal slots the nest's parent bird takes from the frogs. 0 = the bird is outside the budget (the proposal
        /// the user chose with the nest: "the nest costs no animal slots"; the sign bird at the first bush never counted either). 1 = it takes one frog slot.
        /// One line to change (AnimalBudgetTests pins the table at 0; at 1 the frogs draw one fewer and the total is 24).</summary>
        public static int NestBirdSlots = 0;
        /// <summary>The most of <paramref name="kind"/> drawn on the current land.</summary>
        public static int Cap(Kind kind) => Cap(kind, LandArea);

        /// <summary>All kinds together on land of <paramref name="area"/> u2.</summary>
        public static int Total(float area)
        {
            int n = 0; foreach (Kind k in Enum.GetValues(typeof(Kind))) n += Cap(k, area);
            return n;
        }
        public static int Total() => Total(LandArea);
        public static int BaseTotal { get { int n = 0; foreach (int b in Base) n += b; return n; } }

        public static int Rabbits => Cap(Kind.Rabbits);
        public static int Deer => Cap(Kind.Deer);
        public static int Ducks => Cap(Kind.Ducks);
        public static int Fish => Cap(Kind.Fish);
        public static int Frogs => Cap(Kind.Frogs);
        public static int Wolves => Cap(Kind.Wolves);
    }
}
