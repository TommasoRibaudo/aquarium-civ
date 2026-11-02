using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Audio;
using AquariumCiv.Sim;

namespace AquariumCiv.View.ShowMe
{
    public enum WishBeat { Given, Met }

    /// <summary>What the view plays when a wish is given or met (#191): the sound, the wisher's reaction, and who reacts at night.</summary>
    public sealed class WishPayoffPlan
    {
        public WishBeat beat;
        /// <summary>The sound, at once (<see cref="CueLibrary.WishGiven"/>, or the small cheer for the Met beat). Played at night too, softer: <see cref="quiet"/>.</summary>
        public string cue;
        public bool quiet;
        public int wisherId;
        /// <summary>The wisher is not on screen: fly to them (the view's call) rather than wait for the player to find them.</summary>
        public bool flyToWisher;
        /// <summary>Show the effect where the gift lands, at once. Always true: the gift is placed whether anyone is awake or not.</summary>
        public bool effectNow = true;
        /// <summary>The wisher's own words, and whether they say them now. At night with the wisher asleep they wait for waking.</summary>
        public PeopleQuote wisherLine;
        public bool wisherSaysNow;
        /// <summary>Night, wisher asleep: someone awake says this instead, now (null if nobody is awake).</summary>
        public PeopleQuote witnessLine;
    }

    /// <summary>Pure decisions for #191's payoff: no dice, no sim writes. The view (LifePlanner scenes, audio, shaping effects) plays what this says.</summary>
    public static class WishPayoff
    {
        public static WishPayoffPlan Plan(SimCore sim, Wish wish, WishBeat beat, bool night, Func<int, bool> isAwake, IEnumerable<int> awakeIds, bool wisherOnScreen)
        {
            if (sim == null || wish == null) return null;
            var plan = new WishPayoffPlan
            {
                beat = beat, wisherId = wish.notableId, quiet = night,
                cue = beat == WishBeat.Given ? CueLibrary.WishGiven : CueLibrary.CheerSmall,
                flyToWisher = !wisherOnScreen,
                wisherLine = PeopleVoice.WishReaction(sim, wish, beat == WishBeat.Met),
            };
            bool wisherAwake = !night || (isAwake != null && isAwake(wish.notableId));
            plan.wisherSaysNow = wisherAwake && plan.wisherLine != null;
            if (!wisherAwake && beat == WishBeat.Given && awakeIds != null)
            {
                int witness = awakeIds.Where(i => i != wish.notableId).DefaultIfEmpty(-1).Min();
                if (witness >= 0) plan.witnessLine = PeopleVoice.WishWitness(sim, witness, wish);
            }
            return plan;
        }
    }
}
