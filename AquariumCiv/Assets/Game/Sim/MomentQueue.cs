using System;

namespace AquariumCiv.Sim
{
    /// <summary>#369: the ceremonies that wait for the Keeper (visit-loop.md 2.1; research 16 §4.1).</summary>
    public enum MomentKind { Wedding, Newborn, NewLeader, Callback, Festival }

    /// <summary>#369: where a held moment is. Waiting in the queue, Playing in the box now, Played while watched, or Carded: it
    /// could not be staged (held too long, the queue overflowed, its people are gone, the baby grew, the view could not play it)
    /// and is told as a card instead. Its chronicle line is in the history whatever happens.</summary>
    public enum HeldMomentState { Waiting, Playing, Played, Carded }

    /// <summary>#369: why a moment became a card.</summary>
    public enum MomentCardReason { None, HeldTooLong, QueueFull, PeopleGone, BabyGrown, NotStaged, Cheat }

    /// <summary>#369: the size of the box, by its population (research 16 §4.2's rows).</summary>
    public enum MomentBoxSize { Tribe, Camp, Village, Town }

    /// <summary>#369: one ceremony held for the Keeper. The sim fact (the pairing, the birth, the new chief) already happened in
    /// the month it names; this is the scene that waits to be watched. Saved with the box.</summary>
    [Serializable]
    public class HeldMoment
    {
        public int id;
        public MomentKind kind;
        public int entryId = -1;            // the chronicle line that made it (the history keeps it either way)
        public int personA = -1;            // wedding: the two; newborn: the child, then the parent; new leader: the holder;
        public int personB = -1;            // a callback or a festival: whoever its line is about (-1 = none)
        public long month;                  // the game month it happened
        public double clockHours;           // SimState.clockHours when it happened (real hours)
        public float priority;              // the line's salience plus the kind's bonus: the top one plays first
        public HeldMomentState state;
        public MomentCardReason cardReason;
        public long endMonth = -1;          // the month it was played or became a card
        public double startedAt = -1;       // MomentPacing.watchedMinutes when it started playing
        public int batch = -1;              // MomentPacing.releases when it was released (a busy day releases 2 or 3 in one batch)
        public bool welcome;                // played as the welcome moment of an open
        public bool cheat;                  // queued by a cheat
        public int deferrals;               // t2-polish: times the view could not stage it yet (its people were out); it waited again
        public double retryAt;              // t2-polish: not handed over again before this watched minute (MomentPacing.watchedMinutes)
    }

    /// <summary>#369: the director's state, saved with the box. Its clock is the watched minutes: only live, watched play moves it,
    /// never an away catch-up.</summary>
    [Serializable]
    public class MomentPacing
    {
        public int nextId = 1;
        public double watchedMinutes;       // real minutes the box has been watched (the director's clock)
        public double credit = 1;           // moments owed: +1 per interval watched, capped (MomentRules.CreditCap); a release spends 1
        public double restUntil;            // no release before this watched minute (the rest after a moment)
        public int lastKind = -1;           // the kind released last in this visit (-1 = none yet): never the same kind twice in a row
        public int visits;                  // visits the director has counted
        public int visitPlayed;             // moments released in this visit
        public bool welcomeDue;             // this visit has not had its welcome moment yet, and one was waiting at the open
        public int releases;                // batches released so far (the busy-day roll's salt)
        public int lastLeaderId = -2;       // the era's leader at the end of last month (-2 = not known yet: an old save, a new box)
        public bool busyNext;               // cheat: the next release is a busy day if the box has two kinds waiting
    }

    /// <summary>
    /// #369: the director's numbers in one place (visit-loop.md 2.2; research 16 §4.2). All [est] until the diary test.
    /// Rate is a budget per watched minute: one moment per <see cref="IntervalMinutes"/>, linear in population between the rows;
    /// a rest of <see cref="RestMinutes"/> after each; at most <see cref="VisitCap"/> a visit; the queue keeps
    /// <see cref="QueueCap"/>, and the oldest beyond it becomes a card.
    /// </summary>
    public static class MomentRules
    {
        /// <summary>The rows (research 16 §4.2): a tribe up to 6 people, a camp to 10, a village to 22, then a town [est].</summary>
        public const int CampFromPop = 7, VillageFromPop = 10, TownFromPop = 22;

        /// <summary>Watched minutes per moment at these populations, linear between them, flat outside [est]:
        /// tribe ~1 per 15 min, camp ~1 per 10, village ~1 per 8, town ~1 per 6.</summary>
        static readonly int[] AnchorPop = { 6, 10, 22, 30 };
        static readonly double[] AnchorMinutes = { 15, 10, 8, 6 };

