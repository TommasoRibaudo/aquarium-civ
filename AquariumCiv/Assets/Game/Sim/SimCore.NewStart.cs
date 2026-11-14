using System;

namespace AquariumCiv.Sim
{
    /// <summary>#656/#677: the camp's fire, as the land lets it burn. The view draws it; the planner reads it for the fire's chores.</summary>
    public enum CampFireStage
    {
        /// <summary>Nothing to burn yet (no bushes): no fire, the wanderers huddle where the camp will be.</summary>
        None = 0,
        /// <summary>A small fire of dry grass and twigs from the first bushes: low, quick flames, no logs. Tended by gathering an
        /// armful of dry grass and twigs at the meadow's edge (never "chopping kindling", never "wood").</summary>
        GrassAndTwig = 1,
        /// <summary>From the first wild wood on: a fire built with logs (bigger, warmer).</summary>
        Logs = 2,
    }

    /// <summary>#656/#677: how a new living-land box began. Default-safe: an old save loads with <see cref="on"/> false and plays as before.</summary>
    [Serializable]
    public class NewStartState
    {
        /// <summary>The box began with the carried stock (a basket of food and a bundle of sticks).</summary>
        public bool on;
        /// <summary>The arrival set the basket and the bundle down where they settle: the HUD shows the stock from here.</summary>
        public bool setDown;
        public long setDownMonth = -1;
        /// <summary>The first month the land gave food (berries or rabbits): the first refill shown at the pile.</summary>
        public long landFoodMonth = -1;
        /// <summary>The first logs from a grown wild tree (the woods; never the Keeper's tree).</summary>
        public long firstWildWoodMonth = -1;
        /// <summary>The view played the first-logs moment once.</summary>
        public bool firstWildWoodSeen;
        /// <summary>Debug: a fire stage held by the cheat (-1 = the land decides).</summary>
        public int fireOverride = -1;
    }

    /// <summary>
    /// #656/#677 (the user, 2026-11-14), on a living land only (the ecology on; the old slice keeps food 20 / wood 4):
    /// <list type="bullet">
    /// <item>The people arrive with what they carried: a basket of food (<see cref="CarriedFood"/>, the start's food stock) and a bundle of
    /// sticks (<see cref="CarriedSticks"/>, shown and kept, NOT spendable wood). Usable wood starts at 0.</item>
    /// <item>Until the land gives food (a bush or the rabbits) the basket runs down at <see cref="CarriedRation"/> a person a month; the
    /// land's food comes after that (the usual economy). Food never goes below 0 and nobody dies of hunger (<see cref="Hungry"/> is shown, never punished).</item>
    /// <item>Monthly wood comes only from grown WILD trees, never the Keeper's tree: the woods (<see cref="WoodsTreeCount"/>, chopped)
    /// <see cref="WildWoodPerTree"/> a tree, and the wild trees near the Keeper's tree (<see cref="DeadfallTreeCount"/>: their fallen branches
    /// gathered, never chopped) <see cref="DeadfallPerTree"/>, capped by the people's hands and the lumber mill (the old income).</item>
    /// <item>The fire (<see cref="FireStage"/>): none before any bush, grass and twigs from the first bush, logs from the first wild wood
    /// (<see cref="FirstWildWoodPending"/>: a moment the view plays once).</item>
    /// </list>
    /// No dice: nothing here draws from the sim's streams.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The carried basket's food (was a plain 20 in the stock).</summary>
        public const float CarriedFood = 12f;
        /// <summary>How many sticks the carried bundle shows (the view's prop; never counted as wood).</summary>
        public const int CarriedSticks = 5;
        /// <summary>What a person eats from the basket a month before the land gives food [est]: 12 last 5 people about a year (an hour of play).</summary>
        public const float CarriedRation = 0.2f;
        /// <summary>Wood a month from each grown wild tree of the woods [est], before the mood/mess pace and the lean.</summary>
        public const float WildWoodPerTree = 0.6f;
        /// <summary>Wood a month from each grown wild tree near the Keeper's tree [est]: its fallen branches, gathered (never chopped, #573).</summary>
        public const float DeadfallPerTree = 0.3f;

        /// <summary>The first logs came home from a wild tree (raised once, live or in catch-up; the view plays the moment when it is watched).</summary>
        public event Action FirstWildWood;

        NewStartState NewStart => State.newStart ??= new NewStartState();

        /// <summary>This box began with the carried stock (a living land made after #656/#677).</summary>
        public bool NewStartOn => EcologyOn && State.newStart != null && State.newStart.on;

