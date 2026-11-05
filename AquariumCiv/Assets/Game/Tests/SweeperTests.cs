using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Care moves up (#60, life-in-the-box §7, choice 6 ★): town sweepers really trim overgrowth, slowly
    /// (no trash heaps any more, #313); never the glass; the Keeper always has care left; nothing changes in the camp or the village.</summary>
    public class SweeperTests
    {
        /// <summary>The sim before #60: nobody sweeps (the rates are 0, so every number follows the old code path).</summary>
        internal static SimConfig NoSweepers()
        {
            var c = new SimConfig();
            c.trimPerHour = 0f;
            return c;
        }

        /// <summary>Hour by hour, crossing each era as soon as it's ready, until the box is a town.</summary>
        internal static SimCore ToTown(int seed, SimConfig cfg = null)
        {
            var sim = NewSim(seed, cfg);
            for (int h = 0; h < 300 && sim.State.era != Era.Town; h++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assert.AreEqual(Era.Town, sim.State.era, $"seed {seed} reaches the town");
            return sim;
        }

        static SimCore Copy(SimCore sim, SimConfig cfg) => new SimCore(cfg, JsonUtility.FromJson<SimState>(JsonUtility.ToJson(sim.State)));

        /// <summary>A freshly cleaned town, twice: with its sweepers and without.</summary>
        static (SimCore with, SimCore without) CleanTown(int seed)
        {
            var town = ToTown(seed);
            town.CleanAll();
            return (Copy(town, new SimConfig()), Copy(town, NoSweepers()));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void TownSweepersKeepTheMessLowerButNeverClearIt(int seed)
        {
            var (with, without) = CleanTown(seed);
            Assert.Greater(with.Sweepers, 0, "a town has sweepers");
            for (int h = 1; h <= 48; h++)
            {
                with.Advance(1); without.Advance(1);
                var a = with.State; var b = without.State;
                Assert.Less(a.overgrowth, b.overgrowth, $"seed {seed} h{h}: weeded");
                Assert.Greater(a.overgrowth, 0f, $"seed {seed} h{h}: the weeds keep coming");
                Assert.LessOrEqual(with.MessLevel, without.MessLevel, $"seed {seed} h{h}");
            }
            Assert.LessOrEqual(with.State.overgrowth, 1f);
            TestContext.WriteLine($"seed {seed} 48 h: overgrowth {with.State.overgrowth:0.000} vs {without.State.overgrowth:0.000}, " +
                                  $"mess {with.MessLevel:0.000} vs {without.MessLevel:0.000}");
        }

        [Test]
        public void TheGlassIsAlwaysTheKeepers()
        {
            var (with, without) = CleanTown(7);
            for (int h = 1; h <= 60; h++)
            {
                with.Advance(1); without.Advance(1);
                Assert.AreEqual(without.State.grime, with.State.grime, $"h{h}: grime untouched");
                Assert.AreEqual(Math.Min(1f, h / 48f), with.State.grime, 0.002f, $"h{h}: grime fills in 48 h as ever");
            }
            Assert.AreEqual(1f, with.State.grime);
        }

        /// <summary>However many sweepers a town has and however hard they work, they clear at most half the incoming mess:
        /// two days of neglect still leave most of the mess for the Keeper (and all the glass).</summary>
        [Test]
        public void TheKeeperAlwaysHasCareLeft()
        {
            var tireless = new SimConfig { trimPerHour = 0.5f }; // 100x the real rate
            var town = Copy(ToTown(1234), tireless);
            foreach (var n in town.State.notables.Where(n => n.alive && n.age >= 16f && n.age < town.Config.retireAge))
                town.DebugSetJob(n, JobType.Sweeper); // every working notable sweeps
            town.State.population = 60;               // ... and a big crowd puts out 5 more
            town.CleanAll();
            Assert.GreaterOrEqual(town.Sweepers, 10);
            for (int h = 1; h <= 48; h++)
            {
                Assert.Greater(town.Sweepers, 0);
                Assert.AreEqual(town.Config.careMaxShare / town.Config.messFullHours, town.TrimPerHour, 1e-6f, $"h{h}: weeds capped at half the inflow");
                town.Advance(1);
            }
            Assert.AreEqual(0.5f, town.State.overgrowth, 0.01f, "half the weeds still grow");
            Assert.GreaterOrEqual(town.MessLevel, 0.75f, "most of the mess is still the Keeper's");
            TestContext.WriteLine($"{town.Sweepers} sweepers, 48 h: overgrowth {town.State.overgrowth:0.000}, mess {town.MessLevel:0.000}");
        }

        /// <summary>Nobody sweeps in the camp or the village: with sweepers 100× busier, both are bit-for-bit the sim before
        /// #60 (same runtime).</summary>
        [Test]
        public void TheCampAndTheVillageAreUnchanged()
        {
            var busy = new SimConfig { trimPerHour = 0.5f, careMaxShare = 1f };
            var camp = NewSim(1234, busy); var campBefore = NewSim(1234, NoSweepers());
            camp.Advance(48); campBefore.Advance(48);
            Assert.AreEqual(Era.Camp, camp.State.era);
            Assert.AreEqual(0, camp.Sweepers);
            Assert.AreEqual(JsonUtility.ToJson(campBefore.State), JsonUtility.ToJson(camp.State), "camp");

            var village = NewSim(77, busy); var villageBefore = NewSim(77, NoSweepers());
            for (int m = 0; m < 12 * 60; m++)
            {
                village.Step(); villageBefore.Step();
                if (m == 12 * 5) { village.ForceNextEra(); villageBefore.ForceNextEra(); }
            }
            Assert.AreEqual(Era.Village, village.State.era);
            Assert.AreEqual(0, village.Sweepers);
            Assert.AreEqual(JsonUtility.ToJson(villageBefore.State), JsonUtility.ToJson(village.State), "village");
            Assert.IsFalse(village.State.chronicle.Any(e => e.kind == "sweepers"));
        }

        [Test]
        public void SweepingIsDeterministic()
        {
            var a = ToTown(7); var b = ToTown(7);
            a.CleanAll(); b.CleanAll();
            a.Advance(60); b.Advance(60);
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State));
        }

        static string Care(SimState s) => s.population + "|" + s.grime.ToString("R") + "|" + s.overgrowth.ToString("R") + "|" +
                                         string.Join("/", s.chronicle.Select(c => c.text));

        [Test]
        public void CatchUpSweepsLikeLivePlay()
        {
            var town = ToTown(1234);
            town.CleanAll();
            var offline = Copy(town, new SimConfig()); var live = Copy(town, new SimConfig());
            var r = offline.CatchUp(6);
            Assert.IsFalse(r.Slowed);
            for (int i = 0; i < 6 * 60; i++) live.Advance(1.0 / 60); // one call per real minute
            Assert.AreEqual(Care(live.State), Care(offline.State));
            var nobody = Copy(town, NoSweepers());
            nobody.CatchUp(6);
            Assert.Less(offline.State.overgrowth, nobody.State.overgrowth, "they swept while you were away");

            // A long absence: the unlived hours are topped up as mess, and the sweepers work those real hours too.
            var away = Copy(town, new SimConfig()); var awayBefore = Copy(town, NoSweepers());
            away.CatchUp(30); awayBefore.CatchUp(30);
            Assert.AreEqual(awayBefore.State.grime, away.State.grime, "the glass is the Keeper's");
            Assert.Less(away.State.overgrowth, awayBefore.State.overgrowth);
            Assert.Greater(away.State.overgrowth, 0.3f);
        }

        /// <summary>#313: the trash heaps are gone. An old save that still carries the field loads (JsonUtility drops it) and
        /// no heap ever turns up, whatever the box does.</summary>
        [Test]
        public void OldSavesWithTrashLoadAndNoTrashAppears()
        {
            var sim = ToTown(7);
            string json = JsonUtility.ToJson(sim.State).TrimEnd('}') +
                          ",\"trash\":[{\"id\":99,\"slot\":1,\"offsetX\":0.1,\"offsetZ\":0.1,\"month\":3,\"x\":1.0,\"z\":1.0}],\"trashAccum\":0.5,\"sweepAccum\":0.5}";
            var loaded = new SimCore(new SimConfig(), JsonUtility.FromJson<SimState>(json));
            loaded.Advance(48);
            StringAssert.DoesNotContain("trash", JsonUtility.ToJson(loaded.State), "nothing of the old heaps is kept or saved again");
        }

        [Test]
        public void TheFirstSweepersMakeTheChronicleOnce()
        {
            var sim = ToTown(1);
            Assert.IsFalse(sim.State.chronicle.Any(e => e.kind == "sweepers" && e.month < sim.State.eraStartMonth), "not before the town");
            sim.Advance(30);
            var lines = sim.State.chronicle.Where(e => e.kind == "sweepers").ToList();
            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("has sweepers now", lines[0].text);
            StringAssert.DoesNotContain("glass", lines[0].text);
            Assert.LessOrEqual(lines[0].month - sim.State.eraStartMonth, 1, "the first month of the town");
        }

        [Test]
        public void DebugSweepTrimsWeedsInTheTownOnly()
        {
            var camp = NewSim(3);
            camp.Advance(10);
            Assert.IsFalse(camp.DebugSweep());
            var town = ToTown(7);
            town.Advance(5);
            float weeds = town.State.overgrowth;
            float grime = town.State.grime;
            Assert.IsTrue(town.DebugSweep());
            Assert.Less(town.State.overgrowth, weeds);
            Assert.AreEqual(grime, town.State.grime);
        }
    }
}
