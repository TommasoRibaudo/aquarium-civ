using System;
using AquariumCiv.Sim.Settle;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #710: the one visible-animal budget. The most animals of each kind the box DRAWS (the sim may count more; these only cap what
    /// the views show). The table is stated for the 6 x 6 land at 33 animals (about 0.9 per u2); every cap scales with the land's
    /// area and is rounded, so today's 6 x 4.4 land draws 24 and the deeper box (6 x 6) draws 33.
    /// A NEW KIND OF ANIMAL MUST TAKE ITS SHARE FROM THE OTHERS: add a row, lower the others so <see cref="BaseTotal"/> stays about 33,
    /// and keep the total test (AnimalBudgetTests) green. Never add a kind on top.
    /// </summary>
    public static class AnimalBudget
    {
        public enum Kind { Rabbits, Deer, Ducks, Fish, Frogs, Wolves }

        /// <summary>The land the table is stated for (6 x 6 u2), and the caps there.</summary>
        public const float BaseArea = 36f;
        // Wolves count the whole drawn pack, pups included.
        static readonly int[] Base = { 8, 6, 6, 5, 4, 4 };

        /// <summary>The land's area now (2*HX by 2*HZ). The deeper box raises HZ and the caps follow.</summary>
        public static float LandArea => 2f * Terrain.HX * 2f * Terrain.HZ;

        /// <summary>The most of <paramref name="kind"/> drawn on land of <paramref name="area"/> u2 (at least 1).</summary>
        public static int Cap(Kind kind, float area) => Math.Max(1, (int)MathF.Round(Base[(int)kind] * area / BaseArea));
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