        /// <summary>The basket and the bundle are down: the HUD shows the stock numbers. Always true on an old box or with the ecology off.</summary>
        public bool StockSetDown => !NewStartOn || State.newStart.setDown;

        /// <summary>The month the stock was set down (-1 = not yet, or an old box).</summary>
        public long StockSetDownMonth => NewStartOn ? State.newStart.setDownMonth : -1;

        /// <summary>The land gives food: a bush has come up or rabbits are about. Off the ecology always.</summary>
        public bool LandGivesFood => !EcologyOn || BerriesGrow || RabbitsSeen;

        /// <summary>The month the land first gave food on a new box (the first refill at the pile), or -1.</summary>
        public long LandFoodMonth => NewStartOn ? State.newStart.landFoodMonth : -1;

        /// <summary>The food is gone: shown (slower, looking at the sky), never punished. Nobody dies of it and the box never fails.</summary>
        public bool Hungry => State.food <= 0.001f;

        /// <summary>The first wild wood came (the first logs from a grown wild tree), or -1.</summary>
        public long FirstWildWoodMonth => NewStartOn ? State.newStart.firstWildWoodMonth : -1;
        public bool FirstWildWoodReached => FirstWildWoodMonth >= 0;
        /// <summary>The first-logs moment is due and the view has not played it yet. Call <see cref="MarkFirstWildWoodSeen"/> once it plays.</summary>
        public bool FirstWildWoodPending => FirstWildWoodReached && !State.newStart.firstWildWoodSeen;
        public void MarkFirstWildWoodSeen() { if (NewStartOn) NewStart.firstWildWoodSeen = true; }

        /// <summary>The fire as the land lets it burn now. An old box or the old slice: always a log fire, as before.</summary>
        public CampFireStage FireStage
        {
            get
            {
                if (!NewStartOn) return CampFireStage.Logs;
                var n = State.newStart;
                if (n.fireOverride >= 0) return (CampFireStage)Math.Min(n.fireOverride, (int)CampFireStage.Logs);
                if (n.firstWildWoodMonth >= 0) return CampFireStage.Logs;
                return BerriesGrow ? CampFireStage.GrassAndTwig : CampFireStage.None;
            }
        }

        /// <summary>Called by <see cref="NewWorld"/> and the museum's next box once the ecology has started: the carried stock.</summary>
        void BeginCarriedStock()
        {
            if (!EcologyOn) return;
            State.food = CarriedFood;
            State.wood = 0f;
            State.newStart = new NewStartState { on = true };
        }

        /// <summary>The arrival set the basket and the bundle down (the view calls it at the end of the walk-in). True when it changed.</summary>
        public bool SetDownStock()
        {
            if (!NewStartOn || NewStart.setDown) return false;
            NewStart.setDown = true;
            NewStart.setDownMonth = State.month;
            return true;
        }

        /// <summary>The month's wood (before the pace and the lean). A new box: only from grown wild trees (the woods, chopped; and the
        /// deadfall of those near the Keeper's tree, gathered), capped by the hands and the mill. The Keeper's own tree gives nothing.</summary>
        float WoodIncome(int pop)
        {
            float hands = 1f + pop * 0.15f + Count(BuildingType.Lumbermill) * 1.5f;
            if (!NewStartOn) return hands;
            float wild = WoodsTreeCount * WildWoodPerTree + DeadfallTreeCount * DeadfallPerTree;
            return wild <= 0f ? 0f : Math.Min(wild, hands);
        }

        /// <summary>Grown wild trees that give wood at all: the woods (<see cref="WoodsTreeCount"/>) and the deadfall trees (<see cref="DeadfallTreeCount"/>).</summary>
        public int WildWoodTreeCount => WoodsTreeCount + DeadfallTreeCount;

