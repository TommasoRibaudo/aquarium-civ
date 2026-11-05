using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#311 / #314: what the tweezers take (weeds round the tree, the forest edge) and the creeping vine. Pure sim.</summary>
    public class EcoTweezersTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>A free cell well away from the yard and the hollows, with room for neighbours.</summary>
        static int OpenCell(SimCore sim)
        {
            var land = sim.EcoGround;
            for (int j = 8; j < SiteMap.NZ - 8; j++)
                for (int i = 8; i < SiteMap.NX - 8; i++)
                {
                    int c = SiteMap.Index(i, j);
                    bool ok = true;
                    for (int dj = -3; dj <= 3 && ok; dj++)
                        for (int di = -3; di <= 3; di++)
                        {
                            int n = SiteMap.Index(i + di, j + dj);
                            if (land.InBasin(n) || land.yard[n]) { ok = false; break; }
                        }
                    if (ok) return c;
                }
            Assert.Fail("no open cell");
            return -1;
        }

        static void Set(SimCore sim, int c, EcoCover cv) => sim.State.eco.cover[c] = (byte)cv;
        static float X(int c) => SiteMap.CXc(c);
        static float Z(int c) => SiteMap.CZc(c);

        [Test]
        public void PluckSapling_TakesAWildSeedlingAndLeavesMeadow()
        {
            var sim = World();
            int c = OpenCell(sim);
            Set(sim, c, EcoCover.Seedling);
            sim.State.eco.moisture[c] = 0.5f;
            Assert.IsTrue(sim.EcoCanPluckSapling(c));
            Assert.AreEqual(c, sim.EcoPluckSapling(X(c), Z(c), 0.15f));
            Assert.AreEqual((byte)EcoCover.Grass, sim.State.eco.cover[c]);
            Assert.AreEqual(-1, sim.EcoPluckSapling(X(c), Z(c), 0.15f), "nothing left to pluck");
        }

        [Test]
        public void PluckSapling_TakesAYoungWildTreeToo_AndBareGroundStaysBare()
        {
            var sim = World();
            int c = OpenCell(sim);
            Set(sim, c, EcoCover.Young);
            sim.State.eco.moisture[c] = 0f;
            Assert.AreEqual(c, sim.EcoPluckSapling(X(c), Z(c), 0.15f));
            Assert.AreEqual((byte)EcoCover.Bare, sim.State.eco.cover[c]);
        }

        [Test]
        public void PluckSapling_NeverTheKeepersTree()
        {
            var sim = World();
            int c = OpenCell(sim);
            sim.State.eco.treeCell = c;
            Set(sim, c, EcoCover.Seedling);
            Assert.IsFalse(sim.EcoCanPluckSapling(c));
            Assert.AreEqual(-1, sim.EcoPluckSapling(X(c), Z(c), 0.3f));
            Set(sim, c, EcoCover.Young);
            Assert.AreEqual(-1, sim.EcoPluckSapling(X(c), Z(c), 0.3f));
            Assert.AreEqual((byte)EcoCover.Young, sim.State.eco.cover[c], "still standing");
        }

        [Test]
        public void PluckSapling_NeverAGrownTree_NeverTheHeartOfAWood()
        {
            var sim = World();
            int c = OpenCell(sim);
            int i = SiteMap.I(c), j = SiteMap.J(c);
            Set(sim, c, EcoCover.Tree);
            Assert.AreEqual(-1, sim.EcoPluckSapling(X(c), Z(c), 0.15f), "a grown tree stays");
            // a sapling at the wood's edge (two grown trees by it) comes out; inside the wood (three) it stays
            int s = SiteMap.Index(i + 2, j);
            Set(sim, s, EcoCover.Young);
            Set(sim, SiteMap.Index(i + 3, j), EcoCover.Tree);
            Set(sim, SiteMap.Index(i + 1, j), EcoCover.Tree);
            Assert.IsTrue(sim.EcoCanPluckSapling(s), "two grown neighbours: the forest's edge");
            Set(sim, SiteMap.Index(i + 2, j + 1), EcoCover.Tree);
            Assert.IsFalse(sim.EcoCanPluckSapling(s), "three grown neighbours: the heart");
            Assert.AreEqual(-1, sim.EcoPluckSapling(X(s), Z(s), 0.15f));
            Assert.AreEqual((byte)EcoCover.Young, sim.State.eco.cover[s]);
        }

        [Test]
        public void PluckSapling_OnlyWithinReach_AndTheNearestFirst()
        {
            var sim = World();
            int c = OpenCell(sim);
            int far = SiteMap.Index(SiteMap.I(c) + 3, SiteMap.J(c));
            Set(sim, c, EcoCover.Seedling); Set(sim, far, EcoCover.Seedling);
            Assert.AreEqual(-1, sim.EcoPluckSapling(X(c), Z(c) + 0.5f, 0.1f), "out of reach");
            Assert.AreEqual(far, sim.EcoPluckSapling(X(far) - 0.02f, Z(far), 0.2f), "the one nearest the finger");
        }

        [Test]
        public void CreepEdge_PlacesPluckableSeedlings_AndThePluckClearsThem()
        {
            var sim = World();
            sim.DebugTreeStage(1); // a Keeper's tree to creep from
            int n = sim.DebugCreepForestEdge(6);
            Assert.Greater(n, 0);
            var e = sim.State.eco;
            var cells = Enumerable.Range(0, EcoLand.N).Where(c => e.cover[c] == (byte)EcoCover.Seedling && c != e.treeCell).ToList();
            Assert.AreEqual(n, cells.Count(c => sim.EcoCanPluckSapling(c)));
            foreach (var c in cells.ToList()) Assert.AreEqual(c, sim.EcoPluckSapling(X(c), Z(c), 0.05f));
            Assert.AreEqual(0, Enumerable.Range(0, EcoLand.N).Count(c => e.cover[c] == (byte)EcoCover.Seedling && c != e.treeCell));
        }

        [Test]
        public void TreeWeeds_ShowAsThreeTufts_EachPluckTakesOne()
        {
            var sim = World();
            Assert.IsFalse(sim.DebugGrowTreeWeeds(), "no tree, no weeds round it");
            sim.DebugTreeStage(1);
            Assert.AreEqual(0, sim.EcoTreeWeedPieces);
            Assert.IsTrue(sim.DebugGrowTreeWeeds());
            Assert.AreEqual(3, sim.EcoTreeWeedPieces);
            for (int k = 2; k >= 0; k--) { Assert.IsTrue(sim.EcoPluckTreeWeed()); Assert.AreEqual(k, sim.EcoTreeWeedPieces); }
            Assert.IsFalse(sim.EcoPluckTreeWeed());
            Assert.AreEqual(0f, sim.EcoTree.weeds, 1e-4f);
        }

        // ---------------------------------------------------------------- the vine
        /// <summary>A camp with young trees on damp ground: where a vine may start.</summary>
        static SimCore Shaded(int seed = 7)
        {
            var sim = World(seed);
            var e = sim.State.eco;
            e.campGateMonth = 0;
            var land = sim.EcoGround;
            for (int c = 0; c < EcoLand.N; c++) if (!land.InBasin(c)) e.cover[c] = (byte)EcoCover.Grass;
            int mid = OpenCell(sim);
            for (int k = 0; k < 6; k++) e.cover[SiteMap.Index(SiteMap.I(mid) + k * 2, SiteMap.J(mid))] = (byte)EcoCover.Young;
            return sim;
        }

        static void Seasons(SimCore sim, int n)
        {
            for (int k = 0; k < n; k++)
            {
                for (int c = 0; c < EcoLand.N; c++) sim.State.eco.moisture[c] = 0.7f;
                sim.Advance(3.0 / 12);
            }
        }

        [Test]
        public void Vine_DoesNotStartBeforeTheCamp()
        {
            var sim = World(); // a barren land: the camp gate is shut
            Assert.IsTrue(sim.BeforeCamp);
            Seasons(sim, 12);
            Assert.AreEqual(0, sim.EcoVine.Count);
        }

        [Test]
        public void Vine_StartsInDampShade_GrowsInALine_AndIsCapped()
        {
            var sim = Shaded();
            Seasons(sim, 40);
            var v = sim.EcoVine;
            Assert.Greater(v.Count, 0, "a vine came up in the damp shade");
            Assert.LessOrEqual(v.Count, new EcoTuning().vineCap);
            Assert.AreEqual(v.Count, v.Distinct().Count(), "one piece per cell");
            foreach (var c in v) Assert.IsFalse(sim.EcoGround.yard[c] || sim.EcoGround.InBasin(c));
            int linked = v.Count(c => v.Any(o => o != c && System.Math.Abs(SiteMap.I(o) - SiteMap.I(c)) <= 1 && System.Math.Abs(SiteMap.J(o) - SiteMap.J(c)) <= 1));
            Assert.GreaterOrEqual(linked, v.Count / 2, "a strand runs in a line, not a scatter");
        }

        [Test]
        public void Vine_IsSlowerThanTheWeeds()
        {
            var sim = Shaded();
            Seasons(sim, 4); // a game year
            Assert.LessOrEqual(sim.EcoVine.Count * SimCore.VineLoadPerCell, 0.25f, "a year of vine is under a quarter of the overgrowth");
            Assert.Less(sim.EcoVine.Count, 10);
        }

        [Test]
        public void Vine_IsDeterministic()
        {
            var a = Shaded(); var b = Shaded();
            Seasons(a, 20); Seasons(b, 20);
            CollectionAssert.AreEqual(a.EcoVine.ToList(), b.EcoVine.ToList());
        }

        [Test]
        public void Vine_CountsAsOvergrowth_AndPluckingTakesIt()
        {
            var sim = World();
            sim.State.eco.campGateMonth = 0;
            int c = OpenCell(sim);
            float before = sim.MessLevel;
            Assert.Greater(sim.DebugGrowVine(X(c), Z(c), 6), 0);
            int n = sim.EcoVine.Count;
            Assert.AreEqual(n * SimCore.VineLoadPerCell / 3f, sim.MessLevel - before, 1e-4f);
            var first = sim.EcoVine[0];
            Assert.IsTrue(sim.EcoPluckVine(first));
            Assert.AreEqual(n - 1, sim.EcoVine.Count);
            Assert.IsFalse(sim.EcoPluckVine(first));
            foreach (var cell in sim.EcoVine.ToList()) sim.EcoPluckVine(cell);
            Assert.AreEqual(before, sim.MessLevel, 1e-5f);
        }

        [Test]
        public void Vine_SurvivesASaveAndLoad()
        {
            var sim = World();
            int c = OpenCell(sim);
            sim.DebugGrowVine(X(c), Z(c), 5);
            var json = UnityEngine.JsonUtility.ToJson(sim.State);
            var back = UnityEngine.JsonUtility.FromJson<SimState>(json);
            CollectionAssert.AreEqual(sim.State.eco.vine, back.eco.vine);
        }
    }
}
