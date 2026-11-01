using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#150: in the camp every load ends at the stash by the fire, not in the fire, and the stash leaves the log seats
    /// and the crumb's stone alone. #152: the bucket goes out empty and comes back full.</summary>
    public class CampStashAndBucketTests
    {
        const int Days = 40;

        static SimCore Camp(int seed, int months)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < months && !sim.State.eraReady; m++) sim.Step();
            Assert.AreEqual(Era.Camp, sim.State.era);
            return sim;
        }

        static SimCore Village(int seed)
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            for (int m = 0; m < 120 * 12 && sim.State.era < Era.Village; m++)
            {
                sim.Step();
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(Era.Village, sim.State.era);
            for (int m = 0; m < 24; m++) sim.Step();
            return sim;
        }

        /// <summary>Everyone's planned segments for some days: spring to winter, so the seasonal loads all show.</summary>
        static List<PersonActivity> Segs(SimCore sim, int days = Days, int stride = 9)
        {
            var planner = LifePlanner.Wired(sim);
            var all = new List<PersonActivity>();
            var day = new List<PersonActivity>();
            for (int i = 0; i < days; i++)
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, i * stride, day);
                    all.AddRange(day);
                }
            return all;
        }

        static bool IsLoad(PersonActivity a) => a.moving && a.Group == ActivityGroup.Carry;

        // ------------------------------------------------------------------ #150: where a load ends

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CampLoadsEndAtTheStashNotInTheFire(int seed)
        {
            var carries = Segs(Camp(seed, 60)).Where(IsLoad).ToList();
            var inFire = carries.Where(a => a.place.kind == PlaceKind.Fire).Select(a => $"{a.who} {a.kind} {a.prop} {a.from}->{a.place} {a.start:0.00}-{a.end:0.00} {a.source} {a.trip}");
            Assert.IsEmpty(inFire, "nothing is set down in the fire");
            var kinds = carries.Where(a => a.place == CampStash.Stand).Select(a => a.prop).Distinct().ToList();
            TestContext.WriteLine("to the stash: " + string.Join(", ", kinds));
            Assert.Contains(Prop.Hide, kinds, "the hunter's hide");
        }

        [Test]
        public void AllKindsOfCampLoadReachTheStash()
        {
            var kinds = new HashSet<Prop>();
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
                foreach (var a in Segs(Camp(seed, 30)).Where(IsLoad).Where(a => a.place == CampStash.Stand)) kinds.Add(a.prop);
            TestContext.WriteLine(string.Join(", ", kinds));
            Assert.IsTrue(kinds.Contains(Prop.Basket), "the gatherer's basket");
            Assert.IsTrue(kinds.Contains(Prop.Buckets), "the cook's water");
        }

        [Test]
        public void TheWaitingEraStacksItsSeedBasketsAtTheStash()
        {
            var sim = Camp(1, 120 * 12);
            Assert.IsTrue(sim.State.eraReady, "the era waits");
            var seeds = Segs(sim, 10, 1).Where(a => a.prop == Prop.SeedBag && a.kind == ActivityKind.Carry).ToList();
            Assert.Greater(seeds.Count, 0);
            Assert.IsTrue(seeds.All(a => a.place == CampStash.Stand));
        }

        [Test]
        public void AfterTheCampLoadsGoWhereTheyWent()
        {
            var sim = Village(1);
            Assert.IsFalse(LifePlanner.Wired(sim).TryGetStash(out _), "the stash is the camp's");
            Assert.IsFalse(Segs(sim, 10).Any(a => a.place.kind == PlaceKind.Stash));
        }

        [Test]
        public void TheJobReportNamesTheStash()
        {
            var sim = Camp(1, 60);
            var planner = LifePlanner.Wired(sim);
            var carry = Segs(sim).First(a => IsLoad(a) && a.place == CampStash.Stand && a.prop == Prop.Hide);
            StringAssert.Contains("taking a hide to the stash", planner.Describe(carry));
        }

        // ------------------------------------------------------------------ #150: where the stash is

        [Test]
        public void TheStashStaysClearOfTheSeatsAndTheCrumbsStone()
        {
            CampStash.At(0f, 0f, CampStash.PileRadius, out float px, out float pz);
            CampStash.At(0f, 0f, CampStash.StandRadius, out float sx, out float sz);
            for (int i = 0; i < 3; i++)
            {
                // Pieces.Firepit's seats. Both ways the 0.3 log could lie, to be sure of it.
                float a = i * 2.1f + 0.4f;
                float cx = (float)Math.Cos(a), cz = (float)Math.Sin(a);
                for (float u = -0.15f; u <= 0.15f; u += 0.05f)
                {
                    Assert.Greater(Dist(px, pz, cx * (0.4f + u), cz * (0.4f + u)), CampStash.PileExtent + 0.1f, $"seat {i}, radial");
                    Assert.Greater(Dist(px, pz, cx * 0.4f - cz * u, cz * 0.4f + cx * u), CampStash.PileExtent + 0.1f, $"seat {i}, across");
                    Assert.Greater(Dist(sx, sz, cx * (0.4f + u), cz * (0.4f + u)), 0.1f, $"stand by seat {i}");
                }
            }
            Assert.Greater(Dist(px, pz, 0.34f, 0.1f), CampStash.PileExtent + 0.09f, "RelicProps: the crumb's stone");
            Assert.Greater(CampStash.StandRadius, 0.3f, "outside the ring round the fire");
            Assert.GreaterOrEqual(CampStash.PileRadius - CampStash.StandRadius, CampStash.PileExtent, "the carrier stands at the pile's edge, not in it");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void TheStashHasItsGroundToTheEndOfTheCamp(int seed)
        {
            var sim = Camp(seed, 120 * 12);
            var st = sim.Settlement;
            var pos = new SettlePlacePositions(sim);
            Assert.IsTrue(pos.TryGetPosition(CampStash.Pile, out float px, out float pz));
            Assert.IsTrue(pos.TryGetPosition(CampStash.Stand, out float sx, out float sz));
            st.Fire(out float fx, out float fz, out _);
            Assert.AreEqual(CampStash.PileRadius, Dist(px, pz, fx, fz), 1e-3f);
            Assert.AreEqual(CampStash.StandRadius, Dist(sx, sz, fx, fz), 1e-3f);
            Assert.Greater(st.WalkCost(SiteMap.CellAt(sx, sz)), 0, "a carrier can stand there");
            Assert.Greater(st.WalkCost(SiteMap.CellAt(px, pz)), 0, "the pile is on open ground");
            for (int c = 0; c < SiteMap.N; c++)
            {
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                if (st.WalkCost(c) == 0 && Dist(x, z, fx, fz) > 0.3f) // not the hearth's own lot
                    Assert.GreaterOrEqual(Dist(x, z, px, pz), CampStash.PileExtent, $"a lot or water at ({x:0.0}, {z:0.0})");
            }
        }

        [Test]
        public void TheRouteToTheStashEndsThereNotAtTheFire()
        {
            var sim = Camp(2, 60);
            var pos = new SettlePlacePositions(sim);
            var route = new List<Waypoint>();
            Assert.IsTrue(pos.TryRoute(Place.Land(PlaceKind.Woods), CampStash.Stand, route));
            Assert.IsTrue(pos.TryGetPosition(CampStash.Stand, out float sx, out float sz));
            var last = route[route.Count - 1];
            Assert.Less(Dist(last.x, last.z, sx, sz), SiteMap.Cell * 1.5f);
        }

        // ------------------------------------------------------------------ #150: what a view sizes it from

        [Test]
        public void TheStockGrowsAndShrinksWithFoodAndWood()
        {
            var none = CampStash.StockOf(0f, 0f);
            Assert.AreEqual((0, 0, 0), (none.logs, none.baskets, none.hides));
            var least = CampStash.StockOf(1f, 1f);
            Assert.AreEqual((1, 1, 0), (least.logs, least.baskets, least.hides), "any stock shows");
            var full = CampStash.StockOf(CampStash.FullFood, CampStash.FullWood);
            Assert.AreEqual((CampStash.MaxLogs, CampStash.MaxBaskets, CampStash.MaxHides), (full.logs, full.baskets, full.hides));
            var more = CampStash.StockOf(CampStash.FullFood * 9f, CampStash.FullWood * 9f);
            Assert.AreEqual((full.logs, full.baskets, full.hides), (more.logs, more.baskets, more.hides), "a full pile stays in its ground");
            int logs = 0, baskets = 0, hides = 0;
            for (float v = 0f; v <= 1f; v += 0.01f)
            {
                var s = CampStash.StockOf(v * CampStash.FullFood, v * CampStash.FullWood);
                Assert.GreaterOrEqual(s.logs, logs);
                Assert.GreaterOrEqual(s.baskets, baskets);
                Assert.GreaterOrEqual(s.hides, hides);
                logs = s.logs; baskets = s.baskets; hides = s.hides;
            }
        }

        [Test]
        public void TryGetStashReadsTheStockAndLooksAtTheFire()
        {
            var sim = Camp(1, 60);
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.TryGetStash(out var look));
            Assert.Greater(look.stock.baskets + look.stock.logs, 0);
            Assert.IsTrue(planner.Positions.TryGetPosition(CampStash.Pile, out float px, out float pz));
            Assert.AreEqual(px, look.x, 1e-5f);
            Assert.AreEqual(pz, look.z, 1e-5f);
            Assert.AreEqual(CampStash.PileRadius, Dist(look.faceX, look.faceZ, 0f, 0f), 1e-3f);
            sim.State.food = 0f; sim.State.wood = 0f; // a cheat: no Invalidate needed, the stock is read fresh
            Assert.IsTrue(planner.TryGetStash(out var empty));
            Assert.AreEqual(0, empty.stock.logs + empty.stock.baskets + empty.stock.hides);
        }

        [Test]
        public void NoPositionsNoStashButTheDayStillPlans()
        {
            var sim = Camp(1, 60);
            var planner = new LifePlanner(sim);
            Assert.IsFalse(planner.TryGetStash(out _));
            var day = new List<PersonActivity>();
            foreach (var who in planner.People) Assert.Greater(planner.DaySchedule(who, 3, day), 0);
        }

        [Test]
        public void ThePlannerNeverTouchesTheStock()
        {
            var sim = Camp(1, 60);
            float food = sim.State.food, wood = sim.State.wood;
            Segs(sim, 10);
            LifePlanner.Wired(sim).TryGetStash(out _);
            Assert.AreEqual(food, sim.State.food);
            Assert.AreEqual(wood, sim.State.wood);
        }

        // ------------------------------------------------------------------ #152: the bucket

        [TestCase(1)]
        [TestCase(2)]
        public void TheWalkOutIsEmptyAndTheWalkBackFull_Camp(int seed) => BucketRuns(Segs(Camp(seed, 60)));

        [Test]
        public void TheWalkOutIsEmptyAndTheWalkBackFull_Village() => BucketRuns(Segs(Village(1)));

        static void BucketRuns(List<PersonActivity> segs)
        {
            var draws = segs.Where(a => a.kind == ActivityKind.FetchWater && !a.moving).ToList();
            Assert.Greater(draws.Count, 0, "someone draws water");
            Assert.IsTrue(draws.All(a => a.prop == Prop.EmptyBucket), "not full until it's drawn");
            var back = segs.Where(a => a.kind == ActivityKind.FetchWater && a.moving).ToList();
            Assert.Greater(back.Count, 0, "and carries it home");
            Assert.IsTrue(back.All(a => a.prop == Prop.Buckets), "full on the way back");
            var outs = segs.Where(a => a.kind == ActivityKind.Walk && a.prop == Prop.EmptyBucket).ToList();
            Assert.Greater(outs.Count, 0, "the walk out");
            Assert.AreEqual(0, segs.Count(a => a.kind == ActivityKind.Walk && a.prop == Prop.Buckets), "nobody walks out with a full bucket");
        }

        [Test]
        public void TheDawnRunInOrder()
        {
            var sim = Camp(1, 60);
            var planner = LifePlanner.Wired(sim);
            var day = new List<PersonActivity>();
            int runs = 0;
            for (int d = 0; d < Days; d++)
                foreach (var who in planner.People)
                {
                    planner.DaySchedule(who, d, day);
                    for (int i = 1; i + 1 < day.Count; i++)
                    {
                        if (day[i].kind != ActivityKind.FetchWater || day[i].moving) continue;
                        Assert.AreEqual((ActivityKind.Walk, Prop.EmptyBucket), (day[i - 1].kind, day[i - 1].prop));
                        Assert.AreEqual((ActivityKind.FetchWater, Prop.Buckets), (day[i + 1].kind, day[i + 1].prop));
                        Assert.AreEqual(day[i].place, day[i - 1].place, "walks to where it draws");
                        Assert.AreEqual(day[i].place, day[i + 1].from, "and back from there");
                        runs++;
                    }
                }
            Assert.Greater(runs, 0);
        }

        [Test]
        public void AFetchedBucketGoesOutEmptyAndComesBackFull()
        {
            var segs = Segs(Camp(1, 60));
            var outs = segs.Where(a => a.kind == ActivityKind.Walk && a.trip == TripType.Errand && a.prop == Prop.EmptyBucket).ToList();
            Assert.Greater(outs.Count, 0, "the camp's cook and the chores fetch water too");
            Assert.IsTrue(segs.Where(a => a.kind == ActivityKind.Carry && a.prop == Prop.EmptyBucket).Count() == 0, "a load is never empty");
            Assert.Greater(segs.Count(a => a.kind == ActivityKind.Carry && a.prop == Prop.Buckets), 0);
        }

        [Test]
        public void TheEmptyBucketRidesLikeTheFullOne()
        {
            foreach (var k in new[] { ActivityKind.Walk, ActivityKind.FetchWater, ActivityKind.Carry })
                Assert.AreEqual(ActivityInfo.SlotOf(Prop.Buckets, k), ActivityInfo.SlotOf(Prop.EmptyBucket, k));
        }

        // ------------------------------------------------------------------ determinism

        [Test]
        public void TheSameWorldPlansTheSameDays()
        {
            var a = Segs(Camp(7, 60), 8);
            var b = Segs(Camp(7, 60), 8);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].kind, b[i].kind);
                Assert.AreEqual(a[i].place, b[i].place);
                Assert.AreEqual(a[i].prop, b[i].prop);
                Assert.AreEqual(a[i].start, b[i].start);
                Assert.AreEqual(a[i].end, b[i].end);
            }
        }

        static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }
}
