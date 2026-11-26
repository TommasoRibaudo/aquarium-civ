using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain; // not UnityEngine.Terrain

namespace AquariumCiv.Tests
{
    /// <summary>
    /// #768: fields land on damp, fertile ground and not on the town centre's ring, with the organic placement kept (decisions.md
    /// 2026-10-29). Each seed grows a century (SettleTests.Century); the numbers go to the log: mean fertility, mean distance to the
    /// water (moisture: nearer is damper) and distance to the green for every field.
    /// </summary>
    public class SettleFieldGroundTests
    {
        static readonly (int seed, int preset)[] Runs = { (7, 0), (11, 1), (1, 2) };

        static void Log(string s)
        {
#if UNITY_2017_1_OR_NEWER
            UnityEngine.Debug.Log("[SettleFields] " + s);
#else
            TestContext.Progress.WriteLine("[SettleFields] " + s);
#endif
        }

        struct Stat { public int n; public float fert, wet, centre, minCentre; }

        static Stat Measure(SimCore sim, LotKind kind)
        {
            var w = sim.Settlement; var map = w.Map;
            var cells = new List<int>();
            var st = new Stat { minCentre = 99f };
            foreach (var l in w.Lots)
            {
                if (l.retired || l.kind != kind) continue;
                SiteMap.RectCells(l.x, l.z, l.yaw, l.w, l.d, 0f, cells);
                if (cells.Count == 0) continue;
                float f = 0, wd = 0;
                foreach (int c in cells) { f += map.Fertility(c, sim.State.month, sim.State.nudges); wd += map.waterDist[c]; }
                st.n++; st.fert += f / cells.Count; st.wet += wd / cells.Count;
                float d = Terrain.Dist(l.x, l.z, map.greenX, map.greenZ);
                st.centre += d; st.minCentre = Math.Min(st.minCentre, d);
            }
            if (st.n > 0) { st.fert /= st.n; st.wet /= st.n; st.centre /= st.n; }
            return st;
        }

