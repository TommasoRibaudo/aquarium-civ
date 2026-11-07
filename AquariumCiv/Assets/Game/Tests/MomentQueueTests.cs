using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// The moment queue and its director (visit loop C2, #369; docs/design/visit-loop.md 2.1-2.2, research 16 §4.1-4.2): a ceremony
    /// waits in a queue while its fact happens on time; the director plays them only while the player watches, on a budget per
    /// watched minute by the box's size, with a rest after each, never two of a kind in a row, the top one on the open, and a card
    /// after 8 game years. Pure C# (JsonUtility only), so it also runs in Tools/SimTests.
    /// </summary>
    public class MomentQueueTests
    {
        static SimConfig Cfg(bool on = true) => new SimConfig { moments = on };

        /// <summary>A box watched for a year, its queue and pacing emptied, a full budget.</summary>
        static SimCore Box(int seed = 7)
        {
            var sim = NewSim(seed, Cfg());
            for (int m = 0; m < 12; m++) sim.Advance(1.0 / 12);
            Clear(sim);
            return sim;
        }

        static void Clear(SimCore sim)
        {
            sim.State.moments.Clear();
            var p = sim.MomentPacing;
            p.credit = 1; p.restUntil = 0; p.visitPlayed = 0; p.lastKind = -1; p.welcomeDue = false; p.busyNext = false;
        }

        /// <summary>A moment with no people to outlive (a festival or a callback), straight into the queue.</summary>
        static HeldMoment Add(SimCore sim, MomentKind kind, float priority = 0.6f, long monthsAgo = 0)
        {
            var p = sim.MomentPacing;
            var m = new HeldMoment { id = p.nextId++, kind = kind, month = sim.State.month - monthsAgo, priority = priority };
            sim.State.moments.Add(m);
            return m;
        }

        /// <summary>Months watched with only <paramref name="m"/> in the queue (the box's own new ceremonies are taken out, and a
        /// town's cap keeps the queue from trimming it), so its age alone decides.</summary>
        static void HoldOnly(SimCore sim, HeldMoment m, int months)
        {
            for (int i = 0; i < months; i++)
            {
                sim.State.population = 30;
                sim.Advance(1.0 / 12);
                sim.State.moments.RemoveAll(x => x != m);
            }
        }

        static void WatchMinutes(SimCore sim, double minutes) { for (int i = 0; i < (int)System.Math.Round(minutes); i++) sim.Advance(1 / 60.0); }

        // ---------- the queue: the fact happens, the ceremony waits ----------

        [Test]
        public void WeddingsBirthsAndFestivalsQueueTheirCeremonyAndTheLineStaysInTheHistory()
        {
            var sim = NewSim(11, Cfg());
            for (int y = 0; y < 30; y++) sim.Advance(1.0);
            var kinds = sim.State.moments.Select(m => m.kind).Distinct().ToList();
            Assert.That(sim.State.moments.Count, Is.GreaterThan(0), "a box that lives 30 years has ceremonies to hold");
            foreach (var m in sim.State.moments)
            {
                var e = sim.State.chronicle.FirstOrDefault(x => x.id == m.entryId);
                if (sim.State.month - m.month < 12 * 25) Assert.IsNotNull(e, "the fact's line is in the chronicle");
            }
            CollectionAssert.Contains(kinds, MomentKind.Newborn);
        }

        [Test]
        public void OffNothingIsQueuedAndTheCardsAreAsBefore()
        {
            var on = NewSim(11, Cfg(true));
            var off = NewSim(11, Cfg(false));
            for (int y = 0; y < 20; y++) { on.Advance(1.0); off.Advance(1.0); }
            Assert.AreEqual(0, off.State.moments.Count);
            Assert.AreEqual(Fingerprint(off.State), Fingerprint(on.State), "the queue draws no dice: the world is the same");
        }

        [Test]
        public void AHeldCeremonyIsNotACardUntilItBecomesOne()
        {
            var sim = Box();
            sim.CardsSeenToNow();
            sim.Advance(1.0 / 12); // a month after the last one read
            var e = sim.Chronicle("festival", "The people gathered round the fire for a feast.", 0.65f);
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m, "the festival's ceremony waits");
            Assert.AreEqual(MomentKind.Festival, m.kind);
            bool InStack() => sim.UnreadCardsCapped(64).cards.Concat(sim.UnreadCardsCapped(64).also).Any(c => c.entries.Contains(e));
            Assert.IsFalse(InStack(), "waiting: it will be seen, not read");
            sim.CompactCardLedger();
            sim.Advance(1.0 / 12);
            sim.CompactCardLedger();
            Assert.That(sim.State.cards.since, Is.LessThan(e.month), "the ledger never moves past a held ceremony");
            m.state = HeldMomentState.Carded;
            Assert.IsTrue(InStack(), "carded: told as a card");
        }

        [Test]
        public void ACallbackWaitsAsAMomentAndIsNotACardWhileItWaits()
        {
            var sim = Box();
            sim.CardsSeenToNow();
            sim.Advance(1.0 / 12);
            var e = sim.Chronicle(SimCore.CallbackKind, "Because the Keeper chose the woods, the woodpile has a roof.", 0.8f);
            var m = sim.MomentOfEntry(e.id);
            Assert.IsNotNull(m);
            Assert.AreEqual(MomentKind.Callback, m.kind);
            Assert.IsTrue(sim.MomentHidesCard(e), "#371: the moment says whose choice came back, so its card waits until it becomes one");
        }

        // ---------- nothing plays while away; the welcome on the open ----------

        [Test]
        public void NoMomentPlaysWhileAwayAndNoBudgetGrows()
        {
            var sim = Box();
            Add(sim, MomentKind.Festival);
            sim.MomentPacing.credit = 0;
            double watched = sim.MomentPacing.watchedMinutes;
            int startedAway = 0;
            sim.Chronicled += _ => { if (sim.CatchingUp) startedAway += sim.StartDueMoments().Count; };
            sim.CatchUp(6);
            Assert.That(sim.State.chronicle.Count, Is.GreaterThan(0));
            Assert.AreEqual(0, startedAway, "a view asking during the catch-up gets nothing");
            Assert.AreEqual(watched, sim.MomentPacing.watchedMinutes, 1e-9, "the director's clock is the watched minutes only");
            Assert.AreEqual(0, sim.MomentPacing.credit, 1e-9);
            Assert.IsFalse(sim.State.moments.Any(m => m.state == HeldMomentState.Playing || m.state == HeldMomentState.Played));
        }

        [Test]
        public void TheTopWaitingMomentPlaysOnTheOpen()
        {
            var sim = Box();
            Add(sim, MomentKind.Festival, 0.6f);
            var top = Add(sim, MomentKind.Callback, 5f); // above anything the away years queue
            var p = sim.MomentPacing;
            p.credit = 0; p.restUntil = p.watchedMinutes + 100; p.visitPlayed = 99; // spent, resting, capped: the welcome ignores all three
            sim.CatchUp(2);
            Assert.AreEqual("", sim.MomentWaitReason() ?? "", "the open has its welcome");
            var got = sim.StartDueMoments();
            Assert.AreEqual(1, got.Count);
            Assert.AreSame(top, got[0], "the top one");
            Assert.IsTrue(got[0].welcome);
            Assert.AreEqual(HeldMomentState.Playing, top.state);
            Assert.AreEqual(0, sim.StartDueMoments().Count, "one at a time");
            Assert.IsTrue(sim.FinishMoment(top.id));
            Assert.AreEqual(HeldMomentState.Played, top.state);
            Assert.AreEqual(0, sim.StartDueMoments().Count, "after the welcome the budget and the rest hold the next");
        }

        [Test]
        public void AShortAbsenceIsNotANewVisitSoNoWelcome()
        {
            var sim = Box();
            Add(sim, MomentKind.Festival);
            sim.MomentPacing.credit = 0;
            sim.Advance(1 / 60.0);
            sim.CatchUp(5 / 60.0);
            Assert.AreEqual("budget", sim.MomentWaitReason(), "five minutes away: the same visit");
            sim.CatchUp(1);
            Assert.IsNull(sim.MomentWaitReason(), "an hour away: a new visit, the welcome is due");
        }

        [Test]
        public void AMomentCutOffByLeavingPlaysAgainOnTheNextVisit()
        {
            var sim = Box();
            var m = Add(sim, MomentKind.Festival, 5f);
            Assert.AreEqual(1, sim.StartDueMoments().Count);
            sim.CatchUp(3);
            Assert.IsNull(sim.MomentWaitReason());
            Assert.AreEqual(HeldMomentState.Waiting, m.state, "left before it ended: waiting again");
            Assert.AreSame(m, sim.StartDueMoments().Single());
        }

        // ---------- the director: rates, rest, kinds ----------

        /// <summary>A view that plays each moment for one watched minute, a box kept at <paramref name="pop"/> people, a queue kept
        /// stocked with several kinds. Returns the batches in release order.</summary>
        static List<List<HeldMoment>> PlayFor(SimCore sim, int pop, int minutes)
        {
            var batches = new List<List<HeldMoment>>();
            int k = 0;
            for (int t = 0; t < minutes; t++)
            {
                sim.State.population = pop;
                foreach (var m in sim.PlayingMoments()) if (sim.MomentPacing.watchedMinutes - m.startedAt >= 1 - 1e-9) sim.FinishMoment(m.id);
                while (sim.WaitingMoments().Count < 3) // the kind fewer of which wait, so the queue is never all one kind
                {
                    var w = sim.WaitingMoments();
                    var kind = w.Count(m => m.kind == MomentKind.Festival) <= w.Count(m => m.kind == MomentKind.Callback) ? MomentKind.Festival : MomentKind.Callback;
                    Add(sim, kind, 0.5f + (k++ % 3) * 0.1f);
                }
                var got = sim.StartDueMoments();
                if (got.Count > 0) batches.Add(got);
                sim.Advance(1 / 60.0);
            }
            return batches;
        }

        [Test]
        public void ATribeGetsAtMostTwoAVisitSpacedByAbout15WatchedMinutes()
        {
            var sim = Box();
            var b = PlayFor(sim, 5, 120);
            Assert.AreEqual(2, b.Sum(x => x.Count), "max 2 a visit");
            Assert.That(b[1][0].startedAt - b[0][0].startedAt, Is.EqualTo(15).Within(1.01), "1 per ~15 watched minutes");
            Assert.AreEqual("this visit has had its moments", sim.MomentWaitReason());
            sim.CatchUp(1);
            var again = PlayFor(sim, 5, 30);
            Assert.That(again.Sum(x => x.Count), Is.EqualTo(2), "a new visit: the welcome, then one more after ~15 minutes");
        }

        [Test]
        public void ACampGetsOneAboutEvery10To15MinutesAndAtMostThreeAVisit()
        {
            var sim = Box();
            var b = PlayFor(sim, 8, 120);
            Assert.AreEqual(3, b.Sum(x => x.Count));
            double gap = b[1][0].startedAt - b[0][0].startedAt;
            Assert.That(gap, Is.InRange(10.0, 15.0), "pop 8: 12.5 min, between the tribe's 15 and the camp's 10 (the crowd drifts by one)");
        }

        [Test]
        public void AVillageGetsAboutOneEvery8To10WatchedMinutes()
        {
            var sim = Box();
            var b = PlayFor(sim, 15, 120);
            int n = b.Sum(x => x.Count);
            double interval = MomentRules.IntervalMinutes(15);
            Assert.That(interval, Is.InRange(8.0, 10.0));
            Assert.That(n, Is.InRange((int)(120 / System.Math.Ceiling(interval)) - 1, (int)(120 / interval) + 2), $"about 120/{interval:0.0}");
            Assert.IsTrue(b.All(x => x.Count == 1), "no busy days below a town");
        }

        [Test]
        public void ATownGetsAboutOneEvery6To8MinutesWithBusyDaysOfTwoOrThreeKinds()
        {
            var sim = Box();
            var b = PlayFor(sim, 25, 240);
            double interval = MomentRules.IntervalMinutes(25);
            Assert.That(interval, Is.InRange(6.0, 8.0));
            // a view that looks once a watched minute releases on the first whole minute the budget is full
            Assert.That(b.Count, Is.InRange((int)(240 / System.Math.Ceiling(interval)) - 1, (int)(240 / interval) + 2), "batches by the budget");
            foreach (var x in b)
            {
                Assert.That(x.Count, Is.InRange(1, 3));
                Assert.AreEqual(x.Count, x.Select(m => m.kind).Distinct().Count(), "a busy day is different kinds at once");
            }
            Assert.That(b.Sum(x => x.Count), Is.LessThanOrEqualTo(b.Count * 3));
        }

        [Test]
        public void ABusyDayReleasesTwoOrThreeAtOnce()
        {
            var sim = Box();
            sim.State.population = 25;
            Add(sim, MomentKind.Festival); Add(sim, MomentKind.Callback); Add(sim, MomentKind.Wedding); // the wedding: no people, carded at the pick
            sim.DebugBusyDay();
            var got = sim.StartDueMoments();
            Assert.That(got.Count, Is.InRange(2, 3));
            Assert.AreEqual(got.Count, got.Select(m => m.kind).Distinct().Count());
        }

        [Test]
        public void TheRestHoldsTheNextMomentAfterOneEnds()
        {
            var sim = Box();
            sim.State.population = 15; // a village: 4 minutes' rest
            Add(sim, MomentKind.Festival); Add(sim, MomentKind.Callback);
            var first = sim.StartDueMoments().Single();
            WatchMinutes(sim, 1);
            sim.FinishMoment(first.id);
            for (int t = 0; t < 3; t++)
            {
                sim.MomentPacing.credit = 1; sim.State.population = 15;
                Assert.AreEqual("resting", sim.MomentWaitReason(), $"minute {t} of the rest");
                WatchMinutes(sim, 1);
            }
            WatchMinutes(sim, 1);
            sim.MomentPacing.credit = 1;
            Assert.IsNull(sim.MomentWaitReason(), "rested");
        }

        [Test]
        public void NeverTwoOfTheSameKindInARow()
        {
            var sim = Box();
            Add(sim, MomentKind.Festival, 0.9f); Add(sim, MomentKind.Festival, 0.8f);
            var a = sim.StartDueMoments().Single();
            sim.FinishMoment(a.id);
            var p = sim.MomentPacing;
            p.credit = 1; p.restUntil = 0;
            Assert.AreEqual("the same kind as the last one", sim.MomentWaitReason());
            var cb = Add(sim, MomentKind.Callback, 0.1f);
            Assert.AreSame(cb, sim.StartDueMoments().Single(), "a lower one of another kind goes first");

            var run = PlayFor(Box(3), 15, 200).SelectMany(x => x).ToList();
            for (int i = 1; i < run.Count; i++)
                if (run[i].batch != run[i - 1].batch) Assert.AreNotEqual(run[i - 1].kind, run[i].kind, $"#{i}");
        }

        [Test]
        public void ARealBoxWatchedTenHoursInOneVisitNeverBursts()
        {
            var sim = NewSim(21, Cfg());
            int played = 0;
            for (int t = 0; t < 600; t++) // ten hours watched in one visit, a view that plays each moment for a minute
            {
                foreach (var m in sim.PlayingMoments()) if (sim.MomentPacing.watchedMinutes - m.startedAt >= 1) sim.FinishMoment(m.id);
                played += sim.StartDueMoments().Count;
                sim.Advance(1 / 60.0);
            }
            int cap = MomentRules.VisitCap(sim.MomentSize);
            if (cap != int.MaxValue) Assert.That(played, Is.LessThanOrEqualTo(cap + 2), "the visit's cap (the size may change during the hours)");
            Assert.That(played, Is.LessThanOrEqualTo(600 / 6 + 1), "never faster than a town's rate");
        }

        // ---------- held too long: a card ----------

        [Test]
        public void AMomentHeldOverEightGameYearsBecomesACard()
        {
            var sim = Box();
            var e = sim.Chronicle("festival", "The people gathered round the fire for a feast.", 0.65f);
            var m = sim.MomentOfEntry(e.id);
            HoldOnly(sim, m, 8 * 12); // nobody asks the director: nothing plays
            Assert.AreEqual(HeldMomentState.Waiting, m.state, "8 years: still held");
            HoldOnly(sim, m, 2);
            Assert.AreEqual(HeldMomentState.Carded, m.state);
            Assert.AreEqual(MomentCardReason.HeldTooLong, m.cardReason);
            Assert.IsTrue(sim.State.chronicle.Contains(e), "the history keeps it");
            Assert.IsFalse(sim.MomentHidesCard(e), "it is a card now");
        }

        [Test]
        public void ANewbornIsHeldUpOnlyWhileUnderTwo()
        {
            var sim = Box();
            var m = Add(sim, MomentKind.Newborn);
            m.personA = sim.State.notables.Where(n => n.alive).OrderBy(n => n.age).First().id;
            HoldOnly(sim, m, 25);
            Assert.AreEqual(HeldMomentState.Carded, m.state);
            Assert.AreEqual(MomentCardReason.BabyGrown, m.cardReason);
        }

        [Test]
        public void TheQueueKeepsItsCapAndTheOldestBecomesACard()
        {
            var sim = Box();
            sim.State.population = 5; // a tribe keeps 2
            var e1 = sim.Chronicle("festival", "a", 0.6f);
            sim.State.population = 5;
            var e2 = sim.Chronicle("anniversary", "b", 0.6f);
            sim.State.population = 5;
            var e3 = sim.Chronicle("festival", "c", 0.6f);
            Assert.AreEqual(HeldMomentState.Carded, sim.MomentOfEntry(e1.id).state);
            Assert.AreEqual(MomentCardReason.QueueFull, sim.MomentOfEntry(e1.id).cardReason);
            Assert.AreEqual(HeldMomentState.Waiting, sim.MomentOfEntry(e2.id).state);
            Assert.AreEqual(HeldMomentState.Waiting, sim.MomentOfEntry(e3.id).state);
        }

        /// <summary>t2-evict (the user's call, 2026-11-06): a full queue cards its lowest-priority moment, not simply the oldest. A tribe
        /// (cap 2) has a wedding, then three births during an away: the wedding still waits for the Keeper, births become cards.</summary>
        [Test]
        public void AFullQueueCardsTheLowestPriorityMomentSoAWeddingOutlastsARunOfBirths()
        {
            var sim = Box();
            var adults = sim.State.notables.Where(n => n.alive && n.age >= 18f).OrderBy(n => n.id).ToList();
            Assert.GreaterOrEqual(adults.Count, 2);
            sim.State.population = 5; // a tribe keeps 2
            var w = sim.Chronicle("wedding", $"{adults[0].FullName} married {adults[1].FullName} by the fire.", 0.7f, adults[0].id);
            var wedding = sim.MomentOfEntry(w.id);
            Assert.IsNotNull(wedding);
            wedding.personB = adults[1].id; // the couple (Chronicle names one; the sim's own wedding line names both)
            var births = new List<HeldMoment>();
            for (int i = 0; i < 3; i++)
            {
                sim.CatchUp(0.05); // away: a few weeks pass, nothing plays
                sim.State.population = 5;
                var baby = sim.DebugBirth();
                Assert.IsNotNull(baby);
                var b = sim.MomentOfEntry(sim.State.chronicle.Last(e => e.kind == "birth" && e.notableId == baby.id).id);
                Assert.IsNotNull(b);
                births.Add(b);
            }
            sim.CatchUp(0.05);
            Assert.AreEqual(MomentRules.QueueCap(MomentBoxSize.Tribe), 2);
            CollectionAssert.Contains(new[] { HeldMomentState.Waiting, HeldMomentState.Playing, HeldMomentState.Played }, wedding.state, "the wedding outlasts the births");
            Assert.That(births.Count(b => b.state == HeldMomentState.Carded && b.cardReason == MomentCardReason.QueueFull), Is.GreaterThanOrEqualTo(1), "a birth was carded");
            Assert.IsTrue(sim.MomentHidesCard(w), "so the peek can't read the wedding");
            var oldest = births[0];
            Assert.AreEqual(HeldMomentState.Carded, oldest.state, "of equal births the oldest goes first");
        }

        [Test]
        public void AMomentTheViewCannotStageBecomesACardAndGivesTheBudgetBack()
        {
            var sim = Box();
            var a = Add(sim, MomentKind.Festival, 0.9f);
            Add(sim, MomentKind.Callback, 0.5f);
            var got = sim.StartDueMoments().Single();
            Assert.AreSame(a, got);
            Assert.IsTrue(sim.FinishMoment(a.id, false));
            Assert.AreEqual(HeldMomentState.Carded, a.state);
            Assert.AreEqual(MomentCardReason.NotStaged, a.cardReason);
            Assert.AreEqual(1, sim.MomentPacing.credit, 1e-9, "the budget is given back");
            Assert.AreEqual(1, sim.StartDueMoments().Count, "the next one may play at once: no rest after nothing");
        }

        /// <summary>t2-polish: the return peek said "Esk married Aveline" for a wedding never shown: the welcome handed the wedding over
        /// while one of the couple was on a hunt, the planner couldn't cast it ("the couple isn't here"), and the view told it as a card
        /// at once. Now it waits again (DeferMoment): its line stays out of the cards, it is offered again after a short wait, and only
        /// after <see cref="MomentRules.MaxDeferrals"/> tries is it a card.</summary>
        [Test]
        public void AMomentThatCannotBeStagedYetWaitsAgainAndIsNotACard()
        {
            var sim = Box();
            var e = sim.Chronicle("festival", "The people gathered round the fire for a feast.", 0.9f);
            var m = sim.MomentOfEntry(e.id);
            sim.MomentPacing.welcomeDue = true;
            var got = sim.StartDueMoments().Single();
            Assert.AreSame(m, got);
            Assert.IsTrue(got.welcome);
            Assert.IsTrue(sim.DeferMoment(m.id));
            Assert.AreEqual(HeldMomentState.Waiting, m.state, "it waits for its people");
            Assert.IsTrue(sim.MomentHidesCard(e), "a deferred ceremony is still not a card");
            Assert.IsTrue(sim.MomentPacing.welcomeDue, "the welcome is still due");
            Assert.AreEqual(1, sim.MomentPacing.credit, 1e-9, "the budget is given back");
            Assert.AreEqual(0, sim.StartDueMoments().Count, "not offered again at once");
            Assert.AreEqual("waiting for its people", sim.MomentWaitReason());
            Assert.That(sim.MomentDueInMinutes(), Is.EqualTo(MomentRules.RetryMinutes).Within(1e-6));
            WatchMinutes(sim, MomentRules.RetryMinutes + 1);
            Assert.AreSame(m, sim.StartDueMoments().Single(), "offered again (still the welcome)");
            Assert.IsTrue(m.welcome);
            for (int i = 1; i < MomentRules.MaxDeferrals - 1; i++)
            {
                Assert.IsTrue(sim.DeferMoment(m.id));
                Assert.AreEqual(HeldMomentState.Waiting, m.state);
                WatchMinutes(sim, MomentRules.RetryMinutes + 1);
                Assert.AreSame(m, sim.StartDueMoments().Single());
            }
            Assert.IsTrue(sim.DeferMoment(m.id));
            Assert.AreEqual(HeldMomentState.Carded, m.state, "a moment no view can stage is a card after a few tries, not held for 8 years");
            Assert.AreEqual(MomentCardReason.NotStaged, m.cardReason);
            Assert.IsFalse(sim.MomentHidesCard(e));
            Assert.IsFalse(sim.DeferMoment(m.id), "only a playing moment");
        }

        /// <summary>t4-fixes (tester run 3): a festival handed over at night sat "Playing" with nothing on screen. Now it goes back to
        /// waiting at once, without counting a try (a night never makes it a card), and the welcome stays due for the morning.</summary>
        [Test]
        public void AMomentHandedOverAtNightWaitsWithoutCountingATry()
        {
            var sim = Box();
            var e = sim.Chronicle("festival", "The people gathered round the fire for a feast.", 0.9f);
            var m = sim.MomentOfEntry(e.id);
            sim.MomentPacing.welcomeDue = true;
            for (int i = 0; i < MomentRules.MaxDeferrals + 2; i++)
            {
                Assert.AreSame(m, sim.StartDueMoments().Single());
                Assert.IsTrue(sim.NightWaitMoment(m.id));
                Assert.AreEqual(HeldMomentState.Waiting, m.state, "never playing with nothing shown");
                Assert.AreEqual(0, sim.PlayingMoments().Count);
                Assert.AreEqual(0, m.deferrals, "a night is not a try");
                Assert.IsTrue(sim.MomentPacing.welcomeDue, "the welcome waits for the morning");
                Assert.IsTrue(sim.MomentHidesCard(e), "still not a card");
                Assert.AreEqual(0, sim.StartDueMoments().Count, "not offered again at once");
                WatchMinutes(sim, MomentRules.NightRetryMinutes + 0.1);
            }
            Assert.IsFalse(sim.NightWaitMoment(m.id), "only a playing moment");
        }

        [Test]
        public void ADeferredMomentLetsTheNextOnePlay()
        {
            var sim = Box();
            var a = Add(sim, MomentKind.Festival, 0.9f);
            var b = Add(sim, MomentKind.Callback, 0.5f);
            Assert.AreSame(a, sim.StartDueMoments().Single());
            Assert.IsTrue(sim.DeferMoment(a.id));
            Assert.AreSame(b, sim.StartDueMoments().Single(), "the next one waiting is not held up by one whose people are out");
        }

        [Test]
        public void NothingPlaysDuringTheFirstRun()
        {
            var sim = Box();
            Add(sim, MomentKind.Festival);
            sim.State.firstRun.active = true;
            Assert.AreEqual("the first run", sim.MomentWaitReason());
            Assert.AreEqual(0, sim.StartDueMoments().Count);
        }

        // ---------- the save ----------

        [Test]
        public void TheQueueAndThePacingRoundTripAndAnOldSaveLoads()
        {
            var sim = Box();
            Add(sim, MomentKind.Festival, 0.7f);
            Add(sim, MomentKind.Callback, 0.9f, 3);
            sim.StartDueMoments();
            sim.MomentPacing.restUntil = 12.5; sim.MomentPacing.releases = 4;
            string json = JsonUtility.ToJson(sim.State);
            var back = JsonUtility.FromJson<SimState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(back));
            Assert.AreEqual(2, back.moments.Count);
            var loaded = new SimCore(Cfg(), back);
            Assert.IsNull(loaded.MomentWaitReason(), "a load is a new visit: what was playing waits again and the welcome is due");
            Assert.AreEqual(2, loaded.WaitingMoments().Count);
            Assert.AreEqual(MomentKind.Callback, loaded.StartDueMoments().Single().kind, "the top one");

            var old = JsonUtility.FromJson<SimState>("{\"month\":5,\"population\":4}");
            Assert.IsNotNull(old.moments);
            Assert.AreEqual(0, old.moments.Count);
            Assert.IsNotNull(old.momentPacing);
            Assert.AreEqual(1, old.momentPacing.credit, 1e-9);
            Assert.AreEqual(-2, old.momentPacing.lastLeaderId);
        }

        // ---------- t1-moments: a ceremony's line is a card only if its ceremony never plays ----------

        static bool InAnyCardPath(SimCore sim, ChronicleEntry e)
        {
            bool Has(ReturnStack st) => st.cards.Concat(st.also).Any(c => c.entries.Contains(e));
            return Has(sim.UnreadCardsCapped(1000)) || Has(sim.ReturnCardsCapped(-1, 1000));
        }

        [Test]
        public void ACeremonyLineIsNotACardEvenToAListenerOfTheLineItself()
        {
            // Tester run 1: a card said "Esk married Aveline" though no wedding had been shown live. The chronicle event came before the
            // queue knew the line, so anything working out the cards on it (the history dot) saw an unread card.
            var sim = Box();
            sim.CardsSeenToNow();
            sim.Advance(1.0 / 12);
            bool sawCard = false;
            sim.Chronicled += e => { if (e.kind == "festival") sawCard = InAnyCardPath(sim, e) || sim.HasUnreadCards; };
            sim.Chronicle("festival", "The people gathered round the fire for a feast.", 0.65f);
            Assert.IsFalse(sawCard, "the moment is queued before anyone is told of the line");
        }

        [Test]
        public void APlayedCeremonyStaysOutOfTheCardsUntilTheLedgerIsPastIt()
        {
            var sim = Box();
            sim.CardsSeenToNow();
            sim.Advance(1.0 / 12);
            var e = sim.Chronicle("wedding", "A and B married by the winter fire.", 0.7f, 1);
            var m = sim.MomentOfEntry(e.id);
            m.state = HeldMomentState.Played; m.endMonth = sim.State.month;
            for (int i = 0; i < (MomentRules.KeepFinishedMonths + 24); i++) sim.Advance(1.0 / 12); // 32 game years; the stack was never opened
            Assert.IsTrue(sim.State.chronicle.Contains(e));
            Assert.IsFalse(InAnyCardPath(sim, e), "it was watched live: its line never becomes a card later");
        }

        [Test]
        public void AwayAndBackNoCeremonyLineIsACardUnlessItsMomentBecameOne()
        {
            foreach (int seed in new[] { 3, 7, 11 })
            {
                var sim = Box(seed);
                sim.CardsSeenToNow();
                int firstId = sim.State.chronicle.Count > 0 ? sim.State.chronicle.Max(x => x.id) : 0; // the box's first year was lived before the queue was emptied
                for (int round = 0; round < 6; round++)
                {
                    sim.CatchUp(8);
                    sim.BeginVisit();
                    foreach (var e in sim.State.chronicle.Where(x => x.id > firstId && MomentRules.KindOfLine(x.kind, out _) && x.month > sim.State.cards.since))
                    {
                        var m = sim.MomentOfEntry(e.id);
                        if (InAnyCardPath(sim, e))
                            Assert.IsTrue(m == null ? false : m.state == HeldMomentState.Carded, $"seed {seed} round {round}: '{e.text}' ({e.kind} #{e.id} month {e.month}) is a card ({(m == null ? "no moment; in list: " + sim.State.moments.Any(x => x.entryId == e.id) + ", moments " + sim.State.moments.Count : m.state.ToString())})");
                    }
                    var due = sim.StartDueMoments();
                    foreach (var d in due) sim.FinishMoment(d.id, true);
                }
            }
        }
    }
}
