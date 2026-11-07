using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// Visit loop C7 (#374): the per-visit telemetry (moments seen live, gaps, choices by outcome, cards read), its persistence, the log
    /// format version and the recall decision. A scripted session of 3 visits; the numbers are exact. Pure C#.
    /// </summary>
    public class VisitTelemetryTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
        DateTime clock;
        MemoryTelemetryStorage disk;
        TelemetryLog log;
        SimCore sim;
        VisitTelemetryTracker tracker;
        readonly HashSet<int> mine = new HashSet<int>(); // the sim queues ceremonies of its own while it runs: only the scripted ones count

        [SetUp]
        public void SetUp()
        {
            clock = T0;
            disk = new MemoryTelemetryStorage();
            log = new TelemetryLog(disk, () => clock, "main");
            log.StartSession("open", 0, "0.1", "test", 0);
            sim = NewSim(7, new SimConfig { moments = true });
            sim.State.moments.Clear();
            tracker = new VisitTelemetryTracker(null, sim, null, "main");
            log.Visits = tracker;
        }

        HeldMoment Queue(MomentKind kind)
        {
            var m = new HeldMoment { id = sim.MomentPacing.nextId++, kind = kind, month = sim.State.month, priority = 0.6f };
            sim.State.moments.Add(m);
            mine.Add(m.id);
            return m;
        }

        void Watch(double minutes) { for (int i = 0; i < (int)Math.Round(minutes); i++) sim.Advance(1 / 60.0); sim.State.moments.RemoveAll(m => !mine.Contains(m.id)); tracker.Observe(sim, log); }

        /// <summary>The next moment plays (the budget is lifted) and the view finishes it.</summary>
        HeldMoment Play(bool played = true)
        {
            sim.DebugMomentDueNow();
            var started = sim.StartDueMoments();
            Assert.AreEqual(1, started.Count, "one moment starts: " + sim.MomentWaitReason());
            tracker.Observe(sim, log);
            sim.FinishMoment(started[0].id, played);
            tracker.Observe(sim, log);
            return started[0];
        }

        Dilemma Raise(int id)
        {
            var d = new Dilemma { id = id, defId = "d" + id, month = sim.State.month, raisedHours = sim.State.clockHours, state = DilemmaState.Open, visits = 1 };
            sim.State.dilemmas.Add(d);
            tracker.Observe(sim, log);
            return d;
        }

        void End(Dilemma d, DilemmaEnd end)
        {
            d.end = end;
            d.state = end == DilemmaEnd.Answered ? DilemmaState.Answered : DilemmaState.Lapsed;
            d.endMonth = sim.State.month;
            tracker.Observe(sim, log);
        }

        List<TelemetryEvent> Events(string name) => log.ReadAllLines().Select(TelemetryEvent.Parse).Where(e => e != null && e.name == name).ToList();

        /// <summary>Visit 1: 2 moments live (a wedding at the open, a festival 6 watched minutes later), a card from a full queue; one
        /// choice raised. Visit 2: a newborn stopped (a card), the choice answered, a second raised. Visit 3 (two opens): the second
        /// let to the people, a third lapsed after three visits.</summary>
        void ScriptedSession()
        {
            // ---- visit 1
            tracker.Open(log, sim, 0);
            var first = Queue(MomentKind.Callback); var second = Queue(MomentKind.Festival);
            var old = Queue(MomentKind.Festival); old.state = HeldMomentState.Carded; old.cardReason = MomentCardReason.QueueFull; old.endMonth = sim.State.month;
            tracker.Observe(sim, log);
            Watch(1);
            Raise(100);
            Play();                        // the welcome: the top one (the wedding: 0.6; the festival is the same, the wedding is older/first id)
            Watch(6);
            Play();                        // the festival, 6 watched minutes on
            tracker.Close(log);
            // ---- visit 2 (an hour away)
            clock = clock.AddHours(1);
            tracker.Open(log, sim, 1.0);
            Queue(MomentKind.Festival);
            Watch(2);
            Play(false);                   // handed over, but could not be staged: stopped, and a card
            var d100 = sim.State.dilemmas.First(d => d.id == 100);
            End(d100, DilemmaEnd.Answered);
            Raise(101);
            tracker.Close(log);
            // ---- visit 3 (two opens: 20 minutes away, then 6 minutes away)
            clock = clock.AddHours(0.33);
            tracker.Open(log, sim, 0.33);
            log.CardShown("c1", "story", 0, 2); log.CardShown("c2", "story", 1, 2);
            log.CardRead("c1", 3.0);
            End(sim.State.dilemmas.First(d => d.id == 101), DilemmaEnd.LetThemDecide);
            Raise(102);
            End(sim.State.dilemmas.First(d => d.id == 102), DilemmaEnd.Visits);
            tracker.Close(log);
            clock = clock.AddMinutes(6);
            tracker.Open(log, sim, 0.1);
            tracker.Close(log);
        }

        [Test]
        public void ThreeVisitsGiveExactNumbers()
        {
            ScriptedSession();
            var sums = Events(Ev.VisitSum);
            Assert.AreEqual(3, sums.Select(e => (int)e.Num("n")).Distinct().Count(), "three visits");
            var v1 = sums.Last(e => e.Num("n") == 1);
            Assert.AreEqual(1, v1.Num("opens")); Assert.AreEqual(2, v1.Num("live")); Assert.AreEqual(0, v1.Num("stopped")); Assert.AreEqual(1, v1.Num("carded"));
            Assert.AreEqual(7, v1.Num("watched_min"), 0.01);
            Assert.AreEqual(1, v1.Num("raised")); Assert.AreEqual(0, v1.Num("answered"));
            Assert.AreEqual(6, double.Parse(v1.Str("gaps"), System.Globalization.CultureInfo.InvariantCulture), 0.01, "the gap between the two live moments");
            var v2 = sums.Last(e => e.Num("n") == 2);
            Assert.AreEqual(1, v2.Num("opens")); Assert.AreEqual(0, v2.Num("live")); Assert.AreEqual(1, v2.Num("stopped")); Assert.AreEqual(1, v2.Num("carded"));
            Assert.AreEqual(2, v2.Num("watched_min"), 0.01);
            Assert.AreEqual(1, v2.Num("raised")); Assert.AreEqual(1, v2.Num("answered"), "choice 100 answered on visit 2");
            var v3 = sums.Last(e => e.Num("n") == 3);
            Assert.AreEqual(2, v3.Num("opens")); Assert.AreEqual(1, v3.Num("raised")); Assert.AreEqual(1, v3.Num("let_decide")); Assert.AreEqual(1, v3.Num("lapsed"));
            Assert.AreEqual(2, v3.Num("cards_shown")); Assert.AreEqual(1, v3.Num("cards_read"));
            Assert.AreEqual(2, Events(Ev.MomentLive).Count(e => e.Str("result") == "played"));
            Assert.AreEqual(1, Events(Ev.MomentLive).Count(e => e.Str("result") == "stopped"));
            Assert.AreEqual(1, Events(Ev.MomentCard).Count()); Assert.AreEqual("queuefull", Events(Ev.MomentCard)[0].Str("why"));
            var ends = Events(Ev.ChoiceEnd);
            Assert.AreEqual(new[] { "answered", "let_decide", "lapsed" }, ends.Select(e => e.Str("end")).ToArray());
            Assert.AreEqual(new[] { 2.0, 3.0, 3.0 }, ends.Select(e => e.Num("n")).ToArray(), "the visit each choice ended on");
            Assert.AreEqual(4, Events(Ev.VisitStart).Count); Assert.IsFalse(Events(Ev.VisitStart).Last().Flag("fresh"), "the fourth open is inside visit 3");
        }

        [Test]
        public void TheReportFoldsTheVisitsIn()
        {
            ScriptedSession();
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock).visitStats;
            Assert.AreEqual(3, r.visits); Assert.AreEqual(4, r.opens);
            Assert.AreEqual(2, r.live); Assert.AreEqual(1, r.stopped); Assert.AreEqual(2, r.carded);
            Assert.AreEqual(3, r.raised); Assert.AreEqual(1, r.answered); Assert.AreEqual(1, r.letDecide); Assert.AreEqual(1, r.lapsed);
            Assert.AreEqual(1, r.answerHours.Count);
            Assert.AreEqual(1, r.gapCount); Assert.AreEqual(6, r.AvgGapMin.Value, 0.01);
            Assert.AreEqual(2.0 / 3, r.AvgMomentsPerVisit.Value, 1e-9);
            StringAssert.Contains("Visits: 3", TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock).ToText());
            StringAssert.Contains("\"moments_live\":2", TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock).ToJson());
        }

        [Test]
        public void AVisitOfNothingWritesNoSecondSummary()
        {
            tracker.Open(log, sim, 0);
            tracker.Close(log); tracker.Close(log);
            Assert.AreEqual(1, Events(Ev.VisitSum).Count, "closing again with nothing new writes nothing");
        }

        // ---- persistence ----

        [Test]
        public void TheRunningVisitSurvivesARestart()
        {
            tracker.Open(log, sim, 0);
            Queue(MomentKind.Festival);
            Watch(1);
            Play();
            Raise(100);
            log.CardRead("c", 2);
            tracker.Close(log);
            var json = disk.files[VisitTelemetryTracker.SidecarName("main")];
            var back = VisitTelemetryState.FromJson(json);
            Assert.AreEqual(1, back.visit); Assert.AreEqual(1, back.live); Assert.AreEqual(1, back.raised); Assert.AreEqual(1, back.cardsRead);
            Assert.AreEqual(1, back.opens); Assert.AreEqual(tracker.State.watched, back.watched, 1e-9);
            // a restart: a new log, a new tracker over the same sim, the loaded state
            var log2 = new TelemetryLog(disk, () => clock, "main");
            var t2 = new VisitTelemetryTracker(VisitTelemetryTracker.Load(log2), sim, null, "main");
            log2.Visits = t2;
            t2.Observe(sim, log2);
            Assert.AreEqual(1, t2.State.live, "the played moment and the raised choice are not counted twice");
            Assert.AreEqual(1, t2.State.raised);
            t2.Open(log2, sim, 0.05);                      // five minutes away: the same visit, one more open
            Assert.AreEqual(1, t2.State.visit); Assert.AreEqual(2, t2.State.opens);
            t2.Open(log2, sim, 2);                         // two hours away: the next visit, fresh counters
            Assert.AreEqual(2, t2.State.visit); Assert.AreEqual(1, t2.State.opens); Assert.AreEqual(0, t2.State.live);
        }

        [Test]
        public void WhatEndedWhileTheAppWasDeadIsCountedOnTheNextLook()
        {
            tracker.Open(log, sim, 0);
            var m = Queue(MomentKind.Festival);
            var d = Raise(100);
            tracker.Observe(sim, log);
            tracker.Close(log);                            // closed with the newborn waiting and the choice open
            m.state = HeldMomentState.Carded; m.cardReason = MomentCardReason.HeldTooLong;   // away: it aged out
            End(d, DilemmaEnd.Visits);
            var log2 = new TelemetryLog(disk, () => clock, "main");
            var t2 = new VisitTelemetryTracker(VisitTelemetryTracker.Load(log2), sim, null, "main");
            t2.Observe(sim, log2);
            Assert.AreEqual(1, t2.State.carded); Assert.AreEqual(1, t2.State.lapsed);
        }

        [Test]
        public void ExistingHistoryIsNotRecounted()
        {
            var m = Queue(MomentKind.Wedding); m.state = HeldMomentState.Played;
            var d = Raise(5); d.state = DilemmaState.Answered; d.end = DilemmaEnd.Answered;
            var t2 = new VisitTelemetryTracker(null, sim, null, "main");
            t2.Open(log, sim, 0);
            t2.Observe(sim, log);
            Assert.AreEqual(0, t2.State.live); Assert.AreEqual(0, t2.State.raised); Assert.AreEqual(0, t2.State.answered);
        }

        [Test]
        public void CheatsAreLeftOut()
        {
            var t2 = new VisitTelemetryTracker(null, sim, id => id == 9, "main");
            t2.Open(log, sim, 0);
            var m = Queue(MomentKind.Festival); m.cheat = true; m.state = HeldMomentState.Played;
            var d = new Dilemma { id = 9, state = DilemmaState.Answered, end = DilemmaEnd.Answered };
            sim.State.dilemmas.Add(d);
            t2.Observe(sim, log);
            Assert.AreEqual(0, t2.State.live); Assert.AreEqual(0, t2.State.raised); Assert.AreEqual(0, t2.State.answered);
        }

        [Test]
        public void ADeadDiskDoesNotThrow()
        {
            disk.failWrites = true;
            Assert.DoesNotThrow(() => { tracker.Open(log, sim, 0); tracker.Close(log); });
            Assert.Greater(log.WriteErrors, 0);
        }

        [Test]
        public void ATornStateFileStartsFresh()
        {
            var s = VisitTelemetryState.FromJson("{\"visit\":4,\"live\"");
            Assert.AreEqual(0, s.visit);
            Assert.AreEqual(0, VisitTelemetryState.FromJson("").visit);
        }

        // ---- the version ----

        [Test]
        public void SessionStartCarriesTheLogVersionAndAnOldLogReadsAsVersionOne()
        {
            Assert.AreEqual(2, Ev.TelemetryVersion, "the visit events are format version 2");
            var start = log.ReadAllLines().Select(TelemetryEvent.Parse).First(e => e.name == Ev.SessionStart);
            Assert.AreEqual(Ev.TelemetryVersion, start.Num("tv"));
            Assert.AreEqual(2, TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock).logVersion);
            var old = new[] { "{\"t\":\"2026-11-02T09:00:00.000Z\",\"s\":\"a\",\"e\":\"session_start\",\"reason\":\"open\",\"tz_min\":0,\"install_utc\":\"2026-11-02T09:00:00.000Z\"}" };
            var r = TelemetryAnalyzer.Analyze(old, T0);
            Assert.AreEqual(1, r.logVersion); Assert.IsFalse(r.visitStats.Any);
        }

        // ---- the card hooks and the debug line ----

        [Test]
        public void CardHooksCountShownAndReadAndTheDebugLineSaysSo()
        {
            tracker.Open(log, sim, 0);
            log.CardShown("a", "story", 0, 1); log.CardRead("a", 1.5);
            StringAssert.Contains("visit 1: 1 open(s)", tracker.Line());
            StringAssert.Contains("cards 1 shown, 1 read", tracker.Line());
            var noTracker = new TelemetryLog(new MemoryTelemetryStorage(), () => clock, "x");
            Assert.DoesNotThrow(() => { noTracker.CardShown("a", "story", 0, 1); noTracker.CardRead("a", 1); });
        }

        // ---- recall of seen vs read ----

        [Test]
        public void RecallIsAskedRarelyAndOnlyWhenThereIsSomethingToRecall()
        {
            var s = new VisitTelemetryState { visit = 2, liveSinceAsk = 1 };
            Assert.IsFalse(VisitRecall.ShouldAsk(s, false), "not before visit 3");
            s.visit = 3;
            Assert.IsTrue(VisitRecall.ShouldAsk(s, false));
            Assert.IsFalse(VisitRecall.ShouldAsk(s, true), "not while something is on screen");
            s.liveSinceAsk = 0; Assert.IsFalse(VisitRecall.ShouldAsk(s, false), "nothing was seen or read");
            s.readSinceAsk = 2; Assert.IsTrue(VisitRecall.ShouldAsk(s, false));
            s.lastAskVisit = 3; s.visit = 6; Assert.IsFalse(VisitRecall.ShouldAsk(s, false), "every 4 visits");
            s.visit = 7; Assert.IsTrue(VisitRecall.ShouldAsk(s, false));
            s.dismissedInARow = VisitRecall.MaxDismissedInARow; Assert.IsFalse(VisitRecall.ShouldAsk(s, false), "given up after two dismissals");
            StringAssert.DoesNotContain("tank", VisitRecall.Question); StringAssert.DoesNotContain("glass", VisitRecall.Question);
        }

        [Test]
        public void RecallAnswerIsStoredNextToWhatWasSeenLiveAndRead()
        {
            tracker.Open(log, sim, 0);
            Queue(MomentKind.Festival); Watch(1); Play();
            log.CardRead("c1", 2); log.CardRead("c2", 2);
            tracker.RecallShown(log);
            tracker.RecallAnswered(log, 2, 1);
            var ev = Events(Ev.VisitRecall);
            Assert.AreEqual(new[] { "shown", "answered" }, ev.Select(e => e.Str("outcome")).ToArray());
            Assert.AreEqual(1, ev[1].Num("live_since")); Assert.AreEqual(2, ev[1].Num("read_since"));
            Assert.AreEqual(2, ev[1].Num("named")); Assert.AreEqual(1, ev[1].Num("named_seen"));
            Assert.AreEqual(0, tracker.State.liveSinceAsk, "the next question counts from here");
            Assert.AreEqual(1, tracker.State.lastAskVisit);
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock).visitStats;
            Assert.AreEqual(1, r.recallShown); Assert.AreEqual(1, r.recallAnswered); Assert.AreEqual(2, r.recallNamed); Assert.AreEqual(1, r.recallNamedSeen);
        }
    }
}