        /// <summary>A moment held longer than this (game months, 8 years ≈ 8 real hours) becomes a card: a long absence never
        /// freezes the world, and no old wedding is staged as new [est].</summary>
        public const int MaxHoldMonths = 8 * SimConfig.MonthsPerYear;
        /// <summary>A newborn is held up to the glass only while under 2 [est].</summary>
        public const int NewbornHoldMonths = 2 * SimConfig.MonthsPerYear;
        /// <summary>An open after at least this long away (real hours, 15 minutes) is a new visit (the same rule as the choices').</summary>
        public const double VisitAwayHours = 0.25;
        /// <summary>The budget saved up while nothing waits: one moment, so a quiet hour doesn't turn into a burst [est].</summary>
        public const double CreditCap = 1.0;
        /// <summary>A moment the view never finished (it lost track) ends by itself after this many watched minutes.</summary>
        public const double MaxPlayMinutes = 3.0;
        /// <summary>t2-polish: a moment the view could not stage yet (its people on a hunt, in the tent, asleep) waits again and is
        /// offered after this many watched minutes; after <see cref="MaxDeferrals"/> such tries it becomes a card [est].</summary>
        public const double RetryMinutes = 2.0;
        public const int MaxDeferrals = 6;
        /// <summary>A town's release is a "busy day" this often (2 or 3 kinds at once, a wedding during the market) [est].</summary>
        public const float BusyDayChance = 0.25f;
        public const int BusyDayMax = 3;
        /// <summary>Kind bonuses on the line's salience: the Keeper's choice coming back matters most (the user: "consequences of
        /// choices"), then a new leader [est].</summary>
        public const float CallbackBonus = 0.1f, NewLeaderBonus = 0.05f;
        /// <summary>Finished moments (played or carded) are kept this long, then forgotten (the chronicle keeps the line).</summary>
        public const int KeepFinishedMonths = 30 * SimConfig.MonthsPerYear;

        public static MomentBoxSize SizeOf(int population) =>
            population < CampFromPop ? MomentBoxSize.Tribe : population < VillageFromPop ? MomentBoxSize.Camp : population < TownFromPop ? MomentBoxSize.Village : MomentBoxSize.Town;

        /// <summary>Watched minutes per moment for this population: 15 / 10 / 8 / 6, linear between the rows.</summary>
        public static double IntervalMinutes(int population) => ByPopulation(population, AnchorMinutes);

        /// <summary>#367: the population curve itself, one value per row (tribe 6, camp 10, village 22, town 30), linear between the
        /// rows, flat outside. The background-life director (Life/StoryDirector.cs) scales on it too, so both get busy alike.</summary>
        public static double ByPopulation(int population, double[] atRows)
        {
            if (atRows == null || atRows.Length != AnchorPop.Length) throw new System.ArgumentException("one value per row", nameof(atRows));
            if (population <= AnchorPop[0]) return atRows[0];
            for (int i = 1; i < AnchorPop.Length; i++)
                if (population <= AnchorPop[i])
                {
                    double t = (population - AnchorPop[i - 1]) / (double)(AnchorPop[i] - AnchorPop[i - 1]);
                    return atRows[i - 1] + (atRows[i] - atRows[i - 1]) * t;
                }
            return atRows[atRows.Length - 1];
        }

        /// <summary>The rest after a moment (watched minutes): 8 / 6 / 4 / 3.</summary>
        public static double RestMinutes(MomentBoxSize size) => size switch
        {
            MomentBoxSize.Tribe => 8, MomentBoxSize.Camp => 6, MomentBoxSize.Village => 4, _ => 3,
        };

        /// <summary>At most this many moments a visit: a tribe 2, a camp 3, a village or a town no cap (the rate holds them).</summary>
        public static int VisitCap(MomentBoxSize size) => size switch
        {
            MomentBoxSize.Tribe => 2, MomentBoxSize.Camp => 3, _ => int.MaxValue,
        };

        /// <summary>How many wait at most: 2 / 3 / 4 / 5; the oldest beyond becomes a card.</summary>
        public static int QueueCap(MomentBoxSize size) => size switch
        {
            MomentBoxSize.Tribe => 2, MomentBoxSize.Camp => 3, MomentBoxSize.Village => 4, _ => 5,
        };

        /// <summary>A kind whose line stays a return card while its ceremony waits or after it played. None does now: #371 stages a
        /// callback as a live moment (the people go to the thing the choice left, the "Because the Keeper chose ..." line is its
        /// caption), so its card appears only if the moment becomes a card (held too long, queue full, could not be staged).</summary>
        public static bool AlsoACard(MomentKind kind) => false;

        public static float KindBonus(MomentKind kind) => kind == MomentKind.Callback ? CallbackBonus : kind == MomentKind.NewLeader ? NewLeaderBonus : 0f;

        /// <summary>The kind a chronicle line of this kind holds (a new leader is decided by the sim: it needs the seat). False if none.</summary>
        public static bool KindOfLine(string entryKind, out MomentKind kind)
        {
            switch (entryKind)
            {
                case "wedding": kind = MomentKind.Wedding; return true;
                case "birth": kind = MomentKind.Newborn; return true;
                case "festival": case "anniversary": kind = MomentKind.Festival; return true;
                case SimCore.CallbackKind: kind = MomentKind.Callback; return true;
                default: kind = MomentKind.Wedding; return false;
            }
        }

        /// <summary>Role lines that can seat a new leader (a won contest, an heir, a successor, a challenge won, one out of the
        /// crowd). Not a re-election, a step-down, a death, the era's own line (the era moment stages that) or the offices.</summary>
        public static bool SeatsALeader(string entryKind) => entryKind switch
        {
            "role_won" or "role_contest" or "role_inherited" or "role_succession" or "role_challenge" or "role_rise" or "role_reluctant" => true,
            _ => false,
        };
    }
}
