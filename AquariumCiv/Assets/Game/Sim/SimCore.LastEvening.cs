using System;
using System.Collections.Generic;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #789 (docs/design/animation-specs.md B82, approved by the user 2026-11-26): the last evening, the sim side. The sim already decides an
    /// elder's death (the yearly age roll); on the dusk before the night it comes, while the Keeper watches, the view may play one goodbye at the
    /// fire. This side says who is in their last evening (<see cref="LastEveningElders"/>), who of them gets it (<see cref="ChooseLastEvening"/>:
    /// the rules are <see cref="LastEveningRules"/>) and stamps it (<see cref="MarkLastEvening"/>: <see cref="SimState.lastEveningFor"/>, the
    /// month, the view day, the visit, and one History line in its own id range). The death itself is never moved, nothing is said or carded
    /// while the Keeper is away, and no dice are drawn.
    /// </summary>
    public partial class SimCore
    {
        /// <summary>The chronicle kind of the line (History only: it was watched, so it is never a return card).</summary>
        public const string LastEveningKind = "lastevening";
        /// <summary>The lines' own ids, above the shared counter, the glance lines (1.6e9) and the statue's (1.7e9): a line only some boxes get
        /// must not shift the shared ids (and so the id-keyed dice) of everything after it (the #788 lesson).</summary>
        public const int LastEveningLineIdBase = 1_800_000_000;

        /// <summary>Debug ("Last evening" cheats): elders whose death a cheat set for the next month, whatever the calendar. Not saved.</summary>
        readonly HashSet<int> debugDiesNextMonth = new HashSet<int>();

        /// <summary>The next month is the year's turn, where the age roll runs.</summary>
        int NextCalendarMonth => SeasonCalendar.OfMonth(State.month + 1, State.seasonShift);

        /// <summary>The sim has set this elder's death for tonight (the age roll at the year's turn, or a cheat's).</summary>
        public bool DiesTonight(Notable n) =>
            n != null && n.alive && (debugDiesNextMonth.Contains(n.id) && n.age >= Config.elderAge || LastEveningRules.DiesAtTheTurn(n, NextCalendarMonth, Config.notableAgeFactor, Config.elderAge));

        /// <summary>The elders in their last evening now (alive, in the box, the death set for tonight), lowest id first.</summary>
        public List<Notable> LastEveningElders() => State.notables.Where(DiesTonight).OrderBy(n => n.id).ToList();

        /// <summary>Who may sit with them (B82.0 "Who takes part"), living, in order: the partner, children, grandchildren, brothers and
        /// sisters, then friends (a friend link either way).</summary>
        public List<int> KinAndFriendsOf(Notable n)
        {
            var ids = new List<int>();
            if (n == null) return ids;
            void Add(Notable k) { if (k != null && k.alive && k.id != n.id && !ids.Contains(k.id)) ids.Add(k.id); }
            Add(Alive(n.partnerId));
            var kids = AllChildrenOf(n.id);
            foreach (var c in kids) Add(c);
            foreach (var c in kids) foreach (var g in AllChildrenOf(c.id)) Add(g);
            foreach (var sib in SiblingsOf(n)) Add(sib);
            Add(Alive(n.friendId));
            foreach (var o in State.notables) if (o.friendId == n.id) Add(o);
            return ids;
        }

        /// <summary>The ties that make it family (not only friends): the partner, children, grandchildren, brothers and sisters.</summary>
        public bool IsKinOf(Notable n, int otherId)
        {
            if (n == null) return false;
            var o = FindNotable(otherId);
            if (o == null) return false;
            if (n.partnerId == otherId || o.partnerId == n.id) return true;
            var kids = AllChildrenOf(n.id);
            if (kids.Any(c => c.id == otherId) || kids.Any(c => AllChildrenOf(c.id).Any(g => g.id == otherId))) return true;
            return SiblingsOf(n).Any(s => s.id == otherId);
        }

        /// <summary>The month, view day, view hour and visit the last one played (−1: never); whose it was.</summary>
        public int LastEveningFor => State.lastEveningFor;

        /// <summary>Why no last evening can play now, for the elder <paramref name="who"/> with <paramref name="kinFree"/> kin or friends free (null: it may).</summary>
        public string LastEveningWhy(Notable who, int kinFree, bool watched, bool otherPlaying)
        {
            if (who == null || !DiesTonight(who)) return "nobody is in their last evening";
            if (CatchingUp) watched = false;
            return LastEveningRules.Why(watched, otherPlaying, State.month, State.lastEveningMonth, MomentPacing.visits, State.lastEveningVisit, kinFree);
        }

        /// <summary>
        /// The elder who gets the last evening now, or null (<paramref name="why"/> says why): of the elders in their last evening, those with kin
        /// or a friend free (<paramref name="free"/>: the view's "drawn and free now"), the pinned one first, else the one with the most free. One
        /// per visit, two game years apart, only while <paramref name="watched"/>, never over another held moment (<paramref name="otherPlaying"/>).
        /// </summary>
        public Notable ChooseLastEvening(Func<int, bool> free, bool watched, bool otherPlaying, out string why)
        {
            var ready = new List<(Notable, int)>();
            foreach (var n in LastEveningElders()) ready.Add((n, KinAndFriendsOf(n).Count(k => free == null || free(k))));
            var who = LastEveningRules.Pick(ready, State.pinnedNotableId);
            int kin = who == null ? 0 : ready.First(r => r.Item1 == who).Item2;
            if (who == null && ready.Count > 0) { why = "no kin or friend free"; return null; }
            why = LastEveningWhy(who, kin, watched, otherPlaying);
            return why == null ? who : null;
        }

        /// <summary>
        /// The view plays it now: stamps who (the elder stays in from <paramref name="viewDay"/> at <paramref name="indoorsFrom"/> view hours until
        /// the night takes them), the month and the visit (the spacing), and writes the one line to History (its own id; marked read: it was seen).
        /// <paramref name="with"/>: who sits with them (the line names the partner, else "the family", else "friends").
        /// </summary>
        public ChronicleEntry MarkLastEvening(Notable who, IReadOnlyList<int> with, long viewDay, float indoorsFrom)
        {
            if (who == null) return null;
            var s = State;
            s.lastEveningFor = who.id; s.lastEveningMonth = s.month; s.lastEveningVisit = MomentPacing.visits;
            s.lastEveningDay = viewDay; s.lastEveningHour = indoorsFrom;
            var partner = with != null && with.Contains(who.partnerId) ? Alive(who.partnerId) : null;
            bool family = with != null && with.Any(k => IsKinOf(who, k));
            if (s.lastEveningNextId < LastEveningLineIdBase) s.lastEveningNextId = LastEveningLineIdBase;
            var e = RecordAs(s.lastEveningNextId++, LastEveningKind, LastEveningRules.Caption(who.name, partner?.name, family), 0.5f, who.id, partner?.id ?? -1);
            var c = s.cards ??= new CardLedger();
            if (!c.read.Contains(e.id)) c.read.Add(e.id); // watched: History only, never a return card
            return e;
        }

        /// <summary>The elder of the last evening is still alive and stays in (the planner keeps them indoors from the stamp on).</summary>
        public bool StaysInAfterLastEvening(int notableId, long viewDay, float hour, out bool allDay)
        {
            allDay = false;
            var s = State;
            if (s.lastEveningFor < 0 || notableId != s.lastEveningFor || Alive(notableId) == null || viewDay < s.lastEveningDay) return false;
            allDay = viewDay > s.lastEveningDay;
            return allDay || hour >= s.lastEveningHour;
        }

        /// <summary>From <see cref="Step"/>: a cheat's death for tonight comes as the age roll's would (the same line). Nothing without a cheat.</summary>
        void DebugDeathTonight()
        {
            if (debugDiesNextMonth.Count == 0) return;
            foreach (int id in debugDiesNextMonth.OrderBy(x => x))
            {
                var n = Alive(id);
                if (n == null) continue;
                n.alive = false;
                n.diedMonth = State.month;
                Chronicle("death", $"{n.FullName}, {n.trait}, died of old age at {Math.Floor(n.age)}.", 0.85f, n.id);
            }
            debugDiesNextMonth.Clear();
        }

        // ---------- debug (SliceDebugPanel's "Last evening (#789)" block) ----------

        /// <summary>Debug: the spacing and the visit are forgotten, so the next last evening may play now.</summary>
        public void DebugLastEveningClear()
        {
            State.lastEveningMonth = -1; State.lastEveningVisit = -1;
            State.lastEveningFor = -1; State.lastEveningDay = -1;
        }

        /// <summary>
        /// Debug: an elder in their last evening tonight. <paramref name="kin"/>: the oldest elder with kin (else a person is made 62 and given a
        /// partner); false: an elder with no kin and no friend (a newcomer of 62). Their death is set for the next month, whatever the calendar.
        /// <paramref name="not"/>: an elder not to take (the second of two). Returns who, or null and why.
        /// </summary>
        public Notable DebugLastEveningElder(bool kin, out string why, int not = -1)
        {
            why = "";
            Notable n = null;
            if (kin)
            {
                n = State.notables.Where(x => x.alive && x.id != not && x.age >= Config.elderAge && KinAndFriendsOf(x).Count > 0)
                    .OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault();
                if (n == null)
                {
                    n = State.notables.Where(x => x.alive && x.id != not && x.age >= AdultAge).OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault();
                    if (n == null) { why = "nobody grown in the box"; return null; }
                    if (n.age < 62f) n.age = 62f;
                    if (KinAndFriendsOf(n).Count == 0)
                    {
                        var p = State.notables.Where(x => x.alive && x.id != n.id && x.id != not && x.age >= AdultAge && Alive(x.partnerId) == null)
                            .OrderByDescending(x => x.age).ThenBy(x => x.id).FirstOrDefault();
                        if (p == null) { why = "nobody free to be their partner"; return null; }
                        n.partnerId = p.id; p.partnerId = n.id;
                    }
                }
            }
            else
            {
                n = AddNotable(62f, -1, null);
                n.friendId = -1;
            }
            n.lifespan = Math.Max(n.lifespan, n.age + 0.5f);
            debugDiesNextMonth.Add(n.id);
            return n;
        }

        /// <summary>Debug ("two elders at once"): two elders with kin in their last evening tonight; only one scene plays. Who, or null and why.</summary>
        public (Notable a, Notable b) DebugTwoLastEvenings(out string why)
        {
            var a = DebugLastEveningElder(true, out why);
            if (a == null) return (null, null);
            var b = DebugLastEveningElder(true, out why, a.id);
            return (a, b);
        }
    }
}
