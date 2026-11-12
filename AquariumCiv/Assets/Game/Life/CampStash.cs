using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>What the pile by the fire holds (#150): counts of things a view draws.</summary>
    public struct StashStock
    {
        public int logs, baskets, hides;
        /// <summary>#543: dried fish on the pile (0..<see cref="CampStash.MaxFish"/>).</summary>
        public int fish;
    }

    /// <summary>The stash as a view needs it (<see cref="LifePlanner.TryGetStash"/>): the pile's world spot, the way from it
    /// to the fire (not normalised, to turn the pile), and the stock.</summary>
    public struct StashLook
    {
        public float x, z, faceX, faceZ;
        public StashStock stock;
    }

    /// <summary>
    /// #150: the camp's stash, pure. Carriers used to walk into the fire and their load vanished; now they stop at
    /// <see cref="Stand"/>, just outside the fire's ring of people, with the pile at <see cref="Pile"/> behind it.
    ///
    /// The spot never moves: it is the gap between two log seats (Pieces.Firepit: at 0.4, 2.5 and 4.6 radians, 0.4 out,
    /// 0.3 long) that the huts leave free: over 60 worlds to the end of the camp, the front gap lost its ground to a hut in
    /// half of them, so no gap is picked at run time and the pile never jumps. The crumb's flat stone (RelicProps: fire + (0.34, 0.1)) is on the far side of the fire.
    /// A view only reads this; nothing here touches the sim.
    /// </summary>
    public static class CampStash
    {
        /// <summary>Where a walk with a load ends.</summary>
        public static readonly Place Stand = Place.Land(PlaceKind.Stash);
        /// <summary>The middle of the pile.</summary>
        public static readonly Place Pile = Place.Land(PlaceKind.Stash, 1);

        /// <summary>The direction from the fire (radians: x = cos, z = sin, as Pieces.Firepit).</summary>
        public const float Angle = 3.55f;
        /// <summary>Distances from the fire (the ring of people round it is at 0.3).</summary>
        public const float StandRadius = 0.34f, PileRadius = 0.46f;
        /// <summary>The ground a full pile may cover, round its middle.</summary>
        public const float PileExtent = 0.1f;

        public const int MaxLogs = 10, MaxBaskets = 4, MaxHides = 3, MaxFish = 6;
        /// <summary>The stock that shows as a full pile. A camp's food sits at the sim's cap (20 + 6 a head), and its wood
        /// runs from a few logs to hundreds, so the two are read as plain amounts, not against a cap.</summary>
        public const float FullFood = 150f, FullWood = 400f;

        /// <summary>A point <paramref name="radius"/> from the fire, towards the stash.</summary>
        public static void At(float fx, float fz, float radius, out float x, out float z)
        {
            x = fx + (float)Math.Cos(Angle) * radius;
            z = fz + (float)Math.Sin(Angle) * radius;
        }

        /// <summary>The pile for a stock. Any stock shows (one log, one basket) and low stock changes it most: the square
        /// root makes the first few logs count. Hides join in the upper half of the food.</summary>
        public static StashStock StockOf(float food, float wood)
        {
            const float half = FullFood * 0.5f;
            return new StashStock
            {
                logs = Steps(wood, FullWood, MaxLogs),
                baskets = Steps(food, FullFood, MaxBaskets),
                hides = Steps(food - half, half, MaxHides),
            };
        }

        /// <summary>#543: the pile's dried fish as the view draws them: every fish shows up to <see cref="MaxFish"/> (the sim's pile holds
        /// <see cref="SimCore.FishPileMax"/>, three to a drawn fish beyond that).</summary>
        public static int FishShown(int inPile) => inPile <= 0 ? 0 : Math.Min(MaxFish, inPile <= 4 ? inPile : 4 + (inPile - 4 + 6) / 7);

        static int Steps(float value, float full, int max)
        {
            if (value <= 0f) return 0;
            return Math.Min(max, (int)Math.Ceiling(max * Math.Sqrt(Math.Min(1f, value / full))));
        }
    }
}
