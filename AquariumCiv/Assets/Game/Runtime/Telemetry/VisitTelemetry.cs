using AquariumCiv.Sim;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AquariumCiv.Game
{
    /// <summary>
    /// #374 (visit loop C7): what the diary test needs, per visit. A visit is an open after at least
    /// <see cref="SimCore.DilemmaVisitAwayHours"/> away (or the first); opens inside it are counted. For each visit the tracker keeps
    /// the opens, the watched minutes, the moments played live (or stopped), the moments that became cards, the gaps between live
    /// releases (watched minutes), the choices raised / answered / left to the people / lapsed, and the cards shown and read.
    /// It only reads the sim (no hook into the queue): <see cref="Observe"/> diffs the moment and choice states each frame, like
    /// <see cref="DilemmaTelemetryTracker"/>. The numbers go into the same telemetry log as new events (see <see cref="Ev.VisitStart"/>
    /// ...), never free text; <see cref="VisitTelemetryState"/> persists the running visit between launches.
    /// </summary>
    [Serializable]
    public class VisitTelemetryState
    {
        public int visit;                    // visits so far (0 = none yet); the running one is this number
        public int opens;                    // opens in the running visit
        public double watched;               // watched minutes in the running visit
        public int live, stopped, carded;    // moments: played live, handed over but not staged, became cards (not played)
        public string gaps = "";             // watched minutes between live releases, ';' separated
        public double lastBatchAt = -1;      // MomentPacing.watchedMinutes of the last live release this visit (-1 = none)
        public int lastBatch = -1;
        public int raised, answered, letDecide, lapsed;
        public string answerHours = "";      // hours from raise to answer, ';' separated (only the Keeper's own answers)
        public int cardsShown, cardsRead;
        public int rev;                      // changes whenever a counter does (so a visit_sum is written only when it moved)
        public int sumRev = -1;              // rev of the last visit_sum written
        public double lastWatchedAbs = -1;   // MomentPacing.watchedMinutes at the last look
        public int momentMark = -1;          // MomentPacing.nextId at the last look: ids from here on were made since
        public int choiceMark = -1;          // the largest dilemma id seen + 1
        public string openMoments = "", openChoices = ""; // ids waiting / open at the last look, ';' separated
        // recall of seen vs read (VisitRecall): what was seen live and read on cards since the question last appeared
        public int liveSinceAsk, readSinceAsk, lastAskVisit = -1, dismissedInARow, asks;

        public string ToJson()
        {
            var sb = new StringBuilder("{");
            void N(string k, double v) { if (sb.Length > 1) sb.Append(','); Json.Str(sb, k); sb.Append(':'); Json.Num(sb, v); }
            void S(string k, string v) { if (sb.Length > 1) sb.Append(','); Json.Str(sb, k); sb.Append(':'); Json.Str(sb, v ?? ""); }
            N("visit", visit); N("opens", opens); N("watched", watched); N("live", live); N("stopped", stopped); N("carded", carded);
            S("gaps", gaps); N("lastBatchAt", lastBatchAt); N("lastBatch", lastBatch);
            N("raised", raised); N("answered", answered); N("letDecide", letDecide); N("lapsed", lapsed); S("answerHours", answerHours);
            N("cardsShown", cardsShown); N("cardsRead", cardsRead); N("rev", rev); N("sumRev", sumRev); N("lastWatchedAbs", lastWatchedAbs);
            N("momentMark", momentMark); N("choiceMark", choiceMark); S("openMoments", openMoments); S("openChoices", openChoices);
            N("liveSinceAsk", liveSinceAsk); N("readSinceAsk", readSinceAsk); N("lastAskVisit", lastAskVisit);
            N("dismissedInARow", dismissedInARow); N("asks", asks);
            return sb.Append('}').ToString();
        }

        /// <summary>A fresh state when the text is empty or torn.</summary>
        public static VisitTelemetryState FromJson(string text)
        {
            var s = new VisitTelemetryState();
            if (string.IsNullOrWhiteSpace(text) || !Json.TryParseFlat(text.Trim(), out var m)) return s;
            double D(string k, double def) => m.TryGetValue(k, out var v) && v is double d ? d : def;
            string T(string k) => m.TryGetValue(k, out var v) && v is string t ? t : "";
            s.visit = (int)D("visit", 0); s.opens = (int)D("opens", 0); s.watched = D("watched", 0);
            s.live = (int)D("live", 0); s.stopped = (int)D("stopped", 0); s.carded = (int)D("carded", 0);
            s.gaps = T("gaps"); s.lastBatchAt = D("lastBatchAt", -1); s.lastBatch = (int)D("lastBatch", -1);
            s.raised = (int)D("raised", 0); s.answered = (int)D("answered", 0); s.letDecide = (int)D("letDecide", 0); s.lapsed = (int)D("lapsed", 0);
            s.answerHours = T("answerHours"); s.cardsShown = (int)D("cardsShown", 0); s.cardsRead = (int)D("cardsRead", 0);
            s.rev = (int)D("rev", 0); s.sumRev = (int)D("sumRev", -1); s.lastWatchedAbs = D("lastWatchedAbs", -1);
            s.momentMark = (int)D("momentMark", -1); s.choiceMark = (int)D("choiceMark", -1);
            s.openMoments = T("openMoments"); s.openChoices = T("openChoices");
            s.liveSinceAsk = (int)D("liveSinceAsk", 0); s.readSinceAsk = (int)D("readSinceAsk", 0); s.lastAskVisit = (int)D("lastAskVisit", -1);
            s.dismissedInARow = (int)D("dismissedInARow", 0); s.asks = (int)D("asks", 0);
            return s;
        }
    }

    public class VisitTelemetryTracker
    {
        public const string SidecarPrefix = "visits-";   // visits-<box>.json next to the log (not a "box-" file: the log's list ignores it)
        public static string SidecarName(string boxId) => SidecarPrefix + boxId + ".json";

        public VisitTelemetryState State { get; }
        readonly Func<int, bool> isCheat;
        /// <summary>The box this tracker watches: fixed at the start, since the log may already point at the next box when it closes.</summary>
        readonly string box;
        readonly Dictionary<int, HeldMomentState> moments = new Dictionary<int, HeldMomentState>();
        readonly HashSet<int> choiceKnown = new HashSet<int>(), choiceClosed = new HashSet<int>();

        public VisitTelemetryTracker(VisitTelemetryState state, SimCore sim = null, Func<int, bool> isCheat = null, string boxId = null)
        {
            State = state ?? new VisitTelemetryState();
            box = boxId;
            this.isCheat = isCheat;
            if (sim != null) Baseline(sim);
        }

        static HashSet<int> Ids(string s)
        {
            var r = new HashSet<int>();
            foreach (var p in (s ?? "").Split(';')) if (int.TryParse(p, out var i)) r.Add(i);
            return r;
        }

        static string Join(IEnumerable<int> ids) => string.Join(";", ids);

        /// <summary>What was already counted: every moment and choice the sim has now, except those the last look saw waiting or
        /// open that have ended since, and those made since (an away catch-up can card a waiting moment).</summary>
        void Baseline(SimCore sim)
        {
            var st = State;
            var openM = Ids(st.openMoments);
            var openC = Ids(st.openChoices);
            if (sim.State.moments != null)
                foreach (var m in sim.State.moments)
                {
                    bool finished = m.state == HeldMomentState.Played || m.state == HeldMomentState.Carded;
                    if (st.momentMark >= 0 && m.id >= st.momentMark) continue;       // made since: seen for the first time in Observe
                    if (finished && openM.Contains(m.id)) continue;                  // ended since the last look: counted in Observe
                    moments[m.id] = m.state;
                }
            if (sim.State.dilemmas != null)
                foreach (var d in sim.State.dilemmas)
                {
                    if (st.choiceMark >= 0 && d.id >= st.choiceMark) continue;
                    choiceKnown.Add(d.id);
                    if (d.state != DilemmaState.Open && !openC.Contains(d.id)) choiceClosed.Add(d.id);
                }
        }

        // ---- visits ------------------------------------------------------------------------------------------------

        /// <summary>The app opened or came back (a new telemetry session). Starts a visit after a real absence, else counts an open
        /// in the running one.</summary>
        public void Open(TelemetryLog log, SimCore sim, double awayHours)
        {
            var st = State;
            bool fresh = st.visit == 0 || awayHours >= SimCore.DilemmaVisitAwayHours - 1e-9;
            if (fresh)
            {
                Close(log);
                Reset(st); // the recall counters, the marks and the open ids carry over
                st.visit++;
            }
            st.opens++;
            st.lastWatchedAbs = sim?.State?.momentPacing != null ? sim.MomentPacing.watchedMinutes : -1;
            Touch();
            log?.Log(Ev.VisitStart, "box", box ?? log.BoxId, "n", st.visit, "opens", st.opens, "away_h", Math.Round(awayHours, 3), "fresh", fresh);
            Save(log);
        }

        static void Reset(VisitTelemetryState st)
        {
            st.opens = 0; st.watched = 0; st.live = st.stopped = st.carded = 0; st.gaps = ""; st.lastBatchAt = -1; st.lastBatch = -1;
            st.raised = st.answered = st.letDecide = st.lapsed = 0; st.answerHours = ""; st.cardsShown = st.cardsRead = 0;
            st.rev = 0; st.sumRev = -1;
        }

        void Touch() => State.rev++;

        // ---- looking at the sim ----------------------------------------------------------------------------------------

        /// <summary>Call every frame (or step) after the sim moved. Writes the events of what changed since the last call.</summary>
        public void Observe(SimCore sim, TelemetryLog log)
        {
            if (sim?.State == null || State.visit == 0) return;
            var st = State;
            // watched minutes
            if (sim.State.momentPacing != null)
            {
                double now = sim.MomentPacing.watchedMinutes;
                if (st.lastWatchedAbs >= 0 && now > st.lastWatchedAbs) { st.watched = Math.Round(st.watched + (now - st.lastWatchedAbs), 6); Touch(); }
                st.lastWatchedAbs = now;
            }
            // moments: the ones that ended since the last look, in the order they started
            if (sim.State.moments != null)
            {
                List<HeldMoment> ended = null;
                foreach (var m in sim.State.moments)
                {
                    bool finished = m.state == HeldMomentState.Played || m.state == HeldMomentState.Carded;
                    bool had = moments.TryGetValue(m.id, out var was);
                    if (had && was == m.state) continue;
                    moments[m.id] = m.state;
                    if (!finished || m.cheat) continue;
                    if (had && (was == HeldMomentState.Played || was == HeldMomentState.Carded)) continue;
                    (ended ??= new List<HeldMoment>()).Add(m);
                }
                if (ended != null)
                {
                    ended.Sort((a, b) => a.startedAt != b.startedAt ? a.startedAt.CompareTo(b.startedAt) : a.id.CompareTo(b.id));
                    foreach (var m in ended) MomentEnded(log, m);
                }
            }
            // choices
            if (sim.State.dilemmas != null)
                foreach (var d in sim.State.dilemmas)
                {
                    bool cheat = isCheat != null && isCheat(d.id);
                    if (choiceKnown.Add(d.id) && !cheat)
                    {
                        st.raised++; Touch();
                        log?.Log(Ev.ChoiceRaised, "box", box ?? log.BoxId, "n", st.visit, "id", d.id, "small", d.size == DilemmaSize.Small ? (object)1 : null);
                    }
                    if (d.state == DilemmaState.Open || !choiceClosed.Add(d.id) || cheat || d.end == DilemmaEnd.Debug) continue;
                    ChoiceEnded(sim, log, d);
                }
            // what to remember if the app is killed before Close
            st.momentMark = sim.State.momentPacing != null ? sim.MomentPacing.nextId : st.momentMark;
            int maxD = -1; if (sim.State.dilemmas != null) foreach (var d in sim.State.dilemmas) if (d.id > maxD) maxD = d.id;
            st.choiceMark = Math.Max(st.choiceMark, maxD + 1);
            var openM = new List<int>(); if (sim.State.moments != null) foreach (var m in sim.State.moments) if (m.state == HeldMomentState.Waiting || m.state == HeldMomentState.Playing) openM.Add(m.id);
            var openC = new List<int>(); if (sim.State.dilemmas != null) foreach (var d in sim.State.dilemmas) if (d.state == DilemmaState.Open) openC.Add(d.id);
            st.openMoments = Join(openM); st.openChoices = Join(openC);
        }

        void MomentEnded(TelemetryLog log, HeldMoment m)
        {
            var st = State;
            if (m.state == HeldMomentState.Played)
            {
                st.live++; st.liveSinceAsk++;
                object gap = null;
                if (m.batch != st.lastBatch)
                {
                    if (st.lastBatchAt >= 0 && m.startedAt >= 0)
                    {
                        double g = Math.Round(Math.Max(0, m.startedAt - st.lastBatchAt), 3);
                        st.gaps = st.gaps.Length == 0 ? F(g) : st.gaps + ";" + F(g);
                        gap = g;
                    }
                    st.lastBatch = m.batch; st.lastBatchAt = m.startedAt;
                }
                Touch();
                log?.Log(Ev.MomentLive, "box", box ?? log.BoxId, "n", st.visit, "id", m.id, "kind", m.kind.ToString().ToLowerInvariant(), "result", "played",
                    "welcome", m.welcome ? (object)1 : null, "at_min", Math.Round(st.watched, 2), "gap_min", gap);
            }
            else if (m.cardReason == MomentCardReason.NotStaged)
            {
                st.stopped++; st.carded++; Touch(); // handed over, not staged: it is a card now too
                log?.Log(Ev.MomentLive, "box", box ?? log.BoxId, "n", st.visit, "id", m.id, "kind", m.kind.ToString().ToLowerInvariant(), "result", "stopped", "at_min", Math.Round(st.watched, 2));
            }
            else
            {
                st.carded++; Touch();
                log?.Log(Ev.MomentCard, "box", box ?? log.BoxId, "n", st.visit, "id", m.id, "kind", m.kind.ToString().ToLowerInvariant(), "why", m.cardReason.ToString().ToLowerInvariant());
            }
        }

        void ChoiceEnded(SimCore sim, TelemetryLog log, Dilemma d)
        {
            var st = State;
            string end;
            double? hours = null;
            if (d.end == DilemmaEnd.Answered || d.end == DilemmaEnd.None && d.state == DilemmaState.Answered)
            {
                end = "answered"; st.answered++;
                hours = Math.Round(Math.Max(0, sim.State.clockHours - d.raisedHours), 3);
                st.answerHours = st.answerHours.Length == 0 ? F(hours.Value) : st.answerHours + ";" + F(hours.Value);
            }
            else if (d.end == DilemmaEnd.LetThemDecide) { end = "let_decide"; st.letDecide++; hours = Math.Round(Math.Max(0, sim.State.clockHours - d.raisedHours), 3); }
            else { end = d.end == DilemmaEnd.Gone ? "gone" : "lapsed"; st.lapsed++; }
            Touch();
            log?.Log(Ev.ChoiceEnd, "box", box ?? log.BoxId, "n", st.visit, "id", d.id, "end", end, "shown_visits", d.visits, "hours", hours);
        }

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        // ---- cards (the existing card events call these) -------------------------------------------------------------------

        public void CardShown() { State.cardsShown++; Touch(); }
        public void CardRead() { State.cardsRead++; State.readSinceAsk++; Touch(); }

        // ---- writing the summary -------------------------------------------------------------------------------------------

        /// <summary>The session ended (pause or quit): the visit so far as one line, if anything moved since the last one.</summary>
        public void Close(TelemetryLog log)
        {
            var st = State;
            if (st.visit == 0 || st.sumRev == st.rev) { Save(log); return; }
            st.sumRev = st.rev;
            log?.Log(Ev.VisitSum, "box", box ?? log.BoxId, "n", st.visit, "opens", st.opens, "watched_min", Math.Round(st.watched, 2),
                "live", st.live, "stopped", st.stopped, "carded", st.carded, "gaps", st.gaps.Length > 0 ? st.gaps : null,
                "raised", st.raised, "answered", st.answered, "let_decide", st.letDecide, "lapsed", st.lapsed,
                "answer_h", st.answerHours.Length > 0 ? st.answerHours : null, "cards_shown", st.cardsShown, "cards_read", st.cardsRead);
            Save(log);
        }

        public void Save(TelemetryLog log) => log?.WriteSidecar(SidecarName(box ?? log.BoxId), State.ToJson());

        public static VisitTelemetryState Load(TelemetryLog log) => VisitTelemetryState.FromJson(log?.ReadSidecar(SidecarName(log.BoxId)));

        /// <summary>One line for the debug panel: this visit's numbers so far.</summary>
        public string Line()
        {
            var st = State;
            return string.Format(CultureInfo.InvariantCulture,
                "visit {0}: {1} open(s), {2:0.0} min watched · moments {3} live / {4} stopped / {5} cards · gaps [{6}] · choices {7} raised, {8} answered, {9} let-decide, {10} lapsed · cards {11} shown, {12} read",
                st.visit, st.opens, st.watched, st.live, st.stopped, st.carded, st.gaps.Replace(";", ", "), st.raised, st.answered, st.letDecide, st.lapsed, st.cardsShown, st.cardsRead);
        }

        // ---- recall of seen vs read (data shape; the question sheet is not wired, see VisitRecall) -----------------------------

        /// <summary>The question appeared (decision: <see cref="VisitRecall.ShouldAsk"/>). Logs the counts it will be compared with.</summary>
        public void RecallShown(TelemetryLog log)
        {
            var st = State;
            st.asks++; st.lastAskVisit = st.visit;
            log?.Log(Ev.VisitRecall, "box", box ?? log.BoxId, "n", st.visit, "outcome", "shown", "live_since", st.liveSinceAsk, "read_since", st.readSinceAsk);
            Touch(); Save(log);
        }

        /// <summary>Answered with a count only (never free text): how many things they named, and how many of those they say they saw
        /// happen (the rest they read on cards, or did not place). -1 = not asked.</summary>
        public void RecallAnswered(TelemetryLog log, int named, int namedSeen = -1)
        {
            var st = State;
            log?.Log(Ev.VisitRecall, "box", box ?? log.BoxId, "n", st.visit, "outcome", "answered", "named", named, "named_seen", namedSeen >= 0 ? (object)namedSeen : null,
                "live_since", st.liveSinceAsk, "read_since", st.readSinceAsk);
            st.liveSinceAsk = 0; st.readSinceAsk = 0; st.dismissedInARow = 0;
            Touch(); Save(log);
        }

        public void RecallDismissed(TelemetryLog log)
        {
            var st = State;
            st.dismissedInARow++;
            log?.Log(Ev.VisitRecall, "box", box ?? log.BoxId, "n", st.visit, "outcome", "dismissed", "live_since", st.liveSinceAsk, "read_since", st.readSinceAsk);
            Touch(); Save(log);
        }
    }

    /// <summary>
    /// When to ask "What happened in your box since last time?" (research 16 §6): optional, rare (every
    /// <see cref="EveryVisits"/> visits from visit <see cref="FirstVisit"/>), only when something could be recalled (a moment seen live
    /// or a card read since the last time), and given up after <see cref="MaxDismissedInARow"/> dismissals. The slice has no survey
    /// form (the in-game feedback sheet has no survey hook), so this is the decision and the data shape; a sheet like
    /// <c>RecallPromptView</c> would show <see cref="Question"/> and call <see cref="VisitTelemetryTracker.RecallAnswered"/>.
    /// </summary>
    public static class VisitRecall
    {
        public const int FirstVisit = 3, EveryVisits = 4, MaxDismissedInARow = 2;
        /// <summary>The tester's question. The answer is free text only in the tester's own words elsewhere; the log gets counts.</summary>
        public const string Question = "What happened in your box since last time?";

        public static bool ShouldAsk(VisitTelemetryState s, bool busy)
        {
            if (s == null || busy || s.visit < FirstVisit || s.dismissedInARow >= MaxDismissedInARow) return false;
            if (s.lastAskVisit >= 0 && s.visit - s.lastAskVisit < EveryVisits) return false;
            return s.liveSinceAsk + s.readSinceAsk > 0;
        }
    }

    /// <summary>The visit numbers read back from the log (for the report and the feedback issue). Last <c>visit_sum</c> per box and visit wins.</summary>
    public class VisitStats
    {
        public int visits, opens, live, stopped, carded, raised, answered, letDecide, lapsed, cardsShown, cardsRead, recallShown, recallAnswered, recallNamed, recallNamedSeen;
        public double watchedMin, gapSum;
        public int gapCount;
        public List<double> answerHours = new List<double>();
        public bool Any => visits > 0;
        public double? AvgMomentsPerVisit => visits > 0 ? live / (double)visits : (double?)null;
        public double? AvgGapMin => gapCount > 0 ? gapSum / gapCount : (double?)null;

        public string ToText()
        {
            var c = CultureInfo.InvariantCulture;
            string avg = AvgGapMin.HasValue ? AvgGapMin.Value.ToString("0.0", c) + " min" : "-";
            string ans = answerHours.Count > 0 ? ", median " + DilemmaStats.Median(answerHours).Value.ToString("0.0", c) + " h to answer" : "";
            return string.Format(c, "Visits: {0} ({1} opens, {2:0.#} min watched) · moments {3} live / {4} stopped / {5} cards (avg {6:0.0} live per visit, avg gap {7}) · choices {8} raised, {9} answered, {10} let-decide, {11} lapsed{12} · cards {13} shown / {14} read · recall asked {15}x, answered {16}x",
                visits, opens, watchedMin, live, stopped, carded, AvgMomentsPerVisit ?? 0, avg, raised, answered, letDecide, lapsed, ans, cardsShown, cardsRead, recallShown, recallAnswered);
        }
    }
}
