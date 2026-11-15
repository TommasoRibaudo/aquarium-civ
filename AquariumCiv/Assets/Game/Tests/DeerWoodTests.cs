using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;

namespace AquariumCiv.Tests
{
    /// <summary>#690 (playtest, feedback#80): the deer come to a wood, not to the first few trees. The jar reads grown trees that stand in
    /// company (<see cref="EcoModel.WoodTrees"/>), so trees scattered over the box never open it and a clump does.</summary>
    public class DeerWoodTests
    {
        static SimCore RungsPlaced()
        {
            var sim = LifeJarTests.World();
            var m = Model(sim);
            foreach (var s in new[] { LifeSpecies.Thicket, LifeSpecies.Rabbits })
            { var j = m.Jar(s); j.state = (int)JarState.Placed; j.placings = 1; j.placedMonth = 0; }
            return sim;
        }

        static EcoModel Model(SimCore sim) => new EcoModel(sim.State.eco, sim.EcoGround, sim.Config.eco);

        /// <summary>Grown trees on every <paramref name="gap"/>th dry cell in both directions: <paramref name="n"/> of them, none within
        /// reach of another.</summary>
        static int Scatter(SimCore sim, int n, int gap)
        {
            var g = sim.EcoGround; var e = sim.State.eco; int put = 0;
            for (int j = 3; j < SiteMap.NZ - 3 && put < n; j += gap)
                for (int i = 3; i < SiteMap.NX - 3 && put < n; i += gap)
                {
                    int c = SiteMap.Index(i, j);
                    if (g.InBasin(c) || g.yard[c] || (EcoCover)e.cover[c] != EcoCover.Bare) continue;
                    e.cover[c] = (byte)EcoCover.Tree; put++;
                }
            Model(sim).Count();
            return put;
        }

        [Test]
        public void ScatteredTrees_KeepTheJarShut_HoweverManyStand()
        {
            var sim = RungsPlaced(); var m = Model(sim);
            int reach = sim.Config.eco.jarWoodReach;
            int put = Scatter(sim, 60, reach + 2); // none within reach of another
            Assert.GreaterOrEqual(put, 25, "the box holds a good many scattered trees: " + put);
            Assert.AreEqual(put, sim.State.eco.cTrees);
            Assert.GreaterOrEqual(sim.State.eco.cTrees, 5 * 5, "many times the old gate of 5");
            Assert.AreEqual(0, m.WoodTrees());
            m.UpdateJars(3);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Deer));
            StringAssert.StartsWith("Deer need a thicker wood", m.JarNeed(LifeSpecies.Deer, 3));
        }

        [Test]
        public void AFewTreesTogether_AreStillNotAWood()
        {
            var sim = RungsPlaced(); var m = Model(sim);
            int want = sim.Config.eco.jarDeerWood;
            PlacedLifeTests.Wood(sim, want - 1); // a block one tree short
            Assert.Less(m.WoodTrees(), want);
            m.UpdateJars(3);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Deer));
            Assert.AreEqual($"Deer need a thicker wood ({m.WoodTrees()}/{want})", m.JarNeed(LifeSpecies.Deer, 3));
        }

        [Test]
        public void ADenseWood_OpensTheJar_AndTheNeedClears()
        {
            var sim = RungsPlaced(); var m = Model(sim);
            int want = sim.Config.eco.jarDeerWood;
            PlacedLifeTests.Wood(sim, want + 4);
            Assert.GreaterOrEqual(m.WoodTrees(), want);
            m.UpdateJars(3);
            Assert.AreEqual(JarState.Ready, m.JarStateOf(LifeSpecies.Deer));
            Assert.IsNull(m.JarNeed(LifeSpecies.Deer, 3));
        }

        [Test]
        public void ScatteredTreesBesideAWood_DoNotCountForIt()
        {
            var sim = RungsPlaced(); var m = Model(sim);
            int want = sim.Config.eco.jarDeerWood;
            PlacedLifeTests.Wood(sim, want - 2);
            int before = m.WoodTrees();
            Scatter(sim, 40, sim.Config.eco.jarWoodReach + 2);
            // lone trees may land beside the block and join it; far ones add nothing, so the count never rises by more than the block's rim
            Assert.Less(before, want);
            Assert.Less(m.WoodTrees(), want + 3);
        }

        [Test]
        public void TheRungBelowStillCountsFirst()
        {
            var sim = LifeJarTests.World(); var m = Model(sim);
            PlacedLifeTests.Wood(sim, 30);
            m.UpdateJars(3);
            Assert.AreEqual(JarState.Locked, m.JarStateOf(LifeSpecies.Deer));
            Assert.AreEqual("Deer need the rabbits first", m.JarNeed(LifeSpecies.Deer, 3));
        }
    }
}
