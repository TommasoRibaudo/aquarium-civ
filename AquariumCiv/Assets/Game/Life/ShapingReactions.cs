using System;
using AquariumCiv.Sim;

namespace AquariumCiv.Life
{
    /// <summary>What the people do when a Keeper's gift lands, by tool (#142): the first to see it looks up and walks over
    /// for a look, a couple more follow, and whoever is too far off looks up and points from where they stand. Long before
    /// the people answer it (<see cref="ShapingAnswers"/>, a game year or more later). Staged by
    /// <see cref="LifePlanner.StartShapingReaction"/> on the relic moments' machinery. Data, not code: tune here.</summary>
    public static class ShapingReactions
    {
        /// <summary>Real seconds a reaction lasts when someone walks over [est].</summary>
        public const float Seconds = 18f;

        /// <summary>The longest a reaction stretches so someone from the far side of the box can still come over at the
        /// walking pace, and the steps it stretches in [est].</summary>
        public const float MaxSeconds = 38f, StretchStep = 4f;

        /// <summary>Real seconds the first to see it stands looking before they set off, and the others [est].</summary>
        public const float FirstNotice = 2f, OthersNotice = 3.2f;

        /// <summary>Real seconds at the gift, at the end of the reaction [est].</summary>
        public const float AtTheGift = 7f;

        /// <summary>Real seconds someone looks and points from where they stand [est].</summary>
        public const float FromAfar = 7f;

        /// <summary>A reaction with less of the day left than this waits for the next day [est].</summary>
        public const float MinSeconds = 3f;

        /// <summary>What's asked of a reaction by day (the test's bar, #142): someone reacts this soon after a gift lands.</summary>
        public const float WithinSeconds = 10f;

        /// <summary>Asleep, for a gift: a nap counts. Nobody is woken for it.</summary>
        public static bool Asleep(ActivityKind k) => k == ActivityKind.Sleep || k == ActivityKind.Nap;

        /// <summary>Can someone walk over at this time, in a reaction of this length? Not after dark (as the answers:
        /// user 2026-10-30), so the walk has to be over by nightfall; from dawn on, it's light enough.</summary>
        public static bool CanWalkAt(ViewTime at, float seconds) =>
            at.hour >= LifeClock.Dawn && at.hour < LifeClock.Night && LifeClock.Advance(at, seconds - AtTheGift).hour < LifeClock.Night;

        /// <summary>What someone does at the gift: grown-ups, then kids.</summary>
        public static (ActivityKind grown, ActivityKind kids) AtGift(string kind) => kind switch
        {
            SimCore.NudgeKind.Rain => (ActivityKind.HandsOut, ActivityKind.Splash),
            SimCore.NudgeKind.Grove => (ActivityKind.Poke, ActivityKind.Play),
            SimCore.NudgeKind.Herd => (ActivityKind.Watch, ActivityKind.JumpAndPoint), // the deer, the goats, the fish
            _ => (ActivityKind.Watch, ActivityKind.Point),
        };

        /// <summary>The reaction as beats (relic-moment data). <paramref name="walk"/>: the walking cast (look, come over,
        /// look at it) with the onlookers; without it only the onlookers, for the night and for nobody near enough.
        /// The look up is wary in belief stage 1, as every reaction to the Keeper is then.</summary>
        public static RelicMomentDef For(string kind, int belief, float seconds, bool walk)
        {
            var d = new RelicMomentDef { relicId = null, seconds = seconds, maxKids = walk ? 2 : 0, maxGrown = walk ? 2 : 0, maxQueue = 3, standIns = false };
            float F(float s) => Math.Min(1f, s / seconds);
            void B(string id, RelicCast c, float from, float to, ActivityKind k, RelicWhere where)
                => d.beats.Add(new RelicBeat { id = id, cast = c, from = from, to = to, kind = k, where = where });
            var notice = belief <= 1 ? ActivityKind.LookUpWary : ActivityKind.LookUp;
            if (walk)
            {
                var (grown, kids) = AtGift(kind);
                float there = 1f - F(AtTheGift);
                void Walker(RelicCast c, float looks, ActivityKind k)
                {
                    B("look up", c, 0f, F(looks), notice, RelicWhere.Here);
                    B("come over", c, F(looks), there, ActivityKind.Point, RelicWhere.Spot);
                    B("look at it", c, there, 1f, k, RelicWhere.Spot);
                }
                Walker(RelicCast.Owner, FirstNotice, grown);
                Walker(RelicCast.Grown, OthersNotice, grown);
                Walker(RelicCast.Kids, OthersNotice, kids);
            }
            float look = F(FromAfar * 0.5f);
            B("look up", RelicCast.Onlookers, 0f, look, notice, RelicWhere.Here);
            B("point", RelicCast.Onlookers, look, F(FromAfar), ActivityKind.Point, RelicWhere.Here);
            return d;
        }
    }
}
