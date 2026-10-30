using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AquariumCiv.Game
{
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
            return sb.Append('}').ToString();
        }
    }

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

            foreach (var e in sorted)
            {
                r.counts[e.name] = (r.counts.TryGetValue(e.name, out var n) ? n : 0) + 1;
                switch (e.name)
                {
                    case Ev.SessionStart:
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
