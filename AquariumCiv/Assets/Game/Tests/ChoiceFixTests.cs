using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// The choice fix (visit loop C1, #368; docs/design/visit-loop.md 2.3-2.4): a choice is raised only while the player watches, or held
    /// and raised on the next open; it never resolves unseen; it closes on an answer, "Let them decide" (credited as the Keeper's), being
    /// shown on three visits, or its people being gone; it is paced by real hours (a gap of about 3 h), not by opens; a return shows 1 / 2 / 3
    /// cards by time away. Pure C# (JsonUtility only), so it also runs in Tools/SimTests.
    /// </summary>
    public class ChoiceFixTests
    {
        static SimConfig Cfg() => new SimConfig { dilemmas = true };

        static DilemmaDef Way(string id) => new DilemmaDef
        {
            id = id, kind = DilemmaKind.WhichWay, cast = DilemmaCast.None, question = "The fields, or the woods?",
            options = new[]
            {
                new DilemmaOptionDef { label = "the fields", hint = "h", lean = SimCore.Lean.Food },
                new DilemmaOptionDef { label = "the woods", hint = "h", lean = SimCore.Lean.Craft },
            },
        };

        /// <summary>A box with 20 distinct no-role choices (a choice never repeats in a box), the Keeper's switch on, a year lived.</summary>
        static SimCore Box(int seed = 5)
        {
            var sim = NewSim(seed, Cfg());
            sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => Way("w" + i)).ToArray();
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            return sim;
        }

        /// <summary>The player watches for <paramref name="hours"/> real hours (live steps).</summary>
        static void Watch(SimCore sim, double hours) { for (double h = 0; h < hours - 1e-9; h += 1.0 / 12) sim.Advance(1.0 / 12); }

        static void DueNow(SimCore sim) { sim.State.dilemmaNextHours = sim.State.clockHours; sim.State.dilemmaLastRaiseHours = -1e9; NoWishes(sim); }

        /// <summary>A wish holds the shared slot (an open one is waited out): the tests that time a choice start with none.</summary>
        static void NoWishes(SimCore sim)
        {
            foreach (var w in sim.State.wishes) { if (w.state == WishState.Open) w.state = WishState.Lapsed; w.endMonth = sim.State.month - 100; }
        }

        // ---------- raised only while watching, held for the open ----------

        [Test]
        public void NothingIsRaisedDuringAnAwayCatchUpButTheOpenRaisesIt()
        {
            var sim = Box();
            DueNow(sim);
            sim.CatchUp(2);
            Assert.AreEqual(0, sim.State.dilemmas.Count, "away: held, not raised");
            Assert.IsNull(sim.OpenDilemma);
            Assert.IsTrue(sim.BeginVisit(), "the open raises the held choice");
            var d = sim.OpenDilemma;
            Assert.IsNotNull(d);
            Assert.AreEqual(1, d.visits, "raised on this open: shown once");
            Assert.AreEqual(1, sim.State.dilemmas.Count);
        }

        [Test]
        public void ADueChoiceIsRaisedWhileWatching()
        {
            var sim = Box();
            DueNow(sim);
            sim.Advance(1.0 / 12);
            Assert.IsNotNull(sim.OpenDilemma, "watched: raised in the live step");
            Assert.AreEqual(1, sim.OpenDilemma.visits);
        }

        [Test]
        public void ANightAwayLeavesTheChoiceOpenAndNothingLapses()
        {
            var sim = Box();
            sim.DebugRaiseDilemma();
            var d = sim.OpenDilemma;
            sim.CatchUp(12);
            sim.CatchUp(200);
            Assert.AreEqual(DilemmaState.Open, d.state, "never resolves unseen");
            Assert.AreEqual(1, sim.State.dilemmas.Count, "and no second one is raised away");
            Assert.AreEqual(1, d.visits, "being away is not being shown");
        }

        [Test]
        public void AwayTwelveHoursFindsExactlyOneNeverAStack()
        {
            var sim = Box();
            sim.CatchUp(12);
            Assert.AreEqual(0, sim.State.dilemmas.Count);
            sim.BeginVisit();
            Assert.AreEqual(1, sim.State.dilemmas.Count);
            Assert.IsNotNull(sim.OpenDilemma);
            // away again: the same waiting choice, not a second
            sim.CatchUp(12);
            sim.BeginVisit();
            Assert.AreEqual(1, sim.State.dilemmas.Count);
            Assert.AreEqual(2, sim.OpenDilemma.visits);
            // answered and away another night: one more, only one
            sim.LetThemDecide(sim.OpenDilemma.id);
            sim.CatchUp(12);
            sim.BeginVisit();
            Assert.AreEqual(2, sim.State.dilemmas.Count);
            Assert.AreEqual(1, sim.State.dilemmas.Count(x => x.state == DilemmaState.Open));
        }

        // ---------- pacing in hours ----------

        [Test]
        public void ANewChoiceNeedsThreeRealHoursSinceTheLastWasRaised()
        {
            var sim = Box();
            sim.DebugRaiseDilemma();
            double raisedAt = sim.State.clockHours;
            sim.LetThemDecide(sim.OpenDilemma.id);
            sim.State.dilemmaNextHours = sim.State.clockHours; // due, but the gap is not over
            Watch(sim, 2.5);
            Assert.AreEqual(1, sim.State.dilemmas.Count, "2.5 h on: still the gap");
            Watch(sim, 0.75);
            Assert.AreEqual(2, sim.State.dilemmas.Count, "past 3 h: raised");
            Assert.GreaterOrEqual(sim.State.dilemmas[1].raisedHours - raisedAt, SimCore.DilemmaGapHours - 1e-6);
        }

        [Test]
        public void OpeningEveryFiveMinutesForAnHourSeesTheSameChoiceOrNone()
        {
            // a waiting choice: the same one, never a second, never burned by the quick opens
            var sim = Box();
            sim.DebugRaiseDilemma();
            var d = sim.OpenDilemma;
            for (int i = 0; i < 12; i++) { sim.CatchUp(5 / 60.0); sim.BeginVisit(); }
            Assert.AreSame(d, sim.OpenDilemma);
            Assert.AreEqual(1, sim.State.dilemmas.Count);
            Assert.AreEqual(1, d.visits, "five-minute opens are one visit");

            // an answered one, a new one due: still none inside the gap
            var sim2 = Box();
            sim2.DebugRaiseDilemma();
            sim2.LetThemDecide(sim2.OpenDilemma.id);
            DueNow(sim2);
            sim2.State.dilemmaLastRaiseHours = sim2.State.clockHours; // just raised
            for (int i = 0; i < 12; i++) { sim2.CatchUp(5 / 60.0); sim2.BeginVisit(); }
            Assert.AreEqual(1, sim2.State.dilemmas.Count, "an hour of quick opens: no new choice");
            Assert.IsNull(sim2.OpenDilemma);
        }

        [Test]
        public void BigOnesComeEverySixToTenHours()
        {
            var sim = Box();
            for (int i = 0; i < 6; i++)
            {
                sim.CatchUp(24);
                sim.BeginVisit();
                if (sim.OpenDilemma != null) sim.LetThemDecide(sim.OpenDilemma.id);
            }
            Assert.AreEqual(6, sim.State.dilemmas.Count, "one per long absence, never a stack");
            // watching: the spacing between raises is the 6-10 h window, not less
            var live = Box(7);
            live.State.dilemmaNextHours = -1;
            for (int h = 0; h < 80; h++)
            {
                Watch(live, 0.5);
                if (live.OpenDilemma != null) live.LetThemDecide(live.OpenDilemma.id);
            }
            var ds = live.State.dilemmas;
            Assert.GreaterOrEqual(ds.Count, 3);
            for (int i = 1; i < ds.Count; i++)
            {
                double gap = ds[i].raisedHours - ds[i - 1].raisedHours;
                Assert.GreaterOrEqual(gap, 6 - 1e-6, $"gap {i}");
                Assert.LessOrEqual(gap, 10 + 2 + 1e-6, $"gap {i} (a wish open at the time delays it)");
            }
        }

        [Test]
        public void TheSizeHookAskedBySchedulerIsBigToday()
        {
            var sim = Box();
            Assert.AreEqual(DilemmaSize.Big, sim.NextDilemmaSize());
            sim.DebugRaiseDilemma();
            Assert.AreEqual(DilemmaSize.Big, sim.OpenDilemma.size);
        }

        // ---------- visits ----------

        [Test]
        public void ShownOnThreeVisitsThenItClosesUnansweredWithTheDefault()
        {
            var sim = Box();
            sim.DebugRaiseDilemma();
            var d = sim.OpenDilemma;
            int expect = sim.DilemmaDefaultOption(d);
            Assert.AreEqual(1, d.visits);
            sim.CatchUp(1); sim.BeginVisit(); Assert.AreEqual(2, d.visits);
            sim.CatchUp(1); sim.BeginVisit(); Assert.AreEqual(3, d.visits);
            Assert.AreEqual(DilemmaState.Open, d.state, "shown on three visits: still waiting");
            sim.CatchUp(1); sim.BeginVisit();
            Assert.AreEqual(DilemmaState.Lapsed, d.state, "the fourth open: it closes");
            Assert.AreEqual(DilemmaEnd.Visits, d.end);
            Assert.AreEqual(expect, d.option, "the people's default");
            Assert.AreEqual(-1, d.answerEntryId, "not the Keeper's: no Keeper line");
            Assert.IsFalse(sim.State.chronicle.Any(e => e.keeper && e.month >= d.month && e.id == d.answerEntryId));
        }

        [Test]
        public void AVisitIsAnOpenAfterFifteenMinutesAway()
        {
            var sim = Box();
            sim.BeginVisit();
            int v0 = sim.State.visitCount;
            sim.CatchUp(0.1); sim.BeginVisit();
            Assert.AreEqual(v0, sim.State.visitCount, "6 minutes away: the same visit");
            sim.CatchUp(0.3); sim.BeginVisit();
            Assert.AreEqual(v0 + 1, sim.State.visitCount, "18 minutes away: a new visit");
            Watch(sim, 2); sim.BeginVisit();
            Assert.AreEqual(v0 + 1, sim.State.visitCount, "watching the whole time is one visit");
        }

        [Test]
        public void ARaiseWhileWatchingCountsAsItsFirstVisitNotTwo()
        {
            var sim = Box();
            DueNow(sim);
            sim.Advance(1.0 / 12);
            var d = sim.OpenDilemma;
            Assert.AreEqual(1, d.visits);
            sim.BeginVisit(); // the app is already in front: not a new visit
            Assert.AreEqual(1, d.visits);
            sim.CatchUp(1); sim.BeginVisit();
            Assert.AreEqual(2, d.visits);
        }

        // ---------- Let them decide, and gone ----------

        [Test]
        public void LetThemDecideIsCreditedAsTheKeepersChoiceWithThePeoplesDefault()
        {
            var sim = Box();
            var d = sim.DebugRaiseDilemma();
            int expect = sim.DilemmaDefaultOption(d);
            Assert.IsTrue(sim.LetThemDecide(d.id, DilemmaPath.Box));
            Assert.AreEqual(DilemmaState.Answered, d.state, "answered, not lapsed");
            Assert.AreEqual(DilemmaEnd.LetThemDecide, d.end);
            Assert.AreEqual(expect, d.option);
            Assert.AreEqual(DilemmaPath.Box, d.path);
            var line = sim.State.chronicle.Single(e => e.id == d.answerEntryId);
            Assert.IsTrue(line.keeper, "the Keeper's line");
            StringAssert.Contains("let the people decide", line.text);
            Assert.AreEqual(1, sim.State.dilemmaLeans.Count, "the default's effects apply");
            Assert.IsFalse(sim.LetThemDecide(d.id), "once");
            Assert.IsFalse(sim.AnswerDilemma(d.id, 0, DilemmaPath.Card), "and final");
        }

        [Test]
        public void LetThemDecideIsAnOptionOnEveryDef()
        {
            foreach (var def in DilemmaDefs.All)
            {
                var sim = NewSim(11, Cfg());
                sim.DilemmaCatalog = new[] { def };
                for (int m = 0; m < 12 * 12; m++) sim.Advance(1.0 / 12);
                var d = sim.DebugRaiseDilemma(def.id);
                if (d == null) continue; // not in reach in this seed's box
                Assert.IsTrue(sim.LetThemDecide(d.id), def.id);
                Assert.AreEqual(DilemmaEnd.LetThemDecide, d.end, def.id);
            }
        }

        [Test]
        public void ItClosesWhenItsPeopleAreGone()
        {
            int tried = 0;
            for (int seed = 1; seed < 60 && tried < 3; seed++)
            {
                var sim = NewSim(seed, Cfg());
                var def = new DilemmaDef
                {
                    id = "who", kind = DilemmaKind.Who, cast = DilemmaCast.TwoCandidates, role = DilemmaDef.Leader, replacesHolder = true,
                    question = "{a} or {b}?",
                    options = new[] { new DilemmaOptionDef { label = "{a}", hint = "h", takesRole = true }, new DilemmaOptionDef { label = "{b}", hint = "h", takesRole = true } },
                };
                sim.DilemmaCatalog = new[] { def };
                for (int m = 0; m < 36; m++) sim.Advance(1.0 / 12);
                if (sim.State.era != Era.Camp || sim.RoleCandidates(RoleType.Chief).Count < 2) continue;
                var d = sim.DebugRaiseDilemma("who");
                if (d == null) continue;
                tried++;
                sim.FindNotable(d.personB).alive = false;
                sim.Advance(1.0 / 12);
                Assert.AreEqual(DilemmaState.Lapsed, d.state);
                Assert.AreEqual(DilemmaEnd.Gone, d.end);
                Assert.AreEqual(0, d.option, "the living one");
            }
            Assert.Greater(tried, 0);
        }

        // ---------- the save ----------

        [Test]
        public void TheNewFieldsRoundTripAndAnOldDilemmaLoadsAsShownOnce()
        {
            var sim = Box();
            sim.CatchUp(1); sim.BeginVisit();
            var d = sim.DebugRaiseDilemma();
            sim.CatchUp(1); sim.BeginVisit();
            var back = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            Assert.AreEqual(sim.State.visitCount, back.visitCount);
            Assert.AreEqual(sim.State.clockHours, back.clockHours, 1e-9);
            Assert.AreEqual(sim.State.dilemmaLastRaiseHours, back.dilemmaLastRaiseHours, 1e-9);
            Assert.AreEqual(sim.State.dilemmaNextHours, back.dilemmaNextHours, 1e-9);
            Assert.AreEqual(2, back.dilemmas[0].visits);
            Assert.AreEqual(JsonUtility.ToJson(sim.State), JsonUtility.ToJson(back));

            // a dilemma saved before the fix has no visits field: it counts as shown once
            var old = JsonUtility.FromJson<Dilemma>("{\"id\":3,\"defId\":\"x\",\"month\":4}");
            Assert.AreEqual(1, old.visits);
            Assert.AreEqual(DilemmaEnd.None, old.end);
        }

        // ---------- cards 1 / 2 / 3 ----------

        [Test]
        public void TheCardCapIsOneTwoThreeByTimeAway()
        {
            Assert.AreEqual(1, SimCore.CardCap(0.2));
            Assert.AreEqual(1, SimCore.CardCap(0.99));
            Assert.AreEqual(2, SimCore.CardCap(1));
            Assert.AreEqual(2, SimCore.CardCap(8));
            Assert.AreEqual(3, SimCore.CardCap(8.1));
            Assert.AreEqual(3, SimCore.CardCap(72));
        }

        [Test]
        public void TheRestLandInHistoryAndAlso()
        {
            var sim = NewSim(8, new SimConfig());
            for (int m = 0; m < 12 * 12; m++) sim.Advance(1.0 / 12);
            var all = sim.ReturnCardsCapped(-1, 100);
            Assert.GreaterOrEqual(all.cards.Count, 4, "enough stories to cap");
            foreach (double hours in new[] { 0.5, 4.0, 20.0 })
            {
                var stack = sim.ReturnCards(-1, hours);
                int cap = SimCore.CardCap(hours);
                Assert.LessOrEqual(stack.cards.Count, cap, $"{hours} h away");
                Assert.AreEqual(all.cards.Count, stack.cards.Count + stack.also.Count, "nothing is dropped: the rest wait in Also");
                foreach (var c in stack.also)
                    Assert.IsTrue(sim.State.chronicle.Any(e => e.id == c.lead.id), "and every one is in the chronicle, the History");
            }
        }

        // ---------- t1-moments: a choice held for the open is staged in the box first (visit-loop 2.4) ----------

        [Test]
        public void AHeldChoiceIsFlaggedToStageOnTheOpenAndOnlyOnce()
        {
            var sim = Box();
            DueNow(sim);
            sim.CatchUp(2);
            Assert.IsFalse(sim.ChoiceStageDue, "away: nothing to stage yet");
            Assert.IsTrue(sim.BeginVisit());
            Assert.IsTrue(sim.ChoiceStageDue, "the open found a choice waiting: the box stages it first");
            sim.ChoiceStaged();
            Assert.IsFalse(sim.ChoiceStageDue, "staged once for this open");
            sim.CatchUp(1);
            sim.BeginVisit();
            Assert.IsTrue(sim.ChoiceStageDue, "the next open: the same waiting choice is staged again");
            sim.LetThemDecide(sim.OpenDilemma.id);
            Assert.IsFalse(sim.ChoiceStageDue, "answered: nothing to stage");
        }

        [Test]
        public void NoChoiceWaitingMeansNothingToStage()
        {
            var sim = Box();
            sim.State.dilemmaLastRaiseHours = sim.State.clockHours; // just raised: the gap has not passed
            sim.CatchUp(0.01);
            Assert.IsFalse(sim.BeginVisit());
            Assert.IsFalse(sim.ChoiceStageDue);
        }

        [Test]
        public void TheWelcomeMomentComesBeforeTheStagedChoice()
        {
            var cfg = new SimConfig { dilemmas = true, moments = true };
            var sim = NewSim(5, cfg);
            sim.DilemmaCatalog = Enumerable.Range(0, 20).Select(i => Way("w" + i)).ToArray();
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            sim.State.moments.Clear();
            var mom = new HeldMoment { id = sim.MomentPacing.nextId++, kind = MomentKind.Festival, month = sim.State.month, priority = 0.6f };
            sim.State.moments.Add(mom);
            DueNow(sim);
            sim.CatchUp(2);
            Assert.IsTrue(sim.BeginVisit());
            Assert.IsTrue(sim.ChoiceStageDue);
            Assert.IsTrue(sim.MomentBeforeChoice(), "a moment waits to be the welcome: the choice waits for it");
            var due = sim.StartDueMoments();
            Assert.AreEqual(1, due.Count);
            Assert.IsTrue(sim.MomentBeforeChoice(), "it is playing: the choice still waits");
            sim.FinishMoment(due[0].id, true);
            Assert.IsFalse(sim.MomentBeforeChoice(), "the welcome is over");
            Assert.IsTrue(sim.ChoiceStageDue, "now the held choice is staged");
        }
    }
}
