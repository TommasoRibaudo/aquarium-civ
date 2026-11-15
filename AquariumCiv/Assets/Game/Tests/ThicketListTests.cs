using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#728 (landscaping.md 8.2, decision 2026-11-14 "A3 bushes", reverses "exactly one thicket"): the thickets are a list. The
    /// first comes from the thicket jar, each earned bush founds another small one. At most 12, areas never overlap; a v1 save loads as one
    /// thicket; the settlement keeps off every one; the rabbits keep out of every one. (Crowding is ThicketCrowdingTests, #729.)</summary>
    public class ThicketListTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A box with the Keeper's tree down, the first thicket placed by the camp and n more thickets placed from the pouch.</summary>
        static SimCore WithThickets(int more, int seed = 7)
        {
            var sim = World(seed);
            var g = sim.EcoGround;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare && !g.InBasin(c)) sim.State.eco.cover[c] = (byte)EcoCover.Grass; // meadow everywhere dry
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0, "the first thicket");
            for (int i = 0; i < more; i++) Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0, $"thicket {i + 2}");
            return sim;
        }

        static List<(int cell, float radius, int bushes)> List(SimCore sim)
        {
            var l = new List<(int, float, int)>(); sim.BushThickets(l); return l;
        }

        static float Reach(SimCore sim, float radius) => radius * (1f + sim.Config.eco.thicketEdge * 0.5f);

        [Test]
        public void TwoThickets_BothStand_WithTheirFounders_AndApartDiscs()
        {
            var sim = WithThickets(1);
            var l = List(sim);
            Assert.AreEqual(2, sim.BushThicketCount);
            Assert.AreEqual(2, l.Count);
            foreach (var th in l) Assert.GreaterOrEqual(th.bushes, sim.Config.eco.thicketFounders, "each has its founders");
            float d = Terrain.Dist(SiteMap.CXc(l[0].cell), SiteMap.CZc(l[0].cell), SiteMap.CXc(l[1].cell), SiteMap.CZc(l[1].cell));
            Assert.GreaterOrEqual(d, Reach(sim, l[0].radius) + Reach(sim, l[1].radius), "the areas do not overlap");
            Assert.Greater(sim.BushThicketZone(l[0].cell), 0.9f);
            Assert.Greater(sim.BushThicketZone(l[1].cell), 0.9f);
            Assert.AreEqual(l[0].cell, sim.State.eco.thicketA, "thicketA is still the first thicket");
            Assert.AreEqual(l[0].radius, sim.State.eco.thicketRadius);
            Assert.AreEqual(0, sim.BushesInPouch, "the earned bush was spent");
            Assert.AreEqual(2 * 15, sim.BushCap, "#729: two thickets hold 15 each (22 / sqrt 2)");
        }

        [Test]
        public void EachThicket_FillsToItsCap_AndNoBushGrowsOutsideThem()
        {
            var sim = WithThickets(2);
            var e = sim.State.eco;
            for (int s = 0; s < 120; s++) sim.DebugSkipSeasons(1);
            var l = List(sim);
            foreach (var th in l) Assert.AreEqual(sim.BushCap / 3, th.bushes, "each thicket fills to its (crowded) count and stops");
            int outside = 0;
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)e.cover[c] == EcoCover.Bush && sim.BushThicketZone(c) <= 0f) outside++;
            Assert.AreEqual(0, outside, "no bush outside the areas");
        }

        [Test]
        public void PlacingInsideAnExistingDisc_IsRefused_WithAReason_AndSpendsNothing()
        {
            var sim = WithThickets(0);
            sim.DebugGiveBushes(1);
            var first = List(sim)[0];
            Assert.AreEqual("Already a thicket here", sim.ThicketRefusal(first.cell));
            Assert.IsFalse(sim.CanPlaceBush(first.cell));
            Assert.IsFalse(sim.PlaceBush(first.cell));
            // just inside the reach of the edge
            int near = SiteMap.CellAt(SiteMap.CXc(first.cell) + Reach(sim, first.radius) * 0.9f, SiteMap.CZc(first.cell));
            Assert.AreEqual("Already a thicket here", sim.ThicketRefusal(near));
            Assert.IsFalse(sim.PlaceBush(near));
            Assert.AreEqual(1, sim.BushesInPouch, "refusals spent nothing");
            Assert.AreEqual(1, sim.BushThicketCount);
        }

        [Test]
        public void OtherRefusals_HaveTheirOwnReasons()
        {
            var sim = World();
            var g = sim.EcoGround; var e = sim.State.eco;
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)e.cover[c] == EcoCover.Bare && !g.InBasin(c)) e.cover[c] = (byte)EcoCover.Grass;
            int fire = SiteMap.CellAt(g.campX + 0.2f, g.campZ);
            Assert.AreEqual("Too near the fire", sim.ThicketRefusal(fire));
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            int hollow = Enumerable.Range(0, EcoLand.N).First(c => g.InBasin(c));
            Assert.IsNotNull(sim.ThicketRefusal(hollow));
            int far = Enumerable.Range(0, EcoLand.N).First(c => sim.ThicketRefusal(c) == null);
            e.cover[far] = (byte)EcoCover.Tree;
            Assert.AreEqual("Bushes need open meadow", sim.ThicketRefusal(far));
            Assert.AreEqual("Not here", sim.ThicketRefusal(-1));
        }

        [Test]
        public void NeedsTheFirstThicketAndABush()
        {
            var sim = World();
            sim.DebugGiveBushes(1);
            var g = sim.EcoGround;
            Assert.IsFalse(sim.CanPlaceBush(SiteMap.CellAt(g.campX + 1.4f, g.campZ)), "the first thicket is the jar's, not the pouch's");
            var placed = WithThickets(0);
            Assert.IsFalse(placed.CanPlaceBush(Enumerable.Range(0, EcoLand.N).First(c => placed.ThicketRefusal(c) == null)), "no bush in the pouch");
        }

        [Test]
        public void AtMostTwelveThickets()
        {
            var sim = WithThickets(0);
            var cfg = sim.Config.eco;
            cfg.thicketMax = 3; // the cap is a tuning number: three keeps the test on a small box
            sim.DebugGiveBushes(5);
            int placed = 1;
            for (int k = 0; k < 6; k++) if (sim.DebugPlaceThicketNearCamp() >= 0) placed++;
            Assert.AreEqual(3, placed, "the third is the last");
            Assert.AreEqual(3, sim.BushThicketCount);
            int cell = Enumerable.Range(0, EcoLand.N).First(c => !(sim.ThicketRefusal(c) ?? "").StartsWith("Already"));
            Assert.AreEqual("The box holds 3 thickets", sim.ThicketRefusal(cell));
            Assert.AreEqual(12, new EcoTuning().thicketMax, "the real cap is 12 [est]");
        }

        [Test]
        public void ASaveFromBeforeTheList_LoadsAsOneFullThicket()
        {
            var sim = WithThickets(0);
            for (int s = 0; s < 12; s++) sim.DebugSkipSeasons(1);
            var e = sim.State.eco;
            var zones = Enumerable.Range(0, EcoLand.N).Select(c => sim.BushThicketZone(c)).ToArray();
            int cell = e.thicketA; float radius = e.thicketRadius;
            e.thickets = null; // a v1 save has only thicketA / thicketRadius
            string json = JsonUtility.ToJson(sim.State);
            var loaded = JsonUtility.FromJson<SimState>(json);
            loaded.eco.thickets = new List<Thicket>(); // as a save without the list field reads
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            Assert.AreEqual(1, core.BushThicketCount, "one thicket");
            var l = List(core);
            Assert.AreEqual((cell, radius), (l[0].cell, l[0].radius), "at the same cell, at the full radius");
            Assert.AreEqual(core.Config.eco.thicketRadius, l[0].radius);
            for (int c = 0; c < EcoLand.N; c++) Assert.AreEqual(zones[c], core.BushThicketZone(c), 1e-6f, $"zone of cell {c}");
            core.Advance(1);
            Assert.AreEqual(1, core.BushThicketCount);
        }

        [Test]
        public void TheList_RoundTripsInTheSave()
        {
            var sim = WithThickets(2);
            var before = List(sim);
            var loaded = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State));
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            var after = List(core);
            Assert.AreEqual(3, after.Count);
            CollectionAssert.AreEqual(before.Select(t => (t.cell, t.radius, t.bushes)).ToList(), after.Select(t => (t.cell, t.radius, t.bushes)).ToList());
            Assert.AreEqual(before[0].cell, loaded.eco.thicketA);
        }

        [Test]
        public void TheSettlement_ReservesEveryDisc_AndBuildsOnNone()
        {
            var sim = WithThickets(2);
            Assert.IsTrue(sim.DebugOpenCampGate());
            foreach (var t in new[] { BuildingType.Farm, BuildingType.Hut, BuildingType.Hut, BuildingType.Farm, BuildingType.Hut, BuildingType.Lumbermill })
                sim.DebugBuild(t);
            sim.Advance(2);
            sim.Settlement.SyncEco();
            var map = sim.Settlement.Map;
            var l = List(sim);
            int area = 0;
            const byte held = SiteMap.FLaid | SiteMap.FYard | SiteMap.FCommons | SiteMap.FGreen;
            var perDisc = new int[l.Count];
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (sim.BushThicketZone(c) <= 0f) continue;
                area++;
                for (int i = 0; i < l.Count; i++) if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(l[i].cell), SiteMap.CZc(l[i].cell)) < l[i].radius) perDisc[i]++;
                Assert.IsTrue(map.Reserved(c), $"cell {c} in a thicket is reserved");
                Assert.AreEqual(0, map.lot[c], $"cell {c} in a thicket under a lot");
                Assert.Less(map.tier[c], PathNet.Trail, $"cell {c} in a thicket under a path");
                Assert.AreEqual(0, map.flags[c] & held, $"cell {c} in a thicket is a yard, commons, green or laid path");
            }
            foreach (var n in perDisc) Assert.Greater(n, 10, "each disc has ground of its own");
            Assert.Greater(sim.State.settle.lots.Count, 3, "the box did build, round them");
            TestContext.Out.WriteLine($"{area} reserved cells in {l.Count} thickets, {sim.State.settle.lots.Count} lots");
        }

        [Test]
        public void TheRabbits_KeepOutOfEveryThicket()
        {
            var sim = WithThickets(2);
            var e = sim.State.eco; var m = new EcoModel(e, sim.EcoGround, sim.Config.eco);
            foreach (var th in List(sim))
            {
                Assert.IsTrue(m.InThicketReach(th.cell), "a thicket's heart is out of their reach");
                Assert.IsFalse(sim.LifeGroundOk(LifeSpecies.Rabbits, th.cell));
                int edge = SiteMap.CellAt(SiteMap.CXc(th.cell) + Reach(sim, th.radius), SiteMap.CZc(th.cell));
                if (edge >= 0) Assert.IsTrue(m.InThicketReach(edge), "the widest reach of the edge is still kept");
            }
            int free = 0;
            for (int c = 0; c < EcoLand.N; c++)
            {
                bool near = List(sim).Any(th => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(th.cell), SiteMap.CZc(th.cell)) < Reach(sim, th.radius) + sim.Config.eco.rabbitThicketMargin - 1e-4f);
                Assert.AreEqual(near, m.InThicketReach(c), $"cell {c}");
                if (!near && sim.LifeGroundOk(LifeSpecies.Rabbits, c)) free++;
            }
            Assert.Greater(free, 100, "the rest of the land stays theirs");
        }

        [Test]
        public void SeedlingsAreChokedInAnyThicket()
        {
            var sim = WithThickets(1);
            var l = List(sim);
            var e = sim.State.eco;
            var m = new EcoModel(e, sim.EcoGround, sim.Config.eco);
            foreach (var th in l) Assert.Greater(m.ThicketZone(th.cell), 0.9f);
            Assert.AreEqual(0f, m.ThicketZone(SiteMap.CellAt(sim.EcoGround.campX, sim.EcoGround.campZ)), "no thicket at the fire");
        }
    }
}
