using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Tests
{
    /// <summary>The slice's sim: determinism, catch-up, time, and the care layer as Gate 1 decided it.</summary>
    public class SimCoreTests
    {
        internal static SimCore NewSim(int seed = 42, SimConfig cfg = null)
        {
            cfg ??= new SimConfig();
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        internal static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }

        internal static uint Fingerprint(SimState s)
        {
            string fp = s.population + "|" + s.month + "|" + s.rng.state + "|" + s.econRng.state + "|" + s.storyRng.state + "|" +
                        string.Join(",", s.buildings.Select(b => b.type + b.slot)) + "|" + string.Join("/", s.chronicle.Select(c => c.text)) + "|" +
                        string.Join(",", s.notables.Select(n => n.FullName + n.age.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            return Fnv(fp);
        }

        /// <summary>A fingerprint of the slice world (seed 1234, 48 h). If this fails, a change altered the simulation:
        /// fine when meant (update the number in the same commit and say why), a bug when not.
        /// Eras (#23): nobody watches here, so the box stays a camp at its plateau and builds only camp types, and the
        /// chronicle notes that the village is ready.
        /// Life stages (#43): elders now start at 90, so nobody in these 48 years takes an apprentice.
        /// Notable counts (#26): the camp starts with 3 notables and births stop at 4 (was 5 and 12).</summary>
        [Test]
        public void SliceWorldFingerprint()
        {
            var sim = NewSim(1234);
            sim.Advance(48);
            Assert.AreEqual(Era.Camp, sim.State.era);
            Assert.IsTrue(sim.State.eraReady, "the camp waits at the village threshold");
            Assert.AreEqual(3366573008u, Fingerprint(sim.State), "slice world fingerprint");
        }

        [Test]
        public void SameSeedProducesSameWorld()
        {
            var a = NewSim(); var b = NewSim();
            a.Advance(48); b.Advance(48);
            Assert.AreEqual(JsonUtility.ToJson(a.State), JsonUtility.ToJson(b.State));
        }

        [Test]
        public void CatchUpEqualsLiveStepping()
        {
            var offline = NewSim(); var live = NewSim();
            offline.Advance(48);
            for (int i = 0; i < 48 * 60; i++) live.Advance(1.0 / 60); // one call per real minute
            Assert.AreEqual(JsonUtility.ToJson(offline.State.buildings), JsonUtility.ToJson(live.State.buildings));
            Assert.AreEqual(offline.State.population, live.State.population);
            Assert.AreEqual(string.Join("\n", offline.State.chronicle.Select(e => e.text)), string.Join("\n", live.State.chronicle.Select(e => e.text)));
        }

        [Test]
        public void OneRealHourIsOneGameYear()
        {
            var sim = NewSim();
            sim.Advance(1);
            Assert.AreEqual(12, sim.State.month);
        }

        [Test]
        public void AddMessIsCappedLikeNaturalMess()
        {
            var sim = NewSim();
            sim.AddMess(0.7f, 0.7f, 8);
            sim.AddMess(0.7f, 0.7f, 8);
            Assert.AreEqual(1f, sim.State.grime);
            Assert.AreEqual(1f, sim.State.overgrowth);
            Assert.AreEqual(sim.Config.trashMax, sim.State.trash.Count);
            Assert.AreEqual(sim.State.trash.Count, sim.State.trash.Select(t => t.id).Distinct().Count());
            sim.CleanAll();
            Assert.AreEqual(0f, sim.MessLevel);
        }

        [Test]
        public void MessIsCappedAndNothingIsLost()
        {
            var sim = NewSim();
            sim.Advance(48);
            int buildings = sim.State.buildings.Count;
            sim.Advance(24 * 30); // a month of neglect
            Assert.LessOrEqual(sim.State.grime, 1f);
            Assert.LessOrEqual(sim.State.overgrowth, 1f);
            Assert.LessOrEqual(sim.State.trash.Count, sim.Config.trashMax);
            Assert.GreaterOrEqual(sim.State.buildings.Count, buildings, "neglect must never remove buildings");
            Assert.GreaterOrEqual(sim.State.population, 5, "the civ never collapses");
        }

        [Test]
        public void WorkPaceDropIsLinearAndCappedAtAQuarter()
        {
            var sim = NewSim();
            sim.State.trash.Clear();
            for (int i = 0; i < sim.Config.trashMax / 2; i++) sim.State.trash.Add(new TrashItem { id = 1000 + i });
            sim.State.grime = sim.State.overgrowth = 0.5f; // mess level 0.5
            Assert.AreEqual(0.125f, sim.OutputPenalty, 1e-5f);
            sim.AddMess(1f, 1f, sim.Config.trashMax);
            Assert.AreEqual(0.25f, sim.OutputPenalty, 1e-5f);
            Assert.AreEqual(75, CareConfig.WorkPacePercent(sim));
            Assert.AreEqual(0, CareConfig.SunlightPercent(sim));
        }

        [Test]
        public void GlassIsHalfTheMess()
        {
            var sim = NewSim();
            sim.State.trash.Clear();
            sim.State.overgrowth = 0f;
            sim.State.grime = 1f;
            Assert.AreEqual(0.5f, sim.MessLevel, 1e-5f);
        }

        [Test]
        public void MessNeverTouchesMood()
        {
            // Gate 1: the pace drop is the whole cost. No trash: it would draw from the rng differently in the two worlds.
            var cfg = new SimConfig { trashPerPersonHour = 0f };
            var clean = NewSim(7, cfg); var messy = NewSim(7, cfg);
            for (int i = 0; i < 12; i++)
            {
                clean.CleanAll(); messy.State.grime = messy.State.overgrowth = 1f;
                clean.Step(); messy.Step();
            }
            Assert.AreEqual(clean.State.mood, messy.State.mood, 1e-5f);
            Assert.IsFalse(messy.State.chronicle.Any(e => e.kind == "grumble"), "the people never scold the Keeper");
        }

        [Test]
        public void MessSlowsGrowthOnlySlightly()
        {
            int popClean = 0, popMessy = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var clean = NewSim(seed); var messy = NewSim(seed);
                for (int h = 0; h < 48; h++) { clean.CleanAll(); clean.Advance(1); messy.Advance(1); }
                popClean += clean.State.population; popMessy += messy.State.population;
            }
            TestContext.WriteLine($"mean pop clean {popClean / 40f:0.0}, neglected {popMessy / 40f:0.0}");
            Assert.Less(popMessy, popClean, "neglect must slow growth on average");
            Assert.Greater(popMessy, popClean * 0.75f, "...but only slightly");
        }

        [Test]
        public void NotablesAgeWithTheCalendarAndDieOnlyOfOldAge()
        {
            var sim = NewSim();
            var founder = sim.State.notables[0];
            float startAge = founder.age;
            sim.Advance(10); // 10 game years
            Assert.AreEqual(startAge + 10f, founder.age, 1e-3f, "a person's age matches the calendar");
            Assert.GreaterOrEqual(founder.lifespan, 110f, "they live twice as long as the prototypes' people");
            sim.Advance(24 * 14);
            foreach (var n in sim.State.notables.Where(n => !n.alive))
                Assert.GreaterOrEqual(n.age, n.lifespan, $"{n.FullName} died before their lifespan");
            Assert.GreaterOrEqual(sim.State.notables.Count(n => n.alive), sim.NotablesWanted.min - 1);
        }

        [Test]
        public void VillageGrowsOverAFewDays()
        {
            var sim = NewSim();
            sim.Advance(72);
            Assert.Greater(sim.State.population, 10);
            Assert.Greater(sim.State.buildings.Count, 4);
            Assert.Greater(sim.State.chronicle.Count, 5);
        }

        [Test]
        public void AWeekAwayCatchesUpQuickly()
        {
            // §5.2: a long absence must not stall the open. Generous bound for the Editor; the phone measure comes later.
            var sim = NewSim();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            sim.Advance(24 * 7);
            sw.Stop();
            TestContext.WriteLine($"a week away: {sim.State.month} months in {sw.ElapsedMilliseconds} ms");
            Assert.Less(sw.ElapsedMilliseconds, 2000);
        }
    }
}
