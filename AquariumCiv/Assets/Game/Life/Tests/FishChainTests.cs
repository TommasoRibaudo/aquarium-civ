using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#543: the fish chain. A catch goes into the fish basket (not the berry basket), the carry takes it to the camp's fish rack
    /// and a HangFish beat hangs it, it dries over a view day, the next working morning the rack-keeper takes the dry ones down and carries
    /// them to the food pile, and the pile counts and shows them (CampStash by kind). Saved with the ecology state; old saves load.</summary>
    public class FishChainTests
    {
        // ---------------------------------------------------------------- the sim's counts
        static SimCore Eco(int seed = 5)
        {
            var cfg = new SimConfig { ecology = true, settle = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        [Test]
        public void ACatchGoesBasketRackPile_DryingTakesADay()
        {
            var sim = Eco();
            Assert.IsTrue(sim.FishToBasket()); Assert.IsTrue(sim.FishToBasket()); Assert.IsTrue(sim.FishToBasket());
            Assert.AreEqual(3, sim.FishInBasket);
            Assert.AreEqual(0, sim.FishHang(0, 4), "nothing to hang");
            Assert.AreEqual(2, sim.FishHang(2, 4), "two of the three");
            Assert.AreEqual((1, 2, 0), (sim.FishInBasket, sim.FishOnRack, sim.FishDryOn(4)), "hung today: fresh");
            Assert.AreEqual(0, sim.FishToPile(5, 4), "fresh fish do not go to the pile");
            Assert.AreEqual(2, sim.FishDryOn(5), "dry the next day");
            Assert.AreEqual(1, sim.FishHang(1, 5), "another fish hung on day 5");
            Assert.AreEqual((0, 3, 2), (sim.FishInBasket, sim.FishOnRack, sim.FishDryOn(5)), "the new one is fresh, the first two dry");
            Assert.AreEqual(2, sim.FishToPile(9, 5), "only the dry ones leave");
            Assert.AreEqual((1, 2), (sim.FishOnRack, sim.FishInPile));
            Assert.AreEqual(1, sim.FishDryOn(6));
            Assert.AreEqual(1, sim.FishToPile(1, 6));
            Assert.AreEqual((0, 3), (sim.FishOnRack, sim.FishInPile));
        }

        [Test]
        public void TheCountsHaveRoomLimits_AndTheCampEatsFromThePile()
        {
            var sim = Eco();
            for (int i = 0; i < 40; i++) sim.FishToBasket();
            Assert.AreEqual(SimCore.FishBasketMax, sim.FishInBasket);
            Assert.AreEqual(SimCore.FishRackMax, sim.FishHang(99, 1));
            sim.DebugFillFishBasket(5);
            Assert.AreEqual(0, sim.FishHang(5, 1), "the rack is full");
            sim.DebugFishToPile(99);
            Assert.AreEqual(SimCore.FishPileMax, sim.FishInPile);
            Assert.AreEqual(0, sim.FishToPile(1, 3), "the pile is full");
            Assert.AreEqual(2, sim.FishDayTurn(10, 6), "a fish for every three");
            Assert.AreEqual(0, sim.FishDayTurn(10, 6), "once a day");
            Assert.AreEqual(SimCore.FishPileMax - 2, sim.FishInPile);
            sim.DebugFishToPile(0);
            Assert.AreEqual(0, sim.FishDayTurn(11, 6));
        }

        [Test]
        public void SettlingTwiceMovesNothingMore()
        {
            var sim = Eco();
            sim.DebugFillFishBasket(4);
            Assert.AreEqual(4, sim.FishHang(4, 2));
            Assert.AreEqual(0, sim.FishHang(4, 2));
            Assert.AreEqual(4, sim.FishToPile(4, 3));
            Assert.AreEqual(0, sim.FishToPile(4, 3));
        }

        [Test]
        public void TheChainIsSaved_AnOldSaveHasNone()
        {
            var sim = Eco();
            sim.DebugFillFishBasket(5); sim.FishHang(3, 7); sim.DebugFishToPile(4);
            var back = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));
            Assert.AreEqual((sim.FishInBasket, sim.FishOnRack, sim.FishInPile, sim.FishRackDay), (back.FishInBasket, back.FishOnRack, back.FishInPile, back.FishRackDay));
            Assert.AreEqual(sim.FishDryOn(8), back.FishDryOn(8));

            string json = JsonUtility.ToJson(Eco().State);
            foreach (var f in new[] { "fishBasket", "fishRackFresh", "fishRackDry", "fishPile", "fishRackDay", "fishEatDay" })
                json = System.Text.RegularExpressions.Regex.Replace(json, "\"" + f + "\":-?\\d+,?", "");
            var old = new SimCore(sim.Config, JsonUtility.FromJson<SimState>(json));
            Assert.AreEqual((0, 0, 0, -1L), (old.FishInBasket, old.FishOnRack, old.FishInPile, old.FishRackDay));
        }

        [Test]
        public void WithoutTheEcologyNothingHappens()
        {
            var cfg = new SimConfig();
            var off = new SimCore(cfg, SimCore.NewWorld(5, cfg));
            Assert.IsFalse(off.FishToBasket());
            Assert.AreEqual(0, off.FishHang(3, 1)); Assert.AreEqual(0, off.FishToPile(3, 2)); Assert.AreEqual(0, off.FishDayTurn(1, 6));
            Assert.AreEqual(0, off.DebugFishToPile(5));
        }

        // ---------------------------------------------------------------- the pile shows fish
        [Test]
        public void ThePileShowsFishBesideTheBerries()
        {
            Assert.AreEqual(0, CampStash.FishShown(0));
            Assert.AreEqual(1, CampStash.FishShown(1));
            Assert.AreEqual(4, CampStash.FishShown(4));
            Assert.AreEqual(CampStash.MaxFish, CampStash.FishShown(SimCore.FishPileMax));
            int last = 0;
            for (int i = 0; i <= SimCore.FishPileMax; i++)
            {
                int s = CampStash.FishShown(i);
                Assert.GreaterOrEqual(s, last, "never fewer with more fish");
                Assert.LessOrEqual(s, CampStash.MaxFish);
                last = s;
            }
            var food = CampStash.StockOf(150f, 0f);
            Assert.AreEqual(0, food.fish, "berries and fish are counted apart");
            Assert.AreEqual(CampStash.MaxBaskets, food.baskets);
        }

        static float Gap(float a, float b)
        {
            float d = System.Math.Abs(a - b) % 6.2831853f;
            return d > 3.1415927f ? 6.2831853f - d : d;
        }

        [Test]
        public void TheRackStandsInAFreeGap_AwayFromTheHideRack()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                float a = FishRackSpot.AngleFor(seed), h = FishRackSpot.HideAngle(seed);
                Assert.GreaterOrEqual(Gap(a, h), 1.0f, $"seed {seed}: the two racks are well apart round the fire (each is 0.7 rad wide)");
                foreach (float seat in new[] { 0.4f, 2.5f, 4.6f, CampStash.Angle })
                    Assert.GreaterOrEqual(Gap(a, seat), 0.8f, $"seed {seed}: clear of the seat or the stash at {seat}");
            }
        }

        [Test]
        public void TheHideRackAngleIsThePlannersOwn()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var cfg = new SimConfig { ecology = true };
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                var planner = LifePlanner.Wired(sim);
                Assert.IsTrue(planner.HideFrame(out float x, out float z, out _, out _));
                Assert.IsTrue(planner.FirePoint(out float fx, out float fz));
                float a = (float)System.Math.Atan2(z - fz, x - fx); if (a < 0f) a += 6.2831853f;
                Assert.AreEqual(0f, Gap(FishRackSpot.HideAngle(sim.State.seed), a), 1e-3f, "seed " + seed);
            }
        }

        // ---------------------------------------------------------------- the planner's chain
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
                                && AquariumCiv.Sim.Settle.Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), g.campX, g.campZ) >= far;
                        }
                    if (!ok) continue;
                    for (int dj = 0; dj < h; dj++) for (int di = 0; di < w; di++) e.cover[SiteMap.Index(i + di, j + dj)] = (byte)cv;
                    new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).Count();
                    return;
                }
            Assert.Fail("no room");
        }

        /// <summary>A living land with a pond and the fish placed, two grown-ups fishing.</summary>
        static SimCore FishingCamp(int seed = 5)
        {
            var sim = Eco(seed);
            Block(sim, 6, 6, 1.2f, EcoCover.Water);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Ducks), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Fish), 0);
            foreach (var n in sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge).Take(2)) sim.DebugSetJob(n, JobType.Fisher);
            Assert.IsTrue(sim.FishingWater, "open water and fish");
            return sim;
        }

        static List<PersonActivity> Day(LifePlanner planner, PersonRef who, long day)
        {
            var l = new List<PersonActivity>();
            planner.DaySchedule(who, day, l);
            return l;
        }

        static float DistFromFire(LifePlanner planner, in LifePlanner.FishRackLook look)
        {
            Assert.IsTrue(planner.FirePoint(out float fx, out float fz));
            return (float)System.Math.Sqrt((look.x - fx) * (look.x - fx) + (look.z - fz) * (look.z - fz));
        }

        [Test]
        public void AFishersCatchIsCarriedToTheRackAndHungThere_NotTippedOnThePile()
        {
            var sim = FishingCamp();
            var planner = LifePlanner.Wired(sim);
            int hangs = 0, carries = 0;
            for (long d = 0; d < 40; d++)
                foreach (var who in planner.People)
                {
                    var day = Day(planner, who, d);
                    for (int i = 0; i < day.Count; i++)
                    {
                        var a = day[i];
                        if (a.prop != Prop.FishBasket || a.kind != ActivityKind.Carry || !a.moving) continue;
                        carries++;
                        Assert.AreEqual(PlaceKind.FishRack, a.place.kind, "the fish basket goes to the rack");
                        Assert.Less(i + 1, day.Count);
                        var hang = day[i + 1];
                        Assert.AreEqual(ActivityKind.HangFish, hang.kind, $"{who} at {a.end:0.00}");
                        Assert.AreEqual(PlaceKind.FishRack, hang.place.kind);
                        Assert.IsFalse(hang.moving, "standing at the rack");
                        Assert.AreEqual(a.end, hang.start, 1e-3f, "right after the carry");
                        float secs = LifeClock.RealSecondsBetween(hang.start, hang.end);
                        Assert.That(secs, Is.InRange(2.5f, 4f), "a beat, not a pause");
                        hangs++;
                    }
                }
            TestContext.WriteLine($"{carries} fish-basket carries, {hangs} hangs in 40 days");
            Assert.Greater(carries, 0, "fishers carry their catch home");
        }

        [Test]
        public void TheWholeChain_CatchHangDryTakeDownPile()
        {
            var sim = FishingCamp(5);
            var planner = LifePlanner.Wired(sim);
            Assert.IsTrue(planner.People.Count > 3);
            long hungOn = -1, pileOn = -1; int maxRack = 0, maxPile = 0, takeDowns = 0, hangsSeen = 0;
            for (long d = 0; d < 30 && pileOn < 0; d++)
            {
                // the day's catches, as the view counts them: one per fisher an hour at the pond
                for (float h = 5f; h < 21f; h += 0.1f)
                {
                    var now = new ViewTime(d, h);
                    if ((int)(h * 10 + 0.5f) % 10 == 0)
                        foreach (var who in planner.People)
                            if (planner.Plan(who, now).kind == ActivityKind.Fish) planner.FishCaught(who.Key);
                    planner.ResolveFishChain(now);
                    maxRack = System.Math.Max(maxRack, sim.FishOnRack); maxPile = System.Math.Max(maxPile, sim.FishInPile);
                    if (hungOn < 0 && sim.FishOnRack > 0) hungOn = d;
                    if (pileOn < 0 && sim.FishInPile > 0) pileOn = d;
                }
                foreach (var who in planner.People)
                {
                    var day = Day(planner, who, d);
                    hangsSeen += day.Count(a => a.kind == ActivityKind.HangFish);
                    takeDowns += day.Count(a => a.kind == ActivityKind.TakeDownFish);
                }
            }
            TestContext.WriteLine($"hung on day {hungOn}, on the pile on day {pileOn}; rack max {maxRack}, pile max {maxPile}; {hangsSeen} hang beats, {takeDowns} take-downs");
            Assert.GreaterOrEqual(hungOn, 0, "fish got onto the rack");
            Assert.Greater(maxRack, 0);
            Assert.Greater(pileOn, hungOn, "dried fish reach the pile on a later day, never the same one");
            Assert.Greater(takeDowns, 0, "someone took them down");
            Assert.Greater(maxPile, 0);
        }

        [Test]
        public void TheRacksLookCountsHangsAndTakeDowns()
        {
            var sim = FishingCamp(5);
            var planner = LifePlanner.Wired(sim);
            var now = new ViewTime(2, 10f);
            Assert.IsTrue(planner.TryGetFishRack(now, out var look));
            Assert.AreEqual(0, look.Fish);
            Assert.AreEqual(FishRackSpot.Distance, DistFromFire(planner, look), 1e-3f);
            Assert.AreEqual(1.0, look.dirX * look.dirX + look.dirZ * look.dirZ, 1e-3);

            // "fill fish basket": six fish, someone walks them over and hangs them one by one
            Assert.AreEqual(6, planner.DebugFillBasketNow(now));
            int seen = 0, last = 0;
            for (float h = 10f; h < 11.5f; h += 0.01f)
            {
                var t = new ViewTime(2, h);
                planner.ResolveFishChain(t);
                Assert.IsTrue(planner.TryGetFishRack(t, out look));
                Assert.GreaterOrEqual(look.Fish, last, "the fish go up, never down");
                Assert.LessOrEqual(look.Fish - last, 2, "one by one");
                seen = System.Math.Max(seen, look.Fish); last = look.Fish;
            }
            Assert.AreEqual(6, seen, "all six hang at the end");
            Assert.AreEqual(0, sim.FishInBasket);
            Assert.AreEqual(6, sim.FishOnRack);
            Assert.AreEqual(0, look.dry, "fresh the day they are hung");

            // "fish on the rack": dry fish, someone takes them down and carries them to the pile
            Assert.AreEqual(6, planner.DebugRackNow(new ViewTime(3, 10f)));
            var t0 = new ViewTime(3, 10f);
            Assert.AreEqual(6, planner.TryGetFishRack(t0, out look) ? look.dry : -1, "all dry on the rack");
            int low = 6;
            for (float h = 10f; h < 12f; h += 0.01f)
            {
                var t = new ViewTime(3, h);
                planner.ResolveFishChain(t);
                planner.TryGetFishRack(t, out look);
                Assert.LessOrEqual(look.Fish, low, "they come down, never up");
                low = look.Fish;
            }
            Assert.AreEqual(0, low);
            Assert.AreEqual(0, sim.FishOnRack);
            Assert.AreEqual(6, sim.FishInPile);
        }

        [Test]
        public void ThePilesStockCarriesTheFish()
        {
            var sim = FishingCamp(5);
            var planner = LifePlanner.Wired(sim);
            sim.DebugFishToPile(5);
            Assert.IsTrue(planner.TryGetStash(out var look));
            Assert.AreEqual(5, look.stock.fish);
            sim.DebugFishToPile(0);
            Assert.IsTrue(planner.TryGetStash(out look));
            Assert.AreEqual(0, look.stock.fish);
        }

        [Test]
        public void WithoutTheLivingLand_TheOldFishStickStillGoesToTheStash()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(5, cfg));
            var planner = LifePlanner.Wired(sim);
            Assert.IsFalse(planner.TryGetFishRack(new ViewTime(1, 9f), out _));
            Assert.AreEqual(0, planner.ResolveFishChain(new ViewTime(1, 9f)));
            Assert.AreEqual(-1, planner.DebugFillBasketNow(new ViewTime(1, 9f)));
            Assert.IsFalse(planner.FishCaught(1));
            for (long d = 0; d < 20; d++)
                foreach (var who in planner.People)
                    foreach (var a in Day(planner, who, d))
                    {
                        Assert.AreNotEqual(ActivityKind.HangFish, a.kind);
                        Assert.AreNotEqual(PlaceKind.FishRack, a.place.kind);
                        Assert.AreNotEqual(Prop.FishBasket, a.prop);
                    }
        }
    }
}
