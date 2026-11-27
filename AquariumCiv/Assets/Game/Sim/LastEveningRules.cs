using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #789 (docs/design/animation-specs.md B82.0, approved by the user 2026-11-26): when the last evening may play. Pure: no SimCore, no view,
    /// no dice. It never moves a death: it only reads the one the sim has already set (the yearly age roll, <see cref="DiesAtTheTurn"/>).
    /// It plays only while the Keeper watches, one elder at a time, at most once per visit and at least <see cref="SpacingMonths"/> apart, never
    /// while another held moment, ritual or choice plays (skipped, not queued), and only for an elder with kin or a friend free to sit with them.
    /// </summary>
    public static class LastEveningRules
    {
        /// <summary>After one has played, none for this long (two game years [est], so it never reads as a scheduled die-off).</summary>
        public const int SpacingMonths = 2 * SimConfig.MonthsPerYear;
        /// <summary>Up to this many kin or friends sit in close (B82.0), of whom at most <see cref="MaxKids"/> kids.</summary>
        public const int MaxKin = 4, MaxKids = 2;

        /// <summary>The sim has already set this elder's death for tonight: the next month is the year's turn (the age roll runs there,
        /// <c>SimCore.YearlyNotables</c>) and a year more reaches their lifespan. An elder: <paramref name="elderAge"/> or older.</summary>
        public static bool DiesAtTheTurn(Notable n, int nextCalendarMonth, float ageStep, float elderAge) =>
            n != null && n.alive && n.age >= elderAge && nextCalendarMonth == 0 && n.age + ageStep >= n.lifespan;

        /// <summary>Why the last evening can't play now (null: it may). <paramref name="lastMonth"/>/<paramref name="lastVisit"/>: when the
        /// last one played (−1: never); <paramref name="kinFree"/>: kin or friends free to come for the chosen elder.</summary>
        public static string Why(bool watched, bool otherPlaying, long month, long lastMonth, int visit, int lastVisit, int kinFree)
        {
            if (!watched) return "the Keeper is away (it never plays unseen)";
            if (otherPlaying) return "another moment is playing (skipped, not queued)";
            if (lastVisit >= 0 && visit == lastVisit) return "one already played this visit";
            if (lastMonth >= 0 && month - lastMonth < SpacingMonths) return "one played less than two years ago";
            if (kinFree <= 0) return "no kin or friend free";
            return null;
        }

        /// <summary>One elder of those in their last evening (B82.0): the pinned one first, else the one with the most kin or friends free,
        /// else the lowest id. Those with nobody free are never picked. Null when none is.</summary>
        public static Notable Pick(IReadOnlyList<(Notable who, int kinFree)> ready, int pinnedId)
        {
            Notable best = null; int bestKin = 0;
            foreach (var (who, kin) in ready)
            {
                if (who == null || kin <= 0) continue;
                if (who.id == pinnedId) return who;
                if (best == null || kin > bestKin || kin == bestKin && who.id < best.id) { best = who; bestKin = kin; }
            }
            return best;
        }

        /// <summary>The caption at the stars (B82 "Caption", the one line): it names the elder and who is with them, never a death or an end.</summary>
        public static string Caption(string who, string partner, bool family) =>
            $"{who} looks up at the stars with {(!string.IsNullOrEmpty(partner) ? partner : family ? "the family" : "friends")}.";
    }
}
