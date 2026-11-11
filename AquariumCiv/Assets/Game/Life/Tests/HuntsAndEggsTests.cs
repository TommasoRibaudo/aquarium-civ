using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#574 (playtest #544, #550, #551: "everyone fishing or cutting wood", "I haven't seen anyone hunt ducks nor rabbits").
    /// Cause found headless: at year 29 in 6 of 60 camps (30-45 rabbits in the meadow) no one held the hunter job: a job nobody holds is
    /// only filled from a crowded job, and five workers on five different jobs have none. And nothing ever took an egg. Here: a camp
    /// with rabbits above the floor has a hunter and plans hunts; with ducks above the floor an egg gather is planned and takes an egg;
    /// at the floor none is.</summary>
    public class HuntsAndEggsTests
    {
        /// <summary>A w x h block of <paramref name="cv"/> on dry ground at least <paramref name="far"/> from the fire (as PlacedLifePlanTests).</summary>
        static void Block(SimCore sim, int w, int h, float far, EcoCover cv)
        {
            var g = sim.EcoGround; var e = sim.State.eco;
            for (int j = 3; j + h < SiteMap.NZ - 3; j++)
                for (int i = 3; i + w < SiteMap.NX - 3; i++)
                {
                    bool ok = true;
                    for (int dj = -1; dj <= h && ok; dj++)
                        for (int di = -1; di <= w && ok; di++)
                        {
                            int c = SiteMap.Index(i + di, j + dj);
                            ok = !g.InBasin(c) && !g.yard[c] && e.cover[c] != (byte)EcoCover.Water && e.cover[c] != (byte)EcoCover.Tree
                                && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), g.campX, g.campZ) >= far;
                        }
                    if (!ok) continue;
                    for (int dj = 0; dj < h; dj++) for (int di = 0; di < w; di++) e.cover[SiteMap.Index(i + di, j + dj)] = (byte)cv;
                    new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).Count();
                    return;
                }
            Assert.Fail("no room");
        }

        /// <summary>The tester kind of camp: camp gate open, a thicket, rabbits placed (early on even seeds, late on odd), a pond with ducks and fish and a wood, then lived to
        /// year <paramref name="years"/>. Seeds 3, 9, 19, 23, 24 and 42 had no hunter at year 29 before the fix (the sweep over seeds 1-60 in the issue repro).</summary>
        internal static SimCore Camp(int seed, int years = 29)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            sim.DebugOpenCampGate();
            for (int y = 0; y < years; y++)
            {
                sim.Advance(12 * cfg.StepHours);
                if (y == 2) sim.DebugPlaceNearCamp(LifeSpecies.Thicket);
                if (y == (seed % 2 == 0 ? 3 : 8)) sim.DebugPlaceNearCamp(LifeSpecies.Rabbits);
                if (y == 5) { Block(sim, 6, 6, 1.2f, EcoCover.Water); Block(sim, 6, 5, 1.4f, EcoCover.Tree); sim.DebugPlaceNearCamp(LifeSpecies.Ducks); sim.DebugPlaceNearCamp(LifeSpecies.Fish); }
            }
            return sim;
        }

        static List<Notable> Workers(SimCore sim) => sim.State.notables.Where(n => n.alive && n.age >= 16f && n.age < sim.Config.retireAge).ToList();

        [TestCase(3)] [TestCase(9)] [TestCase(19)] [TestCase(23)] [TestCase(24)] [TestCase(42)] [TestCase(7)]
        public void ACampWithRabbitsAboveTheFloorHasAHunter_AndHuntsMostWorkingDays(int seed)
        {
            var sim = Camp(seed);
            Assert.Greater(sim.HuntSurplus, 5, "rabbits well above the floor");
            Assert.IsTrue(sim.JobOpen(JobType.Hunter));
            Assert.GreaterOrEqual(Workers(sim).Count(n => n.job == JobType.Hunter), 1, "someone holds the hunter job: " + string.Join(",", Workers(sim).Select(n => n.job)));
            var field = new RabbitHerd.Field();
            sim.EcoRabbitsRefresh(field); field.Sync(sim.EcoRabbitsShown());
            var planner = LifePlanner.Wired(sim);
            planner.Rabbits = field;
            int working = 0, withHunt = 0, hunts = 0;
            for (long d = 600; d < 640; d++)
            {
                if (LifeClock.IsRestDay(d)) continue;
                working++;
                int n = planner.HuntsOn(d).Count;
                hunts += n; if (n > 0) withHunt++;
            }
            TestContext.WriteLine($"seed {seed}: {hunts} hunts, {withHunt} of {working} working days");
            Assert.GreaterOrEqual(withHunt, working * 0.6, "a hunt on most working days (storms aside)");
        }

        [Test]
        public void WorkersOnOtherJobsOnly_StillGetAHunterWhenRabbitsAreAboveTheFloor()
        {
            var sim = Camp(7, 29);
            sim.DebugAddRabbits(8);
            Assert.Greater(sim.HuntSurplus, 0);
            string[] jobs = { JobType.Gatherer, JobType.Fisher, JobType.Woodcutter, JobType.Toolmaker };
            Assert.IsTrue(jobs.All(sim.JobOpen), "the camp has fish, trees and a fire");
            var workers = Workers(sim);
            Assert.GreaterOrEqual(workers.Count, 3, "needs a few workers");
            // everyone on a different job, none crowded: the old rule freed nobody
            Assume.That(workers.Count <= jobs.Length, "more workers than jobs: someone would share one (seed pinned: " + workers.Count + ")");
            for (int i = 0; i < workers.Count; i++) { sim.DebugSetJob(workers[i], jobs[i]); workers[i].jobChangedMonth = 0; }
            Assert.AreEqual(0, Workers(sim).Count(n => n.job == JobType.Hunter));
            sim.Advance(sim.Config.StepHours);
            Assert.AreEqual(1, Workers(sim).Count(n => n.job == JobType.Hunter), "the hunter job took someone: " + string.Join(",", Workers(sim).Select(n => n.job)));
            if (workers.Count == jobs.Length) Assert.IsTrue(Workers(sim).Any(n => n.job == JobType.Toolmaker), "never the toolmaker");
        }

        [Test]
        public void MakeAHunter_Cheat_GivesTheJobToAGrownUpWhoIsNotTheToolmaker()
        {
            var sim = Camp(7, 29);
            foreach (var n in Workers(sim)) if (n.job == JobType.Hunter) sim.DebugSetJob(n, JobType.Gatherer);
            string who = sim.DebugMakeHunter();
            Assert.IsNotEmpty(who);
            var h = Workers(sim).Single(n => n.job == JobType.Hunter);
            Assert.AreEqual(who, h.FullName);
        }

        // ---------------------------------------------------------------- eggs
        static SimCore Pond(int ducks, int eggs)
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            sim.DebugOpenCampGate();
            Block(sim, 6, 6, 1.2f, EcoCover.Water);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Ducks), 0);
            sim.State.eco.ducks = ducks; sim.State.eco.duckEggs = eggs;
            return sim;
        }

        static EggPlan FirstGather(LifePlanner planner, out long day)
        {
            for (day = 0; day < 80; day++) { var l = planner.EggGathersOn(day); if (l.Count > 0) return l[0]; }
            return null;
        }

        [Test]
        public void DucksAboveTheFloor_AnEggGatherIsPlanned_WalksToTheReeds_TakesAnEgg_CarriesItToThePile()
        {
            var sim = Pond(6, 3);
            Assert.AreEqual(3, sim.EggSurplus);
            var planner = LifePlanner.Wired(sim);
            var plan = FirstGather(planner, out long day);
            Assert.IsNotNull(plan, "a gather within 80 days");
            Assert.Less(plan.start, plan.pickStart); Assert.Less(plan.pickStart, plan.resolveHour); Assert.Less(plan.resolveHour, plan.carryEnd);
            var pond = Place.Land(PlaceKind.Pond);
            var walk = planner.Plan(plan.who, new ViewTime(day, (plan.start + plan.pickStart) / 2));
            Assert.AreEqual((ActivityKind.Walk, pond), (walk.kind, walk.place), "to the reeds");
            Assert.IsTrue(walk.moving);
            var pick = planner.Plan(plan.who, new ViewTime(day, (plan.pickStart + plan.resolveHour) / 2));
            Assert.AreEqual((ActivityKind.Gather, Prop.Basket, pond), (pick.kind, pick.prop, pick.place), "kneeling at the pond edge with the basket");
            Assert.AreEqual(0, planner.ResolveEggGathers(new ViewTime(day, plan.resolveHour - 0.05f)), "not before the pick is over");
            Assert.AreEqual(3, sim.EcoDuckEggs);
            Assert.AreEqual(1, planner.ResolveEggGathers(new ViewTime(day, plan.resolveHour + 0.01f)));
            Assert.IsTrue(plan.resolved && plan.taken);
            Assert.AreEqual(2, sim.EcoDuckEggs, "one egg out of the nests");
            Assert.AreEqual(1, sim.EcoEggsTaken);
            Assert.AreEqual(0, planner.ResolveEggGathers(new ViewTime(day, 23f)), "settled once");
            Assert.AreEqual(2, sim.EcoDuckEggs);
            var carry = planner.Plan(plan.who, new ViewTime(day, (plan.resolveHour + plan.carryEnd) / 2));
            Assert.AreEqual((ActivityKind.Carry, Prop.Basket), (carry.kind, carry.prop), "the egg in the basket, to the pile");
            Assert.AreEqual(PlaceKind.Stash, carry.place.kind);
            Assert.IsTrue(carry.moving);
            if (plan.end > plan.carryEnd)
                Assert.AreEqual(ActivityKind.TipLoad, planner.Plan(plan.who, new ViewTime(day, (plan.carryEnd + plan.end) / 2)).kind, "tipped at the pile");
        }

        [Test]
        public void DucksAtTheFloor_NoEggGather_AndTakeEggRefuses()
        {
            var sim = Pond(2, 3);
            Assert.AreEqual(0, sim.EggSurplus, "at the floor of 2 no egg is spared");
            Assert.IsFalse(sim.TakeEgg());
            var planner = LifePlanner.Wired(sim);
            for (long d = 0; d < 80; d++) Assert.IsEmpty(planner.EggGathersOn(d));
            Assert.AreEqual(3, sim.EcoDuckEggs);
            Assert.AreEqual(3, sim.DebugEggsToNests(5), "the cheat leaves the nests alone at the floor");
        }

        [Test]
        public void NoDucks_NoEggGather()
        {
            var cfg = new SimConfig { ecology = true };
            var sim = new SimCore(cfg, SimCore.NewWorld(7, cfg));
            var planner = LifePlanner.Wired(sim);
            for (long d = 0; d < 40; d++) Assert.IsEmpty(planner.EggGathersOn(d));
            Assert.IsFalse(planner.DebugStartEggGather(new ViewTime(5, 9f), out _));
        }

        [Test]
        public void ForcedGatherAtTheFloor_WalksBackEmptyHanded_NothingTaken()
        {
            var sim = Pond(2, 0);
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.DebugStartEggGather(new ViewTime(5, 9f), out var plan));
            Assert.AreEqual(1, planner.ResolveEggGathers(new ViewTime(5, plan.resolveHour + 0.01f)));
            Assert.IsFalse(plan.taken);
            Assert.AreEqual(0, sim.EcoEggsTaken);
            var back = planner.Plan(plan.who, new ViewTime(5, (plan.resolveHour + plan.carryEnd) / 2));
            Assert.AreEqual((ActivityKind.Walk, Prop.None), (back.kind, back.prop), "empty basket, no carry");
        }

        [Test]
        public void TheGatherIsDeterministic_AndHuntersHunt()
        {
            var sim = Pond(6, 3);
            var a = FirstGather(LifePlanner.Wired(sim), out long d1);
            var b = FirstGather(LifePlanner.Wired(sim), out long d2);
            Assert.AreEqual(d1, d2); Assert.AreEqual(a.who, b.who); Assert.AreEqual(a.start, b.start);
            var planner = LifePlanner.Wired(sim);
            foreach (var n in Workers(sim)) sim.DebugSetJob(n, JobType.Hunter);
            planner.Invalidate();
            for (long d = 0; d < 80; d++)
                foreach (var g in planner.EggGathersOn(d))
                    Assert.AreNotEqual(JobType.Hunter, planner.JobOf(g.who), "hunters hunt");
        }
    }
}
