using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#730 phase 1 (landscaping.md 5, 8.3): the sim side of the one landscaping tool. It takes seedlings, young trees (the old tweezers'
    /// pluck) and bushes, and grown trees only behind a tuning flag (off); never the Keeper's tree. A plucked thicket bush lowers that
    /// thicket's target and does not grow back; the last one takes the thicket. The tool's name and the grown-tree call are the user's [est].</summary>
    public class PluckToolTests
    {
        static SimCore World(int seed = 7, EcoTuning tune = null)
        {
            var cfg = new SimConfig { ecology = true };
            if (tune != null) cfg.eco = tune;
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static SimCore WithThickets(int more, int seed = 7, EcoTuning tune = null)
        {
            var sim = World(seed, tune);
            var g = sim.EcoGround;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare && !g.InBasin(c)) sim.State.eco.cover[c] = (byte)EcoCover.Grass;
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            for (int i = 0; i < more; i++) Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0);
            return sim;
        }

        static void Seasons(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.DebugSkipSeasons(1); }
        static List<(int cell, float radius, int bushes)> List(SimCore sim) { var l = new List<(int, float, int)>(); sim.BushThickets(l); return l; }
        static float X(int c) => SiteMap.CXc(c);
        static float Z(int c) => SiteMap.CZc(c);

        /// <summary>A grass cell off the yard, the hollow, thickets and the fire, with its neighbours the same: room to stand a plant.</summary>
        static int OpenCell(SimCore sim, int skip = 0)
        {
            var e = sim.State.eco; var g = sim.EcoGround;
            for (int c = 0; c < EcoLand.N; c++)
            {
                if (g.InBasin(c) || g.yard[c] || sim.BushThicketZone(c) > 0f || (EcoCover)e.cover[c] != EcoCover.Grass || c == e.treeCell) continue;
                if (Terrain.Dist(X(c), Z(c), g.campX, g.campZ) < 1.2f) continue;
                int i = SiteMap.I(c), j = SiteMap.J(c);
                if (i < 6 || j < 6 || i > SiteMap.NX - 7 || j > SiteMap.NZ - 7) continue;
                bool ok = true;
                for (int dj = -2; dj <= 2 && ok; dj++) for (int di = -2; di <= 2; di++) { int n = SiteMap.Index(i + di, j + dj); if (g.InBasin(n) || g.yard[n] || (EcoCover)e.cover[n] != EcoCover.Grass) { ok = false; break; } }
                if (ok && skip-- <= 0) return c;
            }
            Assert.Fail("no open cell");
            return -1;
        }

        [Test]
        public void ASeedlingAndAYoungTree_ComeOut_AsTheTweezersPluckDid()
        {
            var sim = WithThickets(0);
            var e = sim.State.eco;
            int a = OpenCell(sim), b = OpenCell(sim, 40);
            e.cover[a] = (byte)EcoCover.Seedling; e.cover[b] = (byte)EcoCover.Young;
            e.moisture[a] = 0.5f; e.moisture[b] = 0f;
            Assert.AreEqual(PluckKind.Sapling, sim.EcoLandscapeKind(a));
            Assert.AreEqual(a, sim.EcoLandscapePluck(X(a), Z(a), 0.1f, out var k));
            Assert.AreEqual(PluckKind.Sapling, k);
            Assert.AreEqual((byte)EcoCover.Grass, e.cover[a], "damp ground goes to meadow");
            Assert.AreEqual(b, sim.EcoLandscapePluck(X(b), Z(b), 0.1f));
            Assert.AreEqual((byte)EcoCover.Bare, e.cover[b], "dry ground stays bare");
            Assert.AreEqual(-1, sim.EcoLandscapePluck(X(a), Z(a), 0.1f), "nothing left");
        }

        [Test]
        public void ABush_ComesOut_AndTheThicketsTargetFallsByOne_AndItDoesNotGrowBack()
        {
            var sim = WithThickets(0);
            Seasons(sim, 60);
            var e = sim.State.eco;
            var th = List(sim)[0];
            Assert.AreEqual(22, th.bushes);
            var bushes = Enumerable.Range(0, EcoLand.N).Where(c => (EcoCover)e.cover[c] == EcoCover.Bush).ToList();
            var three = bushes.Take(3).ToList();
            foreach (int c in three)
            {
                Assert.AreEqual(PluckKind.Bush, sim.EcoLandscapeKind(c));
                Assert.AreEqual(c, sim.EcoLandscapePluck(X(c), Z(c), 0.02f, out var kind));
                Assert.AreEqual(PluckKind.Bush, kind);
                Assert.AreNotEqual((byte)EcoCover.Bush, e.cover[c], "the bush is gone");
            }
            Assert.AreEqual(19, List(sim)[0].bushes);
            Assert.AreEqual(19, sim.BushCap, "the thicket's cap fell by the three");
            Seasons(sim, 40);
            Assert.AreEqual(19, List(sim)[0].bushes, "and it does not grow back");
            Assert.AreEqual(1, sim.BushThicketCount);
            Assert.AreEqual(3, sim.State.eco.thickets[0].trim);
        }

        [Test]
        public void ThePluckedThicketsLossIsItsOwn_TheOthersKeepTheirTargets()
        {
            var sim = WithThickets(1);
            Seasons(sim, 60);
            var e = sim.State.eco;
            var l = List(sim);
            Assert.AreEqual(15, l[0].bushes); Assert.AreEqual(15, l[1].bushes);
            var m = new EcoModel(e, sim.EcoGround, sim.Config.eco);
            int c = Enumerable.Range(0, EcoLand.N).First(x => (EcoCover)e.cover[x] == EcoCover.Bush && m.ThicketAt(x) == 1);
            sim.EcoLandscapePluck(X(c), Z(c), 0.02f);
            Seasons(sim, 30);
            l = List(sim);
            Assert.AreEqual(15, l[0].bushes, "the first thicket is untouched");
            Assert.AreEqual(14, l[1].bushes, "the plucked one holds one fewer");
        }

        [Test]
        public void PluckingAWholeThicketAway_FreesTheRoom_TheOthersGrowBack_AndThePlannerLetsGo()
        {
            var sim = WithThickets(1);
            Seasons(sim, 60);
            var e = sim.State.eco; var map = sim.Settlement.Map;
            var m = new EcoModel(e, sim.EcoGround, sim.Config.eco);
            int cell2 = List(sim)[1].cell;
            Assert.IsTrue(map.Reserved(cell2), "the second thicket's ground is reserved");
            var cells = Enumerable.Range(0, EcoLand.N).Where(c => (EcoCover)e.cover[c] == EcoCover.Bush && m.ThicketAt(c) == 1).ToList();
            Assert.AreEqual(15, cells.Count);
            foreach (int c in cells) Assert.AreEqual(c, sim.EcoLandscapePluck(X(c), Z(c), 0.02f));
            Assert.AreEqual(1, sim.BushThicketCount, "its last bush took the thicket with it");
            Assert.IsFalse(map.Reserved(cell2), "the planner has its ground back");
            Assert.AreEqual(0, sim.BushThicketZone(cell2));
            Seasons(sim, 60);
            var first = List(sim)[0];
            Assert.AreEqual(22, first.bushes, "alone, the first grows back to the full thicket");
            Assert.AreEqual(sim.Config.eco.thicketRadius, first.radius, 1e-4f);
            Assert.IsTrue(sim.LifeIsPlaced(LifeSpecies.Thicket), "the jar stays placed");
        }

        [Test]
        public void ABushOutsideAnyThicket_AnOldSavesStray_StillComesOut()
        {
            var sim = WithThickets(0);
            var e = sim.State.eco;
            int c = OpenCell(sim);
            e.cover[c] = (byte)EcoCover.Bush;
            Assert.AreEqual(-1, sim.State.eco.thickets.Count == 0 ? 0 : new EcoModel(e, sim.EcoGround, sim.Config.eco).ThicketAt(c));
            Assert.AreEqual(c, sim.EcoLandscapePluck(X(c), Z(c), 0.02f));
            Assert.AreEqual(1, sim.BushThicketCount);
            Assert.AreEqual(0, e.thickets[0].trim, "no thicket was trimmed");
        }

        [Test]
        public void TheKeepersTree_IsNeverPlucked_NotEvenGrown_NotEvenWithTheFlag()
        {
            var tune = new EcoTuning { landscapePluckGrown = true };
            var sim = WithThickets(0, 7, tune);
            var e = sim.State.eco;
            int k = e.treeCell;
            foreach (var cv in new[] { EcoCover.Seedling, EcoCover.Young, EcoCover.Tree })
            {
                e.cover[k] = (byte)cv;
                Assert.IsFalse(sim.EcoCanLandscape(k), $"{cv}");
                Assert.AreEqual(-1, sim.EcoLandscapePluck(X(k), Z(k), 0.3f), $"{cv}");
                Assert.AreEqual((byte)cv, e.cover[k]);
            }
        }

        [Test]
        public void AGrownTree_StaysByDefault_AndComesOutWhenTheFlagIsOn()
        {
            Assert.IsTrue(new EcoTuning().landscapePluckGrown, "on by default: the user's call 2026-11-15 (K6)");
            var sim = WithThickets(0, 7, new EcoTuning { landscapePluckGrown = false });
            var e = sim.State.eco;
            int c = OpenCell(sim);
            e.cover[c] = (byte)EcoCover.Tree;
            Assert.IsFalse(sim.EcoLandscapeTakesGrownTrees);
            Assert.AreEqual(-1, sim.EcoLandscapePluck(X(c), Z(c), 0.1f), "a grown tree stays");
            Assert.AreEqual((byte)EcoCover.Tree, e.cover[c]);

            var on = WithThickets(0, 7, new EcoTuning { landscapePluckGrown = true });
            var e2 = on.State.eco;
            int c2 = OpenCell(on);
            e2.cover[c2] = (byte)EcoCover.Tree; e2.moisture[c2] = 0.5f;
            Assert.IsTrue(on.EcoLandscapeTakesGrownTrees);
            Assert.AreEqual(PluckKind.Tree, on.EcoLandscapeKind(c2));
            Assert.AreEqual(c2, on.EcoLandscapePluck(X(c2), Z(c2), 0.1f, out var kind));
            Assert.AreEqual(PluckKind.Tree, kind);
            Assert.AreEqual((byte)EcoCover.Grass, e2.cover[c2]);
            Assert.AreEqual(e2.cTrees, Enumerable.Range(0, EcoLand.N).Count(x => e2.cover[x] == (byte)EcoCover.Tree), "the count follows");
        }

        [Test]
        public void APluckedGrownTree_DoesNotBreakTheCharmTree_TheWoodsOrTheMonth()
        {
            var on = WithThickets(0, 7, new EcoTuning { landscapePluckGrown = true });
            var e = on.State.eco;
            int a = OpenCell(on), b = OpenCell(on, 60);
            e.cover[a] = (byte)EcoCover.Tree; e.cover[b] = (byte)EcoCover.Tree;
            int charm = on.CharmTreeCell();
            Assert.IsTrue(charm == a || charm == b, "the old tree: one of the wild ones");
            Assert.AreEqual(charm, on.EcoLandscapePluck(X(charm), Z(charm), 0.02f));
            int next = on.CharmTreeCell();
            Assert.AreNotEqual(charm, next, "the charm tree is another one now");
            Assert.AreEqual(charm == a ? b : a, next);
            e.cover[next] = (byte)EcoCover.Grass;
            Assert.AreEqual(-1, on.CharmTreeCell(), "no tree: a pole");
            Assert.DoesNotThrow(() => on.Advance(2), "the months run on");
            Assert.AreEqual(-1, on.WoodsTreeCell(X(a), Z(a)), "no woodcutter's tree: none, and no exception");
        }

        [Test]
        public void TheNearestOneWins_WithinReach_AcrossKinds()
        {
            var sim = WithThickets(0);
            var e = sim.State.eco;
            int c = OpenCell(sim);
            int far = SiteMap.Index(SiteMap.I(c) + 3, SiteMap.J(c));
            int bush = SiteMap.Index(SiteMap.I(c) + 1, SiteMap.J(c));
            e.cover[c] = (byte)EcoCover.Seedling; e.cover[far] = (byte)EcoCover.Seedling; e.cover[bush] = (byte)EcoCover.Bush;
            Assert.AreEqual(bush, sim.EcoNearestLandscape(X(bush) - 0.01f, Z(bush), 0.2f), "a bush counts like a seedling");
            Assert.AreEqual(-1, sim.EcoNearestLandscape(X(c), Z(c) + 0.6f, 0.1f), "out of reach");
            Assert.AreEqual(c, sim.EcoNearestLandscape(X(c) - 0.03f, Z(c), 0.05f));
            Assert.AreEqual(e.cover[bush], (byte)EcoCover.Bush, "hovering takes nothing");
        }

        [Test]
        public void TheHeartOfAWood_StaysForTheTool_AsItDidForTheTweezers()
        {
            var sim = WithThickets(0);
            var e = sim.State.eco;
            int c = OpenCell(sim);
            int i = SiteMap.I(c), j = SiteMap.J(c);
            int s = SiteMap.Index(i + 2, j);
            e.cover[s] = (byte)EcoCover.Young;
            foreach (var n in new[] { (3, 0), (1, 0), (2, 1) }) e.cover[SiteMap.Index(i + n.Item1, j + n.Item2)] = (byte)EcoCover.Tree;
            Assert.IsFalse(sim.EcoCanLandscape(s), "three grown neighbours: the forest's own");
        }

        [Test]
        public void TheTweezers_HaveNoSaplingPluckAnyMore()
        {
            Assert.IsNull(typeof(SimCore).GetMethod("EcoPluckSapling"), "the tweezers' sapling pluck moved to the landscaping tool");
            Assert.IsNull(typeof(SimCore).GetMethod("EcoNearestSapling"));
            Assert.IsNull(typeof(SimCore).GetMethod("EcoCanPluckSapling"));
            // what the tweezers still take is untouched
            var sim = WithThickets(0);
            sim.DebugGrowTreeWeeds();
            Assert.IsTrue(sim.EcoPluckTreeWeed());
        }

        [Test]
        public void APluck_CountsAsAPrune_AndWritesOneHistoryLineAMonth()
        {
            var sim = WithThickets(0);
            var e = sim.State.eco;
            int before = sim.KeeperActionsDone(KeeperAction.Prune), lines0 = sim.State.chronicle.Count(x => x.kind == "eco_cleared");
            int a = OpenCell(sim), b = OpenCell(sim, 40);
            e.cover[a] = (byte)EcoCover.Seedling; e.cover[b] = (byte)EcoCover.Seedling;
            sim.EcoLandscapePluck(X(a), Z(a), 0.05f); sim.EcoLandscapePluck(X(b), Z(b), 0.05f);
            Assert.AreEqual(before + 1, sim.KeeperActionsDone(KeeperAction.Prune), "a prune, counted once a month as the tweezers' were");
            Assert.AreEqual(lines0 + 1, sim.State.chronicle.Count(x => x.kind == "eco_cleared"), "one line for the month");
            sim.Advance(sim.Config.StepHours * 2);
            int c = OpenCell(sim, 80); e.cover[c] = (byte)EcoCover.Seedling;
            sim.EcoLandscapePluck(X(c), Z(c), 0.05f);
            Assert.AreEqual(lines0 + 2, sim.State.chronicle.Count(x => x.kind == "eco_cleared"), "and one the next month");
        }

        [Test]
        public void ThePluckAreaCheat_TakesWhatTheToolCould_AndKeepsTheKeepersTree()
        {
            var sim = WithThickets(0);
            Seasons(sim, 40);
            var e = sim.State.eco; var g = sim.EcoGround;
            int c = OpenCell(sim); e.cover[c] = (byte)EcoCover.Seedling;
            int k = e.treeCell;
            var r = sim.DebugLandscapeArea(0f, g.campZ, 20f);
            Assert.GreaterOrEqual(r.saplings, 1);
            Assert.AreEqual(22, r.bushes, "the whole thicket");
            Assert.AreEqual(0, r.trees, "no grown trees by default");
            Assert.AreEqual(0, sim.BushThicketCount);
            Assert.AreNotEqual((byte)EcoCover.Bare, e.cover[k], "the Keeper's tree stands");
        }

        [Test]
        public void TheTrimIsSaved()
        {
            var sim = WithThickets(0);
            Seasons(sim, 60);
            var e = sim.State.eco;
            int c = Enumerable.Range(0, EcoLand.N).First(x => (EcoCover)e.cover[x] == EcoCover.Bush);
            sim.EcoLandscapePluck(X(c), Z(c), 0.02f);
            var loaded = UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State));
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            Assert.AreEqual(1, loaded.eco.thickets[0].trim);
            Seasons(core, 40);
            Assert.AreEqual(21, List(core)[0].bushes);
        }
    }
}