        // Before #768 (same three runs, year 120): mean fertility 0.591 / 0.530 / 0.643, mean water distance 1.093 / 0.597 / 0.739,
        // nearest field to the green 1.224 / 1.664 / 1.965 (0.864 at year 45 in the third).
        [Test]
        public void FieldsStandOnDampFertileGroundOffTheCentreRing()
        {
            foreach (var (seed, preset) in Runs)
            {
                var sim = SettleTests.Century(seed, preset);
                var map = sim.Settlement.Map;
                var s = Measure(sim, LotKind.Farm);
                var fert = new List<float>(); float wetSum = 0; int n = 0;
                for (int c = 0; c < SiteMap.N; c++)
                {
                    if (map.Water(c) || Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), map.greenX, map.greenZ) < Encyclopedia.Of(LotKind.Farm).minGreenDist) continue;
                    fert.Add(map.Fertility(c, sim.State.month, sim.State.nudges)); wetSum += map.waterDist[c]; n++;
                }
                fert.Sort();
                float median = fert[fert.Count / 2], landWet = wetSum / n;
                Log($"seed {seed} preset {preset}: fields {s.n}, mean fertility {s.fert:F3} (land median {median:F3}, top quarter from {fert[fert.Count * 3 / 4]:F3}), "
                    + $"mean water distance {s.wet:F3} (land {landWet:F3}), mean centre distance {s.centre:F3}, nearest {s.minCentre:F3}");
                Assert.GreaterOrEqual(s.n, 3, "the village has its fields");
                Assert.GreaterOrEqual(s.fert, median + 0.03f, $"seed {seed}: fields on fertile ground");
                Assert.Less(s.wet, landWet, $"seed {seed}: fields on damper ground than the land's mean");
                Assert.GreaterOrEqual(s.minCentre, Encyclopedia.Of(LotKind.Farm).minGreenDist - 0.001f, $"seed {seed}: no field on the centre's ring");
            }
        }

        /// <summary>The church's clear-of-the-wood numbers: trees in its lot and a 0.25 margin, and its distance from the green.</summary>
        static (int trees, float centre, bool found) ChurchWood(SimCore sim)
        {
            var w = sim.Settlement; var map = w.Map; var cells = new List<int>();
            foreach (var l in w.Lots)
            {
                if (l.retired || l.kind != LotKind.Church) continue;
                SiteMap.RectCells(l.x, l.z, l.yaw, l.w, l.d, Encyclopedia.Of(LotKind.Church).woodMargin, cells);
                int trees = 0; foreach (int c in cells) if (map.Overgrown(c) > 0f) trees++;
                return (trees, Terrain.Dist(l.x, l.z, map.greenX, map.greenZ), true);
            }
            return (0, 0f, false);
        }

        /// <summary>A village (year 30, the era moments watched) that is then given its church, as the economy does at 24 people.</summary>
        static SimCore VillageWithChurch(int seed, int preset, bool ecology)
        {
            var cfg = new SimConfig { terrainPreset = preset, settle = true, buildingSlots = 40, ecology = ecology };
            var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
            void Month() { sim.Step(); sim.State.simHours = sim.State.month * sim.Config.StepHours; if (sim.State.eraReady) sim.AdvanceEra(); }
            while (sim.State.month < 30 * 12L) Month();
            for (int slot = 0; slot < cfg.buildingSlots && sim.AddBuildingAt(BuildingType.Church, slot) == null; slot++) { }
            for (int i = 0; i < 3; i++) Month();
            return sim;
        }

        [Test]
        public void ChurchStandsClearOfTheWoodNearTheVillage([Values(false, true)] bool ecology)
        {
            foreach (var (seed, preset) in Runs)
            {
                var sim = VillageWithChurch(seed, preset, ecology);
                var (trees, centre, found) = ChurchWood(sim);
                Log($"seed {seed} preset {preset} ecology {ecology}: church {(found ? "standing" : "missing")}, trees in lot+margin {trees}, distance to the green {centre:F3}");
                Assert.IsTrue(found, $"seed {seed}: the village has its church");
                Assert.AreEqual(0, trees, $"seed {seed}: no tree within the church's lot and margin");
                Assert.LessOrEqual(centre, 2.2f, $"seed {seed}: the church stands near the village");
            }
        }

        [Test, Explicit("#769 baseline: the same runs with the wood rule off")]
        public void ChurchBaselineWithoutTheRule([Values(false, true)] bool ecology)
        {
            var spec = Encyclopedia.Of(LotKind.Church); float was = spec.woodMargin;
            try
            {
                foreach (var (seed, preset) in Runs)
                {
                    spec.woodMargin = 0f;
                    var sim = VillageWithChurch(seed, preset, ecology);
                    spec.woodMargin = was; var (trees, centre, found) = ChurchWood(sim); spec.woodMargin = 0f;
                    Log($"BASELINE seed {seed} ecology {ecology}: church {found}, trees in lot+margin {trees}, distance {centre:F3}");
                }
            }
            finally { spec.woodMargin = was; }
        }

        // ---- #769 round 2: the eco wood round a church that stood on clear ground ----

        /// <summary>Eco trees (seedling, young, tree) in the church's lot and margin.</summary>
        static int EcoTreesAtChurch(SimCore sim)
        {
            var cells = new List<int>(); var cover = sim.State.eco.cover; int n = 0;
            foreach (var l in sim.Settlement.Lots)
            {
                if (l.retired || l.kind != LotKind.Church) continue;
                SiteMap.RectCells(l.x, l.z, l.yaw, l.w, l.d, Encyclopedia.Of(LotKind.Church).woodMargin, cells);
                foreach (int c in cells)
                {
                    var cv = (AquariumCiv.Sim.Eco.EcoCover)cover[c];
                    if (cv == AquariumCiv.Sim.Eco.EcoCover.Seedling || cv == AquariumCiv.Sim.Eco.EcoCover.Young || cv == AquariumCiv.Sim.Eco.EcoCover.Tree) n++;
                }
            }
            return n;
        }

        /// <summary>A village (ecology on) given its church at year 30, with a wood grown on the ring just outside the church's margin,
        /// then lived to year 60 with the birds spreading the wood. Returns the worst count of eco trees in the church's lot and margin.</summary>
        static int WorstEcoTreesAtChurch(int seed, int preset, out int atBuild)
        {
            var sim = VillageWithChurch(seed, preset, true);
            atBuild = EcoTreesAtChurch(sim);
            var cover = sim.State.eco.cover; var cells = new List<int>(); Lot church = null;
            foreach (var l in sim.Settlement.Lots) if (!l.retired && l.kind == LotKind.Church) church = l;
            Assert.IsNotNull(church, $"seed {seed}: a church");
            // a wood on the dry ground 0.35-0.6 out from the church's lot: what the Keeper's trees and the birds make of the ground round it
            SiteMap.RectCells(church.x, church.z, church.yaw, church.w, church.d, 0.6f, cells);
            var inner = new HashSet<int>(); var m = new List<int>();
            SiteMap.RectCells(church.x, church.z, church.yaw, church.w, church.d, Encyclopedia.Of(LotKind.Church).woodMargin + 0.05f, m);
            foreach (int c in m) inner.Add(c);
            foreach (int c in cells)
                if (!inner.Contains(c) && sim.Settlement.Map.lot[c] == 0 && !sim.Settlement.Map.Water(c) && cover[c] != (byte)AquariumCiv.Sim.Eco.EcoCover.Water)
                    cover[c] = (byte)AquariumCiv.Sim.Eco.EcoCover.Tree;
            int worst = 0;
            while (sim.State.month < 60 * 12L)
            {
                sim.Step(); sim.State.simHours = sim.State.month * sim.Config.StepHours; if (sim.State.eraReady) sim.AdvanceEra();
                if (sim.State.month % 12 == 0) worst = Math.Max(worst, EcoTreesAtChurch(sim));
            }
            return worst;
        }

        [Test]
        public void TheEcoWoodNeverSpreadsRoundTheChurch()
        {
            foreach (var (seed, preset) in new[] { (7, 0), (11, 1), (1, 2), (3, 0), (5, 1) })
            {
                int worst = WorstEcoTreesAtChurch(seed, preset, out int atBuild);
                Log($"seed {seed}: eco trees in the church's lot and margin: {atBuild} at build, worst {worst} to year 60");
                Assert.AreEqual(0, atBuild, $"seed {seed}: no tree stands in the lot and margin when the church goes up");
                Assert.AreEqual(0, worst, $"seed {seed}: no tree grows into the lot and margin");
            }
        }

        [Test]
        public void TreesAlreadyRoundTheChurchAreClearedWhenItGoesUp()
        {
            foreach (var (seed, preset) in new[] { (7, 0), (11, 1), (1, 2) })
            {
                var cfg = new SimConfig { terrainPreset = preset, settle = true, buildingSlots = 40, ecology = true };
                var sim = new SimCore(cfg, SimCore.NewWorld(seed, cfg));
                void Month() { sim.Step(); sim.State.simHours = sim.State.month * sim.Config.StepHours; if (sim.State.eraReady) sim.AdvanceEra(); }
                while (sim.State.month < 30 * 12L) Month();
                var map = sim.Settlement.Map; var cover = sim.State.eco.cover; int woods = 0;
                for (int c = 0; c < SiteMap.N; c++)
                    if (map.lot[c] == 0 && !map.Water(c) && !map.Green(c) && cover[c] != (byte)AquariumCiv.Sim.Eco.EcoCover.Water
                        && Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), map.greenX, map.greenZ) < 2.4f) { cover[c] = (byte)AquariumCiv.Sim.Eco.EcoCover.Tree; woods++; }
                for (int slot = 0; slot < cfg.buildingSlots && sim.AddBuildingAt(BuildingType.Church, slot) == null; slot++) { }
                for (int i = 0; i < 3; i++) Month();
                int left = EcoTreesAtChurch(sim);
                Log($"seed {seed}: wood on {woods} cells round the village, trees left in the church's lot and margin {left}");
                Assert.IsTrue(ChurchWood(sim).found, $"seed {seed}: the church stands");
                Assert.AreEqual(0, left, $"seed {seed}: the trees round the church are cleared");
            }
        }
    }
}
