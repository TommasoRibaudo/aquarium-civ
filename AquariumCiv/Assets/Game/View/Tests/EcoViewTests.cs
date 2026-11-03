using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.View.Tests
{
    /// <summary>
    /// P8 (#244, #245): the box draws the living land from the sim. The layout copies the sim's cover; the ground under a
    /// bare cell is dry dirt and turns green after a mist; with the land on, the forest layer gives no trees (the cover does);
    /// a greened quarter rebuilds only its own chunk; the pond and the water table follow the sim.
    /// </summary>
    public class EcoViewTests
    {
        static SimCore EcoSim(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static BoxLayout Fill(SimCore sim)
        {
            var L = new BoxLayout();
            SimLayout.Fill(L, sim);
            return L;
        }

        [Test]
        public void TheLayoutCopiesTheSimsCover()
        {
            var sim = EcoSim();
            var L = Fill(sim);
            Assert.IsTrue(L.eco.on, "the living land is on in the layout");
            Assert.IsTrue(L.eco.cover.All(c => c == EcoLayer.Bare), "a new box is bare everywhere");
            Assert.AreEqual(0, L.trees.Count, "no trees on a barren box");
            var st = sim.Settlement;
            float x = st.Map.greenX + 0.5f, z = st.Map.greenZ;
            var greened = sim.Mist(x, z, 2f);
            Assert.That(greened.Count, Is.GreaterThan(0), "the mist greens its core at once");
            L = Fill(sim);
            foreach (int c in greened) Assert.AreEqual((byte)sim.EcoCoverAt(c), L.eco.cover[c], "cell " + c);
            Assert.That(L.eco.GreenAt(SiteMap.CXc(greened[0]), SiteMap.CZc(greened[0])), Is.GreaterThan(0.5f));
            Assert.That(L.eco.GreenAt(-2.7f, 1.9f), Is.EqualTo(0f), "far corner still bare");

            var off = new SimConfig();
            Assert.IsFalse(Fill(new SimCore(off, SimCore.NewWorld(7, off))).eco.on, "the slice's box: off");
        }

        [Test]
        public void TheBoxDrawsBareDirtThenGreen_AndRebuildsOnlyThatQuarter()
        {
            var sim = EcoSim();
            var go = new GameObject("EcoTestBox");
            go.SetActive(false);
            try
            {
                var box = go.AddComponent<BoxView>();
                box.logRebuilds = false;
                box.bakeAO = false;
                SimLayout.Fill(box.layout, sim);
                go.SetActive(true);
                var st = sim.Settlement;
                float x = -1.7f, z = -1.1f; // well inside the front-left quarter, away from the camp
                var before = box.GroundColor(x, z, box.land.Height(x, z), out _);
                Assert.That(before.r, Is.GreaterThan(before.g), "bare ground is dirt (more red than green)");

                sim.Mist(x, z, 2f);
                SimLayout.Fill(box.layout, sim);
                var after = box.GroundColor(x, z, box.land.Height(x, z), out _);
                Assert.That(after.g - after.r, Is.GreaterThan(before.g - before.r + 0.1f), "after a mist it turns clearly green");

                box.Refresh(); // no source on the object: the layout stays as filled
                CollectionAssert.Contains(box.lastChunks, BoxView.ChunkOf(x, z), "the misted quarter rebuilt");
                Assert.That(box.lastChunks.Count, Is.LessThan(BoxView.ChunkCount), "not the whole box");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ThePondAndTheTableFollowTheSim()
        {
            var sim = EcoSim();
            var L = Fill(sim);
            Assert.AreEqual(0, L.eco.pondCells, "a dry hollow at the start");
            Assert.That(L.eco.tableY, Is.LessThan(L.eco.floodY), "the table starts below the flood line");
            sim.DebugSetTable(sim.EcoFloodLine - 0.01f);
            sim.DebugSkipSeasons(2);
            L = Fill(sim);
            Assert.That(L.eco.pondCells, Is.GreaterThan(0), "the water surfaced");
            Assert.That(L.eco.waterY, Is.GreaterThan(0.5f).And.LessThanOrEqualTo(L.eco.floodY + 1e-3f));
            int water = 0;
            for (int c = 0; c < SiteMap.N; c++) if (L.eco.WaterCell(c)) water++;
            Assert.AreEqual(L.eco.pondCells, water, "the pond's cells are the water cells");
        }
    }
}
