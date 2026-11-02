using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>P7 (#225): the dilemma telemetry events, the tracker that diffs the sim, the per-variant analysis and the day-2 recall window. Pure C#.</summary>
    public class DilemmaTelemetryTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
        DateTime clock;
        MemoryTelemetryStorage disk;

        [SetUp] public void SetUp() { clock = T0; disk = new MemoryTelemetryStorage(); }

        TelemetryLog NewLog(string variant = null)
        {
            var log = new TelemetryLog(disk, () => clock, "main");
            if (variant != null) log.SetVariant(variant);
            log.StartSession("open", 0, "0.1", "test", 0);
            return log;
        }

        static Dilemma D(int id, long month, long end, DilemmaState st, int option, DilemmaPath path = DilemmaPath.Box) =>
            new Dilemma { id = id, defId = "def" + id, month = month, endMonth = end, state = st, option = option, path = path };

        List<TelemetryEvent> Events(TelemetryLog log) => log.ReadAllLines().Select(TelemetryEvent.Parse).Where(e => e != null).ToList();

        // ---- events ----

        [Test] public void DilemmaEventsSerialiseWithTheirProps()
        {
            var log = NewLog("B");
            var d = D(7, 24, 36, DilemmaState.Answered, 1, DilemmaPath.Card);
            log.DilemmaRaised(d); log.DilemmaShown(d); log.DilemmaAnswered(d, 4.256);
            var lap = D(8, 48, 72, DilemmaState.Lapsed, 0);
            log.DilemmaLapsed(lap);
            var info = new CallbackInfo { dilemma = d, option = 1, yearsSinceAnswer = 6.5f };
            log.CallbackShown(info); log.CallbackShowMe(info);
            var ev = Events(log);
            Assert.That(ev.Select(e => e.name).Where(n => n != Ev.Variant), Is.EqualTo(new[]
                { Ev.SessionStart, Ev.DilemmaRaised, Ev.DilemmaShown, Ev.DilemmaAnswered, Ev.DilemmaLapsed, Ev.CallbackShown, Ev.CallbackShowMe }));
            var byName = ev.GroupBy(e => e.name).ToDictionary(g => g.Key, g => g.First());
            var raised = byName[Ev.DilemmaRaised];
            Assert.That(raised.Num("id"), Is.EqualTo(7)); Assert.That(raised.Str("def"), Is.EqualTo("def7")); Assert.That(raised.Num("month"), Is.EqualTo(24));
            var a = byName[Ev.DilemmaAnswered];
            Assert.That(a.Num("option"), Is.EqualTo(1)); Assert.That(a.Str("path"), Is.EqualTo("card"));
            Assert.That(a.Num("secs"), Is.EqualTo(4.26).Within(0.001)); Assert.That(a.Num("years_open"), Is.EqualTo(1));
            var l = byName[Ev.DilemmaLapsed];
            Assert.That(l.Num("option"), Is.EqualTo(0)); Assert.That(l.Num("years_open"), Is.EqualTo(2));
            var cs = byName[Ev.CallbackShown];
            Assert.That(cs.Num("years_since"), Is.EqualTo(6.5)); Assert.That(cs.Num("option"), Is.EqualTo(1));
            Assert.That(byName[Ev.CallbackShowMe].Num("id"), Is.EqualTo(7));
            Assert.That(byName[Ev.DilemmaShown].Str("box"), Is.EqualTo("main"));
        }

        [Test] public void NeverShownAnswerLeavesSecsOut()
        {
            var log = NewLog();
            log.DilemmaAnswered(D(1, 0, 12, DilemmaState.Answered, 0), null);
            Assert.That(Events(log).Last().TryGet("secs", out _), Is.False);
        }

        [Test] public void VariantIsInTheSessionRecordAndAMidSessionChangeIsLogged()
        {
            var log = NewLog("A");
            Assert.That(Events(log).First(e => e.name == Ev.SessionStart).Str("variant"), Is.EqualTo("A"));
            log.SetVariant("A"); // no change, no line
            log.SetVariant("B");
            log.Flush();
            Assert.That(Events(log).Count(e => e.name == Ev.Variant), Is.EqualTo(2), "A at start, then B");
            log.StartSession("return", 1, "0.1", "test", 0);
            Assert.That(Events(log).Last(e => e.name == Ev.SessionStart).Str("variant"), Is.EqualTo("B"));
            var none = new TelemetryLog(new MemoryTelemetryStorage(), () => clock, "x");
            none.StartSession("open", 0, "0.1", "test", 0);
            Assert.That(none.ReadAllLines()[0], Does.Not.Contain("variant"));
        }

        [Test] public void HooksAreSafeWithANullLog()
        {
            TelemetryLog log = null;
            var d = D(1, 0, 12, DilemmaState.Answered, 0);
            Assert.DoesNotThrow(() =>
            {
                log.DilemmaRaised(d); log.DilemmaShown(d); log.DilemmaAnswered(d, 1); log.DilemmaLapsed(d);
                log.CallbackShown(new CallbackInfo { dilemma = d }); log.CallbackShowMe(new CallbackInfo { dilemma = d });
            });
        }

        // ---- the tracker, on a real sim ----

        [Test] public void TrackerLogsRaisedAndLapsedOnceAndTimesTheAnswer()
        {
            var sim = SimCoreTests.NewSim(5, new SimConfig { dilemmas = true });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            var log = NewLog("B");
            var tr = new DilemmaTelemetryTracker(sim);
            Assert.That(tr.Observe(sim, log), Is.EqualTo(0));

            var d1 = sim.DebugRaiseDilemma();
            Assert.IsNotNull(d1);
            Assert.That(tr.Observe(sim, log), Is.EqualTo(1));
            Assert.That(tr.Observe(sim, log), Is.EqualTo(0), "not twice");
            Assert.IsTrue(tr.Shown(log, d1, 100));
            Assert.IsFalse(tr.Shown(log, d1, 105), "one shown per dilemma");
            Assert.IsTrue(sim.AnswerDilemma(d1.id, 0, DilemmaPath.Card));
            Assert.IsTrue(tr.Answered(log, d1, 112.5));
            Assert.IsFalse(tr.Answered(log, d1, 113));

            var d2 = sim.DebugRaiseDilemma();
            Assert.IsTrue(sim.DebugLapseDilemma());
            Assert.That(tr.Observe(sim, log), Is.EqualTo(2), "d2 raised and lapsed");
            Assert.That(tr.Observe(sim, log), Is.EqualTo(0));

            var ev = Events(log);
            var ans = ev.Single(e => e.name == Ev.DilemmaAnswered);
            Assert.That(ans.Num("secs"), Is.EqualTo(12.5)); Assert.That(ans.Str("path"), Is.EqualTo("card"));
            var lap = ev.Single(e => e.name == Ev.DilemmaLapsed);
            Assert.That(lap.Num("id"), Is.EqualTo(d2.id)); Assert.That(lap.Num("option"), Is.EqualTo(d2.option));
        }

        [Test] public void TrackerIgnoresWhatWasAlreadyThereAtLoad()
        {
            var sim = SimCoreTests.NewSim(5, new SimConfig { dilemmas = true });
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            sim.DebugRaiseDilemma(); sim.DebugLapseDilemma();
            var tr = new DilemmaTelemetryTracker(sim);
            Assert.That(tr.Observe(sim, NewLog()), Is.EqualTo(0));
        }

        // ---- the analyzer ----

        void Put(List<string> lines, int secs, string session, string name, params object[] kv)
        {
            var e = new TelemetryEvent { utc = T0.AddSeconds(secs), session = session, name = name };
            for (int i = 0; i + 1 < kv.Length; i += 2) e.Set((string)kv[i], kv[i + 1]);
            lines.Add(e.ToLine());
        }

        /// <summary>Session "a1" is variant A (no dilemmas); "b1" is variant B with 6 dilemmas: 3 answered (2 card, 1 box), 1 lapsed after
        /// being shown, 1 lapsed unseen, one still open. Seconds to answer 3, 5, 10.</summary>
        List<string> HandBuilt()
        {
            var l = new List<string>();
            Put(l, 0, "a1", Ev.SessionStart, "reason", "open", "tz_min", 0, "variant", "A");
            Put(l, 600, "b1", Ev.SessionStart, "reason", "open", "tz_min", 0, "variant", "B");
            for (int id = 1; id <= 6; id++) Put(l, 610 + id, "b1", Ev.DilemmaRaised, "box", "main", "id", id, "def", "d" + id, "month", id * 24);
            for (int id = 1; id <= 5; id++) Put(l, 700 + id, "b1", Ev.DilemmaShown, "box", "main", "id", id, "def", "d" + id);
            Put(l, 710, "b1", Ev.DilemmaShown, "box", "main", "id", 1, "def", "d1"); // shown again: still one
            Put(l, 720, "b1", Ev.DilemmaAnswered, "box", "main", "id", 1, "option", 0, "path", "card", "secs", 3.0, "years_open", 0.5);
            Put(l, 730, "b1", Ev.DilemmaAnswered, "box", "main", "id", 2, "option", 1, "path", "card", "secs", 10.0, "years_open", 1.0);
            Put(l, 740, "b1", Ev.DilemmaAnswered, "box", "main", "id", 3, "option", 1, "path", "box", "secs", 5.0, "years_open", 0.25);
            Put(l, 750, "b1", Ev.DilemmaLapsed, "box", "main", "id", 4, "option", 0, "years_open", 2.0);
            Put(l, 751, "b1", Ev.DilemmaLapsed, "box", "main", "id", 6, "option", 0, "years_open", 2.0); // never shown
            Put(l, 800, "b1", Ev.CallbackShown, "box", "main", "id", 1, "option", 0, "years_since", 5.0);
            Put(l, 801, "b1", Ev.CallbackShown, "box", "main", "id", 2, "option", 1, "years_since", 7.0);
            Put(l, 802, "b1", Ev.CallbackShowMe, "box", "main", "id", 2);
            return l;
        }

        [Test] public void AnalyzerSummarisesTheP7MeasuresPerVariant()
        {
            var r = TelemetryAnalyzer.Analyze(HandBuilt(), T0.AddHours(1));
            var b = r.dilemmasByVariant["B"];
            Assert.That(b.sessions, Is.EqualTo(1));
            Assert.That(b.raised, Is.EqualTo(6)); Assert.That(b.shown, Is.EqualTo(5));
            Assert.That(b.answered, Is.EqualTo(3)); Assert.That(b.lapsed, Is.EqualTo(1)); Assert.That(b.lapsedUnseen, Is.EqualTo(1));
            Assert.That(b.AnsweredShare.Value, Is.EqualTo(0.75).Within(1e-9));
            Assert.That(b.answeredCard, Is.EqualTo(2)); Assert.That(b.answeredBox, Is.EqualTo(1));
            Assert.That(b.MedianSeconds, Is.EqualTo(5)); Assert.That(b.MinSeconds, Is.EqualTo(3)); Assert.That(b.MaxSeconds, Is.EqualTo(10));
            Assert.That(b.callbacksShown, Is.EqualTo(2)); Assert.That(b.callbackShowMe, Is.EqualTo(1));
            Assert.That(b.ShowMeShare.Value, Is.EqualTo(0.5));
            var a = r.dilemmasByVariant["A"];
            Assert.That(a.sessions, Is.EqualTo(1)); Assert.That(a.raised + a.shown + a.answered, Is.EqualTo(0)); Assert.That(a.AnsweredShare, Is.Null);
            Assert.That(r.dilemmas.answered, Is.EqualTo(3)); Assert.That(r.dilemmas.sessions, Is.EqualTo(2));
            Assert.That(r.counts[Ev.DilemmaShown], Is.EqualTo(6), "raw event counts include the repeat");
            Assert.That(r.ToText(), Does.Contain("Dilemmas [B]").And.Contain("answered share 75%").And.Contain("Dilemmas [A]"));
            Assert.That(r.ToJson(), Does.Contain("\"dilemma_b_answered\":3").And.Contain("\"dilemma_answered_share\":0.75"));
        }

        [Test] public void MedianOfAnEvenCountAveragesTheMiddle()
        {
            Assert.That(DilemmaStats.Median(new List<double> { 4, 1, 3, 10 }), Is.EqualTo(3.5));
            Assert.That(DilemmaStats.Median(new List<double>()), Is.Null);
        }

        [Test] public void VariantChangeMidSessionSplitsTheStats()
        {
            var l = new List<string>();
            Put(l, 0, "s", Ev.SessionStart, "reason", "open", "tz_min", 0, "variant", "A");
            Put(l, 5, "s", Ev.Variant, "v", "B");
            Put(l, 10, "s", Ev.DilemmaRaised, "box", "main", "id", 1, "def", "x", "month", 24);
            Put(l, 11, "s", Ev.DilemmaShown, "box", "main", "id", 1, "def", "x");
            Put(l, 20, "s", Ev.DilemmaAnswered, "box", "main", "id", 1, "option", 0, "path", "box", "secs", 9.0, "years_open", 0.1);
            var r = TelemetryAnalyzer.Analyze(l, T0.AddHours(1));
            Assert.That(r.dilemmasByVariant["B"].answered, Is.EqualTo(1));
            Assert.That(r.dilemmasByVariant["A"].answered, Is.EqualTo(0));
        }

        [Test] public void LogWithoutDilemmasHasNoDilemmaLines()
        {
            var l = new List<string>();
            Put(l, 0, "s", Ev.SessionStart, "reason", "open", "tz_min", 0);
            var r = TelemetryAnalyzer.Analyze(l, T0.AddHours(1));
            Assert.That(r.ToText(), Does.Not.Contain("Dilemmas"));
            Assert.That(r.ToJson(), Does.Not.Contain("dilemma_"));
        }

        [Test] public void EndToEndThroughTheLogAndDisk()
        {
            var log = NewLog("B");
            var d = D(3, 24, 30, DilemmaState.Answered, 0, DilemmaPath.Box);
            log.DilemmaRaised(d); log.DilemmaShown(d); clock = clock.AddSeconds(8); log.DilemmaAnswered(d, 8);
            var r = TelemetryAnalyzer.Analyze(log.ReadAllLines(), clock);
            Assert.That(r.dilemmasByVariant["B"].answeredBox, Is.EqualTo(1));
            Assert.That(r.dilemmasByVariant["B"].MedianSeconds, Is.EqualTo(8));
        }

        // ---- survey and recall ----

        [Test] public void SurveyAndRecallTextIsTheP7Wording()
        {
            Assert.That(DilemmaSurvey.Q4, Is.EqualTo("What happened in the box felt like my doing"));
            Assert.That(DilemmaSurvey.Q6, Is.EqualTo("Did any question feel like it had a wrong answer, or like homework?"));
            Assert.That(DilemmaSurvey.RecallQuestion, Is.EqualTo("Name one choice you made for the people, and what came of it."));
            Assert.That(DilemmaSurvey.VariantOf(false), Is.EqualTo("A")); Assert.That(DilemmaSurvey.VariantOf(true), Is.EqualTo("B"));
        }

        [Test] public void RecallCanBeAskedOnDayTwoForP7_AndDayThreeStaysTheDefault()
        {
            var s = new RecallState();
            RecallContext Ctx(int day) => new RecallContext { dayIndex = day, sessionSeconds = 60, busy = false, cardsEverShown = 3 };
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(2)), Is.False, "default is still day 3");
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(2), RecallPrompt.DilemmaFirstDay, RecallPrompt.DilemmaLastDay), Is.True);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(1), RecallPrompt.DilemmaFirstDay, RecallPrompt.DilemmaLastDay), Is.False);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(5), RecallPrompt.DilemmaFirstDay, RecallPrompt.DilemmaLastDay), Is.False);
            Assert.That(RecallPrompt.ShouldAsk(s, Ctx(3)), Is.True);
        }
    }
}
