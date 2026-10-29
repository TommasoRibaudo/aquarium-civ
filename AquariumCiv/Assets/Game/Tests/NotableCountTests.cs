using System.Linq;
using AquariumCiv.Sim;
using NUnit.Framework;
using static AquariumCiv.Tests.SimCoreTests;

namespace AquariumCiv.Tests
{
    /// <summary>The notable count grows with the era (game definition §4, #26): about 2–4 in the camp, 4–8 in the village,
    /// up to 12 in the town. Births stop at the era's max; below its min one notable a year comes out of the crowd.</summary>
    public class NotableCountTests
    {
        static int Alive(SimCore sim) => sim.State.notables.Count(n => n.alive);

        [Test]
        public void TheBandsMatchTheGameDefinition()
        {
            var c = new SimConfig();
            Assert.AreEqual((2, 4), (c.campNotables.min, c.campNotables.max));
            Assert.AreEqual((4, 8), (c.villageNotables.min, c.villageNotables.max));
            Assert.AreEqual(12, c.townNotables.max);
            Assert.LessOrEqual(c.villageNotables.min, c.campNotables.max, "no era starts far above where the last one ended");
            Assert.LessOrEqual(c.townNotables.min, c.villageNotables.max);
            Assert.That(c.startNotables, Is.InRange(c.campNotables.min, c.campNotables.max), "a new box starts inside the camp's band");
        }

        [Test]
        public void ANewCampStartsInsideItsBand()
        {
            var sim = NewSim(1);
            Assert.AreEqual(sim.Config.startNotables, Alive(sim));
            // Slice.unity still serializes the old startNotables: 5; the camp clamps it to its max.
            Assert.AreEqual(4, Alive(NewSim(1, new SimConfig { startNotables = 5 })));
        }

        /// <summary>A player who is always watching (every era crossed the moment it's ready), checked at every year end.
        /// The count never goes above the era's max. It may dip below the min for a few years: after an era change the
        /// crowd fills the gap one notable a year (at most the jump in the min, 4 years into the town), and a single
        /// death is replaced in the same year; two deaths in one year leave a one-year dip.</summary>
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1234)]
        public void CountsStayInTheEraBand(int seed)
        {
            var sim = NewSim(seed);
            int run = 0, longestDip = 0, yearsBelow = 0;
            for (int year = 1; year <= 120; year++)
            {
                sim.Advance(1);
                var band = sim.NotablesWanted;
                int n = Alive(sim);
                Assert.LessOrEqual(n, band.max, $"seed {seed} year {year} ({sim.State.era}): {n} notables");
                if (n < band.min) { run++; yearsBelow++; longestDip = System.Math.Max(longestDip, run); }
                else run = 0;
                Assert.GreaterOrEqual(n, sim.Config.campNotables.min, $"seed {seed} year {year}: the box ran out of notables");
                if (sim.State.eraReady) sim.AdvanceEra();
            }
            TestContext.WriteLine($"seed {seed}: era {sim.State.era}, {Alive(sim)} notables at year 120, " +
                                  $"{yearsBelow} years below the band, longest dip {longestDip} years");
            Assert.AreEqual(Era.Town, sim.State.era, "the test should cover all three eras");
            Assert.LessOrEqual(longestDip, sim.Config.townNotables.min - sim.Config.villageNotables.min,
                $"seed {seed}: under the band for {longestDip} years in a row");
            Assert.LessOrEqual(yearsBelow, 12, $"seed {seed}: under the band for {yearsBelow} of 120 years");
        }

        [Test]
        public void ANewEraDrawsNewcomers()
        {
            var sim = NewSim(3);
            sim.Advance(5);
            sim.ForceNextEra(); sim.ForceNextEra(); // straight to the town: it wants 8+
            int before = Alive(sim);
            Assume.That(before, Is.LessThan(sim.Config.townNotables.min));
            sim.Advance(1);
            var line = sim.State.chronicle.Last(e => e.kind == "newcomer");
            StringAssert.StartsWith("A newcomer, ", line.text);
            StringAssert.EndsWith("joined the town.", line.text);
            Assert.AreEqual(line.notableId, sim.State.notables.Last().id);
            sim.Advance(sim.Config.townNotables.min - before);
            Assert.GreaterOrEqual(Alive(sim), sim.Config.townNotables.min, "one a year fills the gap");
        }

        [Test]
        public void ADeathBelowTheBandIsReplacedFromTheCrowd()
        {
            var sim = NewSim(5);
            sim.Advance(2);
            while (Alive(sim) >= sim.Config.campNotables.min) sim.DebugKillOldest();
            sim.Advance(1);
            Assert.AreEqual(sim.Config.campNotables.min, Alive(sim));
            Assert.IsTrue(sim.State.chronicle.Any(e => e.kind == "rise"), "someone rose from the crowd");
            Assert.IsFalse(sim.State.chronicle.Any(e => e.kind == "newcomer"), "a camp replaces its dead; it doesn't grow");
        }

        [Test]
        public void BirthsStopAtTheEraMax()
        {
            var sim = NewSim(11);
            foreach (var n in sim.State.notables) { n.age = 25f; n.lifespan = 500f; }
            sim.Advance(30);
            Assert.AreEqual(sim.Config.campNotables.max, Alive(sim), "young parents fill the camp, and no further");
            sim.ForceNextEra();
            foreach (var n in sim.State.notables) n.age = System.Math.Min(n.age, 25f);
            sim.Advance(40);
            Assert.AreEqual(sim.Config.villageNotables.max, Alive(sim), "the village makes room for more children");
        }
    }
}
