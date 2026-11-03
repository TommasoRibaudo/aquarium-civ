using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using Terrain = AquariumCiv.Sim.Settle.Terrain; // not UnityEngine.Terrain

namespace AquariumCiv.Tests
{
    /// <summary>#115: per-setting terrain shape and palette values, as pure logic. The river valley must come out exactly as it
    /// did before the shape parameters existed; the two settings must differ from it the way the design says and still place
    /// everything.</summary>
    public class SettingTerrainTests
    {
        static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        // ---- the river valley is bit-for-bit what it was ----

        /// <summary>The terrain function as it stood before #115 (copied, with its literals).</summary>
        static float OldHeight(TerrainSpec t, float x, float z)
        {
            float xm = t.mirror ? -x : x;
            float nx = xm + t.noiseOff, nz = z + t.noiseOff * 0.7f;
            float h = Terrain.G + 0.07f * Terrain.Fbm(nx * 0.9f, nz * 0.9f);
            float back = Terrain.Smooth(0.1f, 2.1f, z), left = Terrain.Smooth(2.6f, -2.8f, xm);
            h += 0.62f * back * (0.3f + 0.7f * left) * (0.7f + 0.6f * Terrain.Fbm(nx * 0.6f, nz * 0.6f + 11f));
            float dp = Terrain.Dist(x, z, t.pondX, t.pondZ);
            h -= 0.24f * (1f - Terrain.Smooth(0.45f, 0.98f, dp));
            float dc = Terrain.Dist(x, z, t.centreX, t.centreZ);
            h = Terrain.Lerp(h, Terrain.G + 0.03f, (1f - Terrain.Smooth(0.9f, 1.75f, dc)) * 0.92f);
            return h;
        }

        static float OldForestBias(TerrainSpec t, float x, float z)
        {
            float xm = t.mirror ? -x : x;
            return Terrain.Smooth(-0.6f, 1.8f, z) * 0.6f + Terrain.Smooth(1.5f, -2.5f, xm) * 0.5f;
        }

        [Test]
        public void TheRiverValleyLandIsBitForBitUnchanged()
        {
            foreach (int preset in new[] { 0, 1, 2, 3, 4, 5, -1, -2, 99, 103 })
            {
                var t = TerrainSpec.Preset(preset);
                for (float x = -3f; x <= 3f; x += 0.1f)
                    for (float z = -2.2f; z <= 2.2f; z += 0.1f)
                    {
                        Assert.AreEqual(BitConverter.SingleToInt32Bits(OldHeight(t, x, z)), BitConverter.SingleToInt32Bits(Terrain.Height(t, x, z)), $"height {preset} {x},{z}");
                        Assert.AreEqual(BitConverter.SingleToInt32Bits(OldForestBias(t, x, z)), BitConverter.SingleToInt32Bits(Terrain.ForestBias(t, x, z)), $"forest {preset} {x},{z}");
                        Assert.IsFalse(Terrain.OutcropAt(t, x, z, Terrain.Height(t, x, z)), "no starting outcrops on the old presets");
                    }
            }
            // The old presets keep their meaning (3 and 4 are still mirror and backpond).
            Assert.AreEqual("mirror", TerrainSpec.Preset(1).name);
            Assert.AreEqual("mirror", TerrainSpec.Preset(4).name);
            Assert.AreEqual("backpond", TerrainSpec.Preset(2).name);
            Assert.AreEqual("stylebox", TerrainSpec.Preset(3).name);
        }

        [Test]
        public void TheRiverValleyBoxPlacesExactlyAsBefore()
        {
            // A box that is given no setting keeps the config's preset and the same hash of its saved layout run to run.
            var a = SettleTests.NewBox(7, 0);
            var b = SettleTests.NewBox(7, 0);
            for (int i = 0; i < 40 * 12; i++) { a.Step(); b.Step(); }
            Assert.AreEqual(0, a.State.settle.terrain);
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(a.State), UnityEngine.JsonUtility.ToJson(b.State));
            Assert.AreEqual("stylebox", a.Settlement.Map.spec.name);
        }

        [Test]
        public void ThePaletteOfTheRiverValleyIsTodaysPalette()
        {
            // Hex copied from Palette.cs: if Palette changes on purpose, change both.
            var r = SettingLook.RiverValley;
            Assert.AreEqual(new uint[] { 0x8DAA5B, 0x7A9C4E, 0xA3B866, 0x5E7F43 }, new[] { r.grassA, r.grassB, r.grassC, r.moss });
            Assert.AreEqual(new uint[] { 0xB08D62, 0xC8AA7A, 0xD8C08E, 0xB9A57A }, new[] { r.dirt, r.path, r.sand, r.sandWet });
            Assert.AreEqual(new uint[] { 0x4F6B5E, 0x6FA6A8, 0x4E8C93, 0x9BBF5E }, new[] { r.pondBed, r.water, r.waterDeep, r.sprout });
            Assert.AreEqual(new uint[] { 0x6E9A4B, 0x5C8A45, 0x86AE58, 0x3F6B4A, 0x6B4E3A }, new[] { r.leafA, r.leafB, r.leafC, r.pine, r.bark });
            Assert.AreEqual(new uint[] { 0xA29C93, 0x878178, 0xBDB7AD }, new[] { r.rock, r.rockDark, r.rockLight });
            Assert.AreEqual(0f, r.skyAmount); Assert.AreEqual(0f, r.fogAmount);
            Assert.AreEqual(0f, r.SnowAmount(5f), "the valley has no snow");
            Assert.AreSame(r, SettingLook.For(BoxSetting.RiverValley));
            Assert.AreSame(r, SettingLook.ForPreset(0));
            Assert.AreSame(r, SettingLook.ForPreset(2));
        }

