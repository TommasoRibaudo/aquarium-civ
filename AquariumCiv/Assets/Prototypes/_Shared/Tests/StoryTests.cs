using System.Linq;
using AquariumCiv.Prototypes.Sim;
using NUnit.Framework;
using UnityEngine;

namespace AquariumCiv.Prototypes.Tests
{
    /// <summary>P3+ layers (story events, yearly snapshots) and the guarantee that they leave P2's world alone.</summary>
    public class StoryTests
    {
        static SimCore Story(int seed = 1234, bool years = false)
        {
            var cfg = new SimConfig { outputPenaltyEnabled = false, messAffectsMood = false, storyEvents = true, recordYears = years };
            return new SimCore(cfg, SimCore.NewWorld(seed, cfg));
        }

        static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }

        /// <summary>P2 is in a live diary: a fingerprint of its world (seed 1234, 48 h, both variants) taken before P3
        /// existed. If this fails, a shared change altered P2's simulation.</summary>
        [TestCase(false, 2846394399u)]
        [TestCase(true, 78215650u)]
        public void P2WorldIsUnchangedByLaterPrototypes(bool penalty, uint expected)
        {
            var cfg = new SimConfig { outputPenaltyEnabled = penalty, messAffectsMood = false };
            var sim = new SimCore(cfg, SimCore.NewWorld(1234, cfg));
            sim.Advance(48);
            var s = sim.State;
            string fp = s.population + "|" + s.month + "|" + s.rng.state + "|" + s.econRng.state + "|" +
                        string.Join(",", s.buildings.Select(b => b.type + b.slot)) + "|" + string.Join("/", s.chronicle.Select(c => c.text)) + "|" +
                        string.Join(",", s.notables.Select(n => n.FullName + n.age.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.AreEqual(expected, Fnv(fp));
            Assert.AreEqual(0, s.years.Count, "P2 records no snapshots");
            Assert.IsTrue(string.IsNullOrEmpty(s.settlementName));
        }

        [Test]
        public void StoriesNeverShiftTheEconomyDice()
        {
            var cfg = new SimConfig { outputPenaltyEnabled = false, messAffectsMood = false };
            var plain = new SimCore(cfg, SimCore.NewWorld(99, cfg));
            var story = Story(99);
            plain.Advance(30); story.Advance(30);
            Assert.AreEqual(plain.State.econRng.state, story.State.econRng.state);
            Assert.Greater(story.State.chronicle.Count, plain.State.chronicle.Count);
        }

        [Test]
        public void StoriesAreDeterministicAndCatchUpEqualsLive()
        {
            var a = Story(); var b = Story();
            a.Advance(40);
            for (int i = 0; i < 40 * 12; i++) b.Advance(1.0 / 12);
            Assert.AreEqual(string.Join("\n", a.State.chronicle.Select(e => e.text)), string.Join("\n", b.State.chronicle.Select(e => e.text)));
        }

        [Test]
        public void AnEightHourAbsenceHasEnoughStoriesForCards()
        {
            // P3 variant A shows 3–7 cards: after a 20-year prehistory, 8 h away should usually produce at least 3.
            int enough = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                var sim = Story(seed);
                sim.Advance(20);
                long before = sim.State.month;
                sim.Advance(8);
                if (sim.State.chronicle.Count(e => e.month > before) >= 3) enough++;
            }
            Assert.GreaterOrEqual(enough, 24, $"only {enough}/30 seeds had 3+ stories in 8 h");
        }

        [Test]
        public void StoriesLinkNamedPeople()
        {
            var sim = Story(7);
            sim.Advance(120);
            var s = sim.State;
            var kinds = s.chronicle.Select(e => e.kind).Distinct().ToList();
            TestContext.WriteLine($"{s.chronicle.Count} entries, {kinds.Count} kinds: {string.Join(", ", kinds)}");
            Assert.GreaterOrEqual(kinds.Count, 15, "a century should show most of the template variety");
            Assert.IsTrue(s.notables.Any(n => n.partnerId >= 0), "someone married");
            Assert.IsFalse(string.IsNullOrEmpty(s.settlementName), "the settlement got a name");
            foreach (var e in s.chronicle.Where(e => e.otherNotableId >= 0))
                Assert.IsNotNull(sim.FindNotable(e.otherNotableId), $"{e.kind} links a real person");
            foreach (var n in s.notables.Where(n => n.partnerId >= 0))
                Assert.AreEqual(n.id, sim.FindNotable(n.partnerId).partnerId, "marriage is mutual");
        }

        [Test]
        public void NoTwoLivingPeopleShareAName()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var sim = Story(seed);
                for (int y = 0; y < 60; y++)
                {
                    sim.Advance(1);
                    var alive = sim.State.notables.Where(n => n.alive).Select(n => n.FullName).ToList();
                    Assert.AreEqual(alive.Count, alive.Distinct().Count(), $"seed {seed} year {sim.State.Year}: {string.Join(", ", alive)}");
                }
            }
        }

        [Test]
        public void YearSnapshotsCoverTheHistory()
        {
            var sim = Story(years: true);
            sim.Advance(100);
            var years = sim.State.years;
            Assert.AreEqual(0, years[0].year);
            Assert.AreEqual(100, years[^1].year);
            Assert.AreEqual(101, years.Count);
            Assert.AreEqual(sim.State.population, years[^1].population);
            for (int i = 1; i < years.Count; i++) Assert.GreaterOrEqual(years[i].buildings, years[i - 1].buildings, "buildings are never lost");
        }

        [Test]
        public void OldSavesWithoutStoryFieldsLoad()
        {
            // A save written before P3: no storyRng, no flags, no snapshot list. The story layer must cope.
            var cfg = new SimConfig { storyEvents = true, recordYears = true };
            var old = SimCore.NewWorld(5, new SimConfig());
            var json = JsonUtility.ToJson(old).Replace("\"storyRng\"", "\"_gone\"");
            var loaded = JsonUtility.FromJson<SimState>(json);
            loaded.storyRng = null; loaded.storyFlags = null; loaded.years = null;
            var sim = new SimCore(cfg, loaded);
            Assert.DoesNotThrow(() => sim.Advance(24));
            Assert.IsNotNull(sim.State.storyRng);
            Assert.Greater(sim.State.years.Count, 0);
        }
    }
}
