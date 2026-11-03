using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AquariumCiv.Game
{
    /// <summary>P7 (#225): the dilemma measures for one variant (or all of them). A dilemma is counted once, by its box and sim id.</summary>
    public class DilemmaStats
    {
        public string variant;                 // "A", "B", or "?" when the log has no variant
        public int sessions;                   // opens that ran in this variant (a session counts under every variant it was in)
        public int raised, shown;              // distinct dilemmas raised / put in front of the player
        public int answered, lapsed;           // of those shown: answered by the player / left to lapse
        public int lapsedUnseen;               // lapsed without ever being shown (not in the pass share)
        public int answeredBox, answeredCard;  // the path split, over every answer
        public List<double> secondsToAnswer = new List<double>(); // answer minus shown, in real seconds
        public int callbacksShown, callbackShowMe;
        public int cheatEvents;                // dilemma events caused by debug cheats (debug=1): left out of every number here

        /// <summary>The pass criterion (>= 0.70): of the shown dilemmas that are settled, the share the player answered. Null when none is settled.</summary>
        /// <summary>Of every settled dilemma that was raised (shown or not): the share the player answered. Unseen lapses count as not answered.</summary>
        public double? AnsweredShareOfRaised => answered + lapsed + lapsedUnseen > 0 ? answered / (double)(answered + lapsed + lapsedUnseen) : (double?)null;
        public double? AnsweredShare => answered + lapsed > 0 ? answered / (double)(answered + lapsed) : (double?)null;
        public double? MedianSeconds => Median(secondsToAnswer);
        public double? MinSeconds => secondsToAnswer.Count > 0 ? System.Linq.Enumerable.Min(secondsToAnswer) : (double?)null;
        public double? MaxSeconds => secondsToAnswer.Count > 0 ? System.Linq.Enumerable.Max(secondsToAnswer) : (double?)null;
        /// <summary>Callbacks opened with Show me, as a share of callbacks shown. Null when none were shown.</summary>
        public double? ShowMeShare => callbacksShown > 0 ? callbackShowMe / (double)callbacksShown : (double?)null;
        public bool Any => cheatEvents > 0 || sessions > 0 || raised > 0 || shown > 0 || answered > 0 || lapsed > 0 || callbacksShown > 0;

        public static double? Median(List<double> v)
        {
            if (v.Count == 0) return null;
            var s = new List<double>(v); s.Sort();
            int m = s.Count / 2;
            return s.Count % 2 == 1 ? s[m] : (s[m - 1] + s[m]) / 2;
        }

        public string ToText()
        {
            var c = CultureInfo.InvariantCulture;
            string P(double? x, string f) => x.HasValue ? string.Format(c, f, x.Value) : "-";
            return string.Format(c, "Dilemmas [{0}] · {1} session(s) · raised {2} · shown {3} · answered {4} · lapsed {5} ({6} unseen) · answered of shown {7} · answered of raised {15} · path box {8} / card {9} · seconds to answer median {10} (range {11}-{12}) · callbacks shown {13}, Show me {14} · cheat events: {16}, not counted",
                variant, sessions, raised, shown, answered, lapsed, lapsedUnseen,
                AnsweredShare.HasValue ? string.Format(c, "{0:0}%", AnsweredShare.Value * 100) : "-",
                answeredBox, answeredCard, P(MedianSeconds, "{0:0.#} s"), P(MinSeconds, "{0:0.#}"), P(MaxSeconds, "{0:0.#}"), callbacksShown, callbackShowMe,
                AnsweredShareOfRaised.HasValue ? string.Format(c, "{0:0}%", AnsweredShareOfRaised.Value * 100) : "-", cheatEvents);
        }
    }

    /// <summary>The week-long metrics (#100) and the M4 card/history metrics (#81), computed from the log.</summary>
    public class TelemetryReport
    {
        public int events, badLines;
        public DateTime installUtc;
        public DateTime lastEventUtc;
        /// <summary>The tester's own day boundary (from the phone's timezone at each open).</summary>
        public int tzMinutes;

        // opens and retention. Day 0 = install day (local calendar day).
        public int opens;
        public SortedDictionary<int, int> opensPerDay = new SortedDictionary<int, int>();
        /// <summary>True: opened on that day; false: did not; null: that day has not come yet (or the log has no data for it).</summary>
        public bool? d1, d3, d7;
        public int daysPlayed;

        // first clean
        public double? secondsToFirstClean;          // install (first open) to the first clean
        public double? secondsIntoSessionAtFirstClean;

        // era moments (#88)
        public int erasLive, erasSkipped;
        public List<string> eraRows = new List<string>(); // "era 2: live" ...

        // catch-up
        public int catchUps;
        public double catchUpAvgMs, catchUpMaxMs, longestAwayHours;

        // M4 cards
        public int cardsShown, cardsRead, cardsOpened, cardsFlownTo, showMe, skipAll, peekTaps;
        public double avgDwellSeconds;
        // M4 history
        public int historyOpens, historyOpensUnprompted, timelapseOpens, timelapseOpensUnprompted, scrubs, pinTaps, plays, snapshots, rememberTaps;
        public double scrubYearsTotal;
        public int bookPageTurns, bookPagesSeen;

        // recall question (#100)
        public int recallShown;
        public bool recallAnswered;
        public List<int> storiesRecalled = new List<int>();

        // P7 dilemmas (#225): all variants merged, and per variant ("A" = dilemmas off, "B" = on)
        public DilemmaStats dilemmas = new DilemmaStats { variant = "all" };
        public SortedDictionary<string, DilemmaStats> dilemmasByVariant = new SortedDictionary<string, DilemmaStats>();

        public Dictionary<string, int> counts = new Dictionary<string, int>();

        static string B(bool? v) => v.HasValue ? (v.Value ? "yes" : "no") : "n/a";

        public string ToText()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"Telemetry report · {events} events ({badLines} unreadable lines) · installed {installUtc:yyyy-MM-dd HH:mm}Z");
            sb.AppendLine($"Opens: {opens} over {daysPlayed} day(s) · per day: " + (opensPerDay.Count == 0 ? "-" : string.Join(", ", Pairs(opensPerDay))));
            sb.AppendLine($"Retention: D1 {B(d1)} · D3 {B(d3)} · D7 {B(d7)}");
            sb.AppendLine("First clean: " + (secondsToFirstClean.HasValue
                ? string.Format(c, "{0:0} s after install ({1:0} s into that session)", secondsToFirstClean, secondsIntoSessionAtFirstClean) : "none yet"));
            sb.AppendLine($"Era moments: {erasLive} watched live · {erasSkipped} skipped" + (eraRows.Count > 0 ? " (" + string.Join("; ", eraRows) + ")" : ""));
            sb.AppendLine(string.Format(c, "Catch-ups: {0} · avg {1:0} ms · max {2:0} ms · longest away {3:0.0} h", catchUps, catchUpAvgMs, catchUpMaxMs, longestAwayHours));
            sb.AppendLine(string.Format(c, "Cards: {0} shown · {1} read (avg dwell {2:0.0} s) · {3} opened · {4} flown to · Show me {5} · Skip all {6} · peeks {7}",
                cardsShown, cardsRead, avgDwellSeconds, cardsOpened, cardsFlownTo, showMe, skipAll, peekTaps));
            sb.AppendLine(string.Format(c, "History: {0} opens ({1} unprompted) · time-lapse {2} ({3} unprompted) · {4} scrubs ({5:0} yrs) · pins {6} · plays {7} · snapshots {8} · book {9} turns / {10} pages · Remember {11}",
                historyOpens, historyOpensUnprompted, timelapseOpens, timelapseOpensUnprompted, scrubs, scrubYearsTotal, pinTaps, plays, snapshots, bookPageTurns, bookPagesSeen, rememberTaps));
            sb.AppendLine($"Recall question: shown {recallShown}x · " + (recallAnswered ? "answered " + string.Join(",", storiesRecalled) : "not answered"));
            if (dilemmas.Any)
            {
                foreach (var kv in dilemmasByVariant) sb.AppendLine(kv.Value.ToText());
                if (dilemmasByVariant.Count != 1) sb.AppendLine(dilemmas.ToText());
            }
            return sb.ToString();
        }

        static IEnumerable<string> Pairs(SortedDictionary<int, int> d) { foreach (var kv in d) yield return $"d{kv.Key}:{kv.Value}"; }

        /// <summary>One flat JSON object, for the feedback issue's "session stats" and for scripts. Nothing personal in it.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder("{");
            void N(string k, double v) { if (sb.Length > 1) sb.Append(','); Json.Str(sb, k); sb.Append(':'); Json.Num(sb, v); }
            void T(string k, bool? v) { if (sb.Length > 1) sb.Append(','); Json.Str(sb, k); sb.Append(':').Append(v.HasValue ? (v.Value ? "true" : "false") : "null"); }
            void O(string k, double? v) { if (sb.Length > 1) sb.Append(','); Json.Str(sb, k); sb.Append(':'); if (v.HasValue) Json.Num(sb, v.Value); else sb.Append("null"); }
            N("events", events); N("opens", opens); N("days_played", daysPlayed);
            T("d1", d1); T("d3", d3); T("d7", d7);
            O("secs_to_first_clean", secondsToFirstClean);
            N("eras_live", erasLive); N("eras_skipped", erasSkipped);
            N("catch_ups", catchUps); N("catch_up_avg_ms", catchUpAvgMs); N("catch_up_max_ms", catchUpMaxMs);
            N("cards_shown", cardsShown); N("cards_read", cardsRead); N("avg_dwell_s", avgDwellSeconds); N("cards_opened", cardsOpened);
            N("cards_flown_to", cardsFlownTo); N("show_me", showMe); N("skip_all", skipAll); N("peek_taps", peekTaps);
            N("history_opens", historyOpens); N("history_opens_unprompted", historyOpensUnprompted);
            N("timelapse_opens", timelapseOpens); N("timelapse_scrubs", scrubs); N("pin_taps", pinTaps); N("plays", plays);
            N("snapshots", snapshots); N("book_pages_seen", bookPagesSeen); N("remember_taps", rememberTaps);
            N("recall_shown", recallShown);
            if (dilemmas.Any)
            {
                void D(string pre, DilemmaStats s)
                {
                    N(pre + "raised", s.raised); N(pre + "shown", s.shown); N(pre + "answered", s.answered); N(pre + "lapsed", s.lapsed);
                    O(pre + "answered_share", s.AnsweredShare); O(pre + "answered_of_raised", s.AnsweredShareOfRaised); N(pre + "cheat_events", s.cheatEvents); N(pre + "answered_box", s.answeredBox); N(pre + "answered_card", s.answeredCard);
                    O(pre + "secs_median", s.MedianSeconds); O(pre + "secs_min", s.MinSeconds); O(pre + "secs_max", s.MaxSeconds);
                    N(pre + "callbacks_shown", s.callbacksShown); N(pre + "callbacks_showme", s.callbackShowMe);
                }
                D("dilemma_", dilemmas);
                foreach (var kv in dilemmasByVariant) D("dilemma_" + kv.Key.ToLowerInvariant() + "_", kv.Value);
            }
            return sb.Append('}').ToString();
        }
    }

    class DilemmaTrack { public DilemmaStats stats; public bool raised, shown, answered, lapsed, debug; public int events, answeredBox, answeredCard, callbacksShown, callbackShowMe; public List<double> secs = new List<double>(); }

    public static class TelemetryAnalyzer
    {
        /// <summary>Computes the report from raw log lines (all boxes merged). <paramref name="nowUtc"/> decides which of D1/D3/D7 are measurable yet.</summary>
        public static TelemetryReport Analyze(IEnumerable<string> lines, DateTime nowUtc)
        {
            var r = new TelemetryReport();
            var evs = new List<TelemetryEvent>();
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                var e = TelemetryEvent.Parse(l);
                if (e == null) r.badLines++; else evs.Add(e);
            }
            // rotation and several boxes interleave files: order by time (stable for equal times)
            var idx = new List<int>(); for (int i = 0; i < evs.Count; i++) idx.Add(i);
            idx.Sort((a, b) => { int c = evs[a].utc.CompareTo(evs[b].utc); return c != 0 ? c : a.CompareTo(b); });
            var sorted = new List<TelemetryEvent>(evs.Count); foreach (var i in idx) sorted.Add(evs[i]);
            r.events = sorted.Count;
            if (sorted.Count == 0) return r;
            r.lastEventUtc = sorted[sorted.Count - 1].utc;

            // timezone per session (from its session_start), install time
            var tzBySession = new Dictionary<string, int>();
            int defaultTz = 0;
            DateTime? install = null, firstStart = null;
            foreach (var e in sorted)
            {
                if (e.name != Ev.SessionStart) continue;
                int tz = (int)e.Num("tz_min", 0);
                tzBySession[e.session ?? ""] = tz; defaultTz = tz;
                if (firstStart == null) firstStart = e.utc;
                if (install == null && e.Str("install_utc") != null &&
                    DateTime.TryParse(e.Str("install_utc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var iu)) install = iu;
            }
            r.installUtc = install ?? firstStart ?? sorted[0].utc;
            r.tzMinutes = defaultTz;
            int Tz(TelemetryEvent e) => tzBySession.TryGetValue(e.session ?? "", out var t) ? t : defaultTz;
            int installDay = LocalDay(r.installUtc, tzBySession.Count > 0 && firstStart.HasValue ? FirstTz(sorted, defaultTz) : defaultTz);
            int DayOf(TelemetryEvent e) => LocalDay(e.utc, Tz(e)) - installDay;

            var openDays = new HashSet<int>();
            var pages = new HashSet<int>();
            double dwellSum = 0, catchMsSum = 0;
            DateTime? firstSessionStart = firstStart;
            DateTime curSessionStart = default;
            string curSession = null;
            bool cleaned = false;
            var sessVariant = new Dictionary<string, string>();
            var sessVariants = new HashSet<string>(); // "session|variant": a session counts once under each variant it was in
            void CountSession(string session, string v) { if (v != null && sessVariants.Add((session ?? "") + "|" + v)) VariantStats(v).sessions++; }
            string lastVariant = null;
            var tracks = new Dictionary<string, DilemmaTrack>();

            DilemmaStats VariantStats(string v)
            {
                v = v ?? "?";
                if (!r.dilemmasByVariant.TryGetValue(v, out var s)) r.dilemmasByVariant[v] = s = new DilemmaStats { variant = v };
                return s;
            }
            string VariantOf(TelemetryEvent e) => sessVariant.TryGetValue(e.session ?? "", out var v) ? v : lastVariant;
            DilemmaTrack TrackOf(TelemetryEvent e)
            {
                var t = TrackOf0(e);
                t.events++;
                if (e.Num("debug") != 0) t.debug = true;
                return t;
            }
            DilemmaTrack TrackOf0(TelemetryEvent e)
            {
                string key = e.Str("box", "") + "|" + (int)e.Num("id", -1);
                if (!tracks.TryGetValue(key, out var t)) tracks[key] = t = new DilemmaTrack { stats = VariantStats(VariantOf(e)) };
                return t;
            }

            foreach (var e in sorted)
            {
                r.counts[e.name] = (r.counts.TryGetValue(e.name, out var n) ? n : 0) + 1;
                switch (e.name)
                {
                    case Ev.Variant:
                        sessVariant[e.session ?? ""] = lastVariant = e.Str("v");
                        VariantStats(lastVariant);
                        CountSession(e.session, lastVariant);
                        break;
                    case Ev.DilemmaRaised: TrackOf(e).raised = true; break;
                    case Ev.DilemmaShown: TrackOf(e).shown = true; break;
                    case Ev.DilemmaLapsed: TrackOf(e).lapsed = true; break;
                    case Ev.DilemmaAnswered:
                        {
                            var t = TrackOf(e);
                            t.answered = true;
                            if (e.Str("path") == "card") t.answeredCard++; else t.answeredBox++;
                            if (e.TryGet("secs", out _)) t.secs.Add(e.Num("secs"));
                            break;
                        }
                    case Ev.CallbackShown: TrackOf(e).callbacksShown++; break;
                    case Ev.CallbackShowMe: TrackOf(e).callbackShowMe++; break;
                    case Ev.SessionStart:
                        if (e.Str("variant") != null) { sessVariant[e.session ?? ""] = lastVariant = e.Str("variant"); VariantStats(lastVariant); CountSession(e.session, lastVariant); }
                        else if (lastVariant != null && !sessVariant.ContainsKey(e.session ?? "")) { sessVariant[e.session ?? ""] = lastVariant; CountSession(e.session, lastVariant); }
                        r.opens++;
                        int d = DayOf(e);
                        openDays.Add(d);
                        r.opensPerDay[d] = (r.opensPerDay.TryGetValue(d, out var oc) ? oc : 0) + 1;
                        curSession = e.session; curSessionStart = e.utc;
                        r.longestAwayHours = Math.Max(r.longestAwayHours, e.Num("away_h"));
                        break;
                    case Ev.Clean:
                        if (!cleaned)
                        {
                            cleaned = true;
                            DateTime from = firstSessionStart ?? r.installUtc;
                            r.secondsToFirstClean = Math.Max(0, (e.utc - from).TotalSeconds);
                            r.secondsIntoSessionAtFirstClean = e.session == curSession ? Math.Max(0, (e.utc - curSessionStart).TotalSeconds) : 0;
                        }
                        break;
                    case Ev.EraMoment:
                        bool skipped = e.Str("mode") == "skipped";
                        if (skipped) r.erasSkipped++; else r.erasLive++;
                        r.eraRows.Add($"era {(int)e.Num("era")}: {(skipped ? "skipped" : "live")}");
                        break;
                    case Ev.CatchUp:
                        r.catchUps++; catchMsSum += e.Num("ms"); r.catchUpMaxMs = Math.Max(r.catchUpMaxMs, e.Num("ms"));
                        r.longestAwayHours = Math.Max(r.longestAwayHours, e.Num("away_h"));
                        break;
                    case Ev.CardShown: r.cardsShown++; break;
                    case Ev.CardRead: r.cardsRead++; dwellSum += e.Num("dwell_ms") / 1000.0; break;
                    case Ev.CardOpened: r.cardsOpened++; break;
                    case Ev.CardFlownTo: r.cardsFlownTo++; break;
                    case Ev.ShowMe: r.showMe++; break;
                    case Ev.SkipAll: r.skipAll++; break;
                    case Ev.PeekTap: r.peekTaps++; break;
                    case Ev.HistoryOpen: r.historyOpens++; if (!e.Flag("prompted")) r.historyOpensUnprompted++; break;
                    case Ev.TimelapseOpen: r.timelapseOpens++; if (!e.Flag("prompted")) r.timelapseOpensUnprompted++; break;
                    case Ev.TimelapseScrub: r.scrubs++; r.scrubYearsTotal += Math.Abs(e.Num("to_year") - e.Num("from_year")); break;
                    case Ev.TimelapsePlay: r.plays++; break;
                    case Ev.PinTap: r.pinTaps++; break;
                    case Ev.Snapshot: r.snapshots++; break;
                    case Ev.RememberTap: r.rememberTaps++; break;
                    case Ev.BookPage: r.bookPageTurns++; pages.Add((int)e.Num("page")); break;
                    case Ev.RecallShown: r.recallShown++; break;
                    case Ev.RecallAnswered: r.recallAnswered = true; r.storiesRecalled.Add((int)e.Num("count")); break;
                }
            }
            foreach (var t in tracks.Values)
            {
                var s = t.stats;
                if (t.debug) { s.cheatEvents += t.events; continue; } // a cheat touched it: not a lab number
                s.answeredBox += t.answeredBox; s.answeredCard += t.answeredCard; s.secondsToAnswer.AddRange(t.secs);
                s.callbacksShown += t.callbacksShown; s.callbackShowMe += t.callbackShowMe;
                if (t.raised) s.raised++;
                if (t.shown) s.shown++;
                if (t.answered) { if (t.shown) s.answered++; }
                else if (t.lapsed) { if (t.shown) s.lapsed++; else s.lapsedUnseen++; }
            }
            foreach (var s in r.dilemmasByVariant.Values)
            {
                var a = r.dilemmas;
                a.sessions += s.sessions; a.raised += s.raised; a.shown += s.shown; a.answered += s.answered; a.lapsed += s.lapsed;
                a.lapsedUnseen += s.lapsedUnseen; a.answeredBox += s.answeredBox; a.answeredCard += s.answeredCard;
                a.cheatEvents += s.cheatEvents; a.secondsToAnswer.AddRange(s.secondsToAnswer); a.callbacksShown += s.callbacksShown; a.callbackShowMe += s.callbackShowMe;
            }
            r.daysPlayed = openDays.Count;
            r.bookPagesSeen = pages.Count;
            r.avgDwellSeconds = r.cardsRead > 0 ? dwellSum / r.cardsRead : 0;
            r.catchUpAvgMs = r.catchUps > 0 ? catchMsSum / r.catchUps : 0;

            int today = LocalDay(nowUtc, defaultTz) - installDay;
            r.d1 = Retained(openDays, 1, today);
            r.d3 = Retained(openDays, 3, today);
            r.d7 = Retained(openDays, 7, today);
            return r;
        }

        /// <summary>Day number counted from the install day, in the phone's own timezone.</summary>
        public static int DayIndex(DateTime installUtc, DateTime utc, int tzMinutes) => LocalDay(utc, tzMinutes) - LocalDay(installUtc, tzMinutes);

        static bool? Retained(HashSet<int> openDays, int day, int today)
        {
            if (openDays.Contains(day)) return true;
            return today > day ? false : (bool?)null; // "no" only once that day is over; today it may still happen
        }

        static int FirstTz(List<TelemetryEvent> sorted, int fallback)
        {
            foreach (var e in sorted) if (e.name == Ev.SessionStart) return (int)e.Num("tz_min", fallback);
            return fallback;
        }

        static int LocalDay(DateTime utc, int tzMinutes) => (int)Math.Floor((utc.ToUniversalTime().AddMinutes(tzMinutes) - new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays);
    }
}
