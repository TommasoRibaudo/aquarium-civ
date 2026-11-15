using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using AquariumCiv.Sim.Eco;
using AquariumCiv.Sim.Replay;
using AquariumCiv.Sim.Settle;
using NUnit.Framework;
using UnityEngine;
using Terrain = AquariumCiv.Sim.Settle.Terrain;

namespace AquariumCiv.Tests
{
    /// <summary>#658: every box is 6 x 6 (60 x 60 cells); a box saved at 60 x 44 grows its 16 back rows when it loads
    /// (Sim/BoxGrid.cs, save v4). The v3 fixture is made here from a real played box by cutting every per-cell array back to 2640 cells
    /// (the exact shape a v3 save has), so the test needs no binary file.</summary>
    public class BoxGrowTests
    {
        const int Y = SimConfig.MonthsPerYear, Old = BoxGrid.OldN;
        static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        string dir, path;

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "aqciv-grow-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(dir, "box.json");
        }
        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static SimConfig Cfg() => new SimConfig { ecology = true };

        /// <summary>A played box: three misted spots a season, a village with lots, and a land history.</summary>
        static SimCore Played(int seed, int years)
        {
            var s = new SimCore(Cfg(), SimCore.NewWorld(seed, Cfg()));
            for (int m = 0; m < years * Y; m++)
            {
                if (m % 3 == 0 && s.EcologyOn)
                    for (int k = 0; k < 3; k++)
                    {
                        int cell = (m / 3 * 211 + k * 877 + 301) % Old; // the old land only: a v3 box never touched the back
                        s.Mist(SiteMap.CXc(cell), SiteMap.CZc(cell), 2f);
                    }
                s.Step();
                if (s.State.eraReady) s.AdvanceEra();
            }
            // the lab Keeper is not here: push the box on by hand so a village, yards and paths stand on the land
            for (int k = 0; k < 2; k++) s.ForceNextEra();
            for (int k = 0; k < 6; k++) { s.AddBuildingAt(k % 2 == 0 ? BuildingType.Hut : BuildingType.Farm, 2 + k); s.Step(); }
            for (int m = 0; m < 12 * Y; m++) { s.Step(); if (s.State.eraReady) s.AdvanceEra(); }
            EcoSeen.Mark(s.State.eco);
            return s;
        }

        static T[] Cut<T>(T[] a) => a == null ? null : a.Take(Old).ToArray();

        static byte[] Rle(byte[] a)
        {
            var o = new List<byte>();
            for (int i = 0; i < a.Length;)
            {
                byte v = a[i]; int n = 1;
                while (i + n < a.Length && a[i + n] == v && n < 255) n++;
                o.Add(v); o.Add((byte)n); i += n;
            }
            return o.ToArray();
        }

        /// <summary>Turns a played 6 x 6 state into what a v3 save held: 2640 cells everywhere, the land history cut the same way.</summary>
        static SimState AsV3(SimState src)
        {
            var s = JsonUtility.FromJson<SimState>(JsonUtility.ToJson(src));
            var t = s.settle; var e = s.eco;
            t.flags = Cut(t.flags); t.tier = Cut(t.tier); t.wear = Cut(t.wear); t.dirC = Cut(t.dirC); t.dirS = Cut(t.dirS);
            t.lot = Cut(t.lot); t.ring = Cut(t.ring); t.pathMonth = Cut(t.pathMonth);
            e.cover = Cut(e.cover); e.soil = Cut(e.soil); e.moisture = Cut(e.moisture);
            e.wet = Cut(e.wet); e.puddle = Cut(e.puddle); e.sprout = Cut(e.sprout); e.watered = Cut(e.watered);
            e.seen = e.seen.Take((Old + 7) / 8).ToArray();
            foreach (var step in s.history.land)
            {
                var full = new byte[SiteMap.N];
                int p = 0; var rle = Convert.FromBase64String(step.cover);
                for (int i = 0; i + 1 < rle.Length; i += 2) for (int n = 0; n < rle[i + 1]; n++) full[p++] = rle[i];
                step.cover = Convert.ToBase64String(Rle(full.Take(Old).ToArray()));
            }
            return s;
        }

