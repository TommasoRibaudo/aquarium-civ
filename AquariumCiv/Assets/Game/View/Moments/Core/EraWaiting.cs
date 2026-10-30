using System;
using System.Collections.Generic;
using AquariumCiv.Sim;

namespace AquariumCiv.View.Moments
{
    /// <summary>The props the people pile up while the era waits (era-moment.md §2).</summary>
    public enum WaitingProp
    {
        // waiting for the village
        SeedBaskets,   // stacked by the fire (grows)
        Stakes,        // a stake by every tent
        ClearedStrip,  // a cleared strip of ground at the edge
        // waiting for the town
        TimberPile,    // on the green (grows)
        StonePile,     // on the green (grows)
        MarketBell,    // hung but not rung
        ScaffoldPoles, // leaning on the huts
    }

    /// <summary>What the waiting leader does (era-moment.md §2).</summary>
    public enum WaitingLeader
    {
        None,
        AtFireLookingUp,  // the chief at the fire in the evening, looking up at the glass
        PacesTheGreen,    // the headman paces the green, a roll of plans under an arm
    }

    /// <summary>The era-ready waiting state, read from the sim for the view and the HUD.</summary>
    public class WaitingInfo
    {
        public bool waiting;
        public Era next;
        public long monthsWaited;
        /// <summary>1 when the era just became ready, up to <see cref="EraWaiting.MaxPile"/> after a long wait (0 = not waiting).</summary>
        public int pileLevel;
        public List<WaitingProp> props = new();
        public WaitingLeader leader;
        public bool eldersGather;  // the elders sit together more (village wait)
        public string watchLabel = "";   // the Watch button in the thumb zone
        public string glance = "";       // the glance peek
        public string closeLine = "";    // replaces the evening talk's teaser at the close
    }

    /// <summary>
    /// The era-ready waiting state (era-moment.md §2, #88): <c>eraReady</c> says the era waits; how long it waited
    /// (<c>eraReadyMonth</c>) sets the pile size. Nothing gets worse while it waits: the piles only grow a little, never
    /// anything sad. Pure: reads <see cref="SimState"/> only.
    /// </summary>
    public static class EraWaiting
    {
        public const int MaxPile = 4;
        /// <summary>Months waited at which the piles reach level 2, 3, 4 [est]: a game year (a real hour), six (an
        /// evening), twenty-four (a day away). Real time: a game year is one real hour.</summary>
        public static readonly long[] PileSteps = { 12, 72, 288 };

        /// <summary>Grime (0–100) above which Watch first offers "Wipe for a clear view?" [est]. It never blocks.</summary>
        public const float WipeOfferGrime = 60f;
        public const string WipeOffer = "Wipe for a clear view?", WatchAnyway = "Watch anyway";
        public const string CloseLine = "They'll wait for you";

        public static WaitingInfo Read(SimState s, string place)
        {
            var w = new WaitingInfo();
            if (s == null || !s.eraReady || s.era >= Era.Town) return w;
            w.waiting = true;
            w.next = s.era + 1;
            w.monthsWaited = s.eraReadyMonth < 0 ? 0 : Math.Max(0, s.month - s.eraReadyMonth);
            w.pileLevel = PileLevel(w.monthsWaited);
            string noun = w.next == Era.Village ? "village" : "town";
            w.watchLabel = $"Watch them become a {noun}";
            w.glance = $"{(string.IsNullOrEmpty(place) ? "The settlement" : place)} is ready to become a {noun} · Watch";
            w.closeLine = CloseLine;
            if (w.next == Era.Village)
            {
                w.props.AddRange(new[] { WaitingProp.SeedBaskets, WaitingProp.Stakes, WaitingProp.ClearedStrip });
                w.leader = WaitingLeader.AtFireLookingUp;
                w.eldersGather = true;
            }
            else
            {
                w.props.AddRange(new[] { WaitingProp.TimberPile, WaitingProp.StonePile, WaitingProp.MarketBell, WaitingProp.ScaffoldPoles });
                w.leader = WaitingLeader.PacesTheGreen;
            }
            return w;
        }

        public static int PileLevel(long monthsWaited)
        {
            int level = 1;
            foreach (var step in PileSteps) if (monthsWaited >= step) level++;
            return Math.Min(level, MaxPile);
        }

        /// <summary>Whether a prop grows with the wait (the others are one per tent / hut, or a single thing).</summary>
        public static bool Grows(WaitingProp p) => p == WaitingProp.SeedBaskets || p == WaitingProp.TimberPile || p == WaitingProp.StonePile;

        /// <summary>How many items a growing pile shows at a level [est]: 3, 5, 7, 10 (a comic stack at the top).</summary>
        public static int PileCount(int level) => level <= 0 ? 0 : level switch { 1 => 3, 2 => 5, 3 => 7, _ => 10 };

        /// <summary>Watch first offers a wipe when the glass is very grimy (§3).</summary>
        public static bool OfferWipeFirst(float grime) => grime >= WipeOfferGrime;
    }
}
