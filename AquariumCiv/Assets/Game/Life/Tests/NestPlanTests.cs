using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#709 (B71.c, B71.d): what the people do at the nest. In spring, while it holds eggs: now and then a gatherer takes an egg (settled once
    /// through the sim, never the last, carried to the pile), and on some days the kids look in (never on a gather day, 1 to 3, never touching).
    /// Without eggs nobody goes.</summary>
    public class NestPlanTests
    {
        static SimCore Nested(bool eggs = true)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            sim.DebugOpenCampGate();
            var g = sim.EcoGround;
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare && !g.InBasin(c)) sim.State.eco.cover[c] = (byte)EcoCover.Grass;
            for (int y = 0; y < 16; y++) sim.Advance(12 * cfg.StepHours); // a few years so there are grown-ups and kids
            TestContext.Out.WriteLine($"pop {sim.State.population} notables {sim.State.notables.Count} kids {sim.State.notables.Count(n => n.alive && n.age < SimCore.AdultAge)}");
            Assert.GreaterOrEqual(sim.DebugNestPlace(), 0, "a nest");
            if (eggs) { Assert.Greater(sim.DebugNestLay(), 0); sim.State.eco.nest.eggs = 3; }
            return sim;
        }

        [Test]
        public void NoEggs_NoOneGoes()
        {
            var sim = Nested(false);
            var planner = new LifePlanner(sim);
            for (long d = 0; d < 60; d++) Assert.IsEmpty(planner.NestVisitsOn(d));
            Assert.IsFalse(sim.NestKidsMayLook);
        }

        [Test]
        public void WithEggs_SomeDaysAGathererGoes_SomeDaysKidsLook_NeverBothOnADay()
        {
            var sim = Nested();
            var planner = new LifePlanner(sim);
            var plans = new List<NestPlan>();
            for (long d = 0; d < 120; d++) plans.AddRange(planner.NestVisitsOn(d));
            int gathers = plans.Count(p => p.gather), looks = plans.Count(p => !p.gather);
            TestContext.Out.WriteLine($"{gathers} gathers, {looks} looks in 120 days");
            Assert.Greater(gathers, 3, "now and then"); Assert.Less(gathers, 50, "not a daily job");
            Assert.Greater(looks, 15, "about 1 day in 2"); Assert.Less(looks, 90);
            Assert.AreEqual(plans.Count, plans.Select(p => p.day).Distinct().Count(), "one visit a day at most: the kids never look while a gatherer is there");
            Assert.IsTrue(plans.All(p => p.bushCell == sim.NestCell));
            Assert.IsTrue(plans.Where(p => !p.gather).All(p => p.lookerKeys.Count >= 1 && p.lookerKeys.Count <= 3), "one to three kids");
            Assert.IsTrue(plans.All(p => p.pickStart > p.start && p.resolveHour > p.pickStart && p.end >= p.carryEnd));
            Assert.IsFalse(plans.Where(p => p.gather).Any(p => LifeClock.IsRestDay(p.day)), "a rest day is a rest day");
        }

        [Test]
        public void TheGatherer_IsAGrownUp_TheKids_AreKids_AndThePlanIsTheSameWhateverIsAskedFirst()
        {
            var sim = Nested();
            var planner = new LifePlanner(sim);
            NestPlan g = null, l = null; long gd = 0, ld = 0;
            for (long d = 0; d < 200 && (g == null || l == null); d++)
                foreach (var p in planner.NestVisitsOn(d)) { if (p.gather && g == null) { g = p; gd = d; } if (!p.gather && l == null) { l = p; ld = d; } }
            Assert.NotNull(g); Assert.NotNull(l);
            var gatherer = sim.State.notables.FirstOrDefault(n => n.alive && n.age >= 16f && PersonRef.Notable(n.id).Key == g.key);
            if (gatherer != null) Assert.AreNotEqual(JobType.Toolmaker, gatherer.job);
            planner = new LifePlanner(sim);
            var again = planner.NestVisitsOn(gd).Single();
            Assert.AreEqual(g.start, again.start); Assert.AreEqual(g.key, again.key);
            var look = planner.NestVisitsOn(ld).Single();
            CollectionAssert.AreEqual(l.lookerKeys, look.lookerKeys);
        }

        [Test]
        public void TheEggGather_IsSettledOnce_ThroughTheSim_NeverTheLast_AndFoodGoesUp()
        {
            var sim = Nested();
            sim.State.eco.nest.eggs = 3;
            var finder = new LifePlanner(sim);
            NestPlan found = null;
            for (long d = 0; d < 200 && found == null; d++) found = finder.NestVisitsOn(d).FirstOrDefault(p => p.gather);
            Assert.NotNull(found);
            var planner = new LifePlanner(sim); // only that day is made: no older visit settles with it
            var plan = planner.NestVisitsOn(found.day).Single();
            float food = sim.State.food;
            Assert.AreEqual(0, planner.ResolveNestVisits(new ViewTime(plan.day, plan.resolveHour - 0.1f)));
            var settled = new List<NestPlan>();
            Assert.AreEqual(1, planner.ResolveNestVisits(new ViewTime(plan.day, plan.resolveHour + 0.01f), settled));
            Assert.AreSame(plan, settled[0]);
            Assert.IsTrue(plan.taken); Assert.AreEqual(2, sim.NestEggs); Assert.Greater(sim.State.food, food);
            Assert.AreEqual(0, planner.ResolveNestVisits(new ViewTime(plan.day, 23f)), "once");
            // one egg left to take: the last stays
            sim.State.eco.nest.eggs = 1;
            var next = planner.DebugStartNestVisit(new ViewTime(plan.day + 1, 8f), true, out var forced);
            Assert.IsTrue(next);
            planner.ResolveNestVisits(new ViewTime(forced.day, forced.resolveHour + 0.01f));
            Assert.IsFalse(forced.taken, "never the last egg");
            Assert.AreEqual(1, sim.NestEggs);
        }

        [Test]
        public void TheLook_IsCounted_RaisesTheEvent_AndNothingIsTaken()
        {
            var sim = Nested();
            var heard = new List<NestEventKind>();
            sim.NestChanged += (k, c) => heard.Add(k);
            var planner = new LifePlanner(sim);
            Assert.IsTrue(planner.DebugStartNestVisit(new ViewTime(5, 9f), false, out var look));
            int eggs = sim.NestEggs;
            Assert.AreEqual(1, planner.ResolveNestVisits(new ViewTime(5, look.resolveHour + 0.01f)));
            Assert.AreEqual(1, sim.NestLooks);
            Assert.Contains(NestEventKind.KidsLooked, heard);
            Assert.AreEqual(eggs, sim.NestEggs, "the kids never touch the eggs");
        }

        [Test]
        public void TheGatherersBeat_WalksToTheBush_PicksThere_CarriesBack_AndTheLookersInspectAndWalkHome()
        {
            var sim = Nested();
            var planner = new LifePlanner(sim);
            Assert.IsTrue(planner.DebugStartNestVisit(new ViewTime(5, 9f), true, out var plan));
            var who = plan.key;
            var person = sim.State.notables.Select(n => PersonRef.Notable(n.id)).FirstOrDefault(r => r.Key == who);
            // a mid-pick moment: the gatherer is at the bush with a basket
            float mid = (plan.pickStart + plan.resolveHour) * 0.5f;
            var act = planner.Plan(person, new ViewTime(5, mid));
            Assert.AreEqual(ActivityKind.Gather, act.kind);
            Assert.AreEqual(PlaceKind.BushCell, act.place.kind);
            Assert.AreEqual(sim.NestCell, act.place.index);
            Assert.AreEqual(Prop.Basket, act.prop);
        }
    }
}