        // ---- the mapping ----

        [Test]
        public void SettingsMapToPresetsAndBack()
        {
            Assert.AreEqual(TerrainSpec.DesertOasisPreset, TerrainSpec.PresetFor(BoxSetting.DesertOasis));
            Assert.AreEqual(TerrainSpec.MountainVillagePreset, TerrainSpec.PresetFor(BoxSetting.MountainVillage));
            Assert.AreEqual(2, TerrainSpec.PresetFor(BoxSetting.RiverValley, 2), "no setting: the config's preset");
            Assert.AreEqual(BoxSetting.DesertOasis, TerrainSpec.SettingOf(100));
            Assert.AreEqual(BoxSetting.MountainVillage, TerrainSpec.SettingOf(101));
            Assert.AreEqual(BoxSetting.RiverValley, TerrainSpec.SettingOf(1));
            Assert.AreSame(SettingLook.DesertOasis, SettingLook.ForPreset(100));
            Assert.AreSame(SettingLook.MountainVillage, SettingLook.ForPreset(101));
        }

        [Test]
        public void ANextBoxGetsItsSettingsLandAndKeepsItInTheSave()
        {
            foreach (var (setting, preset) in new[] { (BoxSetting.DesertOasis, 100), (BoxSetting.MountainVillage, 101) })
            {
                var cfg = new SimConfig();
                var old = SettleTests.NewBox(7, 0);
                for (int i = 0; i < 60 * 12; i++) { old.Step(); if (old.State.eraReady) old.AdvanceEra(); }
                old.ForcePeakReady();
                var session = new ClockStart { sim = old, shelf = ShelfState.Single(old.State) };
                var box2 = ClockLogic.StartNextBox(session, cfg, old.CompleteBox(), setting, 5, Now);
                Assert.AreEqual(preset, box2.State.settle.terrain, "chosen before the first lot is placed");
                Assert.AreEqual(preset, box2.TerrainPreset);
                Assert.Greater(box2.State.settle.version, 0);
                Assert.AreEqual(TerrainSpec.Preset(preset).name, box2.Settlement.Map.spec.name);
                // The camp stands on this land's flat founding spot, not the valley's.
                var camp = box2.Settlement.Lots.First();
                var t = TerrainSpec.Preset(preset);
                Assert.Less(Terrain.Dist(camp.x, camp.z, t.centreX, t.centreZ), 0.5f);
                // A save and a load keeps the land (the int was always in the save).
                var loaded = UnityEngine.JsonUtility.FromJson<SimState>(UnityEngine.JsonUtility.ToJson(box2.State));
                Assert.AreEqual(preset, new SimCore(cfg, loaded).TerrainPreset);
            }
        }

        // ---- the shapes ----

        sealed class Stats
        {
            public int water, trees, rocks, cells;
            public float maxH, p90Slope, waterW, waterD, maxSlope;
            public int aboveSnow;
        }

