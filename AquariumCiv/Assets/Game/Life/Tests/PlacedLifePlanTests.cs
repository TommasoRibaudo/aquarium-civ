using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#420, #421 on the planner side: the deer hunt at the plan level (when, who, hit or miss, settled once, never below the
    /// floor), and no fishing of any kind until fish are placed (the fisher's work, the pastimes, the night fishers, the debug bite,
    /// the people living apart).</summary>
    public class PlacedLifePlanTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static void Count(SimCore sim) => new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).Count();

        /// <summary>A w x h block of <paramref name="cv"/> on dry ground at least <paramref name="far"/> from the fire.</summary>
        static void Block(SimCore sim, int w, int h, float far, EcoCover cv, int skip = 0)
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
                    if (!ok || skip-- > 0) continue;
                    for (int dj = 0; dj < h; dj++) for (int di = 0; di < w; di++) e.cover[SiteMap.Index(i + di, j + dj)] = (byte)cv;
                    Count(sim);
                    return;
                }
            Assert.Fail("no room");
        }

        /// <summary>The camp's rungs placed (on bare ground: the debug cheat), a wood of 30 grown trees and a full herd of deer.</summary>
        static SimCore Deer(int seed = 7)
        {
            var sim = World(seed);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Rabbits), 0);
            Block(sim, 6, 5, 1.4f, EcoCover.Tree);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Deer), 0);
            sim.DebugDeerToCap();
            Assert.AreEqual(8, sim.EcoDeerCount);
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge).Take(1))
                sim.DebugSetJob(n, JobType.Hunter);
            return sim;
        }

        // ---------------------------------------------------------------- #420 the deer hunt
        [Test]
        public void NoDeer_NoDeerHunt()
        {
            var sim = World();
            var planner = new LifePlanner(sim);
            for (long d = 0; d < 80; d++) Assert.IsEmpty(planner.DeerHuntsOn(d));
            Assert.IsFalse(planner.DebugStartDeerHunt(new ViewTime(3, 9f), true, out _));
        }

        [Test]
        public void TheDeerHunt_SomeDays_TwoOrThreeGo_TheHunterFirst_SettledOnce_NeverBelowTheFloor()
        {
            var sim = Deer();
            var hunter = sim.State.notables.First(n => n.alive && n.job == JobType.Hunter);
            var planner = new LifePlanner(sim);
            var plans = new List<DeerHuntPlan>();
            for (long d = 0; d < 120; d++) plans.AddRange(planner.DeerHuntsOn(d));
            TestContext.Out.WriteLine($"{plans.Count} deer hunts in 120 days");
            Assert.That(plans.Count, Is.InRange(8, 40), "not a daily job");
            Assert.IsTrue(plans.All(p => p.hunters.Count >= 2 && p.hunters.Count <= 3), "two or three go");
            Assert.IsTrue(plans.All(p => p.hunters.Distinct().Count() == p.hunters.Count));
            Assert.IsTrue(plans.All(p => p.hunters[0] == PersonRef.Notable(hunter.id)), "the hunter leads");
            Assert.IsTrue(plans.All(p => p.start >= LifePlanner.DeerHuntFrom && p.start <= LifePlanner.DeerHuntFrom + LifePlanner.DeerHuntSpread + 0.01f), "after the dawn drink");
            Assert.IsTrue(plans.All(p => p.resolveHour > p.start && p.end > p.resolveHour));
            Assert.IsTrue(plans.Any(p => p.hits) && plans.Any(p => !p.hits), "some miss");
            Assert.AreEqual(plans.Count, plans.Select(p => p.day).Distinct().Count(), "one a day at most");
            // the same plan whatever is asked first
            planner = new LifePlanner(sim); // (the first planner kept only its last days)
            var first = planner.DeerHuntsOn(plans[0].day).Single();
            Assert.AreEqual(plans[0].start, first.start); Assert.AreEqual(plans[0].hits, first.hits); CollectionAssert.AreEqual(plans[0].hunters, first.hunters);
            // settled at the shot, once
            Assert.AreEqual(0, planner.ResolveDeerHunts(new ViewTime(first.day, first.resolveHour - 0.1f)));
            int herd = sim.EcoDeerCount;
            var settled = new List<DeerHuntPlan>();
            Assert.AreEqual(1, planner.ResolveDeerHunts(new ViewTime(first.day, first.resolveHour + 0.01f), settled));
            Assert.AreSame(first, settled[0]);
            Assert.AreEqual(first.hits ? herd - 1 : herd, sim.EcoDeerCount, "a hit takes one deer");
            Assert.AreEqual(first.hits, first.caught);
            Assert.AreEqual(0, planner.ResolveDeerHunts(new ViewTime(first.day, 23f)), "once");
            Assert.AreEqual(1, sim.DeerHuntsSettled(first.day));
            // a reload of the same day doesn't settle it again
            var reload = new LifePlanner(sim);
            reload.DeerHuntsOn(first.day);
            Assert.AreEqual(0, reload.ResolveDeerHunts(new ViewTime(first.day, 23f)));
            // every hit, day after day: never below the floor
            for (int k = 0; k < 30; k++)
            {
                Assert.IsTrue(planner.DebugStartDeerHunt(new ViewTime(200 + k, 8f), true, out var p));
                Assert.IsTrue(p.hits && p.forced && p.debugStarted);
                planner.ResolveDeerHunts(new ViewTime(200 + k, 23f));
                Assert.GreaterOrEqual(sim.EcoDeerCount, 2);
            }
            Assert.AreEqual(2, sim.EcoDeerCount);
            Assert.IsTrue(sim.DeerAntlersUp, "a stag came home on the pole");
            for (long d = 300; d < 360; d++) Assert.IsEmpty(planner.DeerHuntsOn(d), "nothing to spare: no hunt is planned");
        }

        [Test]
        public void DebugDeerHunt_TheMissTakesNothing()
        {
            var sim = Deer();
            var planner = new LifePlanner(sim);
            Assert.IsTrue(planner.DebugStartDeerHunt(new ViewTime(5, 9f), false, out var p));
            Assert.IsFalse(p.hits);
            planner.ResolveDeerHunts(new ViewTime(5, 23f));
            Assert.IsTrue(p.resolved); Assert.IsFalse(p.caught);
            Assert.AreEqual(8, sim.EcoDeerCount);
        }

        // ---------------------------------------------------------------- #421 no fishing until fish are placed
        static List<PersonActivity> Fishing(SimCore sim, int days = 14)
        {
            var planner = LifePlanner.Wired(sim);
            var all = new List<PersonActivity>(); var day = new List<PersonActivity>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, i, day);
                    all.AddRange(day.Where(a => a.kind == ActivityKind.Fish));
                }
            return all;
        }

        [Test]
        public void NoFishing_OfAnyKind_UntilTheFishArePlaced()
        {
            var sim = World(5);
            Block(sim, 6, 6, 1.2f, EcoCover.Water);
            Assert.Greater(sim.EcoPondCells, 0, "a pond");
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge).Take(2)) sim.DebugSetJob(n, JobType.Fisher);
            Assert.IsFalse(sim.FishingOpen);
            Assert.IsFalse(sim.JobOpen(JobType.Fisher), "the fisher's job waits for the fish");
            Assert.IsEmpty(Fishing(sim), "nobody fishes an empty pond");
            Assert.AreEqual(-1, LifePlanner.Wired(sim).DebugFishNow(new ViewTime(2, 10f)), "not even the debug bite");
            Assert.AreEqual(ApartDoing.Wander, ApartLife.Doing(ApartKind.ShoreHut, 0, 10f, sim.FishingOpen), "the shore hut doesn't fish either");

            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Ducks), 0);
            Assert.IsEmpty(Fishing(sim), "the ducks are not fish");
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Fish), 0);
            Assert.IsTrue(sim.FishingOpen);
            Assert.IsTrue(sim.JobOpen(JobType.Fisher));
            Assert.IsNotEmpty(Fishing(sim), "fishing appears at once");
            Assert.Greater(LifePlanner.Wired(sim).DebugFishNow(new ViewTime(2, 10f)), 0);
            Assert.AreEqual(ApartDoing.Fish, ApartLife.Doing(ApartKind.ShoreHut, 0, 10f, sim.FishingOpen));
        }
    }
}
