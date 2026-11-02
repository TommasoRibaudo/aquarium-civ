using System.Linq;
using AquariumCiv.Game;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>#97: the notification's time. A copy runs ahead under the offline rule; a real catch-up must land on that time.</summary>
    public class RunAheadTests
    {
        /// <summary>The copy Runtime will make: a round trip of the state through the save's own serialiser.</summary>
        static SimState Copy(SimState s) => JsonUtility.FromJson<SimState>(JsonUtility.ToJson(s));

        static SimCore Loaded(SimCore sim) => new SimCore(sim.Config, Copy(sim.State));

        /// <summary>A box closed part way through a month, some hours in.</summary>
        static SimCore ClosedAfter(int seed, double hours)
        {
            var sim = NewSim(seed);
            sim.Advance(hours);
            return sim;
        }

        /// <summary>A town whose peak hasn't come yet.</summary>
        static SimCore TownBeforeItsPeak(int seed, SimConfig cfg)
        {
            var sim = NewSim(seed, cfg);
            for (int y = 0; y < 90 && sim.State.era != Era.Town; y++)
            {
                sim.Advance(1);
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            Assume.That(sim.State.era, Is.EqualTo(Era.Town), $"seed {seed} made a town");
            return sim;
        }

        static void AssertItLandsOn(SimCore sim, RunAheadResult r, bool peak)
        {
            bool Ready(SimState s) => peak ? s.peakReady : s.eraReady;
            long Month(SimState s) => peak ? s.peakReadyMonth : s.eraReadyMonth;

            Assert.IsTrue(r.Found);
            var at = Loaded(sim);
            at.CatchUp(r.realHours);
            Assert.IsTrue(Ready(at.State), $"a catch-up of exactly {r.realHours} h makes it ready");
            Assert.AreEqual(r.month, Month(at.State), "in the month the run ahead said");
            Assert.AreEqual(r.month, at.State.month, "and that is the catch-up's last month");

            // The time is the first that does it: a minute short, it isn't ready yet.
            if (r.realHours > 1.0 / 60)
            {
                var earlier = Loaded(sim);
                earlier.CatchUp(r.realHours - 1.0 / 60);
                Assert.IsFalse(Ready(earlier.State), "a minute earlier it isn't ready");
            }

            // Staying away longer doesn't move the month it became ready.
            var later = Loaded(sim);
            later.CatchUp(r.realHours + 20);
            Assert.IsTrue(Ready(later.State));
            Assert.AreEqual(r.month, Month(later.State), "a longer absence reads the same month");
        }

        [Test]
        public void TheTimeMatchesARealCatchUp()
        {
            int full = 0, slow = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var sim = ClosedAfter(seed, 2.0 + seed * 0.37);
                var r = sim.RunAhead(Copy);
                if (r.miss == RunAheadMiss.AlreadyWaiting) continue;
                Assert.AreEqual(RunAheadTarget.NextEra, r.target, $"seed {seed}: {r.miss}");
                Assert.AreEqual(Era.Village, r.era);
                AssertItLandsOn(sim, r, peak: false);
                if (r.realHours <= sim.Config.fullSpeedHours) full++; else slow++;
            }
            TestContext.WriteLine($"ready inside the full-speed hours: {full}, in the slow tail: {slow}");
            Assert.Greater(full + slow, 5, "enough boxes were still waiting to mean something");
        }

        [Test]
        public void TheSlowTailCountsFourRealHoursToTheSimHour()
        {
            var tested = 0;
            for (int seed = 1; seed <= 20 && tested < 3; seed++)
            {
                var sim = ClosedAfter(seed, 0.5);
                var r = sim.RunAhead(Copy);
                if (!r.Found || r.simHours <= sim.Config.fullSpeedHours) continue;
                Assert.AreEqual(8 + (r.simHours - 8) * 4, r.realHours, 1e-9);
                AssertItLandsOn(sim, r, peak: false);
                tested++;
            }
            Assert.Greater(tested, 0, "some box was ready only in the slow tail");
        }

        [Test]
        public void TheNextEraAfterAVillageAndThePeakMatchToo()
        {
            int peaks = 0, towns = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var sim = NewSim(seed);
                for (int y = 0; y < 60 && sim.State.era == Era.Camp; y++) { sim.Advance(1); if (sim.State.eraReady) sim.AdvanceEra(); }
                if (sim.State.era != Era.Village || sim.State.eraReady) continue;
                var r = sim.RunAhead(Copy);
                if (!r.Found) continue;
                Assert.AreEqual(Era.Town, r.era);
                AssertItLandsOn(sim, r, peak: false);
                towns++;
            }
            for (int seed = 1; seed <= 12 && peaks < 3; seed++)
            {
                var sim = NewSim(seed);
                for (int y = 0; y < 90 && sim.State.era != Era.Town; y++) { sim.Advance(1); if (sim.State.eraReady) sim.AdvanceEra(); }
                if (sim.State.era != Era.Town || sim.State.peakReady) continue;
                var r = sim.RunAhead(Copy);
                if (!r.Found) continue;
                Assert.AreEqual(RunAheadTarget.Peak, r.target);
                AssertItLandsOn(sim, r, peak: true);
                peaks++;
            }
            Assert.Greater(towns, 0, "some village became a town");
            Assert.Greater(peaks, 0, "some town reached its peak");
        }

        [Test]
        public void TheRunIsTheSameFromInMemoryAndFromALoadedSave()
        {
            // Back from the background the app catches up on the sim in memory; after a kill, on the loaded save.
            var sim = ClosedAfter(5, 3.3);
            var r = sim.RunAhead(Copy);
            Assert.IsTrue(r.Found);
            var inMemory = ClosedAfter(5, 3.3);
            inMemory.CatchUp(r.realHours);
            Assert.AreEqual(r.month, inMemory.State.eraReadyMonth);
        }

        [Test]
        public void TheRealSaveIsNotTouched()
        {
            var sim = ClosedAfter(3, 2.5);
            int events = 0;
            sim.EraReadied += _ => events++;
            sim.Chronicled += _ => events++;
            sim.PeakReadied += () => events++;
            string before = JsonUtility.ToJson(sim.State);
            var r = sim.RunAhead(Copy);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(before, JsonUtility.ToJson(sim.State));
            Assert.AreEqual(0, events);
            Assert.IsFalse(sim.CatchingUp);
        }

        [Test]
        public void ItIsDeterministic()
        {
            var a = ClosedAfter(9, 4.1).RunAhead(Copy);
            var b = ClosedAfter(9, 4.1).RunAhead(Copy);
            Assert.AreEqual(a.month, b.month);
            Assert.AreEqual(a.realHours, b.realHours);
            Assert.AreEqual(a.monthsRun, b.monthsRun);
        }

        [Test]
        public void NothingToTellWhenItAlreadyWaits()
        {
            var sim = NewSim();
            sim.ForceEraReady();
            bool copied = false;
            var r = sim.RunAhead(s => { copied = true; return Copy(s); });
            Assert.IsFalse(r.Found);
            Assert.AreEqual(RunAheadMiss.AlreadyWaiting, r.miss);
            Assert.IsFalse(copied, "no cost when there is nothing to find");
        }

        [Test]
        public void NothingToTellForAMuseumOrAWaitingPeak()
        {
            var sim = NewSim();
            sim.ForceCompleteBox();
            Assert.AreEqual(RunAheadMiss.BoxComplete, sim.RunAhead(Copy).miss);

            var town = NewSim();
            town.ForceNextEra(); town.ForceNextEra();
            Assert.AreEqual(Era.Town, town.State.era);
            town.ForcePeakReady();
            Assert.AreEqual(RunAheadMiss.AlreadyWaiting, town.RunAhead(Copy).miss);
        }

        [Test]
        public void TheCostIsBoundedByTheLongestAbsence()
        {
            // A threshold nothing can reach: the run must stop at the bound, and the bound is what 30 days away can live.
            var cfg = new SimConfig { villageAt = new EraThreshold { population = 100000, buildings = 100000, years = 100000 } };
            var sim = NewSim(4, cfg);
            sim.Advance(1.5);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = sim.RunAhead(Copy);
            sw.Stop();
            TestContext.WriteLine($"worst case: {r.monthsRun} months in {sw.ElapsedMilliseconds} ms");
            Assert.IsFalse(r.Found);
            Assert.AreEqual(RunAheadMiss.NotInTime, r.miss);
            Assert.AreEqual(2232, sim.RunAheadMaxMonths);
            Assert.AreEqual(sim.RunAheadMaxMonths, r.monthsRun);
            Assert.AreEqual(SaveStore.MaxAwayHours, SimCore.RunAheadMaxAwayHours);

            // The dearest case: a full town that never peaks, 30 days ahead.
            var townCfg = new SimConfig { peakAt = new PeakRule { townYears = 100000, seats = 7, fullYears = 3 } };
            var town = TownBeforeItsPeak(2, townCfg);
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var t = town.RunAhead(Copy);
            sw2.Stop();
            TestContext.WriteLine($"a town: {t.miss} after {t.monthsRun} months in {sw2.ElapsedMilliseconds} ms, {town.State.population} people");
            Assert.AreEqual(sim.RunAheadMaxMonths, t.monthsRun);
        }

        [TestCase(0, 0)]
        [TestCase(5, 5)]
        [TestCase(8, 8)]
        [TestCase(18, 48)]
        [TestCase(48, 168)]
        public void RealHoursUndoTheOfflineRule(double sim, double real)
        {
            var c = new SimConfig();
            Assert.AreEqual(real, c.RealHoursFor(sim), 1e-9);
            Assert.AreEqual(sim, c.OfflineSimHours(c.RealHoursFor(sim)), 1e-9);
        }
    }
}
