using System;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #543: the fish chain, the sim side (the planner's duties and the views read it). A fisher's catch goes into the fish basket;
    /// someone hangs the basket's fish on the rack by the fire (<see cref="FishHang"/>); a fish dries over one view day (hung on day D,
    /// dry from day D + 1); someone takes the dry ones down and carries them to the food pile (<see cref="FishToPile"/>); the camp eats
    /// from the pile (<see cref="FishDayTurn"/>). Counts only, saved with the ecology state; the food number itself is the job's rate
    /// as ever (the pile and the rack show where the fish are, they do not pay twice). Every step is a plain move of a count that is
    /// safe to run twice (a reload that settles a duty again finds the basket empty), and nothing here uses the sim's dice.
    /// With the ecology off none of it does anything.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The basket holds, the rack takes and the pile shows at most this many fish.</summary>
        public const int FishBasketMax = 12, FishRackMax = 12, FishPileMax = 18;

        public int FishInBasket => EcologyOn ? State.eco.fishBasket : 0;
        /// <summary>Fish hanging on the rack now, fresh and dry.</summary>
        public int FishOnRack => EcologyOn ? State.eco.fishRackFresh + State.eco.fishRackDry : 0;
        /// <summary>Dried fish on the food pile.</summary>
        public int FishInPile => EcologyOn ? State.eco.fishPile : 0;
        public long FishRackDay => EcologyOn ? State.eco.fishRackDay : -1;

        /// <summary>Fish on the rack that are dry on view day <paramref name="day"/> (those hung on an earlier day).</summary>
        public int FishDryOn(long day)
        {
            if (!EcologyOn) return 0;
            var e = State.eco;
            return e.fishRackDry + (e.fishRackFresh > 0 && day > e.fishRackDay ? e.fishRackFresh : 0);
        }

        /// <summary>A fisher's catch lands in the fish basket. False (and the fish is not kept) when the basket is full or the ecology is off.</summary>
        public bool FishToBasket()
        {
            if (!EcologyOn || State.eco.fishBasket >= FishBasketMax) return false;
            State.eco.fishBasket++;
            return true;
        }

        void FishDry(long day)
        {
            var e = State.eco;
            if (e.fishRackFresh > 0 && day > e.fishRackDay) { e.fishRackDry += e.fishRackFresh; e.fishRackFresh = 0; }
        }

        /// <summary>Up to <paramref name="n"/> fish go from the basket onto the rack on view day <paramref name="day"/> (the rack's room
        /// permitting); they are dry from the next day. Returns how many were hung.</summary>
        public int FishHang(int n, long day)
        {
            if (!EcologyOn || n <= 0) return 0;
            var e = State.eco;
            FishDry(day);
            int room = FishRackMax - e.fishRackFresh - e.fishRackDry;
            int took = Math.Max(0, Math.Min(Math.Min(n, e.fishBasket), room));
            if (took == 0) return 0;
            e.fishBasket -= took;
            e.fishRackFresh += took;
            e.fishRackDay = day;
            return took;
        }

        /// <summary>Up to <paramref name="n"/> dry fish leave the rack for the food pile (the pile's room permitting) on view day
        /// <paramref name="day"/>. Returns how many.</summary>
        public int FishToPile(int n, long day)
        {
            if (!EcologyOn || n <= 0) return 0;
            var e = State.eco;
            FishDry(day);
            int took = Math.Max(0, Math.Min(Math.Min(n, e.fishRackDry), FishPileMax - e.fishPile));
            e.fishRackDry -= took;
            e.fishPile += took;
            return took;
        }

        /// <summary>The camp eats from the pile once a view day: a fish for every three people (at least one), if there is any.
        /// Returns how many were eaten.</summary>
        public int FishDayTurn(long day, int people)
        {
            if (!EcologyOn || State.eco.fishEatDay == day) return 0;
            State.eco.fishEatDay = day;
            int ate = Math.Min(State.eco.fishPile, Math.Max(1, people / 3));
            State.eco.fishPile -= ate;
            return ate;
        }

        // ---------- debug ----------
        /// <summary>Debug ("fill fish basket"): the basket is full of fish.</summary>
        public int DebugFillFishBasket(int n = 6)
        {
            if (!EcologyOn) return 0;
            State.eco.fishBasket = Math.Min(FishBasketMax, Math.Max(State.eco.fishBasket, n));
            return State.eco.fishBasket;
        }

        /// <summary>Debug ("fish on the rack"): <paramref name="n"/> fish hang on the rack, dry already (hung before <paramref name="day"/>).</summary>
        public int DebugFishOnRack(int n, long day)
        {
            if (!EcologyOn) return 0;
            var e = State.eco;
            e.fishRackFresh = 0;
            e.fishRackDry = Math.Min(FishRackMax, Math.Max(0, n));
            e.fishRackDay = day - 1;
            return FishOnRack;
        }

        /// <summary>Debug ("dried fish to pile"): the pile holds <paramref name="n"/> dried fish.</summary>
        public int DebugFishToPile(int n)
        {
            if (!EcologyOn) return 0;
            State.eco.fishPile = Math.Min(FishPileMax, Math.Max(0, n));
            return State.eco.fishPile;
        }
    }
}
