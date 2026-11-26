using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Life.Tests
{
    /// <summary>#765: two fishermen stood on the same spot at the pond. Every stand at the shore is the n-th person's own: no two fishers (the job,
    /// the evening line, the debug bite) closer than a body width, on a small pond, round a corner, as the pond grows.</summary>
    public class FishersApartTests
    {
        /// <summary>A doll is about 0.1 u wide (SlotSpots.Gap is 0.12): nearer than this and two dolls stand inside each other.</summary>
        const float BodyWidth = 0.1f;

        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static int[] BasinByHeight(SimCore sim) => sim.EcoGround.main.cells.OrderBy(c => sim.EcoGround.height[c]).ThenBy(c => c).ToArray();

        static void Fill(SimCore sim, int[] order, int n)
        {
            for (int c = 0; c < EcoLand.N; c++) if (sim.State.eco.cover[c] == (byte)EcoCover.Water) sim.State.eco.cover[c] = (byte)EcoCover.Bare;
            for (int i = 0; i < n && i < order.Length; i++) sim.State.eco.cover[order[i]] = (byte)EcoCover.Water;
        }

        static List<(float x, float z)> Stands(SimCore sim, float fromX, float fromZ, int fishers)
        {
            var list = new List<(float x, float z)>();
            for (int slot = 0; slot < fishers; slot++)
            {
                Assert.IsTrue(PondShore.NearestShore(sim, fromX, fromZ, slot, out float x, out float z, out float fx, out float fz), "slot " + slot);
                Assert.IsFalse(PondShore.IsWet(sim, x, z), $"slot {slot}: dry");
                Assert.IsTrue(PondShore.IsWet(sim, fx, fz), $"slot {slot}: facing the water");
                list.Add((x, z));
            }
            return list;
        }

        static void AssertApart(List<(float x, float z)> at, string what)
        {
            for (int i = 0; i < at.Count; i++)
                for (int j = i + 1; j < at.Count; j++)
                    Assert.GreaterOrEqual(Terrain.Dist(at[i].x, at[i].z, at[j].x, at[j].z), BodyWidth, $"{what}: fishers {i} and {j} at ({at[i].x:0.00},{at[i].z:0.00}) and ({at[j].x:0.00},{at[j].z:0.00})");
        }

        [Test]
        public void TwoAndThreeFishers_NeverShareASpot_OnAnySizeOfPond()
        {
            var sim = World();
            var order = BasinByHeight(sim);
            Assert.Greater(order.Length, 30, "a basin to fill");
            foreach (int n in new[] { 1, 2, 3, 5, 8, 15, 30, order.Length })
            {
                Fill(sim, order, n);
                foreach (float[] from in new[] { new[] { sim.EcoGround.campX, sim.EcoGround.campZ }, new[] { 0f, 0.8f }, new[] { -2f, 2f }, new[] { 2f, -0.5f } })
                {
                    AssertApart(Stands(sim, from[0], from[1], 2), $"n={n} two");
                    AssertApart(Stands(sim, from[0], from[1], 3), $"n={n} three");
                }
            }
        }

        [Test]
        public void ASmallPond_GivesFourFishersASpotEach()
        {
            var sim = World();
            var order = BasinByHeight(sim);
            foreach (int n in new[] { 1, 2, 3 })
            {
                Fill(sim, order, n);
                AssertApart(Stands(sim, sim.EcoGround.campX, sim.EcoGround.campZ, 4), $"n={n} four");
            }
        }

        [Test]
        public void TheStandsAreStable_AndSlotZeroIsTheNearestShore()
        {
            var sim = World();
            var order = BasinByHeight(sim);
            Fill(sim, order, 12);
            var a = Stands(sim, sim.EcoGround.campX, sim.EcoGround.campZ, 3);
            var b = Stands(sim, sim.EcoGround.campX, sim.EcoGround.campZ, 3);
            CollectionAssert.AreEqual(a, b, "asked again, the same spots");
            PondShore.NearestShore(sim, sim.EcoGround.campX, sim.EcoGround.campZ, 0, out float x, out float z, out _, out _);
            Assert.AreEqual((x, z), a[0]);
        }

        // ------------------------------------------------------------ the planner: everyone who is sent to fish
        static void Block(SimCore sim, int w, int h, float far)
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
                    for (int dj = 0; dj < h; dj++) for (int di = 0; di < w; di++) e.cover[SiteMap.Index(i + di, j + dj)] = (byte)EcoCover.Water;
                    new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).Count();
                    return;
                }
            Assert.Fail("no room");
        }

        /// <summary>A pond of w x h cells with the fish placed: placed in a 6 x 6 pond (the placing needs room), then dried down to the corner w x h.</summary>
        static SimCore FishingWorld(int w, int h)
        {
            var sim = World();
            Block(sim, 6, 6, 1.2f);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Ducks), 0);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Fish), 0);
            var e = sim.State.eco;
            int minI = int.MaxValue, minJ = int.MaxValue;
            for (int c = 0; c < EcoLand.N; c++) if (e.cover[c] == (byte)EcoCover.Water) { minI = System.Math.Min(minI, c % SiteMap.NX); minJ = System.Math.Min(minJ, c / SiteMap.NX); }
            for (int c = 0; c < EcoLand.N; c++)
                if (e.cover[c] == (byte)EcoCover.Water && !(c % SiteMap.NX - minI < w && c / SiteMap.NX - minJ < h)) e.cover[c] = (byte)EcoCover.Bare;
            new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).Count();
            Assert.IsTrue(sim.FishingWater, "a pond with fish");
            return sim;
        }

        /// <summary>Every fisher at the pond in this plan, where the view would put them (the slot the planner numbered): none within a body width.</summary>
        static int CheckFishers(SimCore sim, LifePlanner planner, ViewTime t, string what)
        {
            var acts = new List<PersonActivity>();
            planner.PlanAll(t, acts);
            Assert.IsTrue(planner.Positions.TryGetPosition(Place.Land(PlaceKind.Pond), out float px, out float pz), "the pond has a spot");
            var at = new List<(float x, float z)>();
            foreach (var a in acts)
            {
                if (a.kind != ActivityKind.Fish || a.moving || a.enRoute || a.place.kind != PlaceKind.Pond) continue;
                Assert.IsTrue(PondShore.NearestShore(sim, px, pz, a.slot, out float x, out float z, out _, out _), what);
                at.Add((x, z));
            }
            if (at.Count > 1) AssertApart(at, what + " day " + t.day + " h " + t.hour);
            return at.Count;
        }

        [Test]
        public void TheDebugBite_ThreeFishers_EachOnTheirOwnSpot_SmallPondAndBig()
        {
            foreach (var size in new[] { (1, 1), (2, 2), (6, 6) })
            {
                var sim = FishingWorld(size.Item1, size.Item2);
                var planner = LifePlanner.Wired(sim);
                int sent = planner.DebugFishNow(new ViewTime(2, 10f), 3, 2f);
                Assert.GreaterOrEqual(sent, 2, "grown-ups sent to fish");
                int most = 0;
                for (float h = 10.5f; h < 11.9f; h += 0.25f) most = System.Math.Max(most, CheckFishers(sim, planner, new ViewTime(2, h), $"pond {size}"));
                Assert.GreaterOrEqual(most, 2, $"pond {size}: at least two fish at once, so the check bit");
            }
        }

        [Test]
        public void TheJobAndTheEveningLine_NobodyStandsOnAnother()
        {
            var sim = FishingWorld(2, 2);
            var fishers = sim.State.notables.Where(x => x.alive && x.age >= 16f && x.age < sim.Config.retireAge).Take(4).ToList();
            foreach (var n in fishers) sim.DebugSetJob(n, JobType.Fisher);
            var planner = LifePlanner.Wired(sim);
            int together = 0, seen = 0;
            for (long day = 3; day < 14; day++)
                for (float h = 6f; h < 22.5f; h += 0.5f)
                {
                    int n = CheckFishers(sim, planner, new ViewTime(day, h), "job/evening");
                    if (n >= 2) together++;
                    if (n >= 1) seen++;
                }
            TestContext.Out.WriteLine($"{seen} fishing moments, {together} with two or more");
            Assert.Greater(seen, 0, "the job fishes");
        }

        [Test]
        public void ThePondMoves_TheFishersFollowAndStayApart()
        {
            var sim = FishingWorld(1, 1);
            var order = BasinByHeight(sim);
            var planner = LifePlanner.Wired(sim);
            planner.DebugFishNow(new ViewTime(2, 10f), 3, 6f);
            foreach (int n in new[] { 2, 6, 14, 40, order.Length })
            {
                Fill(sim, order, n);
                planner.Invalidate();
                CheckFishers(sim, planner, new ViewTime(2, 11f), $"grown to {n}");
            }
        }
    }
}
