using System;
using System.Collections.Generic;
using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>Care moves up (#60, life-in-the-box §7, choice 6 ★): town sweepers really clear trash heaps and trim
    /// overgrowth, slowly; never the glass; the Keeper always has care left; nothing changes in the camp or the village.</summary>
    public class SweeperTests
    {
        /// <summary>The sim before #60: nobody sweeps (the rates are 0, so every number follows the old code path).</summary>
        internal static SimConfig NoSweepers()
        {
            var c = new SimConfig();
            c.sweepHeapsPerHour = 0f;
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
            float trashHoursWith = 0, trashHoursWithout = 0;
            for (int h = 1; h <= 48; h++)
            {
                with.Advance(1); without.Advance(1);
                var a = with.State; var b = without.State;
                trashHoursWith += a.trash.Count; trashHoursWithout += b.trash.Count;
                Assert.Less(a.overgrowth, b.overgrowth, $"seed {seed} h{h}: weeded");
                Assert.Greater(a.overgrowth, 0f, $"seed {seed} h{h}: the weeds keep coming");
                if (h >= 4) Assert.Greater(a.trash.Count, 0, $"seed {seed} h{h}: the heaps keep coming");
                Assert.LessOrEqual(with.MessLevel, without.MessLevel, $"seed {seed} h{h}");
                if (h == 24)
                {
                    Assert.Less(a.trash.Count, b.trash.Count, $"seed {seed}: a day in, fewer heaps");
                    TestContext.WriteLine($"seed {seed} 24 h: trash {a.trash.Count} vs {b.trash.Count}, overgrowth {a.overgrowth:0.000} vs {b.overgrowth:0.000}");
                }
            }
            Assert.Less(trashHoursWith, trashHoursWithout * 0.85f, $"seed {seed}: fewer heaps lying about over the two days");
            Assert.LessOrEqual(with.State.overgrowth, 1f);
            Assert.LessOrEqual(with.State.trash.Count, with.Config.trashMax);
            TestContext.WriteLine($"seed {seed} 48 h: trash {with.State.trash.Count} vs {without.State.trash.Count}, overgrowth " +
                                  $"{with.State.overgrowth:0.000} vs {without.State.overgrowth:0.000}, mess {with.MessLevel:0.000} vs {without.MessLevel:0.000}, " +
                                  $"heap-hours {trashHoursWith} vs {trashHoursWithout}");
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
            var tireless = new SimConfig { sweepHeapsPerHour = 5f, trimPerHour = 0.5f }; // 50x and 100x the real rates
            var town = Copy(ToTown(1234), tireless);
            foreach (var n in town.State.notables.Where(n => n.alive && n.age >= 16f && n.age < town.Config.retireAge))
                town.DebugSetJob(n, JobType.Sweeper); // every working notable sweeps
            town.State.population = 60;               // ... and a big crowd puts out 5 more
            town.CleanAll();
            Assert.GreaterOrEqual(town.Sweepers, 10);
            for (int h = 1; h <= 48; h++)
            {
                Assert.Greater(town.Sweepers, 0);
                Assert.AreEqual(town.Config.careMaxShare * town.State.population * town.Config.trashPerPersonHour, town.SweepHeapsPerHour, 1e-6f, $"h{h}: heaps capped at half the inflow");
                Assert.AreEqual(town.Config.careMaxShare / town.Config.messFullHours, town.TrimPerHour, 1e-6f, $"h{h}: weeds capped at half the inflow");
                town.Advance(1);
                if (h >= 3) Assert.Greater(town.State.trash.Count, 0, $"h{h}");
            }
            Assert.AreEqual(0.5f, town.State.overgrowth, 0.01f, "half the weeds still grow");
            Assert.GreaterOrEqual(town.State.trash.Count, 8, "plenty of heaps to flick");
            Assert.GreaterOrEqual(town.MessLevel, 0.75f, "most of the mess is still the Keeper's");
            TestContext.WriteLine($"{town.Sweepers} sweepers, 48 h: trash {town.State.trash.Count}, overgrowth {town.State.overgrowth:0.000}, mess {town.MessLevel:0.000}");
        }

        /// <summary>Nobody sweeps in the camp or the village: with sweepers 100× busier, both are bit-for-bit the sim before
        /// #60 (same runtime).</summary>
        [Test]
        public void TheCampAndTheVillageAreUnchanged()
        {
            var busy = new SimConfig { sweepHeapsPerHour = 10f, trimPerHour = 0.5f, careMaxShare = 1f };
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
            Assert.Greater(village.State.trash.Count, 0);
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
                                         s.sweepAccum.ToString("R") + "|" + s.trashAccum.ToString("R") + "|" +
                                         string.Join(",", s.trash.Select(t => t.id)) + "|" + string.Join("/", s.chronicle.Select(c => c.text));

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
            Assert.Greater(away.State.trash.Count, 0);
            Assert.LessOrEqual(away.State.trash.Count, away.Config.trashMax);
        }

        [Test]
        public void SweepersHeadForTheOldestHeaps()
        {
            var camp = NewSim(3);
            camp.Advance(10);
            Assert.Greater(camp.State.trash.Count, 0);
            Assert.AreEqual(-1, camp.SweeperTarget(), "nobody sweeps in the camp");
            CollectionAssert.IsEmpty(camp.SweeperTargets());

            var town = ToTown(7);
            town.CleanAll();
            Assert.AreEqual(-1, town.SweeperTarget(), "nothing to sweep");
            int removedSeen = 0;
            for (int m = 0; m < 12 * 20; m++)
            {
                var targets = town.SweeperTargets();
                Assert.AreEqual(Math.Min(town.Sweepers, town.State.trash.Count), targets.Count);
                var oldestFirst = town.State.trash.OrderBy(t => t.month).ThenBy(t => t.id).Select(t => t.id).Take(targets.Count).ToList();
                CollectionAssert.AreEqual(oldestFirst, targets, $"month {m}");
                Assert.AreEqual(targets.Count > 0 ? targets[0] : -1, town.SweeperTarget(0));
                Assert.AreEqual(-1, town.SweeperTarget(town.Sweepers), "one heap per sweeper");
                Assert.That(town.SweepProgress, Is.InRange(0f, 1f));
                var before = town.State.trash.Select(t => t.id).ToList();
                town.Step();
                var gone = before.Except(town.State.trash.Select(t => t.id)).ToList();
                if (gone.Count == 0) continue;
                removedSeen++;
                CollectionAssert.AreEqual(targets.Take(gone.Count).ToList(), gone, $"month {m}: the heap they were heading to went");
            }
            Assert.GreaterOrEqual(removedSeen, 2, "the sweepers cleared heaps");
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
        public void DebugSweepClearsAHeapInTheTownOnly()
        {
            var camp = NewSim(3);
            camp.Advance(10);
            Assert.IsFalse(camp.DebugSweep());
            var town = ToTown(7);
            town.Advance(5);
            int heaps = town.State.trash.Count;
            int target = town.SweeperTarget();
            float weeds = town.State.overgrowth;
            float grime = town.State.grime;
            Assert.IsTrue(town.DebugSweep());
            Assert.AreEqual(heaps - 1, town.State.trash.Count);
            Assert.IsFalse(town.State.trash.Any(t => t.id == target));
            Assert.Less(town.State.overgrowth, weeds);
            Assert.AreEqual(grime, town.State.grime);
        }
    }
}
