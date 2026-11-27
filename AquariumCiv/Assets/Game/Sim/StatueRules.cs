using System;
using System.Collections.Generic;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #776 (animation-specs B81.0): who the founder's statue is for and when the people raise it. Pure: no SimCore, no view, no dice.
    /// The statue is for the notable with the most pinned hours (at least <c>legendHours</c>, the "pinned for a day" legend rule), else the
    /// founder, the first notable of the box (the user's answer 1, 2026-11-26). It is raised once a box, a game year after that person has died
    /// (answer 2), in a village or a town of 12 or more people, in spring or summer. Never for the living.
    /// </summary>
    public static class StatueRules
    {
        /// <summary>How long after the death the people wait: one game year (the user's answer 2).</summary>
        public const int WaitMonths = SimConfig.MonthsPerYear;
        /// <summary>A grown village: this many people (<see cref="SimState.population"/>) [est: "a grown village"].</summary>
        public const int MinPeople = 12;

        /// <summary>The person the statue is for: the most pinned (at least <paramref name="legendHours"/>; ties: the lowest id), else the founder
        /// (the lowest id of the first generation: nobody's child), else null (an empty box). Alive or dead: <see cref="Ready"/> asks.</summary>
        public static Notable Who(IReadOnlyList<Notable> notables, Func<int, float> pinnedHours, float legendHours)
        {
            if (notables == null || notables.Count == 0) return null;
            Notable best = null; float bestHours = 0f;
            foreach (var n in notables)
            {
                float h = pinnedHours != null ? pinnedHours(n.id) : 0f;
                if (h < legendHours) continue;
                if (best == null || h > bestHours + 1e-4f || Math.Abs(h - bestHours) <= 1e-4f && n.id < best.id) { best = n; bestHours = h; }
            }
            return best ?? Founder(notables);
        }

        /// <summary>The first notable of the box: the lowest id among those nobody bore (the first generation); with none such, the lowest id.</summary>
        public static Notable Founder(IReadOnlyList<Notable> notables)
        {
            Notable first = null, any = null;
            foreach (var n in notables)
            {
                if (any == null || n.id < any.id) any = n;
                if (n.parentId < 0 && (first == null || n.id < first.id)) first = n;
            }
            return first ?? any;
        }

        /// <summary>Spring or summer by the calendar month (<see cref="SimCore.CalendarMonth"/>: spring 2-4, summer 5-7).</summary>
        public static bool SpringOrSummer(int calendarMonth) => calendarMonth >= 2 && calendarMonth <= 7;

        /// <summary>The people raise it now: nobody stands yet (<paramref name="raised"/> false), the person is dead and a game year has passed since,
        /// the box is a village or a town of <see cref="MinPeople"/> or more, and it is spring or summer.</summary>
        public static bool Ready(Notable who, long month, Era era, int people, int calendarMonth, bool raised)
        {
            if (raised || who == null || who.alive || who.diedMonth < 0) return false;
            if (month - who.diedMonth < WaitMonths) return false;
            if (era == Era.Camp || people < MinPeople) return false;
            return SpringOrSummer(calendarMonth);
        }

        /// <summary>What the figure carries (B81.1 "Its tell"): what the person was.</summary>
        public enum Tell { Nothing, Axe, Hammer, Rod, Basket, Hoe, Crook, Hand }

        /// <summary>The tell: a leader of the box (a chief or a headman: their term is in the history) the antlers' post's crook; else the job's tool
        /// (a woodcutter or toolmaker the axe, a mason or smith the hammer, a fisher the rod, a gatherer or beekeeper the basket, a farmer or
        /// shepherd the hoe); else a kind person the hand held out, palm up; else both hands at the sides.</summary>
        public static Tell TellOf(string job, string trait, bool wasLeader)
        {
            if (wasLeader) return Tell.Crook;
            switch (job)
            {
                case JobType.Woodcutter: case JobType.Toolmaker: return Tell.Axe;
                case JobType.Mason: case JobType.Smith: return Tell.Hammer;
                case JobType.Fisher: return Tell.Rod;
                case JobType.Gatherer: case JobType.Beekeeper: return Tell.Basket;
                case JobType.Farmer: case JobType.Shepherd: return Tell.Hoe;
            }
            return trait == "the kind" ? Tell.Hand : Tell.Nothing;
        }
    }
}
