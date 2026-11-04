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

        [Test]
        public void TheFullPondMeetsBothGlassWalls()
        {
            var sim = EcoSim();
            sim.DebugSetTable(sim.EcoFloodLine - 0.005f);
            sim.DebugSkipSeasons(8);
            var go = new GameObject("EcoPondBox");
            go.SetActive(false);
            try
            {
                var box = go.AddComponent<BoxView>();
                box.bakeAO = false;
                box.land = SimLayout.LandFor(sim.TerrainPreset);
                SimLayout.Fill(box.layout, sim);
                go.SetActive(true);
                var ec = box.layout.eco;
                Assert.That(ec.pondCells, Is.GreaterThan(20), "a real pond");
                // The hollow's water stands in the very corner cell, against both walls.
                Assert.IsTrue(ec.WaterCell(SiteMap.Index(SiteMap.NX - 1, 0)), "water in the front-right corner cell");
                var b = box.DebugWaterMesh.bounds;
                Assert.That(b.max.x, Is.EqualTo(Land.HX + BoxView.GlassInner).Within(0.001f), "the sheet runs to the right glass");
                Assert.That(b.min.z, Is.EqualTo(-Land.HZ - BoxView.GlassInner).Within(0.001f), "and to the front glass");
                Assert.That(b.max.x, Is.LessThanOrEqualTo(Land.HX + BoxView.GlassInner + 1e-4f), "clamped there, never through it");
                Assert.That(b.min.z, Is.GreaterThanOrEqualTo(-Land.HZ - BoxView.GlassInner - 1e-4f));
                // A wall of water stands at the glass, from the ground up to the level.
                var v = box.DebugWaterMesh.vertices;
                float low = float.MaxValue;
                foreach (var p in v) if (Mathf.Abs(p.x - (Land.HX + BoxView.GlassInner)) < 1e-4f && p.y < low) low = p.y;
                Assert.That(low, Is.LessThan(ec.waterY - 0.05f), "water is seen below the level through the side pane");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheDampEarthHasNoLineOrEdgeAtTheTable()
        {
            // Smooth in height for any table and any spot along the glass: no step bigger than a hair per 2 mm,
            // dry well above, damp well below.
            for (int side = 0; side < 4; side++)
                foreach (float table in new[] { 0.3f, 0.6f, 0.85f, 0.97f })
                    for (float along = -2.9f; along < 2.9f; along += 0.37f)
                    {
                        float prev = BoxView.DampAmount(along, side, 0f, table), maxStep = 0f;
                        for (float y = 0.002f; y < 2f; y += 0.002f)
                        {
                            float a = BoxView.DampAmount(along, side, y, table);
                            maxStep = Mathf.Max(maxStep, Mathf.Abs(a - prev)); prev = a;
                        }
                        Assert.That(maxStep, Is.LessThan(0.03f), $"side {side} table {table} at {along:0.00}: a step of {maxStep:0.000} in 2 mm");
                        Assert.That(BoxView.DampAmount(along, side, table + 0.3f, table), Is.EqualTo(0f).Within(1e-4f), "dry well above");
                        if (table > 0.5f) Assert.That(BoxView.DampAmount(along, side, table - 0.3f, table), Is.GreaterThan(0.7f), "damp well below");
                    }
        }

        [Test]
        public void TheDampMeshRisesWithTheTable_AndItsVertexAlphaIsSmooth()
        {
            var sim = EcoSim();
            var go = new GameObject("EcoDampBox");
            go.SetActive(false);
            try
            {
                var box = go.AddComponent<BoxView>();
                box.bakeAO = false;
                box.land = SimLayout.LandFor(sim.TerrainPreset);
                float last = -1f;
                foreach (float above in new[] { -0.5f, -0.3f, -0.1f })
                {
                    sim.DebugSetTable(sim.EcoFloodLine + above);
                    SimLayout.Fill(box.layout, sim);
                    if (!go.activeSelf) go.SetActive(true); else box.Refresh();
                    var m = box.DebugDampMesh;
                    Assert.IsNotNull(m, "the damp earth is drawn");
                    var v = m.vertices; var c = m.colors;
                    float top = 0f, maxA = 0f;
                    for (int i = 0; i < v.Length; i++) if (c[i].a > 0.05f) top = Mathf.Max(top, v[i].y);
                    for (int i = 0; i < c.Length; i++) maxA = Mathf.Max(maxA, c[i].a);
                    Assert.That(top, Is.GreaterThan(last), "the damp top rises with the table");
                    Assert.That(top, Is.LessThanOrEqualTo(box.layout.eco.tableY + 0.13f), "and stops within a hand of the table");
                    Assert.That(maxA, Is.GreaterThan(0.6f), "the earth under the table is clearly damp");
                    last = top;
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheSoilCutEndsAtTheGroundAlongTheHollow_AndTheWaterFaceOnlyUpToTheLevel()
        {
            foreach (bool pond in new[] { false, true })
            {
                var sim = EcoSim();
                if (pond) { sim.DebugSetTable(sim.EcoFloodLine - 0.005f); sim.DebugSkipSeasons(8); }
                var go = new GameObject("EcoCutBox");
                go.SetActive(false);
                try
                {
                    var box = go.AddComponent<BoxView>();
                    box.bakeAO = false;
                    box.land = SimLayout.LandFor(sim.TerrainPreset);
                    SimLayout.Fill(box.layout, sim);
                    go.SetActive(true);
                    // Along both glass walls where the hollow runs: the cut's top edge is the ground, not the old bank's height.
                    int checkedPts = 0;
                    for (float t = 0.6f; t < 2.9f; t += 0.1f)
                        foreach (var p in new[] { new Vector2(t, -Land.HZ), new Vector2(Land.HX, -2.0f + (t - 0.6f) * 0.8f) })
                        {
                            float g = box.land.Height(p.x, p.y);
                            Assert.That(box.DebugCutTop(p), Is.EqualTo(g).Within(1e-4f), $"cut top at {p}");
                            checkedPts++;
                        }
                    Assert.Greater(checkedPts, 20);
                    var wm = box.DebugWaterMesh;
                    var ec = box.layout.eco;
                    if (!pond) { Assert.AreEqual(0, wm.vertexCount, "no water, no water face"); continue; }
                    foreach (var v in wm.vertices)
                        Assert.That(v.y, Is.LessThanOrEqualTo(ec.waterY + 1e-4f), "no water face above the level");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