        static void WriteV3(string path, SimState v3)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new SaveStore.Envelope
            {
                v = 3, lastSeenUtc = Now.ToString("o"), state = v3, shelf = ShelfState.Single(v3).ForSave(),
            }));
        }

        [Serializable] class LotsBox { public List<Lot> lots; public List<Yard> yards; public List<Commons> commons; }

        [Test]
        public void TheLand_Is60By60_AndTheFrontRowsAreWhereTheyWere()
        {
            Assert.AreEqual(60, SiteMap.NZ); Assert.AreEqual(3600, SiteMap.N); Assert.AreEqual(3600, EcoLand.N);
            Assert.AreEqual(-2.2f, SiteMap.Z0, 1e-6f);
            Assert.AreEqual(-2.2f, Terrain.ZMin, 1e-6f); Assert.AreEqual(3.8f, Terrain.ZMax, 1e-6f);
            Assert.AreEqual(Terrain.ZMin + Terrain.HZ, Terrain.CZ, 1e-6f); Assert.AreEqual(Terrain.ZMax - Terrain.HZ, Terrain.CZ, 1e-6f);
            Assert.AreEqual(-2.15f, SiteMap.CZ(0), 1e-5f, "row 0 is still the south row");
            Assert.AreEqual(Terrain.ZMax - 0.05f, SiteMap.CZ(59), 1e-4f);
            Assert.IsTrue(Terrain.InZ(3.7f, 0.05f)); Assert.IsFalse(Terrain.InZ(3.7f, 0.25f)); Assert.IsFalse(Terrain.InZ(-2.1f, 0.25f));
            Assert.AreEqual(Terrain.ZMax - 0.3f, Terrain.ClampZ(99f, 0.3f), 1e-6f); Assert.AreEqual(Terrain.ZMin + 0.3f, Terrain.ClampZ(-99f, 0.3f), 1e-6f);
        }

        [Test]
        public void AV3Save_LoadsGrowsKeepsEveryLotAndCoverAndRoundTrips()
        {
            var full = Played(11, 30);
            Assert.Greater(full.State.settle.lots.Count, 3, "a village stands");
            var v3 = AsV3(full.State);
            Assert.AreEqual(Old, v3.settle.lot.Length); Assert.AreEqual(Old, v3.eco.cover.Length); Assert.AreEqual(330, v3.eco.seen.Length);
            string lotsBefore = JsonUtility.ToJson(new LotsBox { lots = v3.settle.lots, yards = v3.settle.yards, commons = v3.settle.commons });
            var coverBefore = v3.eco.cover.ToArray(); var lotBefore = v3.settle.lot.ToArray(); var soilBefore = v3.eco.soil.ToArray();
            var histBefore = v3.history.land.Select(l => l.cover).ToList();

            WriteV3(path, v3);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var env));
            Assert.AreEqual(SaveStore.CurrentVersion, env.v, "migrated to v4");
            Assert.AreEqual(Old, env.state.settle.lot.Length, "the migration only bumps the version: the grow is the SimCore's");

            var sim = new SimCore(Cfg(), env.state);
            var s = sim.State; var t = s.settle; var e = s.eco;
            // padded to 3600, the old cells untouched
            foreach (var a in new Array[] { t.flags, t.tier, t.wear, t.dirC, t.dirS, t.lot, t.ring, t.pathMonth, e.cover, e.soil, e.moisture, e.wet, e.puddle, e.sprout, e.watered })
                Assert.AreEqual(SiteMap.N, a.Length);
            Assert.AreEqual(450, e.seen.Length);
            Assert.AreEqual(lotsBefore, JsonUtility.ToJson(new LotsBox { lots = t.lots, yards = t.yards, commons = t.commons }), "every lot, yard and commons kept");
            CollectionAssert.AreEqual(lotBefore, t.lot.Take(Old).ToArray(), "every lot cell kept");
            CollectionAssert.AreEqual(coverBefore, e.cover.Take(Old).ToArray(), "every cover cell kept");
            CollectionAssert.AreEqual(soilBefore, e.soil.Take(Old).ToArray());
            CollectionAssert.AreEqual(histBefore, s.history.land.Select(l => l.cover).ToList(), "the saved history is not rewritten");
            // the new land: bare, dry, base soil, no lots, unseen
            var tuning = sim.Config.eco;
            var land = sim.EcoGround;
            for (int c = Old; c < SiteMap.N; c++)
            {
                Assert.AreEqual(0, e.cover[c], "bare");
                Assert.AreEqual(0, t.lot[c], "no lot"); Assert.AreEqual(0, t.flags[c]); Assert.AreEqual(0, t.tier[c]);
                Assert.AreEqual(tuning.soilStart + tuning.soilStartBase * land.baseSoil[c], e.soil[c], 1e-6f);
                Assert.AreEqual(0.04f, e.moisture[c], 1e-6f);
                Assert.AreEqual(0f, e.wet[c]); Assert.AreEqual(0f, e.puddle[c]); Assert.AreEqual(0f, e.sprout[c]); Assert.AreEqual(0f, e.watered[c]);
                Assert.IsFalse(EcoSeen.IsSeen(e, c), "unseen");
            }
            for (int c = 0; c < Old; c++) Assert.AreEqual(EcoSeen.IsSeen(full.State.eco, c), EcoSeen.IsSeen(e, c), "the old cells keep their seen bit");
            Assert.IsTrue(EcoSeen.HasMask(e), "the seen mask is still a mask");
            Assert.AreEqual(full.State.eco.wet.Take(Old).Sum(), e.wet.Take(Old).Sum(), 1e-3f, "the live layers were not wiped");

            // the replay: every recorded year reads 3600 cells, the old ones as they were, the back bare
            var lapse = new TimeLapse(sim);
            for (int y = 0; y <= lapse.MaxYear; y++)
            {
                var cov = lapse.Frame(y).land.cover;
                Assert.AreEqual(SiteMap.N, cov.Length, "year " + y);
                for (int c = Old; c < cov.Length; c++) if (cov[c] != 0) Assert.Fail($"year {y}: back cell {c} is {cov[c]}, not bare");
            }

            // the village walks on: the settlement and the ecology run on the grown land
            Assert.AreEqual(lotBefore.Count(l => l != 0), sim.Settlement.Map.lot.Take(Old).Count(l => l != 0), "the map reads the grown arrays");
            for (int m = 0; m < 3 * Y; m++) { sim.Step(); if (sim.State.eraReady) sim.AdvanceEra(); }
            for (int c = 0; c < Old; c++) if (lotBefore[c] != 0) Assert.AreNotEqual(0, t.lot[c], $"lot cell {c} still held");

            // a second load is a no-op, and the grown save round-trips
            string once = JsonUtility.ToJson(env.state);
            Assert.IsFalse(BoxGrid.Grow(env.state, () => sim.EcoGround, sim.Config.eco), "already full size");
            Assert.AreEqual(once, JsonUtility.ToJson(env.state));
            SaveStore.Write(path, env.state, Now);
            Assert.AreEqual(SaveStore.LoadResult.Loaded, SaveStore.Read(path, out var again));
            Assert.AreEqual(4, again.v);
            Assert.AreEqual(once, JsonUtility.ToJson(again.state), "round trip");
            var sim2 = new SimCore(Cfg(), again.state);
            Assert.AreEqual(once, JsonUtility.ToJson(sim2.State), "a second load changes nothing");
        }

        [Test]
        public void ANewBox_IsFullSizeAndGrowIsANoOp()
        {
            var sim = new SimCore(Cfg(), SimCore.NewWorld(5, Cfg()));
            Assert.AreEqual(SiteMap.N, sim.State.eco.cover.Length);
            Assert.AreEqual(SiteMap.N, sim.State.settle.lot.Length);
            Assert.IsFalse(BoxGrid.Grow(sim.State, () => sim.EcoGround, sim.Config.eco));
        }

        /// <summary>Unity's JsonUtility loads an absent array as an empty one, not null: an empty seen mask, layer or settle array is
        /// "not recorded" and stays empty, or a save with no seen mask comes back from a load with one (all zeros).</summary>
        [Test]
        public void EmptyArrays_AreNotRecorded_AndStayEmpty()
        {
            var sim = new SimCore(Cfg(), SimCore.NewWorld(5, Cfg()));
            var e = sim.State.eco;
            e.seen = Array.Empty<byte>(); e.puddle = new float[0];
            sim.State.settle.pathMonth = new int[0];
            Assert.IsFalse(BoxGrid.Grow(sim.State, () => sim.EcoGround, sim.Config.eco), "nothing to grow");
            Assert.AreEqual(0, e.seen.Length, "no seen mask stays no seen mask");
            Assert.IsFalse(EcoSeen.HasMask(e));
            Assert.AreEqual(0, e.puddle.Length);
            Assert.AreEqual(0, sim.State.settle.pathMonth.Length);
        }

        [Test]
        public void ABoxWithoutTheEcology_AndAnEmptyState_AreLeftAlone()
        {
            var cfg = new SimConfig();
            var sim = new SimCore(cfg, SimCore.NewWorld(3, cfg));
            Assert.DoesNotThrow(() => BoxGrid.Grow(sim.State, () => sim.EcoGround, cfg.eco));
            Assert.IsFalse(BoxGrid.Grow(new SimState(), null, null));
            Assert.IsFalse(BoxGrid.Grow(null, null, null));
        }

        [Test]
        public void SaveVersion_Is4_SoAnOlderBuildRefusesIt()
        {
            Assert.AreEqual(4, SaveStore.CurrentVersion);
            var sim = new SimCore(Cfg(), SimCore.NewWorld(5, Cfg()));
            SaveStore.Write(path, sim.State, Now);
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"v\":4", "\"v\":5"));
            Assert.AreEqual(SaveStore.LoadResult.TooNew, SaveStore.Read(path, out _), "a build that only knows v3 reads v4 the same way: refused, never overwritten");
        }
    }
}
