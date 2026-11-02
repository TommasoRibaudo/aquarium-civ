using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.View.ShowMe
{
    /// <summary>Someone in the box the moment the glass is knocked. <c>reacts</c> is what the planner decided
    /// (<c>LifePlanner.Reacts</c>: not a baby, awake, in range); this class never overrides it.</summary>
    public sealed class KnockWitness
    {
        public int id, voice;
        public bool reacts;
        /// <summary>Distance to the knock, any unit (nearest first when more react than marks are allowed).</summary>
        public float distance;
    }

    /// <summary>One person's beat: the <c>!</c> pops at <c>bangAt</c>, the body starts to turn at <c>bodyAt</c>.</summary>
    public sealed class KnockBeat
    {
        public int id, voice;
        public double bangAt, bodyAt, holdUntil, fadeUntil;
    }

    /// <summary>
    /// The knock on the glass (#161, research 12 §4): a <c>!</c> over each person who reacts, popping 0.15 s before the body
    /// turns, held 1.2 s, then a short fade. Pure timing and the choice of who gets one: hook it where the planner's reaction
    /// to <c>KeeperGesture.TapGlass</c> starts (<c>LifePlanner.Reacts</c> decides who reacts; the stage-3 tap-back scholar
    /// goes through the same call). At most <see cref="MaxMarks"/> so a crowded camp doesn't flash a wall of them: the
    /// nearest to the knock, ties by id.
    /// </summary>
    public static class KnockReaction
    {
        public const float LeadSeconds = 0.15f, HoldSeconds = 1.2f, FadeSeconds = 0.25f, StaggerSeconds = 0.04f;
        public const int MaxMarks = 4;

        /// <param name="knockAt">Real time of the knock. The first mark pops at once; its body turns <see cref="LeadSeconds"/> later.</param>
        public static List<KnockBeat> Plan(double knockAt, IEnumerable<KnockWitness> witnesses)
        {
            var who = witnesses.Where(w => w.reacts).OrderBy(w => w.distance).ThenBy(w => w.id).Take(MaxMarks).ToList();
            var res = new List<KnockBeat>(who.Count);
            for (int i = 0; i < who.Count; i++)
            {
                double pop = knockAt + i * StaggerSeconds;
                res.Add(new KnockBeat
                {
                    id = who[i].id, voice = who[i].voice, bangAt = pop, bodyAt = pop + LeadSeconds,
                    holdUntil = pop + LeadSeconds + HoldSeconds, fadeUntil = pop + LeadSeconds + HoldSeconds + FadeSeconds,
                });
            }
            return res;
        }

        /// <summary>Seconds the body's reaction waits so it starts after the mark (0 once past).</summary>
        public static double BodyDelay(KnockBeat b, double now) => Math.Max(0.0, b.bodyAt - now);
    }
}
