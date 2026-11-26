using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#729 (the user, 2026-11-14: "the first bush tries to grow a full thicket, if you do more the thickets are gradually smaller,
    /// even shrinking when the player is placing a lot"): with n thickets each holds 22 / sqrt(n) bushes and its disc is x n^-0.25; one above
    /// its target loses one bush a season, the farthest from its centre that nobody is picking, until it fits; never below 3.</summary>
    public class ThicketCrowdingTests
    {
        static SimCore World(int seed = 7, EcoTuning tune = null)
        {
            var cfg = new SimConfig { ecology = true };
            if (tune != null) cfg.eco = tune;
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>The Keeper's tree in, meadow everywhere dry, the first thicket placed by the camp and grown to its full 22.</summary>
        static SimCore OneFullThicket(int seed = 7, EcoTuning tune = null)
        {
            var sim = World(seed, tune);
            var g = sim.EcoGround;
            Assert.IsTrue(sim.PlantTree(g.campX + 0.9f, g.campZ));
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)sim.State.eco.cover[c] == EcoCover.Bare && !g.InBasin(c)) sim.State.eco.cover[c] = (byte)EcoCover.Grass;
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            Seasons(sim, 60);
            return sim;
        }

        static void Seasons(SimCore sim, int n) { for (int i = 0; i < n; i++) sim.DebugSkipSeasons(1); }

        static List<(int cell, float radius, int bushes)> List(SimCore sim)
        {
            var l = new List<(int, float, int)>(); sim.BushThickets(l); return l;
        }

        static int Outside(SimCore sim)
        {
            var e = sim.State.eco; int n = 0;
            for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)e.cover[c] == EcoCover.Bush && sim.BushThicketZone(c) <= 0f) n++;
            return n;
        }

        [Test]
        public void TheTargets_AreFullOverSqrtN_RoundedDown_AndTheRadiusFollows()
        {
            var sim = World();
            var m = new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);
            Assert.AreEqual(22, sim.Config.eco.thicketBushes);
            foreach (var (n, want) in new[] { (1, 22), (2, 15), (3, 12), (4, 11), (9, 7), (12, 6) })
                Assert.AreEqual(want, m.ThicketTargetFor(n), $"n = {n}");
            Assert.AreEqual(0.45f, m.ThicketRadiusFor(1), 1e-5f);
            Assert.AreEqual(0.45f / System.MathF.Sqrt(System.MathF.Sqrt(2f)), m.ThicketRadiusFor(2), 1e-5f);
            Assert.AreEqual(0.45f / System.MathF.Sqrt(2f), m.ThicketRadiusFor(4), 1e-5f);
            Assert.AreEqual(0.45f / System.MathF.Sqrt(3f), m.ThicketRadiusFor(9), 1e-5f);
        }

        [Test]
        public void TheTotalBerriesRiseSlowly_NeverWithTheCount()
        {
            var sim = World();
            var m = new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);
            int prev = 0;
            var line = new List<string>();
            for (int n = 1; n <= 12; n++)
            {
                int total = m.ThicketTargetFor(n) * n;
                line.Add($"{n}:{total}");
                if (n > 1) Assert.That(total - prev, Is.InRange(-4, 9), $"n = {n}: one more thicket moves the total by a few bushes at most (rounding down can cost a few)");
                Assert.Less(total, 22 * n * 0.9f + (n == 1 ? 3 : 0), $"n = {n}: far from n full thickets");
                prev = total;
            }
            TestContext.Out.WriteLine("total bushes by thicket count: " + string.Join(" ", line));
            Assert.AreEqual(22, m.ThicketTargetFor(1));
            Assert.Greater(m.ThicketTargetFor(2) * 2, 22);
            Assert.Greater(m.ThicketTargetFor(4) * 4, m.ThicketTargetFor(2) * 2);
            Assert.Greater(m.ThicketTargetFor(9) * 9, m.ThicketTargetFor(4) * 4);
            Assert.LessOrEqual(m.ThicketTargetFor(12) * 12, 22 * 4, "twelve thickets hold under four times one full one");
        }

        [Test]
        public void ANeverBelowThreeFounders()
        {
            var tune = new EcoTuning { thicketCrowdExp = 2f };
            var sim = World(7, tune);
            var m = new EcoModel(sim.State.eco, sim.EcoGround, tune);
            Assert.AreEqual(22, m.ThicketTargetFor(1));
            Assert.AreEqual(5, m.ThicketTargetFor(2));
            for (int n = 3; n <= 12; n++) Assert.AreEqual(3, m.ThicketTargetFor(n), $"n = {n}: the founders' floor");
        }

        [Test]
        public void ASecondThicket_ShrinksTheFirstByOneABushASeason_AndItStopsAtTheTarget()
        {
            var sim = OneFullThicket();
            Assert.AreEqual(22, List(sim)[0].bushes, "the first thicket tried to grow full");
            float r1 = List(sim)[0].radius;
            sim.DebugGiveBushes(1);
            Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0);
            var seq = new List<int> { List(sim)[0].bushes };
            for (int s = 0; s < 14; s++) { Seasons(sim, 1); seq.Add(List(sim)[0].bushes); }
            TestContext.Out.WriteLine("first thicket, bushes by season after the second was placed: " + string.Join(" ", seq));
            for (int i = 1; i < seq.Count; i++) Assert.GreaterOrEqual(seq[i - 1] - seq[i], 0, "it never grows while over target");
            for (int i = 1; i < seq.Count; i++) Assert.LessOrEqual(seq[i - 1] - seq[i], 1, $"season {i}: never by a lump, at most one bush a season");
            Assert.AreEqual(21, seq[1], "one bush went the first season");
            Assert.AreEqual(15, seq[^1], "and it stopped at 15");
            Assert.AreEqual(15, seq[^3], "for a good while");
            Assert.AreEqual(0, Outside(sim), "no stray bush: the disc closed in behind the shedding");
            Assert.Less(List(sim)[0].radius, r1, "the first thicket's disc is smaller");
            Assert.GreaterOrEqual(List(sim)[0].radius, new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco).ThicketRadiusFor(2) - 1e-5f);
            // the second one grows to 15 as well, and nothing leaves the areas
            Seasons(sim, 40);
            var l = List(sim);
            Assert.AreEqual(15, l[0].bushes); Assert.AreEqual(15, l[1].bushes);
            Assert.AreEqual(30, sim.BushCap);
            Assert.AreEqual(0, Outside(sim));
        }

        [Test]
        public void TheFarthestBushGoesFirst_AndNeverOneThePeopleArePicking()
        {
            var sim = OneFullThicket();
            var e = sim.State.eco;
            var first = List(sim)[0];
            float cx = SiteMap.CXc(first.cell), cz = SiteMap.CZc(first.cell);
            var bushes = Enumerable.Range(0, EcoLand.N).Where(c => (EcoCover)e.cover[c] == EcoCover.Bush && sim.BushThicketZone(c) > 0f)
                .OrderByDescending(c => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), cx, cz)).ThenBy(c => c).ToList();
            int farthest = bushes[0], second = bushes[1];
            // someone picks at the farthest bush this visit: it stays, the next farthest goes
            sim.PickBush(farthest, 100);
            sim.DebugGiveBushes(1);
            Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0);
            Seasons(sim, 1);
            Assert.AreEqual((byte)EcoCover.Bush, e.cover[farthest], "the bush being picked stays");
            Assert.AreNotEqual((byte)EcoCover.Bush, e.cover[second], "the next one out goes instead");
            // and with nobody picking, the farthest goes
            var sim2 = OneFullThicket();
            var e2 = sim2.State.eco;
            var f2 = List(sim2)[0];
            int far2 = Enumerable.Range(0, EcoLand.N).Where(c => (EcoCover)e2.cover[c] == EcoCover.Bush && sim2.BushThicketZone(c) > 0f)
                .OrderByDescending(c => Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(f2.cell), SiteMap.CZc(f2.cell))).ThenBy(c => c).First();
            sim2.DebugGiveBushes(1);
            Assert.GreaterOrEqual(sim2.DebugPlaceThicketNearCamp(), 0);
            Seasons(sim2, 1);
            Assert.AreNotEqual((byte)EcoCover.Bush, e2.cover[far2], "the farthest bush went first");
        }

        [Test]
        public void PlacingALot_ShrinksTheWholeBoxGently_AndNeverUnderThree()
        {
            var sim = OneFullThicket();
            sim.DebugGiveBushes(3);
            for (int i = 0; i < 3; i++) Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0, $"thicket {i + 2}");
            Assert.AreEqual(4, sim.BushThicketCount);
            int worstDrop = 0, last = List(sim)[0].bushes;
            for (int s = 0; s < 14; s++)
            {
                Seasons(sim, 1);
                int now = List(sim)[0].bushes;
                worstDrop = System.Math.Max(worstDrop, last - now); last = now;
                foreach (var th in List(sim)) Assert.GreaterOrEqual(th.bushes, 3, "never under the founders");
            }
            Assert.LessOrEqual(worstDrop, 1, "a bush a season, however many were placed");
            Seasons(sim, 40);
            var l = List(sim);
            foreach (var th in l) Assert.AreEqual(11, th.bushes, "four thickets hold 11 each");
            Assert.AreEqual(44, sim.BushCap);
            Assert.AreEqual(0, Outside(sim));
            var m = new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);
            foreach (var th in l) Assert.AreEqual(m.ThicketRadiusFor(4), th.radius, 0.06f, "each disc is the n = 4 size, give or take what its bushes held");
        }

        [Test]
        public void WhenTheCrowdThins_TheThicketsGrowBack()
        {
            var sim = OneFullThicket();
            var e = sim.State.eco;
            sim.DebugGiveBushes(1);
            Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0);
            Seasons(sim, 40);
            Assert.AreEqual(15, List(sim)[0].bushes);
            // the second thicket goes (the plucking tool, #730, does this to the list; here its record is removed)
            var m = new EcoModel(e, sim.EcoGround, sim.Config.eco);
            int cell2 = List(sim)[1].cell;
            foreach (var c in Enumerable.Range(0, EcoLand.N).Where(c => m.ThicketAt(c) == 1 && (EcoCover)e.cover[c] == EcoCover.Bush)) e.cover[c] = (byte)EcoCover.Grass;
            e.thickets.RemoveAt(1);
            Assert.AreEqual(1, sim.BushThicketCount);
            Seasons(sim, 60);
            var first = List(sim)[0];
            Assert.AreEqual(22, first.bushes, "alone again: it grows back to the full thicket");
            Assert.AreEqual(sim.Config.eco.thicketRadius, first.radius, 1e-4f, "and its disc opens to the full radius");
            Assert.AreEqual(0, Outside(sim));
            Assert.AreEqual(e.thicketA, first.cell);
        }

        [Test]
        public void OneThicket_IsExactlyAsBefore()
        {
            // n = 1: the target is the full 22, the radius is the full 0.45 and nothing is ever shed
            var sim = OneFullThicket();
            Seasons(sim, 120);
            var l = List(sim);
            Assert.AreEqual(1, l.Count);
            Assert.AreEqual(22, l[0].bushes);
            Assert.AreEqual(sim.Config.eco.thicketRadius, l[0].radius, 1e-6f);
            Assert.AreEqual(22, sim.BushCap);
        }

        [Test]
        public void TheShrinkingIsSaved_AMidShrinkSaveFinishesTheJob()
        {
            var sim = OneFullThicket();
            sim.DebugGiveBushes(1);
            Assert.GreaterOrEqual(sim.DebugPlaceThicketNearCamp(), 0);
            Seasons(sim, 3);
            Assert.AreEqual(19, List(sim)[0].bushes);
            var loaded = UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(sim.State));
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            Assert.AreEqual(19, List(core)[0].bushes);
            Seasons(core, 40);
            Assert.AreEqual(15, List(core)[0].bushes);
            Assert.AreEqual(0, Outside(core));
        }
    }
}
