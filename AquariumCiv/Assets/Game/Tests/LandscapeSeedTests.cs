using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#727 (landscaping.md 8.1, decision 2026-11-14): once the Keeper's tree stands, each earned seed (#726) plants a normal
    /// wild-like seedling on valid open ground. The Keeper's tree stays the only hand-built one and is never replaced; bad ground refuses
    /// and spends nothing; placed trees count for the village and the deer like any tree.</summary>
    public class LandscapeSeedTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A world with the Keeper's tree in by the camp, the ground round it meadow, and n seeds in the pouch.</summary>
        static SimCore WithTree(int seeds, int seed = 7)
        {
            var sim = World(seed);
            var g = sim.EcoGround; var e = sim.State.eco;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ), "the first seed is the Keeper's tree");
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)e.cover[c] == EcoCover.Bare && !g.InBasin(c)) e.cover[c] = (byte)EcoCover.Grass;
            sim.DebugGiveSeeds(seeds);
            return sim;
        }

        /// <summary>A cell that takes an earned seed, at least <paramref name="from"/> from the Keeper's tree.</summary>
        static int GoodCell(SimCore sim, float from = 0.4f, int skip = 0)
        {
            var e = sim.State.eco;
            float tx = SiteMap.CXc(e.treeCell), tz = SiteMap.CZc(e.treeCell);
            for (int c = 0; c < EcoLand.N; c++)
                if (sim.EcoGround.yard[c] == false && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), tx, tz) >= from && sim.TreeCellFor(SiteMap.CXc(c), SiteMap.CZc(c)) == c && skip-- <= 0) return c;
            return -1;
        }

        [Test]
        public void ASecondSeed_PlantsANormalSeedling_AndSpendsOneFromThePouch()
        {
            var sim = WithTree(2);
            var e = sim.State.eco;
            int keeper = e.treeCell;
            Assert.IsFalse(sim.CanPlantTree, "the Keeper's tree stands");
            Assert.IsTrue(sim.CanPlantSeed);
            Assert.IsTrue(sim.CanPlantAnySeed);
            int c = GoodCell(sim);
            Assert.GreaterOrEqual(c, 0);
            Assert.IsTrue(sim.PlantTree(SiteMap.CXc(c), SiteMap.CZc(c)));
            Assert.AreEqual((byte)EcoCover.Seedling, e.cover[c], "a seedling, as a wild one");
            Assert.AreEqual(1, sim.SeedsInPouch);
            Assert.AreEqual(keeper, e.treeCell, "the Keeper's tree stays the Keeper's");
            Assert.AreEqual(1, e.cSeedlings - 1, "counted (the Keeper's own seedling is the other)");
            int d = GoodCell(sim, 0.4f, 8);
            Assert.IsTrue(sim.PlantTree(SiteMap.CXc(d), SiteMap.CZc(d)));
            Assert.AreEqual(0, sim.SeedsInPouch);
            Assert.IsFalse(sim.CanPlantSeed, "the pouch is empty");
            Assert.IsFalse(sim.CanPlantAnySeed);
            Assert.IsFalse(sim.PlantTree(SiteMap.CXc(GoodCell(sim, 0.4f, 16)), SiteMap.CZc(GoodCell(sim, 0.4f, 16))), "no seed, no planting");
        }

        [Test]
        public void TheKeepersTree_IsNeverReplaced_NorPlantedOver()
        {
            var sim = WithTree(3);
            var e = sim.State.eco;
            int keeper = e.treeCell;
            float progress = e.treeProgress;
            int at = sim.TreeCellFor(SiteMap.CXc(keeper), SiteMap.CZc(keeper));
            Assert.AreNotEqual(keeper, at, "a seed pressed on the Keeper's tree goes to a neighbour, never onto it");
            Assert.IsTrue(sim.PlantTree(SiteMap.CXc(keeper), SiteMap.CZc(keeper)));
            Assert.AreEqual(keeper, e.treeCell);
            Assert.AreEqual(progress, e.treeProgress, "its growth is not touched");
            Assert.AreEqual(2, sim.SeedsInPouch);
            Assert.IsFalse(e.cover[keeper] == (byte)EcoCover.Bare);
        }

        [Test]
        public void InvalidGround_Refuses_AndSpendsNothing()
        {
            var sim = WithTree(3);
            var e = sim.State.eco; var g = sim.EcoGround;
            // the hollow's deepest part: nothing within a few cells is valid, so the press is refused
            int hollow = Enumerable.Range(0, EcoLand.N).Where(c => g.InBasin(c)).OrderBy(c => g.height[c]).First();
            Assert.AreEqual(-1, sim.TreeCellFor(SiteMap.CXc(hollow), SiteMap.CZc(hollow)), "the hollow takes no seedling");
            Assert.IsFalse(sim.PlantTree(SiteMap.CXc(hollow), SiteMap.CZc(hollow)));
            // water
            int w = Enumerable.Range(0, EcoLand.N).First(c => g.InBasin(c));
            e.cover[w] = (byte)EcoCover.Water;
            Assert.AreNotEqual(w, sim.TreeCellFor(SiteMap.CXc(w), SiteMap.CZc(w)));
            // outside the world
            Assert.IsFalse(sim.PlantTree(100f, 100f));
            // a bush, a tree, a seedling, the reeds: not open ground
            int good = GoodCell(sim);
            foreach (var cv in new[] { EcoCover.Bush, EcoCover.Tree, EcoCover.Young, EcoCover.Seedling, EcoCover.Reeds })
            {
                byte was = e.cover[good]; e.cover[good] = (byte)cv;
                Assert.AreNotEqual(good, sim.TreeCellFor(SiteMap.CXc(good), SiteMap.CZc(good)), $"{cv} is not open ground");
                e.cover[good] = was;
            }
            // the glass strip (the bed margin): a seed at the very edge goes in, or is refused, never into the margin
            int edge = SiteMap.Index(0, SiteMap.NZ / 2);
            int to = sim.TreeCellFor(SiteMap.CXc(edge), SiteMap.CZc(edge));
            if (to >= 0) Assert.IsTrue(EcoModel.InTreeBed(to), "the edge press lands inside the bed");
            // the ring round the fire
            int fire = SiteMap.CellAt(g.campX + 0.1f, g.campZ);
            int atFire = sim.TreeCellFor(SiteMap.CXc(fire), SiteMap.CZc(fire));
            if (atFire >= 0) Assert.GreaterOrEqual(Terrain.Dist(SiteMap.CXc(atFire), SiteMap.CZc(atFire), g.campX, g.campZ), sim.Config.eco.bushCampMargin, "none in the fire's ring");
            Assert.AreEqual(3, sim.SeedsInPouch, "refusals spent nothing");
        }

        [Test]
        public void AThicketsArea_IsNotSeedGround()
        {
            var sim = WithTree(1);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            var e = sim.State.eco;
            int inside = Enumerable.Range(0, EcoLand.N).First(c => sim.BushThicketZone(c) > 0.5f && (EcoCover)e.cover[c] != EcoCover.Bush);
            int to = sim.TreeCellFor(SiteMap.CXc(inside), SiteMap.CZc(inside));
            Assert.IsTrue(to < 0 || sim.BushThicketZone(to) <= 0f, "the seed lands outside the thicket");
        }

        [Test]
        public void PlacedSeedlings_GrowLikeWildOnes_AndCountAsTrees()
        {
            var sim = WithTree(10);
            var e = sim.State.eco; var g = sim.EcoGround;
            var cells = new System.Collections.Generic.List<int>();
            for (int k = 0; k < 10; k++)
            {
                int c = GoodCell(sim, 0.4f, k * 9);
                Assert.IsTrue(sim.PlantTree(SiteMap.CXc(c), SiteMap.CZc(c)));
                cells.Add(c);
            }
            Assert.AreEqual(0, sim.SeedsInPouch);
            var m = new EcoModel(e, g, sim.Config.eco);
            for (int s = 0; s < 80; s++)
            {
                for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)e.cover[c] != EcoCover.Water) e.moisture[c] = 0.4f; // kept damp, as the pacing lab's patches
                m.Step(s % 4, s * 3);
            }
            int grown = cells.Count(c => (EcoCover)e.cover[c] == EcoCover.Tree);
            Assert.GreaterOrEqual(grown, 4, "placed seedlings grow into trees by the same dice");
            Assert.GreaterOrEqual(e.cTrees, grown, "and the tree count (the village gate, the deer) sees them");
        }

        [Test]
        public void TheFirstSeed_StillMakesTheKeepersTree_WithAFullPouch()
        {
            var sim = World();
            sim.DebugGiveSeeds(3);
            Assert.IsTrue(sim.CanPlantTree);
            var g = sim.EcoGround;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            Assert.GreaterOrEqual(sim.State.eco.treeCell, 0, "the first press is the Keeper's tree");
            Assert.AreEqual(3, sim.SeedsInPouch, "it costs no pouch seed");
            Assert.IsTrue(sim.State.eco.Reached(EcoMilestones.TreePlanted));
        }
    }
}
