using System;

namespace AquariumCiv.Sim
{
    /// <summary>What one town stall holds, as a view draws it: how full its goods are, 0 (bare counter) to
    /// <see cref="TownStalls.MaxLevel"/> (heaped).</summary>
    public struct StallStock
    {
        public int produce;   // fruit, sacks, baskets: from the food
        public int goods;     // planks, pots, cloth: from the wood the town has to trade
        public int bread;     // loaves: only where a bakery stands, from the food
        public int Level => Math.Max(produce, Math.Max(goods, bread));
        public bool Empty => produce == 0 && goods == 0 && bread == 0;
    }

    /// <summary>
    /// #154: how full the town's stalls are, from what the sim already has (food, wood, the population, the bakery); no
    /// new economy and no dice. Stalls fill one level at a time, the first stall first, so a small stock fills a few and a
    /// plentiful one fills them all; a stall's lowest level still shows something, so "empty" really means nothing.
    /// Pure C#: a view reads <see cref="StockFor"/> per stall every so often.
    /// </summary>
    public static class TownStalls
    {
        public const int MaxLevel = 3;
        /// <summary>Food per head at which the stalls are full (the sim's cap is 20 + 6 a head; a town sits well below it) and wood at which the goods are heaped.</summary>
        public const float FullFoodPerHead = 4f, FullWood = 300f;

        /// <summary>Total filled levels the food gives across <paramref name="stalls"/> stalls (0 .. stalls * MaxLevel).</summary>
        public static int Filled(float stock, float full, int stalls)
        {
            if (stalls <= 0 || stock <= 0f || full <= 0f) return 0;
            float frac = Math.Min(1f, stock / full);
            return Math.Min(stalls * MaxLevel, (int)Math.Ceiling(Math.Sqrt(frac) * stalls * MaxLevel));
        }

        /// <summary>The level of stall <paramref name="index"/> of <paramref name="stalls"/> when <paramref name="filled"/>
        /// levels are spread over them, the first stalls first.</summary>
        public static int LevelOf(int filled, int index, int stalls)
        {
            if (stalls <= 0 || index < 0 || index >= stalls) return 0;
            return Math.Min(MaxLevel, filled / stalls + (index < filled % stalls ? 1 : 0));
        }

        /// <summary>One stall's stock. <paramref name="bakery"/>: a bakery stands, so a stall carries bread too.</summary>
        public static StallStock StockFor(float food, float wood, int population, bool bakery, int index, int stalls)
        {
            int byFood = Filled(food, FullFoodPerHead * Math.Max(1, population), stalls);
            int byWood = Filled(wood, FullWood, stalls);
            return new StallStock
            {
                produce = LevelOf(byFood, index, stalls),
                goods = LevelOf(byWood, index, stalls),
                bread = bakery ? LevelOf(byFood, (stalls - 1 - index), stalls) : 0, // the other end of the row, so the two don't stack on one stall
            };
        }

        public static StallStock StockFor(SimCore sim, int index, int stalls) =>
            StockFor(sim.State.food, sim.State.wood, sim.State.population, sim.Count(BuildingType.Bakery) > 0, index, stalls);

        /// <summary>Debug panel: the stock a cheat sets, "empty", "half" or "full", as food and wood for <see cref="StockFor"/>.</summary>
        public static void DebugStock(string how, int population, out float food, out float wood)
        {
            float f = how == "full" ? 1f : how == "half" ? 0.25f : 0f; // 0.25 of the cap is half the levels (square-root curve)
            food = f * FullFoodPerHead * Math.Max(1, population);
            wood = f * FullWood;
        }
    }
}
