using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AquariumCiv.Sim
{
    /// <summary>
    /// #369: the moment queue and its director (visit-loop.md 2.1-2.2; research 16 §4.1-4.2). "The sim keeps living while you're
    /// away; the moments wait for you."
    /// <list type="bullet">
    /// <item><b>The queue.</b> A wedding, a birth, a new leader, a choice's callback or a festival still happens on time in the sim
    /// and writes its chronicle line as before; its <i>ceremony</i> is queued (<see cref="HeldMoment"/>: kind, people, when,
    /// priority). While it waits, or once played, its line is not a return card; it is a card only if it never gets staged.
    /// (A callback is staged too since #371: <see cref="MomentRules.AlsoACard"/> is false for every kind.)</item>
    /// <item><b>The director</b> plays them only while the player watches, on a budget per watched minute (never per open):
    /// <see cref="MomentRules"/> has the rates by population, the rest after each, the cap per visit, the queue's cap. Never two
    /// of a kind in a row within a visit. The top waiting moment plays on the open (the welcome). A town now and then has a busy
    /// day (2 or 3 kinds at once). A moment held over 8 game years (a newborn: 2) becomes a card.</item>
    /// <item><b>The play contract</b> (the view): <see cref="StartDueMoments"/> hands over what to play now (marked Playing),
    /// the view stages each and calls <see cref="FinishMoment"/> when it is over (or could not be staged: then it is a card).
    /// Nothing plays during an away catch-up, during the first run, or while one is still playing.</item>
    /// </list>
    /// No dice from any stream (the busy-day roll is a hash). Nothing at all unless <see cref="SimConfig.moments"/>.
    /// </summary>
    public partial class SimCore
    {
        // Not saved: a load starts a new visit (as the choices' BeginVisit does).
        bool momentVisitLive;
        double momentAwayHours;
        // entry id -> its moment (rebuilt when the list changes)
        Dictionary<int, HeldMoment> momentByEntry;
        List<HeldMoment> momentIndexed;
        int momentIndexCount = -1, momentIndexFirst = -1, momentIndexLast = -1;

        public bool MomentsOn => Config.moments;
        List<HeldMoment> Moments => State.moments ??= new List<HeldMoment>();
        public MomentPacing MomentPacing => State.momentPacing ??= new MomentPacing();
        public MomentBoxSize MomentSize => MomentRules.SizeOf(State.population);

        /// <summary>The moments waiting, the top first (priority, then the oldest, then the id).</summary>
        public List<HeldMoment> WaitingMoments() =>
            Moments.Where(m => m.state == HeldMomentState.Waiting).OrderByDescending(m => m.priority).ThenBy(m => m.month).ThenBy(m => m.id).ToList();

        /// <summary>The moments the view is playing now.</summary>
        public List<HeldMoment> PlayingMoments() => Moments.Where(m => m.state == HeldMomentState.Playing).ToList();

        public HeldMoment FindMoment(int id) => Moments.FirstOrDefault(m => m.id == id);

        /// <summary>The moment made by this chronicle line (null if none).</summary>
        public HeldMoment MomentOfEntry(int entryId)
        {
            var list = Moments;
            int firstId = list.Count > 0 ? list[0].id : -1, lastId = list.Count > 0 ? list[list.Count - 1].id : -1;
            if (!ReferenceEquals(list, momentIndexed) || list.Count != momentIndexCount || firstId != momentIndexFirst || lastId != momentIndexLast || momentByEntry == null)
            {
                momentByEntry = new Dictionary<int, HeldMoment>();
                foreach (var m in list) if (m.entryId >= 0) momentByEntry[m.entryId] = m;
                momentIndexed = list;
                momentIndexCount = list.Count;
                momentIndexFirst = firstId;
                momentIndexLast = lastId;
            }
            return momentByEntry.TryGetValue(entryId, out var x) ? x : null;
        }

        /// <summary>The line's ceremony is waiting, playing or was played: it is not a return card (it is, or will be, seen in the
        /// box). A line whose moment became a card is a card as usual.</summary>
        public bool MomentHidesCard(ChronicleEntry e)
        {
            if (e == null || State.moments == null || State.moments.Count == 0) return false;
            var m = MomentOfEntry(e.id);
            return m != null && m.state != HeldMomentState.Carded && !MomentRules.AlsoACard(m.kind);
        }

        /// <summary>The line's ceremony still waits (or plays): the card ledger must not move past it, so it can still become a card.</summary>
        public bool MomentStillHeld(ChronicleEntry e)
        {
            if (e == null || State.moments == null || State.moments.Count == 0) return false;
            var m = MomentOfEntry(e.id);
            return m != null && (m.state == HeldMomentState.Waiting || m.state == HeldMomentState.Playing) && !MomentRules.AlsoACard(m.kind);
        }

        // ---------- the queue: the hooks ----------

        /// <summary>From <see cref="Record"/>: a ceremony-worthy line queues its moment. The fact itself already happened.</summary>
        void MomentFromLine(ChronicleEntry e)
        {
            if (!Config.moments || e == null) return;
            if (MomentRules.KindOfLine(e.kind, out var kind))
            {
                int a = e.notableId, b = e.otherNotableId;
                if (kind == MomentKind.Newborn) b = FindNotable(e.notableId)?.parentId ?? -1; // the child, then the parent who holds them up
                QueueMoment(kind, e, a, b);
                return;
            }
            if (e.kind != null && e.kind.StartsWith("role_") && MomentRules.SeatsALeader(e.kind) && e.notableId >= 0)
            {
                var p = MomentPacing;
                int holder = SeatOf(LeaderOf(State.era))?.holderId ?? -1;
                // Unknown until the first month ends (an old save, a new box): no moment rather than a wrong one.
                if (p.lastLeaderId == -2 || holder != e.notableId || holder == p.lastLeaderId) return;
                p.lastLeaderId = holder;
                QueueMoment(MomentKind.NewLeader, e, holder, -1);
            }
        }

        HeldMoment QueueMoment(MomentKind kind, ChronicleEntry e, int a, int b, bool cheat = false)
        {
            var p = MomentPacing;
            var m = new HeldMoment
            {
                id = p.nextId++, kind = kind, entryId = e?.id ?? -1, personA = a, personB = b, month = State.month,
                clockHours = Math.Round(State.clockHours, 6), priority = (e?.salience ?? 0.5f) + MomentRules.KindBonus(kind), cheat = cheat,
            };
            Moments.Add(m);
            TrimMomentQueue();
            return m;
        }

        /// <summary>The queue keeps <see cref="MomentRules.QueueCap"/>: the oldest beyond becomes a card ("older ones become cards").</summary>
        void TrimMomentQueue()
        {
            if (momentNoTrim) return;
            int cap = MomentRules.QueueCap(MomentSize);
            var waiting = Moments.Where(m => m.state == HeldMomentState.Waiting).OrderBy(m => m.month).ThenBy(m => m.id).ToList();
            for (int i = 0; i < waiting.Count - cap; i++) CardMoment(waiting[i], MomentCardReason.QueueFull);
        }

        void CardMoment(HeldMoment m, MomentCardReason why)
        {
            m.state = HeldMomentState.Carded;
            m.cardReason = why;
            m.endMonth = State.month;
        }

        /// <summary>Still stageable: its people are alive, a newborn is under 2, a new leader still holds the seat.</summary>
        MomentCardReason MomentStale(HeldMoment m)
        {
            long age = State.month - m.month;
            if (age > MomentRules.MaxHoldMonths) return MomentCardReason.HeldTooLong;
            switch (m.kind)
            {
                case MomentKind.Wedding:
                    if (Alive(m.personA) == null || Alive(m.personB) == null) return MomentCardReason.PeopleGone;
                    break;
                case MomentKind.Newborn:
                    if (age > MomentRules.NewbornHoldMonths) return MomentCardReason.BabyGrown;
                    if (Alive(m.personA) == null) return MomentCardReason.PeopleGone;
                    break;
                case MomentKind.NewLeader:
                    if (Alive(m.personA) == null || (SeatOf(LeaderOf(State.era))?.holderId ?? -1) != m.personA) return MomentCardReason.PeopleGone;
                    break;
            }
            return MomentCardReason.None;
        }

        /// <summary>Every month (live or catching up): the old and the stale become cards, the queue keeps its cap, the leader is
        /// noted, and long-finished moments are forgotten. No dice.</summary>
        void MomentStep()
        {
            if (!Config.moments) return;
            var s = State;
            var p = MomentPacing;
            p.lastLeaderId = SeatOf(LeaderOf(s.era))?.holderId ?? -1;
            var list = Moments;
            if (list.Count == 0) return;
            foreach (var m in list)
            {
                if (m.state != HeldMomentState.Waiting) continue;
                var why = MomentStale(m);
                if (why != MomentCardReason.None) CardMoment(m, why);
            }
            TrimMomentQueue();
            list.RemoveAll(m => (m.state == HeldMomentState.Played || m.state == HeldMomentState.Carded) && s.month - m.endMonth > MomentRules.KeepFinishedMonths);
        }

        // ---------- the director's clock ----------

        /// <summary>From <see cref="Advance"/> when watched: the director's budget grows with the watched minutes.</summary>
        void MomentWatched(double realHours)
        {
            if (!Config.moments || realHours <= 0) return;
            EnsureMomentVisit();
            var p = MomentPacing;
            double minutes = realHours * 60.0;
            p.watchedMinutes = Math.Round(p.watchedMinutes + minutes, 9);
            p.credit = Math.Min(MomentRules.CreditCap, p.credit + minutes / MomentRules.IntervalMinutes(State.population));
            // A moment the view lost track of ends by itself (played: it was handed over while watched).
            foreach (var m in Moments)
                if (m.state == HeldMomentState.Playing && p.watchedMinutes - m.startedAt > MomentRules.MaxPlayMinutes) FinishMoment(m.id, true);
        }

        /// <summary>From <see cref="CatchUp"/>: the player was away. Nothing plays and no budget grows; the next watched moment
        /// starts a new visit if the absence was long enough.</summary>
        void MomentAway(double realHours)
        {
            if (!Config.moments) return;
            momentAwayHours += Math.Max(0, realHours);
        }

        /// <summary>A new visit (the first watched moment of this sim, or after <see cref="MomentRules.VisitAwayHours"/> away): the
        /// visit's count and kind memory reset, a moment cut off by the absence goes back to waiting, and the welcome is due if
        /// anything waits.</summary>
        void EnsureMomentVisit()
        {
            bool fresh = !momentVisitLive || momentAwayHours >= MomentRules.VisitAwayHours - 1e-9;
            momentAwayHours = 0;
            if (!fresh) return;
            momentVisitLive = true;
            var p = MomentPacing;
            p.visits++;
            p.visitPlayed = 0;
            p.lastKind = -1;
            foreach (var m in Moments)
                if (m.state == HeldMomentState.Playing) { m.state = HeldMomentState.Waiting; m.startedAt = -1; m.batch = -1; m.welcome = false; } // left before it ended: it plays again
            foreach (var m in Moments)
                if (m.state == HeldMomentState.Waiting) { var why = MomentStale(m); if (why != MomentCardReason.None) CardMoment(m, why); }
            p.welcomeDue = Moments.Any(m => m.state == HeldMomentState.Waiting);
        }

        // ---------- the director: what plays now ----------

        /// <summary>Why nothing would start now (null = <see cref="StartDueMoments"/> would hand some over). For the panel and tests.</summary>
        public string MomentWaitReason() => MomentPicks(out _);

        string MomentPicks(out List<HeldMoment> picks)
        {
            picks = null;
            if (!Config.moments) return "moments off";
            if (CatchingUp) return "away";
            if (State.firstRun.active) return "the first run";
            EnsureMomentVisit();
            if (Moments.Any(m => m.state == HeldMomentState.Playing)) return "a moment is playing";
            foreach (var m in Moments)
                if (m.state == HeldMomentState.Waiting) { var why = MomentStale(m); if (why != MomentCardReason.None) CardMoment(m, why); }
            var waiting = WaitingMoments();
            if (waiting.Count == 0) return "nothing waiting";
            var p = MomentPacing;
            var size = MomentSize;
            if (p.welcomeDue) { picks = new List<HeldMoment> { waiting[0] }; return null; } // the welcome: the top one, on the open
            if (p.visitPlayed >= MomentRules.VisitCap(size)) return "this visit has had its moments";
            if (p.watchedMinutes + 1e-9 < p.restUntil) return "resting";
            if (p.credit + 1e-9 < 1.0) return "budget";
            var first = waiting.FirstOrDefault(m => (int)m.kind != p.lastKind);
            if (first == null) return "the same kind as the last one";
            picks = new List<HeldMoment> { first };
            if (p.busyNext || (size == MomentBoxSize.Town && BusyRoll(p.releases) < MomentRules.BusyDayChance))
            {
                int want = 2 + (BusyRoll(p.releases + 7919) < 0.5f ? 0 : 1); // 2 or 3 at once
                foreach (var m in waiting)
                {
                    if (picks.Count >= Math.Min(want, MomentRules.BusyDayMax)) break;
                    if (picks.Any(x => x.kind == m.kind)) continue; // a wedding during the market: different kinds
                    picks.Add(m);
                }
            }
            return null;
        }

        float BusyRoll(int salt)
        {
            uint h = (uint)State.seed * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u ^ 0x3C6EF372u;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        /// <summary>
        /// The view asks: what plays now? Returns the moments to stage now (usually one, a busy day 2 or 3, empty when none is due),
        /// each marked Playing. The view stages each and calls <see cref="FinishMoment"/> when it is over. Call it only when the box
        /// is calm (no sheet, choice scene, evening talk, era moment or wipe in progress): the director doesn't know about those.
        /// </summary>
        public List<HeldMoment> StartDueMoments()
        {
            var list = new List<HeldMoment>();
            if (MomentPicks(out var picks) != null || picks == null) return list;
            var p = MomentPacing;
            bool welcome = p.welcomeDue;
            foreach (var m in picks)
            {
                m.state = HeldMomentState.Playing;
                m.startedAt = p.watchedMinutes;
                m.batch = p.releases;
                m.welcome = welcome;
                list.Add(m);
            }
            p.releases++;
            p.credit = Math.Max(0, p.credit - 1.0);
            p.visitPlayed += list.Count;
            p.lastKind = (int)list[list.Count - 1].kind;
            p.welcomeDue = false;
            p.busyNext = false;
            return list;
        }

        /// <summary>
        /// The view is done with a moment it was handed. <paramref name="played"/>: it was staged and seen (Played; its line stays
        /// out of the cards); false: it could not be staged (it becomes a card, and the visit's count and the budget are given back
        /// if nothing of its batch played). When the last one playing ends, the rest begins. False if it was not playing.
        /// </summary>
        public bool FinishMoment(int id, bool played = true)
        {
            var m = FindMoment(id);
            if (m == null || m.state != HeldMomentState.Playing) return false;
            var p = MomentPacing;
            if (played) { m.state = HeldMomentState.Played; m.endMonth = State.month; }
            else
            {
                CardMoment(m, MomentCardReason.NotStaged);
                p.visitPlayed = Math.Max(0, p.visitPlayed - 1);
                if (!Moments.Any(x => x.batch == m.batch && x != m && x.state != HeldMomentState.Carded))
                {
                    p.credit = Math.Min(MomentRules.CreditCap, p.credit + 1.0);
                    if (m.welcome) p.welcomeDue = Moments.Any(x => x.state == HeldMomentState.Waiting);
                }
            }
            if (!Moments.Any(x => x.state == HeldMomentState.Playing) && Moments.Any(x => x.batch == m.batch && x.state == HeldMomentState.Played))
                p.restUntil = p.watchedMinutes + MomentRules.RestMinutes(MomentSize);
            return true;
        }

        /// <summary>The next release, in watched minutes from now: 0 = due now (or the welcome), -1 = nothing waits or the visit's cap
        /// is reached. A moment playing counts as not due.</summary>
        public double MomentDueInMinutes()
        {
            if (!Config.moments) return -1;
            var waiting = WaitingMoments();
            if (waiting.Count == 0) return -1;
            var p = MomentPacing;
            if (p.welcomeDue) return 0;
            if (p.visitPlayed >= MomentRules.VisitCap(MomentSize)) return -1;
            double rest = Math.Max(0, p.restUntil - p.watchedMinutes);
            double budget = Math.Max(0, (1.0 - p.credit) * MomentRules.IntervalMinutes(State.population));
            return Math.Max(rest, budget);
        }

        // ---------- debug (#369 cheats) ----------

        /// <summary>Debug: queue one moment of each kind now, about the box's living people (lines from the chronicle where it has
        /// them; otherwise a cheat line). The queue's cap is not applied, so all five wait.</summary>
        public List<HeldMoment> DebugQueueOneOfEach()
        {
            var made = new List<HeldMoment>();
            if (!Config.moments) return made;
            var adults = State.notables.Where(n => n.alive && n.age >= MarryAge).OrderBy(n => n.id).ToList();
            var a = adults.Count > 0 ? adults[0] : null;
            var b = adults.Count > 1 ? adults[1] : null;
            var baby = State.notables.Where(n => n.alive && n.parentId >= 0).OrderBy(n => n.age).FirstOrDefault();
            int leader = SeatOf(LeaderOf(State.era))?.holderId ?? a?.id ?? -1;
            ChronicleEntry Last(string kind) => State.chronicle.LastOrDefault(e => e.kind == kind);
            ChronicleEntry Line(string kind, string text, int x, int y)
            {
                // A cheat line is a real chronicle line (the history shows it), recorded without the hook so it isn't queued twice.
                var e = new ChronicleEntry { id = State.NextId(), month = State.month, kind = kind, text = text, salience = 0.6f, notableId = x, otherNotableId = y };
                State.chronicle.Add(e);
                Chronicled?.Invoke(e);
                return e;
            }
            void Add(MomentKind k, ChronicleEntry e, int x, int y) { if (e != null) made.Add(QueueMoment(k, e, x, y, true)); }
            try
            {
                momentNoTrim = true;
                if (a != null && b != null) Add(MomentKind.Wedding, Last("wedding") is { } w && Alive(w.notableId) != null && Alive(w.otherNotableId) != null ? w
                    : Line("wedding", $"{a.FullName} and {b.FullName} promised each other at the fire.", a.id, b.id), a.id, b.id);
                if (baby != null) Add(MomentKind.Newborn, Last("birth") is { } bi && bi.notableId == baby.id ? bi
                    : Line("birth", $"{baby.FullName} was held up for the people to see.", baby.id, -1), baby.id, baby.parentId);
                if (leader >= 0) Add(MomentKind.NewLeader, Line("role_won", $"{FindNotable(leader)?.FullName ?? "Someone"} took the leader's seat at the fire.", leader, -1), leader, -1);
                var cb = Last(CallbackKind);
                Add(MomentKind.Callback, cb ?? Line(CallbackKind, "The people remembered a choice the Keeper made.", a?.id ?? -1, -1), cb?.notableId ?? a?.id ?? -1, -1);
                Add(MomentKind.Festival, Line("festival", "The people gathered round the fire for a feast.", a?.id ?? -1, -1), a?.id ?? -1, -1);
            }
            finally { momentNoTrim = false; }
            return made;
        }

        bool momentNoTrim;

        /// <summary>Debug: the next moment may play now: the budget is full, the rest and the visit's cap are lifted, and the kind
        /// rule forgets the last one. The view starts it on its next check.</summary>
        public void DebugMomentDueNow()
        {
            var p = MomentPacing;
            p.credit = MomentRules.CreditCap;
            p.restUntil = p.watchedMinutes;
            p.visitPlayed = 0;
            p.lastKind = -1;
        }

        /// <summary>Debug: the next release is a busy day (2 or 3 kinds at once) if two kinds wait, whatever the box's size, and it
        /// may start now.</summary>
        public void DebugBusyDay() { MomentPacing.busyNext = true; DebugMomentDueNow(); }

        /// <summary>Debug: the queue, the director's clock and the next due time, in a few lines.</summary>
        public string MomentQueueStatus()
        {
            if (!Config.moments) return "moments off";
            var p = MomentPacing;
            var inv = CultureInfo.InvariantCulture;
            var size = MomentSize;
            double due = MomentDueInMinutes();
            string head = string.Format(inv, "{0} (pop {1}): 1 per {2:0.#} watched min, rest {3:0} min, cap {4}/visit; visit {5}, played {6}; watched {7:0.0} min, credit {8:0.00}",
                size, State.population, MomentRules.IntervalMinutes(State.population), MomentRules.RestMinutes(size),
                MomentRules.VisitCap(size) == int.MaxValue ? "none" : MomentRules.VisitCap(size).ToString(inv), p.visits, p.visitPlayed, p.watchedMinutes, p.credit);
            string next = due < 0 ? "next: none due" : due <= 1e-9 ? "next: due now" + (p.welcomeDue ? " (welcome)" : "") : string.Format(inv, "next: in {0:0.0} watched min", due);
            string why = MomentWaitReason();
            var lines = new List<string> { head, next + (why != null ? " · waiting because: " + why : "") };
            foreach (var m in Moments.Where(m => m.state == HeldMomentState.Playing)) lines.Add($"playing: {Describe(m)}");
            foreach (var m in WaitingMoments()) lines.Add($"waiting: {Describe(m)}");
            return string.Join("\n", lines);
        }

        string Describe(HeldMoment m)
        {
            string who = string.Join(" & ", new[] { m.personA, m.personB }.Where(i => i >= 0).Select(i => FindNotable(i)?.name ?? "?"));
            return string.Format(CultureInfo.InvariantCulture, "#{0} {1}{2} · {3:0.##} · {4} mo ago", m.id, m.kind, who.Length > 0 ? " (" + who + ")" : "", m.priority, State.month - m.month);
        }
    }
}
