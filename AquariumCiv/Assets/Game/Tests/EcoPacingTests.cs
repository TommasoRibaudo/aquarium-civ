using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #569: the water from the mister grows the forest. Misted ground is remembered (EcoState.watered); a seed takes there, seedlings and
    /// young trees grow faster, the grass comes sooner. The pacing numbers themselves (the village at year 25 for an active player)
    /// are PacingLabTests in Tools/SimTests.
    /// </summary>
    public class EcoPacingTests
    {
        static SimCore World(int seed = 7)
        {
            var cfg = new SimConfig { ecology = true };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        /// <summary>Two identical patches far apart on dry ground: a grown tree, six seedlings and six young trees round it, grass between.</summary>
        sealed class Patches
        {
            public readonly EcoModel m; public readonly EcoState st; public readonly int[] centre = new int[2];
            public Patches(int seed)
            {
                var sim = World(seed);
                st = sim.State.eco;
                m = new EcoModel(st, sim.EcoGround, sim.Config.eco);
                var land = sim.EcoGround;
                for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)st.cover[c] != EcoCover.Water) st.cover[c] = (byte)EcoCover.Grass;
                var good = new List<int>();
                for (int c = 0; c < EcoLand.N; c++)
                {
                    int i = SiteMap.I(c), j = SiteMap.J(c);
                    if (i < 14 || i >= SiteMap.NX - 14 || j < 14 || j >= SiteMap.NZ - 14) continue;
                    if (land.InBasin(c) || land.yard[c] || land.slope[c] > 0.2f) continue;
                    good.Add(c);
                }
                // one patch on the left of the box, one on the right
                centre[0] = good.Where(c => SiteMap.I(c) < 22).OrderBy(c => land.height[c]).First();
                centre[1] = good.Where(c => SiteMap.I(c) > SiteMap.NX - 23).OrderBy(c => land.height[c]).First();
                foreach (var cc in centre)
                {
                    int ci = SiteMap.I(cc), cj = SiteMap.J(cc);
                    st.cover[cc] = (byte)EcoCover.Tree;
                    for (int k = 0; k < 6; k++)
                    {
                        double a = k / 6.0 * Math.PI * 2;
                        st.cover[SiteMap.Index(ci + (int)Math.Round(Math.Cos(a) * 4), cj + (int)Math.Round(Math.Sin(a) * 4))] = (byte)EcoCover.Seedling;
                        st.cover[SiteMap.Index(ci + (int)Math.Round(Math.Cos(a + 0.5) * 6), cj + (int)Math.Round(Math.Sin(a + 0.5) * 6))] = (byte)EcoCover.Young;
                    }
                }
                st.birds = 12f; // a flock: the seeds come from the two trees alike
                m.Count();
            }

            public (int saplings, int grown, int grass) Read(int patch, float r = 0.9f)
            {
                int sap = 0, grown = 0, grass = 0; int cc = centre[patch];
                for (int c = 0; c < EcoLand.N; c++)
                {
                    if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), SiteMap.CXc(cc), SiteMap.CZc(cc)) > r || c == cc) continue;
                    switch ((EcoCover)st.cover[c])
                    {
                        case EcoCover.Seedling: case EcoCover.Young: sap++; break;
                        case EcoCover.Tree: grown++; break;
                        case EcoCover.Grass: case EcoCover.Tall: grass++; break;
                    }
                }
                return (sap, grown, grass);
            }

            /// <summary>Runs <paramref name="seasons"/> seasons; both patches are kept equally damp every season so only the water the mister
            /// left on the ground differs. One patch is misted once at the start (a full hold on its tree).</summary>
            public void Run(int mistedPatch, int seasons)
            {
                var cc = centre[mistedPatch];
                m.Mist(SiteMap.CXc(cc), SiteMap.CZc(cc), 2f, 0, null, 2f);
                for (int s = 0; s < seasons; s++)
                {
                    for (int c = 0; c < EcoLand.N; c++) if ((EcoCover)st.cover[c] != EcoCover.Water) st.moisture[c] = 0.4f;
                    m.Step((s + 1) % 4, s * 3);
                }
            }
        }

        [Test]
        public void MistedGround_GrowsAndSeedsFasterThanUnmisted()
        {
            // two patches, one misted once; swap which, over three boxes, so the land's own unevenness cancels
            int mistedGrown = 0, dryGrown = 0, mistedSap = 0, drySap = 0;
            foreach (int seed in new[] { 7, 21, 42 })
                for (int which = 0; which < 2; which++)
                {
                    var p = new Patches(seed);
                    p.Run(which, 12); // three years
                    var (a, b) = (p.Read(0), p.Read(1));
                    var misted = which == 0 ? a : b; var dry = which == 0 ? b : a;
                    mistedGrown += misted.grown; dryGrown += dry.grown;
                    mistedSap += misted.saplings; drySap += dry.saplings;
                }
            TestContext.Out.WriteLine($"after 3 years: young+grown trees misted {mistedGrown} vs dry {dryGrown}; saplings misted {mistedSap} vs dry {drySap}");
            Assert.GreaterOrEqual(mistedGrown, dryGrown * 2f, "seedlings and young trees grew up faster where the mister's water fell");
            Assert.GreaterOrEqual(mistedGrown + mistedSap, (dryGrown + drySap) * 1.1f, "and visibly more plants stand there (more seeds took)");
        }

        [Test]
        public void TheMisterStampsWatered_ItFadesBySeason_AndIsForgottenInTheEnd()
        {
            var sim = World();
            var (x, z) = (sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ);
            Assert.IsTrue(sim.State.eco.watered == null || sim.State.eco.watered.All(v => v == 0f), "a new box has no watered ground");
            sim.Mist(x, z, 4f);
            var w = sim.State.eco.watered;
            int c0 = SiteMap.CellAt(x, z);
            Assert.Greater(w[c0], 0.8f, "the middle of a full mist remembers it");
            int far = SiteMap.CellAt(x + 2.5f, z);
            Assert.AreEqual(0f, w[far], "ground the mist did not reach does not");
            float v0 = w[c0];
            sim.DebugSkipSeasons(1);
            Assert.Less(w[c0], v0, "a season later it has faded");
            Assert.AreEqual(v0 * sim.Config.eco.wateredKeep, w[c0], 0.01f);
            sim.DebugSkipSeasons(120);
            Assert.AreEqual(0f, w[c0], "and is forgotten in the end");
        }

        [Test]
        public void OldSaveWithoutWatered_Loads_AndMistsWork()
        {
            var sim = World();
            sim.Mist(sim.EcoGround.campX + 0.9f, sim.EcoGround.campZ, 2f);
            sim.Advance(1);
            string json = JsonUtility.ToJson(sim.State);
            string old = Regex.Replace(json, "\"watered\":\\[[^\\]]*\\],?", "");
            Assert.AreNotEqual(json, old, "the watered layer was cut out");
            var loaded = JsonUtility.FromJson<SimState>(old);
            Assert.IsTrue(loaded.eco.watered == null || loaded.eco.watered.Length == 0);
            var core = new SimCore(new SimConfig { ecology = true }, loaded);
            core.Advance(3);
            core.Mist(core.EcoGround.campX + 0.9f, core.EcoGround.campZ, 2f);
            Assert.AreEqual(EcoLand.N, core.State.eco.watered.Length);
            Assert.Greater(core.State.eco.watered.Max(), 0.5f);
            // and a save that has it round-trips
            var again = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(core.State));
            CollectionAssert.AreEqual(core.State.eco.watered, again.eco.watered);
        }

        [Test]
        public void NeverMisted_WateredStaysZero_SoNothingChangesForABoxNobodyMists()
        {
            var sim = World();
            sim.Advance(6);
            Assert.IsTrue(sim.State.eco.watered.All(v => v == 0f));
        }

        /// <summary>#605: a placed thicket on a box; watered or not, then stepped season by season with the same damp everywhere.</summary>
        static (int bushes, int outside, int cap) ThicketAfter(int seed, bool watered, int seasons)
        {
            var sim = World(seed);
            Assert.GreaterOrEqual(sim.DebugPlaceNearCamp(LifeSpecies.Thicket), 0);
            var st = sim.State.eco;
            var m = new EcoModel(st, sim.EcoGround, sim.Config.eco);
            if (watered)
                for (int c = 0; c < EcoLand.N; c++) if (m.ThicketZone(c) > 0f && (EcoCover)st.cover[c] != EcoCover.Water) st.watered[c] = 1f;
            for (int s = 0; s < seasons; s++)
            {
                if (watered) for (int c = 0; c < EcoLand.N; c++) if (m.ThicketZone(c) > 0f) st.watered[c] = Math.Max(st.watered[c], 0.8f); // kept misted
                m.Step(s % 4, s * 3);
            }
            int n = 0, outside = 0;
            for (int c = 0; c < EcoLand.N; c++)
                if ((EcoCover)st.cover[c] == EcoCover.Bush) { n++; if (m.ThicketZone(c) <= 0f) outside++; }
            return (n, outside, m.BushCap);
        }

        [Test]
        public void AWateredThicket_FillsFasterThanADryOne_AndStaysInItsArea()
        {
            int wet = 0, dry = 0;
            foreach (int seed in new[] { 7, 21, 42 })
            {
                var w = ThicketAfter(seed, true, 6); var d = ThicketAfter(seed, false, 6);
                TestContext.Out.WriteLine($"seed {seed}: bushes after 6 seasons watered {w.bushes} vs dry {d.bushes} (cap {w.cap})");
                wet += w.bushes; dry += d.bushes;
                Assert.AreEqual(0, w.outside, "no bush outside the thicket's area");
                Assert.LessOrEqual(w.bushes, w.cap);
                var long1 = ThicketAfter(seed, true, 60);
                Assert.LessOrEqual(long1.bushes, long1.cap, "a watered thicket still stops at its cap");
                Assert.AreEqual(0, long1.outside);
            }
            TestContext.Out.WriteLine($"total watered {wet} vs dry {dry}");
            Assert.Greater(wet, dry, "the mister's water brings the thicket on faster");
            Assert.LessOrEqual(wet, dry * 2f, "but only a little faster");
        }
    }
}
