using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>The user's 2026-11-06 calls: hugs on (the book that keeps them rare: HugRules) and toddlers on their feet after their carer
    /// (LifePlanner.Toddle.cs: walking age, the carer's own carry, free hands, short walks, at most three a day, #192's cap).</summary>
    public class HugToddleTests
    {
        // ---------------------------------------------------------------- hugs

        [Test]
        public void HugsADay_GrowWithThePopulation_AndStayFew()
        {
            Assert.AreEqual(2, HugRules.PerDay(6));
            Assert.AreEqual(3, HugRules.PerDay(10));
            Assert.AreEqual(4, HugRules.PerDay(22));
            Assert.AreEqual(6, HugRules.PerDay(30));
            Assert.AreEqual(6, HugRules.PerDay(80), "flat past the town row");
            Assert.LessOrEqual(HugRules.PerDay(16), HugRules.PerDay(22));
        }

        [Test]
        public void TheBook_APairOnceADay_AGapBetween_ACapADay()
        {
            var b = new HugBook();
            Assert.IsTrue(b.Can(5, 1, 2, 0f, 30, HugReason.Reunion, out _));
            b.Took(5, 1, 2, 0f, HugReason.Reunion);
            Assert.IsFalse(b.Can(5, 2, 1, 100f, 30, HugReason.ParentKid, out string why), "the same two, the other way round, the same day");
            StringAssert.Contains("today", why);
            Assert.IsFalse(b.Can(5, 3, 4, HugRules.GapSeconds - 1f, 30, HugReason.Reconcile, out why), "too soon after the last");
            Assert.IsTrue(b.Can(5, 3, 4, HugRules.GapSeconds + 1f, 30, HugReason.Reconcile, out _));
            float t = 100f;
            for (int k = 0; k < 10; k++, t += 100f)
                if (b.Can(5, 10 + k, 50 + k, t, 6, HugReason.ParentKid, out _)) b.Took(5, 10 + k, 50 + k, t, HugReason.ParentKid);
            Assert.AreEqual(HugRules.PerDay(6), b.Today, "a tribe's day stops at its few");
            Assert.IsTrue(b.Can(6, 1, 2, t + 100f, 6, HugReason.Reunion, out _), "a new day: the pair may hug again");
            Assert.IsTrue(b.Can(5, 1, 2, t, 6, HugReason.Cheat, out _), "the cheat skips the book");
            Assert.IsTrue(b.Can(5, 1, 2, t, 6, HugReason.Wedding, out _), "the wedding's hug is the moment's own");
        }

        [Test]
        public void AParentAndKid_HugOnAboutAThirdOfDays_InTheWindow()
        {
            int yes = 0, n = 0;
            for (long day = 0; day < 400; day++)
                for (int part = 0; part < 2; part++)
                {
                    n++;
                    if (!HugRules.ParentKidToday(7, 31, day, part, out float from)) continue;
                    yes++;
                    float lo = part == 0 ? HugRules.DawnFrom : HugRules.EveningFrom, hi = part == 0 ? HugRules.DawnUntil : HugRules.EveningUntil;
                    Assert.That(from, Is.InRange(lo, hi), "from an hour inside the window");
                    Assert.IsTrue(HugRules.ParentKidHours(from, out int p) && p == part);
                }
            Assert.That(yes / (float)n, Is.InRange(HugRules.ParentKidShare - 0.08f, HugRules.ParentKidShare + 0.08f));
            Assert.IsFalse(HugRules.ParentKidHours(12f, out _), "not at midday");
            Assert.IsFalse(HugRules.ParentKidHours(23f, out _), "not at night");
        }

        // ---------------------------------------------------------------- toddlers

        static SimCore SimIn(Era era, int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        static (SimCore sim, LifePlanner planner) WithToddler(Era era, int seed)
        {
            for (int i = 0; i < 20; i++)
            {
                var sim = SimIn(era, seed + i);
                var planner = new LifePlanner(sim);
                if (planner.People.Any(p => planner.StageOf(p) == LifeStage.Baby && planner.AgeOf(p) >= LifePlanner.ToddleAge)) return (sim, planner);
            }
            Assert.Fail($"no {era} box from seed {seed} has a walking-age baby");
            return default;
        }

        [TestCase(Era.Village, 3)]
        [TestCase(Era.Town, 5)]
        public void ToddlersWalk_OnlyAfterTheirCarer_WithFreeHands_OnShortWalks_AtMostThree(Era era, int seed)
        {
            var (sim, planner) = WithToddler(era, seed);
            string before = JsonUtility.ToJson(sim.State);
            var acts = new List<PersonActivity>();
            long day0 = sim.State.month;
            int onFoot = 0, carried = 0;
            for (long day = day0; day < day0 + 20; day++)
                for (float h = 6f; h < 21f; h += 0.2f)
                {
                    var t = new ViewTime(day, h);
                    planner.PlanAll(t, acts);
                    var byKey = acts.ToDictionary(a => a.who.Key);
                    int feet = 0;
                    foreach (var a in acts)
                    {
                        if (!a.carriedBy.IsNone)
                        {
                            var c = byKey[a.carriedBy.Key];
                            Assert.AreEqual(a.who, c.carrying, $"{t}: the carer has them");
                            Assert.AreEqual(a.carryOnFoot, c.carryOnFoot, $"{t}: both say whether the little one walks");
                            if (a.carryOnFoot)
                            {
                                feet++;
                                Assert.GreaterOrEqual(planner.AgeOf(a.who), LifePlanner.ToddleAge, "only a walker toddles");
                                Assert.IsFalse(c.IsBusy, $"{t}: {c.who} has a toddler on foot while {c.kind}");
                                Assert.IsFalse(c.carryOnBack, "on foot is not on the back");
                                Assert.IsTrue(h >= LifePlanner.CarryFrom && h < LifePlanner.CarryUntil, "out only in the carry window (never at night)");
                                StringAssert.Contains("toddling after them", planner.Describe(c));
                            }
                            else carried++;
                        }
                        if (!a.carrying.IsNone && a.carryOnFoot) onFoot++;
                    }
                    Assert.LessOrEqual(feet, LifePlanner.MaxOnFoot, $"{t}: #192's cap");
                }
            Assert.Greater(onFoot, 0, "a toddler is seen on its feet");
            Assert.Greater(carried, 0, "and still carried at other times");
            Assert.AreEqual(before, JsonUtility.ToJson(sim.State), "planning changed the sim");
        }

        [Test]
        public void ABabyUnderWalkingAge_IsNeverOnFoot()
        {
            var sim = SimIn(Era.Village, 3);
            var baby = sim.DebugBirth();
            Assert.IsNotNull(baby);
            var planner = new LifePlanner(sim);
            planner.SetDebugToddlers(true);
            try
            {
                var acts = new List<PersonActivity>();
                long day0 = sim.State.month;
                for (long day = day0; day < day0 + 5; day++)
                    for (float h = 8f; h < 17.5f; h += 0.25f)
                    {
                        planner.PlanAll(new ViewTime(day, h), acts);
                        foreach (var a in acts) if (a.who.Key == PersonRef.Notable(baby.id).Key) Assert.IsFalse(a.carryOnFoot, "a newborn is a babe in arms");
                    }
            }
            finally { planner.SetDebugToddlers(false); }
        }

        [Test]
        public void TheToddlerCheat_PutsEveryWalkerOnItsFeetWhenItMayBe()
        {
            var (sim, planner) = WithToddler(Era.Village, 3);
            var acts = new List<PersonActivity>();
            long day0 = sim.State.month;
            int normal = 0, cheat = 0;
            for (long day = day0; day < day0 + 6; day++)
                for (float h = 8f; h < 17.5f; h += 0.25f) { planner.PlanAll(new ViewTime(day, h), acts); normal += acts.Count(a => a.carryOnFoot && !a.carrying.IsNone); }
            planner.SetDebugToddlers(true);
            try
            {
                for (long day = day0; day < day0 + 6; day++)
                    for (float h = 8f; h < 17.5f; h += 0.25f) { planner.PlanAll(new ViewTime(day, h), acts); cheat += acts.Count(a => a.carryOnFoot && !a.carrying.IsNone); }
            }
            finally { planner.SetDebugToddlers(false); }
            Assert.Greater(cheat, 0, $"the cheat shows toddlers walking (normal {normal})");
            Assert.GreaterOrEqual(cheat, normal);
        }
    }
}
