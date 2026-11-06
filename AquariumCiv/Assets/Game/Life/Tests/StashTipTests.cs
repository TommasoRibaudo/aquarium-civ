using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#361 (the end beat of berry picking): a gatherer's basket carried to the camp's stash pile is tipped onto it, in a short
    /// TipLoad block at the pile right after the carry. Its time comes out of the work before the carry: the day's other blocks keep their times.</summary>
    public class StashTipTests
    {
        static SimCore Camp(int seed, int months)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < months && !sim.State.eraReady; m++) sim.Step();
            Assert.AreEqual(Era.Camp, sim.State.era);
            return sim;
        }

        /// <summary>Every person's day, in order, for days spring to winter.</summary>
        static List<List<PersonActivity>> Days(SimCore sim, int days = 40, int stride = 9)
        {
            var planner = LifePlanner.Wired(sim);
            var all = new List<List<PersonActivity>>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People)
                {
                    var day = new List<PersonActivity>();
                    planner.DaySchedule(who, i * stride, day);
                    all.Add(day);
                }
            return all;
        }

        static bool BasketToStash(PersonActivity a) => a.kind == ActivityKind.Carry && a.moving && a.prop == Prop.Basket && a.place == CampStash.Stand;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryBasketCarriedToTheStashIsTippedThere(int seed)
        {
            int carries = 0, tips = 0;
            foreach (var day in Days(Camp(seed, 60)))
                for (int i = 0; i < day.Count; i++)
                {
                    if (!BasketToStash(day[i])) continue;
                    carries++;
                    Assert.Less(i + 1, day.Count, "the day goes on after the carry");
                    var tip = day[i + 1];
                    Assert.AreEqual(ActivityKind.TipLoad, tip.kind, $"{day[i].who} at {day[i].end:0.00}");
                    Assert.AreEqual(CampStash.Stand, tip.place, "at the pile");
                    Assert.IsFalse(tip.moving, "standing still");
                    Assert.AreEqual(Prop.Basket, tip.prop, "the basket is still in the arms");
                    Assert.AreEqual(day[i].end, tip.start, 1e-3f, "right after the carry");
                    float secs = LifeClock.RealSecondsBetween(tip.start, tip.end);
                    Assert.That(secs, Is.InRange(1.5f, 2.2f), "a short beat, not a pause");
                    tips++;
                }
            TestContext.WriteLine($"seed {seed}: {carries} basket carries to the stash, {tips} tips");
            Assert.Greater(carries, 0, "the gatherers carry baskets home");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void TipsKeepTheDayWhole(int seed)
        {
            // no overlaps or holes: each block starts where the one before ended, tips included
            foreach (var day in Days(Camp(seed, 60)))
                for (int i = 1; i < day.Count; i++)
                    Assert.AreEqual(day[i - 1].end, day[i].start, 1e-3f, $"{day[i].who}: {day[i - 1].kind} -> {day[i].kind}");
        }

        [Test]
        public void OnlyBasketsAtTheStashAreTipped()
        {
            foreach (var day in Days(Camp(1, 60), 20))
                for (int i = 0; i < day.Count; i++)
                    if (day[i].kind == ActivityKind.TipLoad)
                    {
                        Assert.AreEqual(Prop.Basket, day[i].prop);
                        Assert.AreEqual(CampStash.Stand, day[i].place);
                        Assert.IsTrue(i > 0 && BasketToStash(day[i - 1]), "a tip only follows the carry");
                    }
        }

        [Test]
        public void TipLoad_IsBusyWork_NotAMove()
        {
            Assert.IsTrue(ActivityInfo.IsBusy(ActivityKind.TipLoad));
            Assert.IsFalse(ActivityInfo.IsMove(ActivityKind.TipLoad));
            Assert.AreEqual(ActivityGroup.Carry, ActivityInfo.GroupOf(ActivityKind.TipLoad));
        }
    }
}
