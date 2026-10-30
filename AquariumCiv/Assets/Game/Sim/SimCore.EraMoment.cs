using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>One leader's time at the head of the box (chief, headman or lord), for the book's chapters.</summary>
    [Serializable]
    public class LeaderTerm
    {
        public int notableId;
        public string role;
        public long fromMonth;
    }

    /// <summary>One chapter of the book (era-moment.md §7): an era, its years, its leaders and its 3–5 major events.</summary>
    public class BookChapter
    {
        public Era era;
        public string title;            // "The camp years (1–14)"
        public string eraTitle = "";    // the era's own title, "The Years of the Sky Bread" (written when the era ends, #73; "" while it goes on)
        public int fromYear, toYear;    // toYear = -1 while the era goes on
        public long fromMonth, toMonth; // toMonth = -1 while the era goes on
        public List<Notable> leaders = new();
        public List<ChronicleEntry> events = new();
    }

    /// <summary>What <see cref="SimCore.CompleteBox"/> hands to the shelf: the box's last line, the founders of the next
    /// box and the relics they carry.</summary>
    public class BoxCompletion
    {
        public int year;                // the year the box was finished (1-based, as the plate reads)
        public string plate;            // "Glasswick · years 1–118 · camp, village, town"
        public Notable lord;
        public Notable leader;          // the founders' leader: the next camp's first chief
        public List<Notable> founders = new(); // the leader first
        public List<string> relics = new();    // filled by the relics code (#74) through CollectRelicsForCrossing
        public string foundersLine;     // box 2's first card (kind "founders")
    }

    /// <summary>
    /// The era moment and the box completing (docs/design/era-moment.md §6–§10, #86). <see cref="AdvanceEra"/> stays the
    /// only way the era moves (the view calls it at the cheer beat); here it starts the lord line, tells the cleared hearth
    /// and opens a year of festival (a mood lift, no dice). Every month the leaders are logged for the book and the town's
    /// peak is checked; at the peak the box waits for the player, like an era, until <see cref="CompleteBox"/>.
    /// Nothing here draws from a random stream, so the economy's dice never shift.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>Raised once when the town reaches its peak (live or during catch-up).</summary>
        public event Action PeakReadied;

        /// <summary>The relics code (#74) adds the relics that travel with the founders.</summary>
        partial void CollectRelicsForCrossing(List<string> relics);

        /// <summary>The box's name for the view and the plate ("The settlement" until it is named).</summary>
        public string PlaceName => Place;

        // ---------- festival ----------
        /// <summary>The era festival runs for a game year after an era moment.</summary>
        public bool FestivalOn => State.festivalUntilMonth >= 0 && State.month < State.festivalUntilMonth;

        /// <summary>What the festival adds to the mood target this month (0 when none runs).</summary>
        public float FestivalMoodLift => FestivalOn ? Config.festivalMoodLift : 0f;

        // ---------- the era moment ----------
        /// <summary>Called by <see cref="AdvanceEra"/> after the roles moved on.</summary>
        void EraMomentOnChange()
        {
            var s = State;
            var leader = HolderOf(LeaderOf(s.era));
            if (s.era == Era.Town)
            {
                if (s.lordLineId < 0 && leader != null) s.lordLineId = leader.id; // the lordship passes in this family from now on
                if (s.buildings.Any(b => b.type == BuildingType.Camp))
                    Chronicle("era_cleared", "The old fire ring was cleared for a market. Someone kept the stone.", 0.6f);
            }
            LogLeader();
            s.festivalUntilMonth = s.month + Config.festivalYears * SimConfig.MonthsPerYear;
            Chronicle("era_festival", FestivalLine(leader), 0.7f, leader?.id ?? -1);
        }

        string FestivalLine(Notable leader)
        {
            string era = EraNoun(State.era);
            string tail = leader == null ? "" : State.era == Era.Village
                ? $" {leader.name} fell in the well (they're fine)."
                : $" {leader.name} danced on the market steps until the lanterns went out.";
            return $"{Place} danced for three days to welcome the new {era}.{tail}";
        }

        /// <summary>The lord, while the town has one; the founder of the line is <see cref="SimState.lordLineId"/>.</summary>
        public Notable Lord => State.era == Era.Town ? HolderOf(RoleType.Lord) : null;

        /// <summary>Who stands behind the lord's chair: the eldest living child (any age), else the next of the blood.</summary>
        public Notable LordHeir
        {
            get
            {
                var lord = Lord;
                if (lord == null) return null;
                var child = State.notables.Where(n => n.alive && n.parentId == lord.id)
                    .OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault();
                return child ?? Heir(lord).heir;
            }
        }

        // ---------- the monthly step ----------
        /// <summary>End of every month: log a new leader, track the box filling, mark the peak ready once.</summary>
        void PeakStep()
        {
            var s = State;
            if (s.boxComplete) return;
            LogLeader();
            bool full = s.buildings.Count >= Config.buildingSlots || (Settlement != null && Settlement.Waiting() > 0);
            if (!full) s.fullSinceMonth = -1;
            else if (s.fullSinceMonth < 0) s.fullSinceMonth = s.month;
            if (s.era != Era.Town) return;
            s.townSeatsHeld ??= new List<string>();
            foreach (var seat in s.roles)
                if (seat.holderId >= 0 && !s.townSeatsHeld.Contains(seat.role)) s.townSeatsHeld.Add(seat.role);
            if (!s.peakReady && MeetsPeak) MarkPeakReady();
        }

        /// <summary>Game years the box has been full (0 when it isn't).</summary>
        public int YearsFull => State.fullSinceMonth < 0 ? 0 : (int)((State.month - State.fullSinceMonth) / SimConfig.MonthsPerYear);

        /// <summary>The peak rule (<see cref="SimConfig.peakAt"/>): a town old enough, every seat held once, the box full.</summary>
        public bool MeetsPeak
        {
            get
            {
                var r = Config.peakAt;
                return State.era == Era.Town && YearsInEra >= r.townYears && (State.townSeatsHeld?.Count ?? 0) >= r.seats && YearsFull >= r.fullYears;
            }
        }

        void MarkPeakReady()
        {
            State.peakReady = true;
            State.peakReadyMonth = State.month;
            Chronicle("peak-ready", $"{Place} has filled its glass from wall to wall. The old folk say it has never looked finer.", 0.9f);
            PeakReadied?.Invoke();
        }

        void LogLeader()
        {
            var s = State;
            s.leaders ??= new List<LeaderTerm>();
            var seat = SeatOf(LeaderOf(s.era));
            if (seat == null || seat.holderId < 0) return;
            var last = s.leaders.Count > 0 ? s.leaders[s.leaders.Count - 1] : null;
            if (last != null && last.notableId == seat.holderId && last.role == seat.role) return;
            s.leaders.Add(new LeaderTerm { notableId = seat.holderId, role = seat.role, fromMonth = seat.sinceMonth });
        }

        // ---------- the box completing ----------
        /// <summary>The last moment's end: writes the box-complete line, picks the founders of the next box (Choice 10) and
        /// the relics they carry. Only once the peak is ready; returns null otherwise or if the box is already complete.</summary>
        public BoxCompletion CompleteBox()
        {
            var s = State;
            if (!s.peakReady || s.boxComplete) return null;
            var lord = Lord;
            int year = YearNo(s.month);
            var r = new BoxCompletion { year = year, lord = lord, plate = $"{Place} · years 1–{year} · camp, village, town" };
            r.founders = PickFounders(lord);
            r.leader = r.founders.FirstOrDefault();
            CollectRelicsForCrossing(r.relics);

            Chronicle("box-complete", lord != null
                ? $"{Place} was finished in year {year}. Lord {lord.FullName} lit the last lantern. The town lives on, on the Keeper's shelf."
                : $"{Place} was finished in year {year}. The last lantern was lit on the market steps. The town lives on, on the Keeper's shelf.",
                1.0f, lord?.id ?? -1);
            if (r.leader != null)
            {
                var others = r.founders.Skip(1).Select(n => n.family).Distinct().ToList();
                string with = others.Count == 0 ? "" : others.Count == 1 ? $" with the {others[0]} family" : $" with the {others[0]} and {others[1]} families";
                string who = lord != null && r.leader.parentId == lord.id ? $"The lord's younger child, {r.leader.FullName}," : r.leader.FullName;
                var e = Chronicle("founders_left", $"{who} left{with} to found a new home across the shelf. {Place} waved them off from the glass.", 0.9f, r.leader.id);
                if (r.founders.Count > 1) e.otherNotableId = r.founders[1].id;
                string carrying = r.relics.Count > 0 ? $"carrying the {r.relics[0]} wrapped in cloth" : $"carrying a little of {Place} wrapped in cloth";
                r.foundersLine = $"{r.leader.FullName} and {(others.Count == 1 ? "a young family" : "two young families")} crossed to a new land, {carrying}.";
            }
            s.boxComplete = true;
            s.boxCompleteMonth = s.month;
            s.founderIds = r.founders.Select(n => n.id).ToList();
            return r;
        }

        /// <summary>Choice 10: the lord's younger child (one who won't inherit) leads, else a young adult of the lord's
        /// family, else the youngest free adult; two young notables of two other families go with them.</summary>
        List<Notable> PickFounders(Notable lord)
        {
            var heir = LordHeir;
            bool Free(Notable n) => n.alive && n.age >= AdultAge && n != lord && n != heir;
            var adults = State.notables.Where(Free).ToList();
            var lead = lord == null ? null : adults.Where(n => n.parentId == lord.id).OrderByDescending(n => n.age).ThenBy(n => n.id).FirstOrDefault()
                ?? adults.Where(n => n.family == lord.family).OrderBy(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            lead ??= adults.Where(n => RoleOf(n) == null).OrderBy(n => n.age).ThenBy(n => n.id).FirstOrDefault()
                     ?? adults.OrderBy(n => n.age).ThenBy(n => n.id).FirstOrDefault();
            var founders = new List<Notable>();
            if (lead == null) return founders;
            founders.Add(lead);
            var families = new HashSet<string> { lead.family };
            // Young families: free adults without an office first, youngest first; one per family.
            foreach (var n in adults.OrderBy(n => RoleOf(n) == null ? 0 : 1).ThenBy(n => n.age).ThenBy(n => n.id))
            {
                if (founders.Count >= 3) break;
                if (families.Add(n.family)) founders.Add(n);
            }
            return founders;
        }

        // ---------- the book ----------
        /// <summary>The player-facing year of a month: year 1 is the founding year.</summary>
        public static int YearNo(long month) => (int)(month / SimConfig.MonthsPerYear) + 1;

        /// <summary>One chapter per era so far (era-moment.md §7): its years, its leaders and its 3–5 major events.</summary>
        public List<BookChapter> BookChapters(int maxEvents = 5)
        {
            var s = State;
            var starts = new List<long> { 0 };
            starts.AddRange(s.chronicle.Where(e => e.kind == "era").Select(e => e.month).Take((int)s.era));
            var chapters = new List<BookChapter>();
            for (int i = 0; i <= (int)s.era && i < starts.Count; i++)
            {
                var era = (Era)i;
                long from = starts[i], to = i + 1 < starts.Count ? starts[i + 1] : -1;
                var c = new BookChapter { era = era, fromMonth = from, toMonth = to, fromYear = YearNo(from), toYear = to < 0 ? -1 : YearNo(to) };
                string years = to < 0 ? $"{c.fromYear}–…" : $"{c.fromYear}–{c.toYear}";
                c.title = $"The {EraNoun(era)} years ({years})";
                c.eraTitle = EraTitle(era);
                var ids = new List<int>();
                for (int k = 0; k < s.leaders.Count; k++)
                {
                    var t = s.leaders[k];
                    long end = k + 1 < s.leaders.Count ? s.leaders[k + 1].fromMonth : long.MaxValue;
                    bool overlaps = t.fromMonth <= (to < 0 ? long.MaxValue : to) && end >= from && t.role == LeaderOf(era);
                    if (overlaps && !ids.Contains(t.notableId)) ids.Add(t.notableId);
                }
                c.leaders = ids.Select(FindNotable).Where(n => n != null).ToList();
                // Major events: the most salient lines of the era (the era's own lines and hidden kinds aside), told in order.
                c.events = s.chronicle
                    .Where(e => e.month >= from && (to < 0 || e.month < to) && e.salience >= 0.75f && !ChapterSkips(e.kind))
                    .OrderByDescending(e => e.salience).ThenBy(e => e.month).Take(maxEvents)
                    .OrderBy(e => e.month).ThenBy(e => e.id).ToList();
                chapters.Add(c);
            }
            return chapters;
        }

        static bool ChapterSkips(string kind) => kind == "era" || kind == "era-ready" || kind == "coming_of_age" || kind == "box-complete";

        // ---------- debug (debug panel: peak ready now, complete the box now) ----------
        /// <summary>Debug: the town's peak is ready now, whatever the rule says (only in the town).</summary>
        public bool ForcePeakReady()
        {
            if (State.peakReady || State.boxComplete || State.era != Era.Town) return false;
            MarkPeakReady();
            return true;
        }

        /// <summary>Debug: complete the box now (crosses to the town and marks the peak first if needed).</summary>
        public BoxCompletion ForceCompleteBox()
        {
            if (State.boxComplete) return null;
            while (!IsFinalEra) ForceNextEra();
            if (!State.peakReady) MarkPeakReady();
            return CompleteBox();
        }

        /// <summary>Debug: end the era festival now.</summary>
        public void DebugEndFestival() => State.festivalUntilMonth = State.month;
    }
}
