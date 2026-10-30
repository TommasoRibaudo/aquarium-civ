using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>The event log, the week-long metrics and the day-3 recall decision (#81, #100). Pure C#, no Editor.</summary>
    public class TelemetryTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc); // a Monday morning
        DateTime clock;
        MemoryTelemetryStorage disk;

        [SetUp] public void SetUp() { clock = T0; disk = new MemoryTelemetryStorage(); }

        TelemetryLog NewLog(TelemetryOptions o = null, string box = "main") => new TelemetryLog(disk, () => clock, box, o);

        void Open(TelemetryLog log, string reason = "open", double away = 0, int tz = 0) => log.StartSession(reason, away, "0.1", "test", tz);

        // ---- the line format ----

        [Test] public void EventRoundTripsThroughOneLine()
        {
            var e = new TelemetryEvent { utc = T0, session = "ab12", name = Ev.CardRead }.Set("card", "c\"1\n").Set("dwell_ms", 1234.5).Set("ok", true).Set("n", 3);
            var line = e.ToLine();
            Assert.That(line, Does.Not.Contain("\n"));
            var back = TelemetryEvent.Parse(line);
            Assert.That(back.name, Is.EqualTo(Ev.CardRead));
            Assert.That(back.utc, Is.EqualTo(T0));
            Assert.That(back.session, Is.EqualTo("ab12"));
            Assert.That(back.Str("card"), Is.EqualTo("c\"1\n"));
            Assert.That(back.Num("dwell_ms"), Is.EqualTo(1234.5));
            Assert.That(back.Flag("ok"), Is.True);
            Assert.That(back.Num("n"), Is.EqualTo(3));
        }

        [Test] public void TornAndJunkLinesAreRejected()
        {
            var line = new TelemetryEvent { utc = T0, session = "s", name = "x" }.Set("k", "value").ToLine();
            Assert.That(TelemetryEvent.Parse(line.Substring(0, line.Length - 5)), Is.Null);
            Assert.That(TelemetryEvent.Parse(""), Is.Null);
            Assert.That(TelemetryEvent.Parse("not json"), Is.Null);
            Assert.That(TelemetryEvent.Parse("{\"t\":\"2026-11-02T09:00:00.000Z\"}"), Is.Null);
        }

        // ---- the writer ----

        [Test] public void SessionStartIsOnDiskAtOnce_OthersWaitForFlush()
        {
            var log = NewLog();
            Open(log);
            Assert.That(log.ReadAllLines().Count, Is.EqualTo(1));
            log.CardShown("c1", "birth", 0, 3);
            Assert.That(log.PendingCount, Is.EqualTo(1));
            Assert.That(disk.ReadAll("box-main.jsonl").Split('\n').Count(l => l.Length > 0), Is.EqualTo(1), "still buffered");
            log.EndSession("pause", 12); // flush on pause
            Assert.That(log.PendingCount, Is.EqualTo(0));
            Assert.That(disk.ReadAll("box-main.jsonl").Split('\n').Count(l => l.Length > 0), Is.EqualTo(3));
        }

        [Test] public void FlushesWhenFullOrWhenOldEnough()
        {
            var log = NewLog(new TelemetryOptions { flushEvery = 3, flushAfterSeconds = 10 });
            log.PeekTap("a"); log.PeekTap("b");
            Assert.That(log.PendingCount, Is.EqualTo(2));
            log.PeekTap("c");
            Assert.That(log.PendingCount, Is.EqualTo(0));
            log.PeekTap("d");
            clock = clock.AddSeconds(9); log.Tick(); Assert.That(log.PendingCount, Is.EqualTo(1));
            clock = clock.AddSeconds(2); log.Tick(); Assert.That(log.PendingCount, Is.EqualTo(0));
        }

        [Test] public void RotationKeepsTheDiskUnderTheCap()
        {
            var o = new TelemetryOptions { maxFileBytes = 600, keepRotated = 2, flushEvery = 1 };
            var log = NewLog(o);
            for (int i = 0; i < 60; i++) log.PeekTap("card" + i);
            long total = disk.files.Where(f => f.Key.StartsWith("box-")).Sum(f => (long)System.Text.Encoding.UTF8.GetByteCount(f.Value));
            Assert.That(total, Is.LessThanOrEqualTo(600L * 3));
            Assert.That(disk.files.Keys, Does.Contain("box-main.1.jsonl"));
            Assert.That(disk.files.Keys, Does.Contain("box-main.2.jsonl"));
            Assert.That(disk.files.Keys, Does.Not.Contain("box-main.3.jsonl"));
            // newest event is still there, order oldest to newest across files
            var lines = log.ReadAllLines();
            Assert.That(lines.Last(), Does.Contain("card59"));
            var cards = lines.Select(l => TelemetryEvent.Parse(l).Str("card")).Select(c => int.Parse(c.Substring(4))).ToList();
            Assert.That(cards, Is.Ordered);
        }

        [Test] public void ATornLastLineCostsOnlyThatLine()
        {
            var log = NewLog(new TelemetryOptions { flushEvery = 1 });
            log.PeekTap("a");
            disk.files["box-main.jsonl"] += "{\"t\":\"2026-11-02T09:00:01.000Z\",\"s\":\"x\",\"e\":\"peek"; // killed mid-write
            var log2 = NewLog(new TelemetryOptions { flushEvery = 1 });
            log2.PeekTap("b");
            var report = TelemetryAnalyzer.Analyze(log2.ReadAllLines(), clock);
            Assert.That(report.events, Is.EqualTo(2));
            Assert.That(report.badLines, Is.EqualTo(1));
            Assert.That(report.peekTaps, Is.EqualTo(2));
        }

        [Test] public void AFailingDiskNeverThrowsAndKeepsEventsUntilItRecovers()
        {
            var log = NewLog(new TelemetryOptions { flushEvery = 1, maxPending = 5 });
            disk.failWrites = true;
            for (int i = 0; i < 8; i++) log.PeekTap("c" + i);
            Assert.That(log.WriteErrors, Is.GreaterThan(0));
            Assert.That(log.PendingCount, Is.EqualTo(5));
            Assert.That(log.Dropped, Is.EqualTo(3));
            disk.failWrites = false;
            Assert.That(log.Flush(), Is.True);
            Assert.That(log.ReadAllLines().Count, Is.EqualTo(5));
        }

        [Test] public void BoxesGetTheirOwnFiles_AndIdsCannotEscapeTheFolder()
        {
            var log = NewLog(box: "../evil box");
            log.PeekTap("a");
            log.SwitchBox("second");
            log.PeekTap("b");
            log.Flush();
            Assert.That(disk.files.Keys.Where(k => k.StartsWith("box-")), Is.EquivalentTo(new[] { "box-evilbox.jsonl", "box-second.jsonl" }));
        }

        [Test] public void InstallTimeIsStoredOnce()
        {
            NewLog();
            clock = clock.AddDays(5);
            var again = NewLog();
            Assert.That(again.InstallUtc, Is.EqualTo(T0));
        }

        [Test] public void NoPersonalDataInTheSessionLine()
        {
            var log = NewLog();
            Open(log);
            var line = log.ReadAllLines()[0];
            foreach (var banned in new[] { "nick", "name", "email", "device", "tester", "ip" })
                Assert.That(TelemetryEvent.Parse(line).props.Select(p => p.Key), Has.None.EqualTo(banned));
        }

        [Test] public void ShortBackgroundTripsAreNotNewOpens()
        {
            var log = NewLog();
            Assert.That(log.ShouldStartNewSession(60), Is.False);
            Assert.That(log.ShouldStartNewSession(300), Is.True);
        }

        // ---- dwell ----

        [Test] public void DwellPausesWhileCovered()
        {
            var d = new DwellTracker();
            d.Show("c1", 100);
            d.Cover(103);          // person card opens after 3 s
            d.Cover(104);          // fly-to on top
            d.Uncover(110); d.Uncover(112);
            var r = d.End(115);    // 3 s before + 3 s after
            Assert.That(r.Value.seconds, Is.EqualTo(6).Within(1e-9));
            Assert.That(r.Value.card, Is.EqualTo("c1"));
            Assert.That(d.End(120), Is.Null);
        }

        [Test] public void DwellEndedWhileCoveredCountsOnlyTimeBeforeTheCover()
        {
            var d = new DwellTracker();
            d.Show("c1", 0); d.Cover(2);
            Assert.That(d.End(50).Value.seconds, Is.EqualTo(2).Within(1e-9));
        }

        [Test] public void ShowingTheNextCardEndsThePrevious()
        {
            var d = new DwellTracker();
            d.Show("a", 0);
            var prev = d.Show("b", 4);
            Assert.That(prev.Value.card, Is.EqualTo("a"));
            Assert.That(prev.Value.seconds, Is.EqualTo(4).Within(1e-9));
            Assert.That(d.Current, Is.EqualTo("b"));
        }

        // ---- the scripted week ----

        /// <summary>A tester who plays on day 0, 1, 3 and 4 (not 2), cleans after 40 s, watches one era and skips the next.</summary>
        List<string> ScriptedWeek(int tz = 0)
        {
            var log = NewLog();
            Open(log, tz: tz);                                     // day 0, first open
            clock = clock.AddSeconds(40); log.Cleaned("wipe", true);
            log.CardShown("c1", "birth", 0, 2); log.CardRead("c1", 8.5);
            log.CardShown("c2", "death", 1, 2); log.CardOpened("c2", 7); log.CardFlownTo("c2"); log.ShowMe("c2"); log.CardRead("c2", 4);
            log.PeekTap("c1"); log.SkipAll(0);
            log.EraMomentEnded(2, false, 25);
            log.EndSession("pause", 120);
            clock = T0.AddDays(1).AddHours(1); Open(log, "open", 24, tz);   // day 1
            log.CaughtUp(24, 24, 36, 180); log.HistoryOpened("book", false); log.BookPageTurned(1); log.BookPageTurned(2); log.BookPageTurned(2);
            log.TimelapseOpened(true); log.TimelapseScrubbed(10, 40); log.TimelapsePlayed(); log.PinTapped(3); log.SnapshotTaken(); log.RememberTapped(3);
            log.EndSession("pause", 60);
            clock = T0.AddDays(3).AddHours(2); Open(log, "open", 49, tz);   // day 3
            log.CaughtUp(49, 49, 12, 600); log.EraMomentEnded(3, true, 2);
            log.EndSession("quit", 30);
            clock = T0.AddDays(4); Open(log, "open", 20, tz);               // day 4
            log.EndSession("pause", 10);
            return log.ReadAllLines();
        }

        [Test] public void WeekMetrics()
        {
            var r = TelemetryAnalyzer.Analyze(ScriptedWeek(), T0.AddDays(5));
            Assert.That(r.opens, Is.EqualTo(4));
            Assert.That(r.opensPerDay, Is.EquivalentTo(new Dictionary<int, int> { { 0, 1 }, { 1, 1 }, { 3, 1 }, { 4, 1 } }));
            Assert.That(r.daysPlayed, Is.EqualTo(4));
            Assert.That(r.d1, Is.True);
            Assert.That(r.d3, Is.True);
            Assert.That(r.d7, Is.Null, "day 7 has not come yet");
            Assert.That(r.secondsToFirstClean, Is.EqualTo(40).Within(0.01));
            Assert.That(r.secondsIntoSessionAtFirstClean, Is.EqualTo(40).Within(0.01));
            Assert.That(r.erasLive, Is.EqualTo(1));
            Assert.That(r.erasSkipped, Is.EqualTo(1));
            Assert.That(r.catchUps, Is.EqualTo(2));
            Assert.That(r.catchUpMaxMs, Is.EqualTo(600));
            Assert.That(r.catchUpAvgMs, Is.EqualTo(390));
            Assert.That(r.longestAwayHours, Is.EqualTo(49));
        }

        [Test] public void M4Metrics()
        {
            var r = TelemetryAnalyzer.Analyze(ScriptedWeek(), T0.AddDays(5));
            Assert.That(r.cardsShown, Is.EqualTo(2));
            Assert.That(r.cardsRead, Is.EqualTo(2));
            Assert.That(r.avgDwellSeconds, Is.EqualTo(6.25).Within(1e-6));
            Assert.That(r.cardsOpened, Is.EqualTo(1));
            Assert.That(r.cardsFlownTo, Is.EqualTo(1));
            Assert.That(r.showMe, Is.EqualTo(1));
            Assert.That(r.skipAll, Is.EqualTo(1));
            Assert.That(r.peekTaps, Is.EqualTo(1));
            Assert.That(r.historyOpens, Is.EqualTo(1));
            Assert.That(r.historyOpensUnprompted, Is.EqualTo(1));
            Assert.That(r.timelapseOpens, Is.EqualTo(1));
            Assert.That(r.timelapseOpensUnprompted, Is.EqualTo(0));
            Assert.That(r.scrubs, Is.EqualTo(1));
            Assert.That(r.scrubYearsTotal, Is.EqualTo(30));
            Assert.That(r.pinTaps, Is.EqualTo(1));
            Assert.That(r.plays, Is.EqualTo(1));
            Assert.That(r.snapshots, Is.EqualTo(1));
            Assert.That(r.bookPageTurns, Is.EqualTo(3));
            Assert.That(r.bookPagesSeen, Is.EqualTo(2));
            Assert.That(r.rememberTaps, Is.EqualTo(1));
        }

        [Test] public void MissedDaysAreNo_AndFutureDaysAreUnknown()
        {
            var lines = new List<string>();
            var log = NewLog();
            Open(log);                                              // only ever opened on day 0
            lines = log.ReadAllLines();
            var r = TelemetryAnalyzer.Analyze(lines, T0.AddDays(8));
            Assert.That(r.d1, Is.False); Assert.That(r.d3, Is.False); Assert.That(r.d7, Is.False);
            r = TelemetryAnalyzer.Analyze(lines, T0.AddHours(5));
            Assert.That(r.d1, Is.Null); Assert.That(r.d3, Is.Null);
            r = TelemetryAnalyzer.Analyze(lines, T0.AddDays(1).AddHours(1));
            Assert.That(r.d1, Is.False.Or.Null, "day 1 is not over yet");
            Assert.That(r.d1, Is.Null);
        }

        [Test] public void DaysFollowThePhonesTimezone()
        {
            // 23:30 UTC on Monday is already Tuesday in UTC+2: an open at 22:30 UTC the next day is day 1, not day 0/2
            clock = new DateTime(2026, 11, 2, 23, 30, 0, DateTimeKind.Utc);
            var log = NewLog();
            Open(log, tz: 120);
            clock = clock.AddHours(1); Open(log, "open", 1, 120);   // 00:30 UTC Tuesday = 02:30 local Tuesday... still day 0 local (Tue)
            clock = clock.AddHours(22); Open(log, "open", 22, 120); // 22:30 UTC Tue = 00:30 local Wed = day 1
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock.AddDays(2));
            Assert.That(r.opensPerDay[0], Is.EqualTo(2));
            Assert.That(r.opensPerDay[1], Is.EqualTo(1));
            Assert.That(r.d1, Is.True);
        }

        [Test] public void SurvivesRotationBecauseInstallTimeIsInTheSessionLine()
        {
            var o = new TelemetryOptions { maxFileBytes = 400, keepRotated = 1, flushEvery = 1 };
            var log = NewLog(o);
            Open(log);
            clock = T0.AddDays(1); Open(log);
            for (int i = 0; i < 40; i++) { clock = clock.AddMinutes(1); log.PeekTap("x"); }  // pushes day-0's open out of the files
            clock = T0.AddDays(3); Open(log);
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), T0.AddDays(5));
            Assert.That(r.installUtc, Is.EqualTo(T0));
            Assert.That(r.d3, Is.True);
        }

        [Test] public void EmptyLogGivesAnEmptyReport()
        {
            var r = TelemetryAnalyzer.Analyze(new string[0], T0);
            Assert.That(r.events, Is.EqualTo(0));
            Assert.That(r.ToText(), Does.Contain("0 events"));
            Assert.That(r.ToJson(), Does.StartWith("{"));
        }

        [Test] public void ReportReadsAsTextAndJson()
        {
            var r = TelemetryAnalyzer.Analyze(ScriptedWeek(), T0.AddDays(5));
            var text = r.ToText();
            Assert.That(text, Does.Contain("D1 yes · D3 yes · D7 n/a"));
            Assert.That(text, Does.Contain("1 watched live · 1 skipped"));
            var json = r.ToJson();
            Assert.That(json, Does.Contain("\"d7\":null"));
            Assert.That(json, Does.Contain("\"eras_skipped\":1"));
            Assert.That(TelemetryEvent.Parse("{\"t\":\"2026-11-02T09:00:00.000Z\",\"e\":\"x\"}"), Is.Not.Null);
        }

        // ---- the day-3 recall decision ----

        static RecallContext Ctx(int day, double secs = 60, bool busy = false, int cards = 5) =>
            new RecallContext { dayIndex = day, sessionSeconds = secs, busy = busy, cardsEverShown = cards };

        [Test] public void RecallIsAskedFromDayThree_AtACalmMoment()
        {
            var s = new RecallState();
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(2)), Is.False);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(3)), Is.True);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(3, secs: 5)), Is.False, "let the return finish");
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(3, busy: true)), Is.False, "never over an era moment or a card");
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(3, cards: 0)), Is.False, "nothing to recall");
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(5)), Is.True, "a tester who skipped day 3 is asked on their next open");
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(8)), Is.False, "too late to mean day 3");
        }

        [Test] public void RecallIsAskedOncePerDay_AndStopsWhenAnswered()
        {
            var s = new RecallState();
            RecallPrompt.Shown(s, 3);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(3)), Is.False, "not twice in a day");
            RecallPrompt.Dismissed(s);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(4)), Is.True);
            RecallPrompt.Shown(s, 4); RecallPrompt.Dismissed(s);
            RecallPrompt.Shown(s, 5); RecallPrompt.Dismissed(s);
            Assert.That(s.dismissed, Is.EqualTo(3));
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(6)), Is.False, "three closes is enough");
            var s2 = new RecallState();
            RecallPrompt.Answered(s2);
            Assert.That(RecallPrompt.ShouldAsk(s2, Ctx(4)), Is.False);
        }

        [Test] public void RecallStateSurvivesSerialisation()
        {
            var s = new RecallState { shown = 2, dismissed = 1, lastShownDay = 4 };
            var back = UnityEngine.JsonUtility.FromJson<RecallState>(UnityEngine.JsonUtility.ToJson(s));
            Assert.That(back.lastShownDay, Is.EqualTo(4));
            Assert.That(back.dismissed, Is.EqualTo(1));
        }

        [Test] public void RecallAnswerIsLoggedAsANumberOnly()
        {
            var log = NewLog();
            Open(log);
            log.RecallShown(3); log.RecallAnswered(2);
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), T0.AddDays(4));
            Assert.That(r.recallShown, Is.EqualTo(1));
            Assert.That(r.recallAnswered, Is.True);
            Assert.That(r.storiesRecalled, Is.EqualTo(new[] { 2 }));
        }

        [Test] public void RealFilesWork()
        {
            var dir = Path.Combine(Path.GetTempPath(), "aqciv-tel-" + Guid.NewGuid().ToString("N"));
            try
            {
                var log = new TelemetryLog(new FileTelemetryStorage(dir), () => clock, "main");
                Open(log); log.PeekTap("a"); log.EndSession("pause", 3);
                var again = new TelemetryLog(new FileTelemetryStorage(dir), () => clock, "main");
                Assert.That(again.ReadAllLines().Count, Is.EqualTo(3));
                Assert.That(again.InstallUtc, Is.EqualTo(T0));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
