using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#192: parents carry babies outside. A baby is seen most days (in a carer's arms or on their back), is only ever
    /// with someone whose hands are free, is never carried by two, and none of it touches the sim.</summary>
    public class LifeCarryTests
    {
        static SimCore SimIn(Era era, int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < era; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(era, sim.State.era, $"seed {seed} reaches {era}");
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        /// <summary>The first seed from <paramref name="seed"/> up whose box has a baby in it.</summary>
        static SimCore WithBaby(Era era, int seed)
        {
            for (int i = 0; i < 20; i++)
            {
                var sim = SimIn(era, seed + i);
                if (Babies(new LifePlanner(sim)).Count > 0) return sim;
            }
            Assert.Fail($"no {era} box from seed {seed} has a baby");
            return null;
        }

        static List<PersonRef> Babies(LifePlanner planner) => planner.People.Where(p => planner.StageOf(p) == LifeStage.Baby).ToList();

        [TestCase(Era.Village, 3)]
        [TestCase(Era.Town, 5)]
        public void ABabyIsSeenOnMostDaysAndNeverWithTwoCarersOrBusyHands(Era era, int seed)
        {
            var sim = WithBaby(era, seed);
            var planner = new LifePlanner(sim);
            var babies = Babies(planner);
            Assert.GreaterOrEqual(babies.Count, 1, "the box has a baby to carry");
            long day0 = sim.State.month;
            int days = 0, seenDays = 0, babyDays = 0;
            var acts = new List<PersonActivity>();
            for (long day = day0; day < day0 + 30; day++)
            {
                days++;
                var seen = new HashSet<int>();
                for (float h = 6f; h < 20f; h += 0.25f)
                {
                    var t = new ViewTime(day, h);
                    planner.PlanAll(t, acts);
                    var byKey = acts.ToDictionary(a => a.who.Key);
                    var carriedNow = new HashSet<int>();
                    foreach (var a in acts)
                    {
                        if (a.carrying.IsNone) continue;
                        Assert.IsTrue(carriedNow.Add(a.carrying.Key), $"{t}: {a.carrying} has two carers");
                        Assert.AreEqual(LifeStage.Baby, planner.StageOf(a.carrying), "only babies are carried");
                        Assert.IsTrue(planner.StageOf(a.who) is LifeStage.Adult or LifeStage.Elder, "a grown-up carries");
                        Assert.IsTrue(!a.IsBusy || (a.carryOnBack && a.Group != ActivityGroup.Carry), $"{t}: {a.who} has a baby while {a.kind}");
                        Assert.AreNotEqual(ActivityKind.Sleep, a.kind);
                        Assert.AreEqual(a.who, byKey[a.carrying.Key].carriedBy, "the baby says who has them");
                        Assert.IsTrue(a.kind >= 0 && planner.IsValidPlace(a.place));
                        seen.Add(a.carrying.Key);
                        StringAssert.Contains(a.carryOnFoot ? "toddling after them" : a.carryOnBack ? "on their back" : "in their arms", planner.Describe(a)); // 2026-11-06: a walker toddles
                    }
                    foreach (var a in acts)
                        if (!a.carriedBy.IsNone) Assert.IsTrue(carriedNow.Contains(a.who.Key), $"{t}: {a.who} is carried by nobody");
                }
                babyDays += babies.Count;
                seenDays += seen.Count;
            }
            Assert.GreaterOrEqual(seenDays, babyDays * 0.7f, $"a baby is seen most days ({seenDays} of {babyDays} baby-days)");
        }

        [Test]
        public void SomeAreOnTheBackAndSomeInTheArms()
        {
            var sim = WithBaby(Era.Town, 5);
            var planner = new LifePlanner(sim);
            int back = 0, arms = 0;
            var acts = new List<PersonActivity>();
            long day0 = sim.State.month;
            for (long day = day0; day < day0 + 40; day++)
                for (float h = 8f; h < 17.5f; h += 0.5f)
                {
                    planner.PlanAll(new ViewTime(day, h), acts);
                    foreach (var a in acts) if (!a.carrying.IsNone) { if (a.carryOnBack) back++; else arms++; }
                }
            Assert.Greater(arms, 0);
            Assert.Greater(back, 0);
        }

        [Test]
        public void ItIsTheSameEveryTimeAndTheSimIsUntouched()
        {
            var sim = WithBaby(Era.Village, 3);
            string before = JsonUtility.ToJson(sim.State);
            string Run()
            {
                var planner = new LifePlanner(sim);
                var acts = new List<PersonActivity>();
                var log = new System.Text.StringBuilder();
                long day0 = sim.State.month;
                for (long day = day0; day < day0 + 5; day++)
                    for (float h = 7f; h < 19f; h += 0.5f)
                    {
                        planner.PlanAll(new ViewTime(day, h), acts);
                        foreach (var a in acts) if (!a.carrying.IsNone || !a.carriedBy.IsNone) log.Append(day).Append(h).Append(a.who).Append(a.carrying).Append(a.carriedBy).Append(a.carryOnBack).Append(';');
                    }
                return log.ToString();
            }
            string one = Run(), two = Run();
            Assert.IsNotEmpty(one);
            Assert.AreEqual(one, two);
            Assert.AreEqual(before, JsonUtility.ToJson(sim.State), "planning changed the sim");
        }

        [Test]
        public void TheCardAgreesWithWhatYouSee()
        {
            // The baby stage the card shows (SimCore.People: under 4 is a baby) is the planner's stage, and a carried one is a baby.
            var sim = WithBaby(Era.Village, 3);
            var planner = new LifePlanner(sim);
            foreach (var n in sim.State.notables.Where(n => n.alive))
            {
                bool cardBaby = n.age < 4f;
                Assert.AreEqual(cardBaby, planner.StageOf(PersonRef.Notable(n.id)) == LifeStage.Baby, $"{n.FullName} at {n.age}");
            }
        }
    }
}