        /// <summary>#656/#677: a grown wild tree inside the clear radius round the Keeper's tree (never the Keeper's own tree), nearest
        /// (<paramref name="nearX"/>, <paramref name="nearZ"/>), or -1. It is never chopped (#573): its fallen branches are gathered (deadfall).</summary>
        public int DeadfallTreeCell(float nearX, float nearZ, int rank = 0)
        {
            if (!EcologyOn) return -1;
            var e = State.eco; var cfg = Config.eco ?? (Config.eco = new Eco.EcoTuning());
            if (e.treeCell < 0 || e.treeCell >= Eco.EcoLand.N) return -1; // no Keeper's tree: every wild tree is the woods
            const float mx = Settle.Terrain.HX - Settle.Terrain.GlassMargin, mz = Settle.Terrain.HZ - Settle.Terrain.GlassMargin;
            float kx = Settle.SiteMap.CXc(e.treeCell), kz = Settle.SiteMap.CZc(e.treeCell);
            var found = deadfallScratch; found.Clear();
            for (int c = 0; c < Eco.EcoLand.N; c++)
            {
                if ((Eco.EcoCover)e.cover[c] != Eco.EcoCover.Tree || c == e.treeCell) continue;
                float x = Settle.SiteMap.CXc(c), z = Settle.SiteMap.CZc(c);
                if (Math.Abs(x) > mx || Math.Abs(z) > mz) continue;
                if (Settle.Terrain.Dist(x, z, kx, kz) >= cfg.woodsClearRadius) continue; // out there it is the woods (WoodsTreeCell)
                found.Add(((x - nearX) * (x - nearX) + (z - nearZ) * (z - nearZ), c));
            }
            if (found.Count == 0 || rank >= found.Count) return -1;
            found.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.c.CompareTo(b.c));
            return found[Math.Max(0, rank)].c;
        }
        readonly System.Collections.Generic.List<(float d, int c)> deadfallScratch = new();

        /// <summary>How many grown wild trees near the Keeper's tree give deadfall.</summary>
        public int DeadfallTreeCount { get { if (!EcologyOn) return 0; DeadfallTreeCell(0f, 0f, 1 << 20); return deadfallScratch.Count; } }

        /// <summary>The month's food before the land gives any: the basket runs down. Null once the land gives (the usual economy then).</summary>
        float? BasketFood(int pop)
        {
            if (!NewStartOn) return null;
            if (LandGivesFood)
            {
                if (NewStart.landFoodMonth < 0) NewStart.landFoodMonth = State.month;
                return null;
            }
            return Math.Max(0f, State.food - pop * CarriedRation);
        }

        /// <summary>After the month's economy: the first wild wood, and the set-down's safety net (a camp that stands has its stock down).</summary>
        void NewStartStep(float woodGain)
        {
            if (!NewStartOn) return;
            var n = NewStart;
            if (!n.setDown && CampGateOpen) SetDownStock();
            if (woodGain > 0f && n.firstWildWoodMonth < 0) ReachFirstWildWood();
        }

        void ReachFirstWildWood()
        {
            NewStart.firstWildWoodMonth = State.month;
            Chronicle("first_wild_wood", "The first logs came home from the wild trees, and that night the fire was built with wood.", 0.6f);
            FirstWildWood?.Invoke();
        }

        // ---------- debug (the view's panel buttons come with the view work) ----------
        /// <summary>Debug "first wild wood now": a grown wild tree away from the Keeper's tree if there is none, and the first logs now
        /// (the month's wood from it, the milestone, the moment due). Returns what it did.</summary>
        public string DebugFirstWildWoodNow(Func<float, float, float, bool> drawable = null)
        {
            if (!NewStartOn) return "not a new-start box (living land off, or an old box)";
            string tree = WildWoodTreeCount > 0 ? "a wild tree stood" : DebugWildTrees(false, 1, drawable);
            if (WildWoodTreeCount <= 0) return "no wild tree: " + tree;
            if (FirstWildWoodReached) return "the first wild wood came already (month " + NewStart.firstWildWoodMonth + ")";
            float gain = WoodIncome(State.population);
            State.wood = Math.Min(State.wood + gain, 2000f);
            NewStart.firstWildWoodSeen = false;
            ReachFirstWildWood();
            return $"{tree}; the first logs (+{gain:0.0} wood)";
        }

        /// <summary>Debug "empty the basket": the food is gone (hunger shown, nobody harmed).</summary>
        public void DebugEmptyBasket() => State.food = 0f;

        /// <summary>Debug "fire stage": hold the fire at a stage (0 none, 1 grass and twigs, 2 logs); -1 lets the land decide again.</summary>
        public void DebugFireStage(int stage) { if (NewStartOn) NewStart.fireOverride = stage < 0 ? -1 : Math.Min(stage, (int)CampFireStage.Logs); }

        /// <summary>Debug "arrival again": the basket and the bundle are carried in again (not yet set down).
        /// The stock itself is untouched.</summary>
        public void DebugArrivalAgain()
        {
            if (!NewStartOn) return;
            NewStart.setDown = false; NewStart.setDownMonth = -1;
        }
    }
}