        static Stats Measure(int preset, int seed = 1)
        {
            var sim = SettleTests.NewBox(seed, preset);
            var m = sim.Settlement.Map;
            var s = new Stats();
            var slopes = new List<float>();
            float x0 = 9, x1 = -9, z0 = 9, z1 = -9;
            for (int c = 0; c < SiteMap.N; c++)
            {
                s.cells++;
                float x = SiteMap.CXc(c), z = SiteMap.CZc(c);
                s.maxH = Math.Max(s.maxH, m.height[c]);
                slopes.Add(m.slope[c]);
                if (m.pond[c]) { s.water++; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); z0 = Math.Min(z0, z); z1 = Math.Max(z1, z); }
                if (m.Forest(c)) s.trees++;
                if (m.Rock(c)) s.rocks++;
                if (m.height[c] > SettingLook.ForPreset(preset).snowLine) s.aboveSnow++;
            }
            slopes.Sort();
            s.p90Slope = slopes[(int)(slopes.Count * 0.9f)];
            s.maxSlope = slopes[^1];
            s.waterW = x1 - x0; s.waterD = z1 - z0;
            return s;
        }

        [Test]
        public void TheDesertIsLowSparseAndHasAnOasis()
        {
            var valley = Measure(0);
            var desert = Measure(TerrainSpec.DesertOasisPreset);
            Assert.Greater(desert.water, 40, "an oasis pond");
            Assert.Less(desert.water, valley.water * 1.0f + 200, "a small one");
            Assert.Less(desert.maxH, valley.maxH - 0.2f, "dunes, not hills");
            Assert.Less(desert.trees, valley.trees * 0.5f, "little forest");
            Assert.Greater(desert.trees, 5, "but palms stand round the water");
            Assert.AreEqual(0, desert.rocks);
            // The palms are round the water: most of the forest cells are within 1.8 u of the pond.
            var t = TerrainSpec.Preset(TerrainSpec.DesertOasisPreset);
            var m = SettleTests.NewBox(1, TerrainSpec.DesertOasisPreset).Settlement.Map;
            int near = 0, all = 0;
            for (int c = 0; c < SiteMap.N; c++)
                if (m.Forest(c)) { all++; if (Terrain.Dist(SiteMap.CXc(c), SiteMap.CZc(c), t.pondX, t.pondZ) < 1.8f) near++; }
            Assert.GreaterOrEqual(near, all * 0.5f, "palms ring the oasis");
        }

        [Test]
        public void TheMountainIsSteepForestedAndRockyWithAStream()
        {
            var valley = Measure(0);
            var mtn = Measure(TerrainSpec.MountainVillagePreset);
            Assert.Greater(mtn.p90Slope, valley.p90Slope * 1.5f, "steep slopes");
            Assert.Greater(mtn.maxH, valley.maxH + 0.4f, "high ground");
            Assert.Greater(mtn.trees, valley.trees * 1.2f, "dense pines");
            Assert.Greater(mtn.rocks, 20, "rock outcrops");
            Assert.Greater(mtn.aboveSnow, 0, "some ground reaches the snow line");
            Assert.Greater(mtn.water, 30, "a stream");
            Assert.Greater(mtn.waterW, mtn.waterD * 1.8f, "long, not round");
        }

        [Test]
        public void TheFoundingSpotIsDryAndFlatOnBothSettings()
        {
            foreach (int preset in new[] { 100, 101 })
            {
                var t = TerrainSpec.Preset(preset);
                Terrain.Land(t, t.centreX, t.centreZ, out float h, out float slope, out float pondDist);
                Assert.Greater(h, Terrain.WaterY + 0.05f, preset.ToString());
                Assert.Less(slope, 0.25f, preset.ToString());
                Assert.Greater(pondDist, 0.6f, "the water is not on the founding spot");
            }
        }

        [Test]
        public void TheSnowLineIsOnlyOnTheMountain()
        {
            Assert.AreEqual(0f, SettingLook.DesertOasis.SnowAmount(3f));
            var m = SettingLook.MountainVillage;
            Assert.AreEqual(0f, m.SnowAmount(m.snowLine - 0.01f));
            Assert.AreEqual(1f, m.SnowAmount(m.snowLine + m.snowBand + 0.01f), 1e-5f);
            // Every setting's props shares make sense.
            foreach (var l in new[] { SettingLook.RiverValley, SettingLook.DesertOasis, SettingLook.MountainVillage })
                Assert.AreEqual(1f, l.palmShare + l.pineShare + l.broadleafShare + l.cactusShare, 1e-4f, l.setting);
        }

        // ---- the land rules still hold on the new settings ----

        [Test]
        public void EverythingStillFindsASiteOnBothSettingsAcrossTwentySeeds()
        {
            int ran = 0;
            foreach (int preset in new[] { 100, 101 })
                for (int seed = 1; seed <= 20; seed++)
                {
                    var sim = SettleTests.NewBox(seed, preset);
                    for (int i = 0; i < 70 * 12; i++)
                    {
                        sim.Step();
                        sim.State.simHours = sim.State.month * sim.Config.StepHours;
                        if (sim.State.eraReady) sim.AdvanceEra();
                    }
                    var s = SettleChecks.Check(sim.Settlement, sim.State.buildings);
                    string tag = $"preset {preset} seed {seed}";
                    Assert.AreEqual(0, s.overlaps, tag + " overlapping lots");
                    Assert.AreEqual(0, s.inMargin, tag + " lots in the glass margin");
                    Assert.AreEqual(0, s.unconnected, tag + " lots without a path");
                    Assert.AreEqual(0, s.unplacedBuildings, tag + " a building has no site");
                    Assert.Greater(sim.State.buildings.Count, 3, tag + " the box built something");
                    // No lot on water or on rock.
                    var m = sim.Settlement.Map;
                    foreach (var l in sim.State.settle.lots)
                    {
                        if (l.open) continue;
                        var cells = new List<int>();
                        SiteMap.RectCells(l.x, l.z, l.yaw, l.w, l.d, 0f, cells);
                        foreach (int c in cells) Assert.IsFalse(m.pond[c] || m.Rock(c), tag + $" lot {l.id} on water or rock");
                    }
                    ran++;
                }
            Assert.AreEqual(40, ran);
        }
    }
}
